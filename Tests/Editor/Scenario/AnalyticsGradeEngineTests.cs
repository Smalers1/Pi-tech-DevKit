using NUnit.Framework;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the v3 grade engine (base - penalties + bonus). Uses the shared equivalence
    /// golden fixture (the SAME config + events the Web Portal mirror runs) plus hand-derived edge cases:
    /// gate-zeroed step, pure-penalty base 100, scenario fail (terminal), bonus voiding, clamp overflow.
    /// Pure value-object tests (no scene).
    ///
    /// Fixture arithmetic (see AnalyticsEquivalenceFixture): base (0.5+0.0)/2 = 0.25; drop penalty -5; time
    /// goal +10 -> grade clamp01(0.25 - 0.05 + 0.10) = 0.30.
    /// </summary>
    [TestFixture]
    public sealed class AnalyticsGradeEngineTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CompleteStream_MatchesEquivalenceFixtureGrade()
        {
            LabConfig config = AnalyticsEquivalenceFixture.BuildConfig();
            SessionEventStream stream = AnalyticsEquivalenceFixture.BuildCompleteStream();

            GradeResult g = AnalyticsGradeEngine.Compute(config, stream, SessionRole.Participant);

            Assert.That(g.isComplete, Is.True);
            Assert.That(g.failed, Is.False);
            Assert.That(g.grade, Is.EqualTo(AnalyticsEquivalenceFixture.ExpectedGrade).Within(Eps),
                "DevKit canonical reducer must produce the fixture's expected grade (cloud mirror must match)");
        }

        [Test]
        public void CompleteStream_BasePenaltyBonusBreakdown()
        {
            LabConfig config = AnalyticsEquivalenceFixture.BuildConfig();
            GradeResult g = AnalyticsGradeEngine.Compute(config, AnalyticsEquivalenceFixture.BuildCompleteStream(), SessionRole.Participant);

            Assert.That(g.baseScore, Is.EqualTo(0.25f).Within(Eps));
            Assert.That(g.penaltyPointsTotal, Is.EqualTo(5));
            Assert.That(g.bonusPointsTotal, Is.EqualTo(10));

            AnalyticScoreResult s1 = FindStep(g, "A_step1");
            AnalyticScoreResult s2 = FindStep(g, "A_step2");
            Assert.That(s1.score, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(s2.score, Is.EqualTo(0.0f).Within(Eps));

            PenaltyScoreResult drops = FindPenalty(g, "P_drops");
            Assert.That(drops.errorCount, Is.EqualTo(1));
            Assert.That(drops.pointsDeducted, Is.EqualTo(5));

            GoalScoreResult time = FindGoal(g, "G_time");
            Assert.That(time.earnable, Is.True);
            Assert.That(time.passed, Is.True);
        }

        [Test]
        public void IncompleteStream_GradesAsIncomplete()
        {
            LabConfig config = AnalyticsEquivalenceFixture.BuildConfig();
            GradeResult g = AnalyticsGradeEngine.Compute(config, AnalyticsEquivalenceFixture.BuildIncompleteStream(), SessionRole.Participant);

            Assert.That(g.isComplete, Is.False);
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void GateFailedStep_ZeroesStepButStaysApplicable()
        {
            // Two equal steps, A perfect, B has a critical Drop gate that fails on a relevant drop.
            // base must be (1*1 + 1*0)/2 = 0.5, NOT 1.0 (the failed step stays in the denominator).
            var config = new LabConfig { schemaVersion = 2 };
            config.subjects.Add(new TrackedSubject { id = "knife", label = "Knife", scenarioRelevant = true });

            var a = new StepAnalytic { id = "A", label = "A", stepGuid = "a", weight = 1f };
            a.metrics.Add(new StepDurationMetric { id = "a_dur", bands = ScoringBand.DefaultBands() }); // inactive -> 1.0
            var b = new StepAnalytic { id = "B", label = "B", stepGuid = "b", weight = 1f };
            b.metrics.Add(new DropMetric { id = "b_drop", critical = true, bands = ScoringBand.DefaultBands() });
            config.analytics.Add(a);
            config.analytics.Add(b);

            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "a"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 100.0, "a"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 100.0, "b"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 200.0, "b", "knife"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 300.0, "b"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 400.0));

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            AnalyticScoreResult sb = FindStep(g, "B");
            Assert.That(sb.applicable, Is.True);
            Assert.That(sb.stepFailed, Is.True);
            Assert.That(sb.score, Is.EqualTo(0f).Within(Eps));
            Assert.That(g.baseScore, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(g.bonusesVoided, Is.True, "a failed step voids bonuses");
            Assert.That(g.failed, Is.False, "the step does not fail the scenario (failsScenario is off)");
        }

        [Test]
        public void FailsScenarioStep_CriticalGate_FailsWholeScenario()
        {
            var config = new LabConfig { schemaVersion = 2 };
            config.subjects.Add(new TrackedSubject { id = "artery", label = "Artery", scenarioRelevant = true });
            var b = new StepAnalytic { id = "B", label = "Cut", stepGuid = "b", weight = 1f, failsScenario = true };
            b.metrics.Add(new DropMetric { id = "b_drop", label = "Wrong cut", critical = true, bands = ScoringBand.DefaultBands() });
            config.analytics.Add(b);

            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "b"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 100.0, "b", "artery"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 200.0, "b"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 300.0));

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            Assert.That(g.failed, Is.True);
            Assert.That(g.isComplete, Is.True);
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps));
            Assert.That(g.failCauseMetricId, Is.EqualTo("b_drop"));
        }

        [Test]
        public void ScenarioFailedFact_OnUnclosedBracket_IsTerminal()
        {
            // The recorder emitted scenario.failed but the bracket never closed (learner restarted). The engine
            // must still report failed + isComplete (the fail is the outcome), not "Incomplete".
            var config = new LabConfig { schemaVersion = 2 };
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.ScenarioFailed, 500.0, null, null, "b_drop"));
            // no SessionStopped

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            Assert.That(g.failed, Is.True);
            Assert.That(g.isComplete, Is.True);
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void PurePenaltyLab_NoStepAnalytics_BaseIsOneHundred()
        {
            var config = new LabConfig { schemaVersion = 2 };
            config.penalties.Add(new PenaltyRule { id = "P", label = "Drops", kind = PenaltyKind.Drop, pointsPerError = 5, pointsPerWarning = 2, maxDeduction = 20 });
            config.subjects.Add(new TrackedSubject { id = "x", label = "X", scenarioRelevant = true });

            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 100.0, null, "x"));   // relevant -> error -> -5
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 200.0));

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            Assert.That(g.isComplete, Is.True);
            Assert.That(g.baseScore, Is.EqualTo(1f).Within(Eps));
            Assert.That(g.penaltyPointsTotal, Is.EqualTo(5));
            Assert.That(g.grade, Is.EqualTo(0.95f).Within(Eps));
        }

        [Test]
        public void AuthoredSteps_NoneRan_IsIncomplete()
        {
            LabConfig config = AnalyticsEquivalenceFixture.BuildConfig();
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "unrelated"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 100.0, "unrelated"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 200.0));

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            Assert.That(g.isComplete, Is.False, "steps authored but none entered -> never invent a base");
        }

        [Test]
        public void PenaltyCap_LimitsDeduction()
        {
            var config = new LabConfig { schemaVersion = 2 };
            config.subjects.Add(new TrackedSubject { id = "x", label = "X", scenarioRelevant = true });
            config.penalties.Add(new PenaltyRule { id = "P", kind = PenaltyKind.Drop, pointsPerError = 5, maxDeduction = 8 });

            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            for (int i = 0; i < 5; i++) s.Add(new AnalyticsEvent(AnalyticsEventKind.Drop, 100.0 + i, null, "x")); // 5*5=25, capped 8
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 300.0));

            GradeResult g = AnalyticsGradeEngine.Compute(config, s, SessionRole.Participant);
            PenaltyScoreResult p = FindPenalty(g, "P");
            Assert.That(p.pointsDeducted, Is.EqualTo(8));
            Assert.That(p.capped, Is.True);
        }

        // ---- helpers ----
        static AnalyticScoreResult FindStep(GradeResult g, string id)
        {
            for (int i = 0; i < g.steps.Count; i++) if (g.steps[i].id == id) return g.steps[i];
            Assert.Fail($"step '{id}' not found"); return null;
        }
        static PenaltyScoreResult FindPenalty(GradeResult g, string id)
        {
            for (int i = 0; i < g.penalties.Count; i++) if (g.penalties[i].id == id) return g.penalties[i];
            Assert.Fail($"penalty '{id}' not found"); return null;
        }
        static GoalScoreResult FindGoal(GradeResult g, string id)
        {
            for (int i = 0; i < g.goals.Count; i++) if (g.goals[i].id == id) return g.goals[i];
            Assert.Fail($"goal '{id}' not found"); return null;
        }
    }
}
