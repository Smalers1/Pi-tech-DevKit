namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Snapshot of the agent runtime state at observation time. Built by the
    /// IAgentStateSource implementation registered by the host project; the emitter
    /// turns it into a wire envelope. Session/lab identifiers are nullable because
    /// the substrate may observe outside of a scored lab attempt.
    /// </summary>
    public sealed class AgentStateSnapshot
    {
        /// <summary>One of <see cref="AgentObservationKindV1"/> constants.</summary>
        public string Kind;

        public AgentObservationSemanticStateV1 SemanticState;

        /// <summary>Null when the emitter has no rendered representation.</summary>
        public AgentObservationRenderedStateV1 RenderedState;

        public string LabId;
        public string LabVersionId;
        public string AttemptId;
        public string SessionId;
    }
}
