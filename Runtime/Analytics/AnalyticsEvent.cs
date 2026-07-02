using System;
using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The captured timed event stream (map sec-11.4 / sec-11.5) ----------
    // WS B2.1. The recorder (LabAnalytics) turns LabEventBus facts into a portable, ordered list of
    // AnalyticsEvents between the session.started and session.stopped bracket facts. This list is BOTH
    // the reducer input (events -> grade) AND the raw stream bundled into the session report (so the
    // cloud re-computes from the same data - the DevKit reducer is canonical, the portal is a mirror).
    //
    // PORTABILITY: time is stored as <see cref="AnalyticsEvent.tMs"/> - milliseconds since the bracket
    // start - NOT a raw Stopwatch tick. Stopwatch.Frequency differs per machine, so ticks are not
    // portable; the recorder converts once at capture using its own Frequency. Everything downstream
    // (the reducer, the report, the cloud mirror) works in ms.

    /// <summary>What a captured <see cref="AnalyticsEvent"/> is.</summary>
    public enum AnalyticsEventKind
    {
        /// <summary>The graded bracket opened (tMs = 0).</summary>
        SessionStarted = 0,
        /// <summary>The graded bracket closed.</summary>
        SessionStopped = 1,
        /// <summary>A step was entered (<see cref="AnalyticsEvent.stepGuid"/> set).</summary>
        StepEntered = 2,
        /// <summary>A step was completed (<see cref="AnalyticsEvent.stepGuid"/> set).</summary>
        StepCompleted = 3,
        /// <summary>A tracked subject was dropped (<see cref="AnalyticsEvent.subjectId"/> set). Feeds DropMetric.</summary>
        Drop = 4,
        /// <summary>An interaction with a wrong target. Feeds WrongInteractionMetric.</summary>
        WrongInteraction = 5,
        /// <summary>An out-of-order interaction with a relevant subject. Feeds OrderMetric.</summary>
        OrderViolation = 6,
        /// <summary>An authored analytics signal (<see cref="AnalyticsEvent.signalId"/> set).</summary>
        Signal = 7,
        /// <summary>The scenario was FAILED by a critical gate (v3): grade 0 + failed, terminal. The recorder
        /// emits this the moment a scenario-fail gate trips so BOTH reducers derive the fail from the raw stream
        /// even if the failing step never completes (e.g. the learner restarts). <see cref="AnalyticsEvent.signalId"/>
        /// carries the cause metric/penalty id.</summary>
        ScenarioFailed = 8
    }

    /// <summary>
    /// One captured fact in the session's timed stream. Plain serializable data (no Unity refs) so it
    /// round-trips into the session report and the cloud mirror.
    /// </summary>
    [Serializable]
    public sealed class AnalyticsEvent
    {
        /// <summary>What happened.</summary>
        public AnalyticsEventKind kind;

        /// <summary>Milliseconds since the bracket start (portable; NOT a raw Stopwatch tick).</summary>
        public double tMs;

        /// <summary>The step active when this happened (entered/completed: the step itself; interactions:
        /// the step current at the time, for step-scoped attribution). Empty when not applicable.</summary>
        public string stepGuid = string.Empty;

        /// <summary>The tracked subject involved (drop / wrong / order). Empty when not applicable.</summary>
        public string subjectId = string.Empty;

        /// <summary>The authored signal id (Signal kind) or the cause id (ScenarioFailed kind), matched to a
        /// metric/penalty by id. Empty otherwise.</summary>
        public string signalId = string.Empty;

        /// <summary>The user who caused this (multi-user attribution). Empty in single-user labs / when unknown.
        /// Additive in v3 so gates can later score Participant-caused events only (post-B2 MP turn-on).</summary>
        public string userId = string.Empty;

        public AnalyticsEvent() { }

        public AnalyticsEvent(AnalyticsEventKind kind, double tMs, string stepGuid = null,
            string subjectId = null, string signalId = null, string userId = null)
        {
            this.kind = kind;
            this.tMs = tMs;
            this.stepGuid = stepGuid ?? string.Empty;
            this.subjectId = subjectId ?? string.Empty;
            this.signalId = signalId ?? string.Empty;
            this.userId = userId ?? string.Empty;
        }
    }

    /// <summary>
    /// The ordered captured stream for one attempt's graded bracket. <see cref="DurationMs"/> spans
    /// session.started -> session.stopped. <see cref="IsComplete"/> is false for an unfinished session
    /// (no stop) - the report stores it as "incomplete", never "passed" (map sec-11.5).
    /// </summary>
    [Serializable]
    public sealed class SessionEventStream
    {
        /// <summary>The events, in capture order (first is SessionStarted at tMs 0).</summary>
        public List<AnalyticsEvent> events = new List<AnalyticsEvent>();

        /// <summary>True once a SessionStopped event has been recorded.</summary>
        public bool IsComplete
        {
            get
            {
                for (int i = events.Count - 1; i >= 0; i--)
                    if (events[i].kind == AnalyticsEventKind.SessionStopped) return true;
                return false;
            }
        }

        /// <summary>Bracket duration in ms (last event time minus 0). 0 if empty.</summary>
        public double DurationMs => events.Count > 0 ? events[events.Count - 1].tMs : 0.0;

        public void Add(AnalyticsEvent e)
        {
            if (e != null) events.Add(e);
        }
    }
}
