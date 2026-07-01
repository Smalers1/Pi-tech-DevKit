using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using Pitech.XR.Core;
using Debug = UnityEngine.Debug;

namespace Pitech.XR.Analytics
{
    // ---------- LabAnalytics: the in-scene analytics recorder (map sec-11) ----------
    // WS B2.1. The ONE opt-in component an author adds to a GRADED lab (next to LabConsole, on the lab
    // ROOT). It hosts the LabRubric (authoring surface), subscribes to the lab's LabEventBus, captures
    // the timed stream between the session.started / session.stopped bracket facts, gates by the in-scene
    // role, and on SessionStop: computes the on-device readout (AnalyticsGradeEngine, no cloud round-
    // trip) AND assembles + submits the ONE self-contained session report to the host outbox.
    //
    // OPT-IN BY DESIGN: existing labs have no LabAnalytics, so nothing reserializes and the recorder is
    // absent (Proof C clean). A graded lab adds this + a SessionStart/SessionStop bracket in its
    // scenario. With no bracket, no capture ever begins (inert).
    //
    // PLACEMENT: the graph's "Add Step Analytic" flow puts this on a dedicated "Analytics" GameObject that is a
    // SIBLING of the LabConsole (next to it, NOT a child of it) and ensures a LabRuntimeContext on the lab ROOT.
    // The shared lab bus (LabRuntimeContext) MUST live on the lab ROOT - the COMMON ANCESTOR of the console and
    // this sibling: the runner resolves it by parent-walk from the LabConsole, and this recorder resolves the SAME
    // one by parent-walk from the sibling up to that root. (A context on the LabConsole GO would NOT be reachable
    // from a sibling, splitting the bus.) ContentDelivery GetOrAdds + stamps the root context at spawn. Co-locating
    // this on the LabConsole GO also works. (See LabRuntimeContext.Find = parent-walk.)
    //
    // ROLES: the role is read from a SessionRoleSelector (Stergios builds the pick UI on that). Default
    // Participant. Participant = full graded report + readout; Professor = presence-only report; Spectator
    // = nothing emitted (map sec-11.5).

    /// <summary>A crossed warning/error band, for the in-scene notification (the toast Stergios wires).</summary>
    [Serializable]
    public sealed class AnalyticsNotification
    {
        public string metricId;
        public string metricLabel;
        public BandSeverity severity;
        public string subjectId;

        public AnalyticsNotification() { }
        public AnalyticsNotification(string metricId, string metricLabel, BandSeverity severity, string subjectId)
        {
            this.metricId = metricId;
            this.metricLabel = metricLabel;
            this.severity = severity;
            this.subjectId = subjectId;
        }
    }

    /// <summary>UnityEvent carrying the computed <see cref="GradeResult"/> (the readout).</summary>
    [Serializable] public sealed class GradeResultEvent : UnityEvent<GradeResult> { }

    /// <summary>UnityEvent carrying an in-scene <see cref="AnalyticsNotification"/>.</summary>
    [Serializable] public sealed class AnalyticsNotificationEvent : UnityEvent<AnalyticsNotification> { }

    [AddComponentMenu("Pi tech/Analytics/Lab Analytics")]
    [DisallowMultipleComponent]
    public sealed class LabAnalytics : MonoBehaviour
    {
        [Header("Analytics config")]
        [Tooltip("The measurement + grading rubric for this lab. Bundled raw into the session report so the cloud re-computes. Empty rubric = nothing to grade (still records presence/bracket).")]
        public LabRubric rubric = new LabRubric();

        [Header("Wiring (optional - auto-resolved if left empty)")]
        [Tooltip("The in-scene role pick. If empty, resolved by parent-walk; if none found, defaults to Participant.")]
        public SessionRoleSelector roleSelector;

        [Tooltip("Optional explicit outbox sink (a component implementing ISessionReportSink). If empty, the host's XRServices-registered ISessionReportSink is used.")]
        public MonoBehaviour reportSink;   // must implement ISessionReportSink

