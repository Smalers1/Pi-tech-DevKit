---
title: Phase B.2 - one-time Unity testing guide (for Stergios)
date: 2026-06-29
author: Claude Code
status: ready to run after the B.2 code landed (2026-06-29)
audience: Stergios (DevKit / Unity side)
---

# B.2 - what to do in Unity (plain steps)

All the B.2 **code** is written. None of it can be compiled or playtested from my environment, so this is
everything **you** do in Unity to verify it. Do the steps in order. If a step fails, stop and tell me what
the console said.

> The whole design principle: **everything is additive and inert until you opt a lab in.** Existing labs
> that don't add the new components behave exactly as before. So step 1-4 (compile + gate) should be clean
> with zero changes to how current labs play.

---

## What landed in B.2 (so you know what you're testing)

- **B2.1 Analytics** - the scoring engine (the ratified §11.8 math), the on-device lab-end readout, the one
  session report, the offline-outbox seam, role gating, in-scene warning/error notifications, and an
  EditMode test that proves the math.
- **Roles** - `SessionRoleSelector` (you build the pick UI on top; the script is the seam).
- **B2.2 Subjects** - `AnalyticsSubject` (drops/uses) + `AnalyticsSignalEmitter` (authored failures) + the
  recorder classifies correct/wrong/order + an "Auto-detect / Auto-wire" helper on `LabAnalytics`.
- **B2.4 Multiplayer (data plane)** - `FusionScenarioPath` + `NetworkedParamStore` + the runner's follower
  mirror. The full interactive co-op is a **post-B2 on-device** job (see the decisions doc).
- **B2.7 Components** - DevKit versions of `PhysicsStateTrigger` / `UIStateTrigger` / `EventStateListener` /
  `TimelineStateListener` (they resolve the store, no static `Instance`) + dropdown inspectors + a clean
  `ConsoleParameter` editor.
- **B2.5 Localization** - the StringTable resolver (Fusion-style gated so AR still compiles) + a simple
  dictionary lookup + **sample Greek/English** so you can see it render now + a `[Localize]` scan menu item.
- **B2.6 Vitals** - `Vital` + `PatientVitals` (+ the `IAgentStateSource` seam) - the foundation only.

---

## Step 1 - Compile (both projects)

1. Open the **DevKit dev project** (the one with the local package). Let it import.
2. Open the **Console** (Window > General > Console). You should see **0 errors**.
   - New folders that should import: `Runtime/Analytics/*` (lots of new files), `Runtime/Networking/*`
     (FusionScenarioPath, NetworkedParamStore, the 4 graduated components), `Runtime/Localization/*`,
     `Runtime/Vitals/*`, and 3 new editor assemblies (`Editor/Analytics.Editor`, `Editor/Networking.Editor`,
     `Editor/Localization.Editor`).
3. Open **HealthOn VR**. Let it import. **0 errors** again.
   - In VR, Fusion is present, so the Fusion-gated files (`FusionScenarioPath`, `NetworkedParamStore`,
     `TimelineStateListener`) and the StringTable resolver **do** compile here. This is the real test of the
     Fusion code (I wrote it to match the already-green `NetworkedLabStateStore` patterns, but only your
     compile confirms the Fusion API).

If you get errors, copy them to me - most likely a Fusion API name I couldn't verify headless.

## Step 2 - Regenerate the Proof-B baseline (one time)

B.2 deliberately adds new **public** API (the analytics engine, the components, etc.). The gate's Proof B
will flag that as "public API changed." That's expected and correct.

1. Run **Pi tech ▸ Tools ▸ Evaluate Changes**.
2. Proof B will report **additions** (the new public types). Confirm they're all the B.2 types you expect
   (LabAnalytics, the analytics engine/DTOs, the graduated components, SessionRoleSelector, the localization
   lookups, Vital/PatientVitals, IAgentStateSource, NetworkedParamStore...). Nothing should be **removed**.
3. **Regenerate the baseline** (`Tests/Baseline/PublicApi.Pitech.XR.txt`) so it captures the new surface.
   After that, re-run Evaluate Changes and Proof B should be green.

## Step 3 - Evaluate Changes (the gate)

