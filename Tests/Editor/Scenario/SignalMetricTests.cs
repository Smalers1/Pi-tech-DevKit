using NUnit.Framework;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the explicit authored-failure metric (SignalMetric) under the v3 model. A signal
    /// scores on its OWN typed metric (matched by id) and never leaks into a Drop/Wrong/Order metric even when
    /// ids collide. Here the SignalMetric lives on a single step analytic, so the grade equals the base = that
    /// step's score. Pure value-object tests (no scene).
    /// </summary>
    [TestFixture]
    public sealed class SignalMetricTests
    {
        const float Eps = 1e-4f;

        // One step analytic -> the base equals that step's score (no penalties / goals).
        static LabConfig ConfigWith(StepAnalytic step)
        {
            var r = new LabConfig { schemaVersion = 2 };
            r.analytics.Add(step);
            return r;
        }

        // A complete bracket that enters + completes step "s1" around the given mid events.
        static SessionEventStream BracketS1(params AnalyticsEvent[] mid)
        {
            var s = new SessionEventStream();
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStarted, 0.0));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepEntered, 0.0, "s1"));
            foreach (var e in mid) s.Add(e);
            s.Add(new AnalyticsEvent(AnalyticsEventKind.StepCompleted, 800.0, "s1"));
            s.Add(new AnalyticsEvent(AnalyticsEventKind.SessionStopped, 1000.0));
            return s;
        }

        static AnalyticsEvent Signal(string signalId, string stepGuid = "s1", double tMs = 500.0)
            => new AnalyticsEvent(AnalyticsEventKind.Signal, tMs, stepGuid, null, signalId);

        [Test]
        public void SignalMetric_CountsMatchingSignal_ScoresZeroOnError()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1", weight = 1f };
            a.metrics.Add(new SignalMetric { id = "wrongCut", label = "Wrong cut" });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), BracketS1(Signal("wrongCut")), SessionRole.Participant);

            Assert.That(g.isComplete, Is.True);
            Assert.That(g.grade, Is.EqualTo(0f).Within(Eps),
                "a matching signal -> Error -> default Error band 1.0 -> metric 0 -> step 0 -> base 0");
        }

        [Test]
        public void Signal_DoesNotCountTowardDropMetric_EvenWhenIdCollides()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1", weight = 1f };
            a.metrics.Add(new DropMetric { id = "wrongCut", label = "Drops" });   // id collides with the signal
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), BracketS1(Signal("wrongCut")), SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps),
                "a Signal must NOT count as a Drop occurrence, even when the DropMetric id equals the signalId");
        }

        [Test]
        public void SignalMetric_IgnoresNonMatchingSignalId()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1", weight = 1f };
            a.metrics.Add(new SignalMetric { id = "wrongCut", label = "Wrong cut" });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), BracketS1(Signal("somethingElse")), SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps), "no signal matches -> 0 occurrences -> score 1");
        }

        [Test]
        public void StepScopedSignalMetric_CountsOnlySignalsInItsStep()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1", weight = 1f };
            a.metrics.Add(new SignalMetric { id = "sig", label = "Sig" });
            // The signal is raised in s2, not s1 -> must not count for this step-scoped metric.
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), BracketS1(Signal("sig", "s2", 200.0)), SessionRole.Participant);

            Assert.That(g.grade, Is.EqualTo(1f).Within(Eps),
                "a signal raised in another step must not count for this step-scoped SignalMetric");
        }

        [Test]
        public void CriticalSignalMetric_FailsTheStep()
        {
            var a = new StepAnalytic { id = "A", label = "Step 1", stepGuid = "s1", weight = 1f };
            a.metrics.Add(new SignalMetric { id = "sig", label = "Sig", critical = true });
            GradeResult g = AnalyticsGradeEngine.Compute(ConfigWith(a), BracketS1(Signal("sig")), SessionRole.Participant);

            AnalyticScoreResult s = g.steps[0];
            Assert.That(s.stepFailed, Is.True, "a critical signal is a gate: any occurrence fails the step");
            Assert.That(g.baseScore, Is.EqualTo(0f).Within(Eps));
        }
    }
}
