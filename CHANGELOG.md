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

### Notes
- Phase A is **behaviour-neutral**: no runtime logic changed, no serialized field renamed, no
  `[SerializeReference]` type moved namespace/assembly, no `dependencies` block, no version bump, no
  emission. The `unity`-field floor-bump + dependency declarations are a single **Phase D** (post-launch)
  metadata cutover.
- The repo's LF renormalization (`.gitattributes`) is applied as its own deliberate commit, separate from
  the policy file landing here.

## [0.10.5]
- Baseline at the start of the Phase A refactor.
