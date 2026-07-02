using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The analytics equivalence golden fixture (v3 model, 2026-07-02) ----------
    // ONE canned (config + events) -> expected grade, shipped in the runtime assembly so BOTH reducers run it:
    // the DevKit EditMode test AND the Web Portal mirror (B2.3) must produce the IDENTICAL grade. Keep it
    // deterministic and hand-verifiable - the arithmetic is in the doc comment below.
    //
    // Scenario modelled (exercises base + penalty + bonus in one run):
    //   Step 1 (weight 1): StepDuration - 7s -> Warning band (>=5s) -> penalty 0.5 -> score 0.5.
    //   Step 2 (weight 1): Drop metric - one RELEVANT drop (scalpel) -> Error -> band penalty 1.0 -> score 0.0.
    //   BASE   = (1*0.5 + 1*0.0) / (1+1)                = 0.25   (25 grade points)
    //   PENALTY: a scene Drop penalty counts the SAME relevant drop -> Error -> pointsPerError 5 (under cap 20).
    //   GOAL   : TotalTimeUnder 30s; total time 10s -> PASS -> +10 (no step failed, so not voided).
    //   GRADE  = clamp01( 0.25 - 5/100 + 10/100 ) = clamp01(0.30) = 0.30

    /// <summary>The shared (config + events) -> expected grade fixture. Run by both reducers.</summary>
    public static class AnalyticsEquivalenceFixture
    {
        public const string Step1Guid = "step1";
        public const string Step2Guid = "step2";
        public const string ScalpelId = "scalpel";
        public const string DistractorId = "distractor1";

        /// <summary>The hand-verified expected final grade for the complete stream (see the doc above).</summary>
        public const float ExpectedGrade = 0.30f;

        /// <summary>The canned v3 config: two step analytics (base), one Drop penalty, one TotalTimeUnder goal.</summary>
        public static LabConfig BuildConfig()
        {
            var config = new LabConfig { schemaVersion = 2 };

            config.subjects.Add(new TrackedSubject { id = ScalpelId, label = "Scalpel", scenarioRelevant = true });
            config.subjects.Add(new TrackedSubject { id = DistractorId, label = "Forceps (distractor)", scenarioRelevant = false });

            // Step 1: duration, Warning at 5s, Error at 10s.
            var dur = new StepDurationMetric
            {
                id = "m_dur",
                label = "Step 1 duration",
                bands = new List<ScoringBand>
                {
                    new ScoringBand(BandSeverity.None, 0f, 0f, false),
                    new ScoringBand(BandSeverity.Warning, 5f, 0.5f, true),
                    new ScoringBand(BandSeverity.Error, 10f, 1.0f, true)
                }
            };
            var step1 = new StepAnalytic { id = "A_step1", label = "Step 1", stepGuid = Step1Guid, weight = 1f };
            step1.metrics.Add(dur);

            // Step 2: a drop metric (default bands: warning 0.5 / error 1.0).
            var drop = new DropMetric { id = "m_drop", label = "Dropped items", bands = ScoringBand.DefaultBands() };
            var step2 = new StepAnalytic { id = "A_step2", label = "Step 2", stepGuid = Step2Guid, weight = 1f };
            step2.metrics.Add(drop);

            config.analytics.Add(step1);
            config.analytics.Add(step2);

            // Scene penalty: drops anywhere (-2 warning / -5 error per occurrence, cap -20).
            config.penalties.Add(new PenaltyRule
            {
                id = "P_drops", label = "Dropped instruments",
                kind = PenaltyKind.Drop, pointsPerWarning = 2, pointsPerError = 5, maxDeduction = 20
            });

            // Goal: finish under 30 seconds -> +10 bonus.
            config.goals.Add(new Goal
            {
                id = "G_time", label = "Finish under 30s",
                kind = GoalKind.TotalTimeUnder, threshold = 30f, bonusPoints = 10
            });

            return config;
        }

        /// <summary>The complete event stream: 7s in step1, one relevant drop in step2, bracket closed at 10s.</summary>
        public static SessionEventStream BuildCompleteStream()
        {
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, Step1Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 7000.0, Step1Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 7000.0, Step2Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 8000.0, Step2Guid, ScalpelId));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 9000.0, Step2Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 10000.0));
            return s;
        }

        /// <summary>The same run with no SessionStop (crash/quit) - must grade as "incomplete".</summary>
        public static SessionEventStream BuildIncompleteStream()
        {
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, Step1Guid));
            // no StepCompleted, no SessionStopped
            return s;
        }
    }
}
