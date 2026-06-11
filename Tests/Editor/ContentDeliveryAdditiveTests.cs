using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    /// <summary>
    /// WS A3 Step 6 - additive EditMode coverage for ContentDelivery pure logic, on UNMODIFIED code.
    /// Complements the existing PublishTransaction/Addressables tests rather than duplicating them:
    /// the <see cref="PublishTransactionStateMachine.CanTransition"/> PREDICATE directly (incl. terminal
    /// states + null handling, which the end-to-end TryTransition tests don't isolate), and the
    /// <see cref="AddressablesRemoteUrlRewriter"/> "leave non-CCD / empty input unchanged" contract
    /// (marker-agnostic; the positive CCD-rewrite path is covered by AddressablesServiceBuildCcdRemoteLoadPathTests).
    /// </summary>
    public class ContentDeliveryAdditiveTests
    {
        // --- PublishTransactionStateMachine.CanTransition ------------------------------------

        [Test]
        public void CanTransition_AllowsAStepOnTheBuildPath()
            => Assert.IsTrue(PublishTransactionStateMachine.CanTransition(
                PublishTransactionState.Draft, PublishTransactionState.Validating));

        [Test]
        public void CanTransition_RejectsASkipAheadTransition()
            => Assert.IsFalse(PublishTransactionStateMachine.CanTransition(
                PublishTransactionState.Draft, PublishTransactionState.Built));

        [Test]
        public void CanTransition_TerminalStatesGoNowhere()
        {
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition(
                PublishTransactionState.Activated, PublishTransactionState.Draft));
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition(
                PublishTransactionState.Cancelled, PublishTransactionState.Validating));
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition(
                PublishTransactionState.FailedTerminal, PublishTransactionState.Draft));
        }

        [Test]
        public void CanTransition_RejectsNullEmptyAndUnknownStates()
        {
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition(null, PublishTransactionState.Validating));
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition(PublishTransactionState.Draft, null));
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition("", ""));
            Assert.IsFalse(PublishTransactionStateMachine.CanTransition("NotARealState", PublishTransactionState.Draft));
        }

        // --- AddressablesRemoteUrlRewriter identity / no-op contracts ------------------------

        [Test]
        public void RewriteUrl_ReturnsInputUnchanged_ForNullOrEmpty()
        {
            Assert.IsNull(AddressablesRemoteUrlRewriter.RewriteUrl(null, "bucket", "https://base/"));
            Assert.AreEqual("", AddressablesRemoteUrlRewriter.RewriteUrl("", "bucket", "https://base/"));
        }

        [Test]
        public void RewriteUrl_ReturnsInputUnchanged_WhenBucketOrBaseMissing()
        {
            const string url = "https://example.com/whatever";
            Assert.AreEqual(url, AddressablesRemoteUrlRewriter.RewriteUrl(url, null, "https://base/"));
            Assert.AreEqual(url, AddressablesRemoteUrlRewriter.RewriteUrl(url, "bucket", null));
            Assert.AreEqual(url, AddressablesRemoteUrlRewriter.RewriteUrl(url, "", ""));
        }

        [Test]
        public void RewriteUrl_LeavesNonCcdUrlsAlone()
        {
            const string url = "https://cdn.example.com/catalogs/catalog_v1.json?ts=42";
            Assert.AreEqual(url, AddressablesRemoteUrlRewriter.RewriteUrl(url, "anybucket", "https://other/"));
        }

        [Test]
        public void TryParseCcdUrl_FailsCleanly_OnNonCcdUrl()
        {
            bool ok = AddressablesRemoteUrlRewriter.TryParseCcdUrl(
                "https://cdn.example.com/foo/bar", out var bucket, out var releaseBase, out var tail);
            Assert.IsFalse(ok);
            Assert.IsNull(bucket);
            Assert.IsNull(releaseBase);
            Assert.IsNull(tail);
        }

        [Test]
        public void TryParseCcdUrl_FailsCleanly_OnNullOrEmpty()
        {
            Assert.IsFalse(AddressablesRemoteUrlRewriter.TryParseCcdUrl(null, out _, out _, out _));
            Assert.IsFalse(AddressablesRemoteUrlRewriter.TryParseCcdUrl("", out _, out _, out _));
        }
    }
}
