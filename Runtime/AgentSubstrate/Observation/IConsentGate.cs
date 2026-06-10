namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Client-side consent gate. The emitter calls <see cref="IsConsentGranted"/>
    /// before every emission; a false result causes a silent drop (Verbose log only,
    /// not Warning/Error). Mirrors the server-side fail-closed C3 LOCK behaviour and
    /// prevents useless network traffic while PIT-NEW-A consent storage is in flight.
    /// </summary>
    public interface IConsentGate
    {
        bool IsConsentGranted();
    }

    /// <summary>Default fail-closed gate. Always returns false.</summary>
    public sealed class DenyAllConsentGate : IConsentGate
    {
        public bool IsConsentGranted() => false;
    }
}
