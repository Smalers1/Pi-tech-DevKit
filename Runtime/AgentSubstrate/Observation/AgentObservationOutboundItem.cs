namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Envelope + surface pair held by <see cref="AgentObservationQueue"/>. The
    /// surface travels with the envelope so that drop-oldest + retry-after-backoff
    /// sends ship the correct `X-Vicky-Surface` header even when surfaces
    /// interleave or the pump runs against items enqueued from earlier frames.
    /// Auth tokens are NOT carried here — they are resolved fresh per send by
    /// the transport's <see cref="IAgentObservationAuthProvider"/> so an expired
    /// JWT does not survive backoff retries.
    /// </summary>
    public readonly struct AgentObservationOutboundItem
    {
        public readonly AgentObservationV1Envelope Envelope;
        public readonly string Surface;

        public AgentObservationOutboundItem(AgentObservationV1Envelope envelope, string surface)
        {
            Envelope = envelope;
            Surface = surface;
        }
    }
}
