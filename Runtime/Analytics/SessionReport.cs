using System;
using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The session report: the ONE self-contained wire document (map sec-11.5) ----------
    // WS B2.1. Supersedes the per-event AnalyticsEventV1 + Flow-A/B model. ONE report per session
    // (group/session-level, NOT per-participant docs): users+roles, the timed event stream, and the lab
    // rubric BUNDLED RAW (not pre-scored) so the cloud re-computes the grade if scoring changes. Stored
    // once, tenant-scoped (RLS); the cloud asserts report.tenantId == auth tenant, else reject.
    //
    // CROSS-SURFACE: the field shape here is the G2 wire contract handed to the Web Portal (B2.3). It is
    // PENDING Stergios' G2 review/confirm (2026-06-29) - treat the names/shape as proposed-frozen until
    // then. Per-individual scoring is out of scope (the lever exists: stamp events with userId). LMS
    // interop (xAPI/SCORM/cmi5/LTI) is deferred - VICKY is the system of record.

    /// <summary>One participant in a session and the analytics role they took (map sec-11.5).</summary>
    [Serializable]
    public sealed class SessionReportUser
    {
        public string userId = string.Empty;
        public SessionRole role = SessionRole.Participant;

        public SessionReportUser() { }
        public SessionReportUser(string userId, SessionRole role)
        {
            this.userId = userId ?? string.Empty;
            this.role = role;
        }
    }

    /// <summary>
    /// The complete session report. Envelope (tenant/session/lab/version) + users[] + the timed
    /// <see cref="events"/> stream + the bundled raw <see cref="rubric"/>. <see cref="isComplete"/>
    /// false = an unfinished session: stored as "incomplete", never lost, never "passed".
    /// </summary>
    [Serializable]
    public sealed class SessionReport
    {
        /// <summary>Report schema version (forward migration / the portable contract).</summary>
        public int schemaVersion = 1;

        // ---- Envelope (tenant isolation + attribution; map sec-11.5) ----
        public string tenantId = string.Empty;
        public string sessionId = string.Empty;
        public string labId = string.Empty;
        public string labVersion = string.Empty;

        /// <summary>False for a session that never reached SessionStop (crash/quit) - stored as
        /// "incomplete", re-computes to "incomplete", never a pass.</summary>
        public bool isComplete;

        /// <summary>The consent receipt this session's emission is covered by (P8 / EU AI Act lawful-basis
        /// audit trail). The recorder fail-closes when consent is absent, so on every EMITTED report this is
        /// present + granted - the cloud always has the consent record. Part of the G2 wire contract.</summary>
        public Pitech.XR.Core.ConsentReceipt consent;

        /// <summary>Users in this session and their analytics roles (Participant graded, Professor
        /// presence-only, Spectator excluded - the cloud derives per-participant views).</summary>
        public List<SessionReportUser> users = new List<SessionReportUser>();

        /// <summary>The shared timed flow + interaction stream (portable ms). Empty for a presence-only
        /// (Professor-only) report.</summary>
        public List<AnalyticsEvent> events = new List<AnalyticsEvent>();

        /// <summary>The lab rubric bundled RAW (not pre-scored) so the cloud re-computes. Null for a
        /// presence-only report.</summary>
        public LabRubric rubric;
    }
}
