# DevKit v1.0 - P1 Behavior-Neutral Professionalization: Review & Final Plan

> **Status:** Final. This document is the P1 single source of truth for `com.pitech.xr.devkit`. It supersedes the prior P1 plan and the prior architecture's P1 section.
> **Scope:** Phase 1 (P1) only — behavior-neutral professionalization. Post-P1 items (P2/P3/…) are named for traceability but are explicitly out of P1.
> **Governing law:** A change is admitted to P1 **if and only if** it passes all three equivalence proofs simultaneously (golden-trace byte-equal + public-API additions-only + serialized-diff-zero), *and* the proof harness exists when the change lands.

> **Appendices:** A — full surface/disposition census (WS-pre); B — SceneManager today → its transition to LabConsole.

---

## A. Executive summary

- **The package works and ships, but it is organizationally immature.** Functionality and the launch/telemetry/serialization data contracts are carefully designed; the discipline around them (file organization, encapsulation, encoding, dependency truth, test coverage) is not. This is a professionalization phase, not a rescue.
- **The single most important framing decision: draw the P1 line at the equivalence-proof boundary, not at the size-of-change boundary.** Far *more* reorganization is provably behavior-neutral than the current plan allows (god-class file splits, dead-code deletion, namespace hygiene). But the *highest-value* cleanups (runner unification, dispatch registry, undo-correctness, reflection removal) are *not* behavior-neutral and must stay out of P1. Both the conservative plan and the "rewrite everything" instinct are half right.
- **P1 is four things and nothing else:** (1) the **EditMode safety net** — graph-integrity (refs/routes/events) + serialized/GUID-stability + API baseline + pure-logic locks (the PlayMode golden trace is **seeded but deferred to P2**, since P1 changes no runtime logic); (2) provably-dead-code deletion; (3) pure same-namespace/same-assembly file splits and tiny utility extractions, each carrying its `.meta`; (4) zero-compiled-impact hygiene (formatting/EOL/comments, XML docs, `rootNamespace` fixes, package docs/metadata).
- **The safety net must land first.** There is currently zero Scenario/Core/serialization test coverage and no GUID-stability or API-baseline guard. Until it exists, *every* "behavior-neutral" move is unfalsifiable. **The realistic P1 net is cheap and static** (EditMode, runs against real labs, no play mode, no flaky CI); the expensive PlayMode golden trace is P2's tool, only seeded here.
- **Execution order — editor restructure first, then the net, then the cutting.** **WS-pre (census) → WS-6 (editor surfaces + DevKit Hub cockpit + `Evaluate Changes`) → WS-0 (the net) → WS-1/2/3 (the data-touching work).** WS-6 leads because menus/Hub aren't serialized into labs (safe without the net); WS-0 then gates everything that touches lab data. The P1 gate is the in-editor **`DevKit > Evaluate Changes`** button, run against a fixture corpus extracted from real labs — **no server CI in P1** (it arrives at P2 with the PlayMode rig). New features (Make-Multiplayer, Localization) enter as **gated modules + Hub pages** — structure reserved in WS-6, logic post-P1.
- **P1's job is NOT to shrink SceneManager.** It is to make the *next* engineer able to *prove* a shrunken SceneManager behaves identically. The 2,506-line God-class gets *locked* — graph-integrity + serialized/GUID in P1, the golden-trace replay in P2 — not *cracked*. The `RunXxx`/`RunXxxGroup` variants are not bit-identical today (standalone `RunQuestion` debounces; the group variant is first-click-wins), so unification is a behavior change → P2.
- **Two "P1-safe" items from the inputs are mis-scoped and corrected here:** (1) the `Scenario.cs` data-model split stays P1 but is the highest-serialization-risk item and is **gated on the test harness existing first**; (2) adding `dependencies` to `package.json` is **not** metadata-only — it changes UPM resolution in consumer projects, so it moves to **after P1** (the license/URL/`unityRelease` metadata stays P1).
- **Top risks, in order:** (1) a regenerated `.meta` GUID during a file split nulls every authored step graph in shipped lab prefabs; (2) Core.Editor reaches Scenario/Quiz/Stats by **hard-coded `FullName` strings**, an invisible contract a namespace move breaks silently *and* that the standard API baseline would not catch; (3) a confirmed **critical** cross-system bug (`AddressablesRemoteUrlRewriter` nulls the global Addressables transform) that is real but *not* P1 because fixing it changes behavior.
- **The current P1 plan is too conservative on file-splits/dead-code/namespacing and too vague on its gates.** It marks the golden trace "preferred," has unmeasurable exit criteria, has no GUID-stability gate, and does not pin the Core.Editor reflection literals. This document fixes all of that.

---

## B. State of the package

### B.1 Organization

- **God-classes dominate the surface area.** `SceneManager.cs` (~2,506 lines) owns serialized config, input polling, reflection condition evaluation, stat binding, quiz/results orchestration, group concurrency, and an editor-stepping hook — dispatched through *two* parallel hand-written `if (step is X)` ladders with every step type implemented twice (`RunXxx` + `RunXxxGroup`, ~1,000 duplicated lines). `ScenarioGraphWindow.cs` (~4,962 lines) is a single file holding six types including a second `EditorWindow`, with the route/branch schema hand-duplicated across **seven** methods. `ScenarioEditor.cs` (~1,480 lines) mixes the inspector, a `Styles` theme, and eleven `PropertyDrawer`s. `ContentDeliverySpawner.cs` (~1,180 lines) spans policy, UX, Addressables lifecycle, and reflection coordination.
- **The data model itself is disciplined where it matters.** `Scenario.cs` correctly uses `[SerializeReference]`, `[FormerlySerializedAs]` on renamed fields, `[Serializable]` on every subclass, and — load-bearing — the `OnValidate` no-null-strip + `isCompiling` guard that prevents permanent step-graph data loss. This contract is sound and must not be touched in P1.

### B.2 Dependency / asmdef truth

The 12-assembly DAG is topologically sound (Editor→Runtime only, Core as runtime leaf, no cycles, all five Editor asmdefs correctly `includePlatforms:["Editor"]`). But the *metadata* is dishonest:

| Assembly / file | Hard-references (asmdef) | Declared in `package.json` | Truth |
|---|---|---|---|
| `Pitech.XR.ContentDelivery` (runtime) | `Unity.Addressables`, `Unity.ResourceManager`, `Unity.TextMeshPro`, `Unity.ugui` | **none** | Consumer without these gets unresolved-assembly compile errors |
| `Pitech.XR.ContentDelivery.Editor` | `Unity.Addressables.Editor` (+ above) | **none** | Same |
| Addressables usage in source | `#if PITECH_ADDR`-guarded (6 regions) | versionDefine `"1.0.0"` (degenerate floor) | "Optional" in source, but hard-referenced in asmdef → guard's `#else` is **dead** |
| TMP / ugui usage (`ContentDeliveryStatusOverlay.cs:3,5`) | **un-guarded** `using TMPro;` / `using UnityEngine.UI;` | **none** | Genuinely **required**, regardless of any define |
| `com.unity.services.ccd.management` (`PITECH_CCD`) | versionDefine only; never referenced in any runtime `.cs` | n/a | Genuinely optional / effectively dead |
| `Unity.ResourceManager` (explicit ref) | referenced alongside Addressables | n/a | **Correctly kept** — asmdef refs are non-transitive and source uses `ResourceManagement.*` types directly |

Other asmdef-truth issues: `Interactables.Editor.asmdef` `rootNamespace` is wrongly `Pitech.XR.Scenario.Editor` (copy-paste); references mix raw `GUID:` and plain-name forms with no rule; asmdef JSON formatting is inconsistent (Core single-line, Core.Editor 2-space, the rest 4-space); `Interactables.Editor` references `Unity.Timeline` with no apparent consumer.

### B.3 Test coverage

Real but narrow. Five EditMode files exercise **only** ContentDelivery pure logic (CCD URL composition, versioned local paths, idempotency, state-machine transitions). There is **zero** coverage of Core/Scenario/Interactables/Quiz/Stats, no PlayMode `[UnityTest]`, no `[SerializeReference]` open/save round-trip test, no GUID-stability test, and no public-API baseline. The existing test asmdef also uses the deprecated `optionalUnityReferences:["TestAssemblies"]` form — a poor template to copy. **The P1 behavior-neutral claim depends entirely on tests that do not yet exist.**

### B.4 Hygiene

- A stray `--- SCENE MANAGERS ---.prefab` (GUID `a0032abe…`) sits at the **package root** (non-idiomatic for UPM; shell-hostile name).
- No `README.md` / `CHANGELOG.md` / `LICENSE.md` / `.editorconfig` / `.gitattributes`.
- Encoding rot: a lone `0x85` byte inside a `[MenuItem]` path string; `U+FFFD` replacement chars in `DevkitWidgets.cs` comments; Greek-language comments and one Greek `[Tooltip]` scattered through SceneManager, SelectionLists, and editors.
- `Runtime/link.xml` uses `preserve="all"` on six whole assemblies — **safe today** (it does cover the `[SerializeReference]` Step types) but over-broad; must not be narrowed later without enumerating every reflection-instantiated type.

---

## C. Top problems found in the codebase

Prioritized. "BN?" = behavior-neutral (eligible for P1). Evidence is to file:line where cited in findings.

| Sev | File | Issue | BN? | Phase |
|---|---|---|---|---|
| **Critical** | `Runtime/ContentDelivery/AddressablesRemoteUrlRewriter.cs:121,129-138` | `Install()` overwrites the **global** `Addressables.ResourceManager.InternalIdTransformFunc` without saving the prior value; `Uninstall()`/`Clear()` set it to `null` unconditionally → destroys any host/other-package transform. In UaaL this can break the host RN app's Addressables. **Confirmed bug, not a smell.** | No | **after P1** (save-on-install / restore-on-uninstall; regression test; route via bridge/host owner) |
| **High** | `Runtime/Scenario/SceneManager.cs` (whole) | 2,506-line God-class; two parallel type-switch ladders; every step type implemented twice (`RunXxx`/`RunXxxGroup`); shared-field group-exit routing (`_groupExitBranchResolved`, etc.). Variants are **not** bit-identical (`RunQuestion` debounce vs `RunQuestionGroup` first-click-wins). | No | **P2** (lock with golden trace in P1) |
| **High** | `Editor/Core.Editor/Services/{Quiz,Stats,Scenario}Service.cs` | Core.Editor resolves Scenario/Quiz/Stats by hard-coded `FullName` strings (`"Pitech.XR.Quiz.QuizAsset"`, `"Pitech.XR.Stats.StatsConfig"`, `ScenarioGraphWindow` by `t.Name`). **Invisible API contract**: a namespace move breaks the Hub at runtime *and passes the compiler and the standard reflection API baseline*. | n/a (constraint) | **P1** to pin in baseline; renames → P2 |
| **High** | `package.json` + ContentDelivery asmdefs | Zero declared `dependencies` against four hard-referenced Unity packages; TMP/ugui used un-guarded. Consumers without them fail to compile. | Partly | **after P1** (`dependencies` block changes resolution); metadata-only fields are P1 |
| **High** | `Runtime/ContentDelivery/...` (multiple) | String-reflection dispatch (`autoStart`/`Restart`/`StepIndex`/`scenario`/`steps`/`guid`) and `FindObjectsOfType` in **runtime package code** — banned, IL2CPP/AOT-fragile, per-frame in `RuntimeTelemetryAdapter`. | No | **P2** (shared `ISceneRunnerControl` interface in Core) |
| **High** | `Tests/Editor/*` | No Scenario/Core/runner/serialization tests; no golden trace; no GUID-stability or API baseline. The entire P1 safety case rests on absent tests. | Yes (additive) | **P1** (first) |
| Medium | `Editor/Scenario.Editor/ScenarioGraphWindow.cs` (structural `[SerializeReference]` list mutations) | `scenario.steps.Add/Insert/RemoveAt`, `group.steps.*` guarded only by `Undo.RecordObject`, never `RegisterCompleteObjectUndo` → ambiguous managed-reference undo ("type tree changed"). | No | **after P1** |
| Medium | `Editor/Scenario.Editor/ScenarioEditor.cs:220-231` | Per-element "Remove null entry" button deletes a `[SerializeReference]` slot with **no confirmation dialog** — during transient nulls (import/apply/reload) this can permanently destroy a still-valid step. Contradicts the single-sanctioned-path invariant. | No | **after P1** (elevated; can lose shipped-lab data) |
| Medium | `Editor/ContentDelivery.Editor/AddressablesAdapterResolver.cs:23-33` | Vendor-specific `HealthOnAddressablesAdapter` ships inside the generic toolkit and is the reflected default fallback — brand coupling in `com.pitech.xr.devkit`. | No | **P2** (document in P1) |
| Medium | `Editor/Core.Editor/...` (layering) | `Core.Editor → ContentDelivery(.Editor) → Core`; a ContentDelivery feature window (`AddressablesBuilderWindow`) lives inside Core.Editor. Naive "move the window" creates a **circular asmdef ref** that will not compile. | No | **P2** (hard constraint in P1) |
| Medium | `--- SCENE MANAGERS ---.prefab` (root) | Stray content prefab at package root (GUID `a0032abe…`); must carry `.meta` if moved. | No (referenced by GUID) | **P2** |
| Low→Med | hygiene (multiple) | Mojibake (`0x85` in `[MenuItem]`; `U+FFFD` in comments), Greek comments/tooltip, wrong `rootNamespace`, missing root docs, inconsistent asmdef formatting. | Mostly Yes | **P1** (the `0x85` menu string is **after P1**) |

**The one genuinely confirmed P1-deletable dead code** (all verified zero-caller, private/internal, not serialized): `SceneManager.EvalCompare` (1168-1182), `ScenarioEditorUtil.cs` (entire), empty `LaunchContextProviders.cs`, `BuildDefaultPrefabAddressKey` (AddressablesService 811-814), `Styles.Primary`, the `"defaultNextGuid"` ternary (ScenarioEditor 1041), the dead `DevkitWidgets` cluster (StatusChips/StatusBar/Kpi/Tile/StatusRibbon/StatusHeader/ProgressBar/ProgressBarPro + `DevBlocksWindow.SmallButton`), `RebuildLinksFromGraph` forwarder, and the dead `try/catch` in `StatsUIController.Init` (56).

---

