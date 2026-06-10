// RESERVED MODULE - Networking ("Make Multiplayer"). Phase A WS A2 Step 7 reserves the
// assembly (+ a Setup-group Hub tile) ONLY. This slot is intentionally EMPTY and emits
// nothing - reserving the boundary is the deliverable, not the logic.
//
// Logic lands AFTER LAUNCH: NetworkedStates -> the IScenarioFlowStore graduation, plus
// the "Make Multiplayer" editor automation. Gated by the PITECH_HAS_FUSION versionDefine
// (com.exitgames.photon.fusion) - guard real code with #if PITECH_HAS_FUSION; never take a
// hard Fusion dependency. Spec sec-28.2.
//
// Reference direction: this module may reference Pitech.XR.Core / Pitech.XR.Scenario,
// NEVER the reverse (it sits above them in the DAG).
namespace Pitech.XR.Networking
{
}