Run **Pi tech ▸ Tools ▸ Evaluate Changes** again and read the three proofs:
- **Proof A (graph integrity):** green.
- **Proof B (public API):** green after the baseline regen in step 2.
- **Proof C (serialize/GUID stability):** may come back **Inconclusive** if any lab prefab reserializes.
  That is **acceptable for B.2** (B.2 is behaviour-*additive*, not behaviour-neutral like B.1). It should NOT
  be red. If a lab you didn't touch shows a real serialized diff, tell me.

## Step 4 - Run the EditMode tests

1. Window > General > **Test Runner** > **EditMode**.
2. Run all. The new **`AnalyticsGradeEngineTests`** (5 tests) must pass - they prove the §11.8 math
   (grade = 0.25 on the golden fixture, incomplete-on-no-stop, masking on unentered steps). The new
   **`RoutedParamStoreTests`** (6 tests) must pass - they prove the B2.4 declared-param routing headless
   (Networked-scope -> networked store, Local-scope/undeclared -> local, ParamChanged aggregation, Dispose).
   The new **`SignalMetricTests`** (4 tests) must pass - authored signals score on a SignalMetric (matched by
   id) and never leak into a Drop/Wrong/Order metric. The `LocalLabStateStoreTests` now also prove the
   bool-view shares one store with the runner (a SetState write is visible via the backing store, and vice
   versa). The existing `AnalyticsConfigTests` / `ParamStoreTests` must still pass.

---

## Step 5 - Manual playtests (per feature)

Do these in the DevKit dev project (or VR). Each is a small opt-in setup.

### 5a - B2.1 Analytics (the core)
1. Make a tiny scenario: any step, then a **Session Start** step, a couple of normal steps, then a
   **Session Stop** step (these are the graded bracket).
2. On the **lab root** (the same GameObject as `LabConsole`) add a **`Lab Analytics`** component
   (Add Component ▸ Pi tech ▸ Analytics ▸ Lab Analytics).
3. In its **config**, add one Objective ▸ one Step Analytic (set its `stepGuid` to a step in the bracket) ▸
   one **StepDuration** metric with a Warning band (threshold e.g. 5s) and an Error band (e.g. 10s).
4. Wire **`onReadout`** to a small UI panel of yours (or just add a debug listener that logs the grade).
5. Play, walk the bracket, hit Session Stop. You should get the **readout** (grade + per-objective pass/fail)
   with **no cloud call**.
6. Quit mid-bracket once - confirm the report is marked **incomplete** (never "passed").
   - Note: persisting the report needs a host **outbox**. Until you register an `ISessionReportSink`
     (XRServices) the readout still shows but you'll see a one-line "no sink" warning - that's expected.

### 5b - Roles
1. Add a **`Session Role Selector`** on the lab root. Build 3 buttons that call `SelectParticipant()` /
   `SelectProfessor()` / `SelectSpectator()`.
2. Re-run 5a as each role: **Participant** = full graded report; **Professor** = presence-only; **Spectator**
   = nothing emitted.

### 5c - B2.2 Subjects + signals
1. On a grabbable object add **`Analytics Subject`**, set its `subjectId`, and either turn on
   "auto-detect drop below Y" or hook your grab/drop/use events to `ReportGrabbed/ReportDropped/ReportUsed`.
2. Add that subject to the `LabAnalytics` config's **subjects** list (or use the **Auto-detect / Auto-wire**
   buttons on the LabAnalytics inspector - Auto-detect pulls in **InsertStep items AND each SelectionStep's
   correct targets**, owner = that step; the resolved list is by `listIndex`, else `listKey`). Distractors /
   free grabbables you still add by hand.
3. Add a **Drop** metric (Scene Analytic) to the config. Drop the item during a run - it should score.
4. For an authored failure (e.g. wrong cut), add **`Analytics Signal Emitter`**, set its `defaultSignalId`,
   add a **`SignalMetric`** to the config whose **id == that `defaultSignalId`**, and call `Emit()` from your
   wrong-action UnityEvent. (Signals now score ONLY on a `SignalMetric` matched by id - they no longer
   piggy-back on a Drop/Wrong/Order metric.)

