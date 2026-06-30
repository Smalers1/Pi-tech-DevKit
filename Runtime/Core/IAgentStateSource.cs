using System.Collections.Generic;

namespace Pitech.XR.Core
{
    // ---------- IAgentStateSource: the VICKY-observe read seam (map sec-8) ----------
    // WS B2.6. A read-only structured-state seam so the agent layer (VICKY-observe, post-launch) can read
    // a lab's live numeric state - patient vitals first - WITHOUT coupling to any concrete model. Lives in
    // Pitech.XR.Core (the leaf) like the other cross-cutting seams (ILabEventBus / ILabStateStore), so a
    // producer (PatientVitals, Pitech.XR.Vitals) and a future consumer (Pitech.XR.AgentSubstrate) both
    // reference it one-way, never each other. READ-ONLY by contract: VICKY observes, it does not write
    // (the consent/role/audit gates govern any write path elsewhere).

    /// <summary>A read-only source of named numeric lab state for agent observation.</summary>
    public interface IAgentStateSource
    {
        /// <summary>Try to read one named value. Returns false if the key is not exposed.</summary>
        bool TryGetState(string key, out float value);

        /// <summary>Enumerate all exposed (key, value) pairs (a snapshot for observation).</summary>
        IEnumerable<KeyValuePair<string, float>> ReadState();
    }
}
