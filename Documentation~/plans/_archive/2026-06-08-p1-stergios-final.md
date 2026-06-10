---
title: DevKit v1.0 - P1 Behaviour-Neutral Professionalization - Review & Final Plan (Stergios FINAL)
status: FINAL (Stergios, 2026-06-08) - the executable detail for launch Phase A
date: 2026-06-08
owner: Stergios
provenance: Reproduced to disk 2026-06-09 from Stergios' FINAL plan (mojibake cleaned). If this diverges from Stergios' source, his source wins. This is the on-disk "canonical detail" companion referenced by 2026-06-09-phase-a-refactor-and-foundation.md.
maps_to: launch Phase A. WS-name mapping - WS-pre = WS A1 | WS-6 = WS A2 | WS-0 = WS A3 (THE gate) | WS-1 = WS A4 | WS-2 = WS A5 | WS-3 = WS A6 | WS-4 = WS A7 | WS-5 = WS A8.
references:
  - 2026-06-09-phase-a-refactor-and-foundation.md (the launch-framing layer over this plan)
  - 2026-06-09-devkit-launch-plan.md (umbrella)
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md (architecture; §28 domain & content systems; §28.6 Unity 6+ baseline)
---

# DevKit v1.0 - P1 Behaviour-Neutral Professionalization: Review & Final Plan

> **Status:** Final. This document is the P1 single source of truth for `com.pitech.xr.devkit`. It supersedes the prior P1 plan and the prior architecture's P1 section.
> **Scope:** Phase 1 (P1) only - behaviour-neutral professionalization. Post-P1 items (P2/P3/...) are named for traceability but are explicitly out of P1.
> **Governing law:** A change is admitted to P1 **if and only if** it passes all three equivalence proofs simultaneously (golden-trace byte-equal + public-API additions-only + serialized-diff-zero), *and* the proof harness exists when the change lands.
> **Unity baseline (Petros, 2026-06-09):** Unity 6+ is the project/test/build baseline (spec §28.6). Where this plan's §I says "minimal host Unity project (2022.3)", read **Unity 6+**. `package.json version` stays `0.10.5` in P1; the `unity`-field floor-bump + the `dependencies` block are a single after-P1 metadata cutover.

> **Appendices:** A - full surface/disposition census (WS-pre / WS A1); B - SceneManager today + its transition to LabConsole (now also in spec §28 + the after-launch plan).

---

## A. Executive summary

- **The package works and ships, but it is organizationally immature.** Functionality and the launch/telemetry/serialization data contracts are carefully designed; the discipline around them (file organization, encapsulation, encoding, dependency truth, test coverage) is not. This is a professionalization phase, not a rescue.
- **The single most important framing decision: draw the P1 line at the equivalence-proof boundary, not at the size-of-change boundary.** Far *more* reorganization is provably behaviour-neutral than the current plan allows (god-class file splits, dead-code deletion, namespace hygiene). But the *highest-value* cleanups (runner unification, dispatch registry, undo-correctness, reflection removal) are *not* behaviour-neutral and must stay out of P1. Both the conservative plan and the "rewrite everything" instinct are half right.
- **P1 is four things and nothing else:** (1) the **EditMode safety net** - graph-integrity (refs/routes/events) + serialized/GUID-stability + API baseline + pure-logic locks (the PlayMode golden trace is **seeded but deferred to P2**, since P1 changes no runtime logic); (2) provably-dead-code deletion; (3) pure same-namespace/same-assembly file splits and tiny utility extractions, each carrying its `.meta`; (4) zero-compiled-impact hygiene (formatting/EOL/comments, XML docs, `rootNamespace` fixes, package docs/metadata).
- **The safety net must land first.** There is currently zero Scenario/Core/serialization test coverage and no GUID-stability or API-baseline guard. Until it exists, *every* "behaviour-neutral" move is unfalsifiable. **The realistic P1 net is cheap and static** (EditMode, runs against real labs, no play mode, no flaky CI); the expensive PlayMode golden trace is P2's tool, only seeded here.
- **Execution order - editor restructure first, then the net, then the cutting.** **WS-pre (census) -> WS-6 (editor surfaces + DevKit Hub cockpit + `Evaluate Changes`) -> WS-0 (the net) -> WS-1/2/3 (the data-touching work).** WS-6 leads because menus/Hub aren't serialized into labs (safe without the net); WS-0 then gates everything that touches lab data. The P1 gate is the in-editor **`DevKit > Evaluate Changes`** button, run against a fixture corpus extracted from real labs - **no server CI in P1** (it arrives at P2 with the PlayMode rig). New features (Make-Multiplayer, Localization) enter as **gated modules + Hub pages** - structure reserved in WS-6, logic post-P1.
- **P1's job is NOT to shrink SceneManager.** It is to make the *next* engineer able to *prove* a shrunken SceneManager behaves identically. The 2,506-line God-class gets *locked* - graph-integrity + serialized/GUID in P1, the golden-trace replay in P2 - not *cracked*. The `RunXxx`/`RunXxxGroup` variants are not bit-identical today (standalone `RunQuestion` debounces; the group variant is first-click-wins), so unification is a behaviour change -> P2.
- **Two "P1-safe" items from the inputs are mis-scoped and corrected here:** (1) the `Scenario.cs` data-model split stays P1 but is the highest-serialization-risk item and is **gated on the test harness existing first**; (2) adding `dependencies` to `package.json` is **not** metadata-only - it changes UPM resolution in consumer projects, so it moves to **after P1** (the license/URL/`unityRelease` metadata stays P1).
- **Top risks, in order:** (1) a regenerated `.meta` GUID during a file split nulls every authored step graph in shipped lab prefabs; (2) Core.Editor reaches Scenario/Quiz/Stats by **hard-coded `FullName` strings**, an invisible contract a namespace move breaks silently *and* that the standard API baseline would not catch; (3) a confirmed **critical** cross-system bug (`AddressablesRemoteUrlRewriter` nulls the global Addressables transform) that is real but *not* P1 because fixing it changes behaviour.
- **The current P1 plan is too conservative on file-splits/dead-code/namespacing and too vague on its gates.** It marks the golden trace "preferred," has unmeasurable exit criteria, has no GUID-stability gate, and does not pin the Core.Editor reflection literals. This document fixes all of that.

---

## B. State of the package

### B.1 Organization

- **God-classes dominate the surface area.** `SceneManager.cs` (~2,506 lines) owns serialized config, input polling, reflection condition evaluation, stat binding, quiz/results orchestration, group concurrency, and an editor-stepping hook - dispatched through *two* parallel hand-written `if (step is X)` ladders with every step type implemented twice (`RunXxx` + `RunXxxGroup`, ~1,000 duplicated lines). `ScenarioGraphWindow.cs` (~4,962 lines) is a single file holding six types including a second `EditorWindow`, with the route/branch schema hand-duplicated across **seven** methods. `ScenarioEditor.cs` (~1,480 lines) mixes the inspector, a `Styles` theme, and eleven `PropertyDrawer`s. `ContentDeliverySpawner.cs` (~1,180 lines) spans policy, UX, Addressables lifecycle, and reflection coordination.
- **The data model itself is disciplined where it matters.** `Scenario.cs` correctly uses `[SerializeReference]`, `[FormerlySerializedAs]` on renamed fields, `[Serializable]` on every subclass, and - load-bearing - the `OnValidate` no-null-strip + `isCompiling` guard that prevents permanent step-graph data loss. This contract is sound and must not be touched in P1.

### B.2 Dependency / asmdef truth

The 12-assembly DAG is topologically sound (Editor->Runtime only, Core as runtime leaf, no cycles, all five Editor asmdefs correctly `includePlatforms:["Editor"]`). But the *metadata* is dishonest:

| Assembly / file | Hard-references (asmdef) | Declared in `package.json` | Truth |
|---|---|---|---|
| `Pitech.XR.ContentDelivery` (runtime) | `Unity.Addressables`, `Unity.ResourceManager`, `Unity.TextMeshPro`, `Unity.ugui` | **none** | Consumer without these gets unresolved-assembly compile errors |
| `Pitech.XR.ContentDelivery.Editor` | `Unity.Addressables.Editor` (+ above) | **none** | Same |
| Addressables usage in source | `#if PITECH_ADDR`-guarded (6 regions) | versionDefine `"1.0.0"` (degenerate floor) | "Optional" in source, but hard-referenced in asmdef - guard's `#else` is **dead** |
| TMP / ugui usage (`ContentDeliveryStatusOverlay.cs:3,5`) | **un-guarded** `using TMPro;` / `using UnityEngine.UI;` | **none** | Genuinely **required**, regardless of any define |
| `com.unity.services.ccd.management` (`PITECH_CCD`) | versionDefine only; never referenced in any runtime `.cs` | n/a | Genuinely optional / effectively dead |
| `Unity.ResourceManager` (explicit ref) | referenced alongside Addressables | n/a | **Correctly kept** - asmdef refs are non-transitive and source uses `ResourceManagement.*` types directly |

Other asmdef-truth issues: `Interactables.Editor.asmdef` `rootNamespace` is wrongly `Pitech.XR.Scenario.Editor` (copy-paste); references mix raw `GUID:` and plain-name forms with no rule; asmdef JSON formatting is inconsistent (Core single-line, Core.Editor 2-space, the rest 4-space); `Interactables.Editor` references `Unity.Timeline` with no apparent consumer.

### B.3 Test coverage

Real but narrow. Five EditMode files exercise **only** ContentDelivery pure logic (CCD URL composition, versioned local paths, idempotency, state-machine transitions). There is **zero** coverage of Core/Scenario/Interactables/Quiz/Stats, no PlayMode `[UnityTest]`, no `[SerializeReference]` open/save round-trip test, no GUID-stability test, and no public-API baseline. The existing test asmdef also uses the deprecated `optionalUnityReferences:["TestAssemblies"]` form - a poor template to copy. **The P1 behaviour-neutral claim depends entirely on tests that do not yet exist.**

### B.4 Hygiene

- A stray `--- SCENE MANAGERS ---.prefab` (GUID `a0032abe...`) sits at the **package root** (non-idiomatic for UPM; shell-hostile name).
- No `README.md` / `CHANGELOG.md` / `LICENSE.md` / `.editorconfig` / `.gitattributes`.
- Encoding rot: a lone `0x85` byte inside a `[MenuItem]` path string; `U+FFFD` replacement chars in `DevkitWidgets.cs` comments; Greek-language comments and one Greek `[Tooltip]` scattered through SceneManager, SelectionLists, and editors.
- `Runtime/link.xml` uses `preserve="all"` on six whole assemblies - **safe today** (it does cover the `[SerializeReference]` Step types) but over-broad; must not be narrowed later without enumerating every reflection-instantiated type.

---

## C. Top problems found in the codebase

Prioritized. "BN?" = behaviour-neutral (eligible for P1). Evidence is to file:line where cited in findings.