        [Header("Output (wire your UI here)")]
        [Tooltip("Raised at SessionStop with the computed grade - bind your lab-end readout panel here.")]
        public GradeResultEvent onReadout = new GradeResultEvent();

        [Tooltip("Raised live when a warning/error band fires (notifyInScene) - bind your in-scene toast here.")]
        public AnalyticsNotificationEvent onNotification = new AnalyticsNotificationEvent();

#if UNITY_EDITOR
        [Header("Editor testing (never ships)")]
        [Tooltip("EDITOR ONLY: emit the session report even without a host-stamped consent receipt, so you can test " +
                 "the local report + sink in a hand-built scene (which has no ContentDelivery launch to dev-grant " +
                 "consent). Compiled OUT of player builds, so the production fail-closed gate is untouched. The dev " +
                 "report carries no consent receipt. Leave OFF for anything but local testing.")]
        public bool editorEmitWithoutConsent = false;
#endif

        // --- runtime state ---
        LabRuntimeContext _ctx;
        IDisposable _subscription;
        readonly SessionEventStream _stream = new SessionEventStream();
        bool _capturing;
        long _startTick;
        string _currentStepGuid = string.Empty;
        ISessionReportSink _sink;
        bool _submitted;

        void Awake()
        {
            // Resolve the lab context (the bus + the report identity). If none exists (dev/menu lab),
            // create a local one ON THIS object so the recorder is testable; the runner (under the same
            // root) then shares it. ContentDelivery attaches the context on the spawned root before its
            // post-spawn Restart, so the production path resolves that one (with identity stamped).
            _ctx = LabRuntimeContext.Find(this);
            if (_ctx == null)
            {
                _ctx = gameObject.GetComponent<LabRuntimeContext>();
                if (_ctx == null) _ctx = gameObject.AddComponent<LabRuntimeContext>();
            }
        }

        void Start()
        {
            // P7: push the rubric's role capacities into the in-scene role selector so the LOCAL pick guard
            // (SessionRoleSelector.IsSelectable) enforces the per-lab min/max before the learner picks. Done
            // at Start (all Awakes complete, the authored selector exists). Cross-peer headcount (min counts
            // across peers) is inherently multiplayer -> B2.4.
            if (roleSelector == null) roleSelector = SessionRoleSelector.Find(this);
            if (roleSelector != null && rubric != null && rubric.roleCapacities != null)
                roleSelector.SetCapacities(rubric.roleCapacities);
        }

        void OnEnable()
        {
            if (_ctx != null && _ctx.Bus != null && _subscription == null)
                _subscription = _ctx.Bus.Subscribe(OnFact);
        }

        void OnDisable()
        {
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }

            // Graceful teardown mid-bracket (e.g. quit before SessionStop): store as INCOMPLETE, never
            // lost. (True power-loss durability is the host outbox's incremental job - flagged for B2.1.)
            if (_capturing && !_submitted)
            {
                _capturing = false;
                Finalize(false);
            }
        }

