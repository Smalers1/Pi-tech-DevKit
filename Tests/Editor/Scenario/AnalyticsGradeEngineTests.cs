using NUnit.Framework;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the WS B2.1 grade engine (map sec-11.8) via the shared equivalence golden
    /// fixture (<see cref="AnalyticsEquivalenceFixture"/>) - the SAME (rubric + events) the Web Portal
    /// mirror (B2.3) runs, so the two reducers stay in lockstep. Pure value-object tests (no scene).
    ///
    /// Hand-verified expected values for the complete stream (7s in step1 -> Warning 0.5; one relevant
    /// drop -> Error 1.0):
    ///   m_dur  raw 7s, ceiling Warning -> x = 1 - 0.5 = 0.5
    ///   A_step1 = 0.5 ; O_time X_o = 0.5 (not passed, target 0.9)
    ///   m_drop raw 1, Error -> penalty 1.0 -> x = 0.0
    ///   A_safety = 0.0 ; O_safety X_o = 0.0
    ///   Grade G = (0.5 + 0.0) / 2 = 0.25
    /// </summary>
    [TestFixture]
    public sealed class AnalyticsGradeEngineTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CompleteStream_MatchesEquivalenceFixtureGrade()
        {
            LabRubric rubric = AnalyticsEquivalenceFixture.BuildRubric();
            SessionEventStream stream = AnalyticsEquivalenceFixture.BuildCompleteStream();

            GradeResult g = AnalyticsGradeEngine.Compute(rubric, stream, SessionRole.Participant);

            Assert.That(g.isComplete, Is.True, "complete bracket should grade as complete");
            Assert.That(g.grade, Is.EqualTo(AnalyticsEquivalenceFixture.ExpectedGrade).Within(Eps),
                "DevKit canonical reducer must produce the fixture's expected grade (cloud mirror must match)");
        }

        [Test]
        public void CompleteStream_ObjectiveScoresAndPassBars()
        {
            LabRubric rubric = AnalyticsEquivalenceFixture.BuildRubric();
            SessionEventStream stream = AnalyticsEquivalenceFixture.BuildCompleteStream();

            GradeResult g = AnalyticsGradeEngine.Compute(rubric, stream, SessionRole.Participant);

            ObjectiveScoreResult timing = Find(g, "O_time");
            ObjectiveScoreResult safety = Find(g, "O_safety");

            Assert.That(timing.applicable, Is.True);
            Assert.That(timing.score, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(timing.passed, Is.False, "0.5 < target 0.9");

            Assert.That(safety.applicable, Is.True);
            Assert.That(safety.score, Is.EqualTo(0.0f).Within(Eps));
            Assert.That(safety.passed, Is.False);
        }

        [Test]
        public void CompleteStream_MetricSeverities()
        {
            LabRubric rubric = AnalyticsEquivalenceFixture.BuildRubric();
            SessionEventStream stream = AnalyticsEquivalenceFixture.BuildCompleteStream();

            GradeResult g = AnalyticsGradeEngine.Compute(rubric, stream, SessionRole.Participant);

            MetricScoreResult dur = FindMetric(g, "m_dur");
            MetricScoreResult drop = FindMetric(g, "m_drop");

            Assert.That(dur.worstSeverity, Is.EqualTo(BandSeverity.Warning));
            Assert.That(dur.rawValue, Is.EqualTo(7f).Within(Eps));
            Assert.That(drop.worstSeverity, Is.EqualTo(BandSeverity.Error));
            Assert.That(drop.rawValue, Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void IncompleteStream_GradesAsIncomplete()
        {
            LabRubric rubric = AnalyticsEquivalenceFixture.BuildRubric();
            SessionEventStream stream = AnalyticsEquivalenceFixture.BuildIncompleteStream();

            GradeResult g = AnalyticsGradeEngine.Compute(rubric, stream, SessionRole.Participant);

            Assert.That(g.isComplete, Is.False, "an unclosed bracket must be 'incomplete', never a grade");
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void UnenteredStep_DurationMetricIsMasked()
        {
            // A rubric whose step analytic targets a step the stream never enters -> that metric is
            // masked, the objective referencing only it is masked, and (since the other objective is the
            // scene drop with zero drops -> score 1) the grade is 1.0 from the applicable objective only.
            LabRubric rubric = AnalyticsEquivalenceFixture.BuildRubric();
            var stream = new SessionEventStream();
            stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "some_other_step"));
            stream.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 1000.0, "some_other_step"));
            stream.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 2000.0));

            GradeResult g = AnalyticsGradeEngine.Compute(rubric, stream, SessionRole.Participant);

            ObjectiveScoreResult timing = Find(g, "O_time");
            ObjectiveScoreResult safety = Find(g, "O_safety");
            Assert.That(timing.applicable, Is.False, "step1 never entered -> duration masked -> objective masked");
            Assert.That(safety.applicable, Is.True, "scene drop metric is applicable with zero drops");
            Assert.That(safety.score, Is.EqualTo(1f).Within(Eps), "zero drops -> perfect safety");
            Assert.That(g.isComplete, Is.True);
            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps), "only the applicable objective contributes");
        }

        static ObjectiveScoreResult Find(GradeResult g, string id)
        {
            for (int i = 0; i < g.objectives.Count; i++)
                if (g.objectives[i].id == id) return g.objectives[i];
            Assert.Fail($"objective '{id}' not found");
            return null;
        }

        static MetricScoreResult FindMetric(GradeResult g, string id)
        {
            for (int o = 0; o < g.objectives.Count; o++)
                for (int a = 0; a < g.objectives[o].analytics.Count; a++)
                    for (int m = 0; m < g.objectives[o].analytics[a].metrics.Count; m++)
                        if (g.objectives[o].analytics[a].metrics[m].id == id)
                            return g.objectives[o].analytics[a].metrics[m];
            Assert.Fail($"metric '{id}' not found");
            return null;
        }
    }
}
