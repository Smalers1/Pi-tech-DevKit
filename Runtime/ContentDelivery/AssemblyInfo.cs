using System.Runtime.CompilerServices;

// InternalsVisibleTo keeps internal validation logic OUT of the package's public API surface
// (Proof B baseline) while letting the EditMode net lock its behaviour (WS A3 Step 6 -
// LaunchContextValidation tests). Mirrors the AgentSubstrate pattern.
[assembly: InternalsVisibleTo("Pitech.XR.ContentDelivery.Editor.Tests")]
