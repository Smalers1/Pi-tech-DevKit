using System.Runtime.CompilerServices;

// WS B2.4 Step 4: expose Pitech.XR.Scenario internals to the EditMode test assembly so the internal
// RoutedParamStore can be unit-tested headless (the rest of B2.4's Fusion data plane needs a device).
// Test-only friend grant - no public-API change, so Proof B is unaffected.
[assembly: InternalsVisibleTo("Pitech.XR.Scenario.Editor.Tests")]

// WS B2.7 S2: expose internals to the Scenario editor assembly so LabConsoleEditor can read the runtime
// param store (the internal LabConsole.Params) for the play-mode LIVE VALUES readout, without adding a
// public accessor. The editor is a trusted friend; no public-API change.
[assembly: InternalsVisibleTo("Pitech.XR.Scenario.Editor")]
