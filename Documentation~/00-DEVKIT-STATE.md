# DevKit — current state & doc index

> **Status at a glance (2026-06-19):** **Phase A — implemented fully.** **Phase B — in planning.**
> This is the entry-point index for `Documentation~/`. Claims are marked **(verified)** = read from the `.cs`/`.asmdef`,
> or **(reported)** = from a plan/review, verify against code before relying on it.

---

## Phase A — implemented fully (the behaviour-neutral foundation)

The DevKit re-topology + safety net are in place. Code-verified pieces:
- **The seam exists:** `Pitech.XR.Core.ISceneRunnerControl` — exactly **3 members** (`CurrentStepIndex` / `AutoStart` /
  `Restart`), implemented by `SceneManager` as pure forwarders. **(verified)**
- **The gate exists and enforces:** the EditMode **"Evaluate Changes"** net — `Editor/Core.Editor/Tools/EvaluateChanges.cs`
  → `DevKitChecks.RunEditModeGate`, running **Proof A** (graph integrity / snapshot baseline), **Proof B** (public-API +
  Core.Editor type-literals), **Proof C** (serialize/GUID stability + prefab-override-no-churn), with a real test corpus
  under `Tests/Editor/Scenario/`. **(verified)** PlayMode **golden-trace (Proof D) is a seed only** —
  `Tests/PlayMode/GoldenTraceTests.cs` is `Assert.Ignore`d (no committed traces), deferred to Phase D. **(verified)**
- **Module slots reserved:** `Networking` / `Localization` / `Analytics` / `Vitals` are empty assemblies referencing
  only `Core` (`PITECH_HAS_FUSION` / `PITECH_HAS_LOCALIZATION` version-defines wired, guarding no code). **(verified)**
- **`ScenarioFactKeys` is present but inert** (consts + builders; zero emitters/consumers yet). **(verified)**
- **IL2CPP preserve:** `Runtime/link.xml` preserves **6 assemblies** (Core · ContentDelivery · Scenario · Interactables ·
  Stats · Quiz). **(verified)**
- **Execution status:** WS A1–A8 landed and verified GREEN in HealthOn VR tier-2 on **2026-06-12** (97/113 checks; 6 real
  labs enforcing). A6 namespace cleanup deferred to Phase I (renaming public types changes `Type.FullName` → Proof B
  red). **(reported — `plans/2026-06-09-phase-a-refactor-and-foundation.md`)**

> Known follow-ups carried out of Phase A: a **critical `AddressablesRemoteUrlRewriter` global-transform clobber**
> (overwrites `Addressables.ResourceManager.InternalIdTransformFunc` without restore — can break a host RN app under
> UaaL) is **flagged for further review, not yet specced**. The runner's linear/`*Group` `Run*` twins **diverge** (e.g. a
> first-click guard present in `RunQuestionGroup`, absent in `RunQuestion`), so any future dedup is behaviour-affecting.

---

## Phase B — in planning

Phase B is authored as **one full spec first, then carved into two plans** — *not* a 1:1 "this doc = B.1, that doc = B.2."
The two source docs below are **combined** into the complete Phase B spec; that spec is then split into:

- **B.1 — the final DevKit structure** (behaviour-neutral): LabConsole orchestrator, LabEventBus notification spine, the
  seam, typed params/actions, runner extraction, and the multiplayer/localization infrastructure seams.
- **B.2 — the features, built on B.1**: multiplayer (step-sync), analytics, localization.

| Source doc | Lives in | Feeds | Status |
|---|---|---|---|
| **Architecture map** — `devkit-architecture-map.md` | the **workbench** (`_workbench/devkit/`) | the full Phase B spec → **B.1 + B.2** (it already spans structure §1–9 *and* features: multiplayer §10, analytics §11, localization §12) | drafting (being finalized) |
| `plans/2026-06-09-phase-b-analytics.md` | this repo | the analytics portion of **B.2** | **RATIFIED** by Petros 2026-06-10 (pending Stergios sign-off) |

> **Why not "map = B.1, analytics = B.2":** the map carries both structural *and* feature content, and the analytics doc
> is one B.2 feature. They merge into a single Phase B spec, which is **then** divided into B.1 (structure) / B.2 (features).

Key Phase-B decisions (2026-06-19): full step-sync **including branches** ships at launch (path-list **forwarding**, not
the B9 bool bridge); `LabEventBus` is built as the notification spine; uniform AnyCompletes (no per-step barrier yet);
VICKY stays spec-reference; the abandoned 30-asmdef/registry design is **deferred** (the launch path evolves
`SceneManager` → `LabConsole`). The workbench Phase-B drafts were **consolidated into the architecture map and deleted**.

---

## Doc index (`Documentation~/`)

- **`plans/2026-06-09-devkit-launch-plan.md`** — the launch umbrella (10 Ratified Decisions, gate calendar).
- **`plans/2026-06-09-phase-a-refactor-and-foundation.md`** — Phase A + the **WS A3 net / Proof A/B/C** definitions.
- **`plans/2026-06-09-phase-b-analytics.md`** — the ratified Phase B features plan (→ B.2).
- **`plans/2026-06-10-phase-b-multiplayer-step-sync.md`** — the B9 step-sync detail companion.
- **`plans/2026-06-09-phase-c-integration-and-ship.md`** — the ship runbook (store gates, IL2CPP).
- **`plans/_after-launch/2026-06-09-after-launch-plan.md`** — the post-launch **D..I** phase ladder.
- **`specs/2026-04-23-devkit-1.0-target-architecture-design.md`** — the architecture source (LabConsole/LabEventBus,
  §9.8 Fusion, VickyMode, §10.6 consent, §28 domain systems).
- **`specs/2026-06-11-mega-fixture-spec.md`** — the test-corpus / Scenario serialized-surface spec.
- **Operational guides (living references):** `ADDRESSABLES_CONTENT_DELIVERY_GUIDE.md` · `CONTENT_DELIVERY_TEST_MATRIX.md`
  · `dependency-truth-report.md` · `serialization-and-reflection-notes.md` · `testing-and-fixtures.md`.
- `plans/_archive/` · `proposed plans/` — history / not load-bearing.

> All of the above **stay** — only the *workbench* Phase-B drafts were cleaned up. The architecture map (workbench) is
> the one remaining workbench DevKit doc; once finalized it is split (with the ratified analytics plan) into the two Phase B
> plans — **B.1 (structure)** and **B.2 (features)** — which are then promoted into `plans/`.
