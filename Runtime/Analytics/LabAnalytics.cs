using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Pitech.XR.Core;
using Debug = UnityEngine.Debug;

namespace Pitech.XR.Analytics
{
    // ---------- LabAnalytics: the in-scene analytics recorder (map sec-11; v3 model 2026-07-02) ----------
    // The ONE opt-in component an author adds to a GRADED lab. It hosts the LabConfig, subscribes to the lab's
    // LabEventBus, captures the timed stream between session.started / session.stopped, gates by the in-scene
    // role, and at the session end computes the on-device readout (AnalyticsGradeEngine) + submits the ONE
    // self-contained session report to the host outbox.
    //
    // v3 CRITICAL GATES: the recorder detects a scenario-fail LIVE (a critical step metric on a
    // failsScenario step, or a failScenario penalty, tripping) and (1) emits a scenario.failed fact into the
    // stream so both reducers derive the fail from raw data, (2) raises onScenarioFailed so the author can show
    // a Restart / Continue dialog. The scenario KEEPS RUNNING (grading-terminal, not run-terminal). A restart
    // (a fresh session.started while still capturing) FINALIZES + SHIPS the failed attempt before clearing.
    //
    // PLACEMENT / ROLES / CONSENT: unchanged from B2.1 (see LabRuntimeContext + SessionRoleSelector +
    // ConsentReceipt). Participant = graded report + readout; Professor = presence only; Spectator = nothing.

    /// <summary>The three in-scene notification variants, in ascending gravity. Drives which toast card the
    /// author's <see cref="SessionNotificationView"/> shows. NOT hardcoded per event: the recorder derives it
    /// (<see cref="AnalyticsSeverity"/> band severity + whether a critical gate / scenario-fail fired).</summary>
    public enum NotificationLevel
    {
        /// <summary>A soft nudge (a warning-severity band / distractor slip). No gate.</summary>
        Warning = 0,
        /// <summary>An error-severity occurrence that costs grade points but isn't a gate.</summary>
        Error = 1,
        /// <summary>A critical gate fired (a critical metric fails the step, or a fail-scenario rule tripped).</summary>
        Critical = 2
    }

    /// <summary>A live in-scene notification (the toast the author wires). <see cref="level"/> selects the variant;
    /// <see cref="metricLabel"/>/<see cref="subjectId"/> are the dynamic detail the toast renders.</summary>
    [Serializable]
    public sealed class AnalyticsNotification
    {
        public NotificationLevel level;
        public string metricId;
        public string metricLabel;
        public BandSeverity severity;
        public string subjectId;

        public AnalyticsNotification() { }
        public AnalyticsNotification(NotificationLevel level, string metricId, string metricLabel, BandSeverity severity, string subjectId)
        {
            this.level = level;
            this.metricId = metricId;
            this.metricLabel = metricLabel;
            this.severity = severity;
            this.subjectId = subjectId;
        }
    }

    /// <summary>A live scenario-fail, for the Restart / Continue dialog the author wires to onScenarioFailed.</summary>
    [Serializable]
    public sealed class AnalyticsFailure
    {
        public string causeId;
        public string causeLabel;

        public AnalyticsFailure() { }
        public AnalyticsFailure(string causeId, string causeLabel)
        {
            this.causeId = causeId;
            this.causeLabel = causeLabel;
        }
    }

    /// <summary>UnityEvent carrying the computed <see cref="GradeResult"/> (the readout).</summary>
    [Serializable] public sealed class GradeResultEvent : UnityEvent<GradeResult> { }

    /// <summary>UnityEvent carrying an in-scene <see cref="AnalyticsNotification"/>.</summary>
    [Serializable] public sealed class AnalyticsNotificationEvent : UnityEvent<AnalyticsNotification> { }

    /// <summary>UnityEvent carrying a live <see cref="AnalyticsFailure"/> (scenario-fail).</summary>
    [Serializable] public sealed class AnalyticsFailureEvent : UnityEvent<AnalyticsFailure> { }

