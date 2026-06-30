using UnityEngine;

namespace Pitech.XR.Core
{
    /// <summary>
    /// Per-attempt root context for a running lab (map sec-7). Owns the lab-scoped
    /// <see cref="ILabEventBus"/> and carries the attempt / lab-instance ids every fact is stamped
    /// with. Placed on the spawned lab root by ContentDelivery (which owns the attempt/launch
    /// lifecycle) and initialized via <see cref="Initialize"/>. Consumers resolve it by PARENT-WALK
    /// from their own transform (<see cref="Find"/>), never from the global <c>XRServices</c> map -
    /// a global bus reintroduces the multi-runner mis-bind class (map sec-5 / sec-7), true even for a
    /// single lab (stray / editor / additive runner objects).
    ///
    /// INERT in Phase B.1: ContentDelivery now DOES attach this on every spawned lab root (WS B1.1
    /// Step 2 has landed), and the runner DOES publish step.entered/step.completed facts to the bus
    /// (WS B1.7). It stays behaviour-neutral because the bus has ZERO subscribers at launch, so
    /// <see cref="LabEventBus.Publish"/> early-returns - the first subscriber (telemetry-on-bus,
    /// WS B1.1 Step 3 / Phase B.2) is what turns it live. No global lookups, so it is safe in the
    /// runtime package per the no-Find/-FindObjectsOfType rule.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabRuntimeContext : MonoBehaviour
    {
        [SerializeField, Tooltip("Id of this attempt (stamped on every fact). Set by ContentDelivery at lab spawn.")]
        string attemptId;

        [SerializeField, Tooltip("Id of this lab instance (stamped on every fact). Set by ContentDelivery at lab spawn.")]
        string labInstanceId;

        // ---- WS B2.1 (2026-06-29): the session-report identity ----
        // Additive: the analytics recorder (LabAnalytics, Pitech.XR.Analytics) reads tenant/user/lab/
        // version/session from HERE rather than referencing ContentDelivery (which would pull Addressables
        // into the analytics assembly). ContentDelivery's spawner populates these from the LaunchContext
        // at lab spawn via the new Initialize overload. Empty for menu/direct labs (no graded report).
        [SerializeField, Tooltip("Tenant (org) id - the report's tenant stamp (cloud asserts report tenant == auth tenant). Set by ContentDelivery from the LaunchContext.")]
        string tenantId;

        [SerializeField, Tooltip("User id for the session report's user list. Set by ContentDelivery from the LaunchContext.")]
        string userId;

        [SerializeField, Tooltip("Lab id (the report's lab stamp). Set by ContentDelivery from the LaunchContext.")]
        string labId;

        [SerializeField, Tooltip("Resolved lab version id (the report's version stamp). Set by ContentDelivery from the LaunchContext.")]
        string labVersion;

        [SerializeField, Tooltip("Session id - the group/session the report is stored under. Single-player: defaults to the attempt id; multiplayer shares one id across peers (B2.4).")]
        string sessionId;

        // ---- P8 (2026-06-30): consent receipt for the session-report emission gate ----
        // Carried from the LaunchContext by ContentDelivery's spawner (NOT [SerializeField] - it is set at
        // spawn, never authored in the inspector). LabAnalytics emits the report only when this is non-null
        // + IsGranted, and attaches it for the cloud audit trail. Null until stamped -> not-granted (fail-closed).
        ConsentReceipt consent;

        readonly LabEventBus _bus = new LabEventBus();

        // ---- P6 (2026-06-30): MP driver status for the analytics submission gate ----
        // Resolved lazily (first IsDriver read, after spawn) from the lab's flow store - the same internal
        // IScenarioFlowStore the runner binds; it lives in Core so no extra reference / InternalsVisibleTo is
        // needed. A null flow store = single-player = driver. LabAnalytics gates GRADED submission on IsDriver
        // so a follower (whose frontier-mirrored step facts have unreliable durations) never ships a wrong report.
        IScenarioFlowStore _flow;
        bool _flowResolved;

        /// <summary>The lab-scoped notification bus (one per attempt).</summary>
        public ILabEventBus Bus => _bus;

        /// <summary>The attempt this lab run belongs to (stamped on every fact).</summary>
        public string AttemptId => attemptId;

        /// <summary>The lab instance this run belongs to (stamped on every fact).</summary>
        public string LabInstanceId => labInstanceId;

        /// <summary>Tenant (org) id for the session report's tenant stamp (WS B2.1). Empty if unset.</summary>
        public string TenantId => tenantId;

        /// <summary>User id for the session report's user list (WS B2.1). Empty if unset.</summary>
        public string UserId => userId;

        /// <summary>Lab id for the session report's lab stamp (WS B2.1). Empty if unset.</summary>
        public string LabId => labId;

        /// <summary>Resolved lab version id for the session report's version stamp (WS B2.1). Empty if unset.</summary>
        public string LabVersion => labVersion;

        /// <summary>Session id the report is stored under (WS B2.1). Falls back to <see cref="AttemptId"/>
        /// when unset (single-player); multiplayer shares one id across peers (B2.4).</summary>
        public string SessionId => string.IsNullOrEmpty(sessionId) ? attemptId : sessionId;

        /// <summary>The consent receipt for this session's analytics emission (P8). Null until stamped by
        /// ContentDelivery from the LaunchContext; <c>LabAnalytics</c> treats null / not-granted as
        /// fail-closed (no report emitted), and attaches it (when granted) to the report for the audit trail.</summary>
        public ConsentReceipt Consent => consent;

        /// <summary>True when this peer DRIVES the run (single-player, or the MP authority); false for a
        /// follower (P6). A follower's frontier-mirrored step facts have unreliable durations, so
        /// <c>LabAnalytics</c> gates graded submission on this - only the driver ships the authoritative
        /// report. Resolves the flow store lazily on first read; a null flow store (single-player) -> driver.</summary>
        public bool IsDriver
        {
            get
            {
                if (!_flowResolved) { _flow = GetComponentInChildren<IScenarioFlowStore>(true); _flowResolved = true; }
                return _flow == null || _flow.IsDriver;
            }
        }

        /// <summary>Stamp the attempt / lab-instance ids at lab spawn (ContentDelivery, WS B1.1 Step 2).</summary>
        public void Initialize(string attempt, string labInstance)
        {
            attemptId = attempt;
            labInstanceId = labInstance;
        }

        /// <summary>WS B2.1: stamp the attempt / lab-instance ids AND the session-report identity
        /// (tenant / user / lab / version / session) at lab spawn. ContentDelivery calls this from the
        /// LaunchContext. <paramref name="session"/> empty -> <see cref="SessionId"/> falls back to the
        /// attempt id. <paramref name="consentReceipt"/> (P8) is the consent the report emission is gated on
        /// (null -> fail-closed).</summary>
        public void Initialize(string attempt, string labInstance, string tenant, string user,
            string lab, string version, string session = null, ConsentReceipt consentReceipt = null)
        {
            attemptId = attempt;
            labInstanceId = labInstance;
            tenantId = tenant;
            userId = user;
            labId = lab;
            labVersion = version;
            sessionId = session;
            consent = consentReceipt;
        }

        /// <summary>Resolve the nearest context by walking up from <paramref name="from"/>'s
        /// hierarchy (includes inactive parents). Returns null if none is found or the component is
        /// destroyed.</summary>
        public static LabRuntimeContext Find(Component from)
        {
            return from ? from.GetComponentInParent<LabRuntimeContext>(true) : null;
        }
    }
}
