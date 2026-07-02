using System.Collections.Generic;
using NUnit.Framework;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the inert analytics config schema (map sec-11): the default scoring band
    /// set, the per-kind <c>Kind</c> tags, the default-band seeding on a fresh metric, and the
    /// <see cref="LabConfig"/> / <see cref="SessionRoleCapacities"/> defaults. Pure value-object tests -
    /// no GameObjects, no scene state.
    /// </summary>
    [TestFixture]
    public sealed class AnalyticsConfigTests
    {
        // ---------- ScoringBand.DefaultBands ----------

        [Test]
        public void DefaultBands_ReturnsExactlyThreeBands()
        {
            List<ScoringBand> bands = ScoringBand.DefaultBands();
            Assert.That(bands, Is.Not.Null);
            Assert.That(bands.Count, Is.EqualTo(3));
        }

        [Test]
        public void DefaultBands_NoneBand_HasZeroPenaltyAndNoNotify()
        {
            ScoringBand none = ScoringBand.DefaultBands()[0];
            Assert.That(none.name, Is.EqualTo(BandSeverity.None));
            Assert.That(none.penaltyWeight, Is.EqualTo(0f));
            Assert.That(none.notifyInScene, Is.False);
        }

        [Test]
        public void DefaultBands_WarningBand_HasHalfPenaltyAndNotifies()
        {
            ScoringBand warning = ScoringBand.DefaultBands()[1];
            Assert.That(warning.name, Is.EqualTo(BandSeverity.Warning));
            Assert.That(warning.penaltyWeight, Is.EqualTo(0.5f));
            Assert.That(warning.notifyInScene, Is.True);
        }

        [Test]
        public void DefaultBands_ErrorBand_HasFullPenaltyAndNotifies()
        {
            ScoringBand error = ScoringBand.DefaultBands()[2];
            Assert.That(error.name, Is.EqualTo(BandSeverity.Error));
            Assert.That(error.penaltyWeight, Is.EqualTo(1.0f));
            Assert.That(error.notifyInScene, Is.True);
        }

        // ---------- AnalyticsMetric subclasses: Kind == KindId ----------

        [Test]
        public void StepDurationMetric_KindMatchesKindId()
        {
            Assert.That(new StepDurationMetric().Kind, Is.EqualTo(StepDurationMetric.KindId));
        }

        [Test]
        public void SignalMetric_KindMatchesKindId()
        {
            Assert.That(new SignalMetric().Kind, Is.EqualTo(SignalMetric.KindId));
        }

        [Test]
        public void DropMetric_KindMatchesKindId()
        {
            Assert.That(new DropMetric().Kind, Is.EqualTo(DropMetric.KindId));
        }

        [Test]
        public void WrongInteractionMetric_KindMatchesKindId()
        {
            Assert.That(new WrongInteractionMetric().Kind, Is.EqualTo(WrongInteractionMetric.KindId));
        }

        [Test]
        public void OrderMetric_KindMatchesKindId()
        {
            Assert.That(new OrderMetric().Kind, Is.EqualTo(OrderMetric.KindId));
        }

        // ---------- A freshly-constructed metric is seeded with the default band set ----------

        [Test]
        public void FreshMetric_IsSeededWithDefaultBands()
        {
            var metric = new StepDurationMetric();
            Assert.That(metric.bands, Is.Not.Null);
            Assert.That(metric.bands.Count, Is.EqualTo(3));
            Assert.That(metric.bands[0].name, Is.EqualTo(BandSeverity.None));
            Assert.That(metric.bands[1].name, Is.EqualTo(BandSeverity.Warning));
            Assert.That(metric.bands[1].penaltyWeight, Is.EqualTo(0.5f));
            Assert.That(metric.bands[2].name, Is.EqualTo(BandSeverity.Error));
            Assert.That(metric.bands[2].penaltyWeight, Is.EqualTo(1.0f));
        }

        [Test]
        public void FreshMetric_CriticalAndNotifyOnlyDefaultFalse()
        {
            var metric = new StepDurationMetric();
            Assert.That(metric.critical, Is.False);
            Assert.That(metric.notifyOnly, Is.False);
        }

        // ---------- Analytic subclasses: Kind == KindId ----------

        [Test]
        public void StepAnalytic_KindMatchesKindId()
        {
            Assert.That(new StepAnalytic().Kind, Is.EqualTo(StepAnalytic.KindId));
        }

        [Test]
        public void StepAnalytic_DefaultsImportanceThreeAndNoScenarioFail()
        {
            var sa = new StepAnalytic();
            Assert.That(sa.weight, Is.EqualTo(3f));   // v3: importance is a 1-5 scale, default 3 (normal)
            Assert.That(sa.failsScenario, Is.False);
        }

        // ---------- v3 PenaltyRule + Goal defaults ----------

        [Test]
        public void PenaltyRule_Defaults()
        {
            var p = new PenaltyRule();
            Assert.That(p.kind, Is.EqualTo(PenaltyKind.Drop));
            Assert.That(p.pointsPerWarning, Is.EqualTo(2));
            Assert.That(p.pointsPerError, Is.EqualTo(5));
            Assert.That(p.maxDeduction, Is.EqualTo(20));
            Assert.That(p.failScenario, Is.False);
            Assert.That(p.notifyInScene, Is.True);
        }

        [Test]
        public void Goal_Defaults()
        {
            var g = new Goal();
            Assert.That(g.kind, Is.EqualTo(GoalKind.StepsScore));
            Assert.That(g.bonusPoints, Is.EqualTo(10));
            Assert.That(g.threshold, Is.EqualTo(70f));
        }

        // ---------- LabConfig defaults ----------

        [Test]
        public void LabConfig_DefaultSchemaVersionIsTwo()
        {
            Assert.That(new LabConfig().schemaVersion, Is.EqualTo(2));
        }

        // ---------- SessionRoleCapacities defaults ----------

        [Test]
        public void SessionRoleCapacities_UnlimitedSentinelIsNegativeOne()
        {
            Assert.That(SessionRoleCapacities.Unlimited, Is.EqualTo(-1));
        }

        [Test]
        public void SessionRoleCapacities_Defaults_MinParticipantsIsOne_OthersZero()
        {
            var caps = new SessionRoleCapacities();
            Assert.That(caps.minParticipants, Is.EqualTo(1));
            Assert.That(caps.minProfessors, Is.EqualTo(0));
            Assert.That(caps.minSpectators, Is.EqualTo(0));
        }

        [Test]
        public void SessionRoleCapacities_Defaults_AllMaxAreUnlimited()
        {
            var caps = new SessionRoleCapacities();
            Assert.That(caps.maxProfessors, Is.EqualTo(SessionRoleCapacities.Unlimited));
            Assert.That(caps.maxParticipants, Is.EqualTo(SessionRoleCapacities.Unlimited));
            Assert.That(caps.maxSpectators, Is.EqualTo(SessionRoleCapacities.Unlimited));
        }

        // ---------- SessionRole enum ----------

        [Test]
        public void SessionRole_HasProfessorParticipantSpectator()
        {
            Assert.That(System.Enum.IsDefined(typeof(SessionRole), SessionRole.Professor), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(SessionRole), SessionRole.Participant), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(SessionRole), SessionRole.Spectator), Is.True);
        }
    }
}