## D. Problems & unprofessionalities in the CURRENT P1 plan

Direct and specific. The plan's instincts are sound; its scoping and its gates are not.

### D.1 The scope mismatch (the central issue)

**The current plan demotes provably-neutral reorganization to a P2 "inventory," and only blesses "extract tiny utility classes."** (Workstream C / §6.) That is under-scoped against both the user's professionalization intent and the verified findings. The adversarial verifier confirmed **12 of 14** P1-tagged reorganizations are genuinely behavior-neutral, including the full god-class **file splits** (`Scenario.cs` 11 types; `ScenarioEditor.cs` 11 drawers; `ScenarioGraphWindow.cs` 6 types), the **dead-code deletions** above, and the **namespace/`rootNamespace` hygiene** — because `[SerializeReference]` keys on type identity (namespace + assembly + typename), **not file path** (`QuizStep`/`QuizResultsStep` already prove the pattern), and editor types are never referenced by GUID in serialized lab data.

**But the user's "rewrite everything worth it" instinct over-reaches the other way.** The verifier and findings are emphatic that the *highest-value* work is **not** neutral and must stay out of P1: `RunXxx`/`RunXxxGroup` unification and the `IStepRunner` registry (variants diverge), routing `RecordObject→RegisterCompleteObjectUndo` and direct-field→`SerializedObject` (change undo/prefab-override behavior), runtime reflection/`Find` removal, the rewriter restore fix, and any namespace/assembly **move** of a `[SerializeReference]` type (needs `[MovedFrom]`). The plan is **correct** to defer these.

**Correction:** redraw the P1 line at the proof boundary. P1 = every change passing all three proofs (far more than the plan allows). after P1 = everything failing any proof (the most impactful cleanups). See §E.

### D.2 Gaps (things the plan does not capture)