    [AddComponentMenu("Pi tech/Analytics/Lab Analytics")]
    [DisallowMultipleComponent]
    public sealed class LabAnalytics : MonoBehaviour
    {
        [Header("Analytics config")]
        [Tooltip("The measurement + grading config for this lab. Bundled raw into the session report so the cloud re-computes. Empty config = base 100 (nothing to grade).")]
        [FormerlySerializedAs("rubric")]
        public LabConfig config = new LabConfig();

        [Header("Wiring (optional - auto-resolved if left empty)")]
        [Tooltip("The in-scene role pick. If empty, resolved by parent-walk; if none found, defaults to Participant.")]
        public SessionRoleSelector roleSelector;

        [Tooltip("Optional explicit outbox sink (a component implementing ISessionReportSink). If empty, the host's XRServices-registered ISessionReportSink is used.")]
        public MonoBehaviour reportSink;   // must implement ISessionReportSink

        [Header("Output (wire your UI here)")]
        [Tooltip("Raised at session end with the computed grade - bind your lab-end readout panel here.")]
        public GradeResultEvent onReadout = new GradeResultEvent();

        [Tooltip("Raised live when a warning/error penalty or gate fires (notifyInScene) - bind your in-scene toast here.")]
        public AnalyticsNotificationEvent onNotification = new AnalyticsNotificationEvent();

        [Tooltip("Raised the moment a CRITICAL gate fails the scenario (grade 0). Bind a 'You failed - Restart / Continue' dialog here. The scenario keeps running; grade stays 0 unless the learner restarts.")]
        public AnalyticsFailureEvent onScenarioFailed = new AnalyticsFailureEvent();

#if UNITY_EDITOR
        [Header("Editor testing (never ships)")]
        [Tooltip("EDITOR ONLY: emit the session report even without a host-stamped consent receipt, so you can test " +
                 "the local report + sink in a hand-built scene. Compiled OUT of player builds. Leave OFF except for local testing.")]
        public bool editorEmitWithoutConsent = false;
#endif

        // --- runtime state ---
        LabRuntimeContext _ctx;
        IDisposable _subscription;
        readonly SessionEventStream _stream = new SessionEventStream();
        bool _capturing;
        bool _scenarioFailed;
        long _startTick;
        string _currentStepGuid = string.Empty;
        long _currentStepStartTick;
        // Live duration nudge: highest ceiling-band threshold already notified per metric/penalty id, so a
        // crossing fires ONCE per escalation instead of every frame it stays crossed.
        readonly Dictionary<string, float> _durationNotified = new Dictionary<string, float>();
        ISessionReportSink _sink;
        bool _submitted;

        void Awake()
        {
            _ctx = LabRuntimeContext.Find(this);
            if (_ctx == null)
            {
                _ctx = gameObject.GetComponent<LabRuntimeContext>();
                if (_ctx == null) _ctx = gameObject.AddComponent<LabRuntimeContext>();
            }
        }

        void Start()
        {
            if (roleSelector == null) roleSelector = SessionRoleSelector.Find(this);
            // Capacities are authored on the SessionRoleSelector (the single surface). Mirror them into the config
            // so the report still carries them (SessionReportJson reads config.roleCapacities).
            if (roleSelector != null && config != null && roleSelector.Capacities != null)
                config.roleCapacities = roleSelector.Capacities;
        }

        void OnEnable()
        {
            if (_ctx != null && _ctx.Bus != null && _subscription == null)
                _subscription = _ctx.Bus.Subscribe(OnFact);
        }

        void OnDisable()
        {
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }
            // Graceful teardown mid-bracket (quit before SessionStop, or after a fail): ship as INCOMPLETE, never lost.
            if (_capturing && !_submitted)
            {
                _capturing = false;
                Finalize(false);
            }
        }

