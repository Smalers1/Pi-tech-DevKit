namespace Pitech.XR.Analytics
{
    // ---------- The outbox seam: where a finished report goes (map sec-11.5) ----------
    // WS B2.1. Offline durability is HOST-OWNED (decision 2026-06-23): the VR Shell / mobile app owns a
    // durable, disk-backed outbox (persist -> retry-on-reconnect -> survive restart). The DevKit only
    // hands the report across this seam; it does NOT own the queue (a DevKit-owned outbox is the
    // post-launch unification if hosts diverge). An unfinished session is submitted with
    // SessionReport.isComplete == false - stored as "incomplete", never lost, never "passed".
    //
    // The host registers its sink via XRServices (or wires it onto LabAnalytics). If none is registered,
    // LabAnalytics logs a warning and drops the report (so a mis-set-up scene is loud, not silent).

    /// <summary>The host-provided outbox a finished <see cref="SessionReport"/> is handed to.</summary>
    public interface ISessionReportSink : Pitech.XR.Core.IXRService
    {
        /// <summary>Persist + ship the report. MUST be durable (survive restart) and idempotent on the
        /// report's session id (a report may be re-submitted on flush/relaunch). <paramref name="json"/>
        /// is the pre-serialized wire payload (the G2 contract) so the host transports bytes without
        /// re-serializing; <paramref name="report"/> is the live object for any host-side inspection.</summary>
        void Submit(SessionReport report, string json);
    }
}