| Sev | File | Issue | BN? | Phase |
|---|---|---|---|---|
| **Critical** | `Runtime/ContentDelivery/AddressablesRemoteUrlRewriter.cs:121,129-138` | `Install()` overwrites the **global** `Addressables.ResourceManager.InternalIdTransformFunc` without saving the prior value; `Uninstall()`/`Clear()` set it to `null` unconditionally - destroys any host/other-package transform. In UaaL this can break the host RN app's Addressables. **Confirmed bug, not a smell.** | No | **after P1** (save-on-install / restore-on-uninstall; regression test; route via bridge/host owner) |
| **High** | `Runtime/Scenario/SceneManager.cs` (whole) | 2,506-line God-class; two parallel type-switch ladders; every step type implemented twice (`RunXxx`/`RunXxxGroup`); shared-field group-exit routing (`_groupExitBranchResolved`, etc.). Variants are **not** bit-identical (`RunQuestion` debounce vs `RunQuestionGroup` first-click-wins). | No | **P2** (lock with golden trace in P1) |
| **High** | `Editor/Core.Editor/Services/{Quiz,Stats,Scenario}Service.cs` | Core.Editor resolves Scenario/Quiz/Stats by hard-coded `FullName` strings (`"Pitech.XR.Quiz.QuizAsset"`, `"Pitech.XR.Stats.StatsConfig"`, `ScenarioGraphWindow` by `t.Name`). **Invisible API contract**: a namespace move breaks the Hub at runtime *and passes the compiler and the standard reflection API baseline*. | n/a (constraint) | **P1** to pin in baseline; renames -> P2 |
| **High** | `package.json` + ContentDelivery asmdefs | Zero declared `dependencies` against four hard-referenced Unity packages; TMP/ugui used un-guarded. Consumers without them fail to compile. | Partly | **after P1** (`dependencies` block changes resolution); metadata-only fields are P1 |
| **High** | `Runtime/ContentDelivery/...` (multiple) | String-reflection dispatch (`autoStart`/`Restart`/`StepIndex`/`scenario`/`steps`/`guid`) and `FindObjectsOfType` in **runtime package code** - banned, IL2CPP/AOT-fragile, per-frame in `RuntimeTelemetryAdapter`. | No | **P2** (shared `ISceneRunnerControl` interface in Core) |
| **High** | `Tests/Editor/*` | No Scenario/Core/runner/serialization tests; no golden trace; no GUID-stability or API baseline. The entire P1 safety case rests on absent tests. | Yes (additive) | **P1** (first) |
| Medium | `Editor/Scenario.Editor/ScenarioGraphWindow.cs` (structural `[SerializeReference]` list mutations) | `scenario.steps.Add/Insert/RemoveAt`, `group.steps.*` guarded only by `Undo.RecordObject`, never `RegisterCompleteObjectUndo` - ambiguous managed-reference undo ("type tree changed"). | No | **after P1** |
| Medium | `Editor/Scenario.Editor/ScenarioEditor.cs:220-231` | Per-element "Remove null entry" button deletes a `[SerializeReference]` slot with **no confirmation dialog** - during transient nulls (import/apply/reload) this can permanently destroy a still-valid step. Contradicts the single-sanctioned-path invariant. | No | **after P1** (elevated; can lose shipped-lab data) |
| Medium | `Editor/ContentDelivery.Editor/AddressablesAdapterResolver.cs:23-33` | Vendor-specific `HealthOnAddressablesAdapter` ships inside the generic toolkit and is the reflected default fallback - brand coupling in `com.pitech.xr.devkit`. | No | **P2** (document in P1) |
| Medium | `Editor/Core.Editor/...` (layering) | `Core.Editor -> ContentDelivery(.Editor) -> Core`; a ContentDelivery feature window (`AddressablesBuilderWindow`) lives inside Core.Editor. Naive "move the window" creates a **circular asmdef ref** that will not compile. | No | **P2** (hard constraint in P1) |
| Medium | `--- SCENE MANAGERS ---.prefab` (root) | Stray content prefab at package root (GUID `a0032abe...`); must carry `.meta` if moved. | No (referenced by GUID) | **P2** |
| Low-Med | hygiene (multiple) | Mojibake (`0x85` in `[MenuItem]`; `U+FFFD` in comments), Greek comments/tooltip, wrong `rootNamespace`, missing root docs, inconsistent asmdef formatting. | Mostly Yes | **P1** (the `0x85` menu string is **after P1**) |

**The one genuinely confirmed P1-deletable dead code** (all verified zero-caller, private/internal, not serialized): `SceneManager.EvalCompare` (1168-1182), `ScenarioEditorUtil.cs` (entire), empty `LaunchContextProviders.cs`, `BuildDefaultPrefabAddressKey` (AddressablesService 811-814), `Styles.Primary`, the `"defaultNextGuid"` ternary (ScenarioEditor 1041), the dead `DevkitWidgets` cluster (StatusChips/StatusBar/Kpi/Tile/StatusRibbon/StatusHeader/ProgressBar/ProgressBarPro + `DevBlocksWindow.SmallButton`), `RebuildLinksFromGraph` forwarder, and the dead `try/catch` in `StatsUIController.Init` (56).

---

## D. Problems & unprofessionalities in the CURRENT (prior) P1 plan

Direct and specific. The plan's instincts are sound; its scoping and its gates are not.

### D.1 The scope mismatch (the central issue)

**The prior plan demotes provably-neutral reorganization to a P2 "inventory," and only blesses "extract tiny utility classes."** That is under-scoped against both the professionalization intent and the verified findings. The adversarial verifier confirmed **12 of 14** P1-tagged reorganizations are genuinely behaviour-neutral, including the full god-class **file splits** (`Scenario.cs` 11 types; `ScenarioEditor.cs` 11 drawers; `ScenarioGraphWindow.cs` 6 types), the **dead-code deletions** above, and the **namespace/`rootNamespace` hygiene** - because `[SerializeReference]` keys on type identity (namespace + assembly + typename), **not file path** (`QuizStep`/`QuizResultsStep` already prove the pattern), and editor types are never referenced by GUID in serialized lab data.

**But the "rewrite everything worth it" instinct over-reaches the other way.** The verifier and findings are emphatic that the *highest-value* work is **not** neutral and must stay out of P1: `RunXxx`/`RunXxxGroup` unification and the `IStepRunner` registry (variants diverge), routing `RecordObject->RegisterCompleteObjectUndo` and direct-field->`SerializedObject` (change undo/prefab-override behaviour), runtime reflection/`Find` removal, the rewriter restore fix, and any namespace/assembly **move** of a `[SerializeReference]` type (needs `[MovedFrom]`). The plan is **correct** to defer these.

**Correction:** redraw the P1 line at the proof boundary. P1 = every change passing all three proofs. after P1 = everything failing any proof (the most impactful cleanups). See §E.

### D.2 Gaps (things the prior plan does not capture)

