using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The analytics equivalence golden fixture (map sec-11.0 / sec-11.3) ----------
    // WS B2.1 Step 8. ONE canned (rubric + events) -> expected grade, shipped in the runtime assembly so
    // BOTH reducers run it: the DevKit EditMode test (AnalyticsGradeEngineTests) AND the Web Portal
    // mirror (B2.3) must produce the IDENTICAL grade. This is the lockstep guarantee between the
    // canonical DevKit reducer and the cloud mirror (decision 38). Keep it deterministic and hand-
    // verifiable - the expected numbers below are computed in the test's doc comment.
    //
    // Scenario modelled: a 1-step graded bracket. The learner takes 7s in step1 (a Warning duration:
    // >=5s, <10s -> penalty 0.5) and drops the relevant "scalpel" once (relevant drop -> Error -> 1.0).
    //   Timing  objective  = step1 duration score 0.5
    //   Safety  objective  = drop score 0.0
    //   Grade   = (0.5 + 0.0) / 2 = 0.25

    /// <summary>The shared (rubric + events) -> expected grade fixture. Run by both reducers.</summary>
    public static class AnalyticsEquivalenceFixture
    {
        public const string Step1Guid = "step1";
        public const string ScalpelId = "scalpel";
        public const string DistractorId = "distractor1";

        /// <summary>The hand-verified expected final grade for the complete stream (see the doc above).</summary>
        public const float ExpectedGrade = 0.25f;

        /// <summary>The canned rubric: a Timing objective (step1 duration) + a Safety objective (drops).</summary>
        public static LabRubric BuildRubric()
        {
            var rubric = new LabRubric { schemaVersion = 1 };

            rubric.subjects.Add(new TrackedSubject { id = ScalpelId, label = "Scalpel", scenarioRelevant = true });
            rubric.subjects.Add(new TrackedSubject { id = DistractorId, label = "Forceps (distractor)", scenarioRelevant = false });

            // Step analytic: step1 duration, Warning at 5s, Error at 10s.
            var dur = new StepDurationMetric
            {
                id = "m_dur",
                label = "Step 1 duration",
                weight = 1f,
                bands = new List<ScoringBand>
                {
                    new ScoringBand(BandSeverity.None, 0f, 0f, false),
                    new ScoringBand(BandSeverity.Warning, 5f, 0.5f, true),
                    new ScoringBand(BandSeverity.Error, 10f, 1.0f, true)
                }
            };
            var stepAnalytic = new StepAnalytic { id = "A_step1", label = "Step 1", stepGuid = Step1Guid };
            stepAnalytic.metrics.Add(dur);

            // Scene analytic: drops (per-occurrence severity from the registry; relevant -> Error).
            var drop = new DropMetric
            {
                id = "m_drop",
                label = "Dropped items",
                weight = 1f,
                bands = ScoringBand.DefaultBands()   // none 0 / warning 0.5 / error 1.0
            };
            var sceneAnalytic = new SceneAnalytic { id = "A_safety", label = "Safety", category = "Safety" };
            sceneAnalytic.metrics.Add(drop);

            rubric.analytics.Add(stepAnalytic);
            rubric.analytics.Add(sceneAnalytic);

            rubric.objectives.Add(new Objective
            {
                id = "O_time", label = "Timing", weight = 1f, target = 0.9f,
                inputs = new List<ObjectiveInput> { new ObjectiveInput { analyticId = "A_step1", subWeight = 1f } }
            });
            rubric.objectives.Add(new Objective
            {
                id = "O_safety", label = "Safety", weight = 1f, target = 0.9f,
                inputs = new List<ObjectiveInput> { new ObjectiveInput { analyticId = "A_safety", subWeight = 1f } }
            });

            return rubric;
        }

        /// <summary>The complete event stream: 7s in step1, one relevant drop, bracket closed.</summary>
        public static SessionEventStream BuildCompleteStream()
        {
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, Step1Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 3000.0, Step1Guid, ScalpelId));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 7000.0, Step1Guid));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 8000.0));
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
