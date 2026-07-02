using System.Collections.Generic;
using NUnit.Framework;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the explicit authored-failure metric (WS B2.3 review: SignalMetric). Proves
    /// signals score on their OWN typed metric (matched by id), and - the decoupling - that a Signal never
    /// leaks into a Drop/Wrong/Order metric even when ids collide (the old engine counted it on any count
    /// metric whose id == signalId). Pure value-object tests (no scene).
    /// </summary>
    [TestFixture]
    public sealed class SignalMetricTests
    {
        const float Eps = 1e-4f;

        // One analytic -> one objective (weight 1, pass-bar 0.9) so the grade mirrors the metric score.
        static LabConfig ConfigWith(Analytic analytic)
        {
            var r = new LabConfig { schemaVersion = 1 };
            r.analytics.Add(analytic);
            r.objectives.Add(new Objective
            {
                id = "O", label = "O", weight = 1f, target = 0.9f,
                inputs = new List<ObjectiveInput> { new ObjectiveInput { analyticId = analytic.id, subWeight = 1f } }
            });
            return r;
        }

        // A complete graded bracket wrapping the given mid events.
        static SessionEventStream Bracket(params AnalyticsEvent[] mid)
        {
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            foreach (var e in mid) s.Add(e);
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 1000.0));
            return s;
        }

        static AnalyticsEvent Signal(string signalId, string stepGuid = null, double tMs = 500.0)
            => new AnalyticsEvent(AnalyticsEventKind.Signal, tMs, stepGuid, null, signalId);

        [Test]
        public void SignalMetric_CountsMatchingSignal_ScoresZeroOnError()
        {
            var a = new SceneAnalytic { id = "A", label = "Failures", category = "Failures" };
            a.metrics.Add(new SignalMetric { id = "wrongCut", label = "Wrong cut", weight = 1f });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), Bracket(Signal("wrongCut")), SessionRole.Participant);

            Assert.That(g.isComplete, Is.True);
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps),
                "an authored signal derives Error -> default Error band 1.0 -> SignalMetric scores 0");
        }

        [Test]
        public void Signal_DoesNotCountTowardDropMetric_EvenWhenIdCollides()
        {
            // The DropMetric id deliberately COLLIDES with the signal id. The pre-fix engine absorbed the
            // signal into it (scoring 0); the decoupled engine must ignore it - no drops -> score 1.
            var a = new SceneAnalytic { id = "A", label = "Safety", category = "Safety" };
            a.metrics.Add(new DropMetric { id = "wrongCut", label = "Drops", weight = 1f });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), Bracket(Signal("wrongCut")), SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps),
                "a Signal must NOT count as a Drop occurrence, even when the DropMetric id equals the signalId");
        }

        [Test]
        public void SignalMetric_IgnoresNonMatchingSignalId()
        {
            var a = new SceneAnalytic { id = "A", label = "Failures", category = "Failures" };
            a.metrics.Add(new SignalMetric { id = "wrongCut", label = "Wrong cut", weight = 1f });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), Bracket(Signal("somethingElse")), SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps),
                "no signal matches this metric's id -> 0 occurrences -> score 1");
        }

        [Test]
        public void StepScopedSignalMetric_CountsOnlySignalsInItsStep()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1" };
            a.metrics.Add(new SignalMetric { id = "sig", label = "Sig", weight = 1f });
            // s1 is entered (so the step-scoped metric is applicable), but the signal is raised in s2.
            var stream = Bracket(
                new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "s1"),
                new AnalyticsEvent(AnalyticsEventKind.StepEntered, 100.0, "s2"),
                Signal("sig", "s2", 200.0),
                new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 300.0, "s1"));

            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), stream, SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps),
                "a signal raised in another step must not count for this step-scoped SignalMetric");
        }
    }
}
