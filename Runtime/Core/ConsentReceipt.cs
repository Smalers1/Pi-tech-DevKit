using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// A consent receipt (map sec-11.5 / the EU AI Act Annex III lawful-basis trail): the assertion -
    /// carried from the HOST at launch - that this session's analytics processing is covered by a recorded
    /// consent. The DevKit does NOT collect consent: the decision is made upstream (Web Portal / enrolment,
    /// stored in the tenant's backend) and the host app (mobile RN / VR Shell), which holds the authenticated
    /// session, stamps this onto the <c>LaunchContext</c> at launch. It then rides <c>LabRuntimeContext</c>
    /// into <c>LabAnalytics</c>, which emits the session report ONLY when <see cref="IsGranted"/> is true and
    /// attaches this receipt to the report so the cloud has the audit trail. Absent / not-granted ->
    /// fail-closed (no report leaves the device; the on-device readout still shows locally).
    ///
    /// CROSS-SURFACE: this shape is part of the G2 wire contract (it serializes onto the session report) -
    /// confirm the field names with the Web Portal before the G2 freeze.
    /// </summary>
    [Serializable]
    public sealed class ConsentReceipt
    {
        /// <summary>The backend consent-record id this session is covered by. EMPTY = no consent recorded
        /// -> <see cref="IsGranted"/> is false -> fail-closed (nothing emitted). Set by the host at launch.</summary>
        public string consentId = string.Empty;

        /// <summary>The consent policy / terms version the user agreed to (for the audit trail). Set by the host.</summary>
        public string policyVersion = string.Empty;

        /// <summary>When consent was granted, ISO-8601 UTC (for the audit trail). Set by the host.</summary>
        public string grantedAtUtc = string.Empty;

        /// <summary>Granted iff a backend consent-record id is present. A receipt only exists for a recorded
        /// consent, so a non-empty id IS the grant; empty = absent / withdrawn -> fail-closed. (Derived, not
        /// serialized.)</summary>
        public bool IsGranted => !string.IsNullOrWhiteSpace(consentId);
    }
}