        void Update()
        {
            if (_capturing) CheckDurationLive();
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
                _currentStepStartTick = fact.Tick;
                ResetStepDurationNotified(_currentStepGuid);
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, tMs, _currentStepGuid, userId: Uid()));
            }
            else if (string.Equals(key, ScenarioFactKeys.StepCompleted, StringComparison.Ordinal))
            {
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, tMs, fact.Text ?? string.Empty, userId: Uid()));
            }
            else if (string.Equals(key, ScenarioFactKeys.ItemDropped, StringComparison.Ordinal))
            {
                RecordCount(AnalyticsEventKind.Drop, tMs, fact.Text);
            }
            else if (string.Equals(key, ScenarioFactKeys.InteractionUsed, StringComparison.Ordinal))
            {
                ClassifyUse(tMs, fact.Text);
            }
            else if (string.Equals(key, ScenarioFactKeys.AnalyticsSignal, StringComparison.Ordinal))
            {
                var e = new AnalyticsEvent(AnalyticsEventKind.Signal, tMs, _currentStepGuid, null, fact.Text, Uid());
                _stream.Add(e);
                NotifyAndGate(e);
            }
            else if (string.Equals(key, ScenarioFactKeys.SessionStopped, StringComparison.Ordinal))
            {
                _stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, tMs, userId: Uid()));
                _capturing = false;
                Finalize(true);
            }
            // item.grabbed and any other facts are informational - not captured for scoring.
        }

        void BeginCapture(long startTick)
        {
            // Restart mid-bracket (e.g. after a scenario-fail): FINALIZE + SHIP the prior unsubmitted attempt
            // BEFORE clearing the stream, so the failed attempt still leaves the device (v3 requirement).
            if (_capturing && !_submitted)
            {
                _capturing = false;
                Finalize(false);
            }

            _stream.events.Clear();
            _startTick = startTick;
            _capturing = true;
            _submitted = false;
            _scenarioFailed = false;
            _currentStepGuid = string.Empty;
            _currentStepStartTick = startTick;
            _durationNotified.Clear();
            _stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0, userId: Uid()));
        }

        void RecordCount(AnalyticsEventKind kind, double tMs, string subjectId)
        {
            var e = new AnalyticsEvent(kind, tMs, _currentStepGuid, subjectId, null, Uid());
            _stream.Add(e);
            NotifyAndGate(e);
        }

        /// <summary>Classify a raw subject use into wrong-interaction / order-violation / correct (map sec-11.2).</summary>
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
            // relevant subject used in its owner step -> correct: nothing scored.
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

            bool consentGranted = _ctx != null && _ctx.Consent != null && _ctx.Consent.IsGranted;
#if UNITY_EDITOR
            if (!consentGranted && editorEmitWithoutConsent)
            {
                Debug.LogWarning("[Analytics] EDITOR TEST: emitting the session report WITHOUT a host consent receipt (editorEmitWithoutConsent = true). This override does not exist in player builds.", this);
                consentGranted = true;
            }
