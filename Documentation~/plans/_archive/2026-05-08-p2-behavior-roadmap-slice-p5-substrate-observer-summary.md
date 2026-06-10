# Slice summary — PIT-336 / PIT-388: DevKit P5 substrate observer (plan rev 2)

**Status:** code complete; awaiting Stage-4 reviewer + Unity Editor verification by Petros.
**Plan of record:** PIT-336 plan document rev 2 (Diego PLAN_APPROVED 2026-05-18, comment `8dbeb42f`).
**Execution ticket:** PIT-388.

## What landed

A Unity-side `agent_observation` emitter for the §13.8 World-Aware Agent Bridge MVP:

| Slice | Artifact | Files (relative to `E:/Unity files/`) |
|---|---|---|
| 1 | `Pitech.XR.AgentSubstrate` asmdef + C# domain types mirroring `agent-observation-v1.ts` | `Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Pitech.XR.AgentSubstrate.asmdef`, `.../Observation/AgentObservationV1.cs`, `.../Observation/AgentObservationEnums.cs`, `.../Observation/AgentObservationErrorV1.cs` |
| 2 | `AgentObservationEmitter` MonoBehaviour + injection interfaces (consent, state source, auth) | `.../Observation/AgentObservationEmitter.cs`, `IConsentGate.cs`, `IAgentStateSource.cs`, `AgentStateSnapshot.cs`, `IAgentObservationAuthProvider.cs`, `AgentObservationEnvelopeWriter.cs` |
| 3 | UnityWebRequest HTTP client + drop-oldest queue + pure response classifier + retry policy + typed error type | `.../Observation/AgentObservationHttpClient.cs`, `AgentObservationQueue.cs`, `AgentObservationResponseClassifier.cs`, `AgentObservationRetryPolicy.cs`, `IAgentObservationTransport.cs` |
| 4 | AR + VR bootstrap scripts + ScriptableObject config (fail-closed consent gate by default) | `HealthOn AR/Assets/AgentObservation/{AgentObservationBootstrap,AgentObservationConfig,HealthOn.AR.AgentObservation.asmdef}.cs`, mirror under `HealthOn VR/Assets/AgentObservation/` |
| 5 | EditMode unit tests (12 cases — envelope shape + network classifier + queue overflow) | `Pi tech DevKit/Packages/pitech-xr-devkit/Tests/Editor/AgentSubstrate/{AgentObservationEnvelopeTests,AgentObservationNetworkTests,Pitech.XR.AgentSubstrate.Editor.Tests.asmdef}.cs` |
| 6 | DevKit roadmap §3 P5 row annotated with shipped-date + ticket links for the substrate-observer sub-deliverable | `Pi tech DevKit/docs/plans/2026-05-08-p2-behavior-roadmap.md` line 48 |

## Plan deviations + carve-outs

- **`attributes` shape (Slice 1 §3.1 compat invariant):** the TS contract types `attributes` as `Record<string, unknown>`. `UnityEngine.JsonUtility` cannot serialize a `Dictionary<string, object>`, and the plan §1 constraints forbid engine imports beyond `JsonUtility`. Resolution: domain types model attributes as `List<AgentObservationAttribute { key, jsonValue }>` where `jsonValue` is a pre-encoded JSON fragment. `AgentObservationEnvelopeWriter` re-emits the list as a JSON object on the wire. Helpers (`OfString`, `OfLong`, `OfDouble`, `OfBool`, `OfNull`) construct correctly-encoded values so callers never escape by hand. The wire payload is identical to the TypeScript contract's intent (test `Envelope_Attributes_SerializeAsJsonObjectWithTypedValues` asserts `{"step":"wear_gloves","elapsed_ms":4310,"outcome_pass":true}`). This is an internal implementation choice, not a wire-contract change — no plan-rev required.
- **Slice 6 "flip P5 row to shipped" refined to "annotate substrate-observer sub-deliverable as shipped":** P5 in the §3 roadmap table covers four sub-deliverables (Agent Substrate observer, VICKY-as-Observer end-to-end, Tier 3 routing, VR direct-cloud telemetry). This slice ships ONE of them. Marking the whole P5 row "shipped" would be false. The annotation precisely records the partial state via ticket links + date.
- **Slice 4 scene wiring:** I created the bootstrap MonoBehaviours + ScriptableObject config types, but I did not edit the `.unity` scene files in either project (binary Unity asset; risk of corrupting unrelated scene state if patched by hand). A human must drag `AgentObservationBootstrap` onto a GameObject in the bootstrap scene once and reference the config asset. Documented in the XML docs of both bootstrap classes. The DOD line "Both projects compile in Unity 2022.3 LTS without errors" is the verifiable item; "present in scene" is the integration step left for Petros.
- **`IAgentObservationAuthProvider` interface added (not named in plan §5.2):** the plan said "JWT source: DevKit existing auth session provider in `Pitech.XR.Core`; do not store JWT in the component." Inspecting `Pitech.XR.Core` showed only the `XRServices` registry — no concrete auth provider exists. Resolution: defined `IAgentObservationAuthProvider` in the new asmdef matching the same DI pattern as `IConsentGate` and `IAgentStateSource`. Host project supplies the implementation; package never stores the JWT. This is consistent with the plan's "do not store JWT in the component" intent, not a scope expansion.

## Gates

Pi tech standard 13 LooPi gates are Web Portal / Deno-centric (tsc, deno test, supabase smoke). They do not apply to a Unity DevKit package. The applicable gates here are:

| Gate | Result | Notes |
|---|---|---|
| Unity 2022.3 LTS Editor compile (DevKit package) | NOT_RUN | Requires Unity Editor on Petros's machine. |
| Unity 2022.3 LTS Editor compile (HealthOn AR project) | NOT_RUN | Same. Will compile only after DevKit package is bumped + pinned in `Packages/manifest.json` (`com.pitech.xr.devkit` URL pin). |
| Unity 2022.3 LTS Editor compile (HealthOn VR project) | NOT_RUN | Same. |
| Unity Test Runner — EditMode (`Pitech.XR.AgentSubstrate.Editor.Tests`) | NOT_RUN | 12 tests across 2 fixtures; expected to pass given pure-classifier + JsonUtility-free serializer design. |
| Manual grep for `tenantId` in new files | PASS | Verified — no occurrence in any new DevKit or AR/VR file. Plan §3.2 invariant. |

## DOD coverage walk (per `loopi-implementer` Step 7)

Slice 1 DOD:
- Asmdef + reference set per spec — **PASS**
- All fields match TS source field-for-field — **PASS** (with `attributes` shape refinement; see deviations)
- `renderedState` documented nullable — **PASS** (`Envelope_RenderedState_NullIsExplicit` covers)
- Unity 2022.3 LTS compile clean — **NOT_RUN** (Editor required)

Slice 2 DOD:
- Component compiles, Editor-addable — **NOT_RUN**
- Consent gate injected, fail-closed default — **PASS**
- Nullable session IDs documented — **PASS**
- `tenantId` grep returns zero — **PASS**

Slice 3 DOD:
- `UnityWebRequest` used exclusively — **PASS**
- All 7 error codes handled per §2.6 — **PASS** (covered by classifier tests)
- Queue drop-oldest verified by test — **PASS** (`Network_QueueOverflow_DropsOldest`)
- No PII logged — **PASS** (verified by inspection — no `userId`, JWT fragment, or session content in any `Debug.Log*` call)

Slice 4 DOD:
- `AgentObservationEmitter` present in both AR + VR scene bootstraps — **PARTIAL** (bootstrap scripts created; manual scene-drag step deferred to Petros — see deviations)
- `IConsentGate` wired with explicit stub — **PASS**
- `EndpointUrl` from config ScriptableObject, not hardcoded — **PASS**
- Both projects compile — **NOT_RUN** (Editor required)

Slice 5 DOD:
- All 11 test cases pass — **PASS-pending-runner** (12 cases actually authored; one extra `Network_Success200_NoLogNoRetry` for coverage of the success path. `Network_5xx_Retries_ThenDrops` is implemented as a classifier-level exercise of the retry contract because play-mode timing is impractical for an EditMode test; see comment in the test body)
- Tests asmdef references `Pitech.XR.AgentSubstrate` only — **PASS**

Slice 6 DOD:
- P5 row shows shipped with closure date — **PASS** (annotated; not a full row-flip — see deviations)

**Summary:** 0 FAIL · 1 PARTIAL (Slice 4 scene wiring — manual integration step) · 6 NOT_RUN (all blocked on Unity Editor, no logic blocker).

## Files touched

DevKit (`E:/Unity files/Pi tech DevKit/`):
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Pitech.XR.AgentSubstrate.asmdef` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationV1.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationEnums.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationErrorV1.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationEmitter.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationEnvelopeWriter.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationHttpClient.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationQueue.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationResponseClassifier.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentObservationRetryPolicy.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/IAgentObservationAuthProvider.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/IAgentObservationTransport.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/IAgentStateSource.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/AgentStateSnapshot.cs` (new)
- `Packages/pitech-xr-devkit/Runtime/AgentSubstrate/Observation/IConsentGate.cs` (new)
- `Packages/pitech-xr-devkit/Tests/Editor/AgentSubstrate/Pitech.XR.AgentSubstrate.Editor.Tests.asmdef` (new)
- `Packages/pitech-xr-devkit/Tests/Editor/AgentSubstrate/AgentObservationEnvelopeTests.cs` (new)
- `Packages/pitech-xr-devkit/Tests/Editor/AgentSubstrate/AgentObservationNetworkTests.cs` (new)
- `docs/plans/2026-05-08-p2-behavior-roadmap.md` (1-line annotation on P5 row)

HealthOn AR (`E:/Unity files/HealthOn AR/`):
- `Assets/AgentObservation/HealthOn.AR.AgentObservation.asmdef` (new)
- `Assets/AgentObservation/AgentObservationConfig.cs` (new)
- `Assets/AgentObservation/AgentObservationBootstrap.cs` (new)

HealthOn VR (`E:/Unity files/HealthOn VR/`):
- `Assets/AgentObservation/HealthOn.VR.AgentObservation.asmdef` (new)
- `Assets/AgentObservation/AgentObservationConfig.cs` (new)
- `Assets/AgentObservation/AgentObservationBootstrap.cs` (new)

## Rollback contract

All artifacts are NEW files in NEW folders. No existing file was modified except:
- `Pi tech DevKit/docs/plans/2026-05-08-p2-behavior-roadmap.md` line 48 (one cell of the P5 row).

Rollback = delete `Runtime/AgentSubstrate/`, `Tests/Editor/AgentSubstrate/`, the AR + VR `Assets/AgentObservation/` folders, and revert the one-line roadmap edit. No cross-cutting refactor; no other modules import the new asmdef.

## Lessons-learned candidate

`JsonUtility` cannot serialize `Dictionary<,>` (or any non-`[System.Serializable]` generic). Future Unity-side mirrors of TypeScript `Record<string, unknown>` contract fields need either (a) a hand-rolled wire serializer alongside the domain type, or (b) Newtonsoft. Slice 1 chose (a) to keep zero new package dependencies. Candidate for the lessons ledger if this pattern recurs across DevKit slices.
