using System;
using UnityEngine;
using UnityEngine.Events;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    // ---------- EventStateListener (DevKit graduation, WS B2.7) ----------
    // The DevKit version of VR's EventStateListener. Graduated to SUBSCRIBE to ILabStateStore.StateChanged
    // (no per-frame polling, unlike the VR original's Update loop) and resolve the store via
    // GetComponentInParent<ILabStateStore>() - NO static NetworkStateManager.Instance. Fires
    // OnStateActive / OnStateInactive when its state's value changes (and once on bind for the initial
    // value), honouring invertLogic.

    [AddComponentMenu("Pi tech/Networking/Event State Listener")]
    public sealed class EventStateListener : MonoBehaviour
    {
        [Tooltip("The id of the state to listen to (a declared lab state). Pick from the dropdown.")]
        public string stateID;

        [Tooltip("If true, OnStateActive fires when the state is FALSE and vice-versa.")]
        public bool invertLogic;

        public UnityEvent OnStateActive = new UnityEvent();
        public UnityEvent OnStateInactive = new UnityEvent();

        ILabStateStore _store;
        bool _subscribed;
        bool _initialized;
        bool _lastValue;

        void OnEnable() => Bind();

        void Start()
        {
            // Retry once in Start in case the store attached after OnEnable (spawn ordering).
            if (!_subscribed) Bind();
        }

        void OnDisable()
        {
            if (_store != null && _subscribed) _store.StateChanged -= OnStateChanged;
            _subscribed = false;
        }

        void Bind()
        {
            if (_subscribed) return;
            if (_store == null) _store = GetComponentInParent<ILabStateStore>(true);
            if (_store == null) return;
            _store.StateChanged += OnStateChanged;
            _subscribed = true;
            Evaluate(force: true);   // emit the initial state
        }

        void OnStateChanged(string id)
        {
            if (!string.Equals(id, stateID, StringComparison.Ordinal)) return;
            Evaluate(force: false);
        }

        void Evaluate(bool force)
        {
            if (_store == null || string.IsNullOrEmpty(stateID)) return;
            bool active = _store.GetState(stateID);
            if (invertLogic) active = !active;
            if (!force && _initialized && active == _lastValue) return;
            _lastValue = active;
            _initialized = true;
            if (active) OnStateActive.Invoke();
            else OnStateInactive.Invoke();
        }
    }
}
