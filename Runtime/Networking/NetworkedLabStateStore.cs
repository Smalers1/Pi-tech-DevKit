#if PITECH_HAS_FUSION || FUSION_WEAVER
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// The Networked (Fusion) <see cref="ILabStateStore"/> impl - the DevKit graduation of VR's
    /// <c>NetworkStateManager</c> (decision B1.3 S5, 2026-06-29: (b) graduate into the package, so the
    /// networked-state switchboard is one shared, reusable building block for every consumer, the same
    /// way labs already compose Scenario / Stats / Quiz from the DevKit).
    ///
    /// A shared switchboard of up to 64 named booleans over a <c>[Networked]</c> NetworkDictionary,
    /// replicated to every client. Writers (triggers) set authority-gated with an RPC fallback;
    /// readers (listeners) subscribe to <see cref="StateChanged"/> (raised on EVERY client in Render
    /// when a value actually changes - no polling).
    ///
    /// Gated <c>#if PITECH_HAS_FUSION || FUSION_WEAVER</c> so it enables wherever Fusion is present: a
    /// UPM package install defines PITECH_HAS_FUSION, and VR's EMBEDDED Fusion defines FUSION_WEAVER
    /// (the flag VR's own XRShared.Core.asmdef gates on). The Fusion runtime DLLs are auto-referenced
    /// (asmdef overrideReferences:false) so no asmdef reference is needed; the type compiles out where
    /// Fusion is absent (bare gate / AR). Resolved via
    /// <c>GetComponentInParent&lt;ILabStateStore&gt;()</c> / <see cref="LocalLabStateStore.Find"/> -
    /// NO static Instance (the map's "kill the static Instance"; the VR <c>[Obsolete]</c> facade keeps
    /// an Instance forwarding here for hand-wired callers during migration). INERT at launch
    /// (multiplayer turns on in B.2).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedLabStateStore : NetworkBehaviour, ILabStateStore
    {
        [Header("Setup")]
        [Tooltip("States set true on spawn by the state authority.")]
        public List<string> defaultStates = new List<string>();

#if UNITY_EDITOR
        [Header("Debug (read-only)")]
        [SerializeField] List<string> _activeStatesDebug = new List<string>();
#endif

        // 64-state / 64-char cap mirrors VR's NetworkStateManager (the relocated body).
        [Networked, Capacity(64)]
        NetworkDictionary<NetworkString<_64>, NetworkBool> GameStates { get; }

        // Non-networked mirror so Render raises StateChanged exactly once per real change (the bool-view
        // contract the Local twin also honours). GameStates only grows here, so add/update is enough.
        readonly Dictionary<string, bool> _mirror = new Dictionary<string, bool>();

        public event Action<string> StateChanged;

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;
            for (int i = 0; i < defaultStates.Count; i++)
            {
                string s = defaultStates[i];
                if (!string.IsNullOrEmpty(s)) GameStates.Set(s, true);
            }
        }

        public bool GetState(string id)
            => !string.IsNullOrEmpty(id) && GameStates.TryGet(id, out NetworkBool v) && v;

        public void SetState(string id, bool value)
        {
            if (string.IsNullOrEmpty(id)) return;          // ids are clamped to 64 chars by NetworkString<_64>
            if (Object.HasStateAuthority) GameStates.Set(id, value);
            else RPC_RequestSetState(id, value);
        }

        public void Toggle(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            // Resolve the flip ON the authority (RPC) so two near-simultaneous toggles can't race.
            if (Object.HasStateAuthority) GameStates.Set(id, !GetState(id));
            else RPC_RequestToggle(id);
        }

        // Back-compat API mirroring VR's NetworkStateManager 1:1 so the [Obsolete] facade and any
        // direct caller map straight through during migration.
        public void SetStateTrue(string id) => SetState(id, true);
        public void SetStateFalse(string id) => SetState(id, false);
        public void ToggleState(string id) => Toggle(id);

        public override void Render()
        {
#if UNITY_EDITOR
            _activeStatesDebug.Clear();
#endif
            // Runs on every client after state sync. Struct enumerator (<=64 entries) -> no GC.
            foreach (var kvp in GameStates)
            {
                string key = kvp.Key.ToString();
                bool val = kvp.Value;
#if UNITY_EDITOR
                if (val) _activeStatesDebug.Add(key);
#endif
                if (!_mirror.TryGetValue(key, out bool prev) || prev != val)
                {
                    _mirror[key] = val;
                    StateChanged?.Invoke(key);
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestSetState(string id, bool value) => GameStates.Set(id, value);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestToggle(string id) => GameStates.Set(id, !GetState(id));
    }
}
#endif
