using System;

namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Source of agent runtime state snapshots. The emitter subscribes to
    /// <see cref="ObservationReady"/> rather than polling per-frame.
    /// Implementations live in host-project bootstrap code; the DevKit package
    /// ships no concrete runtime source at v1.
    /// </summary>
    public interface IAgentStateSource
    {
        event Action<AgentStateSnapshot> ObservationReady;

        /// <summary>Optional pull path for tests and on-demand snapshots.</summary>
        AgentStateSnapshot GetCurrentState();
    }
}
