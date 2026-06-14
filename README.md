# Pi tech XR DevKit (`com.pitech.xr.devkit`)

Modular in-house Unity toolkit for the **VICKY / HealthOn XR** ecosystem — the shared authoring + runtime
layer consumed by the HealthOn AR and HealthOn VR apps.

> **Status:** v0.10.5 — Phase A (behaviour-neutral refactor & foundation) in progress. Package min editor
> is `2022.3`; consumer projects run **Unity 6+**.

## What's inside

| Layer | Assembly | Role |
|---|---|---|
| Core | `Pitech.XR.Core` | shared primitives, `ScenarioFactKeys` vocabulary, the `ISceneRunnerControl` seam |
| Scenario | `Pitech.XR.Scenario` | the scenario data model (`Scenario` + `[SerializeReference]` `Step` graph) and the `SceneManager` runtime interpreter |
| Quiz / Stats / Interactables | `Pitech.XR.{Quiz,Stats,Interactables}` | domain components |
| Content Delivery | `Pitech.XR.ContentDelivery` | Addressables build / publish + launch-context |
| Agent Substrate | `Pitech.XR.AgentSubstrate` | observation envelope + transport (analytics foundation) |
| Reserved | `Pitech.XR.{Networking,Localization,Analytics,Vitals}` | empty module slots; logic lands Phase B / post-launch |

Editor tooling lives in the matching `*.Editor` assemblies. The **DevKit Hub** (`Pi tech ▸ DevKit`) is the
cockpit for everything.

## DevKit Hub

`Pi tech ▸ DevKit` opens the Hub — task-first pages (Setup / Author / Localization / Deliver / Maintain /
Reference). Every workspace window (Scenario Graph, Dev Blocks, Addressables Builder, Scene Categories) is
reachable in ≤2 clicks; the Hub launches them, it never re-implements them.

## Quality gate — `Evaluate Changes`

`Pi tech ▸ Tools ▸ Evaluate Changes` (also on the Hub **Maintain** page) runs the EditMode safety net and
gives a plain-language verdict. It enforces three equivalence proofs on the scenario graph:

- **Proof A** — graph integrity (no null/dangling object refs, every route resolves, live UnityEvent listeners).
- **Proof B** — the public API is additions-only, and the Core.Editor type literals still resolve.
- **Proof C** — MonoScript GUID stability + open→save serialized-diff zero.

Push only on green — in the bare gate project the 6 real-lab fixtures skip as loud Inconclusive by design
(full lab enforcement runs inside HealthOn VR, tier 2; see `Documentation~/testing-and-fixtures.md` §2/§4a).
**Coverage:** the verdict covers `Pitech.XR.Scenario.Editor.Tests` +
`Pitech.XR.ContentDelivery.Editor.Tests`. The `Pitech.XR.AgentSubstrate.Editor.Tests` suite is excluded
until its known pre-existing failure (501 `NOT_IMPLEMENTED` classification, dispositioned to the module
owner 2026-06-10) is resolved — run it via **Window ▸ General ▸ Test Runner**. The gate requires the
Unity Test Framework package (`com.unity.test-framework`); without it the Hub still compiles and the
Maintain card explains what to install.

## Running the tests

The package's tests live under `Tests/` (EditMode net + a PlayMode golden-trace seed). **A UPM package's
tests only appear in the Unity Test Runner when the package is listed in the *consuming project's*
`Packages/manifest.json`:**

```json
{
  "testables": [ "com.pitech.xr.devkit" ]
}
```

Add that to a Unity 6+ host project (the HealthOn VR/AR project, or a dedicated DevKit test host), then open
**Window ▸ General ▸ Test Runner**. The static fixture corpus under `Tests/Fixtures/` is produced by
`Pi tech ▸ Tools ▸ Export Lab as Test Fixture` (real labs; also writes each fixture's external-dependency
declaration used by the missing-deps skip) and `Generate Synthetic Scenario Fixture` (the **mega
census-superset fixture** — every step type, routing family, GroupStep mode, and listener shape — plus its
variant + LegacyForms twins; spec: `Documentation~/specs/2026-06-11-mega-fixture-spec.md`).

## Documentation

Design docs, phase plans, and the architecture spec live under `Documentation~/`. Intentional
serialization/reflection exceptions are recorded in
[`Documentation~/serialization-and-reflection-notes.md`](Documentation~/serialization-and-reflection-notes.md).

## License

Proprietary — see [LICENSE.md](LICENSE.md).
