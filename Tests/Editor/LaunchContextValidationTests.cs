using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    /// <summary>
    /// WS A3 Step 6 - locks the internal LaunchContextValidation contract on UNMODIFIED code
    /// (reached via InternalsVisibleTo, mirroring the AgentSubstrate pattern). This is the validation
    /// every runtime launch and telemetry emission funnels through; the launch-mode predicates
    /// (IsExternalOnlineLaunch / RequiresRuntimeUrl) encode the online-vs-cache policy the spawner
    /// relies on.
    /// </summary>
    public class LaunchContextValidationTests
    {
        static LaunchContext ValidContext() => new LaunchContext
        {
            launchRequestId = "req-1",
            attemptId = "att-1",
            idempotencyKey = "idem-1",
            labId = "lab-1",
            resolvedVersionId = "v1",
        };

        // --- TryValidateLineage ---------------------------------------------------------------

        [Test]
        public void Lineage_NullContext_FailsWithReason()
        {
            Assert.IsFalse(LaunchContextValidation.TryValidateLineage(null, false, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Lineage_ValidContext_Passes()
        {
            Assert.IsTrue(LaunchContextValidation.TryValidateLineage(ValidContext(), true, out var reason));
            Assert.IsEmpty(reason);
        }

        [TestCase("launchRequestId")]
        [TestCase("attemptId")]
        [TestCase("idempotencyKey")]
        [TestCase("labId")]
        public void Lineage_EachRequiredField_FailsWhenMissing(string field)
        {
            var c = ValidContext();
            switch (field)
            {
                case "launchRequestId": c.launchRequestId = "  "; break;
                case "attemptId": c.attemptId = ""; break;
                case "idempotencyKey": c.idempotencyKey = null; break;
                case "labId": c.labId = " "; break;
            }
            Assert.IsFalse(LaunchContextValidation.TryValidateLineage(c, false, out var reason),
                $"missing {field} must fail");
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Lineage_ResolvedVersionId_OnlyRequiredWhenAsked()
        {
            var c = ValidContext();
            c.resolvedVersionId = "";
            Assert.IsTrue(LaunchContextValidation.TryValidateLineage(c, requireResolvedVersionId: false, out _));
            Assert.IsFalse(LaunchContextValidation.TryValidateLineage(c, requireResolvedVersionId: true, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Lineage_TrimsWhitespaceFieldsInPlace()
        {
            var c = ValidContext();
            c.labId = "  lab-1  ";
            Assert.IsTrue(LaunchContextValidation.TryValidateLineage(c, false, out _));
            Assert.AreEqual("lab-1", c.labId);
        }

        // --- TryValidateRuntimeLaunchContext ----------------------------------------------------

        [Test]
        public void Runtime_LocalDirectLaunch_PassesWithoutUrlOrVersion()
        {
            var c = ValidContext();
            c.resolvedVersionId = "";
            c.runtimeUrl = "";
            c.source = LaunchSource.Direct;
            c.launchedFromCache = false;
            Assert.IsTrue(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out var reason), reason);
        }

        [Test]
        public void Runtime_BridgeLaunch_RequiresResolvedVersionAndUrl()
        {
            var c = ValidContext();
            c.source = LaunchSource.ReactNativeBridge;
            c.resolvedVersionId = "";
            Assert.IsFalse(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out var reason));
            StringAssert.Contains("resolvedVersionId", reason);

            c.resolvedVersionId = "v1";
            c.runtimeUrl = "";
            Assert.IsFalse(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out reason));
            StringAssert.Contains("runtimeUrl", reason);

            c.runtimeUrl = "https://cdn.example.com/content";
            Assert.IsTrue(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out reason), reason);
        }

        [Test]
        public void Runtime_CacheLaunch_RequiresCachedVersion_ButNoUrl()
        {
            var c = ValidContext();
            c.source = LaunchSource.ReactNativeBridge;
            c.launchedFromCache = true;
            c.runtimeUrl = "";
            c.resolvedVersionId = "v1";
            Assert.IsTrue(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out var reason), reason);

            c.resolvedVersionId = "";
            Assert.IsFalse(LaunchContextValidation.TryValidateRuntimeLaunchContext(c, out reason));
            Assert.IsNotEmpty(reason);
        }

        // --- launch-mode predicates -------------------------------------------------------------

        [Test]
        public void IsExternalOnlineLaunch_TriggersOnBridgeOrUrlOrCache()
        {
            Assert.IsFalse(LaunchContextValidation.IsExternalOnlineLaunch(null));

            var direct = ValidContext();
            direct.source = LaunchSource.Direct;
            Assert.IsFalse(LaunchContextValidation.IsExternalOnlineLaunch(direct));

            var bridge = ValidContext();
            bridge.source = LaunchSource.ReactNativeBridge;
            Assert.IsTrue(LaunchContextValidation.IsExternalOnlineLaunch(bridge));

            var url = ValidContext();
            url.runtimeUrl = "https://x";
            Assert.IsTrue(LaunchContextValidation.IsExternalOnlineLaunch(url));

            var cache = ValidContext();
            cache.launchedFromCache = true;
            Assert.IsTrue(LaunchContextValidation.IsExternalOnlineLaunch(cache));
        }

        [Test]
        public void RequiresRuntimeUrl_OnlineYes_CacheNo()
        {
            var bridge = ValidContext();
            bridge.source = LaunchSource.ReactNativeBridge;
            Assert.IsTrue(LaunchContextValidation.RequiresRuntimeUrl(bridge));

            bridge.launchedFromCache = true;
            Assert.IsFalse(LaunchContextValidation.RequiresRuntimeUrl(bridge));
        }

        // --- TryValidateAttemptPayload -----------------------------------------------------------

        static RuntimeTelemetryAttemptPayload ValidPayload() => new RuntimeTelemetryAttemptPayload
        {
            attempt_id = "att-1",
            idempotency_key = "idem-1",
            lab_id = "lab-1",
            completion_status = "completed",
            lab_version_id = "v1",
            duration_seconds = 10,
            critical_error_count = 0,
        };

        [Test]
        public void Attempt_ValidPayload_Passes()
        {
            Assert.IsTrue(LaunchContextValidation.TryValidateAttemptPayload(ValidPayload(), out var reason), reason);
        }

        [Test]
        public void Attempt_NullPayload_Fails()
        {
            Assert.IsFalse(LaunchContextValidation.TryValidateAttemptPayload(null, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [TestCase("attempt_id")]
        [TestCase("idempotency_key")]
        [TestCase("lab_id")]
        [TestCase("completion_status")]
        [TestCase("lab_version_id")]
        public void Attempt_EachRequiredField_FailsWhenMissing(string field)
        {
            var p = ValidPayload();
            switch (field)
            {
                case "attempt_id": p.attempt_id = ""; break;
                case "idempotency_key": p.idempotency_key = " "; break;
                case "lab_id": p.lab_id = null; break;
                case "completion_status": p.completion_status = ""; break;
                case "lab_version_id": p.lab_version_id = ""; break;
            }
            Assert.IsFalse(LaunchContextValidation.TryValidateAttemptPayload(p, out var reason),
                $"missing {field} must fail");
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Attempt_NegativeCounters_Fail()
        {
            var p = ValidPayload();
            p.duration_seconds = -1;
            Assert.IsFalse(LaunchContextValidation.TryValidateAttemptPayload(p, out _));

            p = ValidPayload();
            p.critical_error_count = -1;
            Assert.IsFalse(LaunchContextValidation.TryValidateAttemptPayload(p, out _));
        }
    }
}
