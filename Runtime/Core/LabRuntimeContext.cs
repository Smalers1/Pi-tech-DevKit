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

        readonly LabEventBus _bus = new LabEventBus();

        /// <summary>The lab-scoped notification bus (one per attempt).</summary>
        public ILabEventBus Bus => _bus;

        /// <summary>The attempt this lab run belongs to (stamped on every fact).</summary>
        public string AttemptId => attemptId;

        /// <summary>The lab instance this run belongs to (stamped on every fact).</summary>
        public string LabInstanceId => labInstanceId;

        /// <summary>Stamp the attempt / lab-instance ids at lab spawn (ContentDelivery, WS B1.1 Step 2).</summary>
        public void Initialize(string attempt, string labInstance)
        {
            attemptId = attempt;
            labInstanceId = labInstance;
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
