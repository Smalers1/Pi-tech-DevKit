---
title: Phase A - Refactor & Foundation (behaviour-neutral)
status: RATIFIED by Petros (board) 2026-06-10 - Stergios sign-off + DISPATCHED 2026-06-10; IN PROGRESS (execution started at WS A1)
date: 2026-06-09
owner: Stergios & Alexandros
reviewers:
  - Marie (reviewer)
  - Diego (reviewer fallback)
phase: A
gate: WS A3 net (THE gate) green on unmodified code; store-submission gate PIT-369 binding 2026-08-15
launch: 2026-09-10 (controlled commercial B2B; store gate 2026-08-15)
provenance: SINGLE SOURCE OF TRUTH for Phase A. Merges Stergios' FINAL behaviour-neutral plan (2026-06-08; historically called "DevKit P1" - that name is RETIRED, see Terminology) with the board launch framing (2026-06-09). Stergios' original text is archived for provenance at _archive/2026-06-08-p1-stergios-final.md; on a suspected transcription divergence, check the archive, then fix THIS doc - this doc is what gets executed.
references:
  - 2026-06-09-devkit-launch-plan.md (umbrella / index)
  - 2026-06-09-phase-b-analytics.md (Phase B - the first behaviour-additive work)
  - 2026-06-09-phase-c-integration-and-ship.md (Phase C - integrate + ship)
  - _after-launch/2026-06-09-after-launch-plan.md (post-launch Phases D..I + domain systems)
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md (architecture; §8 Runtime; §28 domain & content systems incl. §28.6 Unity 6+ baseline)
  - _archive/2026-06-08-p1-stergios-final.md (Stergios' original FINAL text - provenance archive)
  - _archive/2026-04-23-p1-foundation.md (the OLD foundation plan - superseded, history only)
---

# Phase A - Refactor & Foundation (behaviour-neutral)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (or superpowers:subagent-driven-development)
> to implement WS-by-WS. Steps use `- [ ]` checkbox syntax - **tick a box when the step is verifiably done**, and add a
> row to the **Status & Progress Log** (bottom of this doc) on every WS start/close. This doc is SELF-CONTAINED: the
> census is Appendix A, the WS A3 ticket set + asmdef JSON + test specs are Appendix I. Local-only edits; Petros runs git.
>
> **Completion discipline (Petros, 2026-06-10): every phase completes IN FULL - every small step ticked, none
> skipped.** Steps tagged **[HUMAN]** are human-owned; AI agents working this phase MUST remind the human owner of
> any unticked [HUMAN] step and must not declare a WS done while one is open.

> **Terminology - the numbered phases are RETIRED (Petros 2026-06-10).** This phase was historically called "DevKit
> P1"; it is now simply **Phase A** of one lettered sequence: **A -> B -> C (launch) -> D -> E -> F -> G -> H -> I
> (after-launch plan)**. Post-launch: **Phase D** runtime foundation + runner extraction, **Phase E** LabConsole +
> multiplayer, **Phase F** authoring/objectives + content systems, **Phase G** Vicky Observer, **Phase H** Vicky
> Interactive + Director foundation, **Phase I** v1.0 API lock. The spec's §17 internal numbering maps via §28.7.
> Never write a bare "P1/P2/P3."

**Goal:** Professionalize the DevKit (usability, efficiency, scalability - the north star) and lay the
**behaviour-neutral foundation** that makes the behaviour-additive launch work (Phase B analytics, Phase C migration)
**provably safe** to land on top of shipped university labs. Phase A is the foundation, not the product: its single
load-bearing deliverable is the **WS A3 EditMode net** that Phase B and Phase C draw against.

**Governing law:** A change is admitted to Phase A **if and only if** it passes all three equivalence proofs
simultaneously (§1), *and* the proof harness exists when the change lands. Failing any one = behaviour change =
deferred out of Phase A.

**Architecture stance:** Behaviour-neutral / additive-only. No emission, no runner extraction, no serialized field
renamed, no `dependencies` block, no version bump. The only "analytics foundation" admitted here is a set of
behaviour-neutral SEAMS (consts vocabulary, reserved module slots, the `ISceneRunnerControl` interface, the net itself).

**Spec reference:** `../specs/2026-04-23-devkit-1.0-target-architecture-design.md` - §8 (Layer 2 Runtime), §6 (layer
model), §13 (DevKit Hub), **§28** (domain & content systems: §28.1 ownership, §28.2 Networking, §28.3 Localization,
§28.4 Vitals, §28.5 AI-authoring seam, **§28.6 Unity 6+ baseline**). Phase A only **reserves** the §28 module slots;
their logic is Phase B / post-launch.

**Unity baseline (Petros, 2026-06-09):** Unity 6+ (6000.0) is the project/test/build baseline. `package.json
version` stays `0.10.5` through Phase A; the `unity`-field floor-bump + `dependencies` block land together as ONE
Phase D (post-launch) metadata cutover (spec §28.6). Where Appendix I says "host Unity project", read **Unity 6+**.

**Duration / window:** **2026-06-02 -> 2026-06-27** (Workspace plan-of-record; In Progress). WS A3 lands first as the gate; the deep-split tail (WS A4..A7) may run
in parallel or slip if it threatens the store deadline. DevKit v1.0 lock target 2026-09-07; PIT-369 store gate
binding 2026-08-15 (no slip room).

**Exit criteria (measurable):** see §4 - the full positive + negative checklist. Headlines: census complete (A1);
WS A3 net GREEN on unmodified code (THE gate); Hub cockpit + single "Pi tech" root + four reserved slots (A2); every
split/deletion proven neutral (A4/A5/A6); docs/metadata professionalized with NO deps block/version bump (A7);
`ISceneRunnerControl` additive (A8); all negative gates hold.

---

## Board addendum (Claude, 2026-06-09) - the launch-context layer

### (a) Phase A's role in the A/B/C launch sequence

The DevKit ships into a hard calendar: **2026-09-10 = controlled commercial B2B launch** (5 existing paid beta
universities onboard day 1; no public signup), with the **store-submission gate (PIT-369) binding at 2026-08-15**.
**No slip room** on that gate; it is the binding constraint on the whole DevKit plan.

| Launch phase | What | Target | Owner |
|---|---|---|---|
| **A - Refactor & Foundation** | behaviour-neutral professionalization + behaviour-neutral seams (this doc) | Jun -> early Jul | Stergios & Alexandros |
| **B - Analytics** | first behaviour-ADDITIVE work: action-tracker, emission, scoring, localization keys, vitals foundation, portal data | Jul-01 -> ~Jul-15 (DevKit-side; Lovable legs by dated gates) | Stergios & Alexandros & Petros & Alex (+ Lovable + Web Portal) |
| **C - Integrate & Ship** | AR + VR labs updated on the new DevKit (+ UaaL, VR Shell, Addressables) with bug-work in parallel; store submissions IN by 2026-08-15 | ~Jul-15 -> Sep-10 | Alexandros & Stergios & Phoebos (+ Lovable, Cursor) |

**Phase A is the foundation, not the product.** It earns its place in a 13-week launch run for one reason: it makes
the behaviour-additive launch work provably safe to land on shipped university labs. Everything post-launch
(Phases D..I + the four §28 domain systems) lives in `_after-launch/2026-06-09-after-launch-plan.md`. Do not pull it
forward.

### (b) The behaviour-neutral boundary (non-negotiable)

The analytics RUNTIME does **not** get bolted into Phase A - that breaks the equivalence-proof discipline AND poisons
the migration net Phase C's JSON round-trip depends on. The ONLY "analytics foundation" admitted here:

| Allowed in Phase A (seams) | Where | Why neutral |
|---|---|---|
| Step-fact vocabulary consts | WS A2 step 5 (accelerator #6) | consts-only, **NO emission** |
| Four reserved module slots | WS A2 (Networking / Localization / Analytics / Vitals) | empty asmdefs + Hub tiles; emit nothing |
| `ISceneRunnerControl` seam | WS A8 | one tiny additive interface, no caller |
| The WS A3 net itself | WS A3 | tests behaviour; does not change it |

**Deferred to Phase B (NOT allowed here):** any emission; serialized analytics config; any ledger; the action-tracker;
scoring; localization keying; the typed Vitals component. See also the Traps list in §H.

### (c) Only WS A3 must finish before Phase B (the parallelism rule)

Phase B starts the moment the net exists; the rest of Phase A (WS A4/A5/A6/A7) proceeds **in parallel** - the
surfaces are disjoint (Phase B lives in its own reserved module; the splits touch `Scenario.cs`/`SceneManager`).
Serializing all of A before B will not fit the calendar.

### (d) Launch-critical core vs the slippable tail

Core = **WS A3 + WS A2 + the seams (WS A8, consts)**. The deep-split/hygiene tail (WS A4..A7) must NOT compete with
the store deadline - run it in the background or slip it. Slipping WS A6 costs cleanliness; slipping WS A3 or the
store gate costs the launch.

### (e) Why WS A3 exists - the AgentObservation CS0122 evidence

LooPi's AgentObservation EditMode tests were authored but **never compiled** ("NOT_RUN - requires Unity Editor"); a
CS0122 shipped silently and only surfaced when Petros opened Unity (since fixed via `InternalsVisibleTo`). That is
exactly the failure mode the "DevKit > Evaluate Changes" gate exists to catch. A net that is "authored but never run"
is not a net.

---

## §0 State of the package (the evidence this plan stands on)

- **God-classes dominate:** `SceneManager.cs` ~2,506 lines (two parallel `if (step is X)` ladders; every step type
  implemented twice - `RunXxx` + `RunXxxGroup`, ~1,000 duplicated, NOT bit-identical lines); `ScenarioGraphWindow.cs`
  ~4,962 lines / 6 types *(census freeze 2026-06-10, re-verified at review: now 5,878 lines / **10 named types** -
  the 2026-06-08 "6" undercounted (omitted `PendingNoteEdit` + `PortMeta`); commit `448301b` added `EditableNote` +
  `GroupBox`; all ride the WS A6 split)*; `ScenarioEditor.cs` ~1,480 lines / 11 drawers (now 1,492);
  `ContentDeliverySpawner.cs` ~1,180 lines.
- **The data model is disciplined where it matters:** `Scenario.cs` correctly uses `[SerializeReference]`,
  `[FormerlySerializedAs]`, `[Serializable]`, and the load-bearing `OnValidate` no-null-strip + `isCompiling` guard.
  **This contract must not be touched in Phase A.**
- **asmdef truth is dishonest:** the 12-assembly DAG is sound (Editor->Runtime only, no cycles), but
  `Pitech.XR.ContentDelivery` hard-references `Unity.Addressables`/`ResourceManager`/`TMP`/`ugui` while
  `package.json` declares **no** dependencies; the `PITECH_ADDR` guard's `#else` is dead; TMP/ugui are un-guarded;
  `Interactables.Editor.asmdef` has a copy-paste `rootNamespace`; `PITECH_CCD` is effectively dead.
- **Test coverage:** five EditMode files, ContentDelivery pure logic ONLY. Zero Scenario/Core/serialization coverage,
  no GUID-stability test, no public-API baseline. **The behaviour-neutral claim depends on tests that do not exist yet
  - which is why WS A3 lands first.**
- **Hygiene:** stray `--- SCENE MANAGERS ---.prefab` at package root; no README/CHANGELOG/LICENSE/.editorconfig/
  .gitattributes; encoding rot (a `0x85` byte in a `[MenuItem]` path, `U+FFFD` chars, Greek comments); `link.xml`
  `preserve="all"` on six assemblies (safe today, must not be narrowed without enumerating reflection-instantiated types).

**Top problems table (drives WS A5/A6 dispositions; BN = behaviour-neutral, eligible for Phase A):**

| Sev | Where | Issue | BN? | Phase |
|---|---|---|---|---|
| **Critical** | `Runtime/ContentDelivery/AddressablesRemoteUrlRewriter.cs:121,129-138` | `Install()` overwrites the GLOBAL `Addressables.ResourceManager.InternalIdTransformFunc` without saving it; `Uninstall()`/`Clear()` null it unconditionally. In UaaL this can break the host RN app. **Confirmed bug.** | No | **after Phase A** (save/restore + regression test) |
| High | `Runtime/Scenario/SceneManager.cs` (whole) | 2,506-line god-class; `RunXxx`/`RunXxxGroup` variants diverge (`RunQuestion` debounces; group variant first-click-wins) | No | **Phase D** (locked + golden-traced here, cracked there) |
| High | `Editor/Core.Editor/Services/{Quiz,Stats,Scenario}Service.cs` | resolves types by hard-coded `FullName` strings - invisible contract a namespace move silently breaks | constraint | **Phase A** pins in baseline; renames Phase D |
| High | `package.json` + ContentDelivery asmdefs | zero declared `dependencies` vs four hard-referenced packages | partly | **after Phase A** (deps block; metadata-only fields are A7) |
| High | `Runtime/ContentDelivery/...` | string-reflection dispatch + `FindObjectsOfType` in runtime code (IL2CPP-fragile, per-frame) | No | **Phase D** (`ISceneRunnerControl` swap) |
| High | `Tests/Editor/*` | no Scenario/serialization/GUID/API tests at all | Yes | **WS A3** (first) |
| Med | `ScenarioGraphWindow.cs` | `[SerializeReference]` list mutations guarded only by `Undo.RecordObject` | No | after Phase A |
| Med | `ScenarioGraphWindow.cs` `DuplicateStep` | *(census freeze 2026-06-10)* Duplicate of a `GroupStep` drops/corrupts nested children - `JsonUtility` does not support `[SerializeReference]`. Pre-existing, predates `448301b`. Verify in-editor: duplicate a populated GroupStep, observe result | No | after Phase A (fix = `EditorJsonUtility` or manual deep copy - behaviour change) |
| Med | `ScenarioEditor.cs:220-231` | ungated "Remove null entry" can permanently destroy a still-valid step | No | after Phase A (elevated) |
| Med | `AddressablesAdapterResolver.cs:23-33` | vendor `HealthOnAddressablesAdapter` inside the generic toolkit | No | Phase D (document now) |
| Med | `Editor/Core.Editor` layering | naive `AddressablesBuilderWindow` move = circular asmdef ref | No | Phase D (constraint recorded) |
| Med | `--- SCENE MANAGERS ---.prefab` (root) | stray content prefab, referenced by GUID | No | Phase D |
| Low-Med | hygiene (multiple) | mojibake, Greek comments, wrong `rootNamespace`, missing root docs | mostly Yes | **WS A4/A7** |

**Confirmed Phase-A-deletable dead code** (all verified zero-caller, private/internal, not serialized):
`SceneManager.EvalCompare` (1168-1182), `ScenarioEditorUtil.cs` (entire), empty `LaunchContextProviders.cs`,
`BuildDefaultPrefabAddressKey` (AddressablesService 811-814), `Styles.Primary`, the `"defaultNextGuid"` ternary
(ScenarioEditor 1041), the dead `DevkitWidgets` cluster (StatusChips/StatusBar/Kpi/Tile/StatusRibbon/StatusHeader/
ProgressBar/ProgressBarPro + `DevBlocksWindow.SmallButton`), the `RebuildLinksFromGraph` forwarder, the dead
`try/catch` in `StatsUIController.Init` (56).

---

## §1 Governing law - the equivalence proofs

Phase A moves declarations between files, deletes dead code, and reformats - **none of it changes the runner's
execution paths.** So the admission test is **static + additive (all EditMode)**, not a runtime replay.

| Proof | What it asserts | Mode |
|---|---|---|
| **Proof A - Scenario graph integrity** *(primary net)* | per lab fixture, the `[SerializeReference]` step graph is intact: (i) refs resolve - no nulled/Missing refs, no null step in any list; (ii) every routing guid (`nextGuid`, `correctNextGuid`/`wrongNextGuid`, `passedNextGuid`/`failedNextGuid` *(QuizResultsStep - added at census freeze 2026-06-10)*, `outcomes[].nextGuid`, `defaultNextGuid`, `specificStepGuid`, `multiConditionBranches[].nextGuid`, `childRequirements[].guid`) is `""` or points to an existing step guid, recursing into `GroupStep.steps`; (iii) every `UnityEvent` persistent listener keeps a live target + non-empty method | EditMode, read-only, real labs |
| **Proof B - Public-API additions-only** | reflected public surface over `Pitech.XR.*` may only GAIN members; **extended** to assert the Core.Editor `FullName` literals still resolve | EditMode, reflection |
| **Proof C - Serialized & GUID integrity** | every MonoScript `.meta` GUID unchanged + open->save serialized-diff zero (scene object AND prefab-instance-with-override) | EditMode, serialized-diff |

**Failing any one = behaviour change = deferred out of Phase A.**

> **Proof D (PlayMode golden trace) = deferred to Phase D; SEEDED only here** (one happy-path fixture proving the
> harness). Because Phase A never changes runtime logic, Proofs A-C are a complete net for it; the golden trace is the
> admission test for Phase D (runner unification). The deterministic driver already exists - the Scenario Graph's
> play-mode Branch/Skip/Outcome buttons call `EditorSkipFromGraph`; the recorder wraps that proven hook. Spec: Appendix I.4/I.5.

---

## Plan structure

Execution order: **WS A1 -> WS A2 -> WS A3 -> WS A4 -> WS A5 -> WS A6 -> WS A7 -> WS A8.**
A1 (census, no code) and A2 (editor-only; menus/Hub are not serialized into labs) deliberately run **before** the
net. **WS A3 is THE gate** - A5/A6 (the only WSs that touch lab data) never land before A3 is green; Phase B may not
start until A3 exists.

| WS | Focus | Gate / depends-on | Detail |
|---|---|---|---|
| **A1** | Inventory / disposition census | none - the map everything follows | Appendix A (77 surfaces) |
| **A2** | Editor surfaces + DevKit Hub cockpit + reserve 4 module slots | A1 (the census is the disposition map); safe before the net | this doc + spec §13/§28 |
| **A3** | EditMode safety net + `DevKit > Evaluate Changes` (**THE GATE**) | none upstream - this IS the gate | Appendix I (full ticket set) |
| **A4** | Formatting / encoding / comment-language normalization | runs through A3 as free insurance | this doc |
| **A5** | Provably-dead-code deletion | **A3 green** | this doc + §0 dead-code list |
| **A6** | Pure file splits + tiny utility extractions | **A3 green** (hard; needs the nested-GroupStep prefab-override fixture) | this doc + Appendix A(F) |
| **A7** | Docs / XML docs / package-root professionalization | A3 (additive; run through baseline) | this doc |
| **A8** | `ISceneRunnerControl` seam | after A3; isolated commit | this doc (exact contract) |

> **WS tags (Codex pass 2026-06-10):** **A1 / A2 / A3 / A8 = LAUNCH_BLOCKER** (the core Phase B/C draw against -
> A8 gates Phase B WS B3) · **A4 / A5 / A6 / A7 = CAN_TRAIL** (the quality tail; may trail or slip per addendum (d)).
> Reconciliation with the completion discipline: a CAN_TRAIL slip is **dispositioned in the Status & Progress Log**
> (what slipped, where it goes) - that satisfies "completes in full"; SILENT skipping never does.

---

## WS A1 - Inventory & disposition census *(run FIRST; no code change)*

**Goal:** enumerate every surface before touching any of them, so every later WS is mechanical and surprise-free.
**Already produced:** Appendix A - 77 surfaces, each with `file:line` + disposition (~40 keep / 18 rename / 13 split /
3 move / 2 delete / 1 defer), the menu-root-unification table, and the SceneManager anatomy. It exists because the
menu-root unification touches reflected `ExecuteMenuItem` callers, the splits move types while pinning `.meta`/GUIDs,
and the deletes must be proven caller-free - none of which is safe to start blind.

**Steps (progress tracking):**
- [x] Step 1: Confirm Appendix A is current vs the live source - spot-check GUIDs/line anchors (e.g. `SceneManager`
      MonoScript GUID `2d431a49d183e9c428369f7f758f75cd`); flag any surface added/removed since 2026-06-08.
      *(Done 2026-06-10: GUID pin EXACT; 30+ anchors verified exact; drift + new surfaces recorded in the census-freeze
      addenda (§0, Appendix A tables, (F) addendum, Appendix B) - headline: `448301b` grew the graph window to
      5,878 lines / 10 named types (original census undercounted at 6); `QuizStep`/`QuizResultsStep` were absent from
      the census; 5th Greek comment found at SceneManager:1533 (plain UTF-8, translate-only); `EditorSkipFromGraph`
      anchor was stale at authoring (1588 -> 1523). Re-verified by the 2026-06-10 adversarial review - 3 factual
      corrections applied, see the review log row.)*
- [x] Step 2: Freeze the census as the disposition reference WS A2..A7 execute against. *(Frozen 2026-06-10 - all
      corrections inlined as dated census-freeze notes; the tables now match live source.)*
- [x] Step 3: Mark the 3-5 labs that become the WS A3 fixture corpus (criteria in Appendix I.3). *(Done 2026-06-10 -
      5 labs marked + the not-in-any-real-lab gap list; see the I.3 addendum table.)*
- [x] Step 4: Cross-check delete/relocate calls against §3 resolved decisions (`ScenarioEditorUtil` = delete;
      `AddressablesBuilderWindow` relocation = Phase D, NOT here). *(Done 2026-06-10: `ScenarioEditorUtil` zero-caller
      verified (grep, package-wide); the rejected EnsureStableGuids alternative is NOT implemented anywhere; the dead
      `SceneManager.EvalCompare` (:1168) is distinct from the two live `ConditionsEvaluator.EvalCompare` call sites
      (:1042/:2226); `LaunchContextProviders.cs` = 266-byte empty placeholder; `AddressablesBuilderWindow` untouched.)*

**Acceptance:** every WS A2..A7 edit maps to a census row; fixture candidates identified. **MET 2026-06-10.**
**Gate:** none - this is the map.

---

## WS A2 - Editor surfaces & DevKit Hub cockpit + reserved module slots *(editor-only; safe before the net)*

**Goal:** one consistent surface taxonomy and a Hub that is the cockpit for everything; reserve the four future
feature modules by one repeatable recipe. Safe before the data net because menus/Hub are **not serialized into labs**
(components bind by script GUID) - proof is compile + "items still appear" + `ExecuteMenuItem` callers resolve.

**Naming (locked):** menu root token = **`Pi tech`** everywhere; home window = **"DevKit Hub"**.

**Steps (progress tracking):**
- [x] Step 1: **Unify the menu root.** *(DONE 2026-06-10 - 17 `[AddComponentMenu]` paths unified across 16 files; grep proved zero programmatic (`ExecuteMenuItem`/`Menu.*`) or serialized refs - `[AddComponentMenu]` is not serialized into labs, so behaviour-neutral by construction; the `DocsPage` `ExecuteMenuItem` callers target window `[MenuItem]` paths, untouched here. In-Unity "items still appear" check pending the WS A3 gate / next editor open.)* Drop the ` XR` from ALL `[AddComponentMenu]` paths (`Pi tech XR/<Module>/...`
      -> `Pi tech/<Module>/...`); keep the `GameObject/Pi tech/` + `Pi tech/` (top-bar + CreateAssetMenu) trees.
      **Each rename updates its `ExecuteMenuItem` callers in `DocsPage.cs` (5 calls) in the same commit**; leave
      Meta's `GameObject/Interaction SDK/...` alone (not ours). Full path-by-path map: Appendix A.
- [x] Step 2: **ORG-03:** *(DONE 2026-06-10 - the 3 types' Add-Component paths moved `Pi tech/Scenario/...` -> `Pi tech/Interactables/...`, folded into the Step 1 edits.)* move `SelectableTarget` / `SelectablesManager` / `MetaSelectRelay` Add-Component paths from
      the `Scenario` group -> the `Interactables` group.
- [x] Step 3: **Mojibake:** *(DONE 2026-06-10 - the single `0x85` byte removed by guarded byte-surgery (Edit/Write re-encode UTF-8 and can't drop one invalid byte); CRLF + the rest of the file byte-preserved (3896 -> 3895 bytes); grep confirmed no `ExecuteMenuItem` targets the old path.)* strip the `0x85` byte from the `SceneCategories` `[MenuItem]` path (grep-verify no
      programmatic reference first - the editor-visible-string rule in WS A6).
- [ ] Step 4: **Rebuild the Hub as the cockpit.** Brand "DevKit Hub"; task-first pages **Setup / Author / Deliver /
      Maintain / Reference**; every workspace window (Scenario Graph, Dev Blocks, Addressables Builder, Scene
      Categories) gets a **launch tile** (the Hub launches them, never re-implements); surface the repair tools +
      `Evaluate Changes` in *Maintain*; add an **"Add Scenario to Scene"** command (home: `ScenarioService`).
- [ ] Step 5: **Surface-type discipline:** verb for commands ("Add Scenario to Scene"), noun for workspaces
      ("Scenario Graph"), "... Wizard" for wizards; Add Component / Create Asset are the fallback tier.
- [ ] Step 6: **Graph readability (derived/visual only - stores nothing):** derived node labels (computed from `Kind`
      + key params, not stored); render the `label` fields branches already carry (`ConditionOutcome.label`,
      `MiniQuizOutcome.label`, choice text); structural coloring by `Kind`/`GroupStep` membership. *(Saved "section
      shapes" + custom branch names = serialized fields = first item AFTER Phase A, not here.)*
- [~] Step 7: **Reserve the four module slots** by the one recipe - own asmdef; optional-package deps gated via *(PARTIAL 2026-06-10 - the 4 asmdefs DONE: `Pitech.XR.{Networking,Localization,Analytics,Vitals}` under `Runtime/<Module>/`, each referencing `Pitech.XR.Core` by name (correct DAG direction, mirrors `AgentSubstrate`), each with a documented empty-namespace placeholder (zero public types -> Proof B trivially additions-only); Networking gated `PITECH_HAS_FUSION`, Localization `PITECH_HAS_LOCALIZATION`, Analytics/Vitals additive. `.meta` auto-generated by Unity on import. REMAINING: the per-module Hub tiles, which fold into Step 4's Hub cockpit rebuild.)*
      `versionDefine` + `#if` (never hard deps); a Hub tile/page under the right task group; references Core/Scenario,
      never the reverse; **logic lands post-reservation**:

| Reserved module | asmdef + Hub tile | Gating | Logic lands | Spec § |
|---|---|---|---|---|
| **Networking** (Make-Multiplayer) | `Pitech.XR.Networking` + Setup tile | `PITECH_HAS_FUSION` versionDefine + `#if` | after-launch (NetworkedStates -> `IScenarioFlowStore`) | §28.2 |
| **Localization** | `Pitech.XR.Localization` + Author tile | `PITECH_HAS_LOCALIZATION` (com.unity.localization) | Phase B WS B7 (keyed Greek+English build-baked); cloud pipeline after-launch | §28.3 |
| **Analytics** | `Pitech.XR.Analytics` + Deliver tile | additive | Phase B WS B1-B6 (the destination) | §28.5; Phase B doc |
| **Vitals** | `Pitech.XR.Vitals` + Author tile | additive | Phase B WS B8 (typed foundation); digital twin after-launch | §28.4 |

- [x] Step 8: Add the **step-fact vocabulary consts** (accelerator §H#6) - `const`s only (`step.completed` etc. + *(DONE 2026-06-10 - `Runtime/Core/ScenarioFactKeys.cs` (`Pitech.XR.Core`, the leaf all consumers reference): `step.completed`; the `scenario.step.<guid>.{done,outcome,completedBy,completedAtTick}` fact-key family + pure key builders; the WS B9 bridge prefix `flow.step.` (single source of truth, prevents drift). Consts + pure string builders ONLY - NO emission, NO `ScenarioStepFact`/flow-store TYPE (those stay the deferred trap). FROZEN-contract header mirrors `AgentObservationEnums`.)*
      the `scenario.step.<guid>.done` key format), **NO emission**.

**Acceptance:** one root token across all four menu systems; every workspace reachable from the Hub in <=2 clicks; all
`ExecuteMenuItem` callers resolve; compiles; mojibake gone; four reserved asmdefs + tiles exist, reference
Core/Scenario only, contain no runtime behaviour. (No lab data touched - confirm with a graph-integrity run once WS A3
lands.)
**Gate:** WS A1 (the census is the disposition map). Does NOT depend on WS A3.

---

## WS A3 - The EditMode safety net + `Evaluate Changes` *(THE GATE)*

**Goal:** make "behaviour-neutral" measurable. Itself behaviour-neutral (purely additive); the precondition for every
later phase. **Full ticket set with test specs + asmdef JSON: Appendix I.**

**Steps (progress tracking):**
- [ ] Step 1: Create `Pitech.XR.Scenario.Editor.Tests` (EditMode) referencing Scenario+Core+Quiz+Interactables+Stats,
      in the **modern** asmdef form (`UNITY_INCLUDE_TESTS` + `nunit.framework.dll` precompiled + `overrideReferences:true`
      + TestRunner refs - exact JSON in Appendix I.1). Do NOT copy the deprecated `optionalUnityReferences` template;
      migrate the existing ContentDelivery test asmdef to the modern form too.
- [ ] Step 2: EditMode-lock the pure logic on UNMODIFIED code: `ConditionsEvaluator.EvalCompare` (all 8 `CompareOp`
      incl. `Mathf.Approximately` + bool encodings); `GroupStep.IsChildRequired*`/`Ensure*`.
- [ ] Step 3: **Proof A test** - scenario graph integrity per Appendix I.0 (invariants + per-lab snapshot JSON).
- [ ] Step 4: **Proof C test** - GUID-stability (`ScriptGuids.json`, Appendix I.7) + open->save serialized-diff per
      fixture, scene object AND prefab-instance-with-override (method: Appendix I.6).
- [ ] Step 5: **Proof B test** - public-API baseline (additions-only) + Core.Editor `FullName` literal resolution
      (Appendix I.8).
- [ ] Step 6: ContentDelivery additive tests: `RewriteUrl`/`TryParseCcdUrl`, `LaunchContextValidation`,
      `PublishTransactionStateMachine.CanTransition`, `PublishReportService` JSON-golden.
- [ ] Step 7: **`Export Lab as Test Fixture` tool** (menu + Hub Maintain + GameObject context; saves the Scenario
      subtree as a self-contained prefab into `Tests/Fixtures/Scenarios/` carrying its `.meta`; captures the
      graph-integrity snapshot baseline). Extract the **3-5 real-lab fixtures** chosen in WS A1 (corpus: Appendix I.3).
- [ ] Step 8: **`DevKit > Evaluate Changes`** - the one-click manual gate (menu item + Hub button; runs the EditMode
      suite via `TestRunnerApi`; plain-language verdict; shared `DevKitChecks.RunEditModeGate()` core + headless
      `RunAll()` so a pre-push hook / Phase D CI attach later unchanged - spec: Appendix I.9-I.11).
- [ ] Step 9: **SEED Proof D** - create `Pitech.XR.Scenario.PlayMode.Tests` + the `GoldenTraceRecorder` and prove it
      on ONE happy-path fixture (schema + harness: Appendix I.4/I.5). NOT a Phase A gate; corpus + CI = Phase D.
- [ ] Step 10: Confirm Proofs A/B/C GREEN on unmodified code; log the green run in the Status & Progress Log.

**Where the tests run:** the package's tests + fixture corpus live INSIDE the package; the DevKit Unity project
(Unity 6+, package embedded) is the iteration gate; after a package bump you also run `Evaluate Changes` in the
HealthOn projects against real scenes = the integration check. One suite, two run-locations. The package gate never
*depends* on a consumer project.

**Acceptance:** Proofs A/B/C runnable + green on unmodified code (Proof C as scene object AND
prefab-instance-with-override); `Evaluate Changes` one-click verdict works; Export-Lab-as-Fixture ships; 3-5 fixtures
committed; golden-trace harness passes on its one seed fixture.
**Gate:** none upstream - this IS the gate A5/A6 + Phase B depend on.

---

## WS A4 - Formatting / encoding / comment-language normalization *(separate commits)*

**Goal:** remove encoding rot and language inconsistency. **Physically separate commits** so a real diff is never
hidden behind whitespace churn.

**Steps (progress tracking):**
- [ ] Step 1: Translate the **five** Greek comments in `SceneManager.cs` (1367/1429/1439/**1533**/2478 - census freeze
      2026-06-10, corrected at review: all five are plain valid UTF-8 Greek, translate-only, NO encoding repair needed;
      the real mojibake remains the `U+FFFD` pair in `DevkitWidgets.cs`, Step 3); normalize the
      `#else` input-branch indentation (token stream unchanged).
- [ ] Step 2: Translate Greek comments + the Greek `[Tooltip]` in `SelectionLists.cs`; Greek comments in
      `SceneManagerEditor.cs`; *(census freeze 2026-06-10)* the four Greek comments in `ScenarioGraphWindow.cs`
      (2129/3345/3351/3799 - same comment-translation nature, behaviour-neutral). *(Tooltips are not serialized into
      assets - neutral. Greek HELP-BOX strings in `SelectionListsEditor` are user-visible -> after Phase A, excluded here.)*
- [ ] Step 3: Fix the two `U+FFFD` mojibake comments in `DevkitWidgets.cs`; re-indent the broken object-initializer braces.
- [ ] Step 4: Reformat all 12 asmdef files to one style (4-space) + consistent field set.
- [ ] Step 5: Fix `AddressablesBuilderWindow` `OnEnable` brace/indentation.
- [ ] Step 6: Run the WS A3 net after each pass (free insurance - touches no compiled logic).

**Acceptance:** only comment/whitespace bytes change; Proofs A/C trivially green.
**Gate:** runs through WS A3.

---

## WS A5 - Dead-code & dead-artifact removal

**Goal:** delete provably-dead code. Every deletion verified zero-caller, private/internal, references no serialized type.

**Steps (progress tracking):**
- [ ] Step 1: Delete `SceneManager.EvalCompare` (1168-1182) - zero callers; both live sites use
      `ConditionsEvaluator.EvalCompare`.
- [ ] Step 2: Delete the unreachable `"defaultNextGuid"` ternary (`ScenarioEditor.cs:1050` - was :1041 pre-`448301b`) + the unused `Styles.Primary`.
- [ ] Step 3: Delete empty `LaunchContextProviders.cs` + `.meta`.
- [ ] Step 4: Delete the dead `DevkitWidgets` cluster (StatusChips/StatusBar/StatusRibbon/StatusHeader/ProgressBar/
      ProgressBarPro/Kpi/Tile + `DevBlocksWindow.SmallButton`) after a zero-reference sweep; remove duplicate comment
      banners + the dead `RebuildLinksFromGraph` forwarder. **Live API excluded:** `Actions` (22 sites), `Card` (21),
      `Pill` (23), `PillsRow`, `StatusChip`, `TileGrid`, `CardGridTwoCol`.
- [ ] Step 5: Delete `BuildDefaultPrefabAddressKey`; inline `ComputeAddressKey` at its two private call sites; remove
      the orphan duplicated `<summary>` above `BuildLocalLabVersionRoot`.
- [ ] Step 6: Replace the dead `try/catch` in `StatsUIController.Init` (~56) with a direct indexer read
      (`StatsRuntime` indexer is provably non-throwing).
- [ ] Step 7: **Delete `ScenarioEditorUtil.cs`** + its `.meta` (RESOLVED §3; the "wire Load to EnsureStableGuids"
      alternative is REJECTED - do not implement it).
- [ ] Step 8: Run the WS A3 net after each deletion - a deletion is only "safe" if the net stays green.

**Acceptance:** Proof B additions-only (internal/private removals), Proof C zero, Proof A unchanged.
**Gate:** **WS A3 green.**

---

## WS A6 - Pure file splits & tiny utility extractions *(each `.cs` carries its `.meta`)*

**Goal:** the structural heart of Phase A - real god-class decomposition that is still provably a *move*. Most
exposed to the GUID-regen risk (§3); hence the hard dependency on the net.

**Steps (progress tracking):**
- [ ] Step 1: **Data-model split (flagship, highest risk).** Move each Step subclass + `ConditionsEvaluator` out of
      `Scenario.cs` into `Runtime/Scenario/Steps/<Type>.cs` (per-type map: Appendix A(F)). **Namespace
      `Pitech.XR.Scenario` unchanged, same asmdef, each file carries its `.meta`.** The `Scenario` MonoBehaviour +
      `OnValidate` guard stay untouched. The file retaining `Scenario` KEEPS `Scenario.cs.meta`'s GUID (split rule:
      Appendix I.7). No `[MovedFrom]`.
- [ ] Step 2: **Graph window split + namespace wrap.** Wrap `ScenarioGraphWindow` + nested types + `StepEditWindow` in
      `namespace Pitech.XR.Scenario.Editor`; one file per type; lift pure helpers (`GroupSummary`,
      `GetGroupPreferredWidth`, `OutcomeLabel`, AutoLayout BFS) into an `internal` static class; demote
      no-external-caller `public static` helpers after grep. *(Verified safe: `ScenarioService.OpenGraph` resolves the
      window by simple `t.Name` - namespace-independent.)*
- [ ] Step 3: **Inspector split.** `ScenarioEditor.cs` -> one file per `PropertyDrawer` + `Styles.cs`, same
      assembly/namespace, each `.meta` carried. **Carry the `using Runtime = Pitech.XR.Scenario;` alias into each
      split file** or fully-qualify, else compile break.
- [ ] Step 4: **ContentDelivery extractions.** Extract the byte-identical `TrySetAutoStart`/`TryRestart` into one
      `internal static` helper. **Exclude the only-near-identical `Find*SceneManager*` helpers from the verbatim
      move** - unify them separately as a small behaviour-equivalent change proven by Proof A. Move `Timestamp` to its
      own file. Split public interfaces/enums (`IContentDeliveryService`, `ILaunchContextProvider`,
      `IContentDeliveryMetadataProvider`, `ContentSourceMode`) into own files (none are `[SerializeReference]`).
- [ ] Step 5: **Features.** Rename non-serialized private Stats fields to `_camelCase`; split `StatsConfig.cs` into
      `StatsConfig.cs`/`StatEffect.cs`/`StatsRuntime.cs` **only after confirming same namespace+assembly** (if any
      `[SerializeReference]` usage is found on `StatEffect`, demote to after Phase A). Normalize the three
      `"Pi tech XR/Scenario/..."` paths on Interactables types (ORG-03). **Excluded:** promoting nested
      `MetaSelectRelay` to its own file (type-name change -> needs `[MovedFrom]` -> after Phase A).
- [ ] Step 6: **Reflection -> typed access in `SceneManagerEditor`** (`gm.scenario`/`gm.StepIndex`/`gm.Restart()` -
      public, assembly already referenced; same values read). *(Distinct from the RUNTIME reflection in
      ContentDelivery, which is Phase D.)*
- [ ] Step 7: **Editor metadata fixes.** `Interactables.Editor.asmdef` `rootNamespace` ->
      `Pitech.XR.Interactables.Editor`; namespace `SelectablesManagerEditor` (+ `: UnityEditor.Editor`); `#if
      UNITY_EDITOR` + namespace fix on `QuizDefaultUIPrefabFactory` (`Pitech.XR.Editor.Quiz` -> `Pitech.XR.Quiz.Editor`).
- [ ] Step 8: Run the WS A3 net after each split; Proof C must stay zero as scene object AND prefab instance.

> **Editor-visible-string rule:** `AddComponentMenu`/`[MenuItem]` path changes are permitted as deliberate,
> single-line, documented edits - NOT bundled into a "pure move" commit - provided a grep first proves the exact path
> is not referenced programmatically (`ExecuteMenuItem`, `Menu.*`, settings, shortcuts, automation).

**Acceptance:** every moved `.cs` carries its `.meta`; GUID-stability green; Proof C zero on all fixtures both ways;
Proof B additions-only; no `[MovedFrom]` anywhere.
**Gate:** **WS A3 green** (hard - Step 1 requires the nested-`GroupStep` prefab-override fixture). May slip per
addendum (d) if it threatens the store deadline.

---

## WS A7 - Documentation, XML docs & package-root professionalization *(additive; zero compiled impact)*

**Steps (progress tracking):**
- [ ] Step 1: XML `///` docs on the API-baseline members (`SceneManager` public fields, `StepIndex`, `Restart`,
      `EditorSkipFromGraph`, selection bridges, `GetOrCreateQuizSession`) + `XRServices`.
- [ ] Step 2: `Debug.LogException(e)` inside the bare `catch {}` blocks in the graph window - **permitted only as a
      diagnostic-output-only change when no test asserts console silence on that path** (console output IS observable
      behaviour); verify after WS A3, else defer.
- [ ] Step 3: Add `README.md`, `CHANGELOG.md`, `LICENSE.md`, `.editorconfig` (encodes the 4-space style),
      `.gitattributes` (LF; `.meta`/`.prefab`/`.asset` as text with explicit eol). Initial LF renormalization is its
      own isolated commit.
- [ ] Step 4: **Metadata-only** `package.json` fields: `license`/`licensesUrl`, `documentationUrl`, `changelogUrl`,
      `unityRelease` floor, `keywords`. **NO `dependencies` block, NO version bump** (the deps block + Unity 6000.0
      floor-bump are ONE Phase D (post-launch) cutover - spec §28.6). Record that `Unity.ResourceManager` is correctly kept.
- [ ] Step 5: Subsystem-notes doc recording the intentional serialization exceptions: the public serialized-field
      surface, the `OnValidate` no-null-strip + `isCompiling` guard, editor-only `FindObjectsOfType` legitimacy, and
      the `link.xml` `preserve="all"` constraint (must not be narrowed without enumerating every
      reflection-instantiated/`[SerializeReference]` type).
- [ ] Step 6: Generate the **dependency-truth REPORT**: per `PITECH_*` define - asmdef hard-ref vs `package.json` vs
      actual `#if`/un-guarded usage. *(The real with/without-Addressables compile matrix moves to the Phase D (post-launch)
      cutover.)*

**Acceptance:** no `.cs` token outside comments changes; API baseline additions-only.
**Gate:** WS A3 (additive; run through the baseline).

---

## WS A8 - `ISceneRunnerControl` seam *(optional; isolated; AFTER WS A3; not mixed into the proof work)*

**Goal:** give ContentDelivery (and later LabConsole) a typed handle to the runner so Phase D can drop string
reflection. One small, separately-reviewed, additive commit once the WS A3 baseline is green.

**Exact contract** (new file `Runtime/Core/ISceneRunnerControl.cs`, assembly `Pitech.XR.Core`):
```csharp
namespace Pitech.XR.Core
{
    /// <summary>Minimal, stable control surface over a scenario runner. Behaviour-neutral seam:
    /// implementers forward to existing members; no new behaviour.</summary>
    public interface ISceneRunnerControl
    {
        int  CurrentStepIndex { get; }       // forwards SceneManager.StepIndex
        bool AutoStart        { get; set; }  // forwards SceneManager.autoStart
        void Restart();                      // forwards SceneManager.Restart()
    }
}
```

**Steps (progress tracking):**
- [ ] Step 1: Add the interface exactly as above; `SceneManager` adds `: ISceneRunnerControl` + three forwarding
      members. No field renamed, nothing made non-public, behaviour identical.
- [ ] Step 2: **Keep it exactly these three members** - do NOT widen toward flow-store/ledger concepts (premature
      lock-in; that is after Phase A).
- [ ] Step 3: Run the WS A3 net - Proof B additions-only; Proof C unchanged (nothing serialized).

**Acceptance:** interface lands additively; proofs green; `SceneManager` untouched as a runner.
**Gate:** after WS A3; its own isolated ticket.

> **Foresight note - Vicky Director (Petros, 2026-06-10).** This seam is the first rung of the ladder that ends at
> **VickyMode.Director** - Vicky *driving* a scenario (advance/branch/pause) in 2027. Do NOT widen the interface now
> (Step 2 stands), but name the trajectory in the XML doc: Phase D extracts the runner behind this seam; Phase E adds
> `IScenarioFlowStore` beneath it; Phase H defines the gated flow-control action vocabulary (`advance_step`,
> `branch_to`, `pause_scenario`) that routes **through LabConsole onto this seam**. Design choices here should never
> assume the caller is only ContentDelivery.

---

## Explicitly deferred (with reason + compatibility note)

| Item | Phase | Why not Phase A | Compatibility note |
|---|---|---|---|
| `AddressablesRemoteUrlRewriter` save/restore of global transform | after Phase A | confirmed bug, but fixing it changes observable behaviour | capture prior func on install, chain, restore (not null) on uninstall; regression test; route via bridge/host owner |
| `package.json` `dependencies` block + versionDefines fix + Unity 6000.0 floor-bump | Phase D, post-launch (ONE cutover) | changes UPM resolution in consumers | TMP/ugui hard; Addressables required (§3); with/without-`PITECH_*` compile test; spec §28.6 |
| `RunXxx`/`RunXxxGroup` unification -> `IStepRunner` registry | Phase D | variants not bit-identical; fails Proof A by construction | proven byte-equal against the completed golden trace first |
| Editor undo-correctness (`RegisterCompleteObjectUndo`; `SerializedObject` routing; gate the ungated null-delete; *(added 2026-06-10)* `Undo.undoRedoPerformed` -> graph reload hook - undo of a node resize leaves stale visuals until Refresh) | after Phase A | changes undo-stack / prefab-override behaviour | the ungated null-delete is ELEVATED - can lose shipped-lab data |
| 7-way route-schema table; shared per-step drawers; `JsonUtility` deep-copy fidelity | after Phase A | variants diverge today | lock with fixture round-trips (Proof C) before merging |
| Runtime reflection/`Find` removal (Interactables + ContentDelivery) | Phase D | changes discovery/timing/caching | via `ISceneRunnerControl`; cache VR/Meta determination |
| Core.Editor layering inversion + `AddressablesBuilderWindow` relocation | Phase D | naive move = circular asmdef ref | first extract editor-UI primitives to a lower assembly |
| HealthOn adapter de-coupling | Phase D | changes resolution for labs on the implicit fallback | gate behind a config migration setting `adapterTypeName` |
| Stray root prefab relocation | Phase D | referenced by GUID `a0032abe...` | carry `.meta`; prove Proof C |
| `link.xml` narrowing | Phase D | size optimization, safe today | enumerate every reflection-instantiated Step type first |
| Saved graph "section shapes" + custom branch names | first item after Phase A | serialized fields -> fails Proof C | editor-only serialized field via the `Scenario.GraphNote` `#if UNITY_EDITOR` pattern. *(2026-06-10: "section shapes" LANDED early - `448301b`'s `GraphGroup` boxes, already in the compliant pattern; saved per-node display name + manual node size (a 448301b feature this row never listed) also landed, reconciled into the `StepGraphDisplay` side-table. REMAINING deferred scope: custom names on label-less branch EDGES only - node header names are NOT that feature.)* |

---

## §3 Resolved decisions + top risks

**Resolved (Stergios 2026-06-08 / Petros 2026-06-09):**
- **No version bump in Phase A.**
- **Unity 6+ baseline (Petros 2026-06-09):** project/tests/builds target Unity 6+ (6000.0); the `unity`-field
  floor-bump + `dependencies` block = ONE Phase D (post-launch) metadata cutover (spec §28.6). The spec §17 "P1 bumps Unity"
  describes the superseded OLD plan. **HealthOn AR and HealthOn VR are BOTH already on Unity 6 (Petros, 2026-06-10);
  Vuforia is fine** - no consumer engine upgrade is a prerequisite. The AR-side launch work (DevKit update, scenario
  updates, UaaL Android + iOS builds, RN embed, AR Addressables, store submission) is owned by the AR surface, not
  DevKit (spec §28.6).
- **4-space + LF** via `.gitattributes`/`.editorconfig`; enforced by the manual `Evaluate Changes` gate (no server CI
  in Phase A; server CI arrives Phase D).
- **Addressables is a REQUIRED dependency** - declared at the Phase D (post-launch) cutover; Phase A documents the posture.
- **Delete `ScenarioEditorUtil`** (WS A5).
- **`AddressablesBuilderWindow` relocation + HealthOn adapter decouple = Phase D**; `Pitech.XR.Core.Editor.UI` leaf
  extraction approved for Phase D; Phase A records the constraints.
- **NetworkedStates boundary:** document-only in Phase A; never introduce a second scene-wide state manager.

**Top risks:**

| Risk | Why it bites | Mitigation |
|---|---|---|
| Regenerated `.meta` GUID during a split nulls every shipped lab's step graph | `[SerializeReference]` resolves by GUID; new GUID = silent dangling routing | Proof C + the WS A6 "each file carries its `.meta`" rule + the I.7 split rule |
| Core.Editor hard-coded `FullName` strings - invisible contract | string literal, not a typed using; compiler will not catch it | Proof B extended to assert the literals resolve |
| `AddressablesRemoteUrlRewriter` global-transform bug (behaviour-CHANGING) | fixing it violates the Phase A boundary | OUT of Phase A; first fix after |

---

## §4 Exit checklist

**Census & editor restructure (A1, A2):**
- [ ] Census confirmed current; every WS A2..A7 edit maps to a census row.
- [ ] Single menu root `Pi tech` across all four menu systems; ORG-03 applied; `0x85` mojibake stripped; all
      `DocsPage` `ExecuteMenuItem` callers resolve; package compiles; no lab data touched.
- [ ] DevKit Hub rebuilt as the cockpit (task-first pages; launch tiles; repair tools + `Evaluate Changes` surfaced;
      "Add Scenario to Scene" added).
- [ ] Four reserved module slots (Networking / Localization / Analytics / Vitals) stubbed by the one recipe -
      structure only, no logic.

**Safety net (A3) - all EditMode:**
- [ ] `Pitech.XR.Scenario.Editor.Tests` exists in the modern asmdef form; discoverable in Test Runner.
- [ ] `Export Lab as Test Fixture` ships; 3-5 real labs extracted into `Tests/Fixtures/`; net green against them.
- [ ] Proof A green (refs/routes/events; per-lab snapshots committed). Pure-logic tests green (`EvalCompare` all 8
      ops; `GroupStep.Ensure*`/`IsChildRequired*`).
- [ ] Proof C green per fixture (GUID-stability + serialized-diff, scene object AND prefab-instance-with-override).
- [ ] Proof B green + the Core.Editor `FullName` literals all resolve. ContentDelivery additive tests green.
- [ ] `DevKit > Evaluate Changes` ships (menu + Hub button), one-click verdict; headless entry exists so a pre-push
      hook / Phase D CI attach unchanged.
- [ ] `.editorconfig` + `.gitattributes` committed; dependency-truth REPORT generated.
- [ ] *(Phase D-prep, not a gate)* golden-trace harness passes on ONE seed fixture.

**Reorganization (proven neutral):**
- [ ] WS A4 landed as separate commits; only comment/whitespace bytes changed.
- [ ] WS A5 deletions all zero-caller; Proofs A/B/C green; `ScenarioEditorUtil.cs` deleted with its `.meta`.
- [ ] WS A6 splits landed; every moved `.cs` carries its `.meta`; Proof C zero both ways; no `[MovedFrom]`.
- [ ] `SceneManagerEditor` reflection -> typed access; behaviour identical. `rootNamespace`/namespace fixes applied.

**Docs / metadata (A7):**
- [ ] XML docs on baseline members; README/CHANGELOG/LICENSE at package root; metadata-only `package.json` fields;
      subsystem-notes doc records the serialization exceptions + `link.xml` constraint.

**Negative gates (must remain TRUE):**
- [ ] `OnValidate` no-null-strip + `isCompiling` guard untouched.
- [ ] No runner unification, no dispatch-registry change, no undo-routing change, no runtime reflection/`Find`
      removal, no rewriter fix, no `dependencies` resolution change, no version bump.
- [ ] No serialized public field renamed/retyped; no `[SerializeReference]` type moved namespace/assembly.
- [ ] No emission anywhere - only the consts vocabulary + reserved slots exist.

---

## §H Accelerators (behaviour-neutral; each must still pass all three proofs)

1. **One gate, two doors** - `Evaluate Changes` button + headless entry over the SAME suite (highest leverage; Phase D
   CI is wiring, not rework).
2. **Over-build the golden-trace fixture corpus** as the Phase D acceptance suite (one per step Kind, all 6 GroupStep
   modes, the divergent debounce/first-click + Selection `allowedWrong` paths). Additive insurance.
3. **Characterization tests on the reflection/`Find` paths Phase D will delete** - pin current observable behaviour.
4. **Write the "Phase D extraction playbook"** while knowledge is fresh (`_groupExitBranchResolved` routing, silent
   type-switch fallthrough, `GroupStep` branchGuid contract).
5. **`ISceneRunnerControl`** = WS A8 (isolated; never woven into the harness).
6. **Step-fact vocabulary consts** = WS A2 step 8 (consts only; the ONE pre-baked contract exception).
7. **Read-only localization-candidate report** (reports TMP strings; no mutation) - feeds Phase B WS B7.

**Traps - do NOT add "to prepare":** pre-baked `IScenarioFlowStore`/`LabEventLedger`/analytics-envelope public types
(redesigned later; premature lock-in); editor-only serialized display fields (fails Proof C); emitting any
events/facts/lifecycle hooks (behaviour).

---

## Status & Progress Log

> Update on EVERY WS start/close + every Evaluate-Changes green run on a milestone. Newest first. This is the
> at-a-glance progress view; the per-WS checkboxes are the detail.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-10 | A2 | **WS A2 IN PROGRESS - mechanical/structural core landed** (under the unity-csharp skill). DONE + committable: Step 1 (17 `[AddComponentMenu]` paths `Pi tech XR/` -> `Pi tech/` across 16 files), Step 2 (ORG-03 - 3 Interactables types moved out of the Scenario menu group), Step 3 (the `0x85` mojibake byte stripped from `SceneCategoriesWindow` via guarded byte-surgery, CRLF preserved), Step 7 **asmdefs** (4 reserved module slots `Pitech.XR.{Networking,Localization,Analytics,Vitals}` + gated versionDefines + empty placeholders), Step 8 (`ScenarioFactKeys` consts in `Pitech.XR.Core` - the frozen step-fact vocabulary, no emission/types). All behaviour-neutral and grep/diff-verified; `[AddComponentMenu]` is not serialized so labs are untouched by construction. **REMAINING (UI-visual; needs in-Unity iteration + one cross-dep): Step 4** (Hub cockpit rebuild: task-first pages + launch tiles + reserved-module tiles + "Add Scenario to Scene" surfacing + **surface "Evaluate Changes" which does not exist until WS A3** - that tile must be stubbed-then-wired), **Step 5** (surface-type naming - menu half done via 1/2, rest folds into the Hub), **Step 6** (graph readability - derived labels/colours, editor-visual). These are deferred to a focused Hub pass with editor verification rather than authored blind (no Unity from this environment - per the skill's verification rule). NOT a silent skip: dispositioned here per the completion-discipline rule. | unity-csharp impl (Claude) |
| 2026-06-10 | A1 | **WS A1 re-reviewed** (unity-csharp skill, 29-agent adversarial workflow; every finding double-verified). 3 factual census corrections applied: (1) SceneManager:1533 is plain UTF-8 Greek, NOT mojibake (the "mojibake" came from a console decoding artifact during the survey - WS A4 Step 1 + the A1 row corrected); (2) graph window = **10 named types**, not 8 (original census's 6 omitted `PendingNoteEdit`+`PortMeta`) - §0 + (A) row corrected so WS A6 plans the full split set; (3) `QuizResultsStep` routes via `passedNextGuid`/`failedNextGuid` - (F) addendum + §1/I.0 Proof A enumerations corrected, with the "derive routing fields generically by `*NextGuid` suffix" requirement added. I.3 hardened: the synthetic fixture is now MANDATORY at A3 Step 7 (no silent demotion of the ratified SpecificChild shape); prefab sweep added (199 VR prefabs: zero step content - labs ship as Addressable SCENES; package stray prefab = only serialized ConditionsStep anywhere); recovery-copy caveat added. Code follow-ups landed into the staged fix: inspector-side `RemoveStepAt` now prunes side-table entries via new `Scenario.RemoveStepGraphDisplayRecursive` (the graph window uses it too); `CollectStepGuids` re-indented to 4-space. New pre-existing defect logged (§0 table): GroupStep duplicate drops nested children (`JsonUtility` vs `[SerializeReference]`) - after Phase A. Deferred-table rows annotated (section-shapes half landed; `undoRedoPerformed` hook added to the undo-hardening row). One deliberate delta vs `448301b` recorded: resize/auto-size undo now records BEFORE mutation (448301b recorded after - inert undo); editor-only, hours-old feature. | unity-csharp review (Claude) |
| 2026-06-10 | A1 | **WS A1 CLOSED** (started + closed same day). Census spot-checked vs live source: SceneManager GUID pin + 20+ anchors EXACT; drift recorded inline as dated census-freeze notes (graph window 5,878 lines / 10 named types post-`448301b` (count corrected at review); MenuItem :148; StepEditWindow :5203; ternary :1050; spawner `Restart` :1173; window `StepIndex` read :1916; SceneManager `EditorSkipFromGraph` :1523 - census anchor was stale at authoring, file unchanged since 2026-04-09). New surfaces flagged: `QuizStep`/`QuizResultsStep` (own files, keep - (F) addendum), `EditableNote`/`GroupBox` (ride the A6 split), `GraphGroup`/`StepGraphDisplay` (Scenario editor-only nested). 5th Greek comment found (SceneManager:1533; plain UTF-8 - the initial "mojibake" label was a console artifact, corrected at review) + 4 untracked graph-window Greek comments joined WS A4. Fixture corpus MARKED (I.3 addendum): Pharmacy / Delirium / Loimokseis / Loimokseis_Old_1 / Delirium Stats Test; NOT-in-any-real-lab gaps listed (ConditionsStep, SpecificChild/`specificStepGuid`, `allowedWrong>0`, `defaultNextGuid`) - synthesize at A3 Step 7; AR labs not on this machine (re-survey when accessible). §3 cross-check clean. | Claude (Stergios dispatch) |
| 2026-06-10 | - | **Phase A DISPATCHED** (Stergios sign-off). Pre-dispatch reconciliation of commit `448301b` ("minor changes, scenariograph", Alexandros): its two runtime-serialized `Step` fields (`graphSize`/`displayName`) hit the §H trap "editor-only serialized display fields (fails Proof C)" + the deferred-table row ("first item after Phase A"); converted to the prescribed editor-only side-table (`Scenario.StepGraphDisplay`, guid-keyed, `#if UNITY_EDITOR` - the `GraphNote` pattern) BEFORE any Proof C fixture exists, so baselines never absorb the violation. Features behave the same (resize / custom header name / duplicate carries entries onto fresh guids / deletes prune them - graph window + inspector) with ONE deliberate delta: resize/auto-size undo now records BEFORE mutation (`448301b` recorded after, so its undo was inert) - editor-only, hours-old feature. The commit's group boxes + note tethers already complied. Heads-up: `graphSize`/`displayName` values saved between 448301b (14:41) and the fix drop silently on next load (feature was hours old). `Step` is back to `guid`+`graphPos` exactly as pre-448301b. | Stergios dispatch (Claude) |
| 2026-06-10 | A3 | Stergios review: restored 3 refinements lost in the merge - I.0 Proof A regains the generic ObjectReference walk (Missing-ref, no per-type list) + object-reference map + rich event fingerprint (stable identity/`m_CallState`/`m_Mode`/args, not just type); I.0 regains the "scene-severed / committed-prefab-only inputs" paragraph that justifies the scene-less local DevKit gate; I.6 reverted to "operate on a COPY, never reserialize the committed fixture in place" (the merge's `ForceReserializeAssets(P)` would dirty the tree / mutate the asset on failure). | Stergios review (Claude) |
| 2026-06-10 | - | Lettered sequence A..I locked (numbered phases retired); AR+VR confirmed Unity 6 (no upgrade prerequisite); Director foresight note on WS A8; completion discipline added | Claude (board) |
| 2026-06-09 | - | Plan merged into single source of truth ("P1" retired into Phase A); filed as PROPOSED (since RATIFIED 2026-06-10) | Claude (board) |

---

## Plan self-review (coverage check)

- [ ] Every WS (A1..A8) is self-contained in this doc (steps + acceptance + gate) - no off-disk dependency.
- [ ] The census (Appendix A) and the WS A3 ticket set (Appendix I) are IN this doc.
- [ ] The four reserved slots cite spec §28; the Unity 6+ baseline cites §28.6.
- [ ] WS A3 is identified as THE gate everywhere (frontmatter, Plan structure, addendum, §4).
- [ ] The behaviour-neutral boundary appears as the addendum (b) seams table + the §4 negative gates + the §H traps.
- [ ] "P1" appears only in the Terminology retirement note + provenance/archive references.
- [ ] The arch-phase numbering used here (Phase D extraction ... Phase I lock) matches the umbrella §2 + after-launch plan.

---

## Execution handoff

**Executes:** Stergios & Alexandros (owners), reviewed by Marie (Diego fallback). **Ratification path:** RATIFIED by Petros (board) 2026-06-10 ->
Petros + Petros's Claude + LooPi (triage) -> Heisenberg/Stergios (architecture + detail) -> dispatch. **Edit
discipline:** local-only edits; **Petros runs git** (the git/branch commands an executor needs are run by Petros as
human-in-the-loop). Tick checkboxes + update the Status & Progress Log as you go; run `Evaluate Changes` before every
push once WS A3 exists.

---

# Appendix I - WS A3 Implementation Pack (the net, concretized)

All paths package-relative (`com.pitech.xr.devkit/`). **Prerequisite:** a UPM package's tests only run when the
package is embedded in a Unity project - stand up (or designate) a **Unity 6+** host project referencing the package
(the HealthOn VR/AR project can serve, or a dedicated `DevKitTestHost`).

**Scope marker:** I.0, I.1 (EditMode), I.2, I.3, I.6, I.7, I.8 = the Phase A net. I.4 + I.5 (PlayMode golden trace) =
Phase D-prep, seeded only.

### I.0 Scenario graph-integrity test (Proof A - the primary net)

Pure EditMode, read-only, runs against real lab prefabs. File: `Tests/Editor/ScenarioGraphIntegrityTests.cs`.
For each lab asset: load it, get the `Scenario`, walk `steps` recursively (into `GroupStep.steps`) collecting all step
`guid`s, then assert:
- **Invariants (no baseline):** no `null` entry in any `[SerializeReference]` `steps` list; every step `guid`
  non-empty + unique; every routing guid (`nextGuid` | `correctNextGuid`/`wrongNextGuid` |
  `passedNextGuid`/`failedNextGuid` *(QuizResultsStep)* | `outcomes[].nextGuid` |
  `defaultNextGuid` + `multiConditionBranches[].nextGuid` | `specificStepGuid` | `childRequirements[].guid`) is `""`
  or a member of the collected set - safest: derive the set generically by the `*NextGuid` suffix over the same
  `SerializedObject` walk, so a future step type's routing field can never be silently missed; every `UnityEvent` (`Choice.onSelected`, `MiniQuizChoice.onSelected`,
  `SelectionStep.onCorrect`/`onWrong`, `EventStep.onEnter`) has, per persistent listener, a non-null target + non-empty
  method name.
- **No *Missing* object reference anywhere in a step (the generic invariant - NO per-type list).** Walk the
  `Scenario`'s `SerializedObject` and visit every `SerializedPropertyType.ObjectReference` reached under the `steps`
  array (`SerializedProperty.Next(enterChildren:true)`, descending into nested `[SerializeReference]` steps, lists and
  arrays). This reaches `QuestionStep.panelRoot`/`panelAnimator`, every `Choice.button`,
  `CueCardsStep.cards[]`/`nextButton`/`tapHint`/`extraObject`/`director`, `TimelineStep.director`, the
  Selection/Conditions/MiniQuiz object fields, the `UnityEvent` call targets, **and any object field added to any step
  in the future**. For each, assert it is not Missing:
  `!(prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)` (a serialized `fileID`/GUID that no
  longer resolves). A field left **cleanly null by design** (`fileID 0`) is allowed; only a *dangling* pointer fails.
- **Snapshot (per-lab baseline JSON - catches a *dropped* or *silently rewired* step/reference/listener the invariants
  would pass).** Both reference layers come from the **same single generic walk** above - no per-Step-type code:
  - **Object-reference map** - for every `ObjectReference` under `steps`, record its **stable property path**
    (e.g. `steps[3].choices[1].button`, `steps[5].panelRoot`) -> the target's **stable identity** (its hierarchy path
    within the fixture, or its serialized `fileID`/GUID - **not** just its type), or `null`/`MISSING`. This is what
    answers "are the Question's panel and choices intact?": it catches a silent rewire (panel swapped to a different
    `RectTransform`; a choice repointed to another `Button`) and a dropped reference a byte-diff can miss after a
    legitimate re-save - with a *human* message (`steps[3].choices[1].button was 'Panel/Btn_Yes', now MISSING`).
  - **Event fingerprint (the typed superset for the call-target subset)** - per persistent call
    (`m_PersistentCalls.m_Calls`), record: the target's **stable identity (path or `fileID`/GUID, NOT just type)**,
    `m_MethodName`, `m_CallState`, `m_Mode`, and the serialized argument values
    (`m_ObjectArgument`(+`m_ObjectArgumentAssemblyTypeName`)/`m_IntArgument`/`m_FloatArgument`/`m_StringArgument`/
    `m_BoolArgument`). (Just `(targetTypeName, method)` is insufficient - it would miss a rewire to a different object
    of the same type.)
  Re-extract both maps after a change and assert equal. Regenerate only via an explicit `--regen`, reviewed as a
  deliberate change.

**What the comparison needs - and what it does *not*.** The baseline is captured **once**, at export, and committed
beside the prefab (`Baseline/GraphSnapshots/<lab>.graph.json`). Every later run re-extracts from the **same committed
fixture prefab under the new package code** and diffs against that committed baseline. **The source HealthOn scene is
never needed again** - its only job was to birth the self-contained fixture (precisely why `Export Lab as Test
Fixture` exists: it severs the scene dependency). The gate's inputs are exactly *committed prefab + committed baseline
+ new code* - nothing else. **This is what lets the scene-less DevKit project be a sufficient, self-contained gate**
(it does not need the HealthOn scenes to run). Re-exporting from a live scene is a separate, deliberate `--regen`,
done only when the *real lab content* changes - never to run the gate.

### I.1 Test assembly layout

`Tests/Editor/Pitech.XR.Scenario.Editor.Tests.asmdef`
```json
{
  "name": "Pitech.XR.Scenario.Editor.Tests",
  "rootNamespace": "Pitech.XR.Scenario.Editor.Tests",
  "references": ["Pitech.XR.Core","Pitech.XR.Scenario","Pitech.XR.Quiz","Pitech.XR.Interactables",
    "Pitech.XR.Stats","Pitech.XR.Scenario.Editor","Pitech.XR.Core.Editor",
    "UnityEngine.TestRunner","UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "autoReferenced": false
}
```
`Tests/PlayMode/Pitech.XR.Scenario.PlayMode.Tests.asmdef` *(seed only; corpus = Phase D)*
```json
{
  "name": "Pitech.XR.Scenario.PlayMode.Tests",
  "rootNamespace": "Pitech.XR.Scenario.PlayMode.Tests",
  "references": ["Pitech.XR.Core","Pitech.XR.Scenario","Pitech.XR.Quiz","Pitech.XR.Interactables",
    "Pitech.XR.Stats","UnityEngine.TestRunner"],
  "includePlatforms": [],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "autoReferenced": false
}
```
`UNITY_INCLUDE_TESTS` keeps both out of player builds; `overrideReferences:true` is required for
`precompiledReferences` to bind nunit; the PlayMode asmdef must NOT reference `UnityEditor.TestRunner`. **Do not copy
the deprecated `optionalUnityReferences:["TestAssemblies"]` template** - migrate the existing ContentDelivery test
asmdef to this modern form too.

### I.2 Folder / file layout
```
Tests/
  Editor/        Pitech.XR.Scenario.Editor.Tests.asmdef + EditMode tests (the net)
    ScenarioGraphIntegrityTests.cs     (Proof A - PRIMARY, I.0)
    ConditionsEvaluatorTests.cs        (8 CompareOp + Approximately + bool encodings)
    GroupStepRequirementTests.cs       (Ensure*/IsChildRequired*)
    ScriptGuidStabilityTests.cs
    PublicApiBaselineTests.cs
    CoreEditorTypeLiteralTests.cs
    SerializedFixtureRoundTripTests.cs
    ContentDeliveryAdditiveTests.cs
  PlayMode/      Pitech.XR.Scenario.PlayMode.Tests.asmdef  (SEED only)
    GoldenTraceTests.cs                ([UnityTest]; one seed fixture)
    GoldenTraceRecorder.cs
  Fixtures/Scenarios/                  *.prefab (+ .meta) - real labs or trimmed copies
  Baseline/
    PublicApi.Pitech.XR.txt
    ScriptGuids.json
    CoreEditorTypeLiterals.txt
    GraphSnapshots/<lab>.graph.json
  Golden/                              <fixture>.trace.json  (Phase D)
```

### I.3 Fixture corpus
Fixtures are read statically in Phase A - they do NOT have to run. Minimum (3-5): `linear_timeline_cuecards_event`,
`branching_question`, `group_specificchild_question` (locks the shared-field routing - highest-risk path), optionally
`miniquiz_selection` (incl. "count met / zero correct / within `allowedWrong` -> CORRECT") and `conditions_component`.
Phase D (runnable, for the golden trace): one per remaining step Kind, all 6 `GroupStep.CompleteWhen` modes, plus
`question_debounce` vs `question_group_firstclick` to pin the divergence before Phase D unifies it.

> **WS A1 Step 3 - corpus MARKED (census freeze 2026-06-10, from the HealthOn VR clone; 15 step-bearing scenes
> surveyed).** Real-lab coverage mapped to the shapes above:
>
> | Fixture | Source lab (HealthOn VR) | Covers |
> |---|---|---|
> | `linear_timeline_cuecards_event` | `Pharmacy.unity` | the ONLY CueCards lab (x20) + Timeline x6 + Event x2 + Question x7; 50 explicit `nextGuid` routes |
> | `branching_question` (+ miniquiz) | `Delirium.unity` | Question x8 + **MiniQuiz x2 with `outcomes[].nextGuid` routing** (the only outcomes user, with `Deliriumold`) + Timeline x10 + Event x19 |
> | `group_multibranch_inserts` | `Loimokseis.unity` | GroupStep x2 with **`multiConditionBranches` x2** + `childRequirements` x6 (the only multi-branch group LAB - the `0 (2)`/`0 (3)`.unity recovery copies of it also match, 2 of the 15 surveyed scenes) + Insert x23 |
> | `selection_correct_wrong` | `Loimokseis_Old_1.unity` | SelectionStep x4 with **`correctNextGuid`/`wrongNextGuid` x4 - the ONLY correct/wrong-routing lab** |
> | `quiz_group_stats` | `Delirium Stats Test.unity` | the only serialized `QuizStep`+`QuizResultsStep` instances + GroupStep + stats wiring (test scene, fine for static reads) |
>
> **Not present in ANY real VR lab** - and therefore **ONE synthetic fixture is MANDATORY at WS A3 Step 7** (it
> completes Proof A's non-vacuous coverage of every ratified routing family; an unexercised invariant is not a net):
> a `GroupStep` in SpecificChild mode with non-empty `specificStepGuid` (the originally planned
> `group_specificchild_question` shape - no real-lab source exists; `group_multibranch_inserts` joins it as the
> real-data group fixture, it does not replace the ratified shape), a `ConditionsStep` (zero uses anywhere in VR
> scenes), a SelectionStep with `allowedWrong > 0` (all four real ones use 0), and a non-empty `defaultNextGuid` -
> one combined synthetic prefab covers all four.
>
> **Prefab sweep (review 2026-06-10, closes the scenes-only survey gap):** all 199 `.prefab` files under the
> HealthOn VR `Assets/` carry ZERO scenario-step content and ZERO `Scenario`/`SceneManager` MonoScript-GUID
> references - every lab exists ONLY as a scene, shipped as an **Addressable scene** (e.g. `lab_delirium` ->
> `lab/delirium/scene/main` = `Delirium.unity`), not a spawned prefab. Consequences: (1) the 5-lab marking stands,
> no prefab adds or duplicates coverage; (2) the gap list above is STRENGTHENED (verified across scenes AND prefabs);
> (3) the prefab-hosted nested-`[SerializeReference]` shape that Proof C's prefab-instance-with-override variant
> exercises has NO real-lab source either - the `Export Lab as Test Fixture` tool (which exports to prefabs) + the
> mandatory synthetic fixture cover it; (4) the package-root stray `--- SCENE MANAGERS ---.prefab` is the only
> committed prefab-hosted step graph anywhere and holds the ONLY serialized `ConditionsStep` instance (3 outcomes,
> all routed) - useful as a static-read input, but it is a template, not a lab, and its Phase D relocation
> disposition is unchanged.
>
> **Caveat:** HealthOn AR labs are not present on this machine (workspace path-mismatch note) - corpus chosen from the
> VR clone only; re-survey AR when accessible.

### I.4 Golden-trace JSON schema (v1) - *Phase D-prep (seed only)*
```json
{
  "schemaVersion": 1,
  "fixture": "branching_question",
  "driver": [ { "stepGuid": "g1", "branchIndex": 1 } ],
  "trace": [
    { "seq": 0, "fromIndex": -1, "toIndex": 0, "stepGuid": "g0", "kind": "Timeline",  "branchGuid": null },
    { "seq": 1, "fromIndex": 0,  "toIndex": 2, "stepGuid": "g1", "kind": "Question",  "branchGuid": "g2" }
  ],
  "sideEffects": [
    { "seq": 0, "type": "unityEvent", "source": "Question.choices[1].onSelected", "atStep": "g1" },
    { "seq": 1, "type": "stat",       "op": "ApplyEffects", "key": "Health", "delta": -10, "atStep": "g1" }
  ]
}
```
Determinism rules: stable key order; `InvariantCulture`; floats `"R"`; no timestamps/frame numbers/instance ids;
preserve emission order; LF; trailing newline. Byte-compare against the committed golden; regenerate only via `--regen`.

### I.5 Golden-trace harness (`GoldenTraceRecorder`) - *Phase D-prep (seed on one fixture)*
1. `#if UNITY_EDITOR` load the fixture prefab via `AssetDatabase`, instantiate into the play-mode scene.
2. Before `Restart()`: add a recording listener to every `UnityEvent`; subscribe a stat-mutation probe
   (wrap/observe `StatsRuntime`); poll `SceneManager.StepIndex` per frame to emit a trace row on change.
3. Drive deterministically with the committed `driver` list via `EditorSkipFromGraph(stepGuid, branchIndex)`.
4. Run until `StepIndex == -1` or a step cap; serialize per I.4; compare.

### I.6 Serialized-diff method (Proof C) - operate on a COPY, never the committed fixture
**Never reserialize the committed fixture in place** - it dirties the working tree, and a failed test would leave a
mutated asset. Per fixture `P`: `AssetDatabase.CopyAsset(P, T)` into a temp folder (e.g. `Assets/_DevKitTestTmp/`);
read `b0 = File.ReadAllText(P)` (committed bytes = the read-only pre-change baseline); reserialize **the copy**
(`AssetDatabase.ForceReserializeAssets(new[]{T})`); `b1 = File.ReadAllText(T)`; `Assert.AreEqual(b0, b1)`; in
`[TearDown]`, `AssetDatabase.DeleteAsset` the whole temp folder. **The committed fixture is never written.** If Unity
normalizes formatting on a first reserialize, capture that normalized form **once** as the committed baseline and
compare the copy to it. **Prefab-instance-with-override variant:** temp scene -> instantiate `P` -> set one override
(e.g. `title`) -> `PrefabUtility.RecordPrefabInstancePropertyModifications` -> save the **temp scene** (NOT `P`) ->
assert the `Scenario` block has no dropped `steps`, no churned `managedReferences` ids, no changed `m_Script` GUID ->
delete the temp scene. **CI backstop (separate from the unit test, on a throwaway checkout only):** a
`ForceReserializeAssets` pass over all fixtures + `git diff --exit-code -- Tests/Fixtures` - never inside a unit test,
never on a dev's working tree.

### I.7 GUID-stability (`ScriptGuids.json`)
Pins the MonoScript GUIDs of every type referenced by `m_Script` in prefabs/scenes: `Scenario`, `SceneManager`,
`QuizUIController`, `QuizResultsUIController`, `QuizAsset`, `StatsUIController`, `StatsConfig`, `SelectablesManager`,
`SelectionLists`, `SelectableTarget`, `ContentDeliverySpawner`, `ContentDeliveryStatusOverlay`. **Split rule
(load-bearing):** plain `[Serializable]` Step classes are referenced by type-string, NOT GUID - when splitting
`Scenario.cs`, the file retaining the `Scenario` MonoBehaviour KEEPS `Scenario.cs.meta`'s GUID; moved step classes get
fresh `.meta` GUIDs (harmless).

### I.8 Public-API baseline (additions-only)
`Tests/Baseline/PublicApi.Pitech.XR.txt`: reflect every `Pitech.XR.*` assembly, enumerate public (+
protected-on-non-sealed) members, one stable sorted line each; test asserts every baseline line still present
(removals fail; additions allowed); update only as a reviewed commit. `Tests/Baseline/CoreEditorTypeLiterals.txt`:
`Pitech.XR.Quiz.QuizAsset`, `Pitech.XR.Stats.StatsConfig`, `Pitech.XR.Quiz.QuizUIController`,
`Pitech.XR.Quiz.QuizResultsUIController`, `Pitech.XR.Stats.StatsUIController`, `Pitech.XR.Scenario.SceneManager`,
`Pitech.XR.Scenario.Scenario`, `ScenarioGraphWindow` - test asserts each resolves. This catches the namespace move the
ordinary baseline misses.

### I.9 Headless entry (the other door - NOT the Phase A gate)
```bash
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform EditMode  -testResults "Logs/editmode.xml"  -logFile "Logs/editmode.log"
# PlayMode (golden trace) - Phase D only:
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform PlayMode  -testResults "Logs/playmode.xml"  -logFile "Logs/playmode.log"
```
`-runTests` auto-quits - do NOT add `-quit` (it races the runner). Exit 0 = all passed; parse the NUnit XML.

### I.10 Gate model
Phase A: human-run in-editor `Evaluate Changes`; push only on green. Optional local hardening: a `core.hooksPath`
pre-push hook calling the I.9 EditMode line. Phase D: server CI (GameCI + license) runs the same commands per PR.

### I.11 `DevKit > Evaluate Changes` - the manual gate
Entry points: `[MenuItem("Pi tech/Tools/Evaluate Changes")]` + a Hub *Maintain* button (same handler). File:
`Editor/Core.Editor/Tools/EvaluateChanges.cs`. Runs the EditMode suite via
`UnityEditor.TestTools.TestRunner.Api.TestRunnerApi` filtered to `Pitech.XR.Scenario.Editor.Tests`; collects results
via `ICallbacks`. Verdict UI: green -> "N checks passed - safe to push"; red -> one plain sentence per failure from
the test message. Shared core `DevKitChecks.RunEditModeGate()` used by BOTH the button and the headless
`static int RunAll()` - one code path, two doors. Growth path: a pre-flight aggregator (net + format check +
graph-integrity summary).

---

# Appendix A - Inventory & Disposition Census (WS A1)

The census IS the WS A1 deliverable - 77 surfaces, each with `file:line` + disposition. Spot-check against the live
source, then freeze.

**Menu-root unification** (the single normalization touching the most rows). Current mix -> target single token
**`Pi tech`**:

| Current prefix | Where | Target (WS A2) |
|---|---|---|
| `Pi tech/` | top-bar `[MenuItem]` windows/commands/tools | keep token; re-group task-first; fix the `0x85` byte |
| `Pi tech XR/` | ALL runtime `[AddComponentMenu]` paths | drop the ` XR` -> `Pi tech/<Module>/...` |
| `GameObject/Pi tech/` | Make Grabbable context entry | keep |
| `Pi tech/` (CreateAssetMenu) | Stats/Quiz/DevBlocks/ContentDelivery configs | keep token; task-first grouping |

Riders: **ORG-03** (SelectableTarget/SelectablesManager/MetaSelectRelay -> Interactables group); **caller fidelity**
(renames of `Pi tech/Scenario Graph`, `Pi tech/Dev Blocks`, `Pi tech/DevKit` MUST update the 5 `ExecuteMenuItem`
callers in `DocsPage.cs`; Meta's `GameObject/Interaction SDK/Add Grab Interaction` untouched).

### (A) Top-bar menu surfaces

| Surface | file:line | Disposition (WS) |
|---|---|---|
| DevkitHubWindow `[MenuItem("Pi tech/DevKit")]` | `Editor/Core.Editor/Hub/DevkitHubWindow.cs:27` | **rename** -> "DevKit Hub" home; tiles + repair tools + Evaluate Changes + Add-Scenario; DocsPage caller tracks (A2) |
| ScenarioGraphWindow `[MenuItem("Pi tech/Scenario Graph")]` | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:148` | **split** 10 named types (census said 6: undercount - `PendingNoteEdit` + `PortMeta` omitted; `EditableNote` + `GroupBox` added by `448301b`) + namespace wrap; Hub tile; DocsPage callers 42/77 (A6, A2) |
| StepEditWindow (no MenuItem) | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:5203` | **split** (A6) |
| DevBlocksWindow `[MenuItem("Pi tech/Dev Blocks")]` | `Editor/Core.Editor/Tools/DevBlocksWindow.cs:41` | **keep** + Hub tile; DocsPage caller 151; dead widgets pruned (A5, A2) |
| AddressablesBuilderWindow `[MenuItem("Pi tech/Addressables Builder")]` | `Editor/Core.Editor/Tools/AddressablesBuilderWindow.cs:59` | **keep** + Hub tile; relocation = Phase D (A2) |
| SceneCategoriesWindow `[MenuItem("Pi tech/Scene/...")]` | `Editor/Core.Editor/Tools/SceneCategoriesWindow.cs:16` | **rename** (strip `0x85`) + Hub tile (A4, A2) |
| Copy Default Quiz UI Prefabs `[MenuItem("Pi tech/Quiz/...")]` | `Editor/Quiz.Editor/QuizDefaultUIPrefabFactory.cs:10` | **keep** (A2) |

### (B) GameObject / Add Component / Create Asset surfaces

| Surface | file:line | Disposition (WS) |
|---|---|---|
| Make Grabbable `[MenuItem("GameObject/Pi tech/Make Grabbable")]` | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:19` | **keep**; Meta's ExecuteMenuItem untouched (A2) |
| MakeGrabbableWindow (ShowUtility) | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:35` | **keep** (A2) |
| StatsConfig `[CreateAssetMenu("Pi tech/Stats Config")]` | `Runtime/Stats/StatsConfig.cs:7` | **keep**; grouping review (A2) |
| QuizAsset `[CreateAssetMenu("Pi tech/Quiz Asset")]` | `Runtime/Quiz/QuizAsset.cs:7` | **keep** (A2) |
| DevBlockItem `[CreateAssetMenu("Pi tech/Dev Blocks/Dev Block")]` | `Editor/Core.Editor/DevBlocks/DevBlockItem.cs:12` | **keep** (A2) |
| AddressablesModuleConfig `[CreateAssetMenu(...)]` | `Runtime/ContentDelivery/AddressablesModuleConfig.cs:29` | **keep**; rewriter fix = after Phase A (A2) |
| AddressablesBuildCatalog `[CreateAssetMenu(...)]` | `Runtime/ContentDelivery/AddressablesBuildCatalog.cs:28` | **keep** (A2) |

### (C) DevKit Hub - window + pages + services *(all keep; cockpit rebuild surfaces)*

DashboardPage `:8` · GuidedSetupPage `:15` · **DocsPage `:8` (CRITICAL caller - `Pi tech/Scenario Graph` 42/77,
`Pi tech/Dev Blocks` 151, `Pi tech/DevKit` 170 + `Window/*`)** · SettingsPage `:11` · IDevkitPage `:6` ·
DevkitContext `:1` · DevkitTheme `:1` (dead `Styles.Primary` removed - A5) · DevkitWidgets `:1` (unused helpers
deleted - A5) · GuidedSetupService · ProjectHealthService · ProjectSetupService · QuizService · ScenarioService (home
for "Add Scenario to Scene") · SceneCategoriesService · SceneManagerService · StatsService - all
`Editor/Core.Editor/...` (A2; some are A3 test targets).

### (D) Editor inspectors, repair tools & GUID services

| Surface | file:line | Disposition (WS) |
|---|---|---|
| ScenarioEditor (11 drawers + Styles) | `Editor/Scenario.Editor/ScenarioEditor.cs:12` | **split** per-file; undo-correctness = after Phase A (A6) |
| SceneManagerEditor | `Editor/Scenario.Editor/SceneManagerEditor.cs:14` | **rename** reflection->typed; deeper removal = Phase D (A6) |
| StatsUIControllerEditor | `Editor/Stats.Editor/StatsUIControllerEditor.cs:10` | **keep**; dead try/catch removed (A5) |
| StatsConfigEditor | `Editor/Stats.Editor/StatsConfigEditor.cs:10` | **keep** (A4, A6) |
| QuizAssetEditor | `Editor/Quiz.Editor/QuizAssetEditor.cs:11` | **keep** (A4) |
| SelectablesManagerEditor | `Editor/Interactables.Editor/SelectablesManagerEditor.cs:7` | **rename** - global-namespace fix (A6) |
| SelectionListsEditor | `Editor/Interactables.Editor/SelectionListsEditor.cs:9` | **keep** (A4, A6) |
| ContentDeliverySpawnerEditor | `Editor/ContentDelivery.Editor/ContentDeliverySpawnerEditor.cs:7` | **keep** (A4) |
| Fix Missing DevKit Script References `[MenuItem("Pi tech/Tools/...",502)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:20` | **keep**, surfaced in Hub; undo = after Phase A (A2) |
| Repair DevKit script GUIDs `[MenuItem("Pi tech/Tools/...",503)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:75` | **keep**, surfaced in Hub (A2) |
| DevKitYamlScriptGuidRepair | `Editor/Scenario.Editor/DevKitYamlScriptGuidRepair.cs:20` | **keep**; comment/EOL only (A3, A4) |
| ScenarioEditorUtil | `Editor/Scenario.Editor/ScenarioEditorUtil.cs:9` | **delete** - provably dead (A5) |

### (E) Runtime components - `[AddComponentMenu]` token unification (`Pi tech XR/` -> `Pi tech/`)

| Surface | file:line | Disposition (WS) |
|---|---|---|
| Scenario | `Runtime/Scenario/Scenario.cs:569` | **rename** -> `Pi tech/Scenario/Scenario`; steps -> `Steps/<Type>.cs` (A6, A2) |
| SceneManager | `Runtime/Scenario/SceneManager.cs:20` | **rename**; delete dead `EvalCompare`; implements `ISceneRunnerControl` (A5, A8, A2) |
| SelectableTarget *(misplaced)* | `Runtime/Interactables/SelectableTarget.cs:18` | **move** ORG-03 -> `Pi tech/Interactables/...` (A6, A2) |
| SelectablesManager *(misplaced)* | `Runtime/Interactables/SelectablesManager.cs:8` | **move** -> `Pi tech/Interactables/...` (A6, A2) |
| MetaSelectRelay *(misplaced, nested)* | `Runtime/Interactables/SelectablesManager.cs:327` | **move** menu path only; file promotion = after Phase A (A2) |
| SelectionLists | `Runtime/Interactables/SelectionLists.cs:81` | **rename** root token (A2) |
| QuizUIController | `Runtime/Quiz/QuizUIController.cs:9` | **rename** (A2) |
| QuizResultsUIController | `Runtime/Quiz/QuizResultsUIController.cs:8` | **rename** (A2) |
| AddressablesBootstrapper | `Runtime/ContentDelivery/AddressablesBootstrapper.cs:7` | **rename** (A2) |
| AttemptReconciliationBridge | `Runtime/ContentDelivery/AttemptReconciliationBridge.cs:9` | **rename** (A2) |
| BridgeLaunchContextReceiver | `Runtime/ContentDelivery/BridgeLaunchContextReceiver.cs:11` | **rename** (A2) |
| SerializedLaunchContextProvider | `Runtime/ContentDelivery/SerializedLaunchContextProvider.cs:8` | **rename** (A2) |
| LaunchContextReporter | `Runtime/ContentDelivery/LaunchContextReporter.cs:34` | **rename** (A2) |
| ContentDeliveryStatusOverlay | `Runtime/ContentDelivery/ContentDeliveryStatusOverlay.cs:14` | **rename** (A2) |
| ContentDeliverySpawner | `Runtime/ContentDelivery/ContentDeliverySpawner.cs:33` | **rename** (A2) |
| RuntimeTelemetryAdapter | `Runtime/ContentDelivery/Analytics/RuntimeTelemetryAdapter.cs:80` | **rename** -> `Pi tech/Analytics/...` (A2) |
| TelemetryAutoWirer | `Runtime/ContentDelivery/Analytics/TelemetryAutoWirer.cs:13` | **rename** (A2) |
| LaunchContextProviders (empty placeholder) | `Runtime/ContentDelivery/LaunchContextProviders.cs:1` | **delete** + `.meta` (A5) |

> **Non-menu runtime services (kept):** `AddressablesService`, `AddressablesBuildService`,
> `AddressablesValidationService`, `PublishReportService`, `AddressablesAdapterResolver`, `ContentDeliveryCapability`
> (some are A3 test targets). **`HealthOnAddressablesAdapter`** - **defer** (de-coupling = Phase D, untouched here).

### (F) The `[SerializeReference]` data model split map (`Scenario.cs` -> `Steps/<Type>.cs`, WS A6 step 1)

| Type | from `Scenario.cs:` | to |
|---|---|---|
| Step (abstract base: guid/graphPos/Kind) | 14 | `Steps/Step.cs` |
| TimelineStep | 23 | `Steps/TimelineStep.cs` |
| CueCardsStep (+ AdvanceMode) | 37 | `Steps/CueCardsStep.cs` |
| QuestionStep (+ Choice) | 104 | `Steps/QuestionStep.cs` |
| MiniQuizStep (+ Choice/Question/Outcome/CompleteMode) | 166 | `Steps/MiniQuizStep.cs` |
| SelectionStep (+ CompleteMode) | 203 | `Steps/SelectionStep.cs` |
| InsertStep | 264 | `Steps/InsertStep.cs` |
| EventStep | 305 | `Steps/EventStep.cs` |
| ConditionsStep (+ ConditionsEvaluator - prime A3 unit-test target) | 341 | `Steps/ConditionsStep.cs` |
| GroupStep (+ CompleteWhen/ChildRequirement/MultiConditionBranch) | 410 | `Steps/GroupStep.cs` |

> **Census freeze addendum (2026-06-10):** two Step subclasses live OUTSIDE `Scenario.cs` and were absent from the
> census tables: **`QuizStep`** (`Runtime/Scenario/QuizStep.cs:8`) + **`QuizResultsStep`**
> (`Runtime/Scenario/QuizResultsStep.cs:12`). Disposition: **keep** (already one-type-per-file, namespace
> `Pitech.XR.Scenario`, no move needed -> no `.meta`/GUID risk). Routing guids *(corrected at review 2026-06-10)*:
> `QuizStep` routes via `nextGuid`/`correctNextGuid`/`wrongNextGuid`; `QuizResultsStep` via
> **`nextGuid`/`passedNextGuid`/`failedNextGuid`**. **WS A3 requirement:** Proof A's routing-guid enumeration must
> include `passedNextGuid`/`failedNextGuid` - safest is deriving routing fields generically (the `*NextGuid` suffix +
> the generic `SerializedObject` walk), never from a hardcoded census list. The Phase D "one per step Kind" corpus
> must include both types. All (F) line anchors
> above re-verified EXACT against live source 2026-06-10 (the `448301b` Step-field additions were reverted to a
> side-table - see the Status log - restoring the original anchors). `Scenario`'s editor-only nested types are now
> `GraphNote` + `GraphGroup` (448301b) + `StepGraphDisplay` (the 448301b reconciliation).

**Disposition summary:** keep ~28 · rename ~16 · move 3 (ORG-03) · split ~13 · delete 3 (`ScenarioEditorUtil`, empty
`LaunchContextProviders.cs`, dead `EvalCompare`) · defer 2+ (HealthOn adapter; window relocation; undo-correctness;
runtime reflection removal).

---

# Appendix B - SceneManager: the Phase A baseline (what must be preserved verbatim)

`SceneManager` (`Runtime/Scenario/SceneManager.cs`, ~2,505 lines) is the runtime scenario interpreter - it IS the
runtime. **Phase A LOCKS it whole**; the transition (extraction -> LabConsole) is post-launch (spec §28 + the
after-launch plan).

**The public contract (preserve verbatim):**
- **Labs** assign `scenario`, `autoStart`, optional stats/quiz/selection refs, `labContentRoot`; call the selection
  bridges `ActivateSelectionList(int)` / `ActivateSelectionListByName(string)` / `CompleteSelection()` /
  `RetrySelection()` from Timeline signals/UnityEvents.
- **ContentDelivery** resolves the manager by `GetType().FullName=="Pitech.XR.Scenario.SceneManager"`
  (`ContentDeliverySpawner.cs:1134`) and **string-reflects** `autoStart` (1151/1158) + `Restart` (1173) - member
  renames silently break the spawn flow.
- **Editor** - `ScenarioGraphWindow` calls `EditorSkipFromGraph(guid, branchIndex)` (SceneManager.cs:1523; the census's
  1588 was a stale anchor - file unchanged since 2026-04-09) + reads `StepIndex` (window :1916, was :1857 pre-`448301b`);
  `SceneManagerEditor` reflects `scenario`(367)/`StepIndex`(467)/`Restart`(505) - all three verified exact 2026-06-10.
- **Lab scenes** (and the package's stray root prefab) reference the component by **MonoScript GUID
  `2d431a49d183e9c428369f7f758f75cd`** and rely on `FormerlySerializedAs` on
  `defaultQuiz`/`quizPanel`/`quizResultsPanel`. *(Prefab sweep 2026-06-10: zero VR lab prefabs carry
  Scenario/SceneManager - labs ship as Addressable scenes; AR unverified from this machine.)*

**Load-bearing semantics (verbatim):** the `FallbackGuid '' == linear-next` contract (1000-1004); the exact `Run()`
type-dispatch order + branchGuid-assignment pattern; `StepIndex` semantics (`{get;private set;}`, `-1` idle/finished,
reflected by name); the public symbol NAMES (reflected as strings); the `FormerlySerializedAs` mappings; the pinned
MonoScript GUID; the `_editorSkip`/`_editorSkipBranchIndex` integer encoding (choice index / `-1` default / `-2`
correct / `-3` wrong / outcome index); the `DeactivateAllVisuals` invariant; the `selectables.pickingEnabled` refcount
discipline; the `_groupExit*` resolve->consume->reset handshake; `Time.unscaledDeltaTime` for real-time waits.

**Transition summary (post-launch; detail in the after-launch plan + spec §28):** Phase A LOCKS -> after-A WRAPS
additively (`IScenarioFlowStore` + `LocalScenarioFlowStore` + the `NetworkedStatesScenarioFlowStore` adapter; minimal
`LabEventLedger`) -> Phase D EXTRACTS behind a thin facade (golden-trace-proven `IStepRunner` registry) -> Phase E
FRONTS with LabConsole + typed Fusion under the flow-store -> Phase G..P7 grow analytics + VICKY on the seams -> at
the 1.0 lock an **offered (never forced)** migration converts labs to LabConsole-native and SceneManager retires.
