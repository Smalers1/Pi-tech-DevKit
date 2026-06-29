// MODULE - Localization. Phase A WS A2 Step 7 reserved the assembly (+ an Author-group Hub tile).
//
// WS B1.5 (Step 3 "lookup seam" + Step 4 "merge seam, logic only") landed the PURE runtime SEAM in
// this assembly - all package-free so it compiles in the bare gate and is INERT by default:
//   - ILocalizationLookup      : key -> localized text contract
//   - LocalizeAttribute        : marks data-asset / code-literal strings for the keying scan
//   - LocalizationServices     : static install point + passthrough resolve facade (off at launch)
//   - CompositeLocalizationLookup : overlay-then-base merge chain
//
// STILL DEFERRED to the Unity/VR loop (need the package or a VR GUID-carry move, unverifiable here):
//   - the StringTable-backed ILocalizationLookup impl (needs com.unity.localization) - guard with
//     #if PITECH_HAS_LOCALIZATION (versionDefine already on the asmdef); never hard-depend.
//   - relocating the VR Editor/Localization pipeline (LocalizationPipeline/ManualTranslationIO/
//     LocalizationScanManifest/LocalizationPipelineWindow + LanguageSwitcher/DoNotLocalize) by
//     GUID-carry, and extending the scan to [Localize] data-asset/code-literal members.
//   See Documentation~/plans/2026-06-26-phase-b1-structural.md WS B1.5 EXECUTION HANDOFF.
//
// Reference direction: this module may reference Pitech.XR.Core / Pitech.XR.Scenario,
// NEVER the reverse.
namespace Pitech.XR.Localization
{
}
