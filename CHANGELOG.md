# Changelog

All notable changes to `com.pitech.xr.devkit` are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the package uses an `Unreleased` section until a
formal release cadence is set (no version bump during Phase A).

## [Unreleased]

### Added
- **EditMode safety net + `Evaluate Changes` gate (WS A3).** Scenario graph-integrity (Proof A), public-API
  additions-only + type-literal resolution (Proof B), MonoScript GUID stability + serialized round-trip
  (Proof C), pure-logic locks (`ConditionsEvaluator`, `GroupStep`, `LaunchContextValidation`,
  `PublishReportService`), the `Export Lab as Test Fixture` + synthetic-fixture generator tools, and a
  golden-trace PlayMode seed (Phase D-prep). The `Pi tech ▸ Tools ▸ Evaluate Changes` window + a shared
  headless entry give one verdict over the same EditMode run (covers the Scenario + ContentDelivery test
  assemblies; the AgentSubstrate suite and the PlayMode seed run via the Test Runner).
- **`ISceneRunnerControl` seam (WS A8).** A minimal typed control surface over the runner in
  `Pitech.XR.Core`; `SceneManager` implements it via forwarding members (`CurrentStepIndex`, `AutoStart`,
  `Restart`). Behaviour-neutral.
- **DevKit Hub cockpit (WS A2).** Task-first pages (Setup / Author / Localization / Deliver / Maintain /
  Reference), launch tiles for every workspace window, four reserved module assemblies
  (`Networking`/`Localization`/`Analytics`/`Vitals`), and the `ScenarioFactKeys` step-fact vocabulary
  (consts + pure key builders only — no emission).
- **Package-root professionalization (WS A7).** README, CHANGELOG, LICENSE, `.editorconfig`,
  `.gitattributes`, metadata-only `package.json` fields, and the serialization/reflection subsystem notes.
  Plus XML `///` docs on the public API-baseline members — `SceneManager` (config fields, `StepIndex`/`Restart`/
  `EditorSkipFromGraph`/the selection bridges/`GetOrCreateQuizSession`) and the `XRServices` service locator.
- **Mega census-superset fixture + missing-deps skip (WS A3 Steps 11–12; test/editor-only).** The
  `Generate Synthetic Scenario Fixture` menu item now builds the **mega fixture** — a strict superset of
  the WS A1 census (all 11 step types, every routing family + empty case, all 6 GroupStep modes + a nested
  group, all 7 listener modes / 3 call-states / the benign detritus shapes, the real-lab identity/topology
  weirdness) — plus a prefab-**variant twin** and the **LegacyForms** old-serialized-form twins; it
  supersedes `synthetic_routing_families`. Fixture exports now also write a per-fixture
  **external-dependency declaration** (`Tests/Baseline/FixtureDeps/`), and the fixture-driven tests skip
  (loud Inconclusive) exactly the declared-and-unmet fixtures — so the bare gate project stays honest while
  the SDK-referencing real-scene fixtures are committed. New `InvariantDetectionTests` (in-memory poisons
  prove the detector fires + a negative control locks the benign shapes) and `LegacySerializedFormTests`
  (the three `[FormerlySerializedAs]` mappings + snapshot equivalence across serialized generations). The
  export internals were refactored behind an internal `ExportSceneCore` (public surface unchanged). Design +
  coverage matrix: `Documentation~/specs/2026-06-11-mega-fixture-spec.md`.
- **Per-lab gate reporting + batch fixture export (WS A3 follow-up; editor/test-only).** `Evaluate Changes`
  now reports **per lab**: each fixture is its own NUnit case (shared `FixtureCorpus` parametrization
  source), so a red surfaces *every* affected lab — not just the first — and passing labs are shown too. The
  verdict window groups results by check into foldouts with a status dot per lab and the **full** message;
  the Step-12 skip is applied per lab (a skipped lab is its own loud Inconclusive). In-test baseline
  auto-capture was retired: a missing baseline is now a per-lab Inconclusive that points at the export tool
  (the only sanctioned capture path). New `Pi tech ▸ Tools ▸ Export All Test Scenes` re-exports every scene
  in a curated, auto-seeded list (`Pi tech ▸ Tools ▸ Manage Test Scenes List`) in one pass via the same
  `ExportSceneCore` — the open-scene export is unchanged. The list is per-project/per-user
  (`EditorUserSettings`, not source-controlled), auto-seeded from the scenes whose name matches a committed
  lab fixture. `FixtureDependencies` moved off the public surface (now `internal` + `InternalsVisibleTo` the
  test assembly), matching the ContentDelivery/AgentSubstrate pattern.

