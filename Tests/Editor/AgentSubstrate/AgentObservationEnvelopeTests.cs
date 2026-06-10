using NUnit.Framework;
using Pitech.XR.AgentSubstrate.Observation;
using UnityEngine;

namespace Pitech.XR.AgentSubstrate.Editor.Tests
{
    [TestFixture]
    public class AgentObservationEnvelopeTests
    {
        AgentObservationEmitter emitter;
        GameObject host;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("emitter-test");
            emitter = host.AddComponent<AgentObservationEmitter>();
            emitter.Surface = AgentObservationSurfaceV1.Ar;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        AgentStateSnapshot SnapshotForLabStep()
        {
            return new AgentStateSnapshot
            {
                Kind = AgentObservationKindV1.LabStepObserved,
                SemanticState = new AgentObservationSemanticStateV1
                {
                    summary = "Learner wore gloves.",
                    attributes =
                    {
                        AgentObservationAttribute.OfString("step", "wear_gloves"),
                        AgentObservationAttribute.OfLong("elapsed_ms", 4310),
                        AgentObservationAttribute.OfBool("outcome_pass", true),
                    },
                },
                RenderedState = new AgentObservationRenderedStateV1
                {
                    text = "Gloves equipped",
                    transcript = null,
                },
                LabId = "lab-1",
                LabVersionId = "ver-1",
                AttemptId = "att-1",
                SessionId = "sess-1",
            };
        }

        [Test]
        public void Envelope_WellFormed_PassesBasicFieldPresence()
        {
            var envelope = emitter.BuildEnvelope(SnapshotForLabStep());
            Assert.AreEqual("v1", envelope.version);
            Assert.AreEqual(1, envelope.observations.Count);
            var o = envelope.observations[0];
            Assert.AreEqual("v1", o.version);
            Assert.IsFalse(string.IsNullOrEmpty(o.observationId));
            Assert.AreEqual(AgentObservationKindV1.LabStepObserved, o.kind);
            Assert.IsFalse(string.IsNullOrEmpty(o.observedAt));
            Assert.AreEqual(AgentObservationSurfaceV1.Ar, o.surface);
            Assert.IsNotNull(o.semanticState);
            Assert.IsNotNull(o.renderedState);
            Assert.IsNotNull(o.engine);
            Assert.AreEqual("unity", o.engine.name);

            // Required-present serialization: renderedState key must appear in
            // the wire JSON even when its value is null (next test covers null).
            var json = AgentObservationEnvelopeWriter.ToJson(envelope);
            StringAssert.Contains("\"renderedState\":", json);
            StringAssert.Contains("\"semanticState\":", json);
            StringAssert.Contains("\"engine\":", json);
            StringAssert.Contains("\"observationId\":", json);
        }

        [Test]
        public void Envelope_TwoObservations_HaveDifferentObservationIds()
        {
            var a = emitter.BuildEnvelope(SnapshotForLabStep()).observations[0];
            var b = emitter.BuildEnvelope(SnapshotForLabStep()).observations[0];
            Assert.AreNotEqual(a.observationId, b.observationId);
        }

        [Test]
        public void Envelope_Surface_MatchesHeaderSurface()
        {
            emitter.Surface = AgentObservationSurfaceV1.Vr;
            var o = emitter.BuildEnvelope(SnapshotForLabStep()).observations[0];
            Assert.AreEqual(AgentObservationSurfaceV1.Vr, o.surface);
        }

        [Test]
        public void Envelope_NoTenantId_NotPresent()
        {
            var envelope = emitter.BuildEnvelope(SnapshotForLabStep());
            var json = AgentObservationEnvelopeWriter.ToJson(envelope);
            StringAssert.DoesNotContain("tenantId", json);
        }

        [Test]
        public void Envelope_RenderedState_NullIsExplicit()
        {
            var snap = SnapshotForLabStep();
            snap.RenderedState = null;
            var envelope = emitter.BuildEnvelope(snap);
            var json = AgentObservationEnvelopeWriter.ToJson(envelope);
            StringAssert.Contains("\"renderedState\":null", json);
        }

        [Test]
        public void Envelope_Attributes_SerializeAsJsonObjectWithTypedValues()
        {
            var envelope = emitter.BuildEnvelope(SnapshotForLabStep());
            var json = AgentObservationEnvelopeWriter.ToJson(envelope);
            StringAssert.Contains("\"step\":\"wear_gloves\"", json);
            StringAssert.Contains("\"elapsed_ms\":4310", json);
            StringAssert.Contains("\"outcome_pass\":true", json);
        }
    }
}