- **No GUID-stability gate.** "assets load without missing scripts" is a downstream symptom, not the invariant. Every shipped lab references scripts by GUID; the file splits the plan should invite *require* an explicit test pinning `Scenario`/`SceneManager`/Step `.meta` GUIDs.
- **The Core.Editor `FullName` reflection contract is unrecorded.** The standard API baseline reflects assembly public surfaces only, so a Step/config namespace move passes the baseline yet breaks the Hub. The plan's own invited splits are the trigger.
- **The layering inversion is never named.** Nothing warns that moving `AddressablesBuilderWindow` to `ContentDelivery.Editor` creates a circular asmdef ref. Someone will attempt the obvious move and break the build.
- **The critical rewriter bug is downgraded to an open question** rather than a confirmed defect with an owner and an after-P1 slot.
- **No optional-dependency compile test.** No gate proving the no-Addressables path even compiles (it currently can't - Addressables is hard-referenced).
- **No automation.** `.editorconfig`/`.gitattributes` added as *files* with no enforced check; the API snapshot "manually maintained" - a non-gate. Formatting churn will recur.
- **Mojibake/encoding under-scoped** to a single vague mention.

### D.3 Vague / unmeasurable criteria

The prior exit criteria - "line ending and formatting churn is controlled," "the codebase is organized well enough," "optional package promises are either tested or removed," "changes are reviewed deliberately" - have **no objective threshold**. Each must become a runnable gate (see §F).

### D.4 The golden trace is marked "preferred," not required

A "smoke checklist" is offered as the acceptable minimum. The PlayMode golden trace (driven by `EditorSkipFromGraph`, capturing `fromIndex/stepGuid/Kind/branchGuid/toIndex` + an **ordered side-effect log**) is the **non-negotiable** precondition for any runner work and the only valid definition of "plays identically." A smoke checklist cannot prove byte-equivalence and cannot unblock P2. **Side-effect-order capture** is omitted - which is exactly what the P2 `RunSelection`/`RunQuestion` unifications hinge on.

### D.5 The "looks safe but isn't" list is missing

No caution about editor cleanups that *look* like P1 but change behaviour: `RecordObject->RegisterCompleteObjectUndo` on `[SerializeReference]` lists, the `JsonUtility` deep-copy fidelity question, routing direct-mutation->`SerializedObject`, and the ungated null-delete. Without an explicit deferred list, a well-meaning contributor will land one of these as a "cleanup."

---

## E. The FINAL P1 plan

**Execution order** (numbered by topic, sequenced by safety): **WS-pre -> WS-6 -> WS-0 -> WS-1 -> WS-2 -> WS-3 -> WS-4 -> WS-5.** Two land *before* the safety net on purpose - **WS-pre** (the inventory census; no code change) and **WS-6** (editor surfaces + DevKit Hub; menus/Hub are *not* serialized into labs, so they cannot corrupt a shipped lab - proof is "compiles + items appear + `ExecuteMenuItem` callers resolve," not the data tests). **WS-0** (the net + `DevKit > Evaluate Changes`) then lands before any *data-touching* work. **The guardrail: WS-2 (dead-code) and WS-3 (file splits) - the only workstreams that touch lab data - never land before WS-0.**

### Equivalence proofs (the P1 admission test, applied to every commit)

P1 moves declarations between files, deletes dead code, and reformats - **none of it changes the runner's execution paths.** So the P1 admission test is **static + additive (all EditMode)**, not a runtime replay. The expensive PlayMode replay is the admission test for *runtime* changes, which is P2.

- **Proof A - Scenario graph integrity** *(primary P1 net)*. For each lab fixture the authored `[SerializeReference]` step graph is intact after the change: (i) **references** - every assigned step reference still resolves, no nulled/Missing refs, no null step in a `[SerializeReference]` list; (ii) **routes** - every routing guid (`nextGuid`, `correctNextGuid`/`wrongNextGuid`, `outcomes[].nextGuid`, `defaultNextGuid`, `specificStepGuid`, `multiConditionBranches[].nextGuid`, `childRequirements[].guid`) is empty or points to an existing step guid, recursing into `GroupStep.steps` (no dangling routes); (iii) **events** - every `UnityEvent` persistent listener keeps a live target + non-empty method (no "Missing" wiring). Universal invariants need no baseline; a per-lab snapshot also catches dropped/rewired steps. **Read-only, pure EditMode, runs against real labs - no play mode, no runnable fixtures.**
- **Proof B - Public-API additions-only.** Reflected `PublicOnly` surface over all `Pitech.XR.*` may only *gain* members. **Extended for this package** to assert the Core.Editor `FullName` literals still resolve.
- **Proof C - Serialized & GUID integrity.** GUID-stability (every MonoScript `.meta` GUID unchanged) + serialized-diff (each fixture, **scene object and prefab-instance-with-override**, open->save = zero structural change).

**Failing any one -> behaviour change -> deferred to after P1.**

> **Deferred to P2 - Proof D (golden trace).** A PlayMode replay (`EditorSkipFromGraph`-driven trace + ordered side-effect log) proves the *runner interprets the graph identically*. Because P1 never changes runtime logic, Proofs A-C are a complete net for it; the golden trace is the admission test for **P2** (the `RunXxx`/`RunXxxGroup` unification, where execution paths actually change). P1 only **seeds** the harness - one happy-path fixture to validate the approach and de-risk P2 - it is **not** a P1 gate, and the full fixture corpus + PlayMode-in-CI is P2 work.

---

### WS-pre (= WS A1) - Inventory & disposition census *(run FIRST; no code change)*

**Goal:** enumerate every surface before touching any of them, so every later WS is mechanical and surprise-free. **Already produced:** **Appendix A** (below) - 77 surfaces, each with `file:line` + disposition (~40 keep / 18 rename / 13 split / 3 move / 2 delete / 1 defer), the menu-root-unification table, and the full SceneManager anatomy. It exists because the menu-root unification touches reflected `ExecuteMenuItem` callers, the splits move types across files while pinning `.meta`/GUIDs, and the deletes must be proven caller-free - none of which is safe to start blind.

**Tasks:** confirm the census is current vs the live source (spot-check GUIDs/line anchors - e.g. `SceneManager` MonoScript GUID `2d431a49d183e9c428369f7f758f75cd`); freeze it as the disposition reference WS-1...WS-6 execute against.
**Acceptance:** every WS-1...WS-6 edit maps to a census row.
**Gate:** none - this is the map everything else follows.

---

### WS-6 (= WS A2) - Editor surfaces & DevKit Hub cockpit *(runs before WS-0; editor-only, safe without the net)*

**Goal:** one consistent surface taxonomy and a Hub that is the cockpit for everything. Safe before the data net because menus/Hub are **not serialized into labs** (components bind by script GUID) - proof is compile + "items still appear" + `ExecuteMenuItem` callers resolve, *not* the graph-integrity test.

**Naming (locked):** menu root token = **`Pi tech`** everywhere; home window = **"DevKit Hub"**.

**Tasks** (findings: ORG-03, SCN-03-hub mojibake, the WS-pre census):
1. **Unify the menu root.** Drop the ` XR` from **all** `[AddComponentMenu]` paths (`Pi tech XR/<Module>/...` -> `Pi tech/<Module>/...`); keep the `GameObject/Pi tech/` and `Pi tech/` (top-bar + CreateAssetMenu) trees. **Each rename updates its internal `ExecuteMenuItem` callers in `DocsPage.cs` (5 calls) in the same commit**; leave Meta's `GameObject/Interaction SDK/...` alone (not ours).
2. **ORG-03:** move `SelectableTarget` / `SelectablesManager` / `MetaSelectRelay` Add-Component paths from the `Scenario` group -> the `Interactables` group.
3. **Mojibake:** strip the `0x85` byte from the `SceneCategories` `[MenuItem]` path (editor-visible-string rule; grep-verify no programmatic reference first).
4. **Rebuild the Hub as the cockpit.** Brand the window **"DevKit Hub"**; reorganize pages **task-first**: **Setup / Author / Deliver / Maintain / Reference**. Every **workspace window** (Scenario Graph, Dev Blocks, Addressables Builder, Scene Categories) gets a **launch tile** (the Hub *launches* them, never re-implements them). Surface the **repair tools** and **`Evaluate Changes`** in *Maintain*. Add an **"Add Scenario to Scene"** command.
5. **Surface-type discipline (5 interaction types):** name by type - verb for **commands** ("Add Scenario to Scene"), noun for **workspaces** ("Scenario Graph"), "... Wizard" for **wizards**. **Add Component / Create Asset are the fallback tier** (the automation does the wiring, so they're barely used - keep tidy, don't foreground).
6. **Graph readability - node labels, section shapes & branch labels.** Split by whether the author's intent must be *saved*:
   - **P1 (derived/visual - stores nothing):** a **derived node label** (computed from `Kind` + key params, e.g. "Question - Is the patient stable?" - not stored); **rendering the labels branches already carry** on their connecting edges (`ConditionOutcome.label`, `MiniQuizOutcome.label`, the choice text - these `label` fields already exist in the data model); and **structural coloring** (by `Kind` / `GroupStep` membership). Pure presentation - behaviour-neutral, fits here.
   - **First after-P1 (additive - persists author intent):** **background "section" shapes** - a purely visual box drawn *behind* a chosen set of step nodes to group/distinguish them, with **no functional effect** on flow - and **custom branch names** on edges that don't already carry a `label`. Both must be *saved*, via an editor-only serialized field using the same `#if UNITY_EDITOR` pattern as the existing `Scenario.GraphNote`. Saving changes the asset -> fails serialized-diff -> not P1, but the very first additive item right after, and low-risk (old labs default to empty).

**Future feature slots - reserve the structure now; logic lands post-P1.** New features enter the DevKit by one repeatable recipe: **(a)** a `Runtime/<Feature>` and/or `Editor/<Feature>.Editor` module with its own asmdef; **(b)** optional-package dependencies are **gated** via `versionDefine` + `#if` (never hard deps); **(c)** the right entry points + a Hub tile/page under the right task group; **(d)** reference Core/Scenario, never the reverse. Reserved slots (per spec §28): **Networking / "Make Multiplayer"** (Fusion-gated `PITECH_HAS_FUSION`; §28.2), **Localization** (`com.unity.localization`-gated `PITECH_HAS_LOCALIZATION`; lift the 4 files from `HealthOn VR/Assets/Scripts/Editor/Localization/`; §28.3), **Analytics** (the Phase B destination), and **Vitals** (the §28.4 patient-digital-twin foundation slot). The deeper step-sync **replication** for Networking is `IScenarioFlowStore` facts - after P1 (P2 the extracted runner consults the store; P3 typed Fusion under LabConsole). The "Make Multiplayer" tool is the *authoring* half; `IScenarioFlowStore` is the *runtime-sync* half (the existing `NetworkedStates` is its backend - spec §28.2).

**Acceptance:** one root token across all four menu systems; every workspace reachable from the Hub in <=2 clicks; all `ExecuteMenuItem` callers resolve; compiles; mojibake gone. (No lab data touched - confirm with a graph-integrity run once WS-0 lands.)
**Gate:** WS-pre (the census is the disposition map). Does **not** depend on WS-0.

---

### WS-0 (= WS A3, THE GATE) - Equivalence harness & test infrastructure *(the safety net - lands before any data-touching work)*

**Goal:** make "behaviour-neutral" measurable. This workstream is itself behaviour-neutral (purely additive) and is the precondition for every later phase.

**Tasks** (findings: PKG-10, PKG-09, SCN-14, SCN-18, SCN-16, CDE-16). Tasks 1-4 + 6-7 are the **P1 net (all EditMode, realistic)**; task 5 is **seed-only (P2-prep)**.

1. Create `Pitech.XR.Scenario.Editor.Tests` (EditMode) referencing Scenario+Core+Quiz+Interactables+Stats. Use the **modern** test-asmdef form (`UNITY_INCLUDE_TESTS` defineConstraint + `nunit.framework.dll` precompiled + `overrideReferences:true` + `UnityEngine/UnityEditor.TestRunner`) - **do not** copy the deprecated `optionalUnityReferences:["TestAssemblies"]` template (PKG-09). (The matching PlayMode asmdef is created in task 5, not now.)
2. EditMode-lock the pure logic on the **unmodified** code: `ConditionsEvaluator.EvalCompare` (Scenario.cs ~381) - all 8 `CompareOp` incl. `Mathf.Approximately` equality and `>0.5f`/`<0.5f` bool encodings; `GroupStep.IsChildRequired*`/`Ensure*` (~468-563).
3. **Scenario graph-integrity test (Proof A - the primary P1 net).** Walk each lab fixture's `[SerializeReference]` graph (recursing into `GroupStep.steps`) and assert: no nulled/Missing references, no dangling routing guids, no Missing `UnityEvent` listeners. Universal invariants (no baseline) + a per-lab snapshot. **Fixtures here are read statically - they do NOT need to run, so use real lab prefabs or lightweight copies.** See §I.0.
4. **Serialized-integrity test (Proof C):** GUID-stability (`Scenario`/`SceneManager` + every MonoScript `.meta` GUID equals a committed constant) **and** open->save serialized-diff (scene object **and** prefab-instance-with-override) per fixture.
5. **Seed (do NOT complete) the PlayMode golden-trace harness - P2-prep, not a P1 gate.** Create `Pitech.XR.Scenario.PlayMode.Tests`, build the `EditorSkipFromGraph`-driven recorder + ordered side-effect log, and prove it on **one** happy-path fixture. The full fixture corpus + PlayMode-in-CI are **P2**. **The deterministic driver already exists** - the Scenario Graph's play-mode Branch / Skip / Outcome buttons call `EditorSkipFromGraph` (the existing manual "scenario test"); the recorder *wraps* that proven hook.
6. **Public-API baseline test** over all `Pitech.XR.*`; **additionally pin** the Core.Editor `FullName` literals (`"Pitech.XR.Quiz.QuizAsset"`, `"Pitech.XR.Stats.StatsConfig"`, `"Pitech.XR.Quiz.QuizUIController"`, `ScenarioGraphWindow` by name) as a "named type resolves" assertion (PKG-04).
7. ContentDelivery additive tests: `RewriteUrl`/`TryParseCcdUrl`, `LaunchContextValidation`, `PublishTransactionStateMachine.CanTransition`; a `PublishReportService` JSON-golden + validation rule-set test (CDE-16).
8. **`DevKit > Evaluate Changes` - the manual gate.** An editor command (menu item + DevKit Hub button) that runs the EditMode suite via `TestRunnerApi` and shows a plain-language verdict ("safe to push" / "lab X: dangling route at g7"). This is the **P1 enforcement model: a developer clicks it before pushing DevKit changes** - there is **no automated CI in P1** (see §I.11). The same suite also exposes a headless entry so the gate can later be wired to a pre-push hook or P2 CI unchanged.
9. **`Export Lab as Test Fixture` tool.** An editor command (`Pi tech > Tools > Export Lab as Test Fixture` + Hub *Maintain* button + `GameObject > Pi tech > Export as Test Fixture` on a selected Scenario) that saves the **Scenario subtree** (Scenario + SceneManager + referenced objects) as a self-contained prefab into `Tests/Fixtures/Scenarios/` carrying its `.meta`, and captures the graph-integrity snapshot baseline. This fills the corpus and turns the scene-less DevKit project into a real test host.

**Testing setup (where the tests run).** The package's tests + a **curated fixture corpus** (3-5 real labs extracted via task 9) live **inside the package**. The **DevKit Unity project** (Unity 6+, package embedded, currently scene-less) becomes the **iteration gate** - a developer runs `Evaluate Changes` there before pushing. Because the *same* suite ships in the package, after a push + package bump you **also run `Evaluate Changes` in the HealthOn project against the real scenes** = the integration check. **One suite, two run-locations.** The package gate must never *depend* on a consumer project.

**Acceptance:** Proofs A/B/C are runnable, all green on unmodified code; Proof C runs each fixture as scene object **and** prefab-instance-with-override; **`DevKit > Evaluate Changes` runs the EditMode net in one click and reports a clear pass/fail verdict**; the golden-trace harness exists and passes on its one seed fixture (its corpus is explicitly P2).
**Gate:** none upstream - this *is* the gate WS-2/WS-3 depend on. The golden trace is **not** required to land any P1 move.

---

### WS-1 (= WS A4) - Formatting / encoding / comment-language normalization *(commit class 1, isolated)*

**Goal:** remove encoding rot and language inconsistency. **Must be physically separate commits** so a real diff is never hidden behind whitespace churn.

**Tasks** (SCN-10, SGW-10, SCN-14-inspectors, DOC-01, SCN-07-hub mojibake, PKG-08, CDE-14):
1. Translate the four Greek comments in `SceneManager.cs` (~1367/1429/1439/2478); normalize the `#else` input-branch indentation to Allman/4-space. *(token stream unchanged)*
2. Translate Greek **comments** and the Greek `[Tooltip]` in `SelectionLists.cs`; Greek comments in `SceneManagerEditor.cs`. *(Tooltip strings are not serialized into assets - neutral. Greek **help-box strings** in `SelectionListsEditor` are user-visible -> P2, excluded here.)*
3. Fix the two `U+FFFD` mojibake comments in `DevkitWidgets.cs`; re-indent the broken object-initializer braces.
4. Reformat all 12 asmdef files to one indent style (4-space) + consistent field set (PKG-08).
5. Fix `AddressablesBuilderWindow` `OnEnable` brace/indentation (CDE-14).

**Excluded (-> after P1):** the `0x85` byte in the `SceneCategories` `[MenuItem]` path - changing a menu-path string is a user-visible string-value change (SCN-03-hub). *(Note: per the editor-visible-string rule in WS-3, the `0x85` fix is promoted into WS-1/WS A4 if grep proves no programmatic reference.)*
**Acceptance:** only comment/whitespace bytes change; Proofs A/C trivially green.
**Gate:** runs through WS-0 proofs as free insurance.

---

### WS-2 (= WS A5) - Dead-code & dead-artifact removal *(commit class 2)*

**Goal:** delete provably-dead code. Every deletion verified zero-caller, private/internal, references no serialized type.

**Tasks** (SCN-02-runner, SCN-03/04-inspectors, CDE-05/06, SCN-12-CD, SCN-07/20-hub, STATS-01):
1. Delete `SceneManager.EvalCompare` (1168-1182) - zero callers; both live sites (1042, 2226) use `ConditionsEvaluator.EvalCompare`.
2. Delete the unreachable `"defaultNextGuid"` ternary (`ScenarioEditor.cs:1041`) and the unused `Styles.Primary` field+initializer.
3. Delete empty `LaunchContextProviders.cs` + `.meta`.
4. Delete the dead `DevkitWidgets` cluster (StatusChips/StatusBar/StatusRibbon/StatusHeader/ProgressBar/ProgressBarPro/Kpi/Tile + `DevBlocksWindow.SmallButton`) after a zero-reference sweep; remove duplicate comment banners and the dead `RebuildLinksFromGraph` forwarder. **Live API excluded:** `Actions` (22 sites), `Card` (21), `Pill` (23), `PillsRow`, `StatusChip`, `TileGrid`, `CardGridTwoCol`.
5. Delete `BuildDefaultPrefabAddressKey`; inline `ComputeAddressKey` at its two private call sites (CDE-05); remove the orphan duplicated `<summary>` above `BuildLocalLabVersionRoot` (CDE-06).
6. Replace the dead `try/catch` in `StatsUIController.Init` (~56) with a direct indexer read - `StatsRuntime` indexer is provably non-throwing (StatsConfig.cs ~123-125).

**RESOLVED (§G, 2026-06-08): DELETE `ScenarioEditorUtil.cs`** (SGW-04), carrying its `.meta` deletion. It is fully dead (zero callers). The "wire `Load` to call `EnsureStableGuids`" alternative is **rejected**.
**Acceptance:** Proof B additions-only (internal/private removals), Proof C zero, Proof A unchanged.
**Gate:** WS-0 (each deletion certified by the net + API baseline).

---

### WS-3 (= WS A6) - Pure file splits & tiny utility extractions *(commit class 2; each `.cs` carries its `.meta`)*

**Goal:** the structural professionalization heart of P1 - real god-class decomposition that is still provably a *move*.

**Tasks** (SCN-08, SCN-06-inspectors, SGW-01/05/19, SCN-07/11/19-CD, ORG-03, STATS-02/03, SCN-04/13-hub, PKG-06):
1. **Data-model split (flagship, highest risk).** Move each Step subclass + `ConditionsEvaluator` out of `Scenario.cs` into `Runtime/Scenario/Steps/<Type>.cs`. **`namespace Pitech.XR.Scenario` unchanged, same asmdef, each carrying its `.meta`.** The `Scenario` MonoBehaviour and `OnValidate` (no-null-strip + `isCompiling` guard) stay untouched. **Runs AFTER WS-0** - unfalsifiable without the serialized-diff harness + GUID test. No `[MovedFrom]` (type identity preserved).
2. **Graph window split + namespace wrap.** Wrap `ScenarioGraphWindow` + nested types + `StepEditWindow` in `namespace Pitech.XR.Scenario.Editor`; split into one file per type. Lift pure helpers (`GroupSummary`, `GetGroupPreferredWidth`, `OutcomeLabel`, AutoLayout BFS) into an `internal` static class; demote no-external-caller `public static` helpers after grep. *(Verified safe: `ScenarioService.OpenGraph` resolves the window by simple `t.Name`, namespace-independent.)*
3. **Inspector split.** Split `ScenarioEditor.cs` into one file per `PropertyDrawer` + a `Styles.cs`, same assembly/namespace, each `.meta` carried; `[CustomPropertyDrawer]`/`[CustomEditor]` bind by type, not file. **Carry the `using Runtime = Pitech.XR.Scenario;` alias (L10) into each split file** or fully-qualify, else compile break.
4. **ContentDelivery extractions.** Extract the **byte-identical** `TrySetAutoStart`/`TryRestart` from Spawner + Bootstrapper into one `internal static` helper (SCN-07). **Scope note:** the `Find*SceneManager*` helpers are only *near*-identical - **exclude them from the verbatim move**; unify separately as a small behaviour-equivalent change proven by Proof A. Move `Timestamp` to its own file (SCN-11). Split public interfaces/enums (`IContentDeliveryService`, `ILaunchContextProvider`, `IContentDeliveryMetadataProvider`, `ContentSourceMode`) into own files (SCN-19) - confirmed none are `[SerializeReference]`.
5. **Features.** Rename non-serialized private Stats fields to `_camelCase` (STATS-02); split `StatsConfig.cs` into `StatsConfig.cs`/`StatEffect.cs`/`StatsRuntime.cs` **only after confirming same namespace+assembly so no `[MovedFrom]` is needed** - `StatEffect` is a serialized authored type; if any `[SerializeReference]` usage is found, demote to **after P1** (STATS-03). Normalize the three `"Pi tech XR/Scenario/..."` `AddComponentMenu` paths on Interactables types to `".../Interactables/..."` (ORG-03). **Excluded:** promoting nested `MetaSelectRelay` to its own file (full type name changes - needs `[MovedFrom]` - after P1).

> **Editor-visible-string consistency note.** Both `AddComponentMenu` paths (here) and the `[MenuItem]` `0x85` mojibake fix change an **editor-UX-visible path string**. Neither fails any proof. The rule: **editor-visible string changes are permitted in P1 as deliberate, single-line, documented edits - NOT bundled into a "pure move" commit - provided a grep first proves the exact path is not referenced programmatically** (`EditorApplication.ExecuteMenuItem`, `Menu.*`, settings, shortcut bindings, automation). Under this rule the `[MenuItem]` `0x85` fix is **promoted into WS-1**; if the grep finds any programmatic reference, that change drops back to after P1.

6. **Reflection -> typed access in `SceneManagerEditor`** (SCN-08-inspectors): `gm.scenario` / `gm.StepIndex` / `gm.Restart()` (public, assembly already referenced). Removes swallowed exceptions + per-repaint reflection; same values read/method invoked. *(Distinct from the **runtime** reflection in ContentDelivery, which is P2.)*
7. **Editor metadata fixes.** `Interactables.Editor.asmdef` `rootNamespace` -> `Pitech.XR.Interactables.Editor` (PKG-06 / SCN-02-interactables); add namespace to `SelectablesManagerEditor` and use `: UnityEditor.Editor` (SCN-04); add `#if UNITY_EDITOR` + namespace `Pitech.XR.Editor.Quiz`->`Pitech.XR.Quiz.Editor` to `QuizDefaultUIPrefabFactory` (SCN-13).

**Acceptance:** every moved `.cs` carries its `.meta`; GUID-stability test green; Proof C zero on all fixtures **as scene object and prefab instance**; Proof B additions-only.
**Gate:** WS-0 (hard dependency - task 1 specifically requires the nested-`GroupStep` prefab-override fixture).

---

### WS-4 (= WS A7) - Documentation, XML docs & package-root professionalization *(additive; zero compiled impact)*

**Goal:** close the docs/metadata gaps and record the intentional exceptions so reviewers stop re-flagging them.

**Tasks** (SCN-17, SGW-09, PKG-11/12/15, CDE-15, PKG-13):
1. Add XML `///` docs to the API-baseline members (`SceneManager` public fields, `StepIndex`, `Restart`, `EditorSkipFromGraph`, selection bridges, `GetOrCreateQuizSession`) and `XRServices`.
2. Add `Debug.LogException(e)` inside the bare `catch {}` blocks in the graph window (SGW-09). **Reclassified - console output is observable behaviour** (it can trip `LogAssert`/`LogAssert.NoUnexpectedReceived`, change `Application.logMessageReceived` handlers, spam device logcat). **Rule: permitted in P1 only as a "diagnostic-output-only" change when no test asserts console silence on that path; otherwise -> after P1.** Default: land it *after* WS-0 confirms no fixture/test asserts console silence on the graph-window paths.
3. Add `README.md`, `CHANGELOG.md`, `LICENSE.md`, `.editorconfig` (encoding the chosen style so PKG-08 stays fixed), `.gitattributes` (normalize EOL; mark `.meta`/`.prefab`/`.asset` as text with explicit eol).
4. Add **metadata-only** `package.json` fields: `license`/`licensesUrl`, `documentationUrl`, `changelogUrl`, `unityRelease` floor, `keywords`. **Do NOT add the `dependencies` block here** (-> after P1; it changes resolution). Record that `Unity.ResourceManager` is **correctly kept**. Do **not** bump `version`. *(Unity 6+: the `unity`-field floor-bump 2022.3->6000.0 + the `dependencies` block are a single after-P1 metadata cutover - spec §28.6.)*
5. Document the intentional Unity-serialization exceptions in a subsystem-notes doc: the public serialized-field surface, `OnValidate` GameObject-rename + the load-bearing no-null-strip/`isCompiling` guard, the editor-only `FindObjectsOfType` legitimacy (CDE-15), and the `link.xml` whole-assembly `preserve="all"` being **safe-but-broad** (PKG-13) - **must not be narrowed without enumerating every reflection-instantiated/`[SerializeReference]` type.**

**Acceptance:** no `.cs` token outside comments changes; API baseline additions-only.
**Gate:** WS-0 (run through the baseline; additive).

---

### WS-5 (= WS A8) - `ISceneRunnerControl` seam *(optional; isolated; AFTER WS-0; not part of the proof work)*

**Goal:** give ContentDelivery (and later LabConsole) a typed handle to the runner so P2 can drop string reflection - landed as one small, separately-reviewed, additive commit once the WS-0 baseline is green. **Not** mixed into any WS-0/WS-2/WS-3 commit.

**Exact contract** (new file `Runtime/Core/ISceneRunnerControl.cs`, assembly `Pitech.XR.Core`, namespace `Pitech.XR.Core`):
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
`SceneManager` adds `: ISceneRunnerControl` and three forwarding members - **no field renamed, nothing made non-public, behaviour identical.** Proof: API-additions-only (B) passes; golden trace (A) unchanged; serialized-diff (C) unchanged (the interface and forwarders are not serialized). **Constraint:** keep it exactly these three members in P1 - do **not** widen it toward flow-store/ledger concepts (that is after P1 and would be premature lock-in).

---

### Explicitly deferred (with reason + compatibility note)

| Item | Phase | Why not P1 | Compatibility note |
|---|---|---|---|
| `AddressablesRemoteUrlRewriter` save/restore of global transform | **after P1** | Fixing it changes observable behaviour (confirmed critical bug) | Capture prior func on install, chain in `TransformLocation`, restore (not null) on uninstall; no-op if current func isn't ours; regression test; route via bridge/host owner |
| `package.json` `dependencies` block + versionDefines range fix + `unity`-field floor-bump | **after P1** | Changes UPM resolution in consumer projects | Reconcile required-vs-optional (TMP/ugui hard; Addressables guarded but hard-referenced); add a with/without-`PITECH_*` compile test; bundle with the Unity 6000.0 floor-bump (spec §28.6) |
| `RunXxx`/`RunXxxGroup` unification (Move A) -> `IStepRunner` registry (Move B) | **P2** | Variants not bit-identical; fails Proof A by construction | Each a separate commit, proven byte-equal against the WS-0 golden trace + side-effect log first |
| Editor undo-correctness (`RecordObject->RegisterCompleteObjectUndo`; direct-field->`SerializedObject`; gate the ungated null-delete) | **after P1** | Changes undo-stack shape / prefab-override behaviour | Validate with prefab-instance fixtures; the ungated null-delete (SCN-12) is **elevated** - can lose shipped-lab data |
| 7-way route-schema table (SGW-02); shared per-step drawers (SGW-07); `JsonUtility` deep-copy fidelity (SGW-16) | **after P1** | Variants diverge today; reproducing byte-for-byte is itself a behaviour risk | Lock with fixture round-trips (Proof C) before merging |
| Runtime reflection/`Find` removal (Interactables INT-01/02; ContentDelivery SCN-04/05) | **P2** | Changes discovery/timing/caching | Introduce `ISceneRunnerControl` in Core; cache VR/Meta determination |
| Core.Editor layering inversion + `AddressablesBuilderWindow` relocation | **P2** | Naive move = circular asmdef ref (will not compile) | First extract shared editor-UI primitives to a lower assembly; then move the window **carrying its `.meta`** |
| HealthOn adapter de-coupling (CDE-02) | **P2** | Changes resolution for labs relying on the implicit fallback | Gate behind a migration that sets `adapterTypeName` on existing configs |
| Stray root prefab relocation (PKG-05) | **P2** | Referenced by GUID `a0032abe...` | Carry `.meta`; verify no scene/variant references it; prove Proof C |
| `link.xml` narrowing (PKG-13) | **P2** | Size optimization, not correctness; safe today | Enumerate every reflection-instantiated Step type before narrowing |
| `0x85` byte in `[MenuItem]` path (SCN-03-hub) | **WS-1/WS-6 (P1)** | Editor-visible string, *not* lab data; grep-verified no programmatic reference | **Promoted into P1** per the editor-visible-string rule - strip the byte, save UTF-8 |

---

## F. P1 exit checklist *(measurable)*

**Census & editor restructure (WS-pre/A1, WS-6/A2 - land first):**
- [ ] Census (Appendix A) confirmed current; every WS-1...WS-6 edit maps to a census row.
- [ ] Single menu root `Pi tech` across top-bar / Add Component / Create Asset / GameObject; ` XR` dropped from all `[AddComponentMenu]`; ORG-03 applied (Selectable* -> Interactables); `0x85` mojibake stripped.
- [ ] All `DocsPage` `ExecuteMenuItem` callers resolve; package compiles; no lab data touched.
- [ ] DevKit Hub rebuilt as the cockpit (task-first pages; launch tiles; repair tools + `Evaluate Changes` surfaced; "Add Scenario to Scene" added).
- [ ] Future-feature slots reserved (gated Networking/Make-Multiplayer + Localization + Analytics + Vitals modules + Hub pages) - structure only, logic post-P1.

**Safety net (WS-0/A3) - the P1 net is all EditMode:**
- [ ] `Pitech.XR.Scenario.Editor.Tests` exists in the **modern** asmdef form; discoverable in Test Runner.
- [ ] `Export Lab as Test Fixture` tool ships; 3-5 real labs extracted into `Tests/Fixtures/`; the DevKit project runs the net green against them.
- [ ] **Graph-integrity test green (Proof A):** refs resolve, no dangling routes, no Missing `UnityEvent` listeners; per-lab snapshots committed.
- [ ] EditMode pure-logic tests cover `EvalCompare` (all 8 ops + `Approximately` + bool encodings) and `GroupStep.Ensure*`/`IsChildRequired*`; all green.
- [ ] 3-5 lab fixtures committed (real or trimmed copies) - read statically (no play mode required).
- [ ] GUID-stability test green (every MonoScript `.meta`).
- [ ] Serialized-diff green per fixture (scene object **and** prefab-instance-with-override).
- [ ] Public-API baseline test green **and** the Core.Editor `FullName` literals all resolve.
- [ ] ContentDelivery additive tests green.
- [ ] *(P2-prep, not a P1 gate)* golden-trace harness exists and passes on **one** seed fixture.

**Gate (P1 = manual in-editor; server CI deferred to P2):**
- [ ] `DevKit > Evaluate Changes` ships (menu item + Hub button); one click runs the EditMode net and shows a pass/fail verdict. **This is the P1 gate.**
- [ ] `.editorconfig` + `.gitattributes` committed; the `Evaluate Changes` run flags violations.
- [ ] A headless entry exists (`-runTests` / a static `RunAll`) so the same gate can later attach to a pre-push hook or P2 CI **unchanged**.
- [ ] **Dependency-truth REPORT** generated (P1): for each `PITECH_*` define, record asmdef hard-reference vs `package.json` vs actual `#if`/un-guarded source usage. *(The real with/without-Addressables compile matrix moves to after P1, with the `dependencies` + Unity-floor cutover.)*
- [ ] *(Deferred to P2)* server CI (GameCI + Unity license) running EditMode + the PlayMode golden trace on every PR.

**Reorganization (proven neutral):**
- [ ] WS-1 formatting/encoding landed as **separate** commits; only comment/whitespace bytes changed.
- [ ] WS-2 dead code deleted; every deletion zero-caller; Proofs A/B/C green; `ScenarioEditorUtil.cs` **deleted** with its `.meta`.
- [ ] WS-3 splits landed; every moved `.cs` carries its `.meta`; Proof C zero as **scene object and prefab instance**; no `[MovedFrom]` required.
- [ ] `SceneManagerEditor` reflection replaced with typed access; behaviour identical.
- [ ] `rootNamespace` / global-namespace editor fixes applied.

**Docs / metadata (WS-4):**
- [ ] XML docs on the API-baseline members + `XRServices`; baseline still additions-only.
- [ ] `README.md` / `CHANGELOG.md` / `LICENSE.md` present at package root.
- [ ] `package.json` metadata fields added; **no** `dependencies` block added; **no** version bump.
- [ ] Subsystem-notes doc records the intentional Unity-serialization exceptions and the `link.xml` constraint.

**Negative gates (must remain TRUE):**
- [ ] `OnValidate` no-null-strip + `isCompiling` guard untouched.
- [ ] No `RunXxx`/`RunXxxGroup` unification, no dispatch-registry change, no `RecordObject->RegisterCompleteObjectUndo`, no direct-field->`SerializedObject` routing change, no runtime reflection/`Find` removal, no rewriter restore fix, no `dependencies` resolution change - all deferred.
- [ ] No serialized public field renamed/retyped; no `[SerializeReference]` type moved namespace/assembly.

---

## G. Open decisions for the human

**RESOLVED 2026-06-08 (Stergios):** #1 keep options open - document coupling only, keep the seam (no decouple). #2 take default (document-only, one switchboard, adapter home -> after P1). #3 approve `Pitech.XR.Core.Editor.UI` leaf extraction in **P2**; P1 records the constraint. #4 no version bump in P1. #5 **4-space + LF via `.gitattributes`** (initial LF renormalization is its own isolated commit) + `.editorconfig`; enforced for now by the **manual `DevKit > Evaluate Changes` gate** (no server CI in P1). #6 **Addressables is a required dependency** - after P1 adds `com.unity.addressables`/`com.unity.textmeshpro`/`com.unity.ugui` to `dependencies` and removes the dead `#else` branches in one commit (bundled with the Unity 6000.0 floor-bump, spec §28.6); P1 documents the posture. #7 **delete** `ScenarioEditorUtil` in WS-2.

Original questions + recommended defaults retained for traceability: (1) HealthOn adapter placement (CDE-02) -> document in P1, move in P2 behind the config migration. (2) NetworkedStates boundary -> document-only in P1; never introduce a second scene-wide state manager. (3) `AddressablesBuilderWindow` relocation (CDE-01) -> P2; P1 records the constraint. (4) Version bump -> not in P1. (5) EOL/formatting -> 4-space via `.editorconfig`. (6) `dependencies` reconciliation -> after-P1 decision; declare TMP/ugui hard, resolve Addressables optionality with the compile-matrix test. (7) `ScenarioEditorUtil` -> delete in WS-2.

---

## H. P1 additions for easier v1.0 implementation (behaviour-neutral accelerators)

Each must still pass all three equivalence proofs (additive/neutral) or it is not P1. Priority order.

1. **One gate, two doors - `Evaluate Changes` now, server CI for free later** *(highest leverage)*. Build the EditMode net once and expose it through (a) `DevKit > Evaluate Changes` (the P1 manual gate) and (b) a headless entry running the *same* suite. The leverage is the single-entry-point design, not standing up CI now.
2. **Over-build the golden-trace fixture corpus now as the P2 acceptance suite.** Beyond the 5 P1 fixtures: one per step Kind, one per `GroupStep` completion mode (all 6), plus the divergent paths (`RunQuestion` debounce vs group first-click-wins; Selection `allowedWrong`/zero-correct; MultiCondition branches). Pure P2 insurance; additive.
3. **Characterization tests on the reflection/`Find` paths P2 will delete** (ContentDelivery `autoStart`/`Restart`/`StepIndex` string-reflection + `FindObjectsOfType`). Pin current observable behaviour so the P2 `ISceneRunnerControl` swap is provable.
4. **Write the "P2 extraction playbook" doc while knowledge is fresh.** Capture the runner divergences + invariants (`_groupExitBranchResolved` routing, silent type-switch fallthrough, `GroupStep` branchGuid contract).
5. **`ISceneRunnerControl` seam - its own isolated ticket (WS-5/A8)** - the one future-contract allowed in P1, **NOT** mixed into the WS-0 proof work.
6. **Stable identifier constants for the analytics/step-fact vocabulary** (`const`s only, no emission): `step.completed` etc. + the `scenario.step.<guid>.done` key format. One source of truth for the Phase B / after-P1 ledger work; prevents stringly-typed drift. Keep minimal.
7. **Elevate the read-only localization-candidate report.** P4 accelerator; behaviour-neutral because it only reports TMP strings (no string-table change, no mutation).

**Traps - do NOT add "to prepare":**
- Pre-baking `IScenarioFlowStore` / `LabEventLedger` / analytics-envelope **public types** you'll redesign in after P1 (the #6 constants are the only exception).
- Editor-only step-display-name/section **serialized fields** (serialized-diff change -> after P1).
- Emitting any events/facts/lifecycle hooks (behaviour -> after P1).

---

## I. P1 Implementation Pack (WS-0 / WS A3 concretized)

Turns WS-0 from a spec into a ticket set. All paths are package-relative (`com.pitech.xr.devkit/`). **Prerequisite:** a UPM package's tests only run when the package is embedded in a Unity project. Stand up (or designate) a minimal host Unity project (**Unity 6+**, per spec §28.6) that references `com.pitech.xr.devkit`. The HealthOn VR/AR consumer project can serve, or a dedicated `DevKitTestHost` project.

**Scope marker:** §I.0, I.1(EditMode), I.2, I.3, I.6, I.7, I.8 are the **P1 net**. §I.4 and §I.5 (the PlayMode golden trace) are **P2-prep - seeded in P1, completed in P2; not a P1 gate.**

### I.0 Scenario graph-integrity test (Proof A - the primary P1 net)

The single most important P1 test, and the cheapest. Pure EditMode, read-only, runs against **real lab prefabs** (no play mode, no runnable fixtures). File: `Tests/Editor/ScenarioGraphIntegrityTests.cs`.

For each lab asset: load it, get the `Scenario`, and **walk `steps` recursively (into `GroupStep.steps`)** collecting the full set of step `guid`s. Then assert:
- **Invariants (no baseline - always true of a valid lab):**
  - no `null` entry in any `[SerializeReference]` `steps` list (a null = a missing-script break);
  - every step `guid` non-empty and unique;
  - every routing guid resolves: for each step gather its outgoing guids by type - `nextGuid` (Timeline/CueCards/Insert/Event/Group), `correctNextGuid`/`wrongNextGuid` (Selection), `outcomes[].nextGuid` (Conditions/MiniQuiz), `defaultNextGuid` + `multiConditionBranches[].nextGuid` (MiniQuiz/Group), `specificStepGuid`, `childRequirements[].guid` - and assert each is `""` (linear fall-through) **or** a member of the collected guid set (**no dangling route**);
  - every `UnityEvent` (`Choice.onSelected`, `MiniQuizChoice.onSelected`, `SelectionStep.onCorrect`/`onWrong`, `EventStep.onEnter`) has, for each persistent listener `i`, `GetPersistentTarget(i) != null` and non-empty `GetPersistentMethodName(i)` (**no "Missing" wiring**).
- **Snapshot (small per-lab baseline JSON):** committed fingerprint = ordered `[(guid, Kind, [outgoing guids])]` + per-event `[(targetTypeName, method)]`. Re-extract after a change and assert equal. Regenerate only via an explicit `--regen`, reviewed as a deliberate change.

Catches exactly the P1 failure class - broken refs, dangling routes, dead events, dropped steps. Implement against **real labs** if the test host has them; otherwise commit a few representative lab prefabs (carrying `.meta`) under `Tests/Fixtures/Scenarios/`.

### I.1 Test assembly layout (EditMode primary; PlayMode seed = P2-prep)

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
`Tests/PlayMode/Pitech.XR.Scenario.PlayMode.Tests.asmdef` *(created in WS-0 task 5 as a seed; the golden-trace corpus that fills it is P2)*
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
Notes: `UNITY_INCLUDE_TESTS` keeps both out of player builds; `overrideReferences:true` is required for `precompiledReferences` to bind nunit; the PlayMode asmdef must **not** reference `UnityEditor.TestRunner`. The golden trace is an in-editor PlayMode gate, run via `-testPlatform PlayMode` in batchmode. **Do not** copy the existing `Tests/Editor/Pitech.XR.ContentDelivery.Editor.Tests.asmdef` template - it uses the deprecated `optionalUnityReferences:["TestAssemblies"]`/`testAssemblies:true` form (PKG-09); migrate it to this modern form too.

### I.2 Folder / file layout
```
Tests/
  Editor/        Pitech.XR.Scenario.Editor.Tests.asmdef + EditMode tests (the P1 net)
    ScenarioGraphIntegrityTests.cs     (Proof A - refs/routes/events; PRIMARY P1 net, §I.0)
    ConditionsEvaluatorTests.cs        (8 CompareOp ops + Approximately + bool encodings)
    GroupStepRequirementTests.cs       (Ensure*/IsChildRequired*)
    ScriptGuidStabilityTests.cs
    PublicApiBaselineTests.cs
    CoreEditorTypeLiteralTests.cs
    SerializedFixtureRoundTripTests.cs
    ContentDeliveryAdditiveTests.cs    (RewriteUrl/validation/state-machine/report-JSON)
  PlayMode/      Pitech.XR.Scenario.PlayMode.Tests.asmdef  (SEED only in P1; corpus = P2)
    GoldenTraceTests.cs                ([UnityTest]; one seed fixture in P1, rest P2)
    GoldenTraceRecorder.cs             (shared harness)
  Fixtures/Scenarios/                  *.prefab (+ .meta) - real labs or trimmed copies
  Baseline/
    PublicApi.Pitech.XR.txt
    ScriptGuids.json
    CoreEditorTypeLiterals.txt
    GraphSnapshots/<lab>.graph.json    (Proof A per-lab snapshot)
  Golden/                              <fixture>.trace.json  (P2)
```

### I.3 Fixture corpus (each prefab = a `Scenario`+`SceneManager` hierarchy)
In P1 the fixtures are read **statically** - they do **not** have to run, so they can be real labs or trimmed copies. P1-minimum (3-5): `linear_timeline_cuecards_event`, `branching_question`, `group_specificchild_question` (locks SCN-11 shared-field routing - highest-risk path), optionally `miniquiz_selection` (incl. SCN-18 "count met / zero correct / within `allowedWrong` -> CORRECT") and `conditions_component`. **P2 (when these must become *runnable* for the golden trace):** add one per remaining step Kind, one per `GroupStep.CompleteWhen` mode (all 6), plus `question_debounce` vs `question_group_firstclick` to pin the SCN-03 divergence before P2 unifies them.

### I.4 Golden-trace JSON schema (v1) - *P2-prep (seed only in P1)*
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
**Determinism rules (or byte-compare is useless):** stable key order, `InvariantCulture`, floats formatted `"R"`, **no** timestamps / frame numbers / object instance ids, preserve emission order (never sort `trace`/`sideEffects`), line endings LF, trailing newline. The test serializes the recorded run with these rules and `Assert.AreEqual(File.ReadAllText(golden), produced)`. Regenerate goldens only via an explicit `--regen` switch.

### I.5 Golden-trace harness (`GoldenTraceRecorder`) - *P2-prep (seed on one fixture in P1)*
1. `#if UNITY_EDITOR` load the fixture prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`, `Object.Instantiate` it into the play-mode scene.
2. Before `Restart()`, walk the `Scenario.steps` graph and add a recording listener to every `UnityEvent` (`Choice.onSelected`, `SelectionStep.onCorrect/onWrong`, `EventStep.onEnter`, `MiniQuizChoice.onSelected`) and subscribe a stat-mutation probe (wrap/observe `StatsRuntime`); poll `SceneManager.StepIndex` each frame to emit a `trace` row on every change (`from`->`to`, current step `guid`/`Kind`, resolved `branchGuid`).
3. Drive deterministically with the fixture's committed `driver` list via `sceneManager.EditorSkipFromGraph(stepGuid, branchIndex)` - no real pointer input, no timing dependence.
4. Run until `StepIndex == -1` (finished) or a step cap; serialize per I.4; compare.

### I.6 Serialized-diff method (Proof C)
Per fixture prefab `P`: (a) `string b0 = File.ReadAllText(P)`; (b) `AssetDatabase.ForceReserializeAssets(new[]{P})` (or load -> `EditorUtility.SetDirty` -> `AssetDatabase.SaveAssets`); (c) `string b1 = File.ReadAllText(P)`; (d) `Assert.AreEqual(b0, b1)`. If Unity normalizes formatting on first reserialize, capture that normalized text **once** as the committed baseline and assert subsequent reserializes equal it. **Prefab-instance-with-override variant:** new temp scene -> instantiate `P` -> set one override (e.g. `title`) -> `PrefabUtility.RecordPrefabInstancePropertyModifications` -> save scene -> assert the `Scenario` block has no dropped `steps` entries, no churned `managedReferences` ids, no changed `m_Script` GUID. CI backstop: `git diff --exit-code -- Tests/Fixtures` after a `ForceReserializeAssets` pass.

### I.7 GUID-stability (`ScriptGuids.json`)
Pins the **MonoScript GUIDs** of every type a prefab/scene references by `m_Script`: `Scenario`, `SceneManager`, `QuizUIController`, `QuizResultsUIController`, `QuizAsset`, `StatsUIController`, `StatsConfig`, `SelectablesManager`, `SelectionLists`, `SelectableTarget`, `ContentDeliverySpawner`, `ContentDeliveryStatusOverlay`. Format `{ "Pitech.XR.Scenario.SceneManager": "<32hex>", ... }`; test resolves each `MonoScript`'s GUID and asserts equality. **Split rule (load-bearing):** plain `[Serializable]` Step classes are referenced by type-string (ns+asm+name), **not** GUID - so when splitting `Scenario.cs`, the file retaining the `Scenario` MonoBehaviour **keeps `Scenario.cs.meta`'s GUID**, while moved step classes get fresh `.meta` GUIDs (harmless). The test only guards the MonoScript set; it is the trip-wire for "a refactor regenerated a script GUID and nulled every shipped prefab reference."

### I.8 Public-API baseline (additions-only)
`Tests/Baseline/PublicApi.Pitech.XR.txt`: reflect every loaded assembly whose name starts `Pitech.XR.`, enumerate public (and protected-on-non-sealed) types/members, format one stable line each (`Namespace.Type::Member(paramTypes)->returnType`), sort `Ordinal`, write. Test regenerates the live surface and asserts **every baseline line is still present** (removals fail; additions allowed). `Tests/Baseline/CoreEditorTypeLiterals.txt`: the inventory of Core.Editor string literals (`Pitech.XR.Quiz.QuizAsset`, `Pitech.XR.Stats.StatsConfig`, `Pitech.XR.Quiz.QuizUIController`, `Pitech.XR.Quiz.QuizResultsUIController`, `Pitech.XR.Stats.StatsUIController`, `Pitech.XR.Scenario.SceneManager`, `Pitech.XR.Scenario.Scenario`, `ScenarioGraphWindow`); test asserts each still resolves. This catches a namespace move the ordinary API baseline would miss.

### I.9 Headless entry (the *other* door - for the optional hook + P2 CI, NOT the P1 gate)
```bash
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform EditMode  -testResults "Logs/editmode.xml"  -logFile "Logs/editmode.log"
# PlayMode (golden trace) - P2 only:
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform PlayMode  -testResults "Logs/playmode.xml"  -logFile "Logs/playmode.log"
```
`-runTests` auto-quits - do **not** add `-quit` (it races the runner). Exit code 0 = all passed; parse the NUnit XML. The EditMode line is what a future hook/CI would call; the PlayMode line is **P2**.

### I.10 Gate model - manual in-editor now, server CI at P2
**P1 (now):** the gate is human-run and in-editor - `DevKit > Evaluate Changes` (§I.11). No GameCI, no Unity license on a runner. `.editorconfig`/`.gitattributes` committed so style is *encoded*.
**Optional hardening (any time, still local):** a shared pre-push hook (`core.hooksPath` -> `.githooks/pre-push`) that calls §I.9's EditMode line and blocks the push on red.
**P2:** server CI (GameCI + Unity license) runs §I.9's EditMode **and** PlayMode lines on every PR.

### I.11 `DevKit > Evaluate Changes` - the manual gate (P1 deliverable)
- **Entry points:** a `[MenuItem("DevKit/Evaluate Changes")]` **and** an "Evaluate Changes" button on a DevKit Hub page (same handler). File: `Editor/Core.Editor/Tools/EvaluateChanges.cs`.
- **What it runs:** the EditMode suite via `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi` -> `Execute(new ExecutionSettings(new Filter{ testMode = TestMode.EditMode, assemblyNames = new[]{"Pitech.XR.Scenario.Editor.Tests"} }))` - and registers a callback (`ICallbacks`/`RegisterCallbacks`) to collect results. (PlayMode excluded in P1.)
- **Verdict UI:** green -> "Evaluate Changes: N checks passed - safe to push." red -> list each failure as a plain sentence sourced from the test's message, e.g. "lab `cardiac_triage`: routing guid `g7` on step `g6` points to no existing step (dangling route)."
- **Shared core:** put the actual run logic in a static `DevKitChecks.RunEditModeGate()` that both the button and a headless `static int RunAll()` call - **one code path, two doors**.
- **Why not just the Test Runner window:** it runs *exactly* the right suite, gives a plain-language verdict a non-expert can act on, and is discoverable. Friction is what kills a manual gate; this removes it.

---

## Appendix A - DevKit Inventory & Disposition Census (WS-pre / WS A1)

A census comes first because every later workstream edits a *shared, cross-referenced* surface - menu paths are reflected by string from `DocsPage`/`ContentDeliverySpawner`, GUIDs are pinned by lab prefabs, and types move between files - so until every surface is enumerated with its exact `file:line`, disposition, and owning workstream, no edit can be proven safe.

**Menu-root unification.** The package mixes **three** menu-root prefixes; the locked target is a single root token **`Pi tech`**:

| Current prefix | Where it appears today | Target (WS-6/A2) |
|---|---|---|
| `Pi tech/` | Top-bar `[MenuItem]` windows/commands/tools | **Keep token.** Re-group task-first; fix the `0x85` mojibake byte in the Scene Categories path. |
| `Pi tech XR/` | **All** runtime `[AddComponentMenu]` paths | **Drop the ` XR` suffix** -> `Pi tech/<Module>/...`. |
| `GameObject/Pi tech/` | `Make Grabbable` GameObject context entry | **Keep**. |
| `Pi tech/` (CreateAssetMenu) | Stats Config, Quiz Asset, Dev Blocks, Content Delivery configs | **Keep token**; task-first grouping. |

Two structural corrections: **ORG-03** - `SelectableTarget`, `SelectablesManager`, `MetaSelectRelay` move Scenario -> Interactables group. **Caller fidelity** - any rename of `Pi tech/Scenario Graph`, `Pi tech/Dev Blocks`, `Pi tech/DevKit` **must** update the `EditorApplication.ExecuteMenuItem` callers in `DocsPage.cs` (5 calls); leave Meta's `GameObject/Interaction SDK/Add Grab Interaction` untouched.

### (A) Top-bar menu surfaces - windows / commands / wizards

| What | Where (file:line) | Disposition (+ WS) |
|---|---|---|
| DevkitHubWindow `[MenuItem("Pi tech/DevKit")]` | `Editor/Core.Editor/Hub/DevkitHubWindow.cs:27` | **rename** -> DevKit Hub home; launch tiles + repair tools + Evaluate Changes + "Add Scenario to Scene". `DocsPage` caller tracks path. *(WS-6)* |
| ScenarioGraphWindow `[MenuItem("Pi tech/Scenario Graph")]` | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:147` | **split** (6 types) + namespace wrap; Hub tile; `DocsPage` callers (42/77). *(WS-3, WS-6)* |
| StepEditWindow (no `[MenuItem]`) | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:4287` | **split** (part of the 6-type split). *(WS-3)* |
| DevBlocksWindow `[MenuItem("Pi tech/Dev Blocks")]` | `Editor/Core.Editor/Tools/DevBlocksWindow.cs:41` | **keep** + Hub tile; `DocsPage` caller (151); dead widgets pruned. *(WS-2, WS-6)* |
| AddressablesBuilderWindow `[MenuItem("Pi tech/Addressables Builder")]` | `Editor/Core.Editor/Tools/AddressablesBuilderWindow.cs:59` | **keep** + Hub tile. **Deferred:** relocation to `...Core.Editor.UI` leaf asmdef -> P2. *(WS-6)* |
| SceneCategoriesWindow `[MenuItem("Pi tech/Scene/...")]` | `Editor/Core.Editor/Tools/SceneCategoriesWindow.cs:16` | **rename** (strip `0x85`) + Hub tile. *(WS-1, WS-6)* |
| Copy Default Quiz UI Prefabs `[MenuItem("Pi tech/Quiz/...")]` | `Editor/Quiz.Editor/QuizDefaultUIPrefabFactory.cs:10` | **keep**. *(WS-6)* |

### (B) GameObject + Add Component + Create Asset surfaces

| What | Where (file:line) | Disposition (+ WS) |
|---|---|---|
| Make Grabbable `[MenuItem("GameObject/Pi tech/Make Grabbable")]` | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:19` | **keep**. Meta's `ExecuteMenuItem` left as-is. *(WS-6)* |
| MakeGrabbableWindow (ShowUtility) | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:35` | **keep**. *(WS-6)* |
| StatsConfig `[CreateAssetMenu("Pi tech/Stats Config")]` | `Runtime/Stats/StatsConfig.cs:7` | **keep**. *(WS-6)* |
| QuizAsset `[CreateAssetMenu("Pi tech/Quiz Asset")]` | `Runtime/Quiz/QuizAsset.cs:7` | **keep**. *(WS-6)* |
| DevBlockItem `[CreateAssetMenu("Pi tech/Dev Blocks/Dev Block")]` | `Editor/Core.Editor/DevBlocks/DevBlockItem.cs:12` | **keep**. *(WS-6)* |
| AddressablesModuleConfig `[CreateAssetMenu("Pi tech/Content Delivery/...")]` | `Runtime/ContentDelivery/AddressablesModuleConfig.cs:29` | **keep**. **Deferred:** rewriter fix -> after P1. *(WS-6)* |
| AddressablesBuildCatalog `[CreateAssetMenu("Pi tech/Content Delivery/...")]` | `Runtime/ContentDelivery/AddressablesBuildCatalog.cs:28` | **keep**. *(WS-6)* |

### (C) DevKit Hub - window + pages + services

DashboardPage `:8`, GuidedSetupPage `:15`, DocsPage `:8` (**CRITICAL caller** - `Pi tech/Scenario Graph` 42/77, `Pi tech/Dev Blocks` 151, `Pi tech/DevKit` 170), SettingsPage `:11`, IDevkitPage `:6`, DevkitContext `:1`, DevkitTheme `:1` (dead `Styles.Primary` removed - WS-2), DevkitWidgets `:1` (unused helpers deleted - WS-2), GuidedSetupService, ProjectHealthService, ProjectSetupService, QuizService, ScenarioService (home for "Add Scenario to Scene"), SceneCategoriesService, SceneManagerService, StatsService - all `Editor/Core.Editor/...` **keep** (cockpit rebuild surfaces, no behaviour change). *(WS-6; some WS-0 test targets.)*

### (D) Editor inspectors, repair tools & GUID services

| What | Where (file:line) | Disposition (+ WS) |
|---|---|---|
| ScenarioEditor `[CustomEditor(typeof(Scenario))]` (11 drawers + Styles) | `Editor/Scenario.Editor/ScenarioEditor.cs:12` | **split** (per-file drawers + Styles.cs). Undo-correctness -> after P1. *(WS-3)* |
| SceneManagerEditor `[CustomEditor(typeof(SceneManager), true)]` | `Editor/Scenario.Editor/SceneManagerEditor.cs:14` | **rename** (reflection->typed access). Deeper reflection/Find removal -> P2. *(WS-3)* |
| StatsUIControllerEditor | `Editor/Stats.Editor/StatsUIControllerEditor.cs:10` | **keep** (dead try/catch removed - WS-2). |
| StatsConfigEditor | `Editor/Stats.Editor/StatsConfigEditor.cs:10` | **keep** (formatting/namespace). *(WS-1, WS-3)* |
| QuizAssetEditor | `Editor/Quiz.Editor/QuizAssetEditor.cs:11` | **keep** (formatting). *(WS-1)* |
| SelectablesManagerEditor | `Editor/Interactables.Editor/SelectablesManagerEditor.cs:7` | **rename** (global-namespace fix -> `Pitech.XR.Interactables.Editor`). *(WS-3)* |
| SelectionListsEditor | `Editor/Interactables.Editor/SelectionListsEditor.cs:9` | **keep** (formatting/namespace). *(WS-1, WS-3)* |
| ContentDeliverySpawnerEditor | `Editor/ContentDelivery.Editor/ContentDeliverySpawnerEditor.cs:7` | **keep** (formatting). *(WS-1)* |
| Fix Missing DevKit Script References `[MenuItem("Pi tech/Tools/...", 502)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:20` | **keep**, surfaced in Hub. Undo-correctness -> after P1. *(WS-6)* |
| Repair DevKit script GUIDs `[MenuItem("Pi tech/Tools/...", 503)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:75` | **keep**, surfaced in Hub. *(WS-6)* |
| DevKitYamlScriptGuidRepair (static GUID rewriter) | `Editor/Scenario.Editor/DevKitYamlScriptGuidRepair.cs:20` | **keep** (GUID-repair test; comment/EOL only). *(WS-0, WS-1)* |
| ScenarioEditorUtil (internal step-GUID helper) | `Editor/Scenario.Editor/ScenarioEditorUtil.cs:9` | **delete** (provably-dead). *(WS-2)* |

### (E) Runtime components - `[AddComponentMenu]` (token unification `Pi tech XR/` -> `Pi tech/`)

| What | Where (file:line) | Disposition (+ WS) |
|---|---|---|
| Scenario `Pi tech XR/Scenario/Scenario` | `Runtime/Scenario/Scenario.cs:569` | **rename** -> `Pi tech/Scenario/Scenario`; step types extracted to `Steps/<Type>.cs`. *(WS-3, WS-6)* |
| SceneManager `Pi tech XR/Scenario/Scene Manager` | `Runtime/Scenario/SceneManager.cs:20` | **rename**; delete dead `EvalCompare`; implements optional `ISceneRunnerControl`. Reflection/Find removal -> P2. *(WS-2, WS-5, WS-6)* |
| SelectableTarget `Pi tech XR/Scenario/Selectable Target` *(misplaced)* | `Runtime/Interactables/SelectableTarget.cs:18` | **move** ORG-03 -> `Pi tech/Interactables/Selectable Target`. *(WS-3, WS-6)* |
| SelectablesManager `Pi tech XR/Scenario/Selectables Manager (...)` *(misplaced)* | `Runtime/Interactables/SelectablesManager.cs:8` | **move** -> `Pi tech/Interactables/Selectables Manager (Meta VR Ready + AR Safe)`. *(WS-3, WS-6)* |
| MetaSelectRelay `Pi tech XR/Scenario/Meta Select Relay (optional)` *(misplaced)* | `Runtime/Interactables/SelectablesManager.cs:327` | **move** -> `Pi tech/Interactables/Meta Select Relay (optional)`. *(WS-3, WS-6)* |
| SelectionLists `Pi tech XR/Interactables/Selection Lists (Controller)` | `Runtime/Interactables/SelectionLists.cs:81` | **rename** -> `Pi tech/Interactables/...`. *(WS-6)* |
| QuizUIController `Pi tech XR/Quiz/Quiz UI Controller` | `Runtime/Quiz/QuizUIController.cs:9` | **rename** -> `Pi tech/Quiz/...`. *(WS-6)* |
| QuizResultsUIController `Pi tech XR/Quiz/Quiz Results UI Controller` | `Runtime/Quiz/QuizResultsUIController.cs:8` | **rename** -> `Pi tech/Quiz/...`. *(WS-6)* |
| AddressablesBootstrapper `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/AddressablesBootstrapper.cs:7` | **rename** -> `Pi tech/Content Delivery/...`. *(WS-6)* |
| AttemptReconciliationBridge `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/AttemptReconciliationBridge.cs:9` | **rename**. *(WS-6)* |
| BridgeLaunchContextReceiver `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/BridgeLaunchContextReceiver.cs:11` | **rename**. *(WS-6)* |
| SerializedLaunchContextProvider `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/SerializedLaunchContextProvider.cs:8` | **rename**. *(WS-6)* |
| LaunchContextReporter `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/LaunchContextReporter.cs:34` | **rename**. *(WS-6)* |
| ContentDeliveryStatusOverlay `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/ContentDeliveryStatusOverlay.cs:14` | **rename**. *(WS-6)* |
| ContentDeliverySpawner `Pi tech XR/Content Delivery/...` | `Runtime/ContentDelivery/ContentDeliverySpawner.cs:33` | **rename**. *(WS-6)* |
| RuntimeTelemetryAdapter `Pi tech XR/Analytics/...` | `Runtime/ContentDelivery/Analytics/RuntimeTelemetryAdapter.cs:80` | **rename** -> `Pi tech/Analytics/...`. *(WS-6)* |
| TelemetryAutoWirer `Pi tech XR/Analytics/...` | `Runtime/ContentDelivery/Analytics/TelemetryAutoWirer.cs:13` | **rename**. *(WS-6)* |
| LaunchContextProviders (placeholder, no types) | `Runtime/ContentDelivery/LaunchContextProviders.cs:1` | **delete** (named dead-code target; carry `.meta` removal). *(WS-2)* |

> **Non-menu runtime services (kept, P1):** `AddressablesService`, `AddressablesBuildService`, `AddressablesValidationService`, `PublishReportService`, `AddressablesAdapterResolver`, `ContentDeliveryCapability` - all `Editor/ContentDelivery.Editor/...`, **keep** (some are WS-0 test targets). **`HealthOnAddressablesAdapter`** (`.../Services/HealthOnAddressablesAdapter.cs:1`) - **defer** (adapter de-coupling -> P2).

### (F) The `[SerializeReference]` data model (`Scenario.cs`)

All step types **split** under WS-3 into `Runtime/Scenario/Steps/<Type>.cs` - **same `namespace Pitech.XR.Scenario`, same asmdef, each carrying its `.meta`** - covered by the WS-0 serialized/graph-integrity proofs. Behaviour-neutral.

| Type | Where (file:line) | -> |
|---|---|---|
| Step (abstract base, guid/graphPos/Kind) | `Runtime/Scenario/Scenario.cs:14` | `Steps/Step.cs` *(WS-3, WS-0)* |
| TimelineStep | `Runtime/Scenario/Scenario.cs:23` | `Steps/TimelineStep.cs` *(WS-3)* |
| CueCardsStep (+ AdvanceMode) | `Runtime/Scenario/Scenario.cs:37` | `Steps/CueCardsStep.cs` *(WS-3)* |
| QuestionStep (+ Choice) | `Runtime/Scenario/Scenario.cs:104` | `Steps/QuestionStep.cs` *(WS-3)* |
| MiniQuizStep (+ Choice/Question/Outcome/CompleteMode) | `Runtime/Scenario/Scenario.cs:166` | `Steps/MiniQuizStep.cs` *(WS-3)* |
| SelectionStep (+ CompleteMode) | `Runtime/Scenario/Scenario.cs:203` | `Steps/SelectionStep.cs` *(WS-3)* |
| InsertStep | `Runtime/Scenario/Scenario.cs:264` | `Steps/InsertStep.cs` *(WS-3)* |
| EventStep | `Runtime/Scenario/Scenario.cs:305` | `Steps/EventStep.cs` *(WS-3)* |
| ConditionsStep (+ ConditionsEvaluator) | `Runtime/Scenario/Scenario.cs:341` | `Steps/ConditionsStep.cs`; `ConditionsEvaluator` is a prime WS-0 unit-test target *(WS-3, WS-0)* |
| GroupStep (+ CompleteWhen/ChildRequirement/MultiConditionBranch) | `Runtime/Scenario/Scenario.cs:410` | `Steps/GroupStep.cs`; graph-integrity tests cover nested SerializeReference *(WS-3, WS-0)* |

**Disposition summary:** keep ~28 · rename ~16 (1 Hub window + Scene Categories mojibake + 2 inspectors + 11 runtime `AddComponentMenu` + SceneManager) · move 3 (ORG-03) · split ~13 (Step base + 10 step types + ScenarioGraphWindow/StepEditWindow + ScenarioEditor) · delete 3 (`ScenarioEditorUtil`, empty `LaunchContextProviders.cs`, dead `SceneManager.EvalCompare`) · defer 2+ (`HealthOnAddressablesAdapter`; AddressablesBuilderWindow relocation, undo-correctness, reflection removal). **Run this census FIRST, as WS A1, before any P1 code lands.**

---

## Appendix B - SceneManager today, and its transition to LabConsole

> **Note:** the full phase-by-phase SceneManager -> LabConsole transition narrative now also lives in the architecture spec §8 (Layer 2 Runtime) + §28 (domain & content systems) and the after-launch plan (`_after-launch/2026-06-09-after-launch-plan.md`). This appendix is retained for the P1 "what SceneManager IS now" baseline.

**Essence.** `SceneManager` (`Runtime/Scenario/SceneManager.cs`, ~2,505 lines, `Pitech.XR.Scenario`) is the **runtime scenario interpreter**: one MonoBehaviour that, on `Start`/`Restart`, walks `Scenario.steps` in a single `Run()` coroutine, dispatches each `Step` subtype to a per-type `RunXxx` coroutine (with a parallel `RunXxxGroup` variant for `GroupStep` concurrency), polls input/conditions/UI to decide completion and the next-step GUID, and brokers stats, quiz, selection, and editor-skip integration - all from a serialized config surface and a tiny public API.

**Public contract that labs / ContentDelivery / editor depend on (must be preserved verbatim in P1):**
- **Labs** assign `scenario`, `autoStart`, optional stats/quiz/selection refs, `labContentRoot`; call the selection bridges `ActivateSelectionList(int)` / `ActivateSelectionListByName(string)` / `CompleteSelection()` / `RetrySelection()` from Timeline signals/UnityEvents.
- **ContentDelivery** resolves the manager by `GetType().FullName=="Pitech.XR.Scenario.SceneManager"` (`ContentDeliverySpawner.cs:1134`) and **string-reflects** `autoStart` (1151/1158), `Restart` (1172). Member renames silently break the spawn flow.
- **Editor** - `ScenarioGraphWindow` calls `EditorSkipFromGraph(guid, branchIndex)` (1588) and reads `StepIndex` (1857); `SceneManagerEditor` reflects `scenario`(367)/`StepIndex`(467)/`Restart`(505).
- **Lab prefabs** reference the component by **Script GUID `2d431a49d183e9c428369f7f758f75cd`**, and rely on `FormerlySerializedAs` on `defaultQuiz`/`quizPanel`/`quizResultsPanel`.

**Load-bearing (preserve verbatim):** the `FallbackGuid '' == linear-next` contract (1000-1004); the exact `Run()` type-dispatch order and branchGuid-assignment pattern; `StepIndex` semantics (`{get;private set;}`, `-1` idle/finished, reflected *by name*); the public symbol **names**; the `FormerlySerializedAs` mappings; the pinned MonoScript GUID; the `_editorSkip`/`_editorSkipBranchIndex` integer encoding; the `DeactivateAllVisuals` invariant; the `selectables.pickingEnabled` refcount discipline; the `_groupExit*` resolve->consume->reset handshake; and the deliberate use of `Time.unscaledDeltaTime` for real-time waits.

**Transition (post-launch; full detail in spec §28 + the after-launch plan):** P1 LOCKS SceneManager (whole, byte-for-byte). after-P1 WRAPS it additively (`IScenarioFlowStore` + `LocalScenarioFlowStore` + the `NetworkedStatesScenarioFlowStore` adapter over the existing `NetworkStateManager`; the `LabEventLedger` minimal runtime). arch-P2 EXTRACTS it behind a thin facade (the `RunXxx`/`RunXxxGroup` -> `IStepRunner` registry, golden-trace-proven). arch-P3 FRONTS it with LabConsole (the only sanctioned outside-in mutation path) + typed Fusion replication under the flow-store. arch-P5-P7 grow analytics + VICKY purely on the seams. At the 1.0 lock, an **offered (never forced)** migration converts labs to LabConsole-native and SceneManager is retired. The runner is bracketed - `IScenarioFlowStore` *below* (facts, not pointer), `LabEventLedger` *alongside* (observation), `LabConsole` *above* (validated actuation) - it knows none of them concretely.
