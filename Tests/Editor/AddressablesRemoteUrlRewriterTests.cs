using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    /// <summary>
    /// Pure-logic coverage for <see cref="AddressablesRemoteUrlRewriter"/>'s CCD URL parse + rewrite
    /// (gate-runnable - no Addressables package required, these methods are always compiled).
    ///
    /// The install/uninstall LIFECYCLE that carries the B1.8 clobber fix (capture -> chain -> restore
    /// the host's <c>InternalIdTransformFunc</c>) lives under <c>#if PITECH_ADDR</c> and touches the
    /// Addressables ResourceManager, so it is NOT exercised here: this test assembly defines neither
    /// PITECH_ADDR nor an Addressables reference. That regression is owned by the host RN app's UaaL
    /// test (B1.8 Step 2, cross-surface) - the host app asserts its own transform survives a DevKit
    /// install/uninstall cycle.
    /// </summary>
    public class AddressablesRemoteUrlRewriterTests
    {
        const string CcdUrl =
            "https://proj.client-api.unity3dusercontent.com/client_api/v1/environments/production/buckets/BUCKET-1/release_by_badge/latest/entry_by_path/content/?path=foo.bundle";

        const string TargetReleaseBase =
            "https://proj.client-api.unity3dusercontent.com/client_api/v1/environments/production/buckets/BUCKET-1/releases/REL-9/entry_by_path/content/";

        [Test]
        public void TryParseCcdUrl_ValidUrl_ExtractsBucketReleaseBaseAndTail()
        {
            Assert.IsTrue(AddressablesRemoteUrlRewriter.TryParseCcdUrl(CcdUrl, out var bucket, out var releaseBase, out var tail));
            Assert.AreEqual("BUCKET-1", bucket);
            StringAssert.EndsWith("/entry_by_path/content/", releaseBase);
            Assert.AreEqual("?path=foo.bundle", tail);
        }

        [Test]
        public void TryParseCcdUrl_NonCcdOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(AddressablesRemoteUrlRewriter.TryParseCcdUrl("https://example.com/some/file.bundle", out _, out _, out _));
            Assert.IsFalse(AddressablesRemoteUrlRewriter.TryParseCcdUrl(null, out _, out _, out _));
            Assert.IsFalse(AddressablesRemoteUrlRewriter.TryParseCcdUrl("", out _, out _, out _));
        }

        [Test]
        public void RewriteUrl_MatchingBucket_RepointsReleaseBaseKeepsTail()
        {
            string result = AddressablesRemoteUrlRewriter.RewriteUrl(CcdUrl, "BUCKET-1", TargetReleaseBase);
            Assert.AreEqual(TargetReleaseBase + "?path=foo.bundle", result);
        }

        [Test]
        public void RewriteUrl_MatchingBucketCaseInsensitive_StillRewrites()
        {
            string result = AddressablesRemoteUrlRewriter.RewriteUrl(CcdUrl, "bucket-1", TargetReleaseBase);
            Assert.AreEqual(TargetReleaseBase + "?path=foo.bundle", result);
        }

        [Test]
        public void RewriteUrl_DifferentBucket_PassesThroughUnchanged()
        {
            Assert.AreEqual(CcdUrl, AddressablesRemoteUrlRewriter.RewriteUrl(CcdUrl, "OTHER-BUCKET", TargetReleaseBase));
        }

        [Test]
        public void RewriteUrl_AlreadyAtTargetReleaseBase_PassesThroughUnchanged()
        {
            string atTarget = TargetReleaseBase + "?path=foo.bundle";
            Assert.AreEqual(atTarget, AddressablesRemoteUrlRewriter.RewriteUrl(atTarget, "BUCKET-1", TargetReleaseBase));
        }

        [Test]
        public void RewriteUrl_NonCcdUrl_PassesThroughUnchanged()
        {
            const string local = "file:///data/local/x.bundle";
            Assert.AreEqual(local, AddressablesRemoteUrlRewriter.RewriteUrl(local, "BUCKET-1", TargetReleaseBase));
        }

        [Test]
        public void RewriteUrl_NullOrEmptyArgs_ReturnUrlUnchanged()
        {
            Assert.AreEqual(CcdUrl, AddressablesRemoteUrlRewriter.RewriteUrl(CcdUrl, null, TargetReleaseBase));
            Assert.AreEqual(CcdUrl, AddressablesRemoteUrlRewriter.RewriteUrl(CcdUrl, "BUCKET-1", null));
            Assert.IsNull(AddressablesRemoteUrlRewriter.RewriteUrl(null, "BUCKET-1", TargetReleaseBase));
        }
    }
}