### Removed
- **Provably-dead code + a stray root artifact (WS A5).** Removed zero-caller / private / `internal`-class
  members: `SceneManager.EvalCompare` (the live paths use `ConditionsEvaluator.EvalCompare`), the unused
  `ScenarioEditor.Styles.Primary`, the redundant `defaultNextGuid` inspector-label special case
  (`ObjectNames.NicifyVariableName` already yields the identical text), the private
  `AddressablesService.BuildDefaultPrefabAddressKey` (its two call sites inlined to `ComputeAddressKey`), the
  dead `try/catch` in `StatsUIController.Init` (the `StatsRuntime` indexer getter is non-throwing), the unused
  `DevkitWidgets` composites (`StatusChips`/`StatusBar`/`ProgressBar`/`Kpi`/`Tile`/`StatusRibbon`/
  `ProgressBarPro`/`StatusHeader` + the private `Accent` field they shared), `DevBlocksWindow.SmallButton`, and
  the dead `RebuildLinksFromGraph` forwarder. Deleted the empty `LaunchContextProviders.cs` placeholder, the
  dead `ScenarioEditorUtil.cs`, and the stray committed `--- SCENE MANAGERS ---.prefab` at the package root
  (zero inbound references). Behaviour-neutral: every removal is absent from the public-API baseline, and no
  `.meta` or MonoScript GUID changed.

### Changed
- **`Scenario.cs` data-model split (WS A6 Step 1).** The `[SerializeReference]` step model moved out of the
  700-line `Scenario.cs` god-class into one file per type under `Runtime/Scenario/Steps/` (`Step`, `TimelineStep`,
  `CueCardsStep`, `QuestionStep`+`Choice`, `MiniQuizStep`+helpers, `SelectionStep`, `InsertStep`, `EventStep`,
  `ConditionsStep`+`ConditionsEvaluator`, `GroupStep`+nested types). `Scenario.cs` keeps only the `Scenario`
  MonoBehaviour (and its `.meta` GUID). Pure move — every type keeps its namespace (`Pitech.XR.Scenario`) and
  assembly, so lab `[SerializeReference]` bindings (resolved by type-name) and all MonoScript GUIDs are unchanged;
  no public API change, no `[MovedFrom]`.
- **`ScenarioGraphWindow` god-class split (WS A6 Step 2).** The 5,897-line graph-window file was decomposed into seven
  files via `partial class` (keeping the global namespace + every public member): the main `ScenarioGraphWindow.cs`
  (window + public API, retains its `.meta`), plus `.EditableNote`/`.GroupBox`/`.GraphView`/`.Edges`/`.StepNode` partials
  holding the (still-nested) inner types, and `StepEditWindow.cs` for the top-level sibling. Pure move — the type keeps
  its `Type.FullName`, the inner types stay logically nested (private), and the EditorWindow MonoScript GUID is unchanged,
  so no public-API or serialization change. (The namespace wrap + public-helper demotion the plan also contemplated are
  deferred to the Phase I API-lock, since those would alter baselined names.)
- **Scenario inspector split (WS A6 Step 3).** The 11 step `PropertyDrawer`s moved out of the 1,505-line
  `ScenarioEditor.cs` (now 888) into `ScenarioStepDrawers.cs` (same assembly + namespace). Pure move — the drawers
  are internal and bind by `[CustomPropertyDrawer]` attribute (not by GUID), so no public-API or serialization
  change. (`ScenarioEditor.Styles` stays nested — it is private and used only by the editor itself.)

- **ContentDelivery extractions (WS A6 Step 4).** The two byte-identical private `TrySetAutoStart`/`TryRestart`
  reflection helpers (duplicated in `AddressablesBootstrapper` + `ContentDeliverySpawner`) were collapsed into one
  shared `internal static SceneRunnerReflection`; `Timestamp` moved out of `PublishTransactionStateMachine.cs` into its
  own file; and the public `IContentDeliveryService`, `ILaunchContextProvider`, `IContentDeliveryMetadataProvider`, and
  `ContentSourceMode` were each split into their own file. All same-namespace (`Pitech.XR.ContentDelivery`), same
  assembly — the dedup is provably byte-identical logic and the splits keep every `Type.FullName` + member, so no
  public-API or serialization change. (The near-identical `Find*SceneManager*` helpers are intentionally **not** unified
  here — that needs the golden-trace Proof A and is deferred.)
