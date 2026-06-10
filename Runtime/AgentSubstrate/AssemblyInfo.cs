using System.Runtime.CompilerServices;

// Grant the EditMode test assembly access to internal types of
// Pitech.XR.AgentSubstrate — specifically AgentObservationEnvelopeWriter, the
// deliberately-internal hand-rolled wire serializer (see its header comment:
// "Internal-only — no public API surface; the emitter uses it."). The envelope
// tests assert the exact wire JSON, so they must call the real serializer.
// InternalsVisibleTo keeps the writer OUT of the package's public API surface
// rather than promoting it to public just to satisfy a test.
[assembly: InternalsVisibleTo("Pitech.XR.AgentSubstrate.Editor.Tests")]
