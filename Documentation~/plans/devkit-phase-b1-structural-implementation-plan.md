---
title: Phase B.1 - Structural (behaviour-neutral) - the DevKit v1.0 restructure
status: READY to dispatch 2026-06-26 (derived from the architecture map; supersedes the 2026-06-09 plan's structural stance). Pending final sign-off.
date: 2026-06-26
author: Claude Code
owner: Claude Code (DevKit repo)
phase: B.1
gate: EditMode "DevKit > Evaluate Changes" net (Proofs A/B/C) green on EVERY change + the dev-playtest checklist (single-player) + on-device 2-client proofs (multiplayer infra)
references:
  - devkit-architecture-map-phase-b.md (the architecture map = SINGLE SOURCE OF TRUTH; decision log 2026-06-19 -> 2026-06-26b)
  - ../plans/2026-06-09-phase-a-refactor-and-foundation.md (Phase A - the WS A3 net that gates every change here; the ISceneRunnerControl seam)
  - ../plans/2026-06-09-phase-c-integration-and-ship.md (Phase C - roll into AR/VR + ship)
  - ../plans/_archive/2026-06-09-phase-b-analytics.md (SUPERSEDED - mined only for execution scaffolding: cloud lane, transports, consent step, exit-criteria format)
companion: devkit-phase-b2-features-implementation-plan.md (B.2 = the behaviour-additive features built on this foundation)
---

# Phase B.1 - Structural (behaviour-neutral)

> **For implementers (Claude Code):** implement WS-by-WS. Steps use `- [ ]` checkbox syntax; add a row to the **Status &
> Progress Log** (bottom) on every WS start/close and every `Evaluate Changes` green run on a milestone. This doc is the
> EXECUTION projection of the architecture map's **B.1 column** (map §13) + decision log through **2026-06-26b**. **Authority
> order: code > map > this plan** - on conflict the map wins; where the map and the code disagree, the code wins (cite `file:line`).
>
> **Completion discipline: every WS completes IN FULL - every step ticked, none skipped.** Steps tagged **[HUMAN]** require a
> physical action Claude Code cannot perform (on-device headset runs, VR-repo approval, git) - surface them, never silently
> pass over them, and do not declare a WS done while one is open. **Edit discipline: local-only edits; the user runs git.**

**Goal.** Land the behaviour-neutral foundation the whole launch rides on: the `LabEventBus` notification plane, the one
typed param/state store (superseding Stats), the multiplayer seams + scene-authored stores (inert at launch), the frozen
analytics + localization + scenario-data contracts, and finally the runner extraction + `SceneManager -> LabConsole`
rename. Every change is **observably identical at runtime** and admitted only by the Evaluate-Changes net.

**Architecture stance.** Behaviour-NEUTRAL, in three precise senses (map §13): **(i) inert additions** (schema/contract
that does nothing until used - the analytics rubric types, `SessionStart/Stop`, `LaunchContext.locale`, `ConsoleParameter`,
the path-store seam, `[MovedFrom]`); **(ii) behaviour-preserving migration** (same output, new path - reflection-poll
telemetry -> bus subscribers; wrapping the runner behind direct calls WITHOUT touching the `Run*` bodies - proven, never
assumed); **(iii) behaviour-additive** - anything switched on belongs in **B.2**. The divergent linear/group twin **dedup**
stays post-launch (map §9.1).

**Authority / spec reference.** `devkit-architecture-map-phase-b.md` - §6 (LabConsole), §7 (the two planes), §8 (param
store), §9 (runner + scenario data), §10 (multiplayer), §11 (analytics schema), §12 (localization), §13 (the split),
§15 (verified dependencies).

**Duration / window.** **2026-06-20 -> 07-14 DevKit-side** (overlapping Phase A's tail). Bound gates: **G2 cross-surface
contract freeze 2026-06-29**; **DevKit SDK emit-API freeze 2026-07-07** (post-freeze additive-only). Phase C integrates
from ~Jul-15.

**Exit criteria (measurable).**
- `LabEventBus` live + lab-scoped; today's reflection-poll telemetry rides it; `ScenarioFactKeys` reconciled. No behaviour change (telemetry output identical).
- One typed param store supersedes Stats; the one Stats-using lab migrates **zero-diff under Proof C**; min/max clamp enforced; no lab relied on an out-of-range value.
- MP seams + scene-authored stores exist and are **inert**: SP + AR + one-peer builds are **trace-identical** to today (Local passthrough).
- Analytics rubric schema + `SessionStart/Stop` + role enum/capacities + tenant+user id serialize; untouched labs **zero-diff (Proof C)**; surface frozen by 07-07.
- Localization module relocated into the DevKit; data-asset + code-literal text keyed; merge seam compiles; additive (no lab regressed).
- Migration discipline (`[MovedFrom]`/`[FormerlySerializedAs]` + dangling-guid lint) in place; the **full manual JSON round-trip** survives Evaluate-Changes on a real lab; `link.xml` updated.
- Runner extracted + renamed to **LabConsole**; single-player behaviour **identical** (Evaluate-Changes + full dev-playtest sign-off); `ISceneRunnerControl` unwidened.
- `AddressablesRemoteUrlRewriter` global-clobber fixed (UaaL regression test green); `Stats.Editor.asmdef` added.
- Every change passed "DevKit > Evaluate Changes" (Proofs A/B/C). No emoji / mojibake in shipped strings or tooltips.

---

## Plan structure

| WS | Focus | Gate / depends-on | Source / map § |
|---|---|---|---|
| B1.1 | `LabEventBus` (Core) + lab-scoped instance + migrate reflection-poll telemetry + reconcile `ScenarioFactKeys` | Phase A net green | map §7, §11 |
| B1.2 | Unified param/state store (`ParamValue`) = successor to Stats; min/max enforced; `ILabStateStore` bool-view | net green | map §8 |
| B1.3 | MP seams + scene-authored Local/Networked stores + path-store + follower-suppression hook (INERT) | net green | map §10 |
| B1.4 | Analytics SCHEMA (rubric types + `SessionStart/Stop` + roles/capacities + tenant+user id) - inert serialized surface | net green; freeze 07-07 | map §11 |
| B1.5 | Localization infra (relocate pipeline + extend keying + merge seam) | net green | map §12 |
| B1.6 | Scenario data durability (`[MovedFrom]` migration + dangling-guid lint) + **full manual JSON round-trip** | net green | map §9.2 |
| B1.7 | Runner extraction + `SceneManager -> LabConsole` rename (direct-ref wiring; seam ignition-only) | net green; **last** | map §6, §9.1 |
| B1.8 | Hygiene (`AddressablesRemoteUrlRewriter` fix + `Stats.Editor.asmdef` + `FindObjectsOfType` -> typed) | net green | map §5 |

> **WS tags.** **B1.1-B1.6 + B1.8 = LAUNCH_BLOCKER · B1.7 = CAN_TRAIL** (the designated slip sink - the extraction + rename
> is the schedule risk; nothing else depends on it, so it slips alone if the window tightens, map §14). B1.6's JSON
> *importer* half may trail behind the *export* + migration half if needed. A tagged slip is **dispositioned in the Status
> & Progress Log - never silently skipped.**

> **Lane note.** B.1 is **entirely DevKit-repo work**. The one cross-surface dependency is the **G2 session-report schema**
> (frozen here; the Web Portal / cloud side rebuilds to it in B.2) - start that hand-off NOW. B1.3's `NetworkStateManager`
> relocation **touches the VR repo** (ask before changing VR project files; ship a `[Obsolete]` facade so hand-wired scenes
> don't break).

> **Parallelism.** Once the Phase A net is green, B1.1 / B1.2 / B1.3 / B1.4 / B1.5 / B1.6 / B1.8 are largely disjoint and can
> proceed in any order. **B1.7 (extraction + rename) runs LAST** - everything else (bus / params / stores / schema / loc /
> migration) is designed to NOT depend on it, so the slip-point is isolated.

---

## The gate + verification stack (map §9.1 / §15)

1. **EditMode "DevKit > Evaluate Changes"** (`Editor/Core.Editor/Tools/EvaluateChanges.cs` -> `DevKitChecks.RunEditModeGate`):
   **Proof A** graph integrity · **Proof B** public-API + Core.Editor type-literals · **Proof C** serialize/GUID stability +
   prefab-override-no-churn. Real + enforcing - **gate EVERY change on it.**
2. **Dev-playtest checklist (single-player):** run each launch lab; tick each step / branch / effect; **signed off, not ad-hoc.**
3. **On-device 2-client proofs (MP infra):** Local/Fusion seam trace-identical in SP and one-peer; capacity + authority-drop.
4. Golden-trace (Proof D) stays a **post-launch CI** seed - never the launch gate.

## The two freeze gates (map §13)

- **G2 - 2026-06-29 - cross-surface:** the analytics **session-report schema** (supersedes per-event `AnalyticsEventV1`;
  **the Web Portal / cloud side must align**), consent, `LaunchContext` (incl. `.locale`, tenant + user id).
- **DevKit SDK emit-API freeze - 2026-07-07:** bus fact shape, the **analytics rubric schema** (`AnalyticsMetric` + bands +
  `TrackedSubject` + `Objective`, **NOT a Step bool**), `SessionStart/Stop`, effect-scope shapes, `ConsoleParameter`.

---

## WS B1.1 - `LabEventBus` (the notification plane)

**Goal.** Build `ILabEventBus` in `Pitech.XR.Core`; a lab-scoped instance owned by `LabRuntimeContext`; migrate today's
reflection-poll telemetry onto it; reconcile `ScenarioFactKeys`. (map §7)

**Scope / files.** `Runtime/Core/` (`ILabEventBus`, `LabRuntimeContext`, `ScenarioFactKeys`); `Runtime/ContentDelivery/Analytics/RuntimeTelemetryAdapter.cs`.

**Steps (progress tracking):**
- [ ] Step 1: Define `ILabEventBus` in Core - facts out, fire-and-forget, **sync in-process**, each subscriber wrapped (one can't break the runner), facts carry a **value snapshot** + `attemptId` + `labInstanceId`.
- [ ] Step 2: Create `LabRuntimeContext` on the spawned lab root (ContentDelivery owns the attempt lifecycle); resolve the bus by **parent-walk**, never `XRServices` (the global mis-bind risk, map §5/§7).
- [ ] Step 3: Migrate `RuntimeTelemetryAdapter` step/session detection from **string-reflection on `SceneManager`** (`:576`,`:593-667`) + the per-frame `FindObjectsOfType` (`:567`) to **bus subscription** (gives accurate enter/exit timestamps; kills the rename-fragility).
- [ ] Step 4: **Reconcile `ScenarioFactKeys`** - delete the dead per-step-bool keys (`flow.step.<guid>`, `scenario.step.<guid>.{done,...}`) the path-list (map §10) supersedes. Zero consumers today = free now, un-removable after 07-07.
- [ ] Step 5: Run "DevKit > Evaluate Changes" + SP playtest - telemetry output is **identical** (behaviour-preserving migration).

**Acceptance.** Bus in Core; lab-scoped instance resolves by parent-walk; telemetry rides the bus (no reflection/scan); fact keys reconciled; telemetry output unchanged.

**Gate.** Phase A net green; Evaluate-Changes + SP playtest.

---

## WS B1.2 - Unified param/state store (successor to Stats)

**Goal.** One typed store (`ParamValue` union); supersede + migrate Stats; min/max **enforced**; `ILabStateStore` bool-view. (map §8)

**Scope / files.** New param-store service (`Runtime/Scenario/` or a param module); `Runtime/Stats/*` (deprecate); `Runtime/Scenario/Steps/ConditionsStep.cs`; the LabConsole editor window.

**Steps (progress tracking):**
- [ ] Step 1: `ParamValue { ParamType tag; float number; NetworkString text }`; store `{ id -> ParamValue }`; **Local** impl = `Dictionary`, **Networked** impl = one `[Networked] NetworkDictionary<id, ParamValue>` (<=64).
- [ ] Step 2: Edit-time `ConsoleParameter { id, type, default, min, max, scope }` (declared in the LabConsole window); typed accessors (`GetBool/GetFloat/SetEnum...`); `ILabStateStore.GetState` = the bool accessor.
- [ ] Step 3: **min/max ENFORCED (clamp on write).** **[HUMAN-verify]** confirm no existing lab relied on an out-of-range value (Stats' min/max were display-only).
- [ ] Step 4: Deprecate Stats -> param ops: `StatEffect` -> param op; `ConditionsStep` Stat source -> param read; `StatsConfig` entries -> param declarations; `Quiz.*` magic keys -> params. Migrate the one lab via `[MovedFrom]`/`[FormerlySerializedAs]` + a one-time editor upgrader.
- [ ] Step 5: Per-param **scope** (Local vs Networked/Shared); Networked auto-degrades to local in no-Fusion/SP; relative ops (`Add`/`Multiply`) apply **authority-only + sequenced**; record provenance/actor (no live writer until the post-launch door).
- [ ] Step 6: Proof C on untouched labs (zero-diff) + the migrated lab (clean upgrade).

**Acceptance.** One store supersedes Stats; the Stats lab migrates clean under Proof C; clamp enforced; no lab broke.

**Gate.** Evaluate-Changes (Proof C) + SP playtest.

---

## WS B1.3 - Multiplayer infra (seams + stores, inert at launch)

**Goal.** Build the MP seams so B.2 is a flip-on; SP/AR stays trace-identical. (map §10)

**Scope / files.** `Runtime/Core/` (`IScenarioFlowStore`, `ILabStateStore`); `Runtime/Networking/` (`LocalScenarioPath`, `FusionScenarioPath`, `LocalLabStateStore`, `NetworkedLabStateStore`); VR repo `Assets/Scripts/Multiplayer/NetworkedStates/` (ask-first).

**Steps (progress tracking):**
- [ ] Step 1: `IScenarioFlowStore` (append-only entered-guid path the runner writes/reads). Impls: `LocalScenarioPath` (always compiled, single-driver passthrough) + `FusionScenarioPath` (`#if PITECH_HAS_FUSION`, `[Networked, Capacity(256)]` ring). **Internal at launch** via `[InternalsVisibleTo]`.
- [ ] Step 2: `ILabStateStore` in Core (bool-view); impls `LocalLabStateStore` (always compiled) + `NetworkedLabStateStore : NetworkBehaviour` (`#if PITECH_HAS_FUSION` - relocate the `NetworkStateManager` body TYPES into the DevKit).
- [ ] Step 3: **Scene-authored, not spawned:** one store per lab on the SCENE-MANAGERS root; resolve via `GetComponentInParent<ILabStateStore>()` (kill the `static Instance`); listeners subscribe to `StateChanged` (no `Update` polling).
- [ ] Step 4: **Follower-suppression hook in the runner, INERT** (never taken in SP - always DRIVE) so B.2 flips it on (decision 36).
- [ ] Step 5: **[HUMAN - VR repo, ask-first]** delete VR `NetworkedStates/` copies, reference DevKit types; ship a `[Obsolete]` static facade forwarding to the resolved store.
- [ ] Step 6: Validators: **read-gate-forbidden** (advance may not gate on a shared fact - AR-hang impossible) + **state-budget** (warn 48 / fail 64, measured baseline = 16/64).
- [ ] Step 7: Prove SP + AR + one-peer builds are **trace-identical** (Local passthrough).

**Acceptance.** Seams + stores exist; SP/AR trace-identical; follower hook inert; validators live.

**Gate.** Evaluate-Changes + SP playtest + AR no-Fusion trace-identity.

---

## WS B1.4 - Analytics SCHEMA (inert serialized surface only)

**Goal.** Land the frozen serialized analytics contracts - NO observers/scoring/emit (those are B.2). Freeze 07-07. (map §11)

**Scope / files.** `Runtime/Analytics/` (the rubric types); `Runtime/Scenario/Steps/` (`SessionStart`/`SessionStop`); `Runtime/ContentDelivery/LaunchContext`.

**Steps (progress tracking):**
- [ ] Step 1: `AnalyticsMetric` (polymorphic `[SerializeReference]`: `StepDuration`/`TotalDuration`/`Drop`/`WrongInteraction`/`Order`) with `ScoringBand[]` (**defaults `none 0 / warning 0.5 / error 1.0`**, author-overridable) - a **sidecar rubric keyed by `step.guid`** (a brick), **NOT fields on `Step`**.
- [ ] Step 2: `TrackedSubject { id, label, target, scenarioRelevant, ownerStepGuid }` (subjects registry) + `Objective { id, label, weight, target, inputs[] }` + `Analytic` (Step/Scene) shapes.
- [ ] Step 3: `SessionStart` / `SessionStop` step types (the graded bracket).
- [ ] Step 4: **Role enum** (Professor/Participant/Spectator) + per-lab **capacities** (Prof 0-inf / Participant 1-inf / Spectator 0-inf).
- [ ] Step 5: `LaunchContext` carries **tenant id + user id** (role/attempt created in-scene).
- [ ] Step 6: Proof C - opted-in steps gain the sidecar; **untouched labs zero-diff**. Freeze the surface for 07-07.

**Acceptance.** All types serialize; untouched labs zero-diff (Proof C); surface frozen-ready.

**Gate.** Evaluate-Changes (Proof C zero-diff); freeze 07-07.

---

## WS B1.5 - Localization infra

**Goal.** Relocate the keying pipeline into the DevKit + close the coverage gap + lay the merge seam. (map §12)

**Scope / files.** New `Runtime`/`Editor` Localization module; VR `Assets/Scripts/Editor/Localization/` (relocate source); `LaunchContext`.

**Steps (progress tracking):**
- [ ] Step 1: Add `locale` to `LaunchContext` (per-client, never networked).
- [ ] Step 2: **Relocate** the VR `Editor/Localization/` pipeline (`GlobalObjectId`-keyed manifest + `ManualTranslationIO`) into a DevKit Localization module.
- [ ] Step 3: **Extend keying** beyond scene `TMP_Text` to **(b) data-asset text** (`QuizAsset` prompts/answers) + **(c) code literals** (`"Wrong"`/`"Correct"`) via a `[Localize]`-attribute/reflection scan + a lookup seam.
- [ ] Step 4: Build the **merge seam** (baked StringTables base + cloud-table overlay under the same keys) - logic only; cloud source is B.2/post-launch.
- [ ] Step 5: Evaluate-Changes - additive, no lab regressed.

**Acceptance.** Module in DevKit; data-asset + code-literal text keyed; merge seam compiles; additive (Proof C).

**Gate.** Evaluate-Changes.

---

## WS B1.6 - Scenario data durability + JSON round-trip

**Goal.** Protect in-scene labs across the restructure + ship the **full manual JSON round-trip** at launch. (map §9.2, decision 40)

**Scope / files.** `Runtime/Scenario/*` (attributes + lint); a JSON DTO + export/import; `Runtime/link.xml`.

**Steps (progress tracking):**
- [ ] Step 1: **(D) migration discipline:** `[MovedFrom]` (type identity) + `[FormerlySerializedAs]` (fields) at **each rename/move**; a one-time editor upgrade pass (open -> re-resolve -> re-save); a **dangling-`nextGuid` lint/repair**.
- [ ] Step 2: **Freeze the `kind`-discriminated JSON shape** (`kind` = each `Step.Kind`; `schemaVersion`; enum-by-name; field naming).
- [ ] Step 3: Implement JSON **export** (lab -> JSON; scene-independent reference resolution).
- [ ] Step 4: Implement JSON **import** (JSON -> lab) - the full manual round-trip; in-scene stays canonical (not a source-of-truth flip).
- [ ] Step 5: Round-trip a real lab; **Proof A (graph integrity) + Proof C (serialization) stay green.**
- [ ] Step 6: Add the new types (`SessionStart/Stop`, `FusionScenarioPath`, typed params) to `Runtime/link.xml` (IL2CPP strip-safety).

**Acceptance.** Renames don't orphan steps; the round-trip survives Evaluate-Changes on a real lab; link.xml updated.

**Gate.** Evaluate-Changes (Proof A + C).

---

## WS B1.7 - Runner extraction + `SceneManager -> LabConsole` rename  *(CAN_TRAIL - the slip sink; runs LAST)*

**Goal.** Extract the run-engine; rename to LabConsole; **direct-reference internal wiring**; the seam stays ignition-only. (map §6/§9.1, decision 34)

**Scope / files.** `Runtime/Scenario/SceneManager.cs` (-> LabConsole + inner runner); `Runtime/Core/ISceneRunnerControl.cs`; `Editor/Core.Editor/` (Hub typed refs); `.meta` GUIDs.

**Steps (progress tracking):**
- [ ] Step 1: Extract the run-engine into a dedicated **inner runner** type (type-first within `Pitech.XR.Scenario`; own assembly later). **Do NOT touch the `Run*` bodies** (twins diverge - dedup is post-launch).
- [ ] Step 2: Rename `SceneManager -> LabConsole`; LabConsole keeps serialized authoring + the param store + **owns and directly drives** the inner runner.
- [ ] Step 3: **Internal wiring = plain direct references** (no internal Core interface); LabConsole **hands the runner the param-store service** (dependency runner->store).
- [ ] Step 4: **`ISceneRunnerControl` (3 members) = cross-assembly ignition ONLY**, implemented by LabConsole, **NOT widened**.
- [ ] Step 5: Wire the runner's **two outputs** - (1) fact emission onto the bus; (2) flow-store write+read via `IScenarioFlowStore`.
- [ ] Step 6: Migrate the **DevKit Hub off string-wiring to typed refs**; preserve `SceneManager.cs.meta` + `Scenario.cs.meta` GUIDs.
- [ ] Step 7: **Full dev-playtest sign-off** + Evaluate-Changes - single-player behaviour identical.

**Acceptance.** Runtime SP behaviour identical (Evaluate-Changes + full playtest sign-off); seam unwidened; Hub typed; no orphaned steps.

**Gate.** Evaluate-Changes + the full dev-playtest checklist.

---

## WS B1.8 - Hygiene (launch-critical fixes)

**Goal.** Close the defects the restructure rides on. (map §5/§13)

**Scope / files.** `Runtime/ContentDelivery/AddressablesRemoteUrlRewriter.cs`; `Editor/Stats.Editor/` (new asmdef).

**Steps (progress tracking):**
- [ ] Step 1: **`AddressablesRemoteUrlRewriter` global-clobber (P0 for UaaL):** on `Install` **capture** the prior `InternalIdTransformFunc` and **chain** to it (don't overwrite); on `Uninstall`/`Clear` **restore the captured prior** (never null unconditionally) (`AddressablesRemoteUrlRewriter.cs:118`).
- [ ] Step 2: **Acceptance test:** a UaaL regression test - the host RN app's Addressables transform survives a DevKit install->uninstall cycle.
- [ ] Step 3: Add `Pitech.XR.Stats.Editor.asmdef` so the orphaned `[CustomEditor]` scripts compile into the package.
- [ ] Step 4: Confirm the `RuntimeTelemetryAdapter` `FindObjectsOfType` runner-bind is gone (folded into B1.1).

**Acceptance.** UaaL regression test green; Stats editors compile; no global Addressables clobber.

**Gate.** Evaluate-Changes + the UaaL regression test.

---

## Delivery-chain alignment + critical path

- **Net-green precondition:** Phase A WS A3 net must be green before any change here lands.
- **Parallel core (06-20 -> 07-07):** B1.1 / B1.2 / B1.3 / B1.4 / B1.5 / B1.6 / B1.8 run concurrently per DRI.
- **G2 (06-29):** freeze the cross-surface session-report schema + consent + `LaunchContext` - **escalate the Web Portal / cloud hand-off now** (only non-DevKit blocker).
- **07-07:** freeze the DevKit SDK emit-API surface (rubric schema, bus fact shape, `SessionStart/Stop`, effect-scope, `ConsoleParameter`).
- **B1.7 (extraction + rename) LAST**, finishing into the 07-14 window; it is the slip-point (CAN_TRAIL) - if the window tightens it trails into Phase C without blocking B1.1-B1.6/B1.8 or B.2.

## Deferred to post-launch (out of B.1 scope)

- The linear/group twin **dedup** (behaviour change; Phase D, map §9.1).
- The LabConsole outside-in **door turn-on**, VICKY-observe, `IScenarioFlowStore` going **public** (Phase E).
- JSON as **source of truth** (portal edits it; scene imports - map §14 #7).
- All B.2 behaviour: analytics observers/scoring/emit, MP turn-on, localization content, Vitals foundation.

## Exit checklist + gate

- [ ] **B1.1** Bus live + lab-scoped; telemetry migrated off reflection; fact keys reconciled; output identical.
- [ ] **B1.2** Param store supersedes Stats; one lab migrated zero-diff; clamp enforced; no lab broke.
- [ ] **B1.3** MP seams + scene-authored stores; SP/AR trace-identical; follower hook inert; validators live.
- [ ] **B1.4** Rubric schema + `SessionStart/Stop` + roles/capacities + tenant+user id serialize; untouched labs zero-diff; frozen 07-07.
- [ ] **B1.5** Localization relocated; data-asset + code-literal keyed; merge seam compiles.
- [ ] **B1.6** Migration discipline + full manual JSON round-trip survives Evaluate-Changes; link.xml updated.
- [ ] **B1.7** Runner extracted + renamed to LabConsole; SP behaviour identical (playtest signed off); seam unwidened.
- [ ] **B1.8** Addressables clobber fixed (UaaL test); `Stats.Editor.asmdef` added.
- [ ] **Gates** G2 (06-29) + 07-07 surfaces frozen. Every change passed "DevKit > Evaluate Changes." No emoji/mojibake.

## Plan self-review (coverage check)

- [ ] Every WS maps to a map §: B1.1 §7/§11 · B1.2 §8 · B1.3 §10 · B1.4 §11 · B1.5 §12 · B1.6 §9.2 · B1.7 §6/§9.1 · B1.8 §5.
- [ ] The behaviour-neutral three-meanings boundary (inert / preserving / additive) is stated and honoured (nothing additive here).
- [ ] The gate is the Phase A net everywhere; B1.7 is the single CAN_TRAIL slip-point.
- [ ] Both freeze gates (G2 06-29 + SDK 07-07) and their exact surfaces are named.
- [ ] The DRIVE/FOLLOW per-step-type contract (below) is specified so B.2 turn-on is mechanical.
- [ ] Cross-repo (VR `NetworkedStates/`) is flagged ask-first with a no-break facade.
- [ ] Real "§" characters; no emoji, no mojibake.

## Execution handoff

**Executors.** Claude Code implements all DevKit-repo WSs, WS-by-WS. The VR-repo touch (B1.3 Step 5) is ask-first. The G2
session-report schema is frozen here and handed to the **Web Portal / cloud side** for the B.2 rebuild.

**Ratification path.** Derived from the architecture map; on sign-off, execute WS-by-WS.

**Edit discipline.** Local-only edits; **the user runs git** (do not `git add`/`commit`/`push`). Tick checkboxes + update the
Status & Progress Log as you go; run `Evaluate Changes` before every commit.

---

## The DRIVE / FOLLOW contract per step type (path-store; INERT at B.1, turned on in B.2)

The flow-store appends the **entered guid**; followers **jump to the frontier** and never re-decide. Per-type runtime care:

| Step type | DRIVE (whoever satisfies first / SP always) | FOLLOW (other peers) |
|---|---|---|
| Question / MiniQuiz / Selection / Quiz | run + accept input; append entered guid + chosen branch guid | show the chosen outcome; **suppress input**; jump to appended guid |
| ConditionsStep (auto-resolving) | evaluate locally; append the resolved next guid | **suppressed** - wait for the appended guid (don't self-resolve) |
| Event / Timeline (no-wait / `waitForEnd`) | run; append on advance | play visuals; follow the frontier (don't auto-advance independently) |
| CueCards | show; append on advance | show; follow advance |
| Insert | whoever inserts drives; append | show inserted state |
| GroupStep | children follow these rules; append the group-exit guid | follow the group-exit guid |
| SessionStart / SessionStop | boundary; append | observe the boundary |

Effects on followers are **display-only** (the driver's run owns the param writes). This table is the **B.2 turn-on spec**.

---

## Status & Progress Log

> Update on EVERY WS start/close + every Evaluate-Changes green run on a milestone. Newest first.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-26 | - | Plan authored from the architecture map (decision log -> 2026-06-26b); B.1 column projected to WS B1.1-B1.8; superseded 2026-06-09 analytics + 2026-06-10 MP-sync plans archived. | Claude Code |