        void OnFact(in LabEvent fact)
        {
            string key = fact.Key;

            if (string.Equals(key, ScenarioFactKeys.SessionStarted, StringComparison.Ordinal))
            {
                BeginCapture(fact.Tick);
                return;
            }

            if (!_capturing) return;   // ignore pre-bracket / post-stop facts

            double tMs = ToMs(fact.Tick);

            if (string.Equals(key, ScenarioFactKeys.StepEntered, StringComparison.Ordinal))
            {
                _currentStepGuid = fact.Text ?? string.Empty;
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, tMs, _currentStepGuid));
            }
            else if (string.Equals(key, ScenarioFactKeys.StepCompleted, StringComparison.Ordinal))
            {
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, tMs, fact.Text ?? string.Empty));
            }
            else if (string.Equals(key, ScenarioFactKeys.ItemDropped, StringComparison.Ordinal))
            {
                RecordCount(AnalyticsEventKind.Drop, tMs, fact.Text);
            }
            else if (string.Equals(key, ScenarioFactKeys.InteractionUsed, StringComparison.Ordinal))
            {
                // The recorder is the single classifier (map sec-11.2): in-registry? relevant?
                // ownerStep == current? -> wrong-interaction / order violation / correct (no fact).
                ClassifyUse(tMs, fact.Text);
            }
            else if (string.Equals(key, ScenarioFactKeys.AnalyticsSignal, StringComparison.Ordinal))
            {
                var e = new AnalyticsEvent(AnalyticsEventKind.Signal, tMs, _currentStepGuid, null, fact.Text);
                _stream.Add(e);
                NotifyForSignal(e);
            }
            else if (string.Equals(key, ScenarioFactKeys.SessionStopped, StringComparison.Ordinal))
            {
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, tMs));
                _capturing = false;
                Finalize(true);
            }
            // item.grabbed and any other facts are informational - not captured for scoring.
        }

        void BeginCapture(long startTick)
        {
            _stream.events.Clear();
            _startTick = startTick;
            _capturing = true;
            _submitted = false;
            _currentStepGuid = string.Empty;
            _stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
        }

        void RecordCount(AnalyticsEventKind kind, double tMs, string subjectId)
        {
            var e = new AnalyticsEvent(kind, tMs, _currentStepGuid, subjectId);
            _stream.Add(e);
            NotifyForCount(e);
        }

        /// <summary>Classify a raw subject use into wrong-interaction / order-violation / correct
        /// (map sec-11.2). A subject not in the registry or a distractor -> wrong; a relevant subject
        /// used while its owner step is not the current step -> order; otherwise correct (no fact).</summary>
        void ClassifyUse(double tMs, string subjectId)
        {
            TrackedSubject s = FindSubject(subjectId);
            if (s == null || !s.scenarioRelevant)
            {
                RecordCount(AnalyticsEventKind.WrongInteraction, tMs, subjectId);
                return;
            }
            if (!string.IsNullOrEmpty(s.ownerStepGuid) && s.ownerStepGuid != _currentStepGuid)
            {
                RecordCount(AnalyticsEventKind.OrderViolation, tMs, subjectId);
                return;
            }
            // relevant subject, no owner step or used in its owner step -> correct: nothing scored.
        }

        void Finalize(bool complete)
        {
            if (_submitted) return;
            _submitted = true;

            SessionRole role = ResolveRole();
            ResolveSink();

            string userId = _ctx != null ? _ctx.UserId : string.Empty;

            if (role == SessionRole.Spectator)
                return;   // no analytics emitted (map sec-11.5)

            // P8: consent gate (fail-closed). Consent is recorded upstream (Web Portal / enrolment); the host
            // stamps the receipt onto the LaunchContext at launch and it rides here via LabRuntimeContext. No
            // granted receipt -> the report is NOT emitted (loud, not silent) - high-risk PII never leaves the
            // device without a recorded lawful basis. The on-device readout (local, no PII off-device) still shows.
            bool consentGranted = _ctx != null && _ctx.Consent != null && _ctx.Consent.IsGranted;
#if UNITY_EDITOR
            // EDITOR-ONLY test override (compiled out of player builds): a hand-built test scene has no
            // ContentDelivery launch to dev-grant consent, so the fail-closed gate would block the local report.
            // This lets you eyeball it anyway. The production gate is UNCHANGED - this branch does not exist in a build.
            if (!consentGranted && editorEmitWithoutConsent)
            {
                Debug.LogWarning("[Analytics] EDITOR TEST: emitting the session report WITHOUT a host consent receipt (editorEmitWithoutConsent = true). This override does not exist in player builds.", this);
                consentGranted = true;
            }
#endif

            if (role == SessionRole.Professor)
            {
                // Presence-only: identity + role + bracket, no event stream, no rubric grading.
                onReadout.Invoke(new GradeResult { role = role, isComplete = false });
                if (!consentGranted) { WarnConsentBlocked(role); return; }
                SessionReport presence = BuildReport(role, userId, withGradedPayload: false, complete);
                Submit(presence);
                return;
            }

            // Participant: full graded report + on-device readout.
            GradeResult grade = AnalyticsGradeEngine.Compute(rubric, _stream, role);
            // An unfinished bracket is never "complete" regardless of the engine's objective math.
            if (!complete) grade.isComplete = false;
            onReadout.Invoke(grade);

            // P6: only the driver/authority submits the graded report. A follower's frontier-mirrored step
            // facts have unreliable durations, so its grade would be wrong - skip its SUBMISSION (the driver
            // ships the authoritative one); the local readout above still shows the follower its own view.
            // Inert single-player (no flow store / IsDriver true). Shared sessionId + presence-merge across
            // peers is the post-B2 MP turn-on.
            if (_ctx != null && !_ctx.IsDriver) { WarnFollowerSkipped(role); return; }

            if (!consentGranted) { WarnConsentBlocked(role); return; }
            SessionReport report = BuildReport(role, userId, withGradedPayload: true, complete);
            Submit(report);
        }

        // P8: fail-closed consent block - loud, not silent (the no-silent-bail rule). The readout was shown
        // locally; no report is shipped because no granted consent receipt is present for this session.
        void WarnConsentBlocked(SessionRole role)
        {
            Debug.LogWarning($"[Analytics] Consent not granted (no granted consent receipt on the launch context): the {role} session report was computed locally but NOT emitted (fail-closed). The host must stamp LaunchContext.consent from the tenant's recorded consent before reports can ship.", this);
        }

        // P6: a follower correctly defers to the driver (expected MP behaviour, not a misconfig) -> Log, not
        // Warning. Inert single-player (always the driver).
        void WarnFollowerSkipped(SessionRole role)
        {
            Debug.Log($"[Analytics] This peer is a follower (not the run authority); the {role} graded report was computed locally but NOT submitted - the driver ships the authoritative session report.", this);
        }

        SessionReport BuildReport(SessionRole role, string userId, bool withGradedPayload, bool complete)
        {
            var report = new SessionReport
            {
                tenantId = _ctx != null ? _ctx.TenantId : string.Empty,
                sessionId = _ctx != null ? _ctx.SessionId : string.Empty,
                labId = _ctx != null ? _ctx.LabId : string.Empty,
                labVersion = _ctx != null ? _ctx.LabVersion : string.Empty,
                isComplete = complete,
                consent = _ctx != null ? _ctx.Consent : null   // P8: lawful-basis audit trail (granted on every emitted report)
            };
            report.users.Add(new SessionReportUser(userId, role));
            if (withGradedPayload)
            {
                // Defensive copy: the recorder reuses _stream across re-runs (BeginCapture clears it), so
                // the report must own its own list rather than alias the live buffer.
                report.events = new List<AnalyticsEvent>(_stream.events);
                report.rubric = rubric;
            }
            return report;
        }

        void Submit(SessionReport report)
        {
            string json = SessionReportJson.Serialize(report);
            if (_sink != null)
            {
                _sink.Submit(report, json);
            }
            else
            {
                // No outbox wired: loud, not silent (per the no-silent-bail rule). The readout still shows;
                // the report is not persisted until the host registers an ISessionReportSink.
                Debug.LogWarning("[Analytics] No ISessionReportSink registered (XRServices or the reportSink field). The session report was computed but not persisted/shipped.", this);
            }
        }

        // --- in-scene notifications (the warning/error nudge; map sec-11.x) ---

        void NotifyForCount(AnalyticsEvent e)
        {
            BandSeverity sev = e.kind == AnalyticsEventKind.OrderViolation ? BandSeverity.Warning
                : e.kind == AnalyticsEventKind.Drop ? (IsRelevant(e.subjectId) ? BandSeverity.Error : BandSeverity.Warning)
                : /* WrongInteraction */ (IsKnownDistractor(e.subjectId) ? BandSeverity.Warning : BandSeverity.Error);

            AnalyticsMetric m = FirstNotifyingMetricOfKind(KindIdFor(e.kind), sev);
            if (m != null)
                onNotification.Invoke(new AnalyticsNotification(m.id, m.label, sev, e.subjectId));
        }

        void NotifyForSignal(AnalyticsEvent e)
        {
            AnalyticsMetric m = FindMetricById(e.signalId);
            if (m == null) return;
            if (HasNotifyingBand(m, BandSeverity.Error))
                onNotification.Invoke(new AnalyticsNotification(m.id, m.label, BandSeverity.Error, e.subjectId));
        }

        // --- helpers ---

        SessionRole ResolveRole()
        {
            if (roleSelector == null) roleSelector = SessionRoleSelector.Find(this);
            return roleSelector != null ? roleSelector.CurrentRole : SessionRole.Participant;
        }

        void ResolveSink()
        {
            if (_sink != null) return;
            if (reportSink is ISessionReportSink s) { _sink = s; return; }
            if (XRServices.TryGet(out ISessionReportSink svc)) _sink = svc;
        }

        double ToMs(long tick)
        {
            long freq = Stopwatch.Frequency;
            if (freq <= 0) return 0.0;
            return (tick - _startTick) * 1000.0 / freq;
        }

        bool IsRelevant(string subjectId)
        {
            TrackedSubject s = FindSubject(subjectId);
            return s != null && s.scenarioRelevant;
        }

        bool IsKnownDistractor(string subjectId)
        {
            TrackedSubject s = FindSubject(subjectId);
            return s != null && !s.scenarioRelevant;
        }

        TrackedSubject FindSubject(string id)
        {
            if (rubric == null || rubric.subjects == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < rubric.subjects.Count; i++)
            {
                TrackedSubject s = rubric.subjects[i];
                if (s != null && s.id == id) return s;
            }
            return null;
        }

        static string KindIdFor(AnalyticsEventKind kind)
        {
            switch (kind)
            {
                case AnalyticsEventKind.Drop: return DropMetric.KindId;
                case AnalyticsEventKind.WrongInteraction: return WrongInteractionMetric.KindId;
                case AnalyticsEventKind.OrderViolation: return OrderMetric.KindId;
                default: return null;
            }
        }

        AnalyticsMetric FirstNotifyingMetricOfKind(string kindId, BandSeverity sev)
        {
            if (kindId == null || rubric == null || rubric.analytics == null) return null;
            for (int i = 0; i < rubric.analytics.Count; i++)
            {
                Analytic a = rubric.analytics[i];
                if (a == null || a.metrics == null) continue;
                for (int j = 0; j < a.metrics.Count; j++)
                {
                    AnalyticsMetric m = a.metrics[j];
                    if (m != null && m.Kind == kindId && HasNotifyingBand(m, sev)) return m;
                }
            }
            return null;
        }

        AnalyticsMetric FindMetricById(string id)
        {
            if (string.IsNullOrEmpty(id) || rubric == null || rubric.analytics == null) return null;
            for (int i = 0; i < rubric.analytics.Count; i++)
            {
                Analytic a = rubric.analytics[i];
                if (a == null || a.metrics == null) continue;
                for (int j = 0; j < a.metrics.Count; j++)
                {
                    AnalyticsMetric m = a.metrics[j];
                    if (m != null && m.id == id) return m;
                }
            }
            return null;
        }

        static bool HasNotifyingBand(AnalyticsMetric m, BandSeverity sev)
        {
            if (m.bands == null) return false;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b != null && b.name == sev && b.notifyInScene && b.penaltyWeight > 0f) return true;
            }
            return false;
        }
    }
}
