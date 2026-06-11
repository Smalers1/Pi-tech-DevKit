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
   Fixture"). The tool copies the saved scene asset, opens the copy additively, completely unpacks
   every prefab instance, gathers all roots under one fixture root (cross-root references — Scenario
   under `--- SCENE MANAGERS ---`, panels under `--- UI ---` — stay intact), then saves
   `Tests/Fixtures/Scenarios/<Scene>.prefab` + its baseline and discards the copy. Your open scene is
   never dirtied. The capture is **faithful**: a graph note the lab already had (e.g. a half-wired
   listener that ships in the lab) is **logged to the Console and you can press "Export anyway"** — the
   net guards it against DevKit-introduced change either way. Only a break the export *itself* caused
   (a cross-root reference that did not survive the gather — detected by diffing the graph before vs
   after the unpack) is a **hard refuse**, because that fixture would not match the lab.
   Repeat per corpus lab: **Pharmacy, Delirium, Loimokseis, Loimokseis_Old_1, Delirium Stats Test**.
2. **Test (DevKit gate project).** `Pi tech > Tools > Evaluate Changes` (also a Maintain card).
   Plain-language verdict over the whole net — see §4 for what it proves. Unity's own Test Runner
   window shows the same tests; Evaluate Changes is the intended surface.
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

- **Clean (or only brand-new labs):** your change did not alter how labs load, serialize, or extract.
  Proceed.
- **Any modification to an existing fixture/baseline:** either the lab itself changed in HealthOn VR
  (fine — commit the regenerated pair, deliberately), or **your code change altered
  serialization/extraction — stop and investigate before pushing.** This is Proof C's
  serialized-diff-zero running against real history instead of a self-captured baseline.

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

**The synthetic fixture** (`Pi tech > Tools > Generate Synthetic Scenario Fixture`, run once in the
gate project, regenerate only deliberately): real labs exercise none of `ConditionsStep` routing,
`SelectionStep allowedWrong > 0`, non-empty MiniQuiz `defaultNextGuid`, or `GroupStep
SpecificChildCompletes` — the synthetic covers those four families so the net protects them too.

## 5. Tool inventory (deliberately this small)

| Surface | Entry | Used in |
|---|---|---|
| `Pi tech > Tools > Evaluate Changes` (+ Maintain card) | the gate | DevKit gate project |
| `Pi tech > Tools > Export Lab as Test Fixture` (+ Maintain card) | fixture export | HealthOn VR |
| `Pi tech > Tools > Generate Synthetic Scenario Fixture` (+ Maintain card) | one-off synthetic | DevKit gate project |

Plus headless `DevKitChecks.RunAll` (`-executeMethod`) — the same gate for a future pre-push hook /
Phase D CI; not a separate tool. Anything beyond these three entries should justify its existence
against this table before being added.

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Export menu item missing in HealthOn VR | DevKit still resolved from the git URL (old cached version), or the package didn't recompile | Check the manifest entry (§1), refocus Unity, check the Console for compile errors. |
| Dialog: "pre-existing graph note(s)… Export anyway?" | The lab itself has a graph imperfection (e.g. a half-wired listener that ships in the lab) | Not a blocker. Press **Export anyway** — it is captured faithfully and the net guards it. Optionally clean the lab in HealthOn VR first (its own quality), then re-export. |
| Export refused: "introduced graph break(s)" | The unpack/gather corrupted the graph — a reference the lab HAD did not survive (export bug, not a lab issue) | Report it. The check diffs the graph before vs after the gather, so a hard refuse here means the export tool needs fixing, not your lab. |
| Export refused: unsaved/dirty scene | The export works on the SAVED scene asset | Save the scene, retry. |
| `No tests to show` in Test Runner | Host manifest lacks `testables` | Add `"testables": ["com.pitech.xr.devkit"]`. |
| Tests *Inconclusive* | (a) no fixtures yet, (b) baseline just bootstrapped, (c) fixture not yet Unity-normalized | (a) run the export loop; (b) commit + re-run; (c) open + re-save the fixture once (or re-export), commit, re-run. |
| Fixture/baseline shows a git diff after re-export | Lab changed, or your code change altered serialization | §3 — decide which; never push an unexplained fixture diff. |
| `Network_NotImplemented_IsDebugLogOnly` fails | Pre-existing AgentSubstrate 501-classifier bug | Known; excluded from the gate; routed to the module owner. |

## 7. CI (Phase D — recorded, not wired)

The headless door exists; CI attaches later without rework (plan §H.1). The
`ForceReserializeAssets` + `git diff --exit-code` byte backstop (Appendix I.6) belongs to that CI
job on a throwaway checkout. Do not wire CI during Phase A.
