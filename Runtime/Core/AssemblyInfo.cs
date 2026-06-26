using System.Runtime.CompilerServices;

// IScenarioFlowStore is INTERNAL at launch (map sec-7): DevKit-only, off the Proof-B public surface,
// reshapeable until it graduates to a public API post-launch (Phase E). These grants let the runner
// (Scenario) use the seam and the transport impls (Networking) implement it, without exposing it.
// Additive; nothing here changes the public API surface.
[assembly: InternalsVisibleTo("Pitech.XR.Scenario")]
[assembly: InternalsVisibleTo("Pitech.XR.Networking")]
