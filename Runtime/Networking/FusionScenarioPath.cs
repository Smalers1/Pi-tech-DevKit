#if PITECH_HAS_FUSION || FUSION_WEAVER
using System;
using Fusion;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// The networked (Fusion) <see cref="IScenarioFlowStore"/> impl (map sec-10): the multiplayer twin
    /// of <see cref="LocalScenarioPath"/>. An append-only, replicated path of ENTERED step guids. The
    /// STATE AUTHORITY appends (the driver); non-authority peers (followers) RPC the authority to append
    /// and track the frontier (<see cref="Last"/>) to jump - so a branch records itself and a stale
    /// local stat can't diverge peers (first-completion-wins, no decider).
    ///
    /// Replicated as a <c>[Networked, Capacity(256)]</c> ring (NetworkDictionary keyed by sequence index
    /// modulo capacity) + a networked count - the map's "ring of recent entries; sync only needs the
    /// FRONTIER" (sec-10.4). Mirrors <c>NetworkedLabStateStore</c>'s proven shape (NetworkDictionary +
    /// Render-diff + an authority-gated RPC) so it compiles wherever that does.
    ///
    /// Gated <c>#if PITECH_HAS_FUSION || FUSION_WEAVER</c> (UPM Fusion OR VR's embedded Fusion); the
    /// Fusion DLLs are auto-referenced (asmdef overrideReferences:false). INTERNAL like the seam
    /// (off the Proof-B surface until Phase E). Add it to a networked lab's root; LabConsole resolves
    /// and binds it. INERT until a networked session runs (single-player binds nothing).
    /// </summary>
    [AddComponentMenu("Pi tech/Networking/Fusion Scenario Path")]
    [DisallowMultipleComponent]
    internal sealed class FusionScenarioPath : NetworkBehaviour, IScenarioFlowStore
    {
        const int Cap = 256;   // matches map sec-10.4 (the recent-frontier ring)

        // Ring of entered guids keyed by (sequence index % Cap); EnteredCount is the running total.
        [Networked, Capacity(Cap)]
        NetworkDictionary<int, NetworkString<_64>> Entered { get; }

        [Networked]
        int EnteredCount { get; set; }

        // Non-networked mirror so Render raises Changed exactly once per real growth (same contract the
        // Local twin honours via AppendEntered).
        int _mirrorCount;

        public event Action Changed;

        /// <summary>The state authority drives the path; everyone else follows.</summary>
        public bool IsDriver => Object != null && Object.HasStateAuthority;

        public int Count => EnteredCount;

        public string Last
        {
            get
            {
                if (EnteredCount <= 0) return string.Empty;
                return Entered.TryGet((EnteredCount - 1) % Cap, out NetworkString<_64> v) ? v.ToString() : string.Empty;
            }
        }

        public string GetEntered(int index)
        {
            // Only the most recent Cap entries are retained (the ring). Older indices are gone.
            if (index < 0 || index >= EnteredCount) return string.Empty;
            if (index < EnteredCount - Cap) return string.Empty;
            return Entered.TryGet(index % Cap, out NetworkString<_64> v) ? v.ToString() : string.Empty;
        }

        public void AppendEntered(string stepGuid)
        {
            if (string.IsNullOrEmpty(stepGuid)) return;   // ids clamped to 64 chars by NetworkString<_64>
            if (Object.HasStateAuthority) AppendOnAuthority(stepGuid);
            else RPC_RequestAppend(stepGuid);
        }

        void AppendOnAuthority(string stepGuid)
        {
            Entered.Set(EnteredCount % Cap, stepGuid);
            EnteredCount = EnteredCount + 1;
        }

        public override void Render()
        {
            // Runs on every client after state sync. Raise Changed once per real growth so followers
            // advance the frontier exactly when it moves (no polling).
            if (EnteredCount != _mirrorCount)
            {
                _mirrorCount = EnteredCount;
                Changed?.Invoke();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestAppend(string stepGuid) => AppendOnAuthority(stepGuid);
    }
}
#endif
