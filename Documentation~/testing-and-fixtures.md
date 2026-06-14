# Testing & fixtures — THE process

One loop, three tools, two projects. Companion to the Phase A plan
([plans/2026-06-09-phase-a-refactor-and-foundation.md](plans/2026-06-09-phase-a-refactor-and-foundation.md),
Appendix I) and [serialization-and-reflection-notes.md](serialization-and-reflection-notes.md).

## 0. The process at a glance

```
 HealthOn VR (labs)                          DevKit gate project (dev's code changes)
 DevKit via file: ─── same on-disk package folder ─── DevKit via file:
        │                                                   │
 1. open lab scene                                          │
 2. Export Lab as Test Fixture  ──writes──►  Tests/Fixtures + Baseline (in the package)
        │                                                   │
        │                                    3. Evaluate Changes (the gate)
        │                                    4. git diff on Tests/ must be clean (§3)
        │                                                   │
        │                                    5. green → dev commits + pushes
 6. consumers update the package  ◄─────────────────────────┘
```

Fixtures are exported **right before testing, in the lab project itself** — they are always in sync
with the labs, every script exists (no missing-script problem, ever), and because both projects
reference the **same folder** via `file:`, there is no copy/transport step: the export lands directly
where the tests read it.

**Two tiers** (mega-fixture spec §7 —
[specs/2026-06-11-mega-fixture-spec.md](specs/2026-06-11-mega-fixture-spec.md)):

| Tier | When | Where | What |
|---|---|---|---|
| **1 — every change** | after any DevKit edit, before any push | HealthOn VR, or the bare gate project (real-scene fixtures Inconclusive-skip there via their committed deps declarations — §4a) | `Evaluate Changes` — the full covered-assembly suite; the **mega fixture** is enforced everywhere |
| **2 — pre-ship** | before updating the git-tracked DevKit reference HealthOn VR ships with | inside HealthOn VR (`file:`) | full gate with the 6 real-scene fixtures **enforced** + the re-export rule (§3): graph snapshots stay content-neutral (prefab fileIDs may churn — *not* a clean `git status Tests/`) |

## 1. One-time setup

**HealthOn VR** (`Packages/manifest.json`) — switch the DevKit entry from the git URL to the local
path:

```json
"com.pitech.xr.devkit": "file:E:/Pi Tech/Vicky/DevKit/Pi-tech-DevKit",
```

(replacing `"https://github.com/Smalers1/pitech-xr-devkit.git"`; forward slashes, spaces are fine).
Unity re-resolves on next focus. Keep `file:` while actively developing the DevKit on this machine;
put the git URL back when you want the pinned released package. Team machines stay on the git URL.

