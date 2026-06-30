using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    // ---------- UIStateTrigger (DevKit graduation, WS B2.7) ----------
    // The DevKit version of VR's UIStateTrigger: a bridge from any UnityEvent (button OnClick, animator
    // event, ...) to a named lab state. Graduated to resolve the store via
    // GetComponentInParent<ILabStateStore>() - NO static NetworkStateManager.Instance. The three methods
    // are UnityEvent-callable, exactly as the VR original.

    [AddComponentMenu("Pi tech/Networking/UI State Trigger")]
    public sealed class UIStateTrigger : MonoBehaviour
    {
        [Tooltip("The id of the state to change (a declared lab state). Pick from the dropdown.")]
        public string stateID;

        ILabStateStore _store;

        void OnEnable() => ResolveStore();

        /// <summary>UnityEvent-callable: set the state true.</summary>
        public void SetStateTrue()
        {
            if (string.IsNullOrEmpty(stateID)) return;
            ResolveStore();
            _store?.SetState(stateID, true);
        }

        /// <summary>UnityEvent-callable: set the state false.</summary>
        public void SetStateFalse()
        {
            if (string.IsNullOrEmpty(stateID)) return;
            ResolveStore();
            _store?.SetState(stateID, false);
        }

        /// <summary>UnityEvent-callable: toggle the state.</summary>
        public void ToggleState()
        {
            if (string.IsNullOrEmpty(stateID)) return;
            ResolveStore();
            _store?.Toggle(stateID);
        }

        void ResolveStore()
        {
            if (_store != null) return;
            _store = GetComponentInParent<ILabStateStore>(true);
        }
    }
}
