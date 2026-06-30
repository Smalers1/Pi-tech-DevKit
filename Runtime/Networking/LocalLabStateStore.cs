using System;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// An optional, standalone <see cref="ILabStateStore"/> bool-view (map sec-10.2): a scene-authored component that
    /// is a NARROW boolean view over the lab's ONE <see cref="IParamStore"/>. It owns NO state of its own -
    /// GetState/SetState/Toggle are sugar over GetBool/SetBool on the backing store, and
    /// <see cref="StateChanged"/> simply forwards the store's <c>ParamChanged</c>. So a trigger (writer) and
    /// a ConditionsStep / effect (reader, via <c>LabConsole.Params</c>) read and write the SAME data.
    ///
    /// BACKING STORE: until <see cref="Initialize"/> is called it owns a private <see cref="LocalParamStore"/>
    /// so the type is self-contained / testable. <see cref="LabConsole"/> calls Initialize (through the
    /// <see cref="IParamStoreBackedState"/> seam) with the lab's shared store on Start, wiring this view to
    /// the same store the runner uses. Despite the name, this single component covers BOTH single-player and
    /// multiplayer: it is a plain (non-networked) MonoBehaviour, and replication comes from the BACKING store
    /// - back it with a networked/routed store (NetworkedParamStore) and its named bools replicate. The
    /// standalone networked switchboard (<c>NetworkedLabStateStore</c>, the VR NetworkStateManager drop-in)
    /// remains for migration; new networked labs use this view + a NetworkedParamStore. NOTE (P5): the root
    /// LabConsole now implements ILabStateStore directly, so most labs need NO separate component - use this
    /// only for a console-less scene, or a sub-tree that needs its own bool-view. Resolve via
    /// <see cref="Find"/> / <c>GetComponentInParent&lt;ILabStateStore&gt;()</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalLabStateStore : MonoBehaviour, ILabStateStore, IParamStoreBackedState
    {
        // The backing store. Defaults to a private Local store; LabConsole injects the lab's shared store
        // (Local or networked/routed) via Initialize. This view never owns named state directly.
        IParamStore _params = new LocalParamStore();
        // The store we are currently subscribed to (so a swap unsubscribes cleanly - no leak / double-fire).
        IParamStore _subscribed;

        public event Action<string> StateChanged;

        void OnEnable() => Resubscribe();
        void OnDisable() => Unsubscribe();
        void OnDestroy() => Unsubscribe();

        /// <summary>Back this view with the lab's shared param store (B.2 wiring). Moves the change
        /// subscription to the new store so StateChanged keeps firing for the right source.</summary>
        public void Initialize(IParamStore store)
        {
            if (store == null || ReferenceEquals(store, _params)) return;
            _params = store;
            Resubscribe();
        }

        public bool GetState(string id) => _params.GetBool(id);

        public void SetState(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            // Change-only fan-out is the store's job (LocalParamStore.SetBool only raises ParamChanged on a
            // real change); StateChanged is raised by forwarding that event, so a no-op write stays silent.
            _params.SetBool(id, value);
        }

        public void Toggle(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _params.SetBool(id, !_params.GetBool(id));
        }

        // Forward the backing store's change to listeners (filtered by id on their side). Covers BOTH a
        // local write and a replicated change arriving from a networked backing store - one uniform path.
        void OnParamChanged(string id) => StateChanged?.Invoke(id);

        void Resubscribe()
        {
            Unsubscribe();
            if (!isActiveAndEnabled) return;   // OnEnable will (re)subscribe once active
            _params.ParamChanged += OnParamChanged;
            _subscribed = _params;
        }

        void Unsubscribe()
        {
            if (_subscribed != null) { _subscribed.ParamChanged -= OnParamChanged; _subscribed = null; }
        }

        /// <summary>Resolve the nearest state store by walking up from <paramref name="from"/>'s
        /// hierarchy (includes inactive parents). Interface-typed so it resolves either the Local or
        /// the networked flavour. Null if none / destroyed.</summary>
        public static ILabStateStore Find(Component from)
            => from ? from.GetComponentInParent<ILabStateStore>(true) : null;
    }
}
