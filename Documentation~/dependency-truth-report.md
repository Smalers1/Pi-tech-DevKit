# Dependency-truth report — `PITECH_*` defines vs asmdefs vs actual usage

**WS A7 Step 6** (Phase A plan). Generated 2026-06-11 by static sweep of every `.asmdef`,
`package.json`, `link.xml`, and `#if PITECH_*` / API-usage grep over all `.cs`. The
*with/without-package compile matrix* is deliberately NOT run here — that is the Phase D
(post-launch) cutover's job; this report records the current truth so the matrix has a baseline.

## 1. Per-define truth table

| Define | Declared in (`versionDefines`) | Watches package | Expr | `.cs` usage | Verdict |
|---|---|---|---|---|---|
| `PITECH_ADDR` | `Pitech.XR.ContentDelivery`, `Pitech.XR.ContentDelivery.Editor` | `com.unity.addressables` | `1.0.0` | Heavy: `AddressablesRemoteUrlRewriter` (4), `ContentDeliverySpawner` (6), `AddressablesService` (17), `AddressablesBuildService` (4), `AddressablesValidationService` (4), `ContentDeliverySpawnerEditor` (2) | **Healthy.** Every file using real Addressables API is guarded (see §2). |
| `PITECH_CCD` | same two asmdefs | `com.unity.services.ccd.management` | `1.0.0` | **0 files** | **Dormant vocabulary.** CCD handling (`TryParseCcdUrl` etc.) is pure string parsing — no CCD SDK API in the package, so nothing needs the guard. Keep: it is the seam a future CCD-SDK feature compiles into. |
| `PITECH_HAS_META_INTERACTION` | `Pitech.XR.Interactables`, `Pitech.XR.Interactables.Editor` | `com.meta.xr.sdk.interaction` | *(any)* | **0 files** | **Dormant vocabulary.** No Meta SDK API anywhere in the package (consistent: also zero hard refs, §2). `MetaSelectRelay` works through Unity-only surfaces. |
| `PITECH_HAS_FUSION` | `Pitech.XR.Interactables`, `Pitech.XR.Interactables.Editor`, `Pitech.XR.Networking` | `com.exitgames.photon.fusion` | *(any)* | 2 (`NetworkingModule.cs` — WS A2 reserved-slot stub) | **Dormant + stub.** Interactables declares but never uses it (forward slot). No Fusion API in the package. |
| `PITECH_HAS_LOCALIZATION` | `Pitech.XR.Localization` | `com.unity.localization` | *(any)* | 2 (`LocalizationModule.cs` — stub) | **Stub only** (WS A2 reserved slot). |
| `PITECH_HAS_TESTFRAMEWORK` | `Pitech.XR.Core.Editor` | `com.unity.test-framework` | `1.0.0` | 5 sites: `DevKitChecks.cs` (2), `EvaluateChanges.cs` (2), `MaintainPage.cs` (1) | **Healthy** (added WS A3). The whole gate UI/runner compiles out for consumers without the Test Framework; the Maintain page shows an install hint instead. |

## 2. Hard references (asmdef `references`) vs optionality

- `Pitech.XR.ContentDelivery` → `Pitech.XR.Core`, **`Unity.Addressables`**, **`Unity.ResourceManager`**, `Unity.TextMeshPro`, `Unity.ugui` (named form).
- `Pitech.XR.ContentDelivery.Editor` → `Pitech.XR.Core`, `Pitech.XR.ContentDelivery`, **`Unity.Addressables.Editor`** (named form).
- `Pitech.XR.Interactables` → GUID refs resolved (2026-06-11, against the HealthOn VR package cache): `Pitech.XR.Core`, `Unity.TextMeshPro`, `Unity.InputSystem`. **No Meta SDK, no Fusion.**
- `Pitech.XR.Interactables.Editor` → GUID refs resolved: `Pitech.XR.Interactables`, `Pitech.XR.Core.Editor`, `Unity.InputSystem`. **No Meta SDK, no Fusion.**
- `Pitech.XR.Networking` / `.Localization` / `.Analytics` / `.Vitals` → `Pitech.XR.Core` only (stubs).

**Reading:** the Addressables refs are belt-and-braces alongside `PITECH_ADDR`: Unity silently skips
a *named* asmdef reference that does not resolve, and with the package absent the define is off, so
all API usage compiles out. `Unity.ResourceManager` is **correctly kept** (plan A7 Step 4): the
runtime types (`AsyncOperationHandle`, locators) live there, not in `Unity.Addressables`.

**Un-guarded-usage check (the failure mode this report exists to catch):** every `.cs` matching
real Addressables API (`AddressableAssets`/`IResourceLocator`/`AsyncOperationHandle`) also contains
`PITECH_ADDR` — except `ContentDeliveryCapability.cs`, which only holds the **string type name**
`"…AddressableAssetSettings, Unity.Addressables.Editor"` for reflection-based capability probing —
correctly un-guarded by design. **No violation found.**

## 3. `package.json`

**No `dependencies` block — intentional.** Optionality is real at the manifest level: installing the
DevKit pulls nothing. The deps block + the Unity `6000.x` floor bump are ONE Phase D (post-launch)
cutover (spec §28.6); do not add either piecemeal. Current floor: `"unity": "2022.3"` (consumers run
Unity 6).

## 4. `link.xml` cross-check

`Runtime/link.xml` preserves all of: Core, ContentDelivery, Scenario, Interactables, Stats, Quiz.
The four reserved-module stubs (Analytics, Localization, Networking, Vitals) and AgentSubstrate are
**not** listed — fine today (no serialized/reflection-instantiated types), but any module that gains
`[SerializeReference]` or by-name-instantiated types MUST be added before it ships in an IL2CPP
build (see serialization-and-reflection-notes.md §6 — never narrow `preserve="all"`).

## 5. Open items for the Phase D matrix (not Phase A work)

1. Empirically compile the package **without** `com.unity.addressables` (and without Test
   Framework) in a throwaway project — confirms the named-ref-skip + define-off path end to end.
2. When the deps block lands (Phase D cutover), decide which of Addressables / ResourceManager /
   InputSystem / TMP become declared dependencies vs stay optional.
3. If a CCD SDK feature ever lands, `PITECH_CCD` is its seam; until then the define stays dormant.
