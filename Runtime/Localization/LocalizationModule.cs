// RESERVED MODULE - Localization. Phase A WS A2 Step 7 reserves the assembly (+ an
// Author-group Hub tile) ONLY. This slot is intentionally EMPTY and emits nothing.
//
// Logic lands in PHASE B (WS B7): keyed Greek + English, build-baked; the cloud
// translation pipeline is after-launch. Gated by the PITECH_HAS_LOCALIZATION versionDefine
// (com.unity.localization) - guard real code with #if PITECH_HAS_LOCALIZATION; never take a
// hard dependency on the Localization package. Spec sec-28.3.
//
// Reference direction: this module may reference Pitech.XR.Core / Pitech.XR.Scenario,
// NEVER the reverse.
namespace Pitech.XR.Localization
{
}
