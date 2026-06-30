using System.IO;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Analytics
{
    // ---------- DebugSessionReportSink: a dev/test outbox (map sec-11.5) ----------
    // WS B2.1 (P4). A DEV/TEST ISessionReportSink: on Submit it logs a one-line summary and writes the raw
    // JSON wire payload to Application.persistentDataPath/<subfolder>/. It is NOT the production outbox - no
    // durable queue, no retry, no restart survival (offline durability is host-owned; see ISessionReportSink).
    // Use it to eyeball session reports while building a lab.
    //
    // TWO MODES:
    //   (a) Drag this onto LabAnalytics.reportSink (it is a MonoBehaviour implementing ISessionReportSink).
    //       Set selfRegister = false for this mode.
    //   (b) Leave selfRegister = true: it self-registers in XRServices as THE ISessionReportSink, so any
    //       LabAnalytics resolves it (LabAnalytics.ResolveSink -> XRServices.TryGet<ISessionReportSink>).
    //
    // NOTE: XRServices has no per-type unregister, so OnDisable cannot remove just this sink (see below) -
    // the registration persists until another sink overwrites it (Register) or the host calls ShutdownAll.

    /// <summary>
    /// A dev/test <see cref="ISessionReportSink"/> that logs each report and writes its JSON under
    /// <c>Application.persistentDataPath</c>. NOT the production outbox. Either assign it to
    /// <c>LabAnalytics.reportSink</c> or leave <see cref="selfRegister"/> on to register it in
    /// <see cref="XRServices"/>.
    /// </summary>
    [AddComponentMenu("Pi tech/Analytics/Debug Session Report Sink")]
    [DisallowMultipleComponent]
    public sealed class DebugSessionReportSink : MonoBehaviour, ISessionReportSink
    {
        [Header("Dev sink")]
        [Tooltip("When true, self-registers in XRServices as the ISessionReportSink on enable (mode b). Set false if you instead assign this to LabAnalytics.reportSink (mode a).")]
        public bool selfRegister = true;

        [Tooltip("Subfolder under Application.persistentDataPath where report .json files are written.")]
        public string subfolder = "session-reports";

        [Tooltip("Also log a one-line summary of each submitted report.")]
        public bool alsoLog = true;

        // Deterministic fallback id when a report has no sessionId (NOT a wall-clock timestamp).
        int _counter;

        void OnEnable()
        {
            if (selfRegister)
            {
                // MUST pin <ISessionReportSink>: a bare Register(this) would key on the concrete type and
                // LabAnalytics' TryGet<ISessionReportSink> would never find it.
                XRServices.Register<ISessionReportSink>(this);
                if (alsoLog) Debug.Log("[DebugSink] registered as ISessionReportSink (dev sink - not a durable outbox).", this);
            }
        }

        void OnDisable()
        {
            // XRServices has NO per-type unregister (only Register/Get/TryGet/InitializeAll/ShutdownAll), so
            // this sink cannot remove just itself here. The registration persists until another sink
            // overwrites it via Register or the host calls ShutdownAll. We deliberately do NOT call
            // ShutdownAll (it would tear down every host service). Documented, not silently bailed.
        }

        // --- ISessionReportSink / IXRService ---

        /// <summary>Write the report JSON to disk and (optionally) log a summary. Idempotent on the session
        /// id (same id -&gt; same file, overwritten).</summary>
        public void Submit(SessionReport report, string json)
        {
            if (report == null)
            {
                if (alsoLog) Debug.LogWarning("[DebugSink] null report; nothing written.", this);
                return;
            }

            string folder = string.IsNullOrEmpty(subfolder) ? "session-reports" : subfolder;
            string dir = Path.Combine(Application.persistentDataPath, folder);
            string fileBase = SanitizeFileName(string.IsNullOrEmpty(report.sessionId)
                ? "session-" + (++_counter)
                : report.sessionId);
            string path = Path.Combine(dir, fileBase + ".json");

            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(path, json ?? string.Empty);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);   // loud, not silent
                return;
            }

            if (alsoLog)
                Debug.Log(
                    $"[DebugSink] session={report.sessionId} tenant={report.tenantId} lab={report.labId} " +
                    $"complete={report.isComplete} users={(report.users != null ? report.users.Count : 0)} " +
                    $"events={(report.events != null ? report.events.Count : 0)} -> {path}", this);
        }

        /// <summary>No-op: a dev file writer has no boot lifecycle.</summary>
        public void Initialize() { }

        /// <summary>No-op: invoked by the host via <see cref="XRServices.ShutdownAll"/>.</summary>
        public void Shutdown() { }

        static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                name = name.Replace(invalid[i], '_');
            return name;
        }
    }
}
