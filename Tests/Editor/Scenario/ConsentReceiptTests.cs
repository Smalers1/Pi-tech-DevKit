using NUnit.Framework;
using Pitech.XR.Core;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the P8 consent contract: the receipt's granted-derivation (a non-empty
    /// consentId IS the grant - empty/whitespace = fail-closed) and the G2 wire shape (the consent block on
    /// the serialized session report). Pure value-object tests (no scene). The fail-closed EMISSION gate
    /// itself lives in LabAnalytics (a MonoBehaviour) and is exercised in Stergios' play-mode pass; here we
    /// lock the data contract the gate and the cloud audit trail depend on.
    /// </summary>
    [TestFixture]
    public sealed class ConsentReceiptTests
    {
        [Test]
        public void IsGranted_FalseWhenConsentIdEmptyOrWhitespace()
        {
            Assert.IsFalse(new ConsentReceipt().IsGranted, "default (empty) receipt must be not-granted (fail-closed)");
            Assert.IsFalse(new ConsentReceipt { consentId = "   " }.IsGranted, "whitespace consentId must be not-granted");
        }

        [Test]
        public void IsGranted_TrueWhenConsentIdPresent()
        {
            Assert.IsTrue(new ConsentReceipt { consentId = "c_123" }.IsGranted);
        }

        [Test]
        public void Json_SerializesConsentBlock_WhenPresent()
        {
            var report = new SessionReport
            {
                consent = new ConsentReceipt
                {
                    consentId = "c_123",
                    policyVersion = "v2",
                    grantedAtUtc = "2026-06-30T10:00:00Z"
                }
            };

            string json = SessionReportJson.Serialize(report);

            StringAssert.Contains("\"consent\":{", json);
            StringAssert.Contains("\"consentId\":\"c_123\"", json);
            StringAssert.Contains("\"policyVersion\":\"v2\"", json);
            StringAssert.Contains("\"grantedAtUtc\":\"2026-06-30T10:00:00Z\"", json);
        }

        [Test]
        public void Json_WritesNullConsent_WhenAbsent()
        {
            var report = new SessionReport { consent = null };
            string json = SessionReportJson.Serialize(report);
            StringAssert.Contains("\"consent\":null", json);
        }
    }
}
