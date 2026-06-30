using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>How a <see cref="PhysicsStateTrigger"/> changes its state on collider enter/exit.</summary>
    public enum PhysicsTriggerMode
    {
        SetTrue,
        SetFalse,
        Toggle
    }

    // ---------- PhysicsStateTrigger (DevKit graduation, WS B2.7) ----------
    // The DevKit version of VR's PhysicsStateTrigger: a trigger collider writes a named lab state on
    // enter/exit. Graduated to resolve the state store via GetComponentInParent<ILabStateStore>() (the
    // Local store in single-player, the Fusion store in multiplayer) - NO static NetworkStateManager
    // .Instance. Same enter/exit semantics as the VR original. The post-B2 VR migration re-wires the
    // labs from VR's component onto this one.

    [AddComponentMenu("Pi tech/Networking/Physics State Trigger")]
    public sealed class PhysicsStateTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [Tooltip("The id of the state to change (a declared lab state). Pick from the dropdown.")]
        public string stateID;

        [Tooltip("SetTrue/SetFalse: enter sets, exit undoes. Toggle: enter flips, exit does nothing.")]
        public PhysicsTriggerMode mode = PhysicsTriggerMode.SetTrue;

        [Header("Collider Filter")]
        [Tooltip("Match any collider with this tag.")]
        public string targetTag = "Player";

        [Tooltip("Optional - if assigned, only this exact collider triggers the state. Tag is ignored.")]
        public Collider specificCollider;

        ILabStateStore _store;

        void OnEnable() => ResolveStore();

        bool Matches(Collider other)
        {
            return specificCollider != null ? other == specificCollider : other.CompareTag(targetTag);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!Matches(other) || string.IsNullOrEmpty(stateID)) return;
            ResolveStore();
            if (_store == null) return;
            switch (mode)
            {
                case PhysicsTriggerMode.SetTrue: _store.SetState(stateID, true); break;
                case PhysicsTriggerMode.SetFalse: _store.SetState(stateID, false); break;
                case PhysicsTriggerMode.Toggle: _store.Toggle(stateID); break;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!Matches(other) || string.IsNullOrEmpty(stateID)) return;
            ResolveStore();
            if (_store == null) return;
            switch (mode)
            {
                case PhysicsTriggerMode.SetTrue: _store.SetState(stateID, false); break;
                case PhysicsTriggerMode.SetFalse: _store.SetState(stateID, true); break;
                case PhysicsTriggerMode.Toggle: break;
            }
        }

        void ResolveStore()
        {
            if (_store != null) return;
            _store = GetComponentInParent<ILabStateStore>(true);
        }
    }
}
