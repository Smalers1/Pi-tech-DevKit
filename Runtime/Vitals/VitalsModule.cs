// Vitals module marker. Phase A reserved the assembly. WS B2.6 (2026-06-29) landed the FOUNDATION
// (CAN_TRAIL, decision 41):
//   - Vital              - a typed value + an optional 3D binding (Timeline speed / Animator param / field)
//   - PatientVitals      - the single typed model, param-store-backed, implements IAgentStateSource
//   - IAgentStateSource  - the VICKY-observe read seam (lives in Pitech.XR.Core)
// The full digital twin (cascade rules, profiles, the ControlOptionManager PUN->Fusion convergence,
// scene migration) is post-launch. Wiring a real 3D binding into a lab scene is the author-side/post-B2
// step; the code path is delivered here.
//
// Reference direction: this assembly references Pitech.XR.Core ONLY (it must not depend on Scenario -
// PatientVitals owns its own LocalParamStore, so there is no LabConsole/Scenario coupling).
namespace Pitech.XR.Vitals
{
}