- **Stats split + private-cache renames (WS A6 Step 5).** `StatsConfig.cs` split into `StatsConfig.cs` (keeps the
  `StatsConfig` ScriptableObject + its `.meta` GUID), `StatEffect.cs` (`StatOp` + `StatEffect`), and `StatsRuntime.cs`
  (`StatsRuntime`) — same `Pitech.XR.Stats` namespace + assembly. The two non-serialized private caches were renamed to
  the `_camelCase` convention (`StatsConfig.table` → `_table`, `StatsRuntime.v` → `_values`); every serialized /
  public-baseline field (`entries`, the `Entry` fields, the `StatEffect` fields) is untouched. Pure move + private rename
  — `StatEffect` is plain `[Serializable]`-by-value (bound by type-name, not GUID) and none of the moved types is a
  pinned Proof-C GUID, so no public-API or serialization change.

- **`SceneManagerEditor` reflection → typed access (WS A6 Step 6).** The custom inspector's three reflection sites
  (reading the `scenario` field, the `StepIndex` property, and invoking `Restart()`) now call those public, baselined
  `SceneManager` members directly — the editor assembly already references the runtime type, so the reflection was only
  reading members it could have named. Editor-only, behaviour-equivalent; the now-unused `System.Reflection` import was
  dropped. (The *runtime* ContentDelivery reflection is untouched — that conversion is Phase D.)
- **6-lab fixture corpus refreshed (2026-06-12; test assets only — no package code).** All 6 real-lab
  fixtures + graph baselines were re-exported in HealthOn VR through the new `ExportSceneCore` batch path
  (`Export All Test Scenes`), and the per-fixture `Tests/Baseline/FixtureDeps/*.deps.json` declarations
  landed (the one-time migration in `Documentation~/testing-and-fixtures.md` §4a). The content changes are
  **authored lab edits in HealthOn VR** (Delirium route removal, `CANVASES` → `--- UI ---` root renames,
  Loimokseis edits) — deliberate, user-attributed corpus updates, not DevKit-induced drift — to be committed
  separately from the code refactor. The refresh also established that export output is **not byte-stable
  across sessions** (benign fileID churn on all 6 labs): re-export neutrality is judged at the **graph-content
  (snapshot)** level, not raw bytes — the testing doc's byte-neutrality wording was corrected accordingly.

### Fixed
- **`GuidedSetupService` scene-presence lookup restored to assignable-type matching.** The WS A2 Author-page
  perf rework had switched the "is this manager already in the scene?" check to an exact-`FullName` cache, so a
  **subclass** of a queried manager type (e.g. a project-specific `SceneManager`/UI-controller derivative) no
  longer registered as present and the Hub could offer duplicate creation. The single one-scan cache is kept;
  the lookup now also matches `requestedType.IsInstanceOfType(component)` (the pre-rework `FindObjectsOfTypeAll(t)`
  semantics), memoized per type. Editor-only; no consumer subclass exists today, so this is a latent-regression fix.
- **`Evaluate Changes` verdict clarity.** The legend's "unmet deps — enforce in HealthOn VR" text was attached to
  the gray *Skipped* dot, which never occurs in the EditMode gate; it now sits on the yellow *not-enforcing*
  (Inconclusive) dot that actually fires, and a green verdict with inconclusive cases now hints that the real
  labs enforce in HealthOn VR (tier 2). Minor polish in the same pass: a `dependencyies` pluralization, two
  orphaned `using System.Reflection;` left by the WS A6 Step 4 dedup, a stray citation artifact in a Hub
  comment, `ExecuteMenuItem` return now checked on all six Maintain-page buttons, and scene-list
  sanitized-name collisions now surfaced in the Manage Test Scenes dialog.

### Notes
- Phase A is **behaviour-neutral**: no runtime logic changed, no serialized field renamed, no
  `[SerializeReference]` type moved namespace/assembly, no `dependencies` block, no version bump, no
  emission. The `unity`-field floor-bump + dependency declarations are a single **Phase D** (post-launch)
  metadata cutover.
- The repo's LF renormalization (`.gitattributes`) is applied as its own deliberate commit, separate from
  the policy file landing here.
- **Known UI-coverage gap (recorded 2026-06-12):** the equivalence proofs are EditMode serialization/API/GUID
  checks and never exercise the graph window's UI interactions (node drag/connect incl. GroupStep proxy-branch
  ports, group boxes, EditableNote attach/detach, StepEditWindow edits, step drawers, undo). After the WS A6
  graph-window split that surface is covered by a ~5-minute manual smoke checklist recorded on the Phase A
  plan's WS A6 Step 2 — run it after editor-UI-touching changes.

## [0.10.5]
- Baseline at the start of the Phase A refactor.
