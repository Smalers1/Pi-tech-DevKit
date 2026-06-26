using System;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// The Local (single-client) <see cref="ILabStateStore"/> impl (map sec-10.2): a scene-authored
    /// component on the scene-managers root, a narrow bool-view over a <see cref="LocalParamStore"/>.
    /// Lives in Pitech.XR.Networking (map sec-10.2), the always-compiled Local flavour alongside
    /// <c>LocalScenarioPath</c>; the Networked twin (<c>NetworkedLabStateStore : NetworkBehaviour</c>)
    /// is the <c>#if PITECH_HAS_FUSION</c> flavour here too. Resolve via <see cref="Find"/>
    /// (parent-walk, interface-typed so it finds either flavour). INERT in Phase B.1 - the type exists
    /// and compiles but is not wired into any scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalLabStateStore : MonoBehaviour, ILabStateStore
    {
        // The bool-view is sugar over the one param store. B.2 injects the lab's shared store via
        // Initialize; until then this owns a private Local store so the type is self-contained/inert.
        IParamStore _params = new LocalParamStore();

        public event Action<string> StateChanged;

        /// <summary>Back this view with the lab's shared param store (B.2 wiring).</summary>
        public void Initialize(IParamStore store)
        {
            if (store != null) _params = store;
        }

        public bool GetState(string id) => _params.GetBool(id);

        public void SetState(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;
            // Change-only: only fan out when the value actually changes (the underlying param store is
            // already change-only; keep this view consistent so listeners never see no-op writes).
            bool changed = _params.GetBool(id) != value;
            _params.SetBool(id, value);
            if (changed) StateChanged?.Invoke(id);
        }

        public void Toggle(string id) => SetState(id, !GetState(id));

        /// <summary>Resolve the nearest state store by walking up from <paramref name="from"/>'s
        /// hierarchy (includes inactive parents). Interface-typed so it resolves either the Local or
        /// the networked flavour. Null if none / destroyed.</summary>
        public static ILabStateStore Find(Component from)
            => from ? from.GetComponentInParent<ILabStateStore>(true) : null;
    }
}
