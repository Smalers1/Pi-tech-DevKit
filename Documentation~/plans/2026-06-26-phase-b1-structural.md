---
title: Phase B.1 - Structural (behaviour-neutral) - the DevKit v1.0 restructure
status: READY to dispatch 2026-06-26 (derived from the architecture map; supersedes the 2026-06-09 plan's structural stance). Pending final sign-off.
date: 2026-06-26
author: Claude Code
owner: Claude Code (DevKit repo)
phase: B.1
gate: EditMode "DevKit > Evaluate Changes" net (Proofs A/B/C) green on EVERY change + the dev-playtest checklist (single-player) + on-device 2-client proofs (multiplayer infra)
references:
  - ../proposed plans/devkit-architecture-map-phase-b.md (the architecture map = SINGLE SOURCE OF TRUTH; decision log 2026-06-19 -> 2026-06-26b)
  - 2026-06-09-phase-a-refactor-and-foundation.md (Phase A - the WS A3 net that gates every change here; the ISceneRunnerControl seam)
  - 2026-06-09-phase-c-integration-and-ship.md (Phase C - roll into AR/VR + ship)
  - _archive/2026-06-09-phase-b-analytics.md (SUPERSEDED - mined only for execution scaffolding: cloud lane, transports, consent step, exit-criteria format)
companion: 2026-06-26-phase-b2-features.md (B.2 = the behaviour-additive features built on this foundation)
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
- [x] Step 1: Define `ILabEventBus` in Core - facts out, fire-and-forget, **sync in-process**, each subscriber wrapped (one can't break the runner), facts carry a **value snapshot** + `attemptId` + `labInstanceId`. *(authored 2026-06-26: `Runtime/Core/ILabEventBus.cs` (interface + `LabEvent` readonly-struct + `LabFactHandler(in)` delegate), `LabEventBus.cs` (alloc-free, subscriber-isolated impl), `LabRuntimeContext.cs` (parent-walk resolver). Pending Unity compile.)*
- [x] Step 2: Create `LabRuntimeContext` on the spawned lab root (ContentDelivery owns the attempt lifecycle); resolve the bus by **parent-walk**, never `XRServices` (the global mis-bind risk, map §5/§7). *(DONE 2026-06-26: `ContentDeliverySpawner.SpawnRoutine` now `AddComponent<LabRuntimeContext>()` on `spawnedInstance` + `Initialize(context.attemptId, new GUID)` AFTER instantiate, BEFORE the post-spawn Restart - so the runner's per-run `Find` resolves it. ADDITIVE + INERT: bus has no subscribers, `LabEventBus.Publish` early-returns on 0 (verified `LabEventBus.cs:26`); skipped when `context==null` (menu/direct). Runtime-only attach -> committed fixtures unaffected (Proof C clean).)*
- [ ] Step 3: Migrate `RuntimeTelemetryAdapter` step/session detection from **string-reflection on the runner (now `LabConsole`)** (`:576`,`:593-667`) + the per-frame `FindObjectsOfType` (`:567`) to **bus subscription** (gives accurate enter/exit timestamps; kills the rename-fragility). *(**UNBLOCKED but RISKY_NEEDS_PLAYTEST** (investigated 2026-06-26): the runner now emits `step.entered`/`step.completed` (Increment 3) AND the context is attached (Step 2), so the bus carries facts. **GAP**: the facts carry only `stepGuid` + a monotonic tick - NOT the step TYPE/Kind or the linear index the current telemetry emits (`step_type`, `detail="index=N"`). Migration must rebuild a `guid->type/index` registry from `scenario.steps` and switch the timestamp source (Stopwatch ticks vs ISO), which has Vicky-ingestion implications. Behaviour-preserving ONLY if the SP playtest proves byte-identical telemetry; recommend a feature flag for rollback. **Author in a dedicated session with the playtest, not blind.**)*
- [x] Step 4: **Reconcile `ScenarioFactKeys`** - delete the dead per-step-bool keys (`flow.step.<guid>`, `scenario.step.<guid>.{done,...}`) the path-list (map §10) supersedes. Zero consumers today = free now, un-removable after 07-07. *(**DONE 2026-06-26**: added `StepEntered`; **deleted** the 11 dead members (`StepKeyPrefix`/`Done|Outcome|CompletedBy|CompletedAtTick`Suffix + the 4 builders + `FlowStepKeyPrefix`/`FlowStep`) after grep-verified ZERO consumers. Only `StepEntered`/`StepCompleted` (the bus fact names) survive. **These are PUBLIC members -> Proof B flagged the removal (the sanctioned reviewed-commit case); the 11 stale lines were removed from `PublicApi.Pitech.XR.txt` (1212->1201) to clear it.**)*
- [ ] Step 5: Run "DevKit > Evaluate Changes" + SP playtest - telemetry output is **identical** (behaviour-preserving migration). *(pending Unity)*

**Acceptance.** Bus in Core; lab-scoped instance resolves by parent-walk; telemetry rides the bus (no reflection/scan); fact keys reconciled; telemetry output unchanged.

**Gate.** Phase A net green; Evaluate-Changes + SP playtest.

---

## WS B1.2 - Unified param/state store (successor to Stats)

**Goal.** One typed store (`ParamValue` union); supersede + migrate Stats; min/max **enforced**; `ILabStateStore` bool-view. (map §8)

**Scope / files.** New param-store service (`Runtime/Scenario/` or a param module); `Runtime/Stats/*` (deprecate); `Runtime/Scenario/Steps/ConditionsStep.cs`; the LabConsole editor window.

**Steps (progress tracking):**
- [x] Step 1: `ParamValue { ParamType tag; float number; NetworkString text }`; store `{ id -> ParamValue }`; **Local** impl = `Dictionary`, **Networked** impl = one `[Networked] NetworkDictionary<id, ParamValue>` (<=64). *(authored 2026-06-26: `Runtime/Core/ParamValue.cs` (managed union; the Fusion unmanaged INetworkStruct variant with `NetworkString<_64>` is the Networking-pass split) + `LocalParamStore.cs` (Dictionary). The **Networked `NetworkDictionary` impl is deferred to the Fusion pass**.)*
- [x] Step 2: Edit-time `ConsoleParameter { id, type, default, min, max, scope }` (declared in the LabConsole window); typed accessors (`GetBool/GetFloat/SetEnum...`); `ILabStateStore.GetState` = the bool accessor. *(authored: `ConsoleParameter.cs`, `IParamStore.cs` (typed + `GetEnum<T>/SetEnum<T>`), `ILabStateStore.cs` bool-view.)*
- [x] Step 3: **min/max ENFORCED (clamp on write).** **[HUMAN-verify]** confirm no existing lab relied on an out-of-range value (Stats' min/max were display-only). *(clamp enforced in `ConsoleParameter.Clamp` whenever a real range is declared (max>min); the opt-out bool was removed in review to honour the map's unconditional "ENFORCED". **[HUMAN-verify] still open.**)*
- [ ] Step 4: Deprecate Stats -> param ops: `StatEffect` -> param op; `ConditionsStep` Stat source -> param read; `StatsConfig` entries -> param declarations; `Quiz.*` magic keys -> params. Migrate the one lab via `[MovedFrom]`/`[FormerlySerializedAs]` + a one-time editor upgrader. **PENDING** - the actual migration touches existing Stats/ConditionsStep + needs the upgrader; `ParamOp` ordinals were locked to `StatOp` so the op migrates by value (review fix).
- [x] Step 5: Per-param **scope** (Local vs Networked/Shared); Networked auto-degrades to local in no-Fusion/SP; relative ops (`Add`/`Multiply`) apply **authority-only + sequenced**; record provenance/actor (no live writer until the post-launch door). *(scope enum + auto-degrade designed; **authority-only+sequenced is the Networked/Fusion impl's job (deferred); provenance deferred** - no live writer.)*
- [ ] Step 6: Proof C on untouched labs (zero-diff) + the migrated lab (clean upgrade). **PENDING Unity** - tier-1 gate already green confirms the additive/inert half is zero-diff; the migrated-lab half follows Step 4.

**Acceptance.** One store supersedes Stats; the Stats lab migrates clean under Proof C; clamp enforced; no lab broke.

**Gate.** Evaluate-Changes (Proof C) + SP playtest.

---

## WS B1.3 - Multiplayer infra (seams + stores, inert at launch)

**Goal.** Build the MP seams so B.2 is a flip-on; SP/AR stays trace-identical. (map §10)

**Scope / files.** `Runtime/Core/` (`IScenarioFlowStore`, `ILabStateStore`); `Runtime/Networking/` (`LocalScenarioPath`, `FusionScenarioPath`, `LocalLabStateStore`, `NetworkedLabStateStore`); VR repo `Assets/Scripts/Multiplayer/NetworkedStates/` (ask-first).

**Steps (progress tracking):**
- [x] Step 1: `IScenarioFlowStore` (append-only entered-guid path the runner writes/reads). Impls: `LocalScenarioPath` (always compiled, single-driver passthrough) + `FusionScenarioPath` (`#if PITECH_HAS_FUSION`, `[Networked, Capacity(256)]` ring). **Internal at launch** via `[InternalsVisibleTo]`. *(authored: `Runtime/Core/IScenarioFlowStore.cs` (internal) + `Runtime/Networking/LocalScenarioPath.cs` + `Runtime/Core/AssemblyInfo.cs` (InternalsVisibleTo Scenario+Networking). **`FusionScenarioPath` deferred to the Fusion pass.**)*
- [x] Step 2: `ILabStateStore` in Core (bool-view); impls `LocalLabStateStore` (always compiled) + `NetworkedLabStateStore : NetworkBehaviour` (`#if PITECH_HAS_FUSION` - relocate the `NetworkStateManager` body TYPES into the DevKit). *(authored: `Runtime/Core/ILabStateStore.cs` + `Runtime/Networking/LocalLabStateStore.cs` (moved Core->Networking in review per map sec-10.2, bool-view over `LocalParamStore`). **`NetworkedLabStateStore` + the `NetworkStateManager` relocation deferred** - Fusion + VR-repo (ask-first).)*
- [x] Step 3: **Scene-authored, not spawned:** one store per lab on the SCENE-MANAGERS root; resolve via `GetComponentInParent<ILabStateStore>()` (kill the `static Instance`); listeners subscribe to `StateChanged` (no `Update` polling). *(`LocalLabStateStore` is a `MonoBehaviour` with `Find()` parent-walk + `StateChanged` - no static Instance, no polling. Scene authoring itself is consumer-side.)*
- [x] Step 4: **Follower-suppression hook in the runner, INERT** (never taken in SP - always DRIVE) so B.2 flips it on (decision 36). *(DONE with B1.7 Increment 3: the `if (_flow != null && _flow.IsDriver) _flow.AppendEntered(...)` guard in `ScenarioRunner.Run()` - a follower (`!IsDriver`) is suppressed from the path write. Inert at launch (`_flow` unbound); the follower frontier-JUMP (read side) is the B.2 turn-on.)*
- [ ] Step 5: **[HUMAN - VR repo, ask-first]** delete VR `NetworkedStates/` copies, reference DevKit types; ship a `[Obsolete]` static facade forwarding to the resolved store.
- [ ] Step 6: Validators: **read-gate-forbidden** (advance may not gate on a shared fact - AR-hang impossible) + **state-budget** (warn 48 / fail 64, measured baseline = 16/64). *(**BLOCKED on B1.2** (investigated 2026-06-26): both validators inspect the lab's authored `ConsoleParameter[]` (read-gate needs the `scope==Networked` set; state-budget counts them) - but **`LabConsole` has no `parameters` field yet** (the param store is authored as Core TYPES but not wired onto `LabConsole`; that's B1.2 Step 4). Authoring against the non-existent field = compile error. Spec is ready (extend `ScenarioGraphSnapshot.CheckInvariants` as a Pass-3; inert by construction - no lab authors Networked facts and baseline is 16/64). **Land WITH B1.2 Step 4 once `LabConsole.parameters` exists.**)*
- [ ] Step 7: Prove SP + AR + one-peer builds are **trace-identical** (Local passthrough).

**Acceptance.** Seams + stores exist; SP/AR trace-identical; follower hook inert; validators live.

**Gate.** Evaluate-Changes + SP playtest + AR no-Fusion trace-identity.

---

## WS B1.4 - Analytics SCHEMA (inert serialized surface only)

**Goal.** Land the frozen serialized analytics contracts - NO observers/scoring/emit (those are B.2). Freeze 07-07. (map §11)

**Scope / files.** `Runtime/Analytics/` (the rubric types); `Runtime/Scenario/Steps/` (`SessionStart`/`SessionStop`); `Runtime/ContentDelivery/LaunchContext`.

**Steps (progress tracking):**
- [x] Step 1: `AnalyticsMetric` (polymorphic `[SerializeReference]`: `StepDuration`/`TotalDuration`/`Drop`/`WrongInteraction`/`Order`) with `ScoringBand[]` (**defaults `none 0 / warning 0.5 / error 1.0`**, author-overridable) - a **sidecar rubric keyed by `step.guid`** (a brick), **NOT fields on `Step`**. *(authored 2026-06-26: `Runtime/Analytics/AnalyticsMetric.cs` + `ScoringBand.cs` + `LabRubric.cs`; pending Unity compile + Evaluate-Changes)*
- [x] Step 2: `TrackedSubject { id, label, target, scenarioRelevant, ownerStepGuid }` (subjects registry) + `Objective { id, label, weight, target, inputs[] }` + `Analytic` (Step/Scene) shapes. *(authored: `TrackedSubject.cs`, `Objective.cs`, `Analytic.cs`. `ObjectiveInput` references the analytic by **id** not an embedded ref - reviewer-ruled SOUND for the measurement/grading split + JSON portability; consistent with the other guid-refs in the schema.)*
- [x] Step 3: `SessionStart` / `SessionStop` step types (the graded bracket). *(authored: `Runtime/Scenario/Steps/SessionStartStep.cs` + `SessionStopStep.cs`)*
- [x] Step 4: **Role enum** (Professor/Participant/Spectator) + per-lab **capacities** (Prof 0-inf / Participant 1-inf / Spectator 0-inf). *(authored: `Runtime/Analytics/SessionRole.cs`; "unlimited" = max `-1`)*
- [x] Step 5: `LaunchContext` carries **tenant id + user id** (role/attempt created in-scene). *(authored 2026-06-26: `LaunchContext.cs` +`tenantId`/`userId` (additive, default empty = inert); done with B1.5 Step 1 `locale`. **contractVersion kept at 1.1.0** - review M1 found the bump is NOT inert (`LaunchContextReporter` emits it in `LaunchLifecyclePayload`), so the bump is a **G2 (06-29) cross-surface** action once fields are populated + Web Portal/cloud aligns. Fields are Proof-B additions-only -> gate green.)*
- [ ] Step 6: Proof C - opted-in steps gain the sidecar; **untouched labs zero-diff**. Freeze the surface for 07-07. **Pending Unity** (cannot run Evaluate-Changes from this environment). `link.xml` Analytics-preserve already added (see B1.6 Step 6). **Open freeze decision:** whether the portable JSON `kind` keys on `Kind`/`KindId` strings or on the CLR type name (see B1.6 Step 2) - settle before 07-07.

**Acceptance.** All types serialize; untouched labs zero-diff (Proof C); surface frozen-ready.

**Gate.** Evaluate-Changes (Proof C zero-diff); freeze 07-07.

---

## WS B1.5 - Localization infra

**Goal.** Relocate the keying pipeline into the DevKit + close the coverage gap + lay the merge seam. (map §12)

**Scope / files.** New `Runtime`/`Editor` Localization module; VR `Assets/Scripts/Editor/Localization/` (relocate source); `LaunchContext`.

**Steps (progress tracking):**
- [x] Step 1: Add `locale` to `LaunchContext` (per-client, never networked). *(authored 2026-06-26: `LaunchContext.locale`, default empty; done with B1.4 Step 5. The relocate/keying/merge-seam steps below remain.)*
- [ ] Step 2: **Relocate** the VR `Editor/Localization/` pipeline (`GlobalObjectId`-keyed manifest + `ManualTranslationIO`) into a DevKit Localization module. *(INVESTIGATED 2026-06-26 - VR source LOCATED at `VR Shell and Labs/HealthOn VR/Assets/Scripts/Editor/Localization/` (`LocalizationPipeline`/`ManualTranslationIO`/`LocalizationScanManifest`/`LocalizationPipelineWindow`) + `Assets/Localization/LanguageSwitcher.cs` + `Assets/Scripts/UI/DoNotLocalize.cs`; namespace `HealthOn.LocalizationTooling`; deps `com.unity.localization` + TMP + **Newtonsoft.Json**. **CANNOT be authored from this environment without risking the green gate / VR build** (must be done with Unity in the loop): (1) the editor asmdef must reference `Unity.Localization.Editor`/`Newtonsoft.Json`, which are NOT DevKit deps -> unresolved refs in the bare gate project (unknown gate impact); (2) the whole module is `#if PITECH_HAS_LOCALIZATION`, so the Evaluate-Changes net compiles it OUT - it can only be verified by a localization-enabled (VR/AR) compile; (3) `LocalizationScanManifest`(SO)/`LanguageSwitcher`/`DoNotLocalize` bind by `.cs.meta` GUID + `LocalizationPipelineWindow` registers `[MenuItem]` -> a clean relocate needs a GUID-carrying move that DELETES the VR copies (atomic; VR-repo #1 rule = ask-first) or a messy GUID re-point; (4) VR consumes the DevKit via `Library/PackageCache`, not the live folder. **PLAN: I author the DevKit module (gated, namespace `Pitech.XR.Localization`, menu -> `Pi tech/Localization/...`); you remove the VR copies + update the package + compile-verify in VR/AR, in a tight loop. Not done blind.**)*
- [ ] Step 3: **Extend keying** beyond scene `TMP_Text` to **(b) data-asset text** (`QuizAsset` prompts/answers) + **(c) code literals** (`"Wrong"`/`"Correct"`) via a `[Localize]`-attribute/reflection scan + a lookup seam.
- [ ] Step 4: Build the **merge seam** (baked StringTables base + cloud-table overlay under the same keys) - logic only; cloud source is B.2/post-launch.
- [ ] Step 5: Evaluate-Changes - additive, no lab regressed.

**Acceptance.** Module in DevKit; data-asset + code-literal text keyed; merge seam compiles; additive (Proof C).

**Gate.** Evaluate-Changes.

### B1.5 EXECUTION HANDOFF (for a future session WITH Unity/VR open - DO NOT attempt from a headless/no-Unity session)

> **Why it was not done 2026-06-26:** the relocate is `#if PITECH_HAS_LOCALIZATION` so the bare DevKit gate compiles it OUT (unverifiable here); the editor asmdef must reference `Unity.Localization.Editor` + `Newtonsoft.Json` which are NOT DevKit deps -> unresolved refs in the bare gate project (could regress the green gate); and a clean relocate must DELETE the VR copies (VR repo #1 rule = ask-first) + VR consumes the DevKit via `Library/PackageCache` (not the live folder). It needs a compile loop. Discriminator already RATIFIED: CLR short type name.

**Source (VR, namespace `HealthOn.LocalizationTooling`):**
- `VR Shell and Labs/HealthOn VR/Assets/Scripts/Editor/Localization/` -> `LocalizationPipeline.cs`, `ManualTranslationIO.cs` (uses **Newtonsoft.Json**), `LocalizationScanManifest.cs` (ScriptableObject + `LocEntry`/`LocEntryStatus`), `LocalizationPipelineWindow.cs` (`[MenuItem("Tools/Localization/Scene Pipeline")]`).
- `VR Shell and Labs/HealthOn VR/Assets/Localization/LanguageSwitcher.cs` (runtime MonoBehaviour, uses `UnityEngine.Localization.Settings`).
- `VR Shell and Labs/HealthOn VR/Assets/Scripts/UI/DoNotLocalize.cs` (runtime marker MonoBehaviour). VR consumers of these types: the `LocalisationTest.unity` test scene + manifest `.asset`s.

**Runbook (each step compile-verified in VR/AR before the next):**
1. DevKit runtime module (`Runtime/Localization/`, ns `Pitech.XR.Localization`, all `#if PITECH_HAS_LOCALIZATION`): add `LanguageSwitcher` + `DoNotLocalize`; add `"Unity.Localization"` to `Pitech.XR.Localization.asmdef` references.
2. DevKit editor module (NEW `Editor/Localization.Editor/Pitech.XR.Localization.Editor.asmdef`, Editor platform, gated `PITECH_HAS_LOCALIZATION`, refs `Pitech.XR.Localization` + `Unity.Localization` + `Unity.Localization.Editor` + `Unity.TextMeshPro` + `Newtonsoft.Json`): add `LocalizationScanManifest`, `LocalizationPipeline`, `ManualTranslationIO`, `LocalizationPipelineWindow`. Change its menu to `"Pi tech/Localization/Scene Pipeline"` (avoid clashing with the VR copy during transition).
3. GUID strategy (pick one): **(a) GUID-CARRY (clean)** - `git mv` each VR `.cs`+`.cs.meta` into the DevKit (preserves the MonoScript GUID so the manifest `.asset` + `LocalisationTest.unity` bind unchanged), then namespace-edit; this DELETES the VR copies in the same step (atomic - confirm with the VR team first). **(b) NEW-GUID** - new files in DevKit, then re-point the few VR assets (regen manifests by re-scan; re-add `LanguageSwitcher`/`DoNotLocalize` in the test scene). (a) is preferred.
4. Remove the VR copies (the 6 files) so there's one source + no duplicate `[MenuItem]` / duplicate GUID. Update the package in VR (`Library/PackageCache`), compile, confirm 0 errors + the pipeline window opens under `Pi tech/Localization`.
5. THEN B1.5 Steps 3-5 (extend keying to data-asset + code literals via a `[Localize]` attr + `ILocalizationLookup` seam; merge seam; Evaluate-Changes).

---

## WS B1.6 - Scenario data durability + JSON round-trip

**Goal.** Protect in-scene labs across the restructure + ship the **full manual JSON round-trip** at launch. (map §9.2, decision 40)

**Scope / files.** `Runtime/Scenario/*` (attributes + lint); a JSON DTO + export/import; `Runtime/link.xml`.

**Steps (progress tracking):**
- [x] Step 1: **(D) migration discipline:** `[MovedFrom]` (type identity) + `[FormerlySerializedAs]` (fields) at **each rename/move**; a one-time editor upgrade pass (open -> re-resolve -> re-save); a **dangling-`nextGuid` lint/repair**. *(DONE 2026-06-26: the dangling-guid **LINT** lives in `ScenarioGraphSnapshot.CheckInvariants` (Proof A). The optional auto-**REPAIR** is now added: `ScenarioGraphSnapshot.RepairDanglingRoutes` / `CountDanglingRoutes` (mirror the lint's EXACT detection - same `Walk`+node-guid set - restricted to the `nextGuid` family -> clear to "" = fall-through; never touch group structural refs or the `[SerializeReference]` list; undo-able) + a dialog-gated "Clear Dangling Routes" button in the Scenario inspector's validation panel. `[MovedFrom]`: the B1.7 rename used **GUID-carry (.meta)** so `[MovedFrom]` was unnecessary (MonoBehaviour binds by GUID); `[FormerlySerializedAs]` + the one-time upgrader attach to the **B1.2 Stats->param migration (deferred, risky)**.)*
- [ ] Step 2: **Freeze the `kind`-discriminated JSON shape** (`schemaVersion`; enum-by-name; field naming). **DECISION (RATIFIED 2026-06-26 by Stergios):** the `kind` discriminator is the **CLR short type name** (e.g. `"MiniQuizStep"`), **NOT `Step.Kind`**. Rationale (code-as-truth): `Step.Kind` is MUTABLE DISPLAY copy - we just changed `SessionStartStep.Kind` `"SessionStart" -> "Session Start"` (a spacing/formatting change, same type); a `Kind`-keyed JSON contract would have silently broken every exported SessionStart on that change. The CLR type name is stable, changes only on a type rename (tracked by `[MovedFrom]`, the discipline we already keep), and aligns with how Unity already discriminates these `[SerializeReference]` types (`managedReferenceFullTypename`, used by `ScenarioGraphSnapshot`). Carry the display `Kind` as an OPTIONAL informational `label` for Portal/human readability. *(This OVERRIDES the investigation agent's "use Step.Kind" recommendation - the SessionStart rename is the counter-evidence. Awaiting Stergios' ratification, then it freezes.)*
- [ ] Step 3: Implement JSON **export** (lab -> JSON; scene-independent reference resolution).
- [ ] Step 4: Implement JSON **import** (JSON -> lab) - the full manual round-trip; in-scene stays canonical (not a source-of-truth flip).
- [ ] Step 5: Round-trip a real lab; **Proof A (graph integrity) + Proof C (serialization) stay green.**
- [x] Step 6: Add the new types (`SessionStart/Stop`, `FusionScenarioPath`, typed params) to `Runtime/link.xml` (IL2CPP strip-safety). *(DONE for everything that exists: `Pitech.XR.Analytics` preserve added (rubric `[SerializeReference]`); `SessionStart/StopStep` covered by the `Pitech.XR.Scenario` preserve; **typed params (`ParamValue`/`ConsoleParameter`/`ParamType|Scope|Op`) confirmed covered by the existing `Pitech.XR.Core` preserve** (verified 2026-06-26). Only `FusionScenarioPath` remains - it is **not authored yet** (Fusion pass), so its link.xml entry lands when the type does.)*

**Acceptance.** Renames don't orphan steps; the round-trip survives Evaluate-Changes on a real lab; link.xml updated.

**Gate.** Evaluate-Changes (Proof A + C).

---

## WS B1.7 - Runner extraction + `SceneManager -> LabConsole` rename  *(CAN_TRAIL - the slip sink; runs LAST)*

**Goal.** Extract the run-engine; rename to LabConsole; **direct-reference internal wiring**; the seam stays ignition-only. (map §6/§9.1, decision 34)

**Scope / files.** `Runtime/Scenario/SceneManager.cs` (-> LabConsole + inner runner); `Runtime/Core/ISceneRunnerControl.cs`; `Editor/Core.Editor/` (Hub typed refs); `.meta` GUIDs.

**Steps (progress tracking):**
- [x] Step 1: Extract the run-engine into a dedicated **inner runner** type (type-first within `Pitech.XR.Scenario`; own assembly later). **Do NOT touch the `Run*` bodies** (twins diverge - dedup is post-launch). *(**Increment 1 done 2026-06-26**: `Runtime/Scenario/ScenarioRunner.cs` (internal sealed; `SceneManager` owns + directly drives it; reads host state via forwarding members). Engine moved **BYTE-EXACT** via scripted line-extraction - adversarial diff of the moved region was EMPTY (2335 lines identical modulo the 12 `this`->`_console` Debug-context args); the Run*/RunXGroup twins kept divergent. Compile fix from review: `DeactivateAllVisuals` -> `internal` (host calls it cross-class, CS0122). Pending: your Unity compile + gate + **SP dev-playtest** (the only runtime-equivalence proof).)*
- [x] Step 2: Rename `SceneManager -> LabConsole`; LabConsole keeps serialized authoring + the param store + **owns and directly drives** the inner runner. *(**Increment 2 done 2026-06-26**: `SceneManager.cs`->`LabConsole.cs` via filesystem `mv` (+ `.cs.meta` carried BYTE-IDENTICAL, GUID `2d431a49...` preserved -> prefabs bind unchanged); class renamed (file-name==class-name); `[AddComponentMenu]` -> "Lab Console". Every compile/reflection ref updated: 3 runtime FullName sites, in-assembly typed refs (`SceneManagerEditor`/`ScenarioGraphWindow`), Hub string-resolvers (values->LabConsole, strings kept), GUID-repair tools (incl. `"Scenario/LabConsole.cs"` source-path), `MegaFixtureBuilder`, the simple-name test refs. Gate baselines: `ScriptGuids.json` (type->LabConsole, GUID unchanged) + `ScriptGuidStabilityTests` + `CoreEditorTypeLiteralTests` + `PublicApi.Pitech.XR.txt` (24 lines prefix-swapped + re-sorted canonical Ordinal, sigs byte-identical). Adversarial 4-lens verify: PASS. **Editor class names `SceneManagerEditor`/`SceneManagerService` kept (not GUID-bound); cosmetic UI copy/comments + the 3 prefab `m_EditorClassIdentifier` lines deferred to Increment 4 (Unity rewrites the prefabs on save; gate treats the mismatch as soft-Inconclusive, never RED).)*
- [x] Step 3: **Internal wiring = plain direct references** (no internal Core interface); LabConsole **hands the runner the param-store service** (dependency runner->store). *(direct-ref wiring DONE - `LabConsole` owns `_runner` and drives it via direct calls/forwarders, no Core interface. The param-store HANDOFF to the runner is **B1.2 Step 4** (Stats->param migration) - the store isn't consumed by the runner until then.)*
- [x] Step 4: **`ISceneRunnerControl` (3 members) = cross-assembly ignition ONLY**, implemented by LabConsole, **NOT widened**. *(unchanged in the rename - `LabConsole` implements the same 3-member seam; not widened.)*
- [x] Step 5: Wire the runner's **two outputs** - (1) fact emission onto the bus; (2) flow-store write+read via `IScenarioFlowStore`. *(**Increment 3 done 2026-06-26**: `ScenarioRunner` emits `step.entered`/`step.completed` onto the bus via `LabRuntimeContext.Find(_console)` (resolved ONCE per run, null at launch) + appends the entered guid to `_flow` (an `IScenarioFlowStore`, unbound at launch) guarded by `IsDriver`. BOTH neutral by construction - context null -> no Publish; `_flow` null -> no append. `BindFlowStore` is the B.2 injection seam. Adversarial verify: PASS (inert, compiles). Two review nits fixed: context resolved per-run (no cache-null-forever B.2 hazard) + monotonic `Stopwatch.GetTimestamp()` tick.)*
- [x] Step 6: Migrate the **DevKit Hub off string-wiring to typed refs**; preserve `SceneManager.cs.meta` + `Scenario.cs.meta` GUIDs. *(per the Execution-detail ruling: Core.Editor MUST stay decoupled, so the Hub KEEPS string-resolution - only the literal VALUES moved to `"Pitech.XR.Scenario.LabConsole"`; the in-assembly sites (`ScenarioGraphWindow`, `SceneManagerEditor`) use typed refs. `LabConsole.cs.meta` GUID preserved; `Scenario.cs.meta` untouched.)*
- [ ] Step 7: **Full dev-playtest sign-off** + Evaluate-Changes - single-player behaviour identical. **[HUMAN]** - recompile -> `Evaluate Changes` -> SP dev-playtest. (The gate proves structure/serialization; only the playtest proves the engine PLAYS identically.)

**Acceptance.** Runtime SP behaviour identical (Evaluate-Changes + full playtest sign-off); seam unwidened; Hub typed; no orphaned steps.

**Gate.** Evaluate-Changes + the full dev-playtest checklist.

### Execution detail (code-mapped 2026-06-26 - the load-bearing mechanics)

- **The file:** `Runtime/Scenario/SceneManager.cs` is **2519 lines**; `public class SceneManager : MonoBehaviour, ISceneRunnerControl` (`:21`). Run engine = `Run()` (`:169`) + `FindIndexByGuid` (`:264`) + 10 `RunX` + 8 `RunXGroup` twins + group machinery (`RunGroupInternal :1633`, `StartGroupChild :1845`, `EvalMultiConditionBranches :1774`) + `EditorSkipFromGraph` (`:1536`). **The twins DIVERGE** (e.g. `RunQuestionGroup` first-click guard `:2165` vs `RunQuestion` debounce `:508`) - **do NOT dedup; move bodies verbatim.**
- **GUID-safety crux (the rename):** `SceneManager.cs.meta` guid = **`2d431a49d183e9c428369f7f758f75cd`** (pinned in the .meta, `ScriptGuidStabilityTests`, `Tests/Baseline/ScriptGuids.json:40`, and shipped prefab YAML `m_Script`). Procedure: **`git mv` BOTH** `SceneManager.cs -> LabConsole.cs` AND `SceneManager.cs.meta -> LabConsole.cs.meta` (meta byte-identical), rename the class, keep namespace+assembly `Pitech.XR.Scenario`. Unity requires **file name == class name**. **`[MovedFrom]` is NOT needed** (a MonoBehaviour binds by m_Script GUID, not by a `[SerializeReference]` managed-type identity). If the meta is not carried -> every lab prefab shows "Missing (Mono Script)" -> data loss. Unity auto-rewrites the prefabs' `m_EditorClassIdentifier` to `LabConsole` on next save (expected, content-neutral).
- **THREE runtime `FullName` reflection sites break on rename** (not one): `RuntimeTelemetryAdapter.cs:576`, `ContentDeliverySpawner.cs:1121`, `AddressablesBootstrapper.cs:113` (all `"Pitech.XR.Scenario.SceneManager"`). Spawner + bootstrapper updates are **mandatory** (else autoStart-defer + post-spawn Restart silently break for Addressable labs); the telemetry one is a **one-line stopgap** until B1.1 Step 3 deletes that reflection block. Member-name reflection (`SceneRunnerReflection` `autoStart`/`Restart`, telemetry `StepIndex`/`scenario`/`steps`/`guid`) **survives** the class rename - leave it.
- **Editor/test/baseline updates (compile-required, same commit):** `SceneManagerEditor.cs:13` `[CustomEditor]` + casts; `ScenarioGraphWindow.cs:1596-1607,1883-1885` typed refs; Hub FullName literals (`AuthorPage.cs:18`, `ReferencePage.cs:62,82`, `SceneManagerService.cs:41,45`, `QuizService.cs:46,48`) - **keep string-resolution** (Core.Editor must NOT take a hard ref to Scenario; "typed refs" = the in-assembly sites only); `CoreEditorTypeLiteralTests.cs:28`, `ScriptGuidStabilityTests.cs` + `ScriptGuids.json` (**type string -> LabConsole, GUID value UNCHANGED**), `GoldenTraceRecorder.cs:52,84`.
- **Public-API baseline = the one SANCTIONED non-additive diff:** the rename reads as ~23 removals + 23 additions in `Tests/Baseline/PublicApi.Pitech.XR.txt` -> regenerate it in the rename commit; the only delta must be `SceneManager`->`LabConsole` with member signatures byte-identical. `link.xml` needs **no** edit (whole `Pitech.XR.Scenario` assembly already preserved).
- **The two outputs (Step 5) are neutral BY CONSTRUCTION, not by assumption:** bus emit resolves via `LabRuntimeContext.Find(this)` which returns **null today** (ContentDelivery doesn't attach the context until B1.1 Step 2) -> `Publish` never runs -> zero delta; flow-store `LocalScenarioPath.IsDriver` is **always true** in SP -> the follower-suppression branch is dead code, the append feeds nothing back. Wire both dormant so B.2 turn-on is a no-runner-edit swap.
- **Increment order (each ends with gate + the relevant playtest; rename ISOLATED):** **0** preflight (confirm gate green on the unmodified runner) -> **1** extract the runner (no rename; runner reads host fields via `_console.<field>`; gate is fully zero-diff here, proves extraction-only neutrality) -> **2** the rename (isolated; meta-carry + all type/string/baseline updates) -> **3** wire the two dormant outputs -> **4** docs/formatting.
- **[HUMAN] the gate CANNOT prove (must be verified in Unity):** (a) the `.meta` GUID actually resolves post-import (Proof A tripwire); (b) **SP runtime equivalence via the dev-playtest checklist** - especially the group/twin micro-behaviours, debounce, group-exit branching - static analysis only proves the bodies didn't change, never that they *play* identically; (c) the Addressable spawn flow (autoStart-defer + post-spawn Restart) after the FullName updates.

---

## WS B1.8 - Hygiene (launch-critical fixes)

**Goal.** Close the defects the restructure rides on. (map §5/§13)

**Scope / files.** `Runtime/ContentDelivery/AddressablesRemoteUrlRewriter.cs`; `Editor/Stats.Editor/` (new asmdef).

**Steps (progress tracking):**
- [x] Step 1: **`AddressablesRemoteUrlRewriter` global-clobber (P0 for UaaL):** on `Install` **capture** the prior `InternalIdTransformFunc` and **chain** to it (don't overwrite); on `Uninstall`/`Clear` **restore the captured prior** (never null unconditionally) (`AddressablesRemoteUrlRewriter.cs:118`). *(authored 2026-06-26: `_priorTransform` captured on Install; `TransformLocation` chains prior-first then applies our CCD pin; Uninstall restores it (never null). Behaviour-neutral when no prior is installed (identical to pre-fix); snapshot-under-lock, prior invoked outside `Sync`. Adversarial review: FIX-CORRECT.)*
- [x] Step 2: **Acceptance test:** a UaaL regression test - the host RN app's Addressables transform survives a DevKit install->uninstall cycle. *(DevKit-side: `Tests/Editor/AddressablesRemoteUrlRewriterTests.cs` - 8 pure parse/rewrite tests, gate-runnable, review TESTS-CORRECT. The install/uninstall **LIFECYCLE regression is the host RN app's UaaL test [HUMAN, cross-surface]** - the bare DevKit test asmdef references no Addressables, so the lifecycle can't run here.)*
- [x] Step 3: Add `Pitech.XR.Stats.Editor.asmdef` so the orphaned `[CustomEditor]` scripts compile into the package. *(authored 2026-06-26: `Editor/Stats.Editor/Pitech.XR.Stats.Editor.asmdef` (refs `Pitech.XR.Stats`, Editor platform; UnityEngine.UI auto-ref'd). Unity generates the `.asmdef.meta` on import - commit it.)*
- [ ] Step 4: Confirm the `RuntimeTelemetryAdapter` `FindObjectsOfType` runner-bind is gone (folded into B1.1). **PENDING** - lands with B1.1 Step 3 (telemetry-onto-bus migration), which is gated on the runner emitting facts (B1.7/B.2).

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

## Editor-tooling backlog (surfaced during B.1; editor-only, NOT B.1-blocking - fix when convenient)

- **ScenarioGraph "Add step" throws NRE when the graph is not linked to a Scenario.** `ScenarioGraphWindow.CreateStep` (`:2572`, `scenario.steps.Add(inst)`) throws `NullReferenceException` when the window has no bound `scenario` and the user picks **Add ▸ <step>** (menu built in `ShowCreateMenu`, `:2147-2159`). **Fix:** guard `CreateStep` / the Add menu on `scenario == null` and pop a clear message ("This Scenario Graph is not linked to a Scenario - open or select one first") instead of throwing. **Better:** dynamically resolve the OPEN scene's Scenario (the first `Scenario` under the active scene, or via the scene's `LabConsole.scenario`) and bind to it so Add just works without manual linking. (Reported 2026-06-26.)
- **Hub "Create Scenario GameObject" should create AND assign, like the LabConsole flow.** The Setup/Author Hub action (`ScenarioService.CreateScenarioGameObject`) creates an orphan Scenario object; it should mirror `SceneManagerEditor.CreateAndAssignScenario` - create the Scenario under the managers root AND assign it onto the scene's `LabConsole.scenario` (wire the LabConsole <-> Scenario link) so the user gets a ready-wired setup instead of an unlinked Scenario. (Reported 2026-06-26.)

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
| 2026-06-26 | B1.4 / B1.6 / B1.7 / B1.5 | **Post-green cleanup + 2 easy-win steps.** Gate confirmed GREEN. Fixed the leftover "Scene Manager" copy the Inc-4 sweep missed: Hub **Setup** page button (`SetupPage.cs`, not in the sweep list) + 3 stray comments (`ScenarioGraphWindow`, `MegaFixtureBuilder` x2) -> verified the remaining `SceneManager` tokens are all legit (Unity `SceneManagement` / kept editor class names / the rename-doc comment) + the fixture `m_Name: Scene Manager` left as shipped test-data. **Scenario authoring gap fixed (user-reported):** `SessionStartStep`/`SessionStopStep` had no ADD option -> added "Add/Session Start" + "Add/Session Stop" to BOTH the graph context-menu (`ScenarioGraphWindow` CreateStep menu) and the inspector dropdown (`ScenarioEditor` AddStep menu) + the inspector kind-badge mapping (completes B1.4 Step 3 usability). **B1.6 Step 1 DONE** (dangling-route auto-repair: `ScenarioGraphSnapshot.RepairDanglingRoutes`/`CountDanglingRoutes` + inspector "Clear Dangling Routes" button, mirrors the lint's exact detection, nextGuid-family only, undo-able). **B1.5**: recorded a self-contained EXECUTION HANDOFF runbook (see WS B1.5) so a future Unity-in-the-loop session can run the relocate; left unbuilt here (would risk the green gate via unresolved asmdef refs + needs a localization-enabled compile). All edits this turn are additive/editor-only -> recompile + the existing gate to confirm. | Claude Code |
| 2026-06-26 | B1.7 / B1.5 | **B1.7 Increment 4 (docs/cosmetic) DONE -> B1.7 is CODE-COMPLETE; B1.5 fully scoped + blocked-on-Unity-loop.** **Inc 4**: 3-agent parallel cosmetic sweep updated 23 files / 46 edits - stale "SceneManager"/"Scene Manager" in COMMENTS + UI STRINGS -> "LabConsole"/"Lab Console" (tooltips, [Header]s, dialogs, button + created-GameObject names, Hub prose, stale source-path comments). **0 defects**: verified the kept identifiers (`SceneManagerEditor`/`SceneManagerService`/`CreateSceneManager`/`USceneManager`/Unity `SceneManagement.SceneManager`) + the 12 `"Pitech.XR.Scenario.LabConsole"` FullName literals are all intact. + extended `GuessTypeFromObjectName` to also match "lab console" (new created-object name) keeping "scene manager" for back-compat. **B1.7 now needs only the [HUMAN] gate + SP dev-playtest (Step 7).** **B1.5**: VR project FOUND in workspace (`VR Shell and Labs/HealthOn VR` - CLAUDE.md "not present" notes are STALE; memory saved). Localization source + deps mapped; discriminator ratified (CLR type name). **Relocate CANNOT be authored from here without risking the green gate (unresolved optional asmdef refs) or the VR build (gated out of the gate; atomic VR-copy removal + PackageCache) - must run with Unity in the loop (plan recorded at B1.5 Step 2).** | Claude Code |
| 2026-06-26 | B1.1 / B1.3 / B1.5 / B1.6 | **Remaining-B.1 triage (7-stream parallel investigation) + B1.1 Step 2 landed.** Gate confirmed GREEN after the dead-key baseline fix. **B1.1 Step 2 DONE**: `ContentDeliverySpawner` attaches `LabRuntimeContext` to the spawned lab root + `Initialize(attemptId, GUID)` before the post-spawn Restart; additive + inert (bus has no subscribers; runtime-only attach so Proof C clean). **Triage of the rest (code-verified, not plan-claimed):** **B1.3 Step 6 validators = BLOCKED on B1.2** (they inspect `LabConsole.parameters`, which doesn't exist yet - the agent assumed it). **B1.1 Step 3 telemetry + B1.2 Step 4 Stats = RISKY_NEEDS_PLAYTEST** (behaviour-preserving migrations; bus facts lack step type/index; Stats needs an upgrader + Proof C - author in dedicated sessions WITH the playtest). **B1.6 Step 3 JSON export = substantial** (`JsonUtility` can't do the `[SerializeReference]` step list -> needs a custom serializer like `BuildSnapshotJson`). **B1.6 Step 2 discriminator DECIDED (pending ratification): CLR short type name, NOT `Step.Kind`** (the `SessionStart->"Session Start"` change proves Kind is mutable display copy). **B1.5 = blocked on VR source** (`LocalizationPipeline.cs`/`ManualTranslationIO` not in this workspace; a reserved empty `Pitech.XR.Localization` module exists). **B1.6 Step 1 dangling-repair = ready (spec'd), defer to next.** | Claude Code |
| 2026-06-26 | B1.7 / B1.1 / B1.3 / B1.6 | **Increments 2+3 + dead-key + link.xml landed.** **(2) Rename `SceneManager->LabConsole`**: `mv` .cs+.cs.meta (GUID `2d431a49...` byte-identical -> prefabs bind unchanged), class+menu renamed, all compile/reflection refs swapped (3 runtime FullName sites, in-assembly typed refs, Hub string VALUES, GUID-repair tools incl. source-path, MegaFixtureBuilder, test simple-name refs), gate baselines updated (`ScriptGuids.json` GUID-unchanged, `ScriptGuidStabilityTests`, `CoreEditorTypeLiteralTests`, `PublicApi` 24 lines swapped+re-sorted byte-identical sigs). **(3) Two dormant outputs** in `ScenarioRunner`: bus emit (`LabRuntimeContext.Find`, resolved per-run, null at launch) + flow-store `AppendEntered` under the inert `IsDriver` hook (B1.3 Step 4) + `BindFlowStore` seam; neutral by construction. **B1.1 Step 4**: deleted 11 zero-consumer dead `ScenarioFactKeys` members. **B1.6 Step 6**: param types confirmed Core-preserved. **Adversarial 4-lens verify: 0 blockers.** Gate caught the public-API removal of the dead keys (Proof B) -> the 11 stale lines removed from `PublicApi.Pitech.XR.txt` (1212->1201, the sanctioned reviewed-commit). Review nits fixed (per-run context resolve; monotonic tick). **NOT YET: [HUMAN] recompile + `Evaluate Changes` + SP dev-playtest; cosmetic UI copy/comments = Increment 4; B1.3 Step 6 validators next.** | Claude Code |
| 2026-06-26 | B1.7 | **Increment 1 (run-engine extraction, no rename) done.** Run-engine moved from `SceneManager.cs` (2519->192 lines, host: fields/lifecycle/forwarders) into new `Runtime/Scenario/ScenarioRunner.cs` (internal sealed, 2393 lines) via **byte-exact scripted line-extraction** (orig 84-102 + 169-1003 + 1017-2497), forwarding members so bodies are verbatim. Two adversarial reviews: **EXTRACTION-FAITHFUL** (moved-region diff EMPTY modulo 12 `this`->`_console`) + **COMPILE/GATE** pass after one fix (`DeactivateAllVisuals` -> internal, the CS0122 the user hit). `SceneManager.cs` keeps its name + `.meta` GUID 2d431a49... (prefabs bind unchanged); public API unchanged; serialized surface unchanged. **NOT YET: rename (Increment 2), the two runner outputs bus+flow-store (Increment 3), and your SP dev-playtest.** | Claude Code |
| 2026-06-26 | ALL | **Gap-review of all B.1 work** (adversarial completeness audit) + **B1.7 code-mapping** (2 agents). Audit: no blockers, foundation coherent. Fixes applied: **M1** reverted `LaunchContext.contractVersion` to 1.1.0 (the bump is NOT inert - `LaunchContextReporter` emits it; bump at G2); **M2** `SessionStart/Stop` Kind -> "Session Start"/"Session Stop" (match the spaced display convention); **N1** `LocalLabStateStore.SetState` now change-only; **N2** `LocalLabStateStore.Find` resolves `ILabStateStore` (finds the networked twin too). Open pre-07-07 must-dos confirmed: delete dead `ScenarioFactKeys` keys (B1.1 Step 4); decide the Kind-vs-CLR JSON discriminator + enum-type-id on ConsoleParameter. **B1.7 execution plan recorded** (see WS B1.7 Execution detail): file is 2519 lines, GUID `2d431a49...` carry, 3 runtime FullName sites, public-API baseline regen, increment order 0-4, [HUMAN] dev-playtest is the only runtime-equivalence proof. | Claude Code |
| 2026-06-26 | B1.8/B1.6 | **Addressables clobber fix (P0)** authored + reviewed: `AddressablesRemoteUrlRewriter` now captures + chains the prior `InternalIdTransformFunc` on Install and restores it on Uninstall (was: overwrite + unconditional null). + `Tests/Editor/AddressablesRemoteUrlRewriterTests.cs` (8 pure parse/rewrite tests, gate-runnable). Adversarial review: FIX-CORRECT + TESTS-CORRECT, no BLOCKER/MAJOR. B1.8 Step 2 lifecycle regression = host RN app UaaL test (cross-surface, [HUMAN]). **Code-as-truth:** B1.6 dangling-guid LINT already lives in `ScenarioGraphSnapshot.CheckInvariants` (Proof A) - not re-implemented; only auto-repair would be new. | Claude Code |
| 2026-06-26 | B1.2 | **Param store authored** (Core, Stats successor, inert): `ParamTypes` (ParamType/ParamScope/ParamOp), `ParamValue` (union), `ConsoleParameter` (min/max enforced), `IParamStore` (typed + `GetEnum<T>`), `LocalParamStore` (StatEffect-faithful ops, change-only `ParamChanged`). Steps 1/2/3/5 authored (partial); Step 4 Stats migration + Step 6 Proof C pending. **Not yet recompiled in Unity.** | Claude Code |
| 2026-06-26 | B1.3 | **MP seams authored** (inert): `IScenarioFlowStore` (Core, internal) + `LocalScenarioPath` (Networking) + `AssemblyInfo` InternalsVisibleTo; `ILabStateStore` (Core) + `LocalLabStateStore` (Networking, bool-view over the param store). Steps 1/2/3 authored (partial - Fusion impls + VR `NetworkStateManager` relocation deferred); Steps 4-7 pending. | Claude Code |
| 2026-06-26 | B1.8/B1.4/B1.5 | `Pitech.XR.Stats.Editor.asmdef` added (B1.8 Step 3); `LaunchContext` +tenantId/userId (B1.4 Step 5) +locale (B1.5 Step 1), contractVersion 1.1.0->1.2.0 (G2 cross-surface; no test pins it). | Claude Code |
| 2026-06-26 | B1.2/B1.3 | **Adversarial review** (3-lens Workflow: unity-correctness + map-faithfulness + gate-impact). Verdict: COMPILE-SAFE, GATE-STAYS-GREEN, faithful w/ minor drift. Fixes: `ParamOp` reordered to `StatOp` ordinals (MAJOR - value-safe migration); `LocalLabStateStore` moved Core->Networking (map sec-10.2); `ConsoleParameter` clampToRange opt-out removed (map "ENFORCED"); `LocalParamStore` no-op `ParamChanged` suppression (mirrors StatsRuntime); Stats.Editor Core ref dropped. Open 07-07 flags: enum-type-id on `ConsoleParameter` (vs JSON enum-by-name); deferred Fusion impls. | Claude Code |
| 2026-06-26 | B1.4 | **Analytics rubric schema authored** (inert, additive). New files under `Runtime/Analytics/`: `ScoringBand.cs` (BandSeverity + ratified defaults none 0/warn 0.5/err 1.0), `AnalyticsMetric.cs` (abstract + 5 kinds), `TrackedSubject.cs`, `Analytic.cs` (Step/Scene), `Objective.cs` (Objective + ObjectiveInput-by-id), `SessionRole.cs` (enum + capacities), `LabRubric.cs` (container). Steps 1-4 done; Step 5 (LaunchContext tenant/user) DEFERRED to a LaunchContext pass (G2 item, bundle w/ B1.5 locale); Step 6 pending Unity Evaluate-Changes. **Not yet compiled in Unity.** | Claude Code |
| 2026-06-26 | B1.1 | **LabEventBus notification plane authored** (Step 1, inert). New files under `Runtime/Core/`: `ILabEventBus.cs` (interface + `LabEvent` readonly-struct fact + `LabFactHandler(in)` delegate), `LabEventBus.cs` (alloc-free, subscriber-isolated, re-entrancy-safe impl), `LabRuntimeContext.cs` (per-attempt root; parent-walk resolver, no XRServices). Step 4 PARTIAL: added `ScenarioFactKeys.StepEntered` (the StepDuration enter-tick fact). Steps 2/3/5 pending (existing-file wiring / Unity). | Claude Code |
| 2026-06-26 | B1.4/B1.6 | `Runtime/link.xml`: added `Pitech.XR.Analytics` assembly preserve (IL2CPP strip-safety for the polymorphic `[SerializeReference]` rubric). Advances B1.6 Step 6 for the Analytics types. | Claude Code |
| 2026-06-26 | B1.1/B1.4 | **Adversarial review run** (2 parallel reviewers: Unity-compile/serialize + map-faithfulness). Verdict: COMPILE-SAFE, SERIALIZE-SAFE, FAITHFUL-TO-SPEC; no BLOCKER/MAJOR code defects; `ObjectiveInput.analyticId` deviation ruled SOUND. Fixes applied: softened the `Kind`-is-the-JSON-discriminator doc claims (it is a kind tag; the JSON `kind` mapping is a B1.6 decision); added `step.entered` key. Open items flagged: (a) settle the `Kind` vs CLR-typename JSON-discriminator question before 07-07; (b) map §11.1 prose (metric scope) should be reconciled to the §11.3 diagram (scope is structural, on the analytic) - **propose, not yet edited the ratified map**. | Claude Code |