- **No GUID-stability gate.** §B2 checks "assets load without missing scripts" — a downstream symptom, not the invariant. Every shipped lab references scripts by GUID; the file splits the plan should invite *require* an explicit test pinning `Scenario`/`SceneManager`/Step `.meta` GUIDs.
- **The Core.Editor `FullName` reflection contract is unrecorded.** §B4 reflects assembly public surfaces only, so a Step/config namespace move passes the baseline yet breaks the Hub. The plan's own invited splits are the trigger.
- **The layering inversion is never named.** Nothing warns that moving `AddressablesBuilderWindow` to `ContentDelivery.Editor` creates a circular asmdef ref. Someone will attempt the obvious move and break the build.
- **The critical rewriter bug is downgraded to an open question** ("check whether `Clear()` incorrectly clears…") rather than a confirmed defect with an owner and an after-P1 slot.
- **No optional-dependency compile test.** §A3 mentions defines/shims abstractly but provides no gate proving the no-Addressables path even compiles (it currently can't — Addressables is hard-referenced).
- **No automation.** `.editorconfig`/`.gitattributes` are added as *files* with no enforced check; the API snapshot is "manually maintained" — a non-gate. Formatting churn will recur.
- **Mojibake/encoding under-scoped** to a single vague mention ("at least one editor text/encoding issue").

### D.3 Vague / unmeasurable criteria

The plan's exit criteria — "line ending and formatting churn is controlled," "the codebase is organized well enough for after P1 / P2," "optional package promises are either tested or removed," "changes are reviewed deliberately" — have **no objective threshold**. They cannot be checked off without subjective judgment. Each must become a runnable gate (see §F).

### D.4 The golden trace is marked "preferred," not required

§B3 offers a "smoke checklist" as the acceptable minimum. The playbook makes the PlayMode golden trace (driven by `EditorSkipFromGraph`, capturing `fromIndex/stepGuid/Kind/branchGuid/toIndex` + an **ordered side-effect log**) the **non-negotiable** precondition for any runner work and the only valid definition of "plays identically." A smoke checklist cannot prove byte-equivalence and cannot unblock P2. The plan also omits **side-effect-order capture**, which is exactly what the P2 `RunSelection`/`RunQuestion` unifications hinge on.

### D.5 The "looks safe but isn't" list is missing

Workstream C gives no caution about editor cleanups that *look* like P1 but change behavior: `RecordObject→RegisterCompleteObjectUndo` on `[SerializeReference]` lists, the `JsonUtility` deep-copy fidelity question, routing direct-mutation→`SerializedObject`, and the ungated null-delete. Without an explicit deferred list, a well-meaning contributor will land one of these as a "cleanup."

---

## E. The FINAL P1 plan

**Execution order** (numbered by topic, sequenced by safety): **WS-pre → WS-6 → WS-0 → WS-1 → WS-2 → WS-3 → WS-4 → WS-5.** Two land *before* the safety net on purpose — **WS-pre** (the inventory census; no code change) and **WS-6** (editor surfaces + DevKit Hub; menus/Hub are *not* serialized into labs, so they cannot corrupt a shipped lab — proof is "compiles + items appear + `ExecuteMenuItem` callers resolve," not the data tests). **WS-0** (the net + `DevKit > Evaluate Changes`) then lands before any *data-touching* work. **The guardrail: WS-2 (dead-code) and WS-3 (file splits) — the only workstreams that touch lab data — never land before WS-0.**

### Equivalence proofs (the P1 admission test, applied to every commit)

P1 moves declarations between files, deletes dead code, and reformats — **none of it changes the runner's execution paths.** So the P1 admission test is **static + additive (all EditMode)**, not a runtime replay. The expensive PlayMode replay is the admission test for *runtime* changes, which is P2.

- **Proof A — Scenario graph integrity** *(primary P1 net)*. For each lab fixture the authored `[SerializeReference]` step graph is intact after the change: (i) **references** — every assigned step reference still resolves, no nulled/Missing refs, no null step in a `[SerializeReference]` list; (ii) **routes** — every routing guid (`nextGuid`, `correctNextGuid`/`wrongNextGuid`, `outcomes[].nextGuid`, `defaultNextGuid`, `specificStepGuid`, `multiConditionBranches[].nextGuid`, `childRequirements[].guid`) is empty or points to an existing step guid, recursing into `GroupStep.steps` (no dangling routes); (iii) **events** — every `UnityEvent` persistent listener keeps a live target + non-empty method (no "Missing" wiring). Universal invariants need no baseline; a per-lab snapshot also catches dropped/rewired steps. **Read-only, pure EditMode, runs against real labs — no play mode, no runnable fixtures.**
- **Proof B — Public-API additions-only.** Reflected `PublicOnly` surface over all `Pitech.XR.*` may only *gain* members. **Extended for this package** to assert the Core.Editor `FullName` literals still resolve.
- **Proof C — Serialized & GUID integrity.** GUID-stability (every MonoScript `.meta` GUID unchanged) + serialized-diff (each fixture, **scene object and prefab-instance-with-override**, open→save = zero structural change).

**Failing any one → behavior change → deferred to after P1.**

> **Deferred to P2 — Proof D (golden trace).** A PlayMode replay (`EditorSkipFromGraph`-driven trace + ordered side-effect log) proves the *runner interprets the graph identically*. Because P1 never changes runtime logic, Proofs A–C are a complete net for it; the golden trace is the admission test for **P2** (the `RunXxx`/`RunXxxGroup` unification, where execution paths actually change). P1 only **seeds** the harness — one happy-path fixture to validate the approach and de-risk P2 — it is **not** a P1 gate, and the full fixture corpus + PlayMode-in-CI is P2 work.

---

### WS-pre — Inventory & disposition census *(run FIRST; no code change)*

**Goal:** enumerate every surface before touching any of them, so every later WS is mechanical and surprise-free. **Already produced:** **Appendix A** (below) — 77 surfaces, each with `file:line` + disposition (≈40 keep / 18 rename / 13 split / 3 move / 2 delete / 1 defer), the menu-root-unification table, and the full SceneManager anatomy. It exists because the menu-root unification touches reflected `ExecuteMenuItem` callers, the splits move types across files while pinning `.meta`/GUIDs, and the deletes must be proven caller-free — none of which is safe to start blind.

**Tasks:** confirm the census is current vs the live source (spot-check GUIDs/line anchors — e.g. `SceneManager` MonoScript GUID `2d431a49d183e9c428369f7f758f75cd`); freeze it as the disposition reference WS-1…WS-6 execute against.
**Acceptance:** every WS-1…WS-6 edit maps to a census row.
**Gate:** none — this is the map everything else follows.

---

### WS-6 — Editor surfaces & DevKit Hub cockpit *(runs before WS-0; editor-only, safe without the net)*

**Goal:** one consistent surface taxonomy and a Hub that is the cockpit for everything. Safe before the data net because menus/Hub are **not serialized into labs** (components bind by script GUID) — proof is compile + "items still appear" + `ExecuteMenuItem` callers resolve, *not* the graph-integrity test.

**Naming (locked):** menu root token = **`Pi tech`** everywhere; home window = **"DevKit Hub"**.

**Tasks** (findings: ORG-03, SCN-03-hub mojibake, the WS-pre census):
1. **Unify the menu root.** Drop the ` XR` from **all** `[AddComponentMenu]` paths (`Pi tech XR/<Module>/…` → `Pi tech/<Module>/…`); keep the `GameObject/Pi tech/` and `Pi tech/` (top-bar + CreateAssetMenu) trees. **Each rename updates its internal `ExecuteMenuItem` callers in `DocsPage.cs` (5 calls) in the same commit**; leave Meta's `GameObject/Interaction SDK/…` alone (not ours).
2. **ORG-03:** move `SelectableTarget` / `SelectablesManager` / `MetaSelectRelay` Add-Component paths from the `Scenario` group → the `Interactables` group.
3. **Mojibake:** strip the `0x85` byte from the `SceneCategories` `[MenuItem]` path (editor-visible-string rule; grep-verify no programmatic reference first).
4. **Rebuild the Hub as the cockpit.** Brand the window **"DevKit Hub"**; reorganize pages **task-first**: **Setup · Author · Deliver · Maintain · Reference**. Every **workspace window** (Scenario Graph, Dev Blocks, Addressables Builder, Scene Categories) gets a **launch tile** (the Hub *launches* them, never re-implements them). Surface the **repair tools** and **`Evaluate Changes`** in *Maintain*. Add an **"Add Scenario to Scene"** command.
5. **Surface-type discipline (5 interaction types):** name by type — verb for **commands** ("Add Scenario to Scene"), noun for **workspaces** ("Scenario Graph"), "… Wizard" for **wizards**. **Add Component / Create Asset are the fallback tier** (the automation does the wiring, so they're barely used — keep tidy, don't foreground).
6. **Graph readability — node labels, section shapes & branch labels.** Split by whether the author's intent must be *saved*:
   - **P1 (derived/visual — stores nothing):** a **derived node label** (computed from `Kind` + key params, e.g. "Question — Is the patient stable?" — not stored); **rendering the labels branches already carry** on their connecting edges (`ConditionOutcome.label`, `MiniQuizOutcome.label`, the choice text — these `label` fields already exist in the data model); and **structural coloring** (by `Kind` / `GroupStep` membership). Pure presentation → behavior-neutral, fits here.
   - **First after-P1 (additive — persists author intent):** **background "section" shapes** — a purely visual box drawn *behind* a chosen set of step nodes to group/distinguish them, with **no functional effect** on flow — and **custom branch names** on edges that don't already carry a `label`. Both must be *saved*, via an editor-only serialized field using the same `#if UNITY_EDITOR` pattern as the existing `Scenario.GraphNote` (so it can reuse/extend that graph-notes infrastructure). Saving changes the asset → fails serialized-diff → not P1, but the very first additive item right after, and low-risk (old labs default to empty).

**Future feature slots — reserve the structure now; logic lands post-P1.** New features enter the DevKit by one repeatable recipe: **(a)** a `Runtime/<Feature>` and/or `Editor/<Feature>.Editor` module with its own asmdef; **(b)** optional-package dependencies are **gated** via `versionDefine` + `#if` (never hard deps); **(c)** the right entry points + a Hub tile/page under the right task group; **(d)** reference Core/Scenario, never the reverse. Two reserved slots:
- **Networking / "Make Multiplayer"** — Fusion-gated (`PITECH_HAS_FUSION`) `Runtime/Networking` + `Editor/Networking.Editor`; surfaced as a `GameObject ▸ Pi tech ▸ Make Multiplayer` **command** + a Hub *Setup* button; injects the rig + Fusion objects at **author-time** (editor only — the host still owns the runtime camera; the DevKit must not spawn the production camera at runtime). *Decision pending:* rig prefab package-shipped (sample) vs host-provided. The deeper step-sync **replication** is the architecture's after P1 (`IScenarioFlowStore` facts) → P3 (typed Fusion), separate from this setup tool.
- **Localization** — `com.unity.localization`-gated (new `PITECH_HAS_LOCALIZATION`) `Editor/Localization.Editor`; lift the 4 files from `HealthOn VR/Assets/Scripts/Editor/Localization/` (`LocalizationPipelineWindow`/`LocalizationPipeline`/`LocalizationScanManifest`/`ManualTranslationIO`); give it its **own Hub "Localization" page under *Author*** launching the `LocalizationPipelineWindow`. Architecture P4.

**How multiplayer works — and what `IScenarioFlowStore` is (the seam it rides on).** Multiplayer syncs **facts, not the pointer.** Today each runner tracks a step *pointer* (`CurrentStepIndex`) — fine single-player, fragile across clients (branching/timing/late-join cause desync). Instead, each client runs its *own* runner and they share durable **step-completion facts**: "step `g3` is done, outcome = correct, by player X, at tick N." A client derives its own visible position from the **set of facts + the graph**; a late joiner rebuilds correct state from the current facts alone (no replayed RPC history). **`IScenarioFlowStore`** is the small interface the runner talks to instead of knowing whether it's local or networked — `IsStepComplete` / `GetStepOutcome` / `CompleteStep` + a `StepFactChanged` event. Single-player labs use `LocalScenarioFlowStore` (in-memory, identical to today); multiplayer labs use a networked implementation that replicates the facts — **the runner can't tell the difference.** That's the seam that lets multiplayer slot in *without rewriting the runner.* Path: **after P1** = define `IScenarioFlowStore` + `LocalScenarioFlowStore` + the fact vocabulary (opt-in; the runner emits facts behind a switch, a no-op with no multiplayer) → **P2** = the extracted runner consults the store for real → **P3** = a typed Fusion backend swaps in *under* the store (the runner never changes) and LabConsole fronts it. The **"Make Multiplayer" tool** is the *authoring* half (inject rig + Fusion objects); `IScenarioFlowStore` is the *runtime-sync* half — two halves of one feature.

**Acceptance:** one root token across all four menu systems; every workspace reachable from the Hub in ≤2 clicks; all `ExecuteMenuItem` callers resolve; compiles; mojibake gone. (No lab data touched — confirm with a graph-integrity run once WS-0 lands.)
**Gate:** WS-pre (the census is the disposition map). Does **not** depend on WS-0.

---

### WS-0 — Equivalence harness & test infrastructure *(the safety net — lands before any data-touching work)*

**Goal:** make "behavior-neutral" measurable. This workstream is itself behavior-neutral (purely additive) and is the precondition for every later phase.

**Tasks** (findings: PKG-10, PKG-09, SCN-14, SCN-18, SCN-16, CDE-16). Tasks 1–4 + 6–7 are the **P1 net (all EditMode, realistic)**; task 5 is **seed-only (P2-prep)**.

1. Create `Pitech.XR.Scenario.Editor.Tests` (EditMode) referencing Scenario+Core+Quiz+Interactables+Stats. Use the **modern** test-asmdef form (`UNITY_INCLUDE_TESTS` defineConstraint + `nunit.framework.dll` precompiled + `overrideReferences:true` + `UnityEngine/UnityEditor.TestRunner`) — **do not** copy the deprecated `optionalUnityReferences:["TestAssemblies"]` template (PKG-09). (The matching PlayMode asmdef is created in task 5, not now.)
2. EditMode-lock the pure logic on the **unmodified** code: `ConditionsEvaluator.EvalCompare` (Scenario.cs ~381) — all 8 `CompareOp` incl. `Mathf.Approximately` equality and `>0.5f`/`<0.5f` bool encodings; `GroupStep.IsChildRequired*`/`Ensure*` (~468-563).
3. **Scenario graph-integrity test (Proof A — the primary P1 net).** Walk each lab fixture's `[SerializeReference]` graph (recursing into `GroupStep.steps`) and assert: no nulled/Missing references, no dangling routing guids, no Missing `UnityEvent` listeners. Universal invariants (no baseline) + a per-lab snapshot (catches dropped/rewired steps). **Fixtures here are read statically — they do NOT need to run, so use real lab prefabs or lightweight copies** (no PlayableDirector/collider/UI plumbing required). See §I.0.
4. **Serialized-integrity test (Proof C):** GUID-stability (`Scenario`/`SceneManager` + every MonoScript `.meta` GUID equals a committed constant) **and** open→save serialized-diff (scene object **and** prefab-instance-with-override) per fixture.
5. **Seed (do NOT complete) the PlayMode golden-trace harness — P2-prep, not a P1 gate.** Create `Pitech.XR.Scenario.PlayMode.Tests`, build the `EditorSkipFromGraph`-driven recorder + ordered side-effect log, and prove it on **one** happy-path fixture. The full fixture corpus (one per Kind / per `GroupStep` mode / the `RunQuestion`-debounce-vs-group-first-click divergence) and PlayMode-in-CI are **P2** (§I.4/I.5 are marked P2-prep). This exists so P2 starts with a working harness, not so P1 waits on it. **The deterministic driver already exists** — the Scenario Graph's play-mode `Branch ▶`/`Skip ▶`/`Outcome ▶` buttons call `EditorSkipFromGraph` (the existing manual "scenario test" the team already uses); the recorder *wraps* that proven hook rather than inventing one.
6. **Public-API baseline test** over all `Pitech.XR.*`; **additionally pin** the Core.Editor `FullName` literals (`"Pitech.XR.Quiz.QuizAsset"`, `"Pitech.XR.Stats.StatsConfig"`, `"Pitech.XR.Quiz.QuizUIController"`, `ScenarioGraphWindow` by name) as a "named type resolves" assertion (PKG-04).
7. ContentDelivery additive tests: `RewriteUrl`/`TryParseCcdUrl`, `LaunchContextValidation`, `PublishTransactionStateMachine.CanTransition`; a `PublishReportService` JSON-golden + validation rule-set test (CDE-16).
8. **`DevKit > Evaluate Changes` — the manual gate (the way developers actually run the net).** An editor command (menu item + DevKit Hub button) that runs the EditMode suite via `TestRunnerApi` and shows a plain-language verdict ("✅ safe to push" / "❌ lab X: dangling route at g7"). This is the **P1 enforcement model: a developer clicks it before pushing DevKit changes** — there is **no automated CI in P1** (see §I.11). Editor-only additive tooling, itself P1-safe. The same suite also exposes a headless entry so the gate can later be wired to a pre-push hook or P2 CI unchanged.
9. **`Export Lab as Test Fixture` tool.** An editor command (`Pi tech ▸ Tools ▸ Export Lab as Test Fixture` + Hub *Maintain* button + `GameObject ▸ Pi tech ▸ Export as Test Fixture` on a selected Scenario) that saves the **Scenario subtree** (Scenario + SceneManager + referenced objects, so refs resolve internally) as a self-contained prefab into `Tests/Fixtures/Scenarios/` carrying its `.meta`, and captures the graph-integrity snapshot baseline. This is the on-ramp that fills the corpus and turns the scene-less DevKit project into a real test host. Editor-only, additive.

**Testing setup (where the tests run).** The package's tests + a **curated fixture corpus** (3–5 real labs extracted via task 9) live **inside the package**. The **DevKit Unity project** (package embedded, currently scene-less) becomes the **iteration gate** — a developer runs `Evaluate Changes` there before pushing. Because the *same* suite ships in the package, after a push + package bump you **also run `Evaluate Changes` in the HealthOn project against the real scenes** = the integration check. Two questions ("is the package neutral?" / "did the real labs survive the bump?"), **one suite, two run-locations.** The package gate must never *depend* on a consumer project (wrong layering); HealthOn validation is the bonus the shipped test enables.

**Acceptance:** Proofs A/B/C are runnable, all green on unmodified code; Proof C runs each fixture as scene object **and** prefab-instance-with-override; **`DevKit > Evaluate Changes` runs the EditMode net in one click and reports a clear pass/fail verdict**; the golden-trace harness exists and passes on its one seed fixture (its corpus is explicitly P2).
**Gate:** none upstream — this *is* the gate WS-2/WS-3 depend on (via Proofs A–C, run through `Evaluate Changes`; the golden trace is **not** required to land any P1 move).

---

### WS-1 — Formatting / encoding / comment-language normalization *(commit class 1, isolated)*

**Goal:** remove encoding rot and language inconsistency. **Must be physically separate commits** so a real diff is never hidden behind whitespace churn (playbook §7).

**Tasks** (findings: SCN-10, SGW-10, SCN-14-inspectors, DOC-01, SCN-07-hub mojibake, PKG-08, CDE-14):

1. Translate the four Greek comments in `SceneManager.cs` (~1367/1429/1439/2478); normalize the `#else` input-branch indentation to Allman/4-space. *(token stream unchanged)*
2. Translate Greek **comments** and the Greek `[Tooltip]` in `SelectionLists.cs`; Greek comments in `SceneManagerEditor.cs`. *(Tooltip strings are not serialized into assets → neutral. Greek **help-box strings** in `SelectionListsEditor` are user-visible → P2, excluded here.)*
3. Fix the two `U+FFFD` mojibake comments in `DevkitWidgets.cs`; re-indent the broken object-initializer braces.
4. Reformat all 12 asmdef files to one indent style (4-space) + consistent field set (PKG-08).
5. Fix `AddressablesBuilderWindow` `OnEnable` brace/indentation (CDE-14).

**Excluded (→ after P1):** the `0x85` byte in the `SceneCategories` `[MenuItem]` path — changing a menu-path string is a user-visible string-value change, not a no-op (SCN-03-hub).
**Acceptance:** only comment/whitespace bytes change; Proofs A/C trivially green.
**Gate:** runs through WS-0 proofs as free insurance (touches no compiled logic).

---

### WS-2 — Dead-code & dead-artifact removal *(commit class 2)*

**Goal:** delete provably-dead code. Every deletion verified zero-caller, private/internal, references no serialized type.

**Tasks** (findings: SCN-02-runner, SCN-03/04-inspectors, CDE-05/06, SCN-12-CD, SCN-07/20-hub, STATS-01):

1. Delete `SceneManager.EvalCompare` (1168-1182) — zero callers; both live sites (1042, 2226) use `ConditionsEvaluator.EvalCompare`.
2. Delete the unreachable `"defaultNextGuid"` ternary (`ScenarioEditor.cs:1041`) and the unused `Styles.Primary` field+initializer.
3. Delete empty `LaunchContextProviders.cs` + `.meta` (declares no types).
4. Delete the dead `DevkitWidgets` cluster (StatusChips/StatusBar/StatusRibbon/StatusHeader/ProgressBar/ProgressBarPro/Kpi/Tile + `DevBlocksWindow.SmallButton`) after a zero-reference sweep; remove the duplicate comment banners and the dead `RebuildLinksFromGraph` forwarder. **Live API excluded:** `Actions` (22 call sites), `Card` (21), `Pill` (23), `PillsRow`, `StatusChip`, `TileGrid`, `CardGridTwoCol`.
5. Delete `BuildDefaultPrefabAddressKey`; inline `ComputeAddressKey` at its two private call sites (CDE-05); remove the orphan duplicated `<summary>` above `BuildLocalLabVersionRoot` (CDE-06).
6. Replace the dead `try/catch` in `StatsUIController.Init` (~56) with a direct indexer read — `StatsRuntime` indexer is provably non-throwing (StatsConfig.cs ~123-125).

**RESOLVED (§G, 2026-06-08): DELETE `ScenarioEditorUtil.cs`** (SGW-04), carrying its `.meta` deletion. It is fully dead (zero callers). The "wire `Load` to call `EnsureStableGuids`" alternative is **rejected** — do not implement it. (Consolidating `Load`'s inline guid pass onto a `SerializedObject` helper, if ever wanted, is a separate, separately-proven move, not part of this deletion.)
**Acceptance:** Proof B additions-only (internal/private removals), Proof C zero, Proof A unchanged.
**Gate:** WS-0 (each deletion certified by the golden trace + API baseline).

---

### WS-3 — Pure file splits & tiny utility extractions *(commit class 2; each `.cs` carries its `.meta`)*

**Goal:** the structural professionalization heart of P1 — real god-class decomposition that is still provably a *move*.

**Tasks** (findings: SCN-08, SCN-06-inspectors, SGW-01/05/19, SCN-07/11/19-CD, ORG-03, STATS-02/03, SCN-04/13-hub, PKG-06):

1. **Data-model split (flagship, highest risk).** Move each Step subclass + `ConditionsEvaluator` out of `Scenario.cs` into `Runtime/Scenario/Steps/<Type>.cs`. **`namespace Pitech.XR.Scenario` unchanged, same asmdef, each carrying its `.meta`.** The `Scenario` MonoBehaviour and `OnValidate` (no-null-strip + `isCompiling` guard) stay untouched. **Ordering: this runs AFTER WS-0** — it is unfalsifiable without the serialized-diff harness + GUID test. No `[MovedFrom]` (type identity preserved).
2. **Graph window split + namespace wrap.** Wrap `ScenarioGraphWindow` + nested types + `StepEditWindow` in `namespace Pitech.XR.Scenario.Editor`; split into one file per type. Lift pure helpers (`GroupSummary`, `GetGroupPreferredWidth`, `OutcomeLabel`, AutoLayout BFS) into an `internal` static class; demote no-external-caller `public static` helpers to `internal`/private after grep. *(Verified safe: `ScenarioService.OpenGraph` resolves the window by simple `t.Name`, namespace-independent — the wrap does not break it.)*
3. **Inspector split.** Split `ScenarioEditor.cs` into one file per `PropertyDrawer` + a `Styles.cs`, same assembly/namespace, each `.meta` carried; `[CustomPropertyDrawer]`/`[CustomEditor]` bind by type, not file. **Carry the `using Runtime = Pitech.XR.Scenario;` alias (L10) into each split file** or fully-qualify, else compile break.
4. **ContentDelivery extractions.** Extract the **byte-identical** `TrySetAutoStart`/`TryRestart` from Spawner + Bootstrapper into one `internal static` helper (SCN-07). **Scope note:** the `Find*SceneManager*` helpers are only *near*-identical (instance-vs-static, differing root param) — **exclude them from the verbatim move**; unify them separately as a small behavior-equivalent change proven by Proof A. Move `Timestamp` to its own file (SCN-11). Split public interfaces/enums (`IContentDeliveryService`, `ILaunchContextProvider`, `IContentDeliveryMetadataProvider`, `ContentSourceMode`) into own files (SCN-19) — confirmed none are `[SerializeReference]`; enums serialize by value.
5. **Features.** Rename non-serialized private Stats fields to `_camelCase` (STATS-02); split `StatsConfig.cs` into `StatsConfig.cs`/`StatEffect.cs`/`StatsRuntime.cs` **only after confirming same namespace+assembly so no `[MovedFrom]` is needed** — `StatEffect` is a serialized authored type; if any `[SerializeReference]` usage is found, demote to **after P1** (STATS-03). Normalize the three `"Pi tech XR/Scenario/..."` `AddComponentMenu` paths on Interactables types to `".../Interactables/..."` (ORG-03). **Excluded:** promoting nested `MetaSelectRelay` to its own file (full type name changes → needs `[MovedFrom]` → after P1).

> **Editor-visible-string consistency note (resolves the `AddComponentMenu` vs `[MenuItem]` inconsistency).** Both `AddComponentMenu` paths (here) and the `[MenuItem]` `0x85` mojibake fix (WS-1 exclusion / deferred table) change an **editor-UX-visible path string**. Neither fails any of the three proofs (no asset / runtime / public-API / serialized change). The honest rule: **editor-visible string changes are permitted in P1 as deliberate, single-line, documented edits — NOT bundled into a "pure move" commit — provided a grep first proves the exact path is not referenced programmatically** (`EditorApplication.ExecuteMenuItem`, `Menu.*`, settings, shortcut bindings, automation). Under this rule the `[MenuItem]` `0x85` fix is **promoted into WS-1** (it is a desirable corruption fix and nothing calls it by path); if the grep finds any programmatic reference, that specific change drops back to after P1. The two are now treated identically.
6. **Reflection → typed access in `SceneManagerEditor`** (SCN-08-inspectors): `gm.scenario` / `gm.StepIndex` / `gm.Restart()` (public, assembly already referenced). Removes swallowed exceptions + per-repaint reflection; same values read/method invoked. *(Distinct from the **runtime** reflection in ContentDelivery, which is P2.)*
7. **Editor metadata fixes.** `Interactables.Editor.asmdef` `rootNamespace` → `Pitech.XR.Interactables.Editor` (PKG-06 / SCN-02-interactables — same one-line edit); add namespace to `SelectablesManagerEditor` and use `: UnityEditor.Editor` (SCN-04); add `#if UNITY_EDITOR` + namespace `Pitech.XR.Editor.Quiz`→`Pitech.XR.Quiz.Editor` to `QuizDefaultUIPrefabFactory` (SCN-13).

**Acceptance:** every moved `.cs` carries its `.meta`; GUID-stability test green; Proof C zero on all fixtures **as scene object and prefab instance**; Proof B additions-only.
**Gate:** WS-0 (hard dependency — task 1 specifically requires the nested-`GroupStep` prefab-override fixture).

---

### WS-4 — Documentation, XML docs & package-root professionalization *(additive; zero compiled impact)*

**Goal:** close the docs/metadata gaps and record the intentional exceptions so reviewers stop re-flagging them.

**Tasks** (findings: SCN-17, SGW-09, PKG-11/12/15, CDE-15, PKG-13):

1. Add XML `///` docs to the API-baseline members (`SceneManager` public fields, `StepIndex`, `Restart`, `EditorSkipFromGraph`, selection bridges, `GetOrCreateQuizSession`) and `XRServices`.
2. Add `Debug.LogException(e)` inside the bare `catch {}` blocks in the graph window (SGW-09). **Reclassified — console output is observable behavior** (it can trip `LogAssert`/`LogAssert.NoUnexpectedReceived`, change `Application.logMessageReceived` handlers, and spam device logcat). Mechanically control-flow-neutral, but NOT proof-neutral. **Rule: permitted in P1 only as a "diagnostic-output-only" change when no test asserts console silence on that path; otherwise → after P1.** Default: land it in P1 *after* WS-0 confirms no fixture/test asserts console silence on the graph-window paths; if any does, defer.
3. Add `README.md`, `CHANGELOG.md`, `LICENSE.md`, `.editorconfig` (encoding the chosen style so PKG-08 stays fixed), `.gitattributes` (normalize EOL; mark `.meta`/`.prefab`/`.asset` as text with explicit eol).
4. Add **metadata-only** `package.json` fields: `license`/`licensesUrl`, `documentationUrl`, `changelogUrl`, `unityRelease` floor (the patch carrying the `[SerializeReference]` prefab-override fix), `keywords`. **Do NOT add the `dependencies` block here** (→ after P1; it changes resolution). Record that `Unity.ResourceManager` is **correctly kept** (non-transitive direct usage). Do **not** bump `version` in a refactor commit.
5. Document the intentional Unity-serialization exceptions in a subsystem-notes doc: the public serialized-field surface (intended Unity contract), `OnValidate` GameObject-rename + the load-bearing no-null-strip/`isCompiling` guard, the editor-only `FindObjectsOfType` legitimacy (CDE-15), and the `link.xml` whole-assembly `preserve="all"` being **safe-but-broad** (PKG-13) — **must not be narrowed without enumerating every reflection-instantiated/`[SerializeReference]` type.**

**Acceptance:** no `.cs` token outside comments changes; API baseline additions-only.
**Gate:** WS-0 (run through the baseline; additive).

---

### Explicitly deferred (with reason + compatibility note)

| Item | Phase | Why not P1 | Compatibility note |
|---|---|---|---|
| `AddressablesRemoteUrlRewriter` save/restore of global transform | **after P1** | Fixing it changes observable behavior (confirmed critical bug) | Capture prior func on install, chain in `TransformLocation`, restore (not null) on uninstall; no-op if current func isn't ours; regression test; route via bridge/host owner |
| `package.json` `dependencies` block + versionDefines range fix | **after P1** | Changes UPM resolution in consumer projects | Reconcile required-vs-optional (TMP/ugui hard; Addressables guarded but hard-referenced); add a with/without-`PITECH_*` compile test |
| `RunXxx`/`RunXxxGroup` unification (Move A) → `IStepRunner` registry (Move B) | **P2** | Variants not bit-identical; fails Proof A by construction | Each a separate commit, proven byte-equal against the WS-0 golden trace + side-effect log first |
| Editor undo-correctness (`RecordObject→RegisterCompleteObjectUndo`; direct-field→`SerializedObject`; gate the ungated null-delete) | **after P1** | Changes undo-stack shape / prefab-override behavior | Validate with prefab-instance fixtures; the ungated null-delete (SCN-12) is **elevated** — can lose shipped-lab data |
| 7-way route-schema table (SGW-02); shared per-step drawers (SGW-07); `JsonUtility` deep-copy fidelity (SGW-16) | **after P1** | Variants diverge today; reproducing byte-for-byte is itself a behavior risk | Lock with fixture round-trips (Proof C) before merging |
| Runtime reflection/`Find` removal (Interactables INT-01/02; ContentDelivery SCN-04/05) | **P2** | Changes discovery/timing/caching | Introduce `ISceneRunnerControl` in Core; cache VR/Meta determination |
| Core.Editor layering inversion + `AddressablesBuilderWindow` relocation | **P2** | Naive move = circular asmdef ref (will not compile) | First extract shared editor-UI primitives to a lower assembly; then move the window **carrying its `.meta`** |
| HealthOn adapter de-coupling (CDE-02) | **P2** | Changes resolution for labs relying on the implicit fallback | Gate behind a migration that sets `adapterTypeName` on existing configs |
| Stray root prefab relocation (PKG-05) | **P2** | Referenced by GUID `a0032abe…` | Carry `.meta`; verify no scene/variant references it; prove Proof C |
| `link.xml` narrowing (PKG-13) | **P2** | Size optimization, not correctness; safe today | Enumerate every reflection-instantiated Step type before narrowing |
| `0x85` byte in `[MenuItem]` path (SCN-03-hub) | **WS-1/WS-6 (P1)** | Editor-visible string, *not* lab data; grep-verified no programmatic reference | **Promoted into P1** per the editor-visible-string rule — strip the byte, save UTF-8 |

---

## F. P1 exit checklist *(measurable)*

**Census & editor restructure (WS-pre, WS-6 — land first):**
- [ ] Census (Appendix A) confirmed current; every WS-1…WS-6 edit maps to a census row.
- [ ] Single menu root `Pi tech` across top-bar / Add Component / Create Asset / GameObject; ` XR` dropped from all `[AddComponentMenu]`; ORG-03 applied (Selectable* → Interactables); `0x85` mojibake stripped.
- [ ] All `DocsPage` `ExecuteMenuItem` callers resolve; package compiles; no lab data touched.
- [ ] DevKit Hub rebuilt as the cockpit (task-first pages; launch tiles for every workspace; repair tools + `Evaluate Changes` surfaced; "Add Scenario to Scene" added).
- [ ] Future-feature slots reserved (gated `Networking`/Make-Multiplayer + `Localization.Editor` modules + Hub pages) — structure only, logic post-P1.

**Safety net (WS-0) — the P1 net is all EditMode:**
- [ ] `Pitech.XR.Scenario.Editor.Tests` exists in the **modern** asmdef form; discoverable in Test Runner.
- [ ] `Export Lab as Test Fixture` tool ships; 3–5 real labs extracted into `Tests/Fixtures/`; the DevKit project runs the net green against them (the iteration gate).
- [ ] **Graph-integrity test green (Proof A — primary net):** refs resolve, no dangling routes, no Missing `UnityEvent` listeners — run against the committed labs; per-lab snapshots committed.
- [ ] EditMode pure-logic tests cover `EvalCompare` (all 8 ops + `Approximately` + bool encodings) and `GroupStep.Ensure*`/`IsChildRequired*`; all green.
- [ ] 3–5 lab fixtures committed (real or trimmed copies) — read statically (no play mode required).
- [ ] GUID-stability test green (every MonoScript `.meta`: Scenario/SceneManager + the rest).
- [ ] Serialized-diff green per fixture (scene object **and** prefab-instance-with-override).
- [ ] Public-API baseline test green **and** the Core.Editor `FullName` literals all resolve.
- [ ] ContentDelivery additive tests (RewriteUrl/validation/state-machine/report-JSON) green.
- [ ] *(P2-prep, not a P1 gate)* golden-trace harness exists and passes on **one** seed fixture.

**Gate (P1 = manual in-editor; server CI deferred to P2):**
- [ ] `DevKit > Evaluate Changes` ships (menu item + Hub button); one click runs the EditMode net and shows a pass/fail verdict. **This is the P1 gate** — a developer runs it green before pushing DevKit changes (§I.11).
- [ ] `.editorconfig` + `.gitattributes` committed; the `Evaluate Changes` run (or a quick local format check) flags violations. *(A format/lint check is **encoded** in `.editorconfig` now; **enforcing** it in server CI is P2.)*
- [ ] A headless entry exists (`-runTests` / a static `RunAll`) so the same gate can later attach to a pre-push hook or P2 CI **unchanged** — building it now, wiring it later.
- [ ] **Dependency-truth REPORT** generated (P1): for each `PITECH_*` define, record asmdef hard-reference vs `package.json` vs actual `#if`/un-guarded source usage. *(P1 cannot honestly run a real with/without-Addressables compile matrix: the asmdef hard-references `Unity.Addressables` today and the decision is "Addressables is required" — so the **real compile matrix moves to after P1**, after `dependencies` are declared and the dead `#else` branches removed. P1 only reports the current reality.)*
- [ ] *(Deferred to P2, not a P1 gate)* server CI (GameCI + Unity license) running EditMode + the PlayMode golden trace on every PR.

**Reorganization (proven neutral):**
- [ ] WS-1 formatting/encoding landed as **separate** commits; only comment/whitespace bytes changed.
- [ ] WS-2 dead code deleted; every deletion zero-caller; Proofs A/B/C green; `ScenarioEditorUtil.cs` **deleted** (resolved §G) with its `.meta`.
- [ ] WS-3 splits landed; every moved `.cs` carries its `.meta`; Proof C zero as **scene object and prefab instance**; no `[MovedFrom]` required (none of the moves changed namespace/assembly).
- [ ] `SceneManagerEditor` reflection replaced with typed access; behavior identical.
- [ ] `rootNamespace` / global-namespace editor fixes applied.

**Docs / metadata (WS-4):**
- [ ] XML docs on the API-baseline members + `XRServices`; baseline still additions-only.
- [ ] `README.md` / `CHANGELOG.md` / `LICENSE.md` present at package root.
- [ ] `package.json` metadata fields added (license/URLs/`unityRelease`); **no** `dependencies` block added; **no** version bump.
- [ ] Subsystem-notes doc records the intentional Unity-serialization exceptions and the `link.xml` constraint.

**Negative gates (must remain TRUE):**
- [ ] `OnValidate` no-null-strip + `isCompiling` guard untouched.
- [ ] No `RunXxx`/`RunXxxGroup` unification, no dispatch-registry change, no `RecordObject→RegisterCompleteObjectUndo`, no direct-field→`SerializedObject` routing change, no runtime reflection/`Find` removal, no rewriter restore fix, no `dependencies` resolution change — all deferred.
- [ ] No serialized public field renamed/retyped; no `[SerializeReference]` type moved namespace/assembly.

---

## G. Open decisions for the human

**RESOLVED 2026-06-08 (Stergios):** #1 keep options open → document coupling only, keep the seam (no decouple). #2 take default (document-only, one switchboard, adapter home → after P1). #3 approve `Pitech.XR.Core.Editor.UI` leaf extraction in **P2**; P1 records the constraint. #4 no version bump in P1. #5 **4-space + LF via `.gitattributes`** (initial LF renormalization is its own isolated commit) + `.editorconfig` (encodes the style); enforced for now by the **manual `DevKit > Evaluate Changes` gate** (no server CI in P1 — Stergios, 2026-06-08), server-CI format check at P2. #6 **Addressables is a required dependency** — after P1 adds `com.unity.addressables`/`com.unity.textmeshpro`/`com.unity.ugui` to `dependencies` and removes the dead `#else` branches in one commit; P1 documents the posture. #7 **delete** `ScenarioEditorUtil` in WS-2.

Original questions + recommended defaults below (retained for traceability).

1. **HealthOn adapter placement (CDE-02).** The vendor-specific `HealthOnAddressablesAdapter` ships inside the generic toolkit and is the reflected default fallback. **Question:** move it out of `com.pitech.xr.devkit` into the HealthOn project (with a migration that sets `adapterTypeName` on existing configs), or keep it and just document the coupling? **Default:** document in P1; **move in P2** behind the config migration.

2. **NetworkedStates boundary.** **Question:** where does the multiplayer/step-sync adapter live, and do we confirm there will be exactly one scene-wide state manager? **Default:** document-only in P1; defer the adapter-location decision to after P1; never introduce a second scene-wide state manager (avoids the circular-package-dependency trap).

3. **`AddressablesBuilderWindow` relocation across asmdef (CDE-01 / SCN-01-hub).** The window lives in `Core.Editor` but belongs in `ContentDelivery.Editor`; the naive move creates a circular ref. **Question:** approve extracting the shared editor-UI primitives (`DevkitTheme`/`DevkitWidgets`/`DevkitContext`/hub refresh hook) into a lower `Core.EditorUI` assembly so the window can move (carrying its `.meta`)? **Default:** **P2**; P1 only records the constraint.

4. **Version bump.** `package.json` is still `0.10.5` against a v1.0 launch framing. **Question:** bump in P1, or only in a dedicated release commit? **Default:** **do not bump in P1** — bump in a deliberate release commit after the phase lands.

5. **EOL / formatting policy.** No `.editorconfig`/`.gitattributes` today; three asmdef formatting styles coexist. **Question:** standardize on 4-space (the current majority) for asmdef JSON and house C# style, enforced via `.editorconfig`? **Default:** **yes, 4-space**, encoded in `.editorconfig` and enforced in CI (WS-1 + WS-4).

6. **`dependencies` required-vs-optional reconciliation (PKG-01/02/03).** TMP/ugui are hard-used; Addressables is `#if`-guarded in source but hard-referenced by asmdef. **Question:** declare Addressables as a hard `dependency`, or make it genuinely optional (drop the asmdef name-references and fully `#if`-guard)? **Default:** **after P1 decision** — declare TMP/ugui as hard deps now; resolve Addressables optionality with the compile-matrix test before declaring it.

7. **`ScenarioEditorUtil` disposition (SGW-04).** **Question:** delete the dead util, or consolidate `Load`'s inline guid pass onto it? **Default:** **delete** in WS-2 (smallest neutral change); treat consolidation as a separate, separately-proven move if desired.

---

## H. P1 additions for easier v1.0 implementation (behavior-neutral accelerators)

Added 2026-06-08. P1's purpose IS to make v1.0 cheap and safe; these deepen that. **Guardrail:** each must still pass all three equivalence proofs (additive/neutral) or it is not P1. Priority order.

1. **One gate, two doors — `Evaluate Changes` now, server CI for free later** *(highest leverage)*. Build the EditMode net once and expose it through (a) `DevKit > Evaluate Changes` (the P1 manual gate — what devs click before pushing) and (b) a headless entry running the *same* suite. P1 enforcement is the button (no server CI yet — see §I.11); because the headless door exists, attaching a pre-push hook or a GameCI PR check at P2 is wiring, not rework. The leverage is the single-entry-point design, not standing up CI now.
2. **Over-build the golden-trace fixture corpus now as the P2 acceptance suite.** Beyond the 5 P1 fixtures: one per step Kind, one per `GroupStep` completion mode (all 6), plus the divergent paths this review found (`RunQuestion` debounce vs group first-click-wins; Selection `allowedWrong`/zero-correct; MultiCondition branches). Pure P2 insurance; additive. De-risks the single biggest v1.0 lift (runner unification).
3. **Characterization tests on the reflection/`Find` paths P2 will delete** (ContentDelivery `autoStart`/`Restart`/`StepIndex` string-reflection + `FindObjectsOfType`). Pin current observable behavior so the P2 `ISceneRunnerControl` swap is provable.
4. **Write the "P2 extraction playbook" doc while knowledge is fresh.** Capture the runner divergences + invariants (`_groupExitBranchResolved` routing, silent type-switch fallthrough, `GroupStep` branchGuid contract) so the P2 engineer doesn't re-excavate. Cheap, neutral.
5. **`ISceneRunnerControl` seam → its own isolated ticket, see WS-5** *(optional; the one future-contract allowed in P1, but **NOT** mixed into the WS-0 proof work)*. It is a new public type, so it ships as a standalone, separately-reviewed commit **after** WS-0 has established the baseline — never woven into the harness. Exact interface text and constraints in **WS-5** below.
6. **Stable identifier constants for the analytics/step-fact vocabulary** (`const`s only, no emission): `step.completed` etc. + the `scenario.step.<guid>.done` key format. One source of truth for after P1 ledger work; prevents stringly-typed drift. Keep minimal.
7. **Elevate the read-only localization-candidate report** (already half in WS-E). P4 accelerator; behavior-neutral because it only reports TMP strings (no string-table change, no mutation).

**Traps — do NOT add "to prepare" (break neutrality or lock in bad contracts):**
- Pre-baking `IScenarioFlowStore` / `LabEventLedger` / analytics-envelope **public types** you'll redesign in after P1 (premature lock-in is anti-accelerative; the #6 constants are the only exception).
- Editor-only step-display-name/section **serialized fields** (serialized-diff change → after P1).
- Emitting any events/facts/lifecycle hooks (behavior → after P1).

---

## I. P1 Implementation Pack (WS-0 concretized)

Turns WS-0 from a spec into a ticket set. All paths are package-relative (`com.pitech.xr.devkit/`). **Prerequisite:** a UPM package's tests only run when the package is embedded in a Unity project. Stand up (or designate) a minimal host Unity project (2022.3) that references `com.pitech.xr.devkit` — this is what CI opens. The HealthOn VR/AR consumer project can serve, or a dedicated `DevKitTestHost` project.

**Scope marker:** §I.0, I.1(EditMode), I.2, I.3, I.6, I.7, I.8 are the **P1 net**. §I.4 and §I.5 (the PlayMode golden trace) are **P2-prep — seeded in P1, completed in P2; not a P1 gate.**

### I.0 Scenario graph-integrity test (Proof A — the primary P1 net)

The single most important P1 test, and the cheapest. Pure EditMode, read-only, runs against **real lab prefabs** (no play mode, no runnable fixtures). File: `Tests/Editor/ScenarioGraphIntegrityTests.cs`.

For each lab asset: load it, get the `Scenario`, and **walk `steps` recursively (into `GroupStep.steps`)** collecting the full set of step `guid`s. Then assert:

- **Invariants (no baseline — always true of a valid lab):**
  - no `null` entry in any `[SerializeReference]` `steps` list (a null = a missing-script break);
  - every step `guid` non-empty and unique;
  - every routing guid resolves: for each step gather its outgoing guids by type — `nextGuid` (Timeline/CueCards/Insert/Event/Group), `correctNextGuid`/`wrongNextGuid` (Selection), `outcomes[].nextGuid` (Conditions/MiniQuiz), `defaultNextGuid` + `multiConditionBranches[].nextGuid` (MiniQuiz/Group), `specificStepGuid`, `childRequirements[].guid` — and assert each is `""` (linear fall-through) **or** a member of the collected guid set (**no dangling route**);
  - every `UnityEvent` (`Choice.onSelected`, `MiniQuizChoice.onSelected`, `SelectionStep.onCorrect`/`onWrong`, `EventStep.onEnter`) has, for each persistent listener `i`, `GetPersistentTarget(i) != null` and non-empty `GetPersistentMethodName(i)` (**no "Missing" wiring**).
- **Snapshot (small per-lab baseline JSON — catches a *dropped* or *silently rewired* step that invariants would pass):** committed fingerprint = ordered `[(guid, Kind, [outgoing guids])]` + per-event `[(targetTypeName, method)]`. Re-extract after a change and assert equal. Regenerate only via an explicit `--regen`, reviewed as a deliberate change.

Catches exactly the P1 failure class — broken refs, dangling routes, dead events, dropped steps — and nothing it misses (runner-logic divergence) is reachable in P1. Implement against **real labs** if the test host has them; otherwise commit a few representative lab prefabs (carrying `.meta`) under `Tests/Fixtures/Scenarios/`.

### I.1 Test assembly layout (EditMode primary; PlayMode seed = P2-prep)

Two assemblies — EditMode cannot run `[UnityTest]` play-mode coroutines, and the golden trace needs play mode (it drives `EditorSkipFromGraph`, which early-returns unless `Application.isPlaying`).

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
Notes: `UNITY_INCLUDE_TESTS` keeps both out of player builds; `overrideReferences:true` is required for `precompiledReferences` to bind nunit; the PlayMode asmdef must **not** reference `UnityEditor.TestRunner`. The PlayMode test loads fixtures with `AssetDatabase` under `#if UNITY_EDITOR` (golden trace is an **in-editor PlayMode gate**, run via `-testPlatform PlayMode` in batchmode — still the editor process). **Do not** copy the existing `Tests/Editor/Pitech.XR.ContentDelivery.Editor.Tests.asmdef` template — it uses the deprecated `optionalUnityReferences:["TestAssemblies"]`/`testAssemblies:true` form (PKG-09); migrate it to this modern form too.

### I.2 Folder / file layout
```
Tests/
  Editor/        Pitech.XR.Scenario.Editor.Tests.asmdef + EditMode tests (the P1 net)
    ScenarioGraphIntegrityTests.cs     (Proof A — refs/routes/events; PRIMARY P1 net, §I.0)
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
  Fixtures/Scenarios/                  *.prefab (+ .meta) — real labs or trimmed copies
  Baseline/
    PublicApi.Pitech.XR.txt
    ScriptGuids.json
    CoreEditorTypeLiterals.txt
    GraphSnapshots/<lab>.graph.json    (Proof A per-lab snapshot)
  Golden/                              <fixture>.trace.json  (P2)
```

### I.3 Fixture corpus (each prefab = a `Scenario`+`SceneManager` hierarchy)
In P1 the fixtures are read **statically** by §I.0/I.4(serialized) — they do **not** have to run, so they can be real labs or trimmed copies. P1-minimum (3–5): `linear_timeline_cuecards_event`, `branching_question`, `group_specificchild_question` (locks SCN-11 shared-field routing — highest-risk path), optionally `miniquiz_selection` (incl. SCN-18 "count met / zero correct / within `allowedWrong` → CORRECT") and `conditions_component`. **P2 (when these must become *runnable* for the golden trace):** add one per remaining step Kind, one per `GroupStep.CompleteWhen` mode (all 6), plus `question_debounce` vs `question_group_firstclick` to pin the SCN-03 divergence before P2 unifies them.

### I.4 Golden-trace JSON schema (v1) — *P2-prep (seed only in P1)*
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
**Determinism rules (or byte-compare is useless):** stable key order (as above), `InvariantCulture`, floats formatted `"R"` (round-trip), **no** timestamps / frame numbers / object instance ids, preserve emission order (never sort `trace`/`sideEffects`), line endings LF, trailing newline. The test serializes the recorded run with these rules and `Assert.AreEqual(File.ReadAllText(golden), produced)`. Regenerate goldens only via an explicit `--regen` switch, reviewed as a deliberate change.

### I.5 Golden-trace harness (`GoldenTraceRecorder`) — *P2-prep (seed on one fixture in P1)*
1. `#if UNITY_EDITOR` load the fixture prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`, `Object.Instantiate` it into the play-mode scene.
2. Before `Restart()`, walk the `Scenario.steps` graph and add a recording listener to every `UnityEvent` (`Choice.onSelected`, `SelectionStep.onCorrect/onWrong`, `EventStep.onEnter`, `MiniQuizChoice.onSelected`) and subscribe a stat-mutation probe (wrap/observe `StatsRuntime`); poll `SceneManager.StepIndex` each frame to emit a `trace` row on every change (`from`→`to`, current step `guid`/`Kind`, resolved `branchGuid`).
3. Drive deterministically with the fixture's committed `driver` list via `sceneManager.EditorSkipFromGraph(stepGuid, branchIndex)` — no real pointer input, no timing dependence.
4. Run until `StepIndex == -1` (finished) or a step cap; serialize per I.4; compare.

### I.6 Serialized-diff method (Proof C)
Per fixture prefab `P`: (a) `string b0 = File.ReadAllText(P)`; (b) `AssetDatabase.ForceReserializeAssets(new[]{P})` (or load → `EditorUtility.SetDirty` → `AssetDatabase.SaveAssets`); (c) `string b1 = File.ReadAllText(P)`; (d) `Assert.AreEqual(b0, b1)`. If Unity normalizes formatting on first reserialize, capture that normalized text **once** as the committed baseline and assert subsequent reserializes equal it. **Prefab-instance-with-override variant:** new temp scene → instantiate `P` → set one override (e.g. `title`) → `PrefabUtility.RecordPrefabInstancePropertyModifications` → save scene → assert the `Scenario` block has no dropped `steps` entries, no churned `managedReferences` ids, no changed `m_Script` GUID. CI backstop: `git diff --exit-code -- Tests/Fixtures` after a `ForceReserializeAssets` pass.

### I.7 GUID-stability (`ScriptGuids.json`)
Pins the **MonoScript GUIDs** of every type a prefab/scene references by `m_Script` — i.e. MonoBehaviours/ScriptableObjects: `Scenario`, `SceneManager`, `QuizUIController`, `QuizResultsUIController`, `QuizAsset`, `StatsUIController`, `StatsConfig`, `SelectablesManager`, `SelectionLists`, `SelectableTarget`, `ContentDeliverySpawner`, `ContentDeliveryStatusOverlay`. Format `{ "Pitech.XR.Scenario.SceneManager": "<32hex>", ... }`; test resolves each `MonoScript`'s GUID and asserts equality. **Split rule (load-bearing):** plain `[Serializable]` Step classes are referenced by type-string (ns+asm+name), **not** GUID — so when splitting `Scenario.cs`, the file retaining the `Scenario` MonoBehaviour **keeps `Scenario.cs.meta`'s GUID**, while moved step classes get fresh `.meta` GUIDs (harmless). The test only guards the MonoScript set; it is the trip-wire for "a refactor regenerated a script GUID and nulled every shipped prefab reference."

### I.8 Public-API baseline (additions-only)
`Tests/Baseline/PublicApi.Pitech.XR.txt`: reflect every loaded assembly whose name starts `Pitech.XR.`, enumerate public (and protected-on-non-sealed) types/members, format one stable line each (`Namespace.Type::Member(paramTypes)->returnType`), sort `Ordinal`, write. Test regenerates the live surface and asserts **every baseline line is still present** (removals fail; additions allowed). Update the baseline only as a reviewed, intentional commit. `Tests/Baseline/CoreEditorTypeLiterals.txt`: the inventory of Core.Editor string literals (`Pitech.XR.Quiz.QuizAsset`, `Pitech.XR.Stats.StatsConfig`, `Pitech.XR.Quiz.QuizUIController`, `Pitech.XR.Quiz.QuizResultsUIController`, `Pitech.XR.Stats.StatsUIController`, `Pitech.XR.Scenario.SceneManager`, `Pitech.XR.Scenario.Scenario`, `ScenarioGraphWindow`); test asserts each still resolves (`Type.GetType`/assembly scan). This is the gate that catches a namespace move the ordinary API baseline would miss.

### I.9 Headless entry (the *other* door — for the optional hook + P2 CI, NOT the P1 gate)
The P1 gate is the in-editor button (§I.11). This CLI form runs the **same** suite without a GUI; it exists so a pre-push hook or P2 server CI can attach later with no rework. Not required for P1.
```bash
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform EditMode  -testResults "Logs/editmode.xml"  -logFile "Logs/editmode.log"
# PlayMode (golden trace) — P2 only:
"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<DevKitTestHost>" \
  -runTests -testPlatform PlayMode  -testResults "Logs/playmode.xml"  -logFile "Logs/playmode.log"
```
`-runTests` auto-quits — do **not** add `-quit` (it races the runner). Exit code 0 = all passed; parse the NUnit XML. The EditMode line is what a future hook/CI would call; the PlayMode line is **P2**.

### I.10 Gate model — manual in-editor now, server CI at P2
**P1 (now):** the gate is human-run and in-editor — `DevKit > Evaluate Changes` (§I.11). A developer clicks it and pushes only on a green verdict. No GameCI, no Unity license on a runner, no host-runner — nothing to provision. `.editorconfig`/`.gitattributes` are committed so style is *encoded*; the button (or a quick local check) surfaces violations.
**Optional hardening (any time, still local):** a shared pre-push hook (`core.hooksPath` → `.githooks/pre-push`) that calls §I.9's EditMode line and blocks the push on red — turns "remembered to check" into "can't push red," still 100% local (uses the dev's own Unity license, no server).
**P2:** server CI (GameCI + Unity license) runs §I.9's EditMode **and** PlayMode lines on every PR. Same suite, same commands — the only new thing is *where* it runs. Deferring it keeps P1 free of CI infrastructure entirely.

### I.11 `DevKit > Evaluate Changes` — the manual gate (P1 deliverable)
The developer-facing one-click runner. Editor-only, additive, P1-safe. Lives beside the existing `DevkitHubWindow`.

- **Entry points:** a `[MenuItem("DevKit/Evaluate Changes")]` **and** an "Evaluate Changes" button on a DevKit Hub page (same handler). File: `Editor/Core.Editor/Tools/EvaluateChanges.cs` (or a Hub page action).
- **What it runs:** the EditMode suite via `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi` — `Execute(new ExecutionSettings(new Filter{ testMode = TestMode.EditMode, assemblyNames = new[]{"Pitech.XR.Scenario.Editor.Tests"} }))` — and registers a callback (`ICallbacks`/`RegisterCallbacks`) to collect results. (PlayMode is **excluded** in P1.)
- **Verdict UI:** on completion, summarize to a clear human verdict, not a dot-tree:
  - green → "✅ Evaluate Changes: N checks passed — safe to push."
  - red → list each failure as a plain sentence sourced from the test's message, e.g. "❌ lab `cardiac_triage`: routing guid `g7` on step `g6` points to no existing step (dangling route)." Show in a small `EditorWindow` (or `EditorUtility.DisplayDialog` for the summary + console for detail).
- **Shared core:** put the actual run logic in a static `DevKitChecks.RunEditModeGate()` that both the button and a headless `static int RunAll()` (for `-executeMethod` / hook / P2 CI) call — **one code path, two doors**, so the manual gate and any future automation can never diverge.
- **Growth path (optional, later):** "Evaluate Changes" can become a pre-flight aggregator — EditMode net **+** the `.editorconfig` format check **+** a one-line graph-integrity summary across all labs — so it's the single "is my change clean?" button before any push.
- **Why not just the Test Runner window:** it runs *exactly* the right suite (not all tests / wrong mode), gives a plain-language verdict a non-expert can act on, and is discoverable/branded ("before I push, I click Evaluate Changes"). Friction is what kills a manual gate; this removes it.

---

## WS-5 — `ISceneRunnerControl` seam *(optional; isolated; AFTER WS-0; not part of the proof work)*

**Goal:** give ContentDelivery (and later LabConsole) a typed handle to the runner so P2 can drop string reflection — landed as one small, separately-reviewed, additive commit once the WS-0 baseline is green. **Not** mixed into any WS-0/WS-2/WS-3 commit.

**Exact contract** (new file `Runtime/Core/ISceneRunnerControl.cs`, assembly `Pitech.XR.Core`, namespace `Pitech.XR.Core`):
```csharp
namespace Pitech.XR.Core
{
    /// <summary>Minimal, stable control surface over a scenario runner. Behavior-neutral seam:
    /// implementers forward to existing members; no new behavior.</summary>
    public interface ISceneRunnerControl
    {
        int  CurrentStepIndex { get; }   // forwards SceneManager.StepIndex
        bool AutoStart        { get; set; } // forwards SceneManager.autoStart
        void Restart();                   // forwards SceneManager.Restart()
    }
}
```
`SceneManager` adds `: ISceneRunnerControl` and three forwarding members (`CurrentStepIndex => StepIndex;` etc.) — **no field renamed, nothing made non-public, behavior identical.** Proof: API-additions-only (B) passes (only additions); golden trace (A) unchanged (no path touched); serialized-diff (C) unchanged (no serialized field added — the interface and forwarders are not serialized). **Constraint:** keep it exactly these three members in P1 — do **not** widen it toward flow-store/ledger concepts (that is after P1 and would be premature lock-in).

---

# Appendices

## Appendix A — DevKit Inventory & Disposition Census (proposed **WS-pre**, run before any P1 code change)

A census comes first because every later workstream (the menu-root unification, the file splits, the dead-code deletions, the namespace fixes) edits a *shared, cross-referenced* surface — menu paths are reflected by string from `DocsPage`/`ContentDeliverySpawner`, GUIDs are pinned by lab prefabs, and types move between files — so until every surface is enumerated with its exact `file:line`, disposition, and owning workstream, no edit can be proven safe. This census **is** that enumeration: do it once, up front, and every subsequent WS becomes mechanical and surprise-free.

## Menu-root unification (the single normalization that touches the most rows)

The package today mixes **three** menu-root prefixes inconsistently. The locked target (WS-6) is a single root token **`Pi tech`** everywhere, with the home window branded **DevKit Hub**:

| Current prefix | Where it appears today | Target (WS-6) |
|---|---|---|
| `Pi tech/` | Top-bar `[MenuItem]` windows/commands/tools (Hub, Scenario Graph, Dev Blocks, Addressables Builder, Scene/…, Quiz/…, Tools/…) | **Keep token.** Re-group task-first; fix the stray `0x85` mojibake byte in the Scene Categories path (WS-1). |
| `Pi tech XR/` | **All** runtime `[AddComponentMenu]` paths (Scenario, Content Delivery, Quiz, Analytics, Interactables) | **Drop the ` XR` suffix** → `Pi tech/<Module>/…` so Add Component reads consistently. |
| `GameObject/Pi tech/` | `Make Grabbable` GameObject context entry | **Keep** (already correct token; separate menu tree). |
| `Pi tech/` (CreateAssetMenu) | Stats Config, Quiz Asset, Dev Blocks, Content Delivery configs | **Keep token**; review only for task-first grouping. |

Two structural corrections ride along with the token unification:
- **ORG-03 module misplacement:** `SelectableTarget`, `SelectablesManager`, `MetaSelectRelay` are Interactables types parked under `Pi tech XR/Scenario/` — **move** to `…/Interactables/`.
- **Caller fidelity:** any rename of `Pi tech/Scenario Graph`, `Pi tech/Dev Blocks`, `Pi tech/DevKit` **must** update the `EditorApplication.ExecuteMenuItem` callers in `DocsPage.cs` (5 calls) and leave Meta's own `GameObject/Interaction SDK/Add Grab Interaction` (invoked from `MakeGrabbableWizard`) untouched — it is not ours.

---

### (A) Top-bar menu surfaces — windows / commands / wizards

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **DevkitHubWindow** — `[MenuItem("Pi tech/DevKit")]`; main cockpit hosting the 4 pages | `Editor/Core.Editor/Hub/DevkitHubWindow.cs:27` | **rename.** Becomes the *DevKit Hub* home; title/branding → "DevKit Hub", gains launch tiles + repair tools + Evaluate Changes + "Add Scenario to Scene". `DocsPage` `ExecuteMenuItem('Pi tech/DevKit')` tracks any path change. *(WS-6)* |
| **ScenarioGraphWindow** — `[MenuItem("Pi tech/Scenario Graph")]`; GraphView node editor | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:147` | **split.** 6 contained types → per-file + namespace wrap. Kept under `Pi tech`, added as a Hub tile; `DocsPage` callers (x2, lines 42/77) updated on rename. *(WS-3, WS-6)* |
| **StepEditWindow** — per-step edit popup; opened from a graph node (no `[MenuItem]`) | `Editor/Scenario.Editor/ScenarioGraphWindow.cs:4287` | **split.** Extracted to its own file as part of the 6-type split; no menu/trigger change. *(WS-3)* |
| **DevBlocksWindow** — `[MenuItem("Pi tech/Dev Blocks")]`; browse/instantiate DevBlock prefabs | `Editor/Core.Editor/Tools/DevBlocksWindow.cs:41` | **keep.** Kept under `Pi tech` + Hub tile; `DocsPage` caller (151) updated on rename; dead `DevkitWidgets` helpers pruned. *(WS-2, WS-6)* |
| **AddressablesBuilderWindow** — `[MenuItem("Pi tech/Addressables Builder")]`; Addressables/CCD pipeline | `Editor/Core.Editor/Tools/AddressablesBuilderWindow.cs:59` | **keep.** Kept under `Pi tech` + Hub tile; opened programmatically from Guided Setup/Settings. **Deferred:** relocation to a `…Core.Editor.UI` leaf asmdef → P2. *(WS-6)* |
| **SceneCategoriesWindow** — `[MenuItem("Pi tech/Scene/Create Scene Categories…")]`; create scene anchors | `Editor/Core.Editor/Tools/SceneCategoriesWindow.cs:16` | **rename.** Strip the stray `0x85` mojibake byte from the menu path; kept under `Pi tech`, grouped task-first + Hub tile. *(WS-1, WS-6)* |
| **Copy Default Quiz UI Prefabs to Project (Editable)** — `[MenuItem("Pi tech/Quiz/…")]` | `Editor/Quiz.Editor/QuizDefaultUIPrefabFactory.cs:10` | **keep.** Already `Pi tech/`; placed in task-first Quiz grouping, no token change. *(WS-6)* |

### (B) GameObject + Add Component + Create Asset surfaces (non-runtime-component entries)

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **Make Grabbable** — `[MenuItem("GameObject/Pi tech/Make Grabbable", false, 10)]`; opens the wizard | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:19` | **keep.** Kept under `GameObject/Pi tech/`. Its internal `ExecuteMenuItem('GameObject/Interaction SDK/Add Grab Interaction')` is **Meta's** — left as-is. *(WS-6)* |
| **MakeGrabbableWindow** — utility wizard (ShowUtility; no `[MenuItem]`) | `Editor/Interactables.Editor/MakeGrabbableWizard.cs:35` | **keep.** Only OUR entry path renames; Meta's `ExecuteMenuItem` untouched; no behavior change. *(WS-6)* |
| **StatsConfig** — `[CreateAssetMenu("Pi tech/Stats Config")]` | `Runtime/Stats/StatsConfig.cs:7` | **keep.** Already `Pi tech/`; grouping review only (e.g. `Pi tech/Stats/Config`). *(WS-6)* |
| **QuizAsset** — `[CreateAssetMenu("Pi tech/Quiz Asset")]` | `Runtime/Quiz/QuizAsset.cs:7` | **keep.** Already `Pi tech/`; grouping review only. *(WS-6)* |
| **DevBlockItem** — `[CreateAssetMenu("Pi tech/Dev Blocks/Dev Block")]` | `Editor/Core.Editor/DevBlocks/DevBlockItem.cs:12` | **keep.** Already consistent; no change. *(WS-6)* |
| **AddressablesModuleConfig** — `[CreateAssetMenu("Pi tech/Content Delivery/Addressables Module Config")]` | `Runtime/ContentDelivery/AddressablesModuleConfig.cs:29` | **keep.** Already consistent; edited via Settings page. **Deferred:** remote-URL rewriter fix → after P1. *(WS-6)* |
| **AddressablesBuildCatalog** — `[CreateAssetMenu("Pi tech/Content Delivery/Addressables Build Catalog")]` | `Runtime/ContentDelivery/AddressablesBuildCatalog.cs:28` | **keep.** Already consistent; no change. *(WS-6)* |

### (C) DevKit Hub — window + pages + services

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **DashboardPage** — status pills + Project Setup 2×2 cards | `Editor/Core.Editor/Pages/DashboardPage.cs:8` | **keep.** Part of the rebuilt cockpit: task-first grouping, launch tiles, Evaluate Changes, repair-tool surfacing. *(WS-6)* |
| **GuidedSetupPage** — scene-agnostic setup wizard cards | `Editor/Core.Editor/Pages/GuidedSetupPage.cs:15` | **keep.** Opens `AddressablesBuilderWindow.Open` directly (decoupled); no token change. *(WS-6)* |
| **DocsPage** — how-to tiles whose buttons `ExecuteMenuItem` | `Editor/Core.Editor/Pages/DocsPage.cs:8` | **keep (CRITICAL caller).** Holds the menu callers that must track renames: `Pi tech/Scenario Graph` (42/77), `Pi tech/Dev Blocks` (151), `Pi tech/DevKit` (170), plus `Window/*`. Docs content refresh. *(WS-4, WS-6)* |
| **SettingsPage** — module flags + `AddressablesModuleConfig` editing | `Editor/Core.Editor/Pages/SettingsPage.cs:11` | **keep.** Opens `AddressablesBuilderWindow.Open` directly; no token change. *(WS-6)* |
| **IDevkitPage** — page contract (`Title` + `BuildUI`) | `Editor/Core.Editor/Hub/IDevkitPage.cs:6` | **keep.** Contract unchanged; underpins the rebuilt cockpit. *(WS-6)* |
| **DevkitContext** — version/icons/module-presence flags | `Editor/Core.Editor/Hub/DevkitContext.cs:1` | **keep.** Backs DevKit Hub branding/title/version. *(WS-6)* |
| **DevkitTheme** — UI Toolkit styling/widget factory | `Editor/Core.Editor/Hub/DevkitTheme.cs:1` | **keep.** Dead `Styles.Primary` removed; theme reused by cockpit. *(WS-2, WS-6)* |
| **DevkitWidgets** — card/tile/chip/pill composites | `Editor/Core.Editor/UI/DevkitWidgets.cs:1` | **keep.** Provably-unused helpers deleted; remaining widgets power launch tiles. *(WS-2, WS-6)* |
| **GuidedSetupService** — reflection-decoupled scene wiring | `Editor/Core.Editor/Services/GuidedSetupService.cs:1` | **keep.** rootNamespace/global-namespace fixes if applicable; exercised by tests. *(WS-0, WS-3)* |
| **ProjectHealthService** — folders/scene/settings/modules checks | `Editor/Core.Editor/Services/ProjectHealthService.cs:1` | **keep.** Behavior unchanged; surfaced by Dashboard. *(WS-0, WS-6)* |
| **ProjectSetupService** — scaffold folders + Main scene | `Editor/Core.Editor/Services/ProjectSetupService.cs:1` | **keep.** Unchanged; invoked from Dashboard cards. *(WS-6)* |
| **QuizService** — create QuizAsset + wire default UI | `Editor/Core.Editor/Services/QuizService.cs:1` | **keep.** Unchanged; invoked from Hub pages. *(WS-6)* |
| **ScenarioService** — create Scenario GO + open graph | `Editor/Core.Editor/Services/ScenarioService.cs:1` | **keep.** Candidate home for the new "Add Scenario to Scene" command logic; otherwise unchanged. *(WS-6)* |
| **SceneCategoriesService** — ensure scene category anchors | `Editor/Core.Editor/Services/SceneCategoriesService.cs:1` | **keep.** Unchanged; backs the mojibake-fixed window. *(WS-6)* |
| **SceneManagerService** — create SceneManager in scene | `Editor/Core.Editor/Services/SceneManagerService.cs:1` | **keep.** Unchanged; invoked from Dashboard Scene card. *(WS-6)* |
| **StatsService** — create StatsConfig asset | `Editor/Core.Editor/Services/StatsService.cs:1` | **keep.** Unchanged; invoked from Hub pages. *(WS-6)* |

### (D) Editor inspectors, repair tools & GUID services

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **ScenarioEditor** — `[CustomEditor(typeof(Scenario))]`; 11 step drawers + Styles | `Editor/Scenario.Editor/ScenarioEditor.cs:12` | **split.** 11 drawers + Styles → per-file (same namespace/asmdef, each `.meta`). **Deferred:** undo-correctness → after P1. *(WS-3)* |
| **SceneManagerEditor** — `[CustomEditor(typeof(SceneManager), true)]`; reflection field access | `Editor/Scenario.Editor/SceneManagerEditor.cs:14` | **rename.** reflection→typed access (behavior-neutral). **Deferred:** deeper reflection/Find removal via `ISceneRunnerControl` → P2. *(WS-3)* |
| **StatsUIControllerEditor** — `[CustomEditor(typeof(StatsUIController))]` | `Editor/Stats.Editor/StatsUIControllerEditor.cs:10` | **keep.** Dead try/catch in `StatsUIController.Init` removed; inspector unchanged. *(WS-2)* |
| **StatsConfigEditor** — `[CustomEditor(typeof(StatsConfig))]` | `Editor/Stats.Editor/StatsConfigEditor.cs:10` | **keep.** Formatting/namespace hygiene only. *(WS-1, WS-3)* |
| **QuizAssetEditor** — `[CustomEditor(typeof(QuizAsset))]` | `Editor/Quiz.Editor/QuizAssetEditor.cs:11` | **keep.** Formatting/comment-language normalization only. *(WS-1)* |
| **SelectablesManagerEditor** — `[CustomEditor(typeof(SelectablesManager))]` | `Editor/Interactables.Editor/SelectablesManagerEditor.cs:7` | **rename.** Global-namespace fix → wrap into `Pitech.XR.Interactables.Editor`. *(WS-3)* |
| **SelectionListsEditor** — `[CustomEditor(typeof(SelectionLists))]` | `Editor/Interactables.Editor/SelectionListsEditor.cs:9` | **keep.** Formatting/namespace hygiene. *(WS-1, WS-3)* |
| **ContentDeliverySpawnerEditor** — `[CustomEditor(typeof(ContentDeliverySpawner), true)]` | `Editor/ContentDelivery.Editor/ContentDeliverySpawnerEditor.cs:7` | **keep.** Formatting only. *(WS-1)* |
| **Fix Missing DevKit Script References on Selection** — `[MenuItem("Pi tech/Tools/…", false, 502)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:20` | **keep.** Kept under `Pi tech/Tools`, **surfaced in the Hub** as a repair tool. **Deferred:** undo-correctness → after P1. *(WS-6)* |
| **Repair DevKit script GUIDs in selected prefab/scene (YAML only)** — `[MenuItem("Pi tech/Tools/…", false, 503)]` | `Editor/Scenario.Editor/DevKitFixMissingScriptRefs.cs:75` | **keep.** Kept under `Pi tech/Tools`, surfaced in the Hub alongside the selection-fix command. *(WS-6)* |
| **DevKitYamlScriptGuidRepair** — static GUID rewriter backing both repair commands | `Editor/Scenario.Editor/DevKitYamlScriptGuidRepair.cs:20` | **keep.** Covered by the GUID-repair test; comment/EOL normalization only, behavior unchanged. *(WS-0, WS-1)* |
| **ScenarioEditorUtil** — internal step-GUID helper | `Editor/Scenario.Editor/ScenarioEditorUtil.cs:9` | **delete.** Provably-dead code (named delete target). *(WS-2)* |

### (E) Runtime components — `[AddComponentMenu]` (token unification `Pi tech XR/` → `Pi tech/`)

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **Scenario** — `Pi tech XR/Scenario/Scenario`; step-graph holder | `Runtime/Scenario/Scenario.cs:569` | **rename.** Menu → `Pi tech/Scenario/Scenario`; step types extracted to `Steps/<Type>.cs`. *(WS-3, WS-6)* |
| **SceneManager** — `Pi tech XR/Scenario/Scene Manager`; runtime interpreter | `Runtime/Scenario/SceneManager.cs:20` | **rename.** Menu → `Pi tech/Scenario/Scene Manager`; delete dead `EvalCompare`; implements optional `ISceneRunnerControl`. **Deferred:** reflection/Find removal → P2. *(WS-2, WS-5, WS-6)* |
| **SelectableTarget** — `Pi tech XR/Scenario/Selectable Target` *(misplaced)* | `Runtime/Interactables/SelectableTarget.cs:18` | **move.** ORG-03: → `Pi tech/Interactables/Selectable Target`. *(WS-3, WS-6)* |
| **SelectablesManager** — `Pi tech XR/Scenario/Selectables Manager (…)` *(misplaced)* | `Runtime/Interactables/SelectablesManager.cs:8` | **move.** → `Pi tech/Interactables/Selectables Manager (Meta VR Ready + AR Safe)`. *(WS-3, WS-6)* |
| **MetaSelectRelay** — `Pi tech XR/Scenario/Meta Select Relay (optional)` *(misplaced)* | `Runtime/Interactables/SelectablesManager.cs:327` | **move.** → `Pi tech/Interactables/Meta Select Relay (optional)`. *(WS-3, WS-6)* |
| **SelectionLists** — `Pi tech XR/Interactables/Selection Lists (Controller)` | `Runtime/Interactables/SelectionLists.cs:81` | **rename.** Already in `…/Interactables/`; unify root → `Pi tech/Interactables/Selection Lists (Controller)`. *(WS-6)* |
| **QuizUIController** — `Pi tech XR/Quiz/Quiz UI Controller` | `Runtime/Quiz/QuizUIController.cs:9` | **rename.** → `Pi tech/Quiz/Quiz UI Controller`. *(WS-6)* |
| **QuizResultsUIController** — `Pi tech XR/Quiz/Quiz Results UI Controller` | `Runtime/Quiz/QuizResultsUIController.cs:8` | **rename.** → `Pi tech/Quiz/Quiz Results UI Controller`. *(WS-6)* |
| **AddressablesBootstrapper** — `Pi tech XR/Content Delivery/Addressables Bootstrapper` | `Runtime/ContentDelivery/AddressablesBootstrapper.cs:7` | **rename.** → `Pi tech/Content Delivery/Addressables Bootstrapper`. *(WS-6)* |
| **AttemptReconciliationBridge** — `Pi tech XR/Content Delivery/Attempt Reconciliation Bridge` | `Runtime/ContentDelivery/AttemptReconciliationBridge.cs:9` | **rename.** → `Pi tech/Content Delivery/…`. *(WS-6)* |
| **BridgeLaunchContextReceiver** — `Pi tech XR/Content Delivery/Bridge Launch Context Receiver` | `Runtime/ContentDelivery/BridgeLaunchContextReceiver.cs:11` | **rename.** → `Pi tech/Content Delivery/…`. *(WS-6)* |
| **SerializedLaunchContextProvider** — `Pi tech XR/Content Delivery/Serialized Launch Context Provider` | `Runtime/ContentDelivery/SerializedLaunchContextProvider.cs:8` | **rename.** → `Pi tech/Content Delivery/…`. *(WS-6)* |
| **LaunchContextReporter** — `Pi tech XR/Content Delivery/Launch Context Reporter` | `Runtime/ContentDelivery/LaunchContextReporter.cs:34` | **rename.** → `Pi tech/Content Delivery/…`. *(WS-6)* |
| **ContentDeliveryStatusOverlay** — `Pi tech XR/Content Delivery/Content Delivery Status Overlay` | `Runtime/ContentDelivery/ContentDeliveryStatusOverlay.cs:14` | **rename.** → `Pi tech/Content Delivery/…`. *(WS-6)* |
| **ContentDeliverySpawner** — `Pi tech XR/Content Delivery/Content Delivery Spawner` | `Runtime/ContentDelivery/ContentDeliverySpawner.cs:33` | **rename.** → `Pi tech/Content Delivery/Content Delivery Spawner`. *(WS-6)* |
| **RuntimeTelemetryAdapter** — `Pi tech XR/Analytics/Runtime Telemetry Adapter` | `Runtime/ContentDelivery/Analytics/RuntimeTelemetryAdapter.cs:80` | **rename.** → `Pi tech/Analytics/Runtime Telemetry Adapter`. *(WS-6)* |
| **TelemetryAutoWirer** — `Pi tech XR/Analytics/Telemetry Auto Wirer` | `Runtime/ContentDelivery/Analytics/TelemetryAutoWirer.cs:13` | **rename.** → `Pi tech/Analytics/Telemetry Auto Wirer`. *(WS-6)* |
| **LaunchContextProviders (placeholder)** — empty back-compat file, no types | `Runtime/ContentDelivery/LaunchContextProviders.cs:1` | **delete.** Named dead-code delete target (carry `.meta` removal). *(WS-2)* |

> **Non-menu runtime services (kept, P1):** `AddressablesService`, `AddressablesBuildService`, `AddressablesValidationService`, `PublishReportService`, `AddressablesAdapterResolver`, `ContentDeliveryCapability` — all `Editor/ContentDelivery.Editor/…`, **keep** (P1 behavior unchanged; some are WS-0 test targets). **`HealthOnAddressablesAdapter`** (`…/Services/HealthOnAddressablesAdapter.cs:1`) — **defer** (adapter de-coupling → P2, untouched in P1).

### (F) The `[SerializeReference]` data model (`Scenario.cs`)

All step types **split** under WS-3 into `Runtime/Scenario/Steps/<Type>.cs` — **same `namespace Pitech.XR.Scenario`, same asmdef, each carrying its `.meta`** — and are covered by the WS-0 serialized/graph-integrity proofs that assert managed-reference stability. Behavior-neutral.

| What it is | Where (file:line) | What happens (+ workstream) |
|---|---|---|
| **Step (abstract base)** — root of the SerializeReference graph (guid/graphPos/Kind) | `Runtime/Scenario/Scenario.cs:14` | **split** → `Steps/Step.cs`. *(WS-3, WS-0)* |
| **TimelineStep** — PlayableDirector playback | `Runtime/Scenario/Scenario.cs:23` | **split** → `Steps/TimelineStep.cs`. *(WS-3)* |
| **CueCardsStep** — ordered cue cards (+ nested `AdvanceMode`) | `Runtime/Scenario/Scenario.cs:37` | **split** → `Steps/CueCardsStep.cs`. *(WS-3)* |
| **QuestionStep** — branching `Choice` buttons | `Runtime/Scenario/Scenario.cs:104` | **split** → `Steps/QuestionStep.cs` (+ `Choice`). *(WS-3)* |
| **MiniQuizStep** — multi-question single panel | `Runtime/Scenario/Scenario.cs:166` | **split** → `Steps/MiniQuizStep.cs` (+ Choice/Question/Outcome/CompleteMode). *(WS-3)* |
| **SelectionStep** — SelectionLists evaluation (+ CompleteMode) | `Runtime/Scenario/Scenario.cs:203` | **split** → `Steps/SelectionStep.cs`. *(WS-3)* |
| **InsertStep** — physics insertion into a slot | `Runtime/Scenario/Scenario.cs:264` | **split** → `Steps/InsertStep.cs`. *(WS-3)* |
| **EventStep** — UnityEvent + optional delay | `Runtime/Scenario/Scenario.cs:305` | **split** → `Steps/EventStep.cs`. *(WS-3)* |
| **ConditionsStep** — read one value, ordered outcomes (+ `ConditionsEvaluator`) | `Runtime/Scenario/Scenario.cs:341` | **split** → `Steps/ConditionsStep.cs`. `ConditionsEvaluator` is a prime WS-0 unit-test target. *(WS-3, WS-0)* |
| **GroupStep** — nested concurrent steps + completion modes | `Runtime/Scenario/Scenario.cs:410` | **split** → `Steps/GroupStep.cs` (+ CompleteWhen/ChildRequirement/MultiConditionBranch). Graph-integrity tests cover nested SerializeReference. *(WS-3, WS-0)* |

## Disposition summary (this census IS WS-pre)

- **keep — 28** (most editor services, pages, kept inspectors, already-correct CreateAssetMenu/Tools entries).
- **rename — 16** (1 Hub window + Scene Categories mojibake fix + 2 inspectors + 11 runtime `AddComponentMenu` token unifications + SceneManager).
- **move — 3** (ORG-03: `SelectableTarget`, `SelectablesManager`, `MetaSelectRelay` from Scenario → Interactables menu group).
- **split — 13** (Scenario data model: Step base + 10 step types + ScenarioGraphWindow/StepEditWindow + ScenarioEditor).
- **delete — 3** (`ScenarioEditorUtil`, empty `LaunchContextProviders.cs`, plus the provably-dead `SceneManager.EvalCompare` block — code-level, WS-2).
- **defer — 2+** (`HealthOnAddressablesAdapter` → P2; AddressablesBuilderWindow asmdef relocation, undo-correctness, reflection removal → after P1 / P2).

**Recommendation: run this census *first*, as WS-pre, before any P1 code lands.** It is the safety net for every later workstream — the menu-root unification touches reflected `ExecuteMenuItem` callers, the splits move types across files while pinning `.meta`/GUIDs, and the deletes must be proven caller-free. Enumerate once, and every WS-1…WS-6 edit becomes mechanical, reviewable, and surprise-free.

---

## Appendix B — SceneManager today, and its transition to LabConsole

## (a) What `SceneManager` IS now

**Essence.** `SceneManager` (`Runtime/Scenario/SceneManager.cs`, ~2,505 lines, `Pitech.XR.Scenario`) is the **runtime scenario interpreter**: one MonoBehaviour that, on `Start`/`Restart`, walks `Scenario.steps` in a single `Run()` coroutine, dispatches each `Step` subtype to a per-type `RunXxx` coroutine (with a parallel `RunXxxGroup` variant for `GroupStep` concurrency), polls input/conditions/UI to decide completion and the next-step GUID, and brokers stats, quiz, selection, and editor-skip integration — all from a serialized config surface and a tiny public API. It does not merely *use* the runtime; it **is** the runtime.

**The ~8 (really 11) responsibilities, with line ranges:**

1. **Config & feature wiring** (23–61) — serialized scenario/stats/quiz/interactables/contentDelivery/labContentRoot fields; every subsystem optional.
2. **Awake setup** (83–124) — stats binding (`_statsBound`), backfill `selectionLists.selectables`, disable `selectables.pickingEnabled`, `DeactivateAllVisuals`, auto-discover + hide quiz panels under `labContentRoot ?? transform.root`.
3. **Lifecycle / pointer entry** (132–141) — `Start`→`autoStart`→`Restart`; `Restart` stops/relaunches `Run()`.
4. **Graph traversal / step pointer** (143–245) — the central `Run()` loop, `StepIndex`, `FindIndexByGuid`, branchGuid→next-index.
5. **Step execution** (247–1348, 1617–2470) — the 12 `RunXxx` runners **plus** the 12 `RunXxxGroup` variants (~1,000 largely-duplicated lines) and `EditorSkipFromGraph`.
6. **Input polling** (1350–1450) — new + legacy Input System, VR-no-pointer early-outs.
7. **Reflection condition evaluation** (1053–1182) — `GetValueFromComponent` et al.; note the **dead** private `EvalCompare` (1168–1182).
8. **Stat mutation** (1184–1206, 710–731) — `ApplyEffects`, `ApplyQuizStats`.
9. **Quiz/results orchestration** (636–799, 978–988) — `RunQuiz`/`RunQuizResults`/`GetOrCreateQuizSession`.
10. **Visual activation** (1452–1522) — `HidePanelRoot`, `DeactivateAllVisuals[Recursive]`, the "only current step visible" invariant.
11. **Group concurrency** (1583–1830) — cancel tokens, picking refcount, `_groupExit*` shared-field routing.

**Public contract that labs / ContentDelivery / editor depend on:**

- **Labs** assign `scenario`, `autoStart`, the optional stats/quiz/selection refs, `labContentRoot`; call the selection bridges `ActivateSelectionList(int)` / `ActivateSelectionListByName(string)` / `CompleteSelection()` / `RetrySelection()` (127–130) from Timeline signals/UnityEvents.
- **ContentDelivery** resolves the manager by `GetType().FullName=="Pitech.XR.Scenario.SceneManager"` (`ContentDeliverySpawner.cs:1134`) and **string-reflects** `autoStart` (1151/1158), `Restart` (1172) — *one-directional, no compile dependency*. Member renames silently break the spawn flow.
- **Editor** — `ScenarioGraphWindow` calls `EditorSkipFromGraph(guid, branchIndex)` (1588) and reads `StepIndex` (1857) to highlight nodes; `SceneManagerEditor` binds serialized props by name and reflects `scenario`(367)/`StepIndex`(467)/`Restart`(505).
- **Lab prefabs** reference the component by **Script GUID `2d431a49d183e9c428369f7f758f75cd`**, and rely on `FormerlySerializedAs` on `defaultQuiz`/`quizPanel`/`quizResultsPanel`.

**What's load-bearing (must be preserved verbatim):** the `FallbackGuid '' == linear-next` contract (1000–1004); the exact `Run()` type-dispatch order and branchGuid-assignment pattern; `StepIndex` semantics (`{get;private set;}`, `-1` idle/finished, reflected *by name*); the public symbol **names** (reflected as strings); the `FormerlySerializedAs` mappings; the pinned MonoScript GUID; the `_editorSkip`/`_editorSkipBranchIndex` integer encoding (choice index / `-1` default / `-2` correct / `-3` wrong / outcome index); the `DeactivateAllVisuals` invariant; the `selectables.pickingEnabled` refcount discipline; the `_groupExit*` resolve→consume→reset handshake; and the deliberate use of `Time.unscaledDeltaTime` for real-time waits.

**The God-class problem, concrete but fair.** Eleven distinct concerns live in one type; side effects (`choice.onSelected.Invoke()`, `ApplyEffects`, `StepIndex` changes) are **silent** — there is no observation surface and no `OnDisable`/`OnDestroy` cleanup. The `RunXxx`/`RunXxxGroup` variants are ~1,000 duplicated lines that are **not** bit-identical (`RunQuestion` debounces; `RunQuestionGroup` is first-click-wins), so they cannot be naively unified without a behavior change. *Fair caveat:* this monolith is also **battle-tested and exhaustively contractual** — every shipped lab and the editor depend on its exact names, GUID, and routing semantics. The problem is maintainability and extensibility, not correctness; the fix must therefore be *behavior-neutral and proof-gated*, never a rewrite.

## (b) The transition, phase by phase

The governing law throughout: **a move is admitted only if it passes golden-trace + API-additions-only + serialized-diff-zero**, and the ordering invariant is **observe before act** — the flow-store (fact portability) and the ledger (observation) are built *first*; LabConsole (actuation) is built *last*.

**P1 — SceneManager is LOCKED.** P1's job is *not* to shrink SceneManager. The runner stays whole, byte-for-byte in execution: `Run()`, all 12 `RunXxx`, all `RunXxxGroup`, `_groupExit*` routing, `EditorSkipFromGraph`, input polling, condition eval, `ApplyEffects`, visuals — **all remain inside**. Only neutral edits land: comment/encoding normalization (WS-1), provably-dead-code deletion (WS-2: the dead `EvalCompare`), and the `Scenario.cs` data-model split (WS-3 — same namespace/asmdef, `.meta` carried). *What appears:* the WS-0 equivalence harness (graph-integrity, serialized/GUID, API-additions proofs + a *seeded* golden-trace harness for P2) and the `DevKit > Evaluate Changes` gate. *One forward seam (WS-5, optional):* `ISceneRunnerControl` in `Pitech.XR.Core` — exactly three members (`CurrentStepIndex`→`StepIndex`, `AutoStart`→`autoStart`, `Restart()`), the first thin handle over the runner; it must **not** be widened toward flow-store/ledger yet. *Compatibility:* total — every shipped lab plays identically; API additions-only; GUIDs pinned.

**after P1 — SceneManager is WRAPPED (additively), still authoritative.** The runner stays put and keeps owning the pointer; new connective tissue arrives as opt-in, compatibility-switched additions. *What appears:* (1) `IScenarioFlowStore` + `LocalScenarioFlowStore` — durable completion facts (`scenario.step.<guid>.done/outcome/completedBy/completedAtTick`); behind a compatibility switch the runner can `CompleteStep(...)` *in addition to* its index advance (no-op on no-MP). (2) `NetworkedStatesScenarioFlowStore` adapter — keeps the existing `NetworkStateManager` as the transitional backend behind the interface. (3) `LabEventLedger` minimal runtime — the observation path; lifecycle vocabulary (`scenario.started`, `step.entered/exited/completed`, `object.action`, `quiz.answered`…) defined, events received; cloud export not yet mandatory. The step-completion *fact* and the *observation surface* begin to be mirrored out — but the runner still computes `idx` locally from the graph. *Compatibility:* a lab that ignores all of it runs unchanged; these are additive seams, not required wiring.

**P2 — SceneManager is EXTRACTED behind a FACADE.** Now the God-class cracks — *because* P1's golden-trace harness (completed here with the full fixture corpus) can finally prove byte-equivalence. The 24 `RunXxx`/`RunXxxGroup` runners → a **dispatch registry of `IStepRunner`s** (one per Kind), reconciling the debounce-vs-first-click divergence as a *deliberate, golden-trace-proven* change; graph traversal → a separated **scenario execution model** that consults `IScenarioFlowStore` for real (no longer behind a switch); condition eval, input, visuals, stat application → focused services. `SceneManager` *becomes* a **thin facade host MonoBehaviour**: same type, same GUID, same serialized fields, same public methods — but internally it *delegates* (`Restart()` starts the extracted runner; `StepIndex` reflects the execution model; `EditorSkipFromGraph` forwards to the driver). `ISceneRunnerControl` is now backed by the real runner, and ContentDelivery's string-reflection is replaced by `ISceneRunnerControl` calls. *Compatibility:* preserved via the facade, proven by the now-complete golden trace; no lab re-authoring.

**P3 — SceneManager is FRONTED (the runner gains a semantic master).** The extracted runner is left intact; **LabConsole is placed *in front of* it.** *What appears:* (1) `LabConsole` runtime — professor controls, lab parameters, scenario commands, *validated* action requests, authority-safe paths, replicated *semantic* state; it owns the semantic action contract (but does **not** retroactively force early step completions/object interactions to route through it). (2) Typed Fusion replication — `[Networked]` fields, explicit bindings, late-join-safe snapshots; this **swaps the backend *under* the flow-store bridge** (`NetworkedStatesScenarioFlowStore` → Fusion) *without invalidating the bridge*, transparent to the runner, which only ever saw `IScenarioFlowStore`. External mutation of the lab is now expected to arrive *through LabConsole*, validated, rather than by poking the runner. *Compatibility:* a single-player no-LabConsole lab still uses `LocalScenarioFlowStore` and runs exactly as in P2.

**P5+ — SceneManager is PRESERVED; analytics & VICKY grow purely on the seams.** P4 (authoring/localization) and P5 (analytics cloud, dashboards, replay) leave the runner contract untouched — P5 consumes the **`LabEventLedger`** stream built in after P1 (`Runtime event → Ledger → exporter → host cloud adapter`); only new *consumers* appear, no provider hard-coded. **P6 (Observer):** VICKY gets **read-only** access from the ledger + flow-store facts — no actuation. **P7 (Action Pilot + 1.0 lock):** VICKY gains **gated actuation exclusively through LabConsole** — it issues a *semantic action request*, LabConsole validates it, and only then drives the runner via `ISceneRunnerControl`/scenario commands. VICKY never touches the runner directly. Observation (ledger) and control (LabConsole) stay separate paths by design.

## (c) End-state: the four concepts, distinguished — and what SceneManager *is*

| Concept | Role | Direction vs. the runner |
|---|---|---|
| **The runner** (extracted SceneManager core) | The *interpreter* — walks the graph, executes the current step, decides the next. | The engine; pulls facts from the store, pushes observations to the ledger. |
| **`IScenarioFlowStore`** | **Fact portability** — `IsStepComplete`/`GetStepOutcome`/`CompleteStep` + `StepFactChanged` over durable completion facts; **replaces the step *pointer* as the unit of sync** (sync facts, not `CurrentStepIndex`). | *Below* the runner; absorbs the entire networking-backend swap (Local → NetworkedStates → typed Fusion) with **zero runner changes**. |
| **`LabEventLedger`** | **Observation** — append-only event stream; read-only w.r.t. the lab; feeds analytics, dashboards, replay, VICKY-watching. | *Alongside/downstream*; listens, never actuates. |
| **`LabConsole`** | **Semantic control / actuation** — professor controls, lab parameters, scenario commands, *validated* action requests, replicated semantic state; the only sanctioned outside-in mutation path. | *In front of* the runner; the only thing that can change the lab from outside. |

**So, once LabConsole exists, what is SceneManager? Be decisive:**

- **It survives as a thin compatibility facade *through the transition* — and is ultimately *replaced* via a migration.** The facade is the *bridge*, not a permanent fixture: every shipped lab prefab references the `SceneManager` MonoScript by the GUID pinned in P1, so the type/component/serialized-fields/public-methods (`Restart`, `StepIndex`, `EditorSkipFromGraph`, the selection bridges, `GetOrCreateQuizSession`) persist as a compatibility surface — the architecture's "keep old APIs as facades" rule — *while* LabConsole + the extracted runner become the real engine. **The destination is full replacement:** at/after the **1.0 lock (P7)** a **migration tool** converts SceneManager-based labs to LabConsole-native; once HealthOn's labs (effectively the only consumer, which you control) are migrated, **SceneManager is deprecated and removed.** This stays compatible with the architecture's one hard rule — *migration is never **forced***: a migration you *offer* and then run on your own labs replaces SceneManager without a flag-day. **Facade = bridge, migration = mechanism, replacement = destination.**
- **It becomes a thin runner behind `IScenarioFlowStore` — but *indirectly*.** SceneManager-the-facade no longer *contains* the runtime; it *hosts and delegates to* an extracted runner, and **that runner's flow is mediated by `IScenarioFlowStore`** (facts, not pointer) and **its observations by `LabEventLedger`**. The runner is the thing that "sits behind the flow-store"; SceneManager is the stable shell labs still see.
- **The three concepts never merge into the runner — they bracket it:** `IScenarioFlowStore` *below* (portable step facts replacing the index pointer), `LabEventLedger` *alongside/downstream* (observation), `LabConsole` *above* (semantic, validated actuation). The runner knows none of them concretely — only the flow-store interface, the ledger sink, and a command surface (`ISceneRunnerControl`/scenario commands) that LabConsole drives.

**The throughline:** P1 locks the God-class, after P1 brackets it with observation + flow seams, P2 hollows it out behind its own facade, P3 puts LabConsole in front and swaps the network backend under the flow-store, P5–P7 grow analytics and VICKY purely on the seams already built, and **at the 1.0 lock an offered migration converts labs to LabConsole-native, after which SceneManager is retired** — and **at no phase is an existing lab *forced* to migrate, because the `SceneManager` component it references never stops existing until you choose to migrate it.**
