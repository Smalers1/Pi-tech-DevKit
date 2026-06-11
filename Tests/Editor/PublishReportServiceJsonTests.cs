using System.IO;
using NUnit.Framework;
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    /// <summary>
    /// WS A3 Step 6 - the PublishReportService JSON check. A literal byte-golden is impossible
    /// (the report embeds createdAt/updatedAt/transactionId/machine identity and a timestamped file
    /// name), so this locks the DETERMINISTIC content contract instead: state-machine progression
    /// through the service's Apply* methods, the idempotency-key derivation, the check-filtering
    /// rule (passed info checks dropped; warnings/errors/failures kept), and the on-disk JSON
    /// written by Save() containing the stable fields. Writes only under Build/_DevKitTestTmp
    /// (out-of-tree, never imported) and deletes it after.
    /// </summary>
    public class PublishReportServiceJsonTests
    {
        const string TmpWorkspace = "Build/_DevKitTestTmp";

        [TearDown]
        public void Cleanup()
        {
            string abs = Path.GetFullPath(TmpWorkspace);
            if (Directory.Exists(abs))
                Directory.Delete(abs, recursive: true);
        }

        static PublishTransactionReportData Draft(PublishReportService svc)
            => svc.CreateDraft(null, PublishTransactionSource.GuidedSetup, "tests", "lab-x", "v1");

        [Test]
        public void CreateDraft_StampsLabLineageAndPendingIdempotencyKey()
        {
            var svc = new PublishReportService();
            var report = Draft(svc);

            Assert.AreEqual(PublishTransactionState.Draft, report.state);
            Assert.AreEqual("lab-x", report.lab.labId);
            Assert.AreEqual("v1", report.lab.labVersionId);
            // The draft key derives from the lineage with the "pending" content hash.
            Assert.AreEqual(
                PublishTransactionIdempotency.BuildKey(report.lab.tenantId, "lab-x", "v1", "pending"),
                report.idempotencyKey);
        }

        [Test]
        public void ApplyValidation_FailingValidation_GoesTerminalWithError()
        {
            var svc = new PublishReportService();
            var report = Draft(svc);

            var validation = new AddressablesValidationResult { summary = "boom" };
            validation.Add(new PublishCheckEntry
            {
                code = "X-1",
                severity = PublishCheckSeverity.Error,
                message = "bad",
                passed = false,
            });

            svc.ApplyValidation(report, validation, "tests");

            Assert.AreEqual(PublishTransactionState.FailedTerminal, report.state);
            Assert.AreEqual(1, report.errors.Count);
            Assert.AreEqual("VALIDATION_FAILED", report.errors[0].code);
            Assert.IsTrue(report.errors[0].terminal);
        }

        [Test]
        public void ApplyValidation_PassingValidation_ReachesValidated()
        {
            var svc = new PublishReportService();
            var report = Draft(svc);

            svc.ApplyValidation(report, new AddressablesValidationResult(), "tests");

            Assert.AreEqual(PublishTransactionState.Validated, report.state);
            Assert.IsEmpty(report.errors);
        }

        [Test]
        public void ApplyBuildResult_Success_ReachesBuilt_AndRekeysOnContentHash()
        {
            var svc = new PublishReportService();
            var report = Draft(svc);
            svc.ApplyValidation(report, new AddressablesValidationResult(), "tests");
            svc.ApplyBuildStart(report, "tests");

            svc.ApplyBuildResult(report, new AddressablesBuildResult
            {
                success = true,
                outputPath = "Build/out",
                catalogHash = "cat-1",
                contentHash = "content-1",
                bundleSizeBytes = 42,
            }, "tests");

            Assert.AreEqual(PublishTransactionState.Built, report.state);
            Assert.AreEqual("content-1", report.artifacts.contentHash);
            Assert.AreEqual(
                PublishTransactionIdempotency.BuildKey(report.lab.tenantId, "lab-x", "v1", "content-1"),
                report.idempotencyKey);
        }

        [Test]
        public void Save_WritesJson_WithStableFields_AndFiltersPassedInfoChecks()
        {
            var svc = new PublishReportService();
            var report = Draft(svc);

            var validation = new AddressablesValidationResult();
            validation.Add(new PublishCheckEntry
            {
                code = "INFO-OK", severity = PublishCheckSeverity.Info, message = "fine", passed = true,
            });
            validation.Add(new PublishCheckEntry
            {
                code = "WARN-1", severity = PublishCheckSeverity.Warning, message = "careful", passed = true,
            });
            svc.ApplyValidation(report, validation, "tests");

            var config = UnityEngine.ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            try
            {
                config.localWorkspaceRoot = TmpWorkspace;

                var result = svc.Save(report, config);

                Assert.IsTrue(result.success, result.summary);
                string disk = Path.GetFullPath(result.jsonPath);
                Assert.IsTrue(File.Exists(disk), "report json not written: " + result.jsonPath);

                string json = File.ReadAllText(disk);
                // Stable identity fields present.
                StringAssert.Contains("\"labId\": \"lab-x\"", json);
                StringAssert.Contains("\"labVersionId\": \"v1\"", json);
                StringAssert.Contains("\"state\": \"" + PublishTransactionState.Validated + "\"", json);
                StringAssert.Contains(report.transactionId, json);
                // Filtering contract: a PASSED Info check is dropped; the Warning is kept.
                StringAssert.DoesNotContain("INFO-OK", json);
                StringAssert.Contains("WARN-1", json);
                // State history serialized.
                StringAssert.Contains("validation_passed", json);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