**DevKit gate project** (`C:\Users\ntano\DevKit`) — already correct: same `file:` path, plus
`"testables": ["com.pitech.xr.devkit"]` (required for the package's tests to appear at all).

> Shared-source caveat: with both projects on `file:`, a compile error in the package breaks BOTH
> editors until fixed. That is the price of zero-transport, and it is worth it.

## 2. The loop

1. **Export (HealthOn VR).** Open a lab scene (saved, no pending edits) →
   `Pi tech > Tools > Export Lab as Test Fixture` (also: DevKit Hub → Maintain → "Export Lab as
   Fixture"). The tool copies the saved scene asset, opens the copy **single** (restoring your scene
   setup from disk afterward), completely unpacks every prefab instance, gathers all roots under one
   fixture root (cross-root references — Scenario under `--- SCENE MANAGERS ---`, panels under
   `--- UI ---` — stay intact), then saves `Tests/Fixtures/Scenarios/<Scene>.prefab` + its baseline and
   discards the copy. Your open scene is never dirtied. The capture is **faithful**: a graph note the
   lab already had (e.g. a half-wired listener that ships in the lab) is **logged to the Console and you
   can press "Export anyway"** — the net guards it against DevKit-introduced change either way. Only a
   break the export *itself* caused (a cross-root reference that did not survive the gather — detected by
   diffing the graph before vs after the unpack) is a **hard refuse**, because that fixture would not
   match the lab.
   Repeat per corpus lab: **DIPAE_Nosileutiki_Meta, Delirium, EkpaSceneEmergency, Loimokseis,
   MoMTScene_Meta, Pharmacy** (the 6 real shipped university scenes — the committed corpus as of
   2026-06-11; the census-marked Loimokseis_Old_1 + Delirium Stats Test were superseded).
   **Batch:** instead of repeating per lab, run `Pi tech > Tools > Export All Test Scenes` (Maintain
   card, or the `Manage Test Scenes List` window) — it re-exports every scene in your curated list in one
   pass through the same export path, with every open scene saved first. The list **auto-seeds** from the
   scenes whose name matches a committed lab fixture (so it arrives pre-filled with the 6 labs) and is
   per-project/per-user (not source-controlled). A re-export of an unchanged lab is **graph-content-neutral,
   not byte-neutral** — export output is not byte-stable across sessions (benign fileID churn, observed on all
   6 labs 2026-06-12), so judge a re-export by its graph **snapshot** (`.graph.json`) diff, not raw prefab bytes.
   The re-export rule (§3) still applies: review `git status Tests/` afterward and attribute any drift.
2. **Test (DevKit gate project).** `Pi tech > Tools > Evaluate Changes` (also a Maintain card).
   Plain-language verdict over the whole net — see §4 for what it proves. The verdict is **per lab** and
   **quiet by default**: a clean run collapses to a one-line tally; a **failure** shows *every* affected
   lab (its own row, full message — not just the first), with "not enforcing" checks collapsed and
   fully-passing checks folded into a count. Tick **"Show all checks"** to list every passing check.
   Unity's own Test Runner window shows the same per-lab cases; Evaluate Changes is the intended surface.
3. **Check the re-export diff (§3), then ship.** Green + clean diff → commit the code change
   (fixtures/baselines too if labs legitimately changed) → push.
4. **Update consumers.** On git-URL machines: Package Manager → Pi tech XR DevKit → Update
   (or delete its entry in `Packages/packages-lock.json` and refocus).

## 3. The re-export rule (what makes this loop *proof*, not ritual)

Baselines captured and compared under the *same* code prove nothing by themselves. The whole
isolation — "is this failure from my DevKit change, or was the lab already like that?" — rests on
**one fixture, two code versions, diffed.** Two rules make that airtight:

**3a. Capture on known-good code, once.** A fixture + baseline must be captured on the *current
shipping* DevKit (clean `main`, your in-flight changes NOT loaded) and committed. That committed pair
is the reference. If you capture for the first time while your own change is already loaded, the
change is silently baked into the baseline (`baseline == current`, nothing to diff) and the gate is
blind to it — the one real hole. So: new lab → export on `main` → commit → *then* start changing the
DevKit.

**3b. Never re-export to chase a red test.** When you are testing your own DevKit change, you do
**not** re-export. The test re-extracts the snapshot from the *committed fixture prefab* under your
new code and diffs it against the *committed baseline* (old code). Re-exporting would overwrite the
reference with your change's output — laundering exactly what the gate is meant to catch. The
export's overwrite dialog says this; heed it. Re-export only for a genuinely changed lab, or to
re-capture on `main`.

After exporting (legitimately) right before a run, check git in the package repo:

```
git status Tests/
```

- **The graph `.graph.json` snapshots are content-unchanged (brand-new labs aside):** your change did not
  alter how labs load, serialize, or extract. Note prefab `.prefab` bytes can still churn — fileIDs are not
  stable across exports — so "clean" means the **graph snapshot** is content-identical, not a zero `git diff`.
  Proceed.
- **A graph `.graph.json` snapshot changed — attribute it; three causes:** (a) **benign fileID churn** only
  (snapshot content identical, fileIDs merely renumbered) — fine; (b) the lab itself changed in HealthOn VR
  (fine — commit the regenerated pair, deliberately; e.g. the 2026-06-12 authored edits: Delirium route
  removal, `CANVASES` → `--- UI ---` renames, Loimokseis edits); or (c) **your code change altered
  serialization/extraction — stop and investigate before pushing.** This is Proof C's diff-zero running
  against real history instead of a self-captured baseline — measured at the **graph-content (snapshot)** level.

**Why "author-normal" mess doesn't weaken this.** The export lets benign authoring rows through
(empty UnityEvent slots, empty/optional fields) instead of refusing — real labs ship with them, and
forcing them out would mean *editing the labs*, changing the thing under test. That does **not** lose
detection: the snapshot records every listener leaf, routing field, and step id, so if a DevKit
change later empties, drops, or rewires any of them, the value is identical on both sides of the diff
when it *shouldn't* change and differs the moment it does — the drift is caught regardless of whether
the row was "tidy." Attribution comes from the same-fixture/two-code diff (3a/3b), not from the lab
being pristine. Lab tidiness is a separate, optional concern (the WS A9 health report), never an
export gate.

## 4. What the gate proves

| Proof | Tests | Locks |
|---|---|---|
| A — graph integrity | `ScenarioGraphIntegrityTests` | Absolute invariants + snapshot vs committed baseline per fixture. `ScenarioGraphSnapshot` is the single walk shared by test and export. |
| B — public API | `PublicApiBaselineTests`, `CoreEditorTypeLiteralTests` | Additions-only API; string-resolved type literals keep resolving. |
| C — serialized & GUID | `ScriptGuidStabilityTests`, `SerializedFixtureRoundTripTests` | `SceneManager` GUID pin + 11-type baseline; reserialize idempotency (on a copy); prefab-override churns nothing into `steps`/`managedReferences`. |
| Pure logic | `ConditionsEvaluatorTests`, `GroupStepRequirementTests` | `CompareOp` semantics; `GroupStep` requirement maintenance. |

Covered assemblies: `Pitech.XR.Scenario.Editor.Tests` + `Pitech.XR.ContentDelivery.Editor.Tests`.
`Pitech.XR.AgentSubstrate.Editor.Tests` is excluded until its owner fixes the known 501-classifier
failure. Zero tests executed reads **RED**, never green.

The four fixture checks run **per lab** — each fixture is its own NUnit case via the shared
`FixtureCorpus` parametrization source. The `Evaluate Changes` window is **quiet by default**: a clean
run is a one-line tally; on a **failure** it expands the failing check and names every affected lab
(status dot + full message), keeps "not enforcing" checks collapsed, and folds fully-passing checks into
a count (tick "Show all checks" to list them). The reverse-direction orphan check (a committed baseline
or deps declaration whose fixture vanished) and a corpus-present backstop are **suite-level** checks
alongside the per-lab ones. Baselines are captured **only by the export tool** — a fixture with no
committed baseline reads as a per-lab Inconclusive pointing there, never written mid-test.

**The mega fixture** (`Pi tech > Tools > Generate Synthetic Scenario Fixture` — same menu item, now
generating the census-superset; regenerate only deliberately): one ~50-step fixture carrying every
step type (11/11 — incl. QuizStep/QuizResultsStep, which no real lab serializes), every routing
family non-vacuous + its empty case, all 6 GroupStep modes (+ a nested group), all 7 listener
modes / 3 call-states / the benign detritus shapes real labs ship, and the real-lab identity and
topology weirdness (Greek + trailing-space names, same-named siblings, unreachable island,
back-edges). One run also produces the **variant twin** (prefab-variant mechanics) and the
**LegacyForms twins** (old serialized-form coverage). It supersedes `synthetic_routing_families`
(its four families carried forward as a strict superset — delete the old prefab + baseline in the
same commit as the first mega generation). Full design + coverage matrix:
[specs/2026-06-11-mega-fixture-spec.md](specs/2026-06-11-mega-fixture-spec.md).

### 4a. The missing-deps skip (why the bare project can stay green)

Real-scene fixtures reference Meta/TMP/project scripts that don't exist in the bare gate project.
The skip is **declaration-keyed, never failure-inferred** (spec §7.1): the export writes
`Tests/Baseline/FixtureDeps/<name>.deps.json` (the fixture's external deps; **no file when
self-contained** — so the mega/synthetic can never acquire one), and a fixture's gate cases skip
**iff** its declaration exists AND has entries AND ≥1 GUID doesn't resolve in the current project.
Skips are loud Inconclusive naming the unmet deps; a fixture without a declaration is always
enforced — a DevKit-introduced dangling ref can never hide behind the skip. Orphaned declarations
fail the gate. **One-time migration — DONE 2026-06-12:** the 6 labs were re-exported in HealthOn and the 6
deps files appeared. (The original "bytes must come back unchanged" acceptance was wrong — export output is
not byte-stable across sessions (fileID churn on all 6); the real, met acceptance is **graph-content
(snapshot) equivalence** plus attribution of any content drift — the 2026-06-12 drift was the user's own
authored lab edits, see §3.)

## 5. Tool inventory (deliberately this small)

| Surface | Entry | Used in |
|---|---|---|
| `Pi tech > Tools > Evaluate Changes` (+ Maintain card) | the gate (per-lab verdict) | DevKit gate project |
| `Pi tech > Tools > Export Lab as Test Fixture` (+ Maintain card) | fixture export (one open scene) | HealthOn VR |
| `Pi tech > Tools > Export All Test Scenes` (+ Maintain card) | batch re-export of the curated test-scenes list | HealthOn VR |
| `Pi tech > Tools > Manage Test Scenes List` (+ Maintain card) | curate the batch list (add/remove/reset) | HealthOn VR |
| `Pi tech > Tools > Generate Synthetic Scenario Fixture` (+ Maintain card) | the mega census-superset fixture (+ variant + LegacyForms twins) | DevKit gate project |

Plus headless `DevKitChecks.RunAll` (`-executeMethod`) — the same gate for a future pre-push hook /
Phase D CI; not a separate tool. The batch export + list manager are thin conveniences over the single
`Export Lab as Test Fixture` (same `ExportSceneCore`), not new capabilities; anything beyond these
entries should justify its existence against this table before being added.

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Export menu item missing in HealthOn VR | DevKit still resolved from the git URL (old cached version), or the package didn't recompile | Check the manifest entry (§1), refocus Unity, check the Console for compile errors. |
| Dialog: "pre-existing graph note(s)… Export anyway?" | The lab itself has a graph imperfection (e.g. a half-wired listener that ships in the lab) | Not a blocker. Press **Export anyway** — it is captured faithfully and the net guards it. Optionally clean the lab in HealthOn VR first (its own quality), then re-export. |
| Export refused: "introduced graph break(s)" | The unpack/gather corrupted the graph — a reference the lab HAD did not survive (export bug, not a lab issue) | Report it. The check diffs the graph before vs after the gather, so a hard refuse here means the export tool needs fixing, not your lab. |
| Export refused: unsaved/dirty scene | The export works on the SAVED scene asset | Save the scene, retry. |
| `No tests to show` in Test Runner | Host manifest lacks `testables` | Add `"testables": ["com.pitech.xr.devkit"]`. |
| A lab reads *Inconclusive* | (a) no fixtures yet, (b) the lab has no committed baseline, (c) the lab's declared deps don't resolve here (bare project), (d) fixture not yet Unity-normalized | (a) run the export loop; (b) export the lab to capture its baseline, commit, re-run (baselines are no longer auto-written mid-test); (c) expected in the bare project — enforce in HealthOn VR (tier 2); (d) open + re-save the fixture once (or re-export), commit, re-run. |
| Fixture/baseline shows a git diff after re-export | Benign fileID churn (export isn't byte-stable across sessions), the lab changed, or your code change altered serialization | §3 — attribute which (compare the graph `.graph.json` snapshot first); never push an unexplained *snapshot* diff. |
| `Network_NotImplemented_IsDebugLogOnly` fails | Pre-existing AgentSubstrate 501-classifier bug | Known; excluded from the gate; routed to the module owner. |

## 7. CI (Phase D — recorded, not wired)

The headless door exists; CI attaches later without rework (plan §H.1). The
`ForceReserializeAssets` + `git diff --exit-code` byte backstop (Appendix I.6) belongs to that CI
job on a throwaway checkout. Do not wire CI during Phase A. **Scope rule (binding, spec §4.3):**
that future reserialize sweep covers `Tests/Fixtures/Scenarios/` ONLY — `Tests/Fixtures/LegacyForms/`
is deliberately NON-normalized (pre-FSA field names); reserializing it silently evaporates the
legacy-form coverage.
