using NUnit.Framework;
using Pitech.XR.AgentSubstrate.Observation;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Pitech.XR.AgentSubstrate.Editor.Tests
{
    [TestFixture]
    public class AgentObservationNetworkTests
    {
        static string ErrorBody(string code) =>
            "{\"version\":\"v1\",\"error\":\"" + code + "\",\"message\":\"" + code + "\"}";

        [Test]
        public void Network_ConsentNotGranted_IsDebugLogOnly()
        {
            var d = AgentObservationResponseClassifier.Classify(403,
                ErrorBody(AgentObservationErrorCodeV1.ConsentNotGranted));
            Assert.AreEqual(AgentObservationLogLevel.Verbose, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
            Assert.AreEqual(AgentObservationErrorCodeV1.ConsentNotGranted, d.ErrorCode);
        }

        [Test]
        public void Network_NotImplemented_IsDebugLogOnly()
        {
            var d = AgentObservationResponseClassifier.Classify(501,
                ErrorBody(AgentObservationErrorCodeV1.NotImplemented));
            Assert.AreEqual(AgentObservationLogLevel.Verbose, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
        }

        [Test]
        public void Network_SchemaInvalid_IsErrorAndNoRetry()
        {
            var d = AgentObservationResponseClassifier.Classify(400,
                ErrorBody(AgentObservationErrorCodeV1.SchemaInvalid));
            Assert.AreEqual(AgentObservationLogLevel.Error, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
        }

        [Test]
        public void Network_MethodNotAllowed_IsErrorAndNoRetry()
        {
            var d = AgentObservationResponseClassifier.Classify(405,
                ErrorBody(AgentObservationErrorCodeV1.MethodNotAllowed));
            Assert.AreEqual(AgentObservationLogLevel.Error, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
        }

        [Test]
        public void Network_AuthRequired_IsWarningAndRetries()
        {
            var d = AgentObservationResponseClassifier.Classify(401,
                ErrorBody(AgentObservationErrorCodeV1.AuthRequired));
            Assert.AreEqual(AgentObservationLogLevel.Warning, d.LogLevel);
            Assert.IsTrue(d.ShouldRetry);
        }

        [Test]
        public void Network_5xx_TriggersRetry()
        {
            var d = AgentObservationResponseClassifier.Classify(500, "");
            Assert.AreEqual(AgentObservationLogLevel.Warning, d.LogLevel);
            Assert.IsTrue(d.ShouldRetry);
        }

        [Test]
        public void Network_5xx_RetryPolicy_BackoffSchedule()
        {
            // 2^1=2, 2^2=4, 2^3=8, then cap at 64.
            Assert.AreEqual(2, AgentObservationRetryPolicy.DelaySeconds(1));
            Assert.AreEqual(4, AgentObservationRetryPolicy.DelaySeconds(2));
            Assert.AreEqual(8, AgentObservationRetryPolicy.DelaySeconds(3));
            Assert.AreEqual(64, AgentObservationRetryPolicy.DelaySeconds(10));
            Assert.AreEqual(3, AgentObservationRetryPolicy.MaxRetries);
        }

        [Test]
        public void Network_5xx_Retries_ThenDrops()
        {
            // Simulate three retry attempts of the classifier on consecutive 500s,
            // matching the real HTTP client's retry loop (plan §2.9). After
            // MaxRetries the production loop logs a warning and drops.
            for (int attempt = 1; attempt <= AgentObservationRetryPolicy.MaxRetries; attempt++)
            {
                var d = AgentObservationResponseClassifier.Classify(500, "");
                Assert.IsTrue(d.ShouldRetry);
                Assert.IsTrue(AgentObservationRetryPolicy.DelaySeconds(attempt) <=
                              AgentObservationRetryPolicy.MaxBackoffSeconds);
            }
        }

        [Test]
        public void Network_RetryBudgetExhausted_LogsWarningAndDrops()
        {
            // Transport-level coverage of the drop branch in
            // AgentObservationHttpClient.SendWithRetry, factored out as
            // TryConsumeRetryBudget so we can assert the warning without
            // standing up a UnityWebRequest. The retry loop calls this with
            // attempt = 1..MaxRetries (all true) and then attempt =
            // MaxRetries+1 (false + warning).
            for (int attempt = 1; attempt <= AgentObservationRetryPolicy.MaxRetries; attempt++)
            {
                Assert.IsTrue(AgentObservationHttpClient.TryConsumeRetryBudget(attempt),
                    "Attempt " + attempt + " within budget should be allowed.");
            }
            LogAssert.Expect(LogType.Warning, new Regex(@"retry budget exhausted"));
            Assert.IsFalse(AgentObservationHttpClient.TryConsumeRetryBudget(
                AgentObservationRetryPolicy.MaxRetries + 1));
        }

        [Test]
        public void Network_UnknownErrorCode_IsWarningPlusDump()
        {
            var d = AgentObservationResponseClassifier.Classify(400, ErrorBody("FUTURE_CODE"));
            Assert.AreEqual(AgentObservationLogLevel.Warning, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
            Assert.AreEqual("UNKNOWN", d.ErrorCode);
        }

        [Test]
        public void Network_QueueOverflow_DropsOldest()
        {
            var q = new AgentObservationQueue(capacity: 2);
            var a = new AgentObservationV1Envelope();
            var b = new AgentObservationV1Envelope();
            var c = new AgentObservationV1Envelope();
            q.Enqueue(new AgentObservationOutboundItem(a, "ar"));
            q.Enqueue(new AgentObservationOutboundItem(b, "ar"));

            // Third enqueue overflows; drop-oldest should evict 'a' and log a warning.
            LogAssert.Expect(LogType.Warning, new Regex(@"queue overflow"));
            q.Enqueue(new AgentObservationOutboundItem(c, "vr"));

            Assert.AreEqual(2, q.Count);
            Assert.AreEqual(1, q.TotalDropped);

            // Drain and confirm order: dequeue returns b then c (a was dropped).
            // The surface travels with the envelope so interleaved AR + VR sends
            // still ship with the correct `X-Vicky-Surface` header.
            Assert.IsTrue(q.TryDequeue(out var first));
            Assert.AreSame(b, first.Envelope);
            Assert.AreEqual("ar", first.Surface);
            Assert.IsTrue(q.TryDequeue(out var second));
            Assert.AreSame(c, second.Envelope);
            Assert.AreEqual("vr", second.Surface);
        }

        [Test]
        public void Network_Success200_NoLogNoRetry()
        {
            var d = AgentObservationResponseClassifier.Classify(200, "");
            Assert.AreEqual(AgentObservationLogLevel.None, d.LogLevel);
            Assert.IsFalse(d.ShouldRetry);
        }

        // Round-2 regression: the round-1 transport bug was that the auth token
        // was captured at the initial Send() call site and reused for every
        // queued envelope thereafter (and for every backoff retry of each). The
        // refactor moves auth-token resolution into the transport's retry loop
        // (per attempt), so the queue's outbound item MUST NOT carry the token.
        // This compile/reflection-time guard catches accidental regression.
        [Test]
        public void Network_OutboundItem_DoesNotCarryAuthToken()
        {
            var fields = typeof(AgentObservationOutboundItem).GetFields();
            foreach (var f in fields)
            {
                StringAssert.DoesNotContain("token", f.Name.ToLowerInvariant(),
                    "AgentObservationOutboundItem must not hold auth tokens — they are resolved per attempt by the transport.");
            }
            var item = new AgentObservationOutboundItem(new AgentObservationV1Envelope(), "ar");
            Assert.AreEqual("ar", item.Surface);
        }

        // Round-2 regression: the transport-seam Send() signature must not
        // accept an access-token parameter. Callers that pass a token would
        // re-introduce the round-1 stale-context bug.
        [Test]
        public void Network_TransportSendSignature_DoesNotTakeAuthToken()
        {
            var method = typeof(IAgentObservationTransport).GetMethod("Send");
            Assert.IsNotNull(method, "IAgentObservationTransport.Send must exist.");
            var ps = method.GetParameters();
            Assert.AreEqual(2, ps.Length,
                "Send must accept (envelope, surface) only; auth is resolved per attempt by the transport implementation.");
            foreach (var p in ps)
            {
                StringAssert.DoesNotContain("token", p.Name.ToLowerInvariant());
            }
        }
    }
}