#endif

            if (role == SessionRole.Professor)
            {
                onReadout.Invoke(new GradeResult { role = role, isComplete = false });
                if (!consentGranted) { WarnConsentBlocked(role); return; }
                SessionReport presence = BuildReport(role, userId, withGradedPayload: false, complete);
                Submit(presence);
                return;
            }

            // Participant: full graded report + on-device readout.
            GradeResult grade = AnalyticsGradeEngine.Compute(config, _stream, role);
            // An unfinished bracket is never "complete" - UNLESS the scenario was failed (a fail is a complete
            // outcome, engine already sets isComplete=true for that case).
            if (!complete && !grade.failed) grade.isComplete = false;
            onReadout.Invoke(grade);

            if (_ctx != null && !_ctx.IsDriver) { WarnFollowerSkipped(role); return; }
            if (!consentGranted) { WarnConsentBlocked(role); return; }

            SessionReport report = BuildReport(role, userId, withGradedPayload: true, complete);
            Submit(report);
        }

        void WarnConsentBlocked(SessionRole role)
        {
            Debug.LogWarning($"[Analytics] Consent not granted: the {role} session report was computed locally but NOT emitted (fail-closed). The host must stamp LaunchContext.consent before reports can ship.", this);
        }

        void WarnFollowerSkipped(SessionRole role)
        {
            Debug.Log($"[Analytics] This peer is a follower; the {role} graded report was computed locally but NOT submitted - the driver ships the authoritative session report.", this);
        }

        SessionReport BuildReport(SessionRole role, string userId, bool withGradedPayload, bool complete)
        {
            var report = new SessionReport
            {
                tenantId = _ctx != null ? _ctx.TenantId : string.Empty,
                sessionId = _ctx != null ? _ctx.SessionId : string.Empty,
                labId = _ctx != null ? _ctx.LabId : string.Empty,
                labVersion = _ctx != null ? _ctx.LabVersion : string.Empty,
                isComplete = complete,   // RAW bracket fact - NOT the grade verdict (the cloud derives failed itself)
                consent = _ctx != null ? _ctx.Consent : null
            };
            report.users.Add(new SessionReportUser(userId, role));
            if (withGradedPayload)
            {
                report.events = new List<AnalyticsEvent>(_stream.events);
                report.config = config;
            }
            return report;
        }

        void Submit(SessionReport report)
        {
            string json = SessionReportJson.Serialize(report);
            if (_sink != null) _sink.Submit(report, json);
            else Debug.LogWarning("[Analytics] No ISessionReportSink registered. The session report was computed but not persisted/shipped.", this);
        }

        // ---- live notifications + gates ----

        /// <summary>On a captured count/signal event: fire the toast for a matching notifying penalty or critical
        /// step gate, and trip a scenario-fail (emit the fact + raise onScenarioFailed) when a failScenario penalty
        /// or a failsScenario step's critical gate hits an error-severity occurrence.</summary>
        void NotifyAndGate(AnalyticsEvent e)
        {
            BandSeverity sev = AnalyticsSeverity.Derive(e.kind, IsRelevant(e.subjectId), IsKnownDistractor(e.subjectId));

            // Scene penalties (run-wide).
            if (config != null && config.penalties != null)
            {
                for (int i = 0; i < config.penalties.Count; i++)
                {
                    PenaltyRule p = config.penalties[i];
                    if (p == null || !PenaltyMatches(p, e)) continue;
                    bool penFails = p.failScenario && sev == BandSeverity.Error;
                    if (p.notifyInScene && sev != BandSeverity.None)
                        onNotification.Invoke(new AnalyticsNotification(LevelFor(sev, penFails), p.id, p.label, sev, e.subjectId));
                    if (penFails)
                        EmitScenarioFail(p.id, string.IsNullOrEmpty(p.label) ? "Critical penalty" : p.label, e.tMs);
                }
            }

            // Critical gate on the CURRENT step.
            StepAnalytic sa = FindCurrentStepAnalytic();
            if (sa != null && sev == BandSeverity.Error)
            {
                AnalyticsMetric gate = FindCriticalMetricForEvent(sa, e);
                if (gate != null)
                {
                    // A critical gate is always the top variant (it zeroes the step, maybe the scenario).
                    onNotification.Invoke(new AnalyticsNotification(NotificationLevel.Critical, gate.id, gate.label, BandSeverity.Error, e.subjectId));
                    if (sa.failsScenario)
                        EmitScenarioFail(gate.id, GateLabel(gate, sa), e.tMs);
                }
            }
        }

        /// <summary>Per-frame duration watch: step-duration notifying bands (current step) + total-duration penalty
        /// tiers, plus their gates (critical step-duration on a failsScenario step; a failScenario duration penalty).</summary>
        void CheckDurationLive()
        {
            if (config == null) return;
            long freq = Stopwatch.Frequency;
            if (freq <= 0) return;
            long now = Stopwatch.GetTimestamp();
            float totalSec = (float)((now - _startTick) / (double)freq);
            bool haveStep = !string.IsNullOrEmpty(_currentStepGuid);
            float stepSec = haveStep ? (float)((now - _currentStepStartTick) / (double)freq) : 0f;
            double nowMs = ToMs(now);

            // Step-duration metrics on the current step.
            StepAnalytic sa = haveStep ? FindCurrentStepAnalytic() : null;
            if (sa != null && sa.metrics != null)
            {
                for (int j = 0; j < sa.metrics.Count; j++)
                {
                    AnalyticsMetric m = sa.metrics[j];
                    if (m == null || m.Kind != StepDurationMetric.KindId) continue;
                    NotifyDurationBands(m, stepSec);
                    if (m.critical && sa.failsScenario && AnalyticsSeverity.DurationGateTrips(m, stepSec))
                        EmitScenarioFail(m.id, GateLabel(m, sa), nowMs);
                }
            }

            // Total-duration penalties (run-wide).
            if (config.penalties != null)
            {
                for (int i = 0; i < config.penalties.Count; i++)
                {
                    PenaltyRule p = config.penalties[i];
                    if (p == null || p.kind != PenaltyKind.TotalDuration || p.tiers == null) continue;
                    NotifyPenaltyTiers(p, totalSec);
                    if (p.failScenario && HighestCrossedTier(p, totalSec) != null)
                        EmitScenarioFail(p.id, string.IsNullOrEmpty(p.label) ? "Over time" : p.label, nowMs);
                }
            }
        }

        // Map a band severity (+ whether a critical gate/scenario-fail is in play) to a toast variant.
        static NotificationLevel LevelFor(BandSeverity sev, bool critical)
        {
            if (critical) return NotificationLevel.Critical;
            return sev == BandSeverity.Error ? NotificationLevel.Error : NotificationLevel.Warning;
        }

        void EmitScenarioFail(string causeId, string causeLabel, double tMs)
        {
            if (_scenarioFailed) return;   // once per bracket
            _scenarioFailed = true;
            _stream.Add(new AnalyticsEvent(AnalyticsEventKind.ScenarioFailed, tMs, _currentStepGuid, null, causeId ?? string.Empty, Uid()));
            onScenarioFailed.Invoke(new AnalyticsFailure(causeId, causeLabel));
        }

        // Fire a duration metric's notifying bands once per escalation (the step-duration live nudge).
        void NotifyDurationBands(AnalyticsMetric m, float elapsedSeconds)
        {
            if (m.bands == null) return;
            float crossed = -1f; BandSeverity sev = BandSeverity.None;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b == null || !b.notifyInScene || b.penaltyWeight <= 0f || b.threshold <= 0f) continue;
                if (elapsedSeconds >= b.threshold && b.threshold > crossed) { crossed = b.threshold; sev = b.name; }
            }
            if (crossed < 0f) return;
            if (_durationNotified.TryGetValue(m.id, out float last) && crossed <= last) return;
            _durationNotified[m.id] = crossed;
            // A critical duration metric on this step reaching its Error band is a gate about to fire -> Critical.
            onNotification.Invoke(new AnalyticsNotification(LevelFor(sev, m.critical && sev == BandSeverity.Error), m.id, m.label, sev, null));
        }

        // Fire a total-duration penalty's crossed tier once per escalation (in-scene "over time" nudge).
        void NotifyPenaltyTiers(PenaltyRule p, float elapsedSeconds)
        {
            if (!p.notifyInScene) return;
            PenaltyTier t = HighestCrossedTier(p, elapsedSeconds);
            if (t == null) return;
            string key = "pen_" + p.id;
            if (_durationNotified.TryGetValue(key, out float last) && t.overSeconds <= last) return;
            _durationNotified[key] = t.overSeconds;
            // A fail-scenario total-duration penalty crossing a tier is about to zero the run -> Critical.
            onNotification.Invoke(new AnalyticsNotification(LevelFor(BandSeverity.Warning, p.failScenario), p.id, p.label, BandSeverity.Warning, null));
        }

        static PenaltyTier HighestCrossedTier(PenaltyRule p, float seconds)
        {
            PenaltyTier best = null; float bestOver = -1f;
            for (int i = 0; i < p.tiers.Count; i++)
            {
                PenaltyTier t = p.tiers[i];
                if (t == null || t.overSeconds <= 0f) continue;
                if (seconds >= t.overSeconds && t.overSeconds > bestOver) { bestOver = t.overSeconds; best = t; }
            }
            return best;
        }

        void ResetStepDurationNotified(string stepGuid)
        {
            if (config == null || config.analytics == null || string.IsNullOrEmpty(stepGuid) || _durationNotified.Count == 0) return;
            for (int i = 0; i < config.analytics.Count; i++)
            {
                if (!(config.analytics[i] is StepAnalytic sa) || sa.stepGuid != stepGuid || sa.metrics == null) continue;
                for (int j = 0; j < sa.metrics.Count; j++)
                {
                    AnalyticsMetric m = sa.metrics[j];
                    if (m != null && m.Kind == StepDurationMetric.KindId) _durationNotified.Remove(m.id);
                }
            }
        }

        // ---- helpers ----

        string Uid() => _ctx != null ? _ctx.UserId : string.Empty;

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

        bool IsRelevant(string subjectId) { TrackedSubject s = FindSubject(subjectId); return s != null && s.scenarioRelevant; }
        bool IsKnownDistractor(string subjectId) { TrackedSubject s = FindSubject(subjectId); return s != null && !s.scenarioRelevant; }

        TrackedSubject FindSubject(string id)
        {
            if (config == null || config.subjects == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < config.subjects.Count; i++)
            {
                TrackedSubject s = config.subjects[i];
                if (s != null && s.id == id) return s;
            }
            return null;
        }

        StepAnalytic FindCurrentStepAnalytic()
        {
            if (config == null || config.analytics == null || string.IsNullOrEmpty(_currentStepGuid)) return null;
            for (int i = 0; i < config.analytics.Count; i++)
                if (config.analytics[i] is StepAnalytic sa && sa.stepGuid == _currentStepGuid) return sa;
            return null;
        }

        static bool PenaltyMatches(PenaltyRule p, AnalyticsEvent e)
        {
            if (!p.TryEventKind(out AnalyticsEventKind ek)) return false;   // TotalDuration handled elsewhere
            if (p.kind == PenaltyKind.Signal) return e.kind == AnalyticsEventKind.Signal && e.signalId == p.signalId;
            return e.kind == ek;
        }

        static AnalyticsMetric FindCriticalMetricForEvent(StepAnalytic sa, AnalyticsEvent e)
        {
            if (sa.metrics == null) return null;
            for (int j = 0; j < sa.metrics.Count; j++)
            {
                AnalyticsMetric m = sa.metrics[j];
                if (m == null || !m.critical) continue;
                switch (e.kind)
                {
                    case AnalyticsEventKind.Drop: if (m is DropMetric) return m; break;
                    case AnalyticsEventKind.WrongInteraction: if (m is WrongInteractionMetric) return m; break;
                    case AnalyticsEventKind.OrderViolation: if (m is OrderMetric) return m; break;
                    case AnalyticsEventKind.Signal: if (m is SignalMetric && m.id == e.signalId) return m; break;
                }
            }
            return null;
        }

        static string GateLabel(AnalyticsMetric m, StepAnalytic sa)
        {
            string ml = string.IsNullOrEmpty(m.label) ? "Critical" : m.label;
            string sl = string.IsNullOrEmpty(sa.label) ? "step" : sa.label;
            return ml + " (" + sl + ")";
        }
    }
}
