namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Abstraction over the HTTP send path so the emitter is testable without
    /// touching UnityWebRequest. AgentObservationHttpClient is the production
    /// implementation; tests substitute a fake recorder.
    ///
    /// Auth tokens are NOT carried on this seam: the transport implementation
    /// resolves a fresh token per send via its own
    /// <see cref="IAgentObservationAuthProvider"/> so that an expired JWT does
    /// not survive a backoff retry. Surface, however, IS carried per-call
    /// because the wire envelope's surface field must equal the
    /// `X-Vicky-Surface` header sent with it.
    /// </summary>
    public interface IAgentObservationTransport
    {
        void Send(AgentObservationV1Envelope envelope, string surface);
    }
}