### 5d - B2.5 Localization (see Greek render now)
1. Anywhere at startup, call:
   `Pitech.XR.Localization.LocalizationServices.Install(new Pitech.XR.Localization.DictionaryLocalizationLookup(Pitech.XR.Localization.SampleLocalizationStrings.Greek));`
2. Run a Quiz - the feedback/results should render in **Greek**. Install `.English` (or call
   `LocalizationServices.Reset()`) to go back. This proves the seam end-to-end without the full pipeline.
3. The `[Localize]` scan: **Pi tech ▸ Localization ▸ Scan [Localize] fields in assets** - it lists the keyed
   QuizAsset strings. (Running the real StringTable pipeline on actual labs is post-B2.)

### 5e - B2.6 Vitals
1. Add **`Patient Vitals`** to a patient object. Add a `Vital` (e.g. id "breathing", a Timeline-speed
   binding pointing at a breathing Timeline).
2. From a test button call `SetVital("breathing", 1.5)` - the bound Timeline speed should change.

### 5f - Lab state <-> conditions share ONE store (#1 unification)
This proves a trigger-set state is now visible to a ConditionsStep (previously they used different stores).
1. On the **lab root** add a **`Local Lab State Store`** (Pi tech ▸ Networking ▸ Local Lab State Store).
   LabConsole binds it to its param store automatically on Start.
2. Declare a **Bool** parameter on LabConsole named e.g. `WaterFlowing` (see 5g for the Parameters UI).
3. Add a **`Physics State Trigger`** (or call `SetState("WaterFlowing", true)` from a button) to set it.
4. Add a **ConditionsStep** that branches on `WaterFlowing` (value source = Stat/param, key `WaterFlowing`).
5. Play: set the state, reach the ConditionsStep - it must now read the **true** the trigger wrote. Before
   the fix it read a separate store and always saw false.
   - Networked variant (on-device): declare `WaterFlowing` **Networked-scope** and add a `NetworkedParamStore`;
     the same bool then replicates (the bool-view writes through LabConsole's routed Params). The standalone
     `NetworkedLabStateStore` is the legacy VR drop-in and is intentionally NOT auto-bound.

### 5g - LabConsole Parameters UI + live values (B2.7 S1/S2)
1. Select the **LabConsole**. The inspector now has a dedicated **Parameters** section (add/remove/reorder;
   each row is the type-aware ConsoleParameter drawer with inline validation).
2. Enter **Play Mode**: a **Live values (runtime)** readout under the list shows each declared parameter's
   current runtime value, updating live as effects/triggers change them.

---

## Step 6 - The B.1 leftover (separate, when you're ready)
- The **Stats / StatsUI playmode test** (B1.2 - the one you saved for "tomorrow"): confirm a RASS-style stat
  tracks through `StatsUI` and the clamp fires. That closes B.1.

## Not now (post-B2, on a headset)
- The **multiplayer 2-client proofs** (B2.4): AnyCompletes + branch + late-join + authority-drop. The data
  plane is built; the full interactive co-op runner semantics + the VR lab migration are the post-B2
  on-device window. See the decisions doc.
- The **declared-param wiring** (B2.4 Step 4) is now wired: if a lab carries a `NetworkedParamStore`
  component, LabConsole routes its **Networked-scope** params through it (replicated) and keeps Local-scope
  params client-local (`RoutedParamStore`). Single-player labs have no such component, so this is invisible
  there (byte-identical). Validating it needs a **2-client Fusion lab** - do it in the same on-device pass:
  set a param Networked-scope, change it on the authority, confirm both peers read the new value and the
  StatsUI mirror still animates.

## If you point HealthOn VR at the local DevKit
- In `VR Shell and Labs/HealthOn VR/Packages/manifest.json`, the devkit line becomes
  `"com.pitech.xr.devkit": "file:../../../Dev Kit/Pi-tech-DevKit"` (you already had it on a git URL; only
  change it if you want VR to consume your local edits for testing). Revert when done if that's your policy.

---

When steps 1-4 are green and 5a-5e behave, B.2 is verified on your side. Then we close out the decisions in
`2026-06-29-B2-open-decisions.md` and you do the on-device MP work in the Phase C window.
