---
title: Phase B.2 - hardening / author-readiness patch plan (Stergios review 2026-06-30)
date: 2026-06-30
author: Claude Code
status: PLAN. Applied: P0, P1, P2, P3, P4, P5, P6, P7, P8, P10, P11; P9 resolved (no script needed). Remaining: P12, P13. (P2 extended 2026-06-30: in-graph Step Analytic authoring on the Scenario Graph nodes - see P2. "Rubric" UI label renamed to "Analytics config".) ITER 4 (2026-06-30): the inspector Objectives->Analytics->Metrics reorg is DONE, plus id/label hidden, 0-1 weight sliders, a Warning/Error band editor, "Tracked Objects" rename, and TWO node-sizing bug fixes - see the ITER 4 note below.
ITER 2 (2026-06-30, Stergios redesign): StepAnalytic is now a WHITE collapsible "lego brick" on top of the step node with its metrics edited INLINE (replaces the badge + the inspector-config flow); LabRubric.schemaVersion hidden ([HideInInspector], field kept); graph LabAnalytics resolution is forward-compatible with a dedicated "Analytics" child object. PENDING (deferred, each its own pass): (a) move LabAnalytics off the LabConsole GO onto an "Analytics" GameObject - needs the LabRuntimeContext bus-sharing handled (Find = parent-walk; safe in production as a child of the lab root, dev/menu editor-test needs care); (b) DONE 2026-06-30 backward-compatibly - new shared SceneRootNames (Core.Editor): CREATE uses "--- SCENE SETUP ---", all FIND/health/category checks accept EITHER name via IsManagersRoot (SceneManagerEditor keeps a local legacy const, different assembly), UI copy updated. VALIDATED by 7+ existing fixture prefabs still named "--- SCENE MANAGERS ---" that now resolve via the fallback; MegaFixtureBuilder + golden graph snapshots intentionally keep the legacy name (a backward-compat fixture); (c) inspector "Analytics config" reorg to the Objectives -> Analytics -> Metrics hierarchy (mermaid sec-11.0).
ITER 3 (2026-06-30): DONE (a) PLACEMENT - the graph "Add Step Analytic" flow now creates LabAnalytics on a dedicated "Analytics" CHILD of the lab root, and GetOrAdds a LabRuntimeContext on the ROOT so the bus stays shared (runner parent-walks from the console; recorder parent-walks from the child; ContentDelivery GetOrAdds + stamps the same context at spawn - confirmed line 621). Standard "console = lab root" structure assumed. DONE visual refinements: (1) StepAnalytic metrics now edit in a WINDOW (new StepAnalyticEditWindow, mirrors StepEditWindow) opened from the brick's "Edit..." button - the old inline metrics dropdown is gone; the brick is a compact white indicator row (ANALYTIC + Edit + a bigger, vertically-centered X). (2) Node auto-sizing: the colored header is pinned (flexShrink/flexGrow = 0) so it keeps a fixed height when Settings open, and a collapsed step that owns an analytic reserves the brick height (GetCollapsedHeight = GetBaseCollapsedHeight + AnalyticBrickHeight); the body still extends downward (height Auto) per existing SetPosition logic. Adversarially verified ok; NOT compiled (Unity pass eyeballs node sizing + the dev/menu bus). REMAINING: only (c) the inspector Objectives->Analytics->Metrics reorg.
ITER 4 (2026-06-30): inspector reorg (c) DONE + author-friendliness refinements + 2 sizing bug fixes. (1) `LabRubricBuilder.cs` rewritten top-down OBJECTIVES -> ANALYTICS -> METRICS; each objective shows its analytics' metrics read-only (the tree is visible); StepAnalytics are READ-ONLY here (authored in the graph), only SceneAnalytics editable; the raw `rubric` is no longer double-drawn (`OnInspectorGUI` now `DrawPropertiesExcluding(..., "rubric")`, keeping the wiring fields). (2) id + label HIDDEN everywhere (auto-assigned unique id + sensible label) EXCEPT a **Signal metric's id** (it is the link `AnalyticsSignalEmitter` matches, so it stays visible/editable). (3) weights are 0-1 SLIDERS via `[Range(0,1)]` on `AnalyticsMetric.weight`, `Objective.weight`, `ObjectiveInput.subWeight` (attribute-only; no API/baseline change). (4) ScoringBands replaced by a kind-aware **Warning/Error toggle** editor (each: penalty 0-1 slider + notify-in-scene; + a seconds threshold for DURATION kinds; count kinds hide the unused threshold; the `None` band is kept in DATA for the engine/tests but hidden) - in BOTH `StepAnalyticEditWindow` and the inspector. `ScoringBand.DefaultBands()` UNCHANGED (3 bands, tests + grade engine depend on it). (5) "Subjects" relabeled **"Tracked Objects"** - UI label only; field `subjects` + class `TrackedSubject` + API baseline UNCHANGED. (6) TWO node-sizing bugs in `StepNode`: the analytic brick is now `flexShrink = 0` (no longer squeezed, so the colored header stops being shoved up); and a manually-resized node no longer FREEZES auto-sizing - manual WIDTH stays fixed but manual HEIGHT becomes a `minHeight` FLOOR with `height = Auto`, and a `_resizing` flag suppresses auto-fit ONLY during the live handle drag (re-fits on mouse-up). U8 (objective target as time): NOT a schema change - time limits live on the Total Duration metric's bands (seconds); the objective pass-bar stays 0-1 (explained; can add a time label later if wanted). Adversarially verified ok (0 blockers); NOT compiled - Stergios' Unity pass eyeballs the inspector + node sizing. Questions answered this turn: Signal metric (authored-failure count, matched by id), TotalDuration setup (a Scene Analytic, in the inspector), Auto-detect/Auto-wire.
ITER 5 (2026-06-30): PLACEMENT CORRECTED to SIBLING + LabConsole shows an Analytics link. The graph "Add Step Analytic" flow was creating the "Analytics" GameObject as a CHILD of the LabConsole (Stergios: "still inside the lab console gameobject"); it now creates it as a **SIBLING** (under the console's PARENT = the lab root). Because a sibling can't parent-walk into the console, the `LabRuntimeContext` moved from the console to the **lab ROOT** (`anchor.root`) - the COMMON ANCESTOR of the console and the sibling - so the shared event bus still resolves by parent-walk (and matches where `ContentDeliverySpawner` stamps it at spawn, line 621, avoiding a double/empty context). `FindOrCreateAnalyticsChild` -> `FindOrCreateAnalyticsSibling(anchor)`; the resolve now searches the whole lab-root subtree (`anchor.root.GetComponentInChildren<LabAnalytics>`). ALSO: the LabConsole inspector (`SceneManagerEditor`) gained an **Analytics** section - a RESOLVED link (Select/Ping) to the lab's LabAnalytics (LabConsole holds no serialized ref - the recorder self-resolves the bus), or an "Add Lab Analytics" button using the same sibling + root-context placement. Note: existing CHILD-placed Analytics from ITER 3/4 are REUSED, not auto-migrated - delete the old child "Analytics" object to get the sibling. Edge case: a LabConsole that is a scene ROOT (no parent) gets a root-level sibling whose dev bus can't share by parent-walk (production is fine - spawner stamps the prefab root); labs normally have a parent so this is moot. Asmdef already refs Analytics+Core; ASCII-clean; no dangling refs. NOT compiled - Stergios' Unity pass.
scope: turn B.2 from "runtime scaffolding + partial editor helpers" into an author-ready feature slice.
exclusions: the Analytics SAMPLE SCENE is explicitly out of scope (Stergios' call).
---

# B.2 hardening plan

Stergios reviewed B.2 and (correctly) called it runtime scaffolding + partial editor helpers, not an
author-ready release. This is the patch plan. **Code is ground truth**; below each finding is reconciled
against the actual code first, then a concrete patch. UI-bearing patches include "build this object / attach
this script" instructions; editor-tooling patches are scripts I author (no scene work for you).

## A. Status reconciliation (what's already true vs the review)

| # | Finding | Real status (verified) |
|---|---|---|
| H3 | LabConsole params not shown (SceneManagerEditor doesn't draw them) | **FIXED (P0, applied).** Two `[CustomEditor(typeof(LabConsole))]` existed - `SceneManagerEditor` (real) + my `LabConsoleEditor` (last turn). Unity used one, so params were hidden / the Features UI risked being shadowed. Merged the Parameters section + live values into `SceneManagerEditor`; deleted `LabConsoleEditor`. |
| H4 | State store split from params | **FIXED (P5, applied 2026-06-30).** `LabConsole` now IS the `ILabStateStore` (bool-view over its own `Params`); triggers resolve the root directly - no separate component needed. Optional `LocalLabStateStore` views are still back-wired to the shared store. Networked-dict reconciliation remains a post-B2 VR item. |
| M2 | Signals have no explicit metric type | **STALE - done last turn.** `SignalMetric` exists; engine scores signals only on it. |
| M4 / "NetworkedParamStore validity" | Object touched before validity | **STALE - done last turn.** `Ready` guard + pre-spawn mirror buffer on Set/Apply/TryGet/Render. |
| Mojibake | Greek sample mojibake | **STALE - done last turn.** Rebuilt from code points; file is pure ASCII. |
| H1 / "sample scene" | No executable B2 sample | **OUT OF SCOPE** per your call (sample scene). The dev sink + readout script below still make a hand-built scene runnable. |
| MP / Vitals / Localization pipeline / telemetry-on-bus / B2.3 cloud | - | **DEFERRED by design** (post-B2 on-device / host / Vicky sign-off). Patches below only do the DevKit-side guard rails + honest labelling. |

## B. Patches

Legend: **[editor]** = I author an editor script, no scene work for you · **[script+UI]** = I author a runtime
script, you build a GameObject and attach it (instructions in section C) · **[runtime]** = code only.

### P0 - Fix double LabConsole editor + show params  ✅ APPLIED
Merged Parameters section + play-mode live values into `SceneManagerEditor`; deleted the conflicting
`LabConsoleEditor.cs`. Resolves H3. Nothing for you to do but verify the LabConsole inspector shows a
**Parameters** box (and live values in Play mode).

### P1 - Analytics rubric authoring inspector  [editor]   (review H2 - the headline gap)  ✅ APPLIED (2026-06-30)
**Built + adversarially verified.** New `Editor/Analytics.Editor/LabRubricBuilder.cs` (a second `partial` of
`LabAnalyticsEditor`; the existing file gains only `partial` + one `DrawRubricBuilder()` call - all auto-detect/
auto-wire helpers + the CS0118 `Scenario` alias preserved verbatim). A full serializedObject-driven builder:
- Analytics list: Add **Step / Scene Analytic** ([SerializeReference] managed-ref create via ScenarioEditor's
  exact idiom; null-slots surfaced, never stripped).
- Per analytic: Metrics via a kind-**FILTERED** typed picker (StepDuration/TotalDuration/Drop/Wrong/Order/Signal),
  each with its ScoringBands (+/- band rows).
- Objectives (id/label/weight/target) whose inputs pick an analytic id from a **dropdown** (no typed strings).
- Subjects + Role-capacities sections; foldouts persisted via EditorPrefs.
- Inline validation: empty/duplicate ids (per scope), weights summing to 0, metric-scope mismatch, objective
  with no inputs, input referencing an unknown analytic.
- No runtime/serialization change; no asmdef change (Analytics.Editor already references Analytics + Scenario);
  editor-only (no Proof-B impact). ASCII-only (no glyphs).

### P2 - StepAnalytic authoring tied to scenario steps  [editor]   ✅ APPLIED (2026-06-30); EXTENDED 2026-06-30 to IN-GRAPH
**Now authored directly on the Scenario Graph nodes** (Stergios overruled "picker only": the graph node IS the main
developer authoring surface), **plus** the step PICKER inside the P1 builder as a secondary path. Both write the
IDENTICAL `StepAnalytic.stepGuid` data.
- **In-graph (primary):** right-click a step node -> "Add Step Analytic" (creates the StepAnalytic for that step in
  the lab's rubric; offers to add a `LabAnalytics` to the lab root if none exists; then selects it so metrics get
  filled in the inspector). When one exists the menu shows "Edit Step Analytic" (select+ping) / "Remove Step
  Analytic" (confirm+delete). Step nodes that own an analytic show a green `ANALYTIC` badge. New partial
  `Editor/Scenario.Editor/ScenarioGraphWindow.Analytics.cs` + a menu block & badge in `...StepNode.cs` + a one-line
  `RefreshAnalyticsIndex()` in `Load()`. Mutation mirrors `LabAnalyticsEditor.AutoDetect` (RegisterCompleteObjectUndo
  + direct rubric edit + SetDirty), so graph- and inspector-created StepAnalytics are identical.
- **Asmdef edge added (Stergios-sanctioned):** `Pitech.XR.Scenario.Editor` now references `Pitech.XR.Analytics`.
  ACYCLIC - the Analytics runtime assembly depends only on Core, never on Scenario. Editor-only; no public-API change.
- **Picker (secondary):** `stepGuid` is still authorable via a dropdown of the linked Scenario's steps in the P1
  builder (`ResolveScenario`; label = `<Kind>: <displayName> (<short guid>)`).
- The previously-DEFERRED in-graph "has-analytics" badge is now DONE (the green `ANALYTIC` node badge above).
- RENAME (Stergios 2026-06-30): the Unity-facing "Rubric" label is now "Analytics config" (the `[Header]` on
  `LabAnalytics.rubric` + the builder section label). Code keeps `LabRubric`/`rubric` (a fine code-level name).

### P3 - Default readout panel  [script+UI]   (review H2 - "readout is only an event seam")  ✅ APPLIED (2026-06-30)
**Script written + adversarially verified.** `Runtime/Analytics/SessionReadoutPanel.cs` - a MonoBehaviour with
`public void Show(GradeResult)` (UnityEvent-bindable) that renders the overall grade (or "Incomplete") into
`overallText` and a per-objective PASS/FAIL breakdown (one rich-text-coloured line each) into `objectivesText`,
shown/hidden via a `CanvasGroup`. Derives overall pass/fail (GradeResult has no `passed` field); safe for the
Professor-presence / incomplete / null / empty-objectives cases. TMP_Text + CanvasGroup only (no UnityEngine.UI).
ASMDEF CHANGE APPLIED: added `Unity.TextMeshPro` to `Pitech.XR.Analytics.asmdef`. YOU: build the panel GameObject +
wire `LabAnalytics.onReadout -> Show` (section C-1).

### P4 - Dev session-report sink  [script+UI]   (review H5 - "no sink impl")  ✅ APPLIED (2026-06-30)
**Script written + adversarially verified.** `Runtime/Analytics/DebugSessionReportSink.cs` implements
`ISessionReportSink` (+ the inherited `IXRService` Initialize/Shutdown no-ops): on `Submit` it writes the JSON
to `Application.persistentDataPath/<subfolder>/<sessionId>.json` and logs a one-line summary. Two modes:
`selfRegister` (default true) registers it in `XRServices` as the `ISessionReportSink`, or assign it to
`LabAnalytics.reportSink`. No asmdef change. Loud on IO failure; documents that XRServices has no per-type
unregister. The durable HOST outbox stays host-owned - this is the dev/test sink. YOU: attach it (section C-2).

### P5 - One source of truth for state  [runtime]   (review H4 - the architecture)  ✅ APPLIED (2026-06-30)
**Done** (your call: "yes directly"). `LabConsole` now implements `ILabStateStore` directly -
GetState/SetState/Toggle over its own `_paramStore`, `StateChanged` forwards `ParamChanged`. Triggers/listeners
that `GetComponentInParent<ILabStateStore>()` resolve **LabConsole on the lab root** - no separate component,
no possible disconnect. LabConsole deliberately does NOT implement `IParamStoreBackedState` (it owns + builds
`_paramStore` in Awake, so nothing needs to back it); `Start` now backs *every* separate
`IParamStoreBackedState` view via `GetComponentsInChildren`, so an optional `LocalLabStateStore` on a sub-tree
shares the same store and cannot diverge. Networked reconciliation (retire `NetworkedLabStateStore`'s separate
dict for Networked-scope bool params) stays a post-B2 VR-migration item; a CO-LOCATION CAUTION was added to
`NetworkedLabStateStore` (don't put it on the LabConsole root - ambiguous resolution).
- Files: `Runtime/Scenario/LabConsole.cs`; doc-only: `Runtime/Core/ILabStateStore.cs`,
  `Runtime/Networking/LocalLabStateStore.cs`, `Runtime/Networking/NetworkedLabStateStore.cs`.
- Proofs: public-API **additive** (LabConsole gains the ILabStateStore members) -> regen Proof-B baseline (P13).
  **No new serialized fields** -> SP byte-identical (Proof C clean). Behaviour delta is additive: triggers in a
  lab that previously had NO state store now resolve LabConsole and function (intended); labs that had a
  `LocalLabStateStore` are unchanged (it's back-wired to the same store).

### P6 - Multiplayer analytics trust  [runtime]   (review H6 - follower durations wrong)  ✅ APPLIED (2026-06-30)
**Done.** New public `LabRuntimeContext.IsDriver` (resolves the lab's internal `IScenarioFlowStore` lazily in
Core - no InternalsVisibleTo widening, no LabConsole change; null flow store = single-player = driver).
`LabAnalytics.Finalize` now skips the GRADED (Participant) `Submit` when `!_ctx.IsDriver` (a follower) - the
local readout still shows, but only the driver ships the authoritative report. Inert single-player (always the
driver). Follower defer is a `Debug.Log` (expected MP behaviour, not a misconfig).
- Files: `Runtime/Core/LabRuntimeContext.cs`, `Runtime/Analytics/LabAnalytics.cs`.
- Still post-B2 (MP on-device): shared `sessionId` across peers + Professor presence-merge (each peer still
  submits its own presence today); the authority-migration teardown edge. Public-API additive (IsDriver) ->
  Proof-B regen.

### P7 - Role capacities wired + enforced locally  [runtime]   (review M1)  ✅ APPLIED (2026-06-30)
**Done.** `LabAnalytics.Start` now pushes `rubric.roleCapacities -> SessionRoleSelector.SetCapacities`, so the
LOCAL pick guard (`IsSelectable`: a max of 0 forbids a role) actually enforces the per-lab capacities before
the learner picks. Min counts + cross-peer headcount ("at least 1 participant across the session") are
inherently multiplayer -> B2.4. No public-API change (`SetCapacities` was already public; `Start` is private).
- Files: `Runtime/Analytics/LabAnalytics.cs`; test `Tests/Editor/Scenario/SessionRoleCapacityTests.cs`.

### P8 - Consent field (DevKit side)  [runtime]   (review H5 - consent plan/code mismatch)  ✅ APPLIED (2026-06-30)
**Done** (interview: single analytics consent · receipt = consentId + policyVersion + grantedAtUtc · fail-closed).
New `Pitech.XR.Core.ConsentReceipt` (consentId / policyVersion / grantedAtUtc; `IsGranted` = non-empty consentId).
The host stamps it onto `LaunchContext.consent` at launch (the DevKit never queries the backend); the spawner
carries it `LaunchContext -> LabRuntimeContext.Consent`; `LabAnalytics.Finalize` emits the report ONLY when
`IsGranted` (else a loud warning, no emit - the local readout still shows), and attaches the receipt to
`SessionReport` (+ `SessionReportJson`) for the cloud audit trail.
- Dev convenience (my call - veto if unwanted): `LaunchContextFactory` menu/direct launches self-grant a
  clearly-marked DEV consent (policyVersion "dev") so the in-editor loop works; production (RN/VR host) stamps
  the real receipt and never uses those factories.
- Files: `Runtime/Core/ConsentReceipt.cs` (new); `Runtime/ContentDelivery/LaunchContext.cs`,
  `Runtime/ContentDelivery/ContentDeliverySpawner.cs`, `Runtime/Core/LabRuntimeContext.cs`,
  `Runtime/Analytics/SessionReport.cs`, `Runtime/Analytics/SessionReportJson.cs`,
  `Runtime/Analytics/LabAnalytics.cs`; test `Tests/Editor/Scenario/ConsentReceiptTests.cs`.
- G2: `contractVersion` stays 1.1.0 and `SessionReport.schemaVersion` stays 1 (additive, inert by default);
  **confirm the field names with the Web Portal**, then bump `contractVersion -> 1.2.0` at the G2 freeze.
- Proofs: public-API ADDITIVE (the ConsentReceipt type + the consent fields/property) -> regen Proof-B baseline
  (P13). No new SERIALIZED fields on any scene component (`LabRuntimeContext.consent` is non-serialized;
  LabConsole/LabAnalytics serialized shape unchanged) - the new fields live on plain runtime DTOs (LaunchContext,
  SessionReport), default-empty and round-trip safe.

### P9 - Subject interaction adapter   (review "auto-wire still leaves hooks manual")  ✅ RESOLVED - NO SCRIPT (2026-06-30)
**No adapter needed (verified).** `AnalyticsSubject` ALREADY exposes parameterless, UnityEvent-callable
`public void ReportGrabbed()/ReportDropped()/ReportUsed()` - so an author wires grab/select/drop UnityEvents
straight to it in the inspector. An `AnalyticsInteractionAdapter` would be pure indirection (an extra component
+ a serialized ref) for zero new capability, so it was deliberately NOT written. Direct-wiring steps are in
section C-3. A future *automatic* Meta-Interaction auto-bind belongs in `Pitech.XR.Interactables` (the only
assembly defining `PITECH_HAS_META_INTERACTION` + referencing Meta types), NOT in Analytics.

### P10 - Hub pages: drop "reserved"  [editor]   (review M3)   ✅ APPLIED (2026-06-30)
**Done.** Stripped the "Reserved" pills + "coming/lands Phase B" copy across `Editor/Core.Editor/Pages/`:
`DeliverPage` (Analytics -> present-tense, "Available" pill, points to the in-graph Step Analytic flow), `AuthorPage`
(Vitals -> "Available"), `LocalizationPage` (whole page -> present-tense, "Available"). The shared per-page
`ReservedTile` helper was removed (grep-confirmed zero `ReservedTile`/"Reserved" left in Pages). JUDGMENT CALL:
`SetupPage` Networking was made HONEST instead of "Available" ("foundation shipped; full co-op step-sync post-launch",
pill = "Post-launch"), since full MP is genuinely post-launch - flag if you want it read differently or removed.

### P11 - Vitals sample binding  [script+UI]   (review "vitals foundation-only")  ✅ APPLIED (2026-06-30)
**Script written + adversarially verified.** `Runtime/Vitals/SampleBreathingVital.cs` - a tiny per-frame driver
that pushes a sinusoidal breathing waveform through `PatientVitals.SetVital(id, value)` each frame (the existing
`Vital` binding maps value->scene, but nothing drove it over TIME). `breathsPerMinute`, min/max amplitude, and
`StartBreathing`/`StopBreathing`/`SetRate` (UnityEvent-callable). No asmdef change. YOU: attach `PatientVitals`
+ author a `Vital` whose id matches, plus this driver (section C-4). Full digital twin stays post-launch.

### P12 - (optional) Lab State debug window  [editor]   (review "no network state view")
An EditorWindow listing the lab's declared bool params + their live runtime values + which triggers/listeners
reference each id. Nice-to-have for authors; lower priority than P1/P2.

### P13 - Gates / housekeeping  [you, in Unity]
Regenerate the Proof-B baseline (B.2 adds public types incl. `SignalMetric`, `IParamStoreBackedState`,
`SessionReadoutPanel`, `DebugSessionReportSink`), run EditMode tests, run Evaluate Changes. Decide the
telemetry-on-bus flip (needs Vicky-ingestion sign-off).

## C. UI build instructions (for the [script+UI] patches)

### C-1 Readout panel (P3)
1. Under your lab's UI canvas (world-space for VR), create a `GameObject` named **"Session Readout"** and add a
   `CanvasGroup` (the panel hides itself on Awake when a CanvasGroup is present, so it stays hidden until Session Stop).
2. Add **two TMP-Text children**: one for the overall grade ("85%" / "Incomplete"), one for the per-objective
   breakdown (the panel writes one rich-text-coloured PASS/FAIL line per objective into it - no row prefab/container).
3. Attach **`SessionReadoutPanel`** to "Session Readout". Assign `overallText` + `objectivesText` (and optionally
   `canvasGroup`, `passColor`, `failColor`).
4. On the lab root's **`LabAnalytics`**, under **On Readout ()**, add the "Session Readout" object and pick
   `SessionReadoutPanel.Show`.
Result: at Session Stop the panel fills in the grade + per-objective PASS/FAIL (colour-coded), no cloud call.
(`Unity.TextMeshPro` was added to `Pitech.XR.Analytics.asmdef` for `TMP_Text` - already applied.)

### C-2 Dev report sink (P4)
- Simplest: on the **lab root** (same object as `LabConsole`/`LabAnalytics`) add **`DebugSessionReportSink`**,
  then drag it into `LabAnalytics.reportSink`.
- Or global: add `DebugSessionReportSink` to a bootstrap object; it self-registers in `XRServices`, and every
  lab's LabAnalytics finds it automatically (no per-lab wiring).
Result: the "no sink" warning goes away; report JSON is written under persistentDataPath.

### C-3 Feeding interaction events to analytics (P9 - no script)
There is no adapter - `AnalyticsSubject` is already directly wireable:
1. On each tracked grabbable/selectable, add **`Analytics Subject`** (set `subjectId` to match a `TrackedSubject.id`;
   the editor auto-wirer usually adds it + fills the id).
2. On your interaction source's UnityEvent (Meta Select/Unselect, the RespawnOnDrop sample, a UI Button, a tool-fire
   event), press **+**, drag in the Analytics Subject object, and pick the no-arg method: grab ->
   `AnalyticsSubject.ReportGrabbed`, drop -> `ReportDropped`, use/activate -> `ReportUsed`.
3. (Optional, zero-dependency) tick `autoDetectDropBelowY` + set `dropY` on the subject for auto drop detection.
4. For authored non-grab/drop/use failures, use the sibling **`Analytics Signal Emitter`** (`EmitSignal(id)` / `Emit()`).
Note: all of this is inert unless a `LabAnalytics` exists in the scene.

### C-4 Vitals binding (P11)
1. On the patient object add **`Patient Vitals`**, and author a `Vital` with id e.g. **"breathing"** and a binding:
   Timeline-speed pointing at a looping chest `PlayableDirector`, an Animator float parameter, or a Field on a small
   blendshape-driver script (the Field binding reflection-sets a public float - it does NOT call SetBlendShapeWeight,
   so that tiny bridge is the author's job).
2. Add **`Sample Breathing Vital`** to the same object (or under PatientVitals). Set its `vitalId` to "breathing" and
   `breathsPerMinute` (~14); it drives the vital each frame so the binding animates a live chest rise/fall.
   `StartBreathing()`/`StopBreathing()`/`SetRate(bpm)` are UnityEvent-callable. (For a one-shot manual value instead,
   call `PatientVitals.SetVital("breathing", value)` from a button/EventStep.)

### Role pick UI (from last turn, still needed)
Build 3 buttons calling `SessionRoleSelector.SelectParticipant()/SelectProfessor()/SelectSpectator()` on the
lab root's `SessionRoleSelector`. After P7 the buttons disable for roles whose rubric capacity is 0.

## D. Suggested order
1. **P0** (done) -> verify in Unity.
2. **P5 + P6 + P7 + P8** (runtime correctness: one store, MP trust, capacities, consent) - small, high value.
3. **P1 + P2** (the authoring UX - the real "author-ready" headline). Largest.
4. **P3 + P4 + P9 + P11** (the runtime scripts you attach) - unblock end-to-end without a sample scene.
5. **P12** (optional debug window) - polish (P10 hub copy is done).
6. **P13** (your Unity gate pass).

## E. Decisions I need from you
1. ~~**P5:**~~ RESOLVED - `LabConsole` implements `ILabStateStore` directly ("yes directly", applied).
2. ~~**P8:**~~ RESOLVED - receipt (consentId + policyVersion + grantedAtUtc) + fail-closed gate, applied. Still
   owed: Web Portal alignment on the field names before G2, then bump contractVersion 1.1.0 -> 1.2.0. Optional
   veto: dev/menu launches self-grant a "dev" consent (so the editor loop emits) - say if you want that off.
3. **What's left:** P1, P2 (incl. in-graph Step Analytic authoring), and P10 are DONE. Remaining: P12 (optional
   debug window) + P13 (your Unity gate pass incl. the Proof-B baseline regen).
   Want **P1 + P2** next, or a different cut?
