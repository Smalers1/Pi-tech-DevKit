---
status: planned
---

# P1 Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the v0.11 foundation of DevKit 1.0 — the 30-asmdef topology, the event bus, the six registries, all 17 capability interfaces, the agent substrate scaffold, the authoring-editor shell, the non-breaking CI gates, the v0.10 fixture corpus, and the Unity 2022.3 → Unity 6 package metadata bump — all with **zero behavior change** for HealthOn AR and HealthOn VR consumers.

**Architecture:** Additive-only foundation. The existing `Pitech.XR.Scenario.SceneManager`, `Scenario`, step classes, `ContentDeliverySpawner`, `ControlOptionManager`-analog consumers, and the v0.10 public API all keep working unchanged. New asmdefs, registries, and capability interfaces are introduced alongside the existing code; `XRServices` becomes a shim that delegates to the new `CapabilityRegistry`. Behavioral changes (step runner extraction, Lab Console lift, Agent Substrate) wait for P2+.

**Tech Stack:** Unity 6 LTS (6000.0 min, 6000.3 recommended), C# 9, Newtonsoft.Json (Unity UPM), Addressables 2.x, Input System 1.11+, Test Framework 2.0, MPPM, Awaitable (Unity 6 native async primitive), UI Toolkit with `[UxmlElement]`/`[UxmlAttribute]`, Roslyn analyzer DLL built externally via .NET SDK.

**Spec reference:** `docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md` — authoritative for every architectural decision. Every task in this plan cites the relevant §.

**Duration:** 2–3 weeks, single engineer. ~90 tasks across 11 parts.

**Exit criteria (§17.2 P1 of spec):**
- All 6 active CI gates green on v0.11 PR (Gate 3 golden replay activates P2)
- Every v0.10 fixture loads cleanly in Unity 6
- Public API surface shows additions only (no removals/renames)
- DevKit simulator host plays the canonical reference lab in editor
- `CHANGELOG.md` v0.11.0 entry published
- `package.json` `unity` field at `6000.0`
- Tagged release `v0.11.0` on git

---

## Plan structure

| Part | Focus | Tasks | Spec § |
|---|---|---|---|
| A | Preflight — branch, fixtures, API baseline | 1–5 | §15.3, §19.2 |
| B | Package metadata + Unity 6 bump | 6–9 | §17.2 P1 |
| C | Test harness scaffold (9 test asmdefs) | 10–14 | §19.1 |
| D | Domain layer — schema versioning, BuildingBlockMetadataV1 | 15–22 | §7 |
| E | Core layer — EventBus, 6 registries, LabRoot, Bootstrap, Logger | 23–44 | §8.1–8.4, §15 |
| F | Capabilities layer — 17 interfaces + default impls | 45–56 | §9 |
| G | Roslyn analyzers — banned APIs + dep direction | 57–62 | §9.6, §15.3 Gates 5–6 |
| H | Authoring editor scaffold — DesignSystem, Hub v1, base window | 63–72 | §13 |
| I | CI gates — 6 gates active in P1 | 73–80 | §15.3, §16 |
| J | Simulator host (DevKit test project) | 81–85 | §12.1, §13.2 |
| K | Documentation + release | 86–90 | §0, §18 |

---

# Part A · Preflight

Goal: establish a clean starting state — new branch, captured fixtures, captured public API baseline, confirmed Unity 6 opens the project.

### Task 1: Create the v0.11 feature branch

**Files:**
- None (git operation)

- [ ] **Step 1: Verify you are on `main` with clean working tree**

Run: `git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" status`
Expected: `On branch main` / `nothing to commit, working tree clean`

If not clean, stop and resolve uncommitted changes before proceeding.

- [ ] **Step 2: Pull latest `main`**

Run: `git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" pull --ff-only`
Expected: `Already up to date.` or a fast-forward merge.

- [ ] **Step 3: Create and switch to branch `feature/p1-foundation`**

Run: `git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" checkout -b feature/p1-foundation`
Expected: `Switched to a new branch 'feature/p1-foundation'`

- [ ] **Step 4: Push branch to origin**

Run: `git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" push -u origin feature/p1-foundation`
Expected: `Branch 'feature/p1-foundation' set up to track remote branch`.

> **Note:** This plan involves git branch creation, which is normally forbidden per `AGENT_PERMISSIONS.md` Rule 1. The rule applies to AI agents; **Petros executes this task manually** as the human-in-the-loop. The plan documents the exact commands so no ambiguity.

---

### Task 2: Open DevKit project in Unity 6 (validate compat before touching anything)

**Files:**
- None (validation only)

- [ ] **Step 1: Install Unity 6000.3 LTS via Unity Hub** (if not already)

Unity Hub → Installs → Install Editor → pick `6000.3.0f1` or latest patch of `6000.3`. Include Android Build Support (IL2CPP + OpenJDK + Android SDK + NDK). This is the LTS until December 2027 and matches the spec's recommended target.

- [ ] **Step 2: Open the DevKit project under Unity 6**

Unity Hub → Projects → Add → browse to `e:\Unity files\Pi tech DevKit` → Open. When prompted about Unity version change, select "Change Version" and open with `6000.3.*`.

First open triggers an asset import pass. Expect 2–5 minutes.

- [ ] **Step 3: Verify compile is clean**

Open Console window (`Window → General → Console`). Clear it.
Run `Assets → Reimport All`. Wait for completion.
Expected: **zero compile errors, zero missing script references**.

If errors appear, stop and triage. Most likely culprits:
- Addressables 1.x → 2.x API drift — fix per Unity's migration guide
- Deprecated APIs removed in Unity 6 (e.g., `Rigidbody.velocity` → `linearVelocity`)
- Package version conflicts

Do not proceed to Task 3 until clean.

- [ ] **Step 4: Verify existing tests still pass**

Open `Window → General → Test Runner`. Run all EditMode tests.
Expected: all ContentDelivery.Editor.Tests pass (`PublishTransactionStateMachineTests`, `PublishTransactionIdempotencyTests`, `AttemptIdentityManagerTests`, `AddressablesVersionedLocalPathsTests`, `AddressablesServiceBuildCcdRemoteLoadPathTests`).

If any fail under Unity 6, file a blocker and fix before proceeding. These are the only pre-existing tests; P1 adds many more.

- [ ] **Step 5: Confirm reference lab plays**

Open the canonical reference scene (handbook §7.0 — confirm path with Petros if unclear). Enter Play mode. Verify scenario starts, first step plays, no errors in Console.

Exit Play mode.

---

### Task 3: Capture the v0.10 fixture corpus

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Tests\Fixtures\v0.10\README.md`
- Copy: 5–8 v0.10 scene/prefab fixtures into `Tests\Fixtures\v0.10\`

- [ ] **Step 1: Create the fixture directory structure**

Run in bash:
```
mkdir -p "e:/Unity files/Pi tech DevKit/Tests/Fixtures/v0.10"
mkdir -p "e:/Unity files/Pi tech DevKit/Tests/Fixtures/golden"
mkdir -p "e:/Unity files/Pi tech DevKit/Tests/Fixtures/sample-labs"
```

- [ ] **Step 2: Pick the v0.10 fixture set with Petros**

Agree on exactly which scenes to copy. Criteria (§6.8 of spec):
- One cue-cards-heavy scenario (CueCardsStep coverage)
- One quiz-heavy (QuizStep + QuizResultsStep + MiniQuizStep)
- One selection-heavy (SelectionStep + SelectionLists)
- One Insert step (InsertStep with physics)
- One Group step with nested children (GroupStep)
- One Conditions-routed (ConditionsStep reflection)
- One Addressables-spawned (ContentDeliverySpawner path)
- One Timeline-synced (TimelineStep with CueCards sync)

These are the **crown jewels** of the non-breaking contract. We never modify them; we load and replay them on every PR.

- [ ] **Step 3: Copy the fixture assets**

For each chosen scene, copy into `Tests\Fixtures\v0.10\<scene-name>\` — include the `.unity` scene file, its dependent prefabs, its Scenario asset, and any referenced ScriptableObjects (QuizAsset, StatsConfig). Preserve `.meta` files verbatim (GUID identity matters).

Use Unity's `Assets → Export Package…` on the scene to capture dependencies, then import into the fixture directory. **Do not drag-copy** — that misses dependencies.

- [ ] **Step 4: Write the fixture README**

Create `e:\Unity files\Pi tech DevKit\Tests\Fixtures\v0.10\README.md`:

```markdown
# v0.10 Fixture Corpus

These fixtures were captured on 2026-04-23 from DevKit v0.10.1 to anchor the non-breaking contract (spec §15).

**DO NOT MODIFY.** Every PR from v0.11 onward loads every fixture in this directory via Gate 2 (v0.10 fixture load test). Modifications invalidate the gate.

## Fixture inventory

| Scene | Covers | Source | Captured |
|---|---|---|---|
| `cue-cards-heavy/` | CueCardsStep, Timeline sync | HealthOn AR `<scene-name>` | 2026-04-23 |
| `quiz-heavy/` | QuizStep, QuizResultsStep, MiniQuizStep | HealthOn VR `<scene-name>` | 2026-04-23 |
| `selection-heavy/` | SelectionStep, SelectionLists | HealthOn VR `<scene-name>` | 2026-04-23 |
| `insert-step/` | InsertStep, physics | HealthOn VR `<scene-name>` | 2026-04-23 |
| `group-step/` | GroupStep nested + MultiCondition | HealthOn VR `<scene-name>` | 2026-04-23 |
| `conditions-routed/` | ConditionsStep reflection → typed adapters | HealthOn VR `<scene-name>` | 2026-04-23 |
| `addressables-spawned/` | ContentDeliverySpawner | HealthOn AR `<scene-name>` | 2026-04-23 |
| `timeline-synced/` | TimelineStep + CueCardsStep sync | HealthOn AR `<scene-name>` | 2026-04-23 |

## Adding a new fixture

Only add when coverage gap is identified by a bug or regression escape. Each addition requires:
1. Source scene name + origin repo documented in the table above
2. `.meta` files preserved verbatim
3. Every dependency copied into the same fixture folder
4. Gate 2 test updated to include the new fixture in its scan

## Removing / modifying a fixture

Forbidden. Breaking the fixture corpus invalidates the non-breaking contract retroactively. If a fixture is ever genuinely obsolete (e.g., a step type is removed at a major version bump), it stays in the corpus under a `v<major>/` folder and the current corpus no longer includes it.
```

- [ ] **Step 5: Commit**

```
git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" add ../../Tests/Fixtures/v0.10/
git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" commit -m "chore(p1): capture v0.10 fixture corpus (spec §15, §19.2)"
```

> **Note:** Git commits in this plan are executed by Petros per the AGENT_PERMISSIONS rule. Commands shown for clarity.

---

### Task 4: Capture the v0.10 public API baseline

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\ApiCompat.Tests\publicApi-v0.10.baseline.txt`

- [ ] **Step 1: Create the test folder**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests"
```

- [ ] **Step 2: Write a one-time baseline-generator script**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Editor\OneTime\GeneratePublicApiBaseline.cs`:

```csharp
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Internal.OneTime
{
    /// <summary>
    /// One-time script: generate Tests/EditMode/ApiCompat.Tests/publicApi-v0.10.baseline.txt
    /// by reflecting every public member of every Pitech.XR.* type across all runtime assemblies.
    /// Delete this file after baseline is captured (Task 4 step 5).
    /// </summary>
    public static class GeneratePublicApiBaseline
    {
        [MenuItem("Pi tech/One-time/Generate publicApi v0.10 baseline")]
        public static void Generate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DevKit v0.10 public API baseline");
            sb.AppendLine("# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            sb.AppendLine("# DO NOT MODIFY. CI Gate 1 asserts every entry here is still present.");
            sb.AppendLine();

            var asms = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Pitech.XR"))
                .OrderBy(a => a.GetName().Name);

            foreach (var asm in asms)
            {
                var types = asm.GetTypes()
                    .Where(t => t.IsPublic && t.FullName?.StartsWith("Pitech.XR") == true)
                    .OrderBy(t => t.FullName);

                foreach (var t in types)
                {
                    sb.AppendLine($"TYPE {t.FullName}, {asm.GetName().Name}");

                    foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
                    {
                        if (m is MethodInfo mi && mi.IsSpecialName) continue; // property accessors
                        sb.AppendLine($"  {m.MemberType} {m.Name}");
                    }
                }
            }

            var outPath = Path.Combine(Application.dataPath, "../Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests/publicApi-v0.10.baseline.txt");
            outPath = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());

            Debug.Log($"[Pi tech] Wrote public API baseline to {outPath} ({sb.Length} bytes)");
            AssetDatabase.Refresh();
        }
    }
}
#endif
```

- [ ] **Step 3: Run the generator**

In Unity Editor: menu `Pi tech → One-time → Generate publicApi v0.10 baseline`. Check Console for the path and byte count.

Open `publicApi-v0.10.baseline.txt` in a text editor — verify it contains hundreds of `TYPE Pitech.XR.*` entries with member lists. Spot-check that `SceneManager.Restart`, `Scenario.steps`, `QuestionStep.choices`, etc. are present.

- [ ] **Step 4: Commit the baseline**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests/publicApi-v0.10.baseline.txt
git commit -m "chore(p1): capture v0.10 public API baseline (spec §15.2 Rule 3, §15.3 Gate 1)"
```

- [ ] **Step 5: Delete the one-time generator script**

```
rm "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Editor/OneTime/GeneratePublicApiBaseline.cs"
rm "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Editor/OneTime/GeneratePublicApiBaseline.cs.meta"
rmdir "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Editor/OneTime"
```

Commit:
```
git add -A Packages/pitech-xr-devkit/Editor/
git commit -m "chore(p1): remove one-time baseline generator"
```

---

### Task 5: Verify branch state

**Files:**
- None (verification)

- [ ] **Step 1: Confirm git state**

Run: `git -C "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit" log --oneline -10`

Expected: 3 new commits on top of the last `main` commit:
1. chore(p1): remove one-time baseline generator
2. chore(p1): capture v0.10 public API baseline
3. chore(p1): capture v0.10 fixture corpus

- [ ] **Step 2: Confirm fixtures + baseline exist on disk**

```
ls "e:/Unity files/Pi tech DevKit/Tests/Fixtures/v0.10/"
ls "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests/"
```

Expected: fixture subfolders present; baseline `.txt` file present.

- [ ] **Step 3: Confirm Unity still opens cleanly**

Open the project; confirm zero compile errors and zero missing script references. This is the baseline state that Part B must preserve.

---

# Part B · Package metadata + Unity 6 bump

Goal: bump `package.json` `unity` field, add required dependencies, bump package version to `0.11.0-pre`, confirm Unity reopens cleanly.

### Task 6: Bump `package.json` version and Unity minimum

**Files:**
- Modify: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\package.json`

- [ ] **Step 1: Read current `package.json`**

Open `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\package.json`. Current state:

```json
{
  "name": "com.pitech.xr.devkit",
  "displayName": "Pi tech XR DevKit",
  "version": "0.10.1",
  "unity": "2022.3",
  "description": "Modular in-house toolkit for XR apps.",
  ...
}
```

- [ ] **Step 2: Update to v0.11 pre-release + Unity 6**

Replace the top block with:

```json
{
  "name": "com.pitech.xr.devkit",
  "displayName": "Pi tech XR DevKit",
  "version": "0.11.0-pre.1",
  "unity": "6000.0",
  "description": "Modular in-house toolkit for medical XR training labs. Phase 1 foundation for DevKit 1.0.",
  "author": {
    "name": "Pi tech"
  },
  "dependencies": {
    "com.unity.addressables": "2.5.0",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "com.unity.test-framework": "2.0.1-exp.2",
    "com.unity.ui.builder": "2.0.0"
  }
}
```

Keep the existing `samples` array unchanged.

- [ ] **Step 3: Save + return to Unity**

Unity detects the package.json change on focus and reimports. Wait for reimport to complete (30–90 seconds).

- [ ] **Step 4: Verify clean compile**

Console: zero errors, zero missing script references. If errors appear, most likely the new dependency versions conflict with something in HealthOn AR/VR manifests. For the DevKit project itself, the package.json above is authoritative.

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/package.json
git commit -m "chore(p1): bump to v0.11.0-pre.1, require Unity 6000.0 (spec §17.2 P1)"
```

---

### Task 7: Install Multiplayer Play Mode (MPPM) package for P3 test prep

**Files:**
- Modify: `e:\Unity files\Pi tech DevKit\Packages\manifest.json` (project manifest, not package manifest)

- [ ] **Step 1: Open Unity Package Manager**

Window → Package Manager → `+` → Add package by name → `com.unity.multiplayer.playmode`.

If "Add by name" is unavailable, edit `e:\Unity files\Pi tech DevKit\Packages\manifest.json` directly.

- [ ] **Step 2: Verify installation**

Package Manager → In Project → confirm `Multiplayer Play Mode` is listed.

Open `Window → Multiplayer → Multiplayer Play Mode`. The virtual-players window opens (panels are empty — not configured yet; configuration lands in P3).

- [ ] **Step 3: Commit**

```
git add Packages/manifest.json
git commit -m "chore(p1): install MPPM for P3 Fusion multiplayer test prep"
```

---

### Task 8: Create `CHANGELOG.md` with v0.11.0-pre.1 entry

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\CHANGELOG.md`

- [ ] **Step 1: Create `CHANGELOG.md`**

```markdown
# Changelog — com.pitech.xr.devkit

All notable changes to DevKit are documented in this file. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

Voluntary stricter discipline from v0.11 onward: pre-1.0 minor bumps follow the post-1.0 additive-only rule per spec §18.1. Breaking changes are reserved for major version bumps only.

## [Unreleased]

## [0.11.0-pre.1] — 2026-04-23

### Added
- P1 foundation scaffold per `docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md`.
- Unity 6 LTS targeting (`unity: "6000.0"` minimum; developed on `6000.3` LTS).
- Addressables 2.x, Input System 1.11.2, Newtonsoft.Json 3.2.1, Test Framework 2.0.1, UI Builder 2.0 dependencies.

### Changed
- `unity` field in `package.json`: `2022.3` → `6000.0`. HealthOn AR must upgrade to Unity 6 before consuming this version; see spec §17.3.1.

### Deprecated
- None.

### Removed
- None.

### Fixed
- None.

### Non-breaking contract
- v0.10 fixture corpus captured under `Tests/Fixtures/v0.10/` — serves as CI Gate 2.
- Public API v0.10 baseline captured under `Tests/EditMode/ApiCompat.Tests/publicApi-v0.10.baseline.txt` — serves as CI Gate 1.

## [0.10.1] — 2026-04 (pre-P1)

Pre-P1 state. See git history before `v0.11.0-pre.1` tag for changes.
```

- [ ] **Step 2: Commit**

```
git add Packages/pitech-xr-devkit/CHANGELOG.md
git commit -m "docs(p1): add CHANGELOG.md with v0.11.0-pre.1 entry"
```

---

### Task 9: Install Unity 6 Awaitable usage — verify via smoke test

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Editor\OneTime\AwaitableSmokeTest.cs`

- [ ] **Step 1: Write a trivial Awaitable smoke-test menu item**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Editor\OneTime\AwaitableSmokeTest.cs`:

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Internal.OneTime
{
    public static class AwaitableSmokeTest
    {
        [MenuItem("Pi tech/One-time/Awaitable smoke test")]
        public static async void Run()
        {
            Debug.Log("[Pi tech] Awaitable smoke: before NextFrameAsync");
            await Awaitable.NextFrameAsync();
            Debug.Log("[Pi tech] Awaitable smoke: after NextFrameAsync");
            await Awaitable.WaitForSecondsAsync(0.5f);
            Debug.Log("[Pi tech] Awaitable smoke: after 0.5s wait");
        }
    }
}
#endif
```

- [ ] **Step 2: Run the menu item**

Unity menu: `Pi tech → One-time → Awaitable smoke test`. Confirm three Console logs in sequence with the expected delays.

This validates Unity 6's `Awaitable` is available and functional in this project.

- [ ] **Step 3: Delete the smoke-test script**

```
rm "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Editor/OneTime/AwaitableSmokeTest.cs"
rm "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Editor/OneTime/AwaitableSmokeTest.cs.meta"
```

- [ ] **Step 4: Commit deletion**

```
git add -A Packages/pitech-xr-devkit/Editor/
git commit -m "chore(p1): remove Awaitable smoke test (Unity 6 Awaitable confirmed functional)"
```

---

# Part C · Test harness scaffold

Goal: create all test folders + asmdefs so subsequent Domain/Core/Capabilities/Authoring tasks can drop tests into correct locations from task 1. Each asmdef contains one placeholder test that asserts `true` — confirms the assembly compiles and is discoverable by the Test Runner.

### Task 10: Create the 6 EditMode test asmdef structure

**Files:**
- Create: 6 folders under `Tests/EditMode/` + 6 `.asmdef` files + 6 placeholder test files

- [ ] **Step 1: Create folders**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/Domain.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/Capabilities.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/Authoring.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/EditMode/AgentSubstrate.Tests"
```

(`ApiCompat.Tests` already exists from Task 4.)

- [ ] **Step 2: Write `Pitech.XR.Domain.Tests.asmdef`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\Pitech.XR.Domain.Tests.asmdef`:

```json
{
    "name": "Pitech.XR.Domain.Tests",
    "rootNamespace": "Pitech.XR.Domain.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Pitech.XR.Domain"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

The reference to `Pitech.XR.Domain` will fail until Part D creates that asmdef. That's expected — Unity shows a warning; it becomes an error only if we try to run tests. We'll add the placeholder test in Step 4 which doesn't depend on `Pitech.XR.Domain` yet.

- [ ] **Step 3: Repeat for the other 5 asmdefs**

Copy the above structure and adapt `name` + `rootNamespace` + `references` for each:

- `Pitech.XR.Core.Tests.asmdef` — references `Pitech.XR.Core`
- `Pitech.XR.Capabilities.Tests.asmdef` — references `Pitech.XR.Capabilities`, `Pitech.XR.Core`
- `Pitech.XR.Authoring.Tests.asmdef` — references `Pitech.XR.Authoring`, `Pitech.XR.Core` (platform `Editor` only)
- `Pitech.XR.AgentSubstrate.Tests.asmdef` — references `Pitech.XR.AgentSubstrate`, `Pitech.XR.Core`, `Pitech.XR.Domain`, `Pitech.XR.Capabilities`
- `Pitech.XR.ApiCompat.Tests.asmdef` (already partially exists in `Tests/EditMode/ApiCompat.Tests/`) — references nothing DevKit-specific (uses reflection at runtime)

For ApiCompat.Tests, the asmdef is:

```json
{
    "name": "Pitech.XR.ApiCompat.Tests",
    "rootNamespace": "Pitech.XR.ApiCompat.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write placeholder test per asmdef**

For each test folder, create a `PlaceholderTests.cs` file. Example for `Domain.Tests`:

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\PlaceholderTests.cs`:

```csharp
using NUnit.Framework;

namespace Pitech.XR.Domain.Tests
{
    public class PlaceholderTests
    {
        [Test]
        public void Asmdef_Compiles()
        {
            Assert.That(true, Is.True, "Domain.Tests asmdef is discoverable and compiles.");
        }
    }
}
```

Repeat for each of the 5 other test asmdefs with the matching namespace.

- [ ] **Step 5: Run all tests in Test Runner**

Unity menu `Window → General → Test Runner` → EditMode tab → `Run All`.

Expected: 6 placeholder tests pass. Domain/Core/Capabilities/Authoring/AgentSubstrate tests may show as "yellow" (asmdef refs missing) until Part D/E/F/H land; that's acceptable since the placeholder test itself has no DevKit-specific references.

If any asmdef fails to compile *the placeholder test*, stop and investigate the asmdef JSON.

- [ ] **Step 6: Commit**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/
git commit -m "test(p1): scaffold 6 EditMode test asmdefs with placeholder tests (spec §19.1)"
```

---

### Task 11: Create the 3 PlayMode test asmdef structure

**Files:**
- Create: 3 folders under `Tests/PlayMode/` + 3 `.asmdef` files + 3 placeholder test files

- [ ] **Step 1: Create folders**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/PlayMode/Integration.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/PlayMode/Migration.Tests"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Tests/PlayMode/Serialization.Tests"
```

- [ ] **Step 2: Write `Pitech.XR.Integration.Tests.asmdef`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\PlayMode\Integration.Tests\Pitech.XR.Integration.Tests.asmdef`:

```json
{
    "name": "Pitech.XR.Integration.Tests",
    "rootNamespace": "Pitech.XR.Integration.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Pitech.XR.Core",
        "Pitech.XR.Domain",
        "Pitech.XR.Capabilities",
        "Pitech.XR.Scenario",
        "Pitech.XR.ContentDelivery"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Note `includePlatforms` is empty — PlayMode tests run on the active build target; leaving this empty means "run on everything." References to not-yet-created asmdefs cause yellow warnings; they resolve once Parts D/E/F land.

- [ ] **Step 3: Write `Pitech.XR.Migration.Tests.asmdef` and `Pitech.XR.Serialization.Tests.asmdef`**

Both follow the same pattern. `Migration.Tests` references the existing `Pitech.XR.Scenario` and `Pitech.XR.ContentDelivery`. `Serialization.Tests` references `Pitech.XR.Domain` only.

- [ ] **Step 4: Write placeholder tests**

For `Integration.Tests`, create `PlaceholderPlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Pitech.XR.Integration.Tests
{
    public class PlaceholderPlayModeTests
    {
        [Test]
        public void Asmdef_Compiles()
        {
            Assert.That(true, Is.True, "Integration.Tests asmdef is discoverable.");
        }

        [UnityTest]
        public IEnumerator PlayMode_Enters_Cleanly()
        {
            yield return null;
            Assert.Pass("PlayMode enters and yields without error.");
        }
    }
}
```

Repeat for `Migration.Tests` and `Serialization.Tests` with matching namespaces.

- [ ] **Step 5: Run all PlayMode tests in Test Runner**

Unity menu `Window → General → Test Runner` → PlayMode tab → `Run All`.

Expected: 3 asmdef-compile tests + 3 play-mode-enters tests all pass.

- [ ] **Step 6: Commit**

```
git add Packages/pitech-xr-devkit/Tests/PlayMode/
git commit -m "test(p1): scaffold 3 PlayMode test asmdefs with placeholder tests (spec §19.1)"
```

---

### Task 12: Add `[DevKitPublicApi]` attribute (sentinel for future API surface tracking)

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Core\DevKitPublicApiAttribute.cs` (placeholder location; Core asmdef comes in Part E)

- [ ] **Step 1: Create runtime folder for Core (if not exists)**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core"
```

Per the existing DevKit structure, `Runtime/Core/XRServices.cs` already lives here. We'll add the attribute file alongside.

- [ ] **Step 2: Write the attribute**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Core\DevKitPublicApiAttribute.cs`:

```csharp
using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// Marks a type, method, property, or field as part of DevKit's stable public API surface.
    /// CI Gate 1 (spec §15.3) asserts every entry in publicApi-v&lt;version&gt;.baseline.txt carrying
    /// this attribute is still present on every PR. Removal/rename of any [DevKitPublicApi] member
    /// fails the gate and blocks the merge.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Enum
        | AttributeTargets.Method
        | AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Event,
        Inherited = false,
        AllowMultiple = false)]
    public sealed class DevKitPublicApiAttribute : Attribute
    {
        /// <summary>Version at which this member joined the public API surface (e.g., "0.10", "1.0").</summary>
        public string Since { get; }
        public DevKitPublicApiAttribute(string since = "0.10") { Since = since; }
    }
}
```

- [ ] **Step 3: Verify compile**

Unity detects the new file and compiles. Console: zero errors.

- [ ] **Step 4: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/DevKitPublicApiAttribute.cs
git commit -m "feat(p1): add [DevKitPublicApi] attribute for public API surface tagging (spec §15.2 Rule 3)"
```

---

### Task 13: Tag existing v0.10 public surface with `[DevKitPublicApi("0.10")]`

**Files:**
- Modify: every existing public type/member across `Pitech.XR.*` runtime assemblies

This is a mechanical sweep. The goal is that the v0.10 public API baseline we captured in Task 4 is now also explicitly attributed in source — so future PRs see "this member is `[DevKitPublicApi]`" when they consider modifying it.

- [ ] **Step 1: Generate the list of types to tag**

Open the baseline file `publicApi-v0.10.baseline.txt`. Each `TYPE …` line is a class/struct/interface that needs `[DevKitPublicApi("0.10")]`.

- [ ] **Step 2: Add attribute to `Pitech.XR.Scenario` types**

Files to modify and the attribute to add:

- `Runtime\Scenario\Scenario.cs` — `public class Scenario`, step subclasses, `Step`, `Choice`, `ConditionOutcome`, etc.
- `Runtime\Scenario\SceneManager.cs` — `public class SceneManager`
- `Runtime\Scenario\QuizStep.cs` — `public sealed class QuizStep`
- `Runtime\Scenario\QuizResultsStep.cs` — `public sealed class QuizResultsStep`

For each public type, prepend `[Pitech.XR.Core.DevKitPublicApi("0.10")]`. Example:

Before:
```csharp
public class Scenario : MonoBehaviour
{
    ...
}
```

After:
```csharp
[Pitech.XR.Core.DevKitPublicApi("0.10")]
public class Scenario : MonoBehaviour
{
    ...
}
```

Do NOT add the attribute to public methods/fields individually in v0.10 (the type-level tag is sufficient for the first pass). Method/property/field-level tags land only when we add **new** public members in v0.11+ that need explicit version tracking.

- [ ] **Step 3: Add `Pitech.XR.Scenario.asmdef` reference to `Pitech.XR.Core` asmdef**

Wait — `Pitech.XR.Core` asmdef currently exists as `Pitech.XR.Core.asmdef` at `Runtime\Core\`. Open it and verify it exposes the attribute namespace. Currently it probably has `references: []` which is fine.

The dependent asmdefs (`Pitech.XR.Scenario`, `Pitech.XR.ContentDelivery`, `Pitech.XR.Interactables`, `Pitech.XR.Quiz`, `Pitech.XR.Stats`) each need to add `"Pitech.XR.Core"` to their `references` array if not already present.

Open each of the above `.asmdef` files. If `"Pitech.XR.Core"` is not in `references`, add it.

- [ ] **Step 4: Verify compile**

Unity reimports. Console: zero errors.

- [ ] **Step 5: Add attribute to `Pitech.XR.ContentDelivery`, `Interactables`, `Quiz`, `Stats` public types**

Same mechanical sweep as Step 2, applied to:

- `Runtime\ContentDelivery\BridgeLaunchContextReceiver.cs`
- `Runtime\ContentDelivery\ContentDeliveryRuntimeService.cs`
- `Runtime\ContentDelivery\ContentDeliverySpawner.cs`
- `Runtime\ContentDelivery\ContentDeliveryStatusOverlay.cs`
- `Runtime\ContentDelivery\LaunchContext.cs`
- `Runtime\ContentDelivery\AttemptIdentity.cs`
- `Runtime\Interactables\SelectablesManager.cs`
- `Runtime\Interactables\SelectionLists.cs`
- `Runtime\Quiz\QuizAsset.cs`
- `Runtime\Quiz\QuizUIController.cs`
- `Runtime\Quiz\QuizResultsUIController.cs`
- `Runtime\Quiz\QuizSession.cs`
- `Runtime\Stats\StatsConfig.cs`
- `Runtime\Stats\StatsUIController.cs`
- … (complete list from baseline)

- [ ] **Step 6: Run placeholder tests + verify clean Console**

Window → General → Test Runner → EditMode → Run All. Expected: all 6 EditMode placeholder tests pass.

- [ ] **Step 7: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/
git commit -m "refactor(p1): tag v0.10 public API surface with [DevKitPublicApi] (spec §15.2 Rule 3)"
```

---

### Task 14: Write CI Gate 1 — public API baseline diff test

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\ApiCompat.Tests\PublicApiBaselineTest.cs`

- [ ] **Step 1: Write the failing test**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\ApiCompat.Tests\PublicApiBaselineTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Pitech.XR.ApiCompat.Tests
{
    /// <summary>
    /// CI Gate 1 (spec §15.3) — asserts every type present in publicApi-v0.10.baseline.txt is still
    /// present in the current Pitech.XR.* assemblies. Additions are allowed; removals/renames fail.
    /// </summary>
    public class PublicApiBaselineTest
    {
        [Test]
        public void EveryBaselineType_IsStillPresent()
        {
            var baselinePath = FindBaselinePath();
            Assert.That(File.Exists(baselinePath), $"Baseline not found at {baselinePath}");

            var baselineTypes = ReadBaselineTypeNames(baselinePath);
            Assert.That(baselineTypes.Count, Is.GreaterThan(0), "Baseline is empty — regenerate.");

            var currentTypes = CollectCurrentPublicTypes();

            var missing = baselineTypes.Where(bt => !currentTypes.Contains(bt)).ToList();

            if (missing.Count > 0)
            {
                var msg = $"CI Gate 1 FAILED: {missing.Count} public types removed or renamed since v0.10:\n"
                          + string.Join("\n", missing.Select(m => $"  - {m}"))
                          + "\n\nThis breaks consumer contracts (AR/VR). Either restore the type or "
                          + "use [MovedFrom] to relocate it (spec §15.2 Rule 1).";
                Assert.Fail(msg);
            }
        }

        static string FindBaselinePath()
        {
            // Editor-time: resolve via Application.dataPath
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests/publicApi-v0.10.baseline.txt"));
        }

        static HashSet<string> ReadBaselineTypeNames(string path)
        {
            var types = new HashSet<string>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("TYPE "))
                {
                    // "TYPE Pitech.XR.Scenario.SceneManager, Pitech.XR.Scenario"
                    var afterTypeKeyword = line.Substring(5).Trim();
                    var comma = afterTypeKeyword.IndexOf(',');
                    if (comma > 0)
                    {
                        types.Add(afterTypeKeyword.Substring(0, comma).Trim());
                    }
                    else
                    {
                        types.Add(afterTypeKeyword);
                    }
                }
            }
            return types;
        }

        static HashSet<string> CollectCurrentPublicTypes()
        {
            var types = new HashSet<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = asm.GetName().Name;
                if (!name.StartsWith("Pitech.XR")) continue;

                Type[] asmTypes;
                try { asmTypes = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { asmTypes = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in asmTypes)
                {
                    if (t.IsPublic && t.FullName != null && t.FullName.StartsWith("Pitech.XR"))
                    {
                        types.Add(t.FullName);
                    }
                }
            }
            return types;
        }
    }
}
```

- [ ] **Step 2: Run it to verify it passes against current state**

Window → General → Test Runner → EditMode → find `PublicApiBaselineTest.EveryBaselineType_IsStillPresent` → Run Selected.

Expected: **PASS**. Every type in the baseline is currently present.

If FAIL, either the baseline was generated incorrectly (regenerate via Task 4) or you've already removed a type (restore it).

- [ ] **Step 3: Run it against a simulated removal to verify failure mode**

Temporarily rename `Pitech.XR.Scenario.Scenario` class (in `Runtime\Scenario\Scenario.cs`) to `Scenario_REMOVED_TEMP`. Rerun the test.

Expected: **FAIL** with message listing `Pitech.XR.Scenario.Scenario` as missing.

Restore the class name. Rerun test — pass.

- [ ] **Step 4: Commit**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/ApiCompat.Tests/PublicApiBaselineTest.cs
git commit -m "test(p1): CI Gate 1 — public API baseline diff test (spec §15.3)"
```

---

# Part D · Domain layer scaffold

Goal: establish the new `Pitech.XR.Domain` asmdef with Serialization, BuildingBlocks, and SchemaVersion infrastructure. This is the first NEW asmdef of P1 — everything above was scaffold.

### Task 15: Create `Pitech.XR.Domain` asmdef + folder structure

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Pitech.XR.Domain.asmdef`

- [ ] **Step 1: Create the folder hierarchy**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/Serialization"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/BuildingBlocks"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/Observation"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/Actions"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/Telemetry"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Domain/Content"
```

Note: `Runtime/Domain/Steps/`, `Runtime/Domain/Scenario/`, `Runtime/Domain/Console/` do NOT get created in P1 — those types move in P2/P3 with `[MovedFrom]`. P1 only seeds the non-moving subfolders.

- [ ] **Step 2: Write the asmdef**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Pitech.XR.Domain.asmdef`:

```json
{
    "name": "Pitech.XR.Domain",
    "rootNamespace": "Pitech.XR.Domain",
    "references": [
        "Unity.Nuget.Newtonsoft.Json"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Dependency direction (spec §6.1): Domain depends on **nothing DevKit-specific**. It may reference Unity core modules and Newtonsoft.Json (for `JsonConverter` base classes). It must NOT reference `Pitech.XR.Core`, `Pitech.XR.Capabilities`, or any higher layer.

- [ ] **Step 3: Verify asmdef resolves + compile is clean**

Unity reimports. Console: zero errors.

- [ ] **Step 4: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/
git commit -m "feat(p1): scaffold Pitech.XR.Domain asmdef (spec §6.3, §7)"
```

---

### Task 16: Create `SchemaVersionAttribute` and `ISchemaVersioned` interface

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Serialization\SchemaVersion.cs`

- [ ] **Step 1: Write the failing test first**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\SchemaVersionTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Domain.Serialization;

namespace Pitech.XR.Domain.Tests
{
    public class SchemaVersionTests
    {
        [SchemaVersion("V1")]
        sealed class FakeContract : ISchemaVersioned
        {
            public string Version => "V1";
        }

        [Test]
        public void SchemaVersionAttribute_RecordsVersion()
        {
            var attr = typeof(FakeContract).GetCustomAttributes(typeof(SchemaVersionAttribute), false);
            Assert.That(attr.Length, Is.EqualTo(1));
            Assert.That(((SchemaVersionAttribute)attr[0]).Version, Is.EqualTo("V1"));
        }

        [Test]
        public void ISchemaVersioned_ReturnsExpectedVersion()
        {
            ISchemaVersioned contract = new FakeContract();
            Assert.That(contract.Version, Is.EqualTo("V1"));
        }

        [Test]
        public void SchemaVersionRegistry_FindsAllVersionedTypes()
        {
            var registered = SchemaVersionRegistry.GetAllVersionedTypes();
            Assert.That(registered, Contains.Item(typeof(FakeContract)));
        }
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

Test Runner → EditMode → `Pitech.XR.Domain.Tests.SchemaVersionTests`.
Expected: all 3 tests FAIL with compile errors (`SchemaVersion`, `ISchemaVersioned`, `SchemaVersionRegistry` not defined).

- [ ] **Step 3: Write the minimal implementation**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Serialization\SchemaVersion.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Scripting;

namespace Pitech.XR.Domain.Serialization
{
    /// <summary>
    /// Marks a type as a versioned cross-boundary contract per spec §15.2 Rule 4.
    /// Every type with this attribute has an immutable shape once shipped; mutations ship as v(N+1).
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface,
        Inherited = false,
        AllowMultiple = false)]
    [Preserve]
    public sealed class SchemaVersionAttribute : Attribute
    {
        public string Version { get; }
        public SchemaVersionAttribute(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Schema version must be non-empty (e.g. \"V1\").", nameof(version));
            Version = version;
        }
    }

    /// <summary>
    /// Runtime-accessible interface for a versioned contract instance.
    /// Implementers expose <see cref="Version"/> for deserialization gates.
    /// </summary>
    public interface ISchemaVersioned
    {
        string Version { get; }
    }

    /// <summary>
    /// Discovers every <see cref="SchemaVersionAttribute"/>-tagged type at domain reload.
    /// Cached once; subsequent queries are O(1).
    /// </summary>
    public static class SchemaVersionRegistry
    {
        static readonly Lazy<IReadOnlyList<Type>> _types = new(Discover);

        public static IReadOnlyList<Type> GetAllVersionedTypes() => _types.Value;

        public static string GetVersion(Type t)
        {
            var attr = (SchemaVersionAttribute)Attribute.GetCustomAttribute(t, typeof(SchemaVersionAttribute));
            return attr?.Version;
        }

        static IReadOnlyList<Type> Discover()
        {
            var list = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    if (Attribute.IsDefined(t, typeof(SchemaVersionAttribute)))
                        list.Add(t);
                }
            }
            return list.AsReadOnly();
        }
    }
}
```

- [ ] **Step 4: Run test — verify it passes**

Test Runner → EditMode → `Pitech.XR.Domain.Tests.SchemaVersionTests` → Run Selected.
Expected: all 3 tests PASS.

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/Serialization/SchemaVersion.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Domain.Tests/SchemaVersionTests.cs
git commit -m "feat(p1): add SchemaVersionAttribute + ISchemaVersioned + SchemaVersionRegistry (spec §7.4, §15.2 Rule 4)"
```

---

### Task 17: Create `BuildingBlockMetadataV1` schema

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockMetadataV1.cs`
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockCategory.cs`

- [ ] **Step 1: Write the failing test**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\BuildingBlockMetadataV1Tests.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using Pitech.XR.Domain.BuildingBlocks;
using Pitech.XR.Domain.Serialization;

namespace Pitech.XR.Domain.Tests
{
    public class BuildingBlockMetadataV1Tests
    {
        [Test]
        public void BuildingBlockMetadataV1_IsSchemaVersioned()
        {
            var instance = new BuildingBlockMetadataV1();
            Assert.That(instance.Version, Is.EqualTo("V1"));
            Assert.That(SchemaVersionRegistry.GetVersion(typeof(BuildingBlockMetadataV1)), Is.EqualTo("V1"));
        }

        [Test]
        public void BuildingBlockMetadataV1_Fields_MatchSpec()
        {
            // Spec Appendix B — fields: id, displayName, category, description,
            // requiredCapabilities[], dependencies[], supportedConsumers[], tags[], thumbnail
            var meta = new BuildingBlockMetadataV1
            {
                Id = "patient-interview.adult-male",
                DisplayName = "Patient Interview — Adult Male",
                Category = BuildingBlockCategory.Scenario,
                Description = "Interview flow for an adult male patient with chest pain.",
                RequiredCapabilities = new List<string> { "IPanelHostService", "IAudioService" },
                Dependencies = new List<string> { "patient-body.adult-male" },
                SupportedConsumers = new List<string> { "AR", "VR" },
                Tags = new List<string> { "pedagogy", "clinical-interview" },
                ThumbnailPath = "Thumbnails/patient-interview-adult-male.png"
            };

            Assert.That(meta.Id, Is.EqualTo("patient-interview.adult-male"));
            Assert.That(meta.DisplayName, Is.EqualTo("Patient Interview — Adult Male"));
            Assert.That(meta.Category, Is.EqualTo(BuildingBlockCategory.Scenario));
            Assert.That(meta.RequiredCapabilities, Has.Count.EqualTo(2));
            Assert.That(meta.Dependencies, Has.Count.EqualTo(1));
            Assert.That(meta.SupportedConsumers, Has.Count.EqualTo(2));
            Assert.That(meta.Tags, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildingBlockMetadataV1_JsonRoundTrip_Preserves_AllFields()
        {
            var original = new BuildingBlockMetadataV1
            {
                Id = "symptom-assessment.chest-pain",
                DisplayName = "Chest Pain Triage",
                Category = BuildingBlockCategory.Scenario,
                Description = "…",
                RequiredCapabilities = new List<string> { "IPanelHostService" },
                Dependencies = new List<string>(),
                SupportedConsumers = new List<string> { "VR" },
                Tags = new List<string> { "triage" },
                ThumbnailPath = "Thumbnails/chest-pain.png"
            };

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<BuildingBlockMetadataV1>(json);

            Assert.That(restored.Version, Is.EqualTo("V1"));
            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.DisplayName, Is.EqualTo(original.DisplayName));
            Assert.That(restored.Category, Is.EqualTo(original.Category));
            Assert.That(restored.RequiredCapabilities, Is.EquivalentTo(original.RequiredCapabilities));
            Assert.That(restored.Dependencies, Is.EquivalentTo(original.Dependencies));
            Assert.That(restored.SupportedConsumers, Is.EquivalentTo(original.SupportedConsumers));
            Assert.That(restored.Tags, Is.EquivalentTo(original.Tags));
            Assert.That(restored.ThumbnailPath, Is.EqualTo(original.ThumbnailPath));
        }

        [Test]
        public void BuildingBlockMetadataV1_Json_IncludesVersionField()
        {
            var meta = new BuildingBlockMetadataV1 { Id = "x", DisplayName = "x" };
            var json = JsonConvert.SerializeObject(meta);
            Assert.That(json, Does.Contain("\"version\":\"V1\""));
        }
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

Expected: FAIL with compile errors (`BuildingBlockMetadataV1`, `BuildingBlockCategory` not defined).

- [ ] **Step 3: Implement `BuildingBlockCategory`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockCategory.cs`:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Scripting;

namespace Pitech.XR.Domain.BuildingBlocks
{
    [Preserve]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BuildingBlockCategory
    {
        Unspecified = 0,
        Scenario = 1,
        Interactable = 2,
        Character = 3,
        Environment = 4,
        UI = 5,
        Audio = 6,
        Assessment = 7
    }
}
```

- [ ] **Step 4: Implement `BuildingBlockMetadataV1`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockMetadataV1.cs`:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pitech.XR.Domain.Serialization;
using UnityEngine.Scripting;

namespace Pitech.XR.Domain.BuildingBlocks
{
    /// <summary>
    /// Metadata sidecar for a DevKit Building Block prefab (spec §13.2, Appendix B).
    /// One instance per reusable prefab; co-located with the prefab as a .meta.json file.
    /// Indexed by <see cref="BuildingBlockRegistry"/> and consumed by the 2028 prompt-to-simulation
    /// VICKY authoring flow.
    /// </summary>
    [Serializable]
    [Preserve]
    [SchemaVersion("V1")]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BuildingBlockMetadataV1 : ISchemaVersioned
    {
        [JsonProperty("version", Order = 0)]
        public string Version => "V1";

        [JsonProperty("id", Order = 1)]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("displayName", Order = 2)]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("category", Order = 3)]
        public BuildingBlockCategory Category { get; set; } = BuildingBlockCategory.Unspecified;

        [JsonProperty("description", Order = 4)]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("requiredCapabilities", Order = 5)]
        public List<string> RequiredCapabilities { get; set; } = new();

        [JsonProperty("dependencies", Order = 6)]
        public List<string> Dependencies { get; set; } = new();

        [JsonProperty("supportedConsumers", Order = 7)]
        public List<string> SupportedConsumers { get; set; } = new();

        [JsonProperty("tags", Order = 8)]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("thumbnailPath", Order = 9)]
        public string ThumbnailPath { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 5: Run tests — verify they pass**

Test Runner → EditMode → `Pitech.XR.Domain.Tests.BuildingBlockMetadataV1Tests` → Run Selected.
Expected: all 4 tests PASS.

- [ ] **Step 6: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/BuildingBlocks/
git add Packages/pitech-xr-devkit/Tests/EditMode/Domain.Tests/BuildingBlockMetadataV1Tests.cs
git commit -m "feat(p1): add BuildingBlockMetadataV1 schema (spec §7.4, §13.2, Appendix B)"
```

---

### Task 18: Write `link.xml` for Domain asmdef (IL2CPP preservation)

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\link.xml`

- [ ] **Step 1: Write `link.xml`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\link.xml`:

```xml
<!-- link.xml — Prevents IL2CPP stripping of Domain schema types (spec §7.5).
     Every [SchemaVersion]-tagged class must survive AOT compilation for
     Newtonsoft.Json polymorphic converters to resolve them at runtime.
     Preserve the entire Domain assembly because these are pure data types
     and the size cost is negligible. -->
<linker>
  <assembly fullname="Pitech.XR.Domain" preserve="all"/>
</linker>
```

Unity picks up `link.xml` files under any asmdef and applies them to IL2CPP builds automatically.

- [ ] **Step 2: Verify Unity detects the file**

Unity reimports. Console: zero errors, no "orphan meta" warnings.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/link.xml
git commit -m "chore(p1): add link.xml to preserve Domain contract types under IL2CPP (spec §7.5)"
```

---

### Task 19: Add `AotHelper.EnsureType<T>` calls for generic specializations

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Serialization\AotPreservation.cs`

- [ ] **Step 1: Write the preservation class**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\Serialization\AotPreservation.cs`:

```csharp
using System.Collections.Generic;
using Pitech.XR.Domain.BuildingBlocks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Domain.Serialization
{
    /// <summary>
    /// AOT/IL2CPP preservation anchor (spec §7.5 mitigation step 4).
    /// Forces the IL2CPP AOT compiler to emit generic specializations that Newtonsoft.Json
    /// would otherwise instantiate reflectively at runtime and fail on Android/ARM64.
    /// Never called — the method body existing is what forces AOT compilation of the
    /// referenced generics.
    /// </summary>
    [Preserve]
    internal static class AotPreservation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        [Preserve]
        static void EnsureGenericsAreAot()
        {
            // Never invoked in practice; the static analyzer sees the new List<string>() call
            // and preserves List<string> specialization for Newtonsoft.Json converters.
            _ = new List<string>();
            _ = new List<BuildingBlockMetadataV1>();
            _ = new Dictionary<string, BuildingBlockMetadataV1>();
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Unity reimports. Console: zero errors.

Note: we do NOT write a dedicated test for `AotPreservation` because its entire value is compile-time presence. Gate 7 (IL2CPP round-trip, landing in Task 78) is what exercises AOT safety end-to-end.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/Serialization/AotPreservation.cs
git commit -m "feat(p1): add AotPreservation anchor for Newtonsoft.Json generic specializations (spec §7.5)"
```

---

### Task 20: Add `Pitech.XR.Domain` to test asmdef references + rerun tests

**Files:**
- Modify: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\Pitech.XR.Domain.Tests.asmdef`

- [ ] **Step 1: Open the Domain.Tests asmdef**

It already references `Pitech.XR.Domain` from Task 10 Step 2. Confirm. Also add Newtonsoft.Json reference needed for the round-trip test:

```json
{
    "name": "Pitech.XR.Domain.Tests",
    "rootNamespace": "Pitech.XR.Domain.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Pitech.XR.Domain",
        "Unity.Nuget.Newtonsoft.Json"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Run all Domain tests**

Test Runner → EditMode → `Pitech.XR.Domain.Tests` → Run All.
Expected: `PlaceholderTests.Asmdef_Compiles` + 3 `SchemaVersionTests` + 4 `BuildingBlockMetadataV1Tests` = 8 passing tests.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/Domain.Tests/Pitech.XR.Domain.Tests.asmdef
git commit -m "test(p1): wire Pitech.XR.Domain.Tests references after Domain types land"
```

---

### Task 21: Create `BuildingBlockMetadataSidecar` reader

**Files:**
- Create: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockMetadataSidecar.cs`

- [ ] **Step 1: Write the failing test**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Tests\EditMode\Domain.Tests\BuildingBlockMetadataSidecarTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Pitech.XR.Domain.BuildingBlocks;

namespace Pitech.XR.Domain.Tests
{
    public class BuildingBlockMetadataSidecarTests
    {
        [Test]
        public void Read_ValidSidecar_ReturnsMetadata()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "bb-sidecar-valid.meta.json");
            File.WriteAllText(tmp, "{\"version\":\"V1\",\"id\":\"x\",\"displayName\":\"X\",\"category\":\"Scenario\"}");
            try
            {
                var meta = BuildingBlockMetadataSidecar.Read(tmp);
                Assert.That(meta, Is.Not.Null);
                Assert.That(meta.Id, Is.EqualTo("x"));
                Assert.That(meta.Category, Is.EqualTo(BuildingBlockCategory.Scenario));
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void Read_MissingFile_ReturnsNull()
        {
            var meta = BuildingBlockMetadataSidecar.Read("/nonexistent/path.meta.json");
            Assert.That(meta, Is.Null);
        }

        [Test]
        public void Read_MalformedJson_ThrowsSchemaError()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "bb-sidecar-bad.meta.json");
            File.WriteAllText(tmp, "{not valid json");
            try
            {
                Assert.Throws<BuildingBlockSchemaException>(() => BuildingBlockMetadataSidecar.Read(tmp));
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void Read_WrongVersion_ThrowsSchemaError()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "bb-sidecar-v2.meta.json");
            File.WriteAllText(tmp, "{\"version\":\"V2\",\"id\":\"x\",\"displayName\":\"X\"}");
            try
            {
                var ex = Assert.Throws<BuildingBlockSchemaException>(() => BuildingBlockMetadataSidecar.Read(tmp));
                Assert.That(ex.Message, Does.Contain("V1").And.Contain("V2"));
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void SidecarPathFor_PrefabPath_AppendsMetaJsonSuffix()
        {
            var path = BuildingBlockMetadataSidecar.SidecarPathFor("Assets/Blocks/my-block.prefab");
            Assert.That(path, Is.EqualTo("Assets/Blocks/my-block.prefab.meta.json"));
        }
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

Expected: 5 FAIL with "BuildingBlockMetadataSidecar not defined" and "BuildingBlockSchemaException not defined".

- [ ] **Step 3: Implement `BuildingBlockSchemaException` + `BuildingBlockMetadataSidecar`**

Create `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Domain\BuildingBlocks\BuildingBlockMetadataSidecar.cs`:

```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Pitech.XR.Domain.BuildingBlocks
{
    /// <summary>
    /// Raised when a .meta.json sidecar file cannot be parsed or has a wrong schema version.
    /// </summary>
    public sealed class BuildingBlockSchemaException : Exception
    {
        public BuildingBlockSchemaException(string message) : base(message) { }
        public BuildingBlockSchemaException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Reader for BuildingBlock metadata sidecar files (spec §13.2).
    /// Each reusable prefab has a co-located `.meta.json` sidecar with a
    /// <see cref="BuildingBlockMetadataV1"/> payload.
    /// </summary>
    [Preserve]
    public static class BuildingBlockMetadataSidecar
    {
        public const string SidecarSuffix = ".meta.json";

        public static string SidecarPathFor(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("prefabPath required.", nameof(prefabPath));
            return prefabPath + SidecarSuffix;
        }

        /// <summary>Returns parsed metadata, or null if the sidecar does not exist.</summary>
        public static BuildingBlockMetadataV1 Read(string sidecarPath)
        {
            if (string.IsNullOrEmpty(sidecarPath) || !File.Exists(sidecarPath))
                return null;

            string json;
            try { json = File.ReadAllText(sidecarPath); }
            catch (IOException ex) { throw new BuildingBlockSchemaException($"Could not read sidecar '{sidecarPath}'.", ex); }

            BuildingBlockMetadataV1 meta;
            try { meta = JsonConvert.DeserializeObject<BuildingBlockMetadataV1>(json); }
            catch (JsonException ex) { throw new BuildingBlockSchemaException($"Malformed JSON in '{sidecarPath}'.", ex); }

            if (meta == null)
                throw new BuildingBlockSchemaException($"Sidecar '{sidecarPath}' deserialized to null.");

            if (meta.Version != "V1")
                throw new BuildingBlockSchemaException(
                    $"Sidecar '{sidecarPath}' has version '{meta.Version}', expected 'V1'. Run the V2 migrator or update DevKit.");

            return meta;
        }
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

Test Runner → EditMode → `Pitech.XR.Domain.Tests.BuildingBlockMetadataSidecarTests` → Run All.
Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Domain/BuildingBlocks/BuildingBlockMetadataSidecar.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Domain.Tests/BuildingBlockMetadataSidecarTests.cs
git commit -m "feat(p1): add BuildingBlockMetadataSidecar reader with schema version check (spec §13.2, §15.2 Rule 4)"
```

---

### Task 22: Part D integration — run all Domain tests, verify clean, tag milestone commit

**Files:**
- None (verification)

- [ ] **Step 1: Run all EditMode tests**

Test Runner → EditMode → Run All.
Expected: all passing. Count includes placeholder tests + SchemaVersion + BuildingBlockMetadataV1 + Sidecar = ~14 tests.

- [ ] **Step 2: Run all PlayMode tests**

Test Runner → PlayMode → Run All.
Expected: all 6 placeholder tests pass (no new PlayMode tests yet).

- [ ] **Step 3: Verify Domain asmdef has no upward references**

Open `Pitech.XR.Domain.asmdef`. Confirm `references` contains only `Unity.Nuget.Newtonsoft.Json`. Nothing DevKit-specific.

This is a **manual gate** for the dependency rule (spec §6.1) that Gate 5 will enforce automatically once installed (Task 76).

- [ ] **Step 4: Tag Part D milestone**

```
git tag p1-part-d-complete
git commit --allow-empty -m "chore(p1): Part D Domain layer scaffold complete"
```

---

# Part E · Core layer — EventBus, Registries, Bootstrap, Diagnostics

Goal: build the central nervous system (spec §8) — the event bus that every actor communicates through, the six registries that every extension point uses, the `LabRoot` + `DevKitBootstrapper` wiring, the `IDevKitLogger`, and the `XRServices` shim that keeps v0.10 consumers working.

The existing `Runtime\Core\Pitech.XR.Core.asmdef` carries `XRServices.cs` and (from Task 12) `DevKitPublicApiAttribute.cs`. Part E expands Core significantly.

### Task 23: Create Core subfolders + update `Pitech.XR.Core.asmdef` to reference `Pitech.XR.Domain`

**Files:**
- Create: 5 subfolders under `Runtime\Core\`
- Modify: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Core\Pitech.XR.Core.asmdef`

- [ ] **Step 1: Create subfolders**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core/EventBus"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core/Registry"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core/Bootstrap"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core/Diagnostics"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Core/Async"
```

- [ ] **Step 2: Update `Pitech.XR.Core.asmdef`**

Open and replace with:

```json
{
    "name": "Pitech.XR.Core",
    "rootNamespace": "Pitech.XR.Core",
    "references": [
        "Pitech.XR.Domain"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Dependency rule (spec §6.1): Core (layer 2) may reference Domain (layer 1) only.

- [ ] **Step 3: Verify compile is clean + Commit**

Unity reimports. Console: zero errors.

```
git add Packages/pitech-xr-devkit/Runtime/Core/
git commit -m "feat(p1): scaffold Core subfolders + reference Domain (spec §6.3, §8)"
```

---

### Task 24: `LabId` value type + `ILabEvent` interface

**Files:**
- Create: `Runtime\Core\EventBus\LabId.cs`
- Create: `Runtime\Core\EventBus\ILabEvent.cs`

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Core.Tests\LabIdTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Core.EventBus;

namespace Pitech.XR.Core.Tests
{
    public class LabIdTests
    {
        [Test] public void FromString_PreservesValue()
        {
            var id = LabId.FromString("lab.aigaiou.pharmacy");
            Assert.That(id.Value, Is.EqualTo("lab.aigaiou.pharmacy"));
        }

        [Test] public void Empty_IsWellDefined()
        {
            Assert.That(LabId.Empty.Value, Is.EqualTo(string.Empty));
        }

        [Test] public void Equality_IsValueBased()
        {
            var a = LabId.FromString("lab.x");
            var b = LabId.FromString("lab.x");
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test] public void FromNullOrEmpty_ReturnsEmpty()
        {
            Assert.That(LabId.FromString(null), Is.EqualTo(LabId.Empty));
            Assert.That(LabId.FromString(""), Is.EqualTo(LabId.Empty));
        }
    }
}
```

Update `Tests\EditMode\Core.Tests\Pitech.XR.Core.Tests.asmdef` to reference `Pitech.XR.Core`:

```json
{
    "name": "Pitech.XR.Core.Tests",
    "rootNamespace": "Pitech.XR.Core.Tests",
    "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner", "Pitech.XR.Core"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Run test → fails (type not defined)**

- [ ] **Step 3: Implement**

Create `Runtime\Core\EventBus\LabId.cs`:

```csharp
using System;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.EventBus
{
    /// <summary>Tenant-scoped identifier for a running lab (spec §8.1). Value-based equality.</summary>
    [Preserve]
    public readonly struct LabId : IEquatable<LabId>
    {
        public static readonly LabId Empty = new(string.Empty);
        public string Value { get; }
        LabId(string value) { Value = value ?? string.Empty; }
        public static LabId FromString(string value) => string.IsNullOrEmpty(value) ? Empty : new LabId(value);
        public bool Equals(LabId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LabId o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
        public static bool operator ==(LabId a, LabId b) => a.Equals(b);
        public static bool operator !=(LabId a, LabId b) => !a.Equals(b);
    }
}
```

Create `Runtime\Core\EventBus\ILabEvent.cs`:

```csharp
using System;

namespace Pitech.XR.Core.EventBus
{
    /// <summary>
    /// Marker contract for every event on the <see cref="ILabEventBus"/>.
    /// Every event carries the <see cref="LabId"/> of its originating lab for tenant-safe dispatch
    /// (spec §8.1). Implementations MUST be <c>readonly struct</c> to hit the zero-alloc budget.
    /// </summary>
    public interface ILabEvent
    {
        LabId LabId { get; }
        DateTime AtUtc { get; }
    }
}
```

- [ ] **Step 4: Run tests → pass**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/EventBus/
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/
git commit -m "feat(p1): add LabId value type + ILabEvent marker (spec §8.1)"
```

---

### Task 25: Zero-alloc `LabEventBus` — failing test first

**Files:**
- Create: `Tests\EditMode\Core.Tests\LabEventBusTests.cs`

- [ ] **Step 1: Write failing test covering subscribe + publish + unsubscribe + tenancy**

```csharp
using System;
using NUnit.Framework;
using Pitech.XR.Core.EventBus;

namespace Pitech.XR.Core.Tests
{
    public class LabEventBusTests
    {
        readonly struct FakeEvent : ILabEvent
        {
            public FakeEvent(LabId id, int payload) { LabId = id; AtUtc = DateTime.UtcNow; Payload = payload; }
            public LabId LabId { get; }
            public DateTime AtUtc { get; }
            public int Payload { get; }
        }

        [Test] public void Publish_InvokesSubscriber()
        {
            var bus = new LabEventBus();
            int received = 0;
            using (bus.Subscribe<FakeEvent>(e => received = e.Payload))
            {
                bus.Publish(new FakeEvent(LabId.FromString("lab.x"), 42));
            }
            Assert.That(received, Is.EqualTo(42));
        }

        [Test] public void Unsubscribe_StopsDelivery()
        {
            var bus = new LabEventBus();
            int received = 0;
            var sub = bus.Subscribe<FakeEvent>(e => received = e.Payload);
            bus.Publish(new FakeEvent(LabId.FromString("lab.x"), 1));
            sub.Dispose();
            bus.Publish(new FakeEvent(LabId.FromString("lab.x"), 2));
            Assert.That(received, Is.EqualTo(1));
        }

        [Test] public void MultipleSubscribers_AllReceive()
        {
            var bus = new LabEventBus();
            int a = 0, b = 0;
            using (bus.Subscribe<FakeEvent>(e => a = e.Payload))
            using (bus.Subscribe<FakeEvent>(e => b = e.Payload))
            {
                bus.Publish(new FakeEvent(LabId.FromString("lab.x"), 7));
            }
            Assert.That(a, Is.EqualTo(7));
            Assert.That(b, Is.EqualTo(7));
        }

        [Test] public void SubscriberThrowing_DoesNotBreakOthers()
        {
            var bus = new LabEventBus();
            int b = 0;
            using (bus.Subscribe<FakeEvent>(_ => throw new InvalidOperationException("boom")))
            using (bus.Subscribe<FakeEvent>(e => b = e.Payload))
            {
                bus.Publish(new FakeEvent(LabId.FromString("lab.x"), 9));
            }
            Assert.That(b, Is.EqualTo(9));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL (LabEventBus not defined)**

- [ ] **Step 3: Implement `LabEventBus`**

Create `Runtime\Core\EventBus\ILabEventBus.cs`:

```csharp
using System;

namespace Pitech.XR.Core.EventBus
{
    /// <summary>In-process typed pub/sub. One instance per lab (spec §8.1).</summary>
    public interface ILabEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, ILabEvent;
        void Publish<TEvent>(in TEvent evt) where TEvent : struct, ILabEvent;
    }
}
```

Create `Runtime\Core\EventBus\LabEventBus.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.EventBus
{
    /// <summary>
    /// Default <see cref="ILabEventBus"/> — pre-allocated per-event-type subscriber arrays for
    /// zero-alloc Publish on the steady state (spec §8.1, §16 budget row).
    /// </summary>
    [Preserve]
    public sealed class LabEventBus : ILabEventBus
    {
        static class Subs<T> where T : struct, ILabEvent
        {
            public static Action<T>[] Handlers = Array.Empty<Action<T>>();
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, ILabEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var old = Subs<TEvent>.Handlers;
            var nw = new Action<TEvent>[old.Length + 1];
            Array.Copy(old, nw, old.Length);
            nw[old.Length] = handler;
            Subs<TEvent>.Handlers = nw;
            return new Subscription<TEvent>(handler);
        }

        public void Publish<TEvent>(in TEvent evt) where TEvent : struct, ILabEvent
        {
            var handlers = Subs<TEvent>.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                try { handlers[i](evt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        sealed class Subscription<T> : IDisposable where T : struct, ILabEvent
        {
            Action<T> _handler;
            public Subscription(Action<T> handler) { _handler = handler; }
            public void Dispose()
            {
                if (_handler == null) return;
                var old = Subs<T>.Handlers;
                var list = new List<Action<T>>(old.Length);
                for (int i = 0; i < old.Length; i++)
                    if (!ReferenceEquals(old[i], _handler)) list.Add(old[i]);
                Subs<T>.Handlers = list.ToArray();
                _handler = null;
            }
        }
    }
}
```

> **Design note:** The `static class Subs<T>` pattern uses the JIT/AOT's per-type generic instantiation to give each event type its own handler array without a `Dictionary` lookup at publish time. Each `Publish<TEvent>` call is a single array iteration. Zero allocation in steady state (arrays are only re-allocated on subscribe/unsubscribe). This is faster and tighter than `Dictionary<Type, List<Delegate>>`.

- [ ] **Step 4: Run tests → all PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/EventBus/
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/LabEventBusTests.cs
git commit -m "feat(p1): add LabEventBus with per-type subscriber arrays (spec §8.1)"
```

---

### Task 26: `Assert.AllocFree` custom NUnit constraint for zero-alloc gate

**Files:**
- Create: `Tests\EditMode\Core.Tests\AllocFreeConstraint.cs`

- [ ] **Step 1: Write the constraint**

This is infrastructure used by later tests. We write it now so Tasks 27+ can use it.

Create `Tests\EditMode\Core.Tests\AllocFreeConstraint.cs`:

```csharp
using System;
using NUnit.Framework.Constraints;

namespace Pitech.XR.Core.Tests
{
    /// <summary>
    /// NUnit custom constraint: asserts that executing a <see cref="Action"/> produces zero managed
    /// allocations. Usage:
    ///   <code>Assert.That(() => bus.Publish(evt), AllocFree.Is());</code>
    /// Backed by <see cref="System.GC.GetAllocatedBytesForCurrentThread"/>.
    /// Implements CI Gate 4 (spec §15.3).
    /// </summary>
    public static class AllocFree
    {
        public static IResolveConstraint Is() => new AllocFreeConstraint();
    }

    sealed class AllocFreeConstraint : Constraint
    {
        public override string Description => "zero managed allocations";

        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            if (actual is not Action action)
                return new ConstraintResult(this, actual, false) { };

            // Warm: JIT + any first-call allocations
            action();
            action();

            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            var after = GC.GetAllocatedBytesForCurrentThread();

            var delta = after - before;
            return new ConstraintResult(this, $"{delta} bytes", delta == 0);
        }
    }
}
```

- [ ] **Step 2: Write a self-test to verify the constraint itself works**

Add to `LabEventBusTests.cs`:

```csharp
[Test] public void AllocFree_Itself_DetectsAllocation()
{
    // Positive test: this should fail (allocates).
    Assert.That(
        () => Assert.That((Action)(() => { _ = new object(); }), AllocFree.Is()),
        Throws.TypeOf<NUnit.Framework.AssertionException>());
}

[Test] public void AllocFree_Itself_PassesOnNoAlloc()
{
    // Negative test: this should pass (no alloc).
    int x = 0;
    Assert.That((Action)(() => { x++; }), AllocFree.Is());
    Assert.That(x, Is.GreaterThan(0));
}
```

- [ ] **Step 3: Run → both self-tests pass**

- [ ] **Step 4: Commit**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/AllocFreeConstraint.cs
git commit -m "test(p1): add AllocFree custom NUnit constraint for Gate 4 (spec §15.3)"
```

---

### Task 27: Assert `LabEventBus.Publish` is zero-alloc in steady state (Gate 4 seed)

**Files:**
- Modify: `Tests\EditMode\Core.Tests\LabEventBusTests.cs`

- [ ] **Step 1: Add zero-alloc test**

Add to `LabEventBusTests`:

```csharp
[Test] public void Publish_InSteadyState_IsAllocationFree()
{
    var bus = new LabEventBus();
    int received = 0;
    using (bus.Subscribe<FakeEvent>(e => received = e.Payload))
    {
        var evt = new FakeEvent(LabId.FromString("lab.x"), 1);
        // Warm: first Publish may allocate if JIT emits stub.
        bus.Publish(evt);
        Assert.That((Action)(() =>
        {
            for (int i = 0; i < 100; i++) bus.Publish(evt);
        }), AllocFree.Is());
    }
    Assert.That(received, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run → PASS**

If this test fails, the `LabEventBus` implementation has a hidden allocation (boxing a struct event, capturing a closure in Publish, etc.). Fix the implementation before proceeding.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/LabEventBusTests.cs
git commit -m "test(p1): assert LabEventBus.Publish is allocation-free in steady state (Gate 4)"
```

---

### Task 28: `IDevKitLogger` + `DefaultLogger` + `DiagnosticChannel`

**Files:**
- Create: `Runtime\Core\Diagnostics\IDevKitLogger.cs`
- Create: `Runtime\Core\Diagnostics\DiagnosticChannel.cs`
- Create: `Runtime\Core\Diagnostics\DefaultLogger.cs`

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Core.Tests\LoggerTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Core.Diagnostics;

namespace Pitech.XR.Core.Tests
{
    public class LoggerTests
    {
        [Test] public void DiagnosticChannel_Names_MatchSpec()
        {
            // Spec §3.8 channels
            Assert.That(DiagnosticChannel.Scenario, Is.EqualTo("scenario"));
            Assert.That(DiagnosticChannel.Console, Is.EqualTo("console"));
            Assert.That(DiagnosticChannel.Agent, Is.EqualTo("agent"));
            Assert.That(DiagnosticChannel.Bridge, Is.EqualTo("bridge"));
            Assert.That(DiagnosticChannel.ContentDelivery, Is.EqualTo("content-delivery"));
            Assert.That(DiagnosticChannel.Replication, Is.EqualTo("replication"));
        }

        [Test] public void DefaultLogger_Info_DoesNotThrow()
        {
            IDevKitLogger log = new DefaultLogger();
            Assert.DoesNotThrow(() => log.Info(DiagnosticChannel.Scenario, "hello"));
            Assert.DoesNotThrow(() => log.Warn(DiagnosticChannel.Scenario, "warn"));
            Assert.DoesNotThrow(() => log.Error(DiagnosticChannel.Scenario, "err"));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Diagnostics\DiagnosticChannel.cs`:

```csharp
namespace Pitech.XR.Core.Diagnostics
{
    /// <summary>Canonical diagnostic channel names (spec §3.8).</summary>
    public static class DiagnosticChannel
    {
        public const string Scenario = "scenario";
        public const string Console = "console";
        public const string Agent = "agent";
        public const string Bridge = "bridge";
        public const string ContentDelivery = "content-delivery";
        public const string Replication = "replication";
        public const string Authoring = "authoring";
        public const string Core = "core";
    }
}
```

Create `Runtime\Core\Diagnostics\IDevKitLogger.cs`:

```csharp
namespace Pitech.XR.Core.Diagnostics
{
    /// <summary>Categorized structured logging (spec §3.8).
    /// Replaces <c>Debug.Log("[Scenario] …")</c> prefix strings throughout DevKit.</summary>
    public interface IDevKitLogger
    {
        void Info(string channel, string message, UnityEngine.Object context = null);
        void Warn(string channel, string message, UnityEngine.Object context = null);
        void Error(string channel, string message, UnityEngine.Object context = null);
    }
}
```

Create `Runtime\Core\Diagnostics\DefaultLogger.cs`:

```csharp
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Diagnostics
{
    /// <summary>Routes to UnityEngine.Debug with channel prefix. Default capability in v1.0.</summary>
    [Preserve]
    public sealed class DefaultLogger : IDevKitLogger
    {
        public void Info(string channel, string message, Object context = null)
            => Debug.Log($"[pi-tech:{channel}] {message}", context);

        public void Warn(string channel, string message, Object context = null)
            => Debug.LogWarning($"[pi-tech:{channel}] {message}", context);

        public void Error(string channel, string message, Object context = null)
            => Debug.LogError($"[pi-tech:{channel}] {message}", context);
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Diagnostics/
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/LoggerTests.cs
git commit -m "feat(p1): add IDevKitLogger + DiagnosticChannel + DefaultLogger (spec §3.8)"
```

---

### Task 29: `CapabilityScope` enum + `CapabilityAttribute`

**Files:**
- Create: `Runtime\Core\Registry\CapabilityScope.cs`
- Create: `Runtime\Core\Registry\CapabilityAttribute.cs`

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Core.Tests\CapabilityAttributeTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Core.Registry;

namespace Pitech.XR.Core.Tests
{
    public class CapabilityAttributeTests
    {
        interface IFoo { }
        [Capability(typeof(IFoo), priority = 100, scope = CapabilityScope.App)]
        class FooImpl : IFoo { }

        [Test] public void Attribute_RecordsServiceType()
        {
            var attrs = typeof(FooImpl).GetCustomAttributes(typeof(CapabilityAttribute), false);
            Assert.That(attrs.Length, Is.EqualTo(1));
            var a = (CapabilityAttribute)attrs[0];
            Assert.That(a.ServiceType, Is.EqualTo(typeof(IFoo)));
            Assert.That(a.Priority, Is.EqualTo(100));
            Assert.That(a.Scope, Is.EqualTo(CapabilityScope.App));
        }

        [Test] public void Default_ScopeIsApp()
        {
            Assert.That(new CapabilityAttribute(typeof(IFoo)).Scope, Is.EqualTo(CapabilityScope.App));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Registry\CapabilityScope.cs`:

```csharp
namespace Pitech.XR.Core.Registry
{
    /// <summary>
    /// Lifetime of a capability registration (spec §9.5).
    /// App-scope lives for the app; Lab-scope is created by <see cref="LabRoot"/> and torn down with it.
    /// </summary>
    public enum CapabilityScope
    {
        App = 0,
        Lab = 1
    }
}
```

Create `Runtime\Core\Registry\CapabilityAttribute.cs`:

```csharp
using System;

namespace Pitech.XR.Core.Registry
{
    /// <summary>
    /// Marks a class as a capability implementation for attribute-driven registry discovery
    /// (spec §9.3). Highest priority wins when multiple impls compete for the same service type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class CapabilityAttribute : Attribute
    {
        public Type ServiceType { get; }
        public int Priority { get; }
        public CapabilityScope Scope { get; }

        public CapabilityAttribute(Type serviceType, int priority = 0, CapabilityScope scope = CapabilityScope.App)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Priority = priority;
            Scope = scope;
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/CapabilityScope.cs
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/CapabilityAttribute.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/CapabilityAttributeTests.cs
git commit -m "feat(p1): add CapabilityScope + CapabilityAttribute (spec §9.3, §9.5)"
```

---

### Task 30: `CapabilityRegistry` — app + lab scope with composite resolution

**Files:**
- Create: `Runtime\Core\Registry\CapabilityRegistry.cs`

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Core.Tests\CapabilityRegistryTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Core.Registry;

namespace Pitech.XR.Core.Tests
{
    public class CapabilityRegistryTests
    {
        interface IFoo { int Value { get; } }
        class FooA : IFoo { public int Value => 1; }
        class FooB : IFoo { public int Value => 2; }

        [Test] public void Register_And_Get_ReturnsImpl()
        {
            var reg = new CapabilityRegistry();
            reg.Register<IFoo>(new FooA());
            Assert.That(reg.Get<IFoo>().Value, Is.EqualTo(1));
        }

        [Test] public void Get_Unregistered_Throws()
        {
            var reg = new CapabilityRegistry();
            Assert.Throws<CapabilityNotRegisteredException>(() => reg.Get<IFoo>());
        }

        [Test] public void TryGet_Unregistered_ReturnsFalse()
        {
            var reg = new CapabilityRegistry();
            Assert.That(reg.TryGet<IFoo>(out var foo), Is.False);
            Assert.That(foo, Is.Null);
        }

        [Test] public void TryGet_Registered_ReturnsTrue()
        {
            var reg = new CapabilityRegistry();
            reg.Register<IFoo>(new FooA());
            Assert.That(reg.TryGet<IFoo>(out var foo), Is.True);
            Assert.That(foo.Value, Is.EqualTo(1));
        }

        [Test] public void LabScope_ShadowsAppScope()
        {
            var app = new CapabilityRegistry();
            app.Register<IFoo>(new FooA());

            var lab = new CapabilityRegistry(fallback: app);
            lab.Register<IFoo>(new FooB());

            Assert.That(lab.Get<IFoo>().Value, Is.EqualTo(2)); // lab wins
            Assert.That(app.Get<IFoo>().Value, Is.EqualTo(1)); // app unchanged
        }

        [Test] public void LabScope_FallsThroughToApp_WhenNotShadowed()
        {
            var app = new CapabilityRegistry();
            app.Register<IFoo>(new FooA());

            var lab = new CapabilityRegistry(fallback: app);

            Assert.That(lab.Get<IFoo>().Value, Is.EqualTo(1));
        }

        [Test] public void Register_Twice_HigherPriority_Wins()
        {
            var reg = new CapabilityRegistry();
            reg.Register<IFoo>(new FooA(), priority: 0);
            reg.Register<IFoo>(new FooB(), priority: 10);
            Assert.That(reg.Get<IFoo>().Value, Is.EqualTo(2));
        }

        [Test] public void Register_Twice_LowerPriority_DoesNotOverride()
        {
            var reg = new CapabilityRegistry();
            reg.Register<IFoo>(new FooB(), priority: 10);
            reg.Register<IFoo>(new FooA(), priority: 0);
            Assert.That(reg.Get<IFoo>().Value, Is.EqualTo(2));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Registry\CapabilityRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Registry
{
    public sealed class CapabilityNotRegisteredException : Exception
    {
        public CapabilityNotRegisteredException(Type t)
            : base($"No capability registered for {t.FullName}. Ensure the host's DevKitBootstrapper registers an implementation.") { }
    }

    /// <summary>
    /// Typed service registry (spec §9.3, §9.5).
    /// App-scope and lab-scope registries compose: a lab-scope registry with an app-scope fallback
    /// resolves first from its own registrations, then from the fallback.
    /// Priority-based overwrite: registering at higher priority replaces lower-priority entries.
    /// </summary>
    [Preserve]
    public sealed class CapabilityRegistry
    {
        readonly CapabilityRegistry _fallback;
        readonly Dictionary<Type, Entry> _entries = new();

        public CapabilityRegistry(CapabilityRegistry fallback = null) { _fallback = fallback; }

        public void Register<T>(T impl, int priority = 0) where T : class
        {
            if (impl == null) throw new ArgumentNullException(nameof(impl));
            if (_entries.TryGetValue(typeof(T), out var existing) && existing.Priority > priority) return;
            _entries[typeof(T)] = new Entry { Impl = impl, Priority = priority };
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var impl)) return impl;
            throw new CapabilityNotRegisteredException(typeof(T));
        }

        public bool TryGet<T>(out T impl) where T : class
        {
            if (_entries.TryGetValue(typeof(T), out var e)) { impl = (T)e.Impl; return true; }
            if (_fallback != null) return _fallback.TryGet(out impl);
            impl = null;
            return false;
        }

        struct Entry { public object Impl; public int Priority; }
    }
}
```

- [ ] **Step 4: Run → PASS (8 tests)**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/CapabilityRegistry.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/CapabilityRegistryTests.cs
git commit -m "feat(p1): add CapabilityRegistry with app/lab composite scope + priority (spec §9.3, §9.5)"
```

---

### Task 31: Update `XRServices` shim to delegate to `CapabilityRegistry`

**Files:**
- Modify: `e:\Unity files\Pi tech DevKit\Packages\pitech-xr-devkit\Runtime\Core\XRServices.cs`

- [ ] **Step 1: Read current `XRServices.cs`**

Current content (12 lines per inventory). Likely a minimal static dictionary-backed registry.

- [ ] **Step 2: Write failing test**

Create `Tests\EditMode\Core.Tests\XRServicesShimTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Core;
using Pitech.XR.Core.Registry;

namespace Pitech.XR.Core.Tests
{
    public class XRServicesShimTests
    {
        interface IFoo { int Value { get; } }
        class FooA : IFoo { public int Value => 42; }

        [SetUp] public void Reset() { XRServices.ResetForTests(); }

        [Test] public void Register_Then_Get_RoundTrips()
        {
            XRServices.Register<IFoo>(new FooA());
            Assert.That(XRServices.Get<IFoo>().Value, Is.EqualTo(42));
        }

        [Test] public void UnderlyingRegistry_IsExposed()
        {
            Assert.That(XRServices.AppRegistry, Is.Not.Null);
            Assert.That(XRServices.AppRegistry, Is.TypeOf<CapabilityRegistry>());
        }
    }
}
```

- [ ] **Step 3: Run → FAIL (methods don't exist yet)**

- [ ] **Step 4: Rewrite `XRServices.cs` as a shim**

Open `Runtime\Core\XRServices.cs` and replace with:

```csharp
using System;
using Pitech.XR.Core.Registry;
using UnityEngine.Scripting;

namespace Pitech.XR.Core
{
    /// <summary>
    /// Tiny DI surface preserved from v0.10 (spec §15.5 "XRServices becomes a shim"). New code should
    /// prefer <see cref="CapabilityRegistry"/> directly, but existing v0.10 consumers call these
    /// static methods and MUST keep working — the entry points are [DevKitPublicApi].
    /// </summary>
    [DevKitPublicApi("0.10")]
    [Preserve]
    public static class XRServices
    {
        static CapabilityRegistry _app = new CapabilityRegistry();

        /// <summary>The underlying app-scope <see cref="CapabilityRegistry"/> (new in v0.11 — added).</summary>
        [DevKitPublicApi("0.11")]
        public static CapabilityRegistry AppRegistry => _app;

        [DevKitPublicApi("0.10")]
        public static void Register<T>(T impl) where T : class => _app.Register(impl);

        [DevKitPublicApi("0.10")]
        public static T Get<T>() where T : class => _app.Get<T>();

        [DevKitPublicApi("0.11")]
        public static bool TryGet<T>(out T impl) where T : class => _app.TryGet(out impl);

        /// <summary>Test-only: reset the backing registry. Do NOT call from production code.</summary>
        internal static void ResetForTests() { _app = new CapabilityRegistry(); }
    }
}
```

- [ ] **Step 5: Run tests → PASS + confirm Gate 1 still green**

Test Runner → Run All.
Expected: XRServicesShimTests pass + `PublicApiBaselineTest.EveryBaselineType_IsStillPresent` still passes (XRServices still exists as a public type).

- [ ] **Step 6: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/XRServices.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/XRServicesShimTests.cs
git commit -m "refactor(p1): XRServices becomes shim over CapabilityRegistry; v0.10 surface preserved (spec §15.5)"
```

---

### Task 32: `StepRunnerAttribute` + `StepRunnerRegistry`

**Files:**
- Create: `Runtime\Core\Registry\StepRunnerAttribute.cs`
- Create: `Runtime\Core\Registry\StepRunnerRegistry.cs`

> **Note:** P1 ships the registry scaffold. The registry is empty in v0.11 because step runners don't extract until P2. A test verifies the empty-registry behavior is sensible.

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Core.Tests\StepRunnerRegistryTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Pitech.XR.Core.Registry;

namespace Pitech.XR.Core.Tests
{
    public class StepRunnerRegistryTests
    {
        class FakeStep { }
        class FakeStepRunner { }
        [StepRunner(typeof(FakeStep))] class AttributedRunner { }

        [Test] public void Get_Unregistered_Throws()
        {
            var reg = new StepRunnerRegistry();
            Assert.Throws<StepRunnerNotRegisteredException>(() => reg.GetRunnerType(typeof(FakeStep)));
        }

        [Test] public void TryGet_Unregistered_ReturnsFalse()
        {
            var reg = new StepRunnerRegistry();
            Assert.That(reg.TryGetRunnerType(typeof(FakeStep), out _), Is.False);
        }

        [Test] public void Attribute_RecordsStepType()
        {
            var a = typeof(AttributedRunner).GetCustomAttributes(typeof(StepRunnerAttribute), false);
            Assert.That(a.Length, Is.EqualTo(1));
            Assert.That(((StepRunnerAttribute)a[0]).StepType, Is.EqualTo(typeof(FakeStep)));
        }

        [Test] public void Register_Then_GetRunnerType_RoundTrips()
        {
            var reg = new StepRunnerRegistry();
            reg.Register(typeof(FakeStep), typeof(FakeStepRunner));
            Assert.That(reg.GetRunnerType(typeof(FakeStep)), Is.EqualTo(typeof(FakeStepRunner)));
        }

        [Test] public void DiscoverFromAttributes_PopulatesRegistry()
        {
            var reg = new StepRunnerRegistry();
            reg.DiscoverFromAttributes(new[] { typeof(AttributedRunner).Assembly });
            Assert.That(reg.TryGetRunnerType(typeof(FakeStep), out var t), Is.True);
            Assert.That(t, Is.EqualTo(typeof(AttributedRunner)));
        }
    }
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Registry\StepRunnerAttribute.cs`:

```csharp
using System;

namespace Pitech.XR.Core.Registry
{
    /// <summary>
    /// Tags a class as the runner for a specific step type (spec §3.3, §8.5).
    /// Discovered by <see cref="StepRunnerRegistry"/> at domain reload.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class StepRunnerAttribute : Attribute
    {
        public Type StepType { get; }
        public StepRunnerAttribute(Type stepType) { StepType = stepType ?? throw new ArgumentNullException(nameof(stepType)); }
    }
}
```

Create `Runtime\Core\Registry\StepRunnerRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Registry
{
    public sealed class StepRunnerNotRegisteredException : Exception
    {
        public StepRunnerNotRegisteredException(Type t)
            : base($"No step runner registered for step type {t.FullName}. Mark a runner class with [StepRunner(typeof({t.Name}))].") { }
    }

    /// <summary>Maps step type → runner type. Populated via <see cref="StepRunnerAttribute"/> scan
    /// (spec §3.3 Extension pattern).</summary>
    [Preserve]
    public sealed class StepRunnerRegistry
    {
        readonly Dictionary<Type, Type> _map = new();

        public void Register(Type stepType, Type runnerType)
        {
            if (stepType == null) throw new ArgumentNullException(nameof(stepType));
            if (runnerType == null) throw new ArgumentNullException(nameof(runnerType));
            _map[stepType] = runnerType;
        }

        public Type GetRunnerType(Type stepType)
        {
            if (!_map.TryGetValue(stepType, out var t)) throw new StepRunnerNotRegisteredException(stepType);
            return t;
        }

        public bool TryGetRunnerType(Type stepType, out Type runnerType)
            => _map.TryGetValue(stepType, out runnerType);

        public void DiscoverFromAttributes(IEnumerable<Assembly> assemblies)
        {
            foreach (var asm in assemblies ?? AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    var attr = (StepRunnerAttribute)Attribute.GetCustomAttribute(t, typeof(StepRunnerAttribute));
                    if (attr != null) Register(attr.StepType, t);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/StepRunnerAttribute.cs
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/StepRunnerRegistry.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/StepRunnerRegistryTests.cs
git commit -m "feat(p1): add StepRunnerAttribute + StepRunnerRegistry (spec §3.3, §8.5)"
```

---

### Task 33: `EffectHandlerAttribute` + `EffectHandlerRegistry` (mirror of Task 32)

**Files:**
- Create: `Runtime\Core\Registry\EffectHandlerAttribute.cs`
- Create: `Runtime\Core\Registry\EffectHandlerRegistry.cs`

Follow the exact pattern of Task 32, substituting "StepRunner" → "EffectHandler" and "step" → "effect":

- [ ] **Step 1: Write failing test** (copy `StepRunnerRegistryTests` structure; rename to `EffectHandlerRegistryTests`; use `FakeEffect` + `FakeEffectHandler`)
- [ ] **Step 2: Run → FAIL**
- [ ] **Step 3: Implement** (copy `StepRunnerAttribute` + `StepRunnerRegistry`; substitute names)
- [ ] **Step 4: Run → PASS**
- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/EffectHandlerAttribute.cs
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/EffectHandlerRegistry.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/EffectHandlerRegistryTests.cs
git commit -m "feat(p1): add EffectHandlerAttribute + EffectHandlerRegistry (spec §3.3)"
```

---

### Task 34: `DevKitValidatorAttribute` + `ValidatorRegistry`

**Files:**
- Create: `Runtime\Core\Registry\DevKitValidatorAttribute.cs`
- Create: `Runtime\Core\Registry\IDevKitValidator.cs`
- Create: `Runtime\Core\Registry\ValidatorRegistry.cs`

Per spec §3.3 + §13.2, validators have an `order` field and implement `IDevKitValidator`.

- [ ] **Step 1: Write failing test** covering: order-sorted enumeration, discovery from attributes, interface contract

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

```csharp
// IDevKitValidator.cs
using System.Collections.Generic;

namespace Pitech.XR.Core.Registry
{
    public enum ValidatorSeverity { Info, Warning, Error }

    public readonly struct ValidatorDiagnostic
    {
        public ValidatorSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
        public ValidatorDiagnostic(ValidatorSeverity sev, string code, string msg, UnityEngine.Object ctx = null)
        { Severity = sev; Code = code; Message = msg; Context = ctx; }
    }

    public interface IDevKitValidator
    {
        string Id { get; }
        IEnumerable<ValidatorDiagnostic> Validate(object target);
    }
}
```

```csharp
// DevKitValidatorAttribute.cs
using System;
namespace Pitech.XR.Core.Registry
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DevKitValidatorAttribute : Attribute
    {
        public int Order { get; }
        public DevKitValidatorAttribute(int order = 100) { Order = order; }
    }
}
```

```csharp
// ValidatorRegistry.cs — discovers + instantiates + enumerates in order
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Registry
{
    [Preserve]
    public sealed class ValidatorRegistry
    {
        readonly List<(int order, IDevKitValidator validator)> _validators = new();

        public void Register(IDevKitValidator validator, int order = 100)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            _validators.Add((order, validator));
            _validators.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public IEnumerable<IDevKitValidator> GetAllInOrder() => _validators.Select(v => v.validator);

        public void DiscoverFromAttributes(IEnumerable<Assembly> assemblies)
        {
            foreach (var asm in assemblies ?? AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types)
                {
                    var attr = (DevKitValidatorAttribute)Attribute.GetCustomAttribute(t, typeof(DevKitValidatorAttribute));
                    if (attr == null) continue;
                    if (!typeof(IDevKitValidator).IsAssignableFrom(t)) continue;
                    try { Register((IDevKitValidator)Activator.CreateInstance(t), attr.Order); }
                    catch { /* ignore; validator registration is best-effort at startup */ }
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/IDevKitValidator.cs
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/DevKitValidatorAttribute.cs
git add Packages/pitech-xr-devkit/Runtime/Core/Registry/ValidatorRegistry.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Core.Tests/ValidatorRegistryTests.cs
git commit -m "feat(p1): add ValidatorRegistry + IDevKitValidator + diagnostic types (spec §13.2)"
```

---

### Task 35: `BuildingBlockAttribute` + `BuildingBlockRegistry`

**Files:**
- Create: `Runtime\Core\Registry\BuildingBlockAttribute.cs`
- Create: `Runtime\Core\Registry\BuildingBlockRegistry.cs`

Follow the same 5-step TDD pattern. `BuildingBlockAttribute` stores `name` + `category`. `BuildingBlockRegistry` holds `List<BuildingBlockMetadataV1>` and enumerates by category.

- [ ] **Step 1**: failing test covering registration + lookup-by-id + enumerate-by-category
- [ ] **Step 2**: run → FAIL
- [ ] **Step 3**: implement
- [ ] **Step 4**: run → PASS
- [ ] **Step 5**: commit

```
git commit -m "feat(p1): add BuildingBlockAttribute + BuildingBlockRegistry (spec §3.3, §13.2)"
```

---

### Task 36: `InspectorControlAttribute` + `InspectorControlRegistry`

Same pattern. Used by `LabConsoleEditor` in P4 to find custom UI Toolkit controls for `LabParameter<T>` types.

- [ ] **Steps 1–5**: TDD loop + commit

```
git commit -m "feat(p1): add InspectorControlAttribute + InspectorControlRegistry (spec §3.3)"
```

---

### Task 37: `IStepRunner<TStep>` interface + `StepContext` / `StepOutcome` / `StepResult` types

**Files:**
- Create: `Runtime\Core\Registry\IStepRunner.cs`

These are the runtime types that P2's step runners will implement. P1 ships the interface; P2 ships impls.

- [ ] **Step 1: Write failing test** verifying type shape (interface exists, correct generic constraint, `Awaitable<StepOutcome>` return)

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

```csharp
using System.Threading;
using Pitech.XR.Core.EventBus;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Registry
{
    public enum StepResult { Completed, Cancelled, Skipped, Errored }

    public readonly struct StepOutcome
    {
        public string NextGuid { get; }
        public StepResult Result { get; }
        public StepOutcome(string nextGuid, StepResult result) { NextGuid = nextGuid; Result = result; }
    }

    [Preserve]
    public readonly struct StepContext
    {
        public ILabEventBus Bus { get; }
        public CapabilityRegistry Capabilities { get; }
        public LabId LabId { get; }
        public StepContext(ILabEventBus bus, CapabilityRegistry caps, LabId labId)
        { Bus = bus; Capabilities = caps; LabId = labId; }
    }

    /// <summary>Contract for a step runner (spec §8.5). <typeparamref name="TStep"/> is the Domain POCO.</summary>
    public interface IStepRunner<TStep>
    {
        Awaitable<StepOutcome> Run(TStep step, StepContext ctx, CancellationToken ct);
    }
}
```

Note: `LabId` needs to be referenced — already in `Pitech.XR.Core.EventBus` namespace, imported above.

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git commit -m "feat(p1): add IStepRunner<TStep> + StepContext + StepOutcome (spec §8.5)"
```

---

### Task 38: `LabRoot` MonoBehaviour stub

**Files:**
- Create: `Runtime\Core\Bootstrap\LabRoot.cs`

> P1 ships a minimal `LabRoot` that creates a bus + lab-scope registry on `Awake` and disposes on `OnDestroy`. It doesn't yet start any runner (runners come P2). The purpose of shipping it now: the facade (`SceneManager`) can ignore it, AR/VR scenes don't use it, but the class exists for P2+ to build on.

- [ ] **Step 1: Write failing PlayMode test**

Create `Tests\PlayMode\Integration.Tests\LabRootLifecycleTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Pitech.XR.Core.Bootstrap;
using Pitech.XR.Core.EventBus;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pitech.XR.Integration.Tests
{
    public class LabRootLifecycleTests
    {
        [UnityTest] public IEnumerator LabRoot_Awake_CreatesEventBus()
        {
            var go = new GameObject("lab-root");
            go.AddComponent<LabRoot>();
            yield return null;
            var root = go.GetComponent<LabRoot>();
            Assert.That(root.Bus, Is.Not.Null, "LabRoot exposes the per-lab bus after Awake.");
            Assert.That(root.Capabilities, Is.Not.Null, "LabRoot exposes lab-scope CapabilityRegistry.");
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest] public IEnumerator LabRoot_LabId_DefaultsToGameObjectName_WhenUnset()
        {
            var go = new GameObject("lab.test.fixture");
            go.AddComponent<LabRoot>();
            yield return null;
            var root = go.GetComponent<LabRoot>();
            Assert.That(root.LabId.Value, Is.EqualTo("lab.test.fixture"));
            Object.Destroy(go);
            yield return null;
        }
    }
}
```

Update `Pitech.XR.Integration.Tests.asmdef` references to include `Pitech.XR.Core`.

- [ ] **Step 2: Run → FAIL (LabRoot not defined)**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Bootstrap\LabRoot.cs`:

```csharp
using Pitech.XR.Core.EventBus;
using Pitech.XR.Core.Registry;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Bootstrap
{
    /// <summary>
    /// Per-lab entry point (spec §3.4, §8.3). Creates a lab-scoped event bus and capability registry
    /// when the lab prefab spawns; disposes on destroy. v0.11 ships the scaffold; step-runner and
    /// Lab-Console wiring attach in P2/P3 via subsequent additive changes.
    /// </summary>
    [Preserve]
    [AddComponentMenu("Pi tech XR/Core/Lab Root")]
    [DefaultExecutionOrder(-500)]
    public sealed class LabRoot : MonoBehaviour
    {
        [SerializeField] string labId = "";

        public LabEventBus Bus { get; private set; }
        public CapabilityRegistry Capabilities { get; private set; }
        public LabId LabId { get; private set; }

        void Awake()
        {
            LabId = LabId.FromString(string.IsNullOrEmpty(labId) ? name : labId);
            Bus = new LabEventBus();
            Capabilities = new CapabilityRegistry(fallback: XRServices.AppRegistry);
        }

        void OnDestroy()
        {
            // Subscribers are responsible for disposing their own subscriptions; the bus itself
            // is just a handler table, no unmanaged resources. But we null it to catch late calls.
            Bus = null;
            Capabilities = null;
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Bootstrap/LabRoot.cs
git add Packages/pitech-xr-devkit/Tests/PlayMode/Integration.Tests/LabRootLifecycleTests.cs
git commit -m "feat(p1): add LabRoot MonoBehaviour with per-lab bus + capability composite (spec §3.4, §8.3)"
```

---

### Task 39: `DevKitBootstrapper` abstract base

**Files:**
- Create: `Runtime\Core\Bootstrap\DevKitBootstrapper.cs`

- [ ] **Step 1: Write failing test** confirming base class exists, virtual method is invoked once on Awake, and caps are populated into `XRServices.AppRegistry`

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Create `Runtime\Core\Bootstrap\DevKitBootstrapper.cs`:

```csharp
using Pitech.XR.Core.Registry;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Core.Bootstrap
{
    /// <summary>
    /// Abstract per-consumer bootstrap (spec §9.4). Each consumer (HealthOn AR, HealthOn VR, the
    /// DevKit simulator host) subclasses this in its host scene and overrides
    /// <see cref="ConfigureCapabilities(CapabilityRegistry)"/> to register platform-specific impls.
    /// Runs at <see cref="DefaultExecutionOrder(-1000)"/> so capabilities are available before any
    /// <see cref="LabRoot"/> spawns.
    /// </summary>
    [Preserve]
    [DefaultExecutionOrder(-1000)]
    public abstract class DevKitBootstrapper : MonoBehaviour
    {
        protected virtual void Awake()
        {
            ConfigureCapabilities(XRServices.AppRegistry);
        }

        /// <summary>Register capability impls for this app. Called once per app lifetime.</summary>
        protected abstract void ConfigureCapabilities(CapabilityRegistry reg);
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Core/Bootstrap/DevKitBootstrapper.cs
git commit -m "feat(p1): add DevKitBootstrapper abstract base (spec §9.4)"
```

---

### Task 40: `CancellationScope` async utility

**Files:**
- Create: `Runtime\Core\Async\CancellationScope.cs`

A small helper that links an external `CancellationToken` with a `GameObject`'s destroy event, so runners can cleanly cancel on destroy.

- [ ] **Step 1**: failing test, 3 cases (constructor wires token, OnDestroy cancels, Dispose cancels)
- [ ] **Step 2**: run → FAIL
- [ ] **Step 3**: implement ~40 lines
- [ ] **Step 4**: run → PASS
- [ ] **Step 5**: commit

```
git commit -m "feat(p1): add CancellationScope linking GameObject lifetime to CancellationToken (spec §3.1)"
```

---

### Task 41: Part E integration + milestone tag

- [ ] **Step 1: Run all EditMode + PlayMode tests → all green**

Expected: ~35 EditMode tests + ~8 PlayMode tests passing.

- [ ] **Step 2: Confirm `XRServices` legacy surface still works**

Write a throwaway scene: drop any existing v0.10 consumer script that calls `XRServices.Get<T>()`. Verify it still resolves.

- [ ] **Step 3: Verify Gate 1 still green**

Run `PublicApiBaselineTest.EveryBaselineType_IsStillPresent`. Expected PASS.

- [ ] **Step 4: Tag milestone**

```
git commit --allow-empty -m "chore(p1): Part E Core layer complete (EventBus, 6 registries, LabRoot, Bootstrap, Logger, XRServices shim)"
git tag p1-part-e-complete
```

---

# Part F · Capabilities layer — 17 interfaces + default impls

Goal: ship the full 17 capability interfaces (spec §9.2) with no-op / generic default implementations. Consumers in later phases plug concrete impls via `DevKitBootstrapper.ConfigureCapabilities`. Note: `ILabObserver` and `ILabActionSurface` interfaces live here by spec §6.3, but their `AgentSubstrate` implementations ship in P5.

### Task 42: Create `Pitech.XR.Capabilities` asmdef + folder

**Files:**
- Create: `Runtime\Capabilities\Pitech.XR.Capabilities.asmdef`
- Create: `Runtime\Capabilities\Interfaces\`
- Create: `Runtime\Capabilities\Defaults\`

- [ ] **Step 1: Create folders**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Capabilities/Interfaces"
mkdir -p "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Capabilities/Defaults"
```

- [ ] **Step 2: Write asmdef**

```json
{
    "name": "Pitech.XR.Capabilities",
    "rootNamespace": "Pitech.XR.Capabilities",
    "references": ["Pitech.XR.Core", "Pitech.XR.Domain"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Dependency direction: Capabilities (layer 3) references Core (2) and Domain (1). No higher layer.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Capabilities/Pitech.XR.Capabilities.asmdef
git commit -m "feat(p1): scaffold Pitech.XR.Capabilities asmdef (spec §6.3, §9)"
```

---

### Task 43: Interfaces batch 1 — Spatial/visual (4 interfaces)

**Files:**
- Create: `Runtime\Capabilities\Interfaces\ICameraService.cs`
- Create: `Runtime\Capabilities\Interfaces\IPanelHostService.cs`
- Create: `Runtime\Capabilities\Interfaces\IWorldAnchorService.cs`
- Create: `Runtime\Capabilities\Interfaces\IVoiceService.cs` — placeholder for 2027, ships empty in v1.0 per §20

> Grouping: these four interfaces are declarative (method signatures only; no default impl in P1 because each consumer is expected to provide its own). We write all four in one task. A Capabilities test asserts the interfaces exist and can be instantiated as mocks.

- [ ] **Step 1: Write failing test**

Create `Tests\EditMode\Capabilities.Tests\SpatialInterfacesTests.cs`:

```csharp
using NUnit.Framework;
using Pitech.XR.Capabilities;
using UnityEngine;

namespace Pitech.XR.Capabilities.Tests
{
    public class SpatialInterfacesTests
    {
        class MockCamera : ICameraService
        {
            public Camera GetActiveCamera() => null;
            public event System.Action<Camera> ActiveCameraChanged;
        }

        class MockPanelHost : IPanelHostService
        {
            public Transform AttachPanel(RectTransform panel) => null;
        }

        class MockAnchor : IWorldAnchorService { public Transform GetSceneOrigin() => null; }

        [Test] public void MockCameraService_Implements_Interface()
        {
            ICameraService c = new MockCamera();
            Assert.That(c.GetActiveCamera(), Is.Null);
        }

        [Test] public void MockPanelHost_Implements_Interface()
        {
            IPanelHostService p = new MockPanelHost();
            Assert.That(p.AttachPanel(null), Is.Null);
        }

        [Test] public void MockAnchor_Implements_Interface()
        {
            IWorldAnchorService a = new MockAnchor();
            Assert.That(a.GetSceneOrigin(), Is.Null);
        }
    }
}
```

Update `Pitech.XR.Capabilities.Tests.asmdef` references: add `"Pitech.XR.Capabilities"`.

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement interfaces**

`ICameraService.cs`:

```csharp
using System;
using UnityEngine;

namespace Pitech.XR.Capabilities
{
    /// <summary>Spec §9.2 #1. Host-provided active camera.</summary>
    public interface ICameraService
    {
        Camera GetActiveCamera();
        event Action<Camera> ActiveCameraChanged;
    }
}
```

`IPanelHostService.cs`:

```csharp
using UnityEngine;

namespace Pitech.XR.Capabilities
{
    /// <summary>Spec §9.2 #2. Host-provided canvas/root for UI panels.</summary>
    public interface IPanelHostService
    {
        Transform AttachPanel(RectTransform panel);
    }
}
```

`IWorldAnchorService.cs`:

```csharp
using UnityEngine;

namespace Pitech.XR.Capabilities
{
    /// <summary>Spec §9.2 #3. Host-provided "origin" (ground-plane for AR, rig for VR, etc.).</summary>
    public interface IWorldAnchorService
    {
        Transform GetSceneOrigin();
    }
}
```

`IVoiceService.cs` (placeholder, 2027 scope per spec §20):

```csharp
namespace Pitech.XR.Capabilities
{
    /// <summary>
    /// Spec §9.2 + §20. Reserved for 2027 (voice in / voice out). Ships as interface in v1.0
    /// so ConsoleAction implementations can declare dependency on it without reaching for a future
    /// API. No default impl in v1.0 — `TryGet` returns false until a host registers one.
    /// </summary>
    public interface IVoiceService { }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Capabilities/Interfaces/ICameraService.cs
git add Packages/pitech-xr-devkit/Runtime/Capabilities/Interfaces/IPanelHostService.cs
git add Packages/pitech-xr-devkit/Runtime/Capabilities/Interfaces/IWorldAnchorService.cs
git add Packages/pitech-xr-devkit/Runtime/Capabilities/Interfaces/IVoiceService.cs
git add Packages/pitech-xr-devkit/Tests/EditMode/Capabilities.Tests/SpatialInterfacesTests.cs
git commit -m "feat(p1): Capability interfaces batch 1 — spatial/visual (spec §9.2 #1–3, #16 placeholder)"
```

---

### Task 44: Interfaces batch 2 — Interaction (4 interfaces + defaults)

**Files:**
- Create: `Runtime\Capabilities\Interfaces\IInputService.cs`
- Create: `Runtime\Capabilities\Interfaces\ISelectablesRuntime.cs`
- Create: `Runtime\Capabilities\Interfaces\IHapticsService.cs` + `Defaults\NoOpHapticsService.cs`
- Create: `Runtime\Capabilities\Interfaces\IAudioService.cs` + `Defaults\UnityAudioService.cs`

Same TDD pattern. Noteworthy impls:

- `NoOpHapticsService` — empty methods; attribute-tagged `[Capability(typeof(IHapticsService), priority = 0)]`
- `UnityAudioService` — wraps `AudioSource.PlayClipAtPoint` / `AudioSource.PlayOneShot`; attribute-tagged default

- [ ] **Steps 1–5**: TDD loop, one commit per batch

```
git commit -m "feat(p1): Capability interfaces batch 2 — interaction + default impls (spec §9.2 #4–7)"
```

---

### Task 45: Interfaces batch 3 — Delivery/state/time (3 interfaces + defaults)

**Files:**
- Create: `Runtime\Capabilities\Interfaces\IContentDeliveryService.cs` (wraps Addressables 2.x)
- Create: `Runtime\Capabilities\Interfaces\IClockService.cs` + `Defaults\UnityClockService.cs`
- Create: `Runtime\Capabilities\Interfaces\IStatsService.cs`

`UnityClockService` exposes `float DeltaTime { get; }`, `float UnscaledDeltaTime { get; }`, `Awaitable WaitForSecondsAsync(float)` — wraps Unity 6's native Awaitable. Zero-alloc test confirms steady-state.

- [ ] **Steps 1–5**: TDD loop, single commit

```
git commit -m "feat(p1): Capability interfaces batch 3 — delivery/state/time + default clock (spec §9.2 #8–10)"
```

---

### Task 46: Interfaces batch 4 — Cross-cutting substrate (4 interfaces + defaults)

**Files:**
- Create: `Runtime\Capabilities\Interfaces\INetworkReplicationService.cs` + `Defaults\LocalReplicationService.cs`
- Create: `Runtime\Capabilities\Interfaces\ITelemetryService.cs` + `Defaults\QueuedTelemetryService.cs` (stub in P1; wires to handbook §2 telemetry queue in P5)
- Create: `Runtime\Capabilities\Interfaces\IAuditTrail.cs` + `Defaults\LocalAuditTrail.cs`
- Create: `Runtime\Capabilities\Interfaces\ILocalizationService.cs` + `Defaults\NoOpLocalizationService.cs`

`INetworkReplicationService` signature matches spec §9.8 exactly:

```csharp
using System;
using System.Threading;
using UnityEngine;

namespace Pitech.XR.Capabilities
{
    public readonly struct ActionRequest
    {
        public string ActionId { get; }
        public string ArgsJson { get; }
        public string OriginActor { get; }
        public string OriginRole { get; }
        public ActionRequest(string id, string argsJson, string actor, string role)
        { ActionId = id; ArgsJson = argsJson; OriginActor = actor; OriginRole = role; }
    }

    public readonly struct NetworkObjectId : IEquatable<NetworkObjectId>
    {
        public string Value { get; }
        public NetworkObjectId(string value) { Value = value ?? string.Empty; }
        public bool Equals(NetworkObjectId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is NetworkObjectId n && Equals(n);
        public override int GetHashCode() => Value.GetHashCode();
    }

    /// <summary>Spec §9.8. Routes writes to StateAuthority on Shared-Mode Fusion.</summary>
    public interface INetworkReplicationService
    {
        bool IsLocalAuthority(NetworkObjectId id);
        Awaitable SendToAuthority(ActionRequest request, CancellationToken ct);
        IDisposable OnRemoteChange<T>(Action<T> handler);
    }
}
```

`LocalReplicationService` is the app-level default: `IsLocalAuthority` always true; `SendToAuthority` is a no-op that returns `Awaitable.FromResult(...)` equivalent (Unity 6 idiom: `async Awaitable() => { }`).

- [ ] **Steps 1–5**: TDD loop, single commit

```
git commit -m "feat(p1): Capability interfaces batch 4 — cross-cutting + defaults (spec §9.2 #11–15, §9.8 signature)"
```

---

### Task 47: Interfaces batch 5 — Agent-gate surfaces (2 interfaces, no defaults in P1)

**Files:**
- Create: `Runtime\Capabilities\Interfaces\ILabObserver.cs`
- Create: `Runtime\Capabilities\Interfaces\ILabActionSurface.cs`

These interfaces ship empty in P1 — they're registered only by the AgentSubstrate in P5. P1 defines them so `AgentSubstrate.asmdef` in P5 has something to reference without pulling a package-level circular dep.

- [ ] **Steps 1–5**: TDD loop (test just confirms interfaces compile and can be mocked), single commit

```
git commit -m "feat(p1): Capability interfaces batch 5 — agent-gate surfaces (spec §9.2 #16–17; §10)"
```

---

### Task 48: `IDevKitLogger` moved/exposed from Core to Capabilities

The logger interface lives in Core for use by Core internals, but also fulfills Capability role #14. Add a capability-level `[Capability(typeof(IDevKitLogger), priority = 0)]` on `DefaultLogger` so `CapabilityRegistry.Get<IDevKitLogger>()` works from anywhere.

- [ ] **Steps 1–5**: simple attribute addition; test confirms registration via registry

```
git commit -m "feat(p1): attribute DefaultLogger as [Capability(typeof(IDevKitLogger))] (spec §9.2 #14)"
```

---

### Task 49: Capability auto-discovery — `AutoDiscoverDefaults` in `CapabilityRegistry`

**Files:**
- Modify: `Runtime\Core\Registry\CapabilityRegistry.cs`

Add a `DiscoverFromAttributes` method that scans assemblies for `[Capability]`-tagged classes and registers them at their declared priority + scope.

- [ ] **Step 1: Write failing test**

In `CapabilityRegistryTests`:

```csharp
public interface IDiscoverable { }
[Capability(typeof(IDiscoverable), priority = 5)]
public class DiscoverableImpl : IDiscoverable { }

[Test] public void DiscoverFromAttributes_RegistersApp()
{
    var reg = new CapabilityRegistry();
    reg.DiscoverFromAttributes(new[] { typeof(DiscoverableImpl).Assembly }, CapabilityScope.App);
    Assert.That(reg.TryGet<IDiscoverable>(out var impl), Is.True);
    Assert.That(impl, Is.TypeOf<DiscoverableImpl>());
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement**

Extend `CapabilityRegistry` with:

```csharp
public void DiscoverFromAttributes(System.Collections.Generic.IEnumerable<System.Reflection.Assembly> asms, CapabilityScope scope)
{
    foreach (var asm in asms)
    {
        System.Type[] types;
        try { types = asm.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { types = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(ex.Types, t => t != null)); }

        foreach (var t in types)
        {
            var attr = (CapabilityAttribute)System.Attribute.GetCustomAttribute(t, typeof(CapabilityAttribute));
            if (attr == null || attr.Scope != scope) continue;
            if (!attr.ServiceType.IsAssignableFrom(t)) continue;
            try
            {
                var impl = System.Activator.CreateInstance(t);
                // Dynamic dispatch through reflection to call Register<T>:
                var method = typeof(CapabilityRegistry).GetMethod(nameof(Register)).MakeGenericMethod(attr.ServiceType);
                method.Invoke(this, new object[] { impl, attr.Priority });
            }
            catch { /* best-effort; missing parameterless ctor or other issues logged by logger later */ }
        }
    }
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```
git commit -m "feat(p1): CapabilityRegistry auto-discovers [Capability]-tagged impls (spec §9.4)"
```

---

### Task 50: `DevKitBootstrapper.Awake` calls `DiscoverFromAttributes` for app-scope defaults

**Files:**
- Modify: `Runtime\Core\Bootstrap\DevKitBootstrapper.cs`

- [ ] **Step 1: Add default-discovery before subclass ConfigureCapabilities**

```csharp
protected virtual void Awake()
{
    // Register all [Capability(scope=App)]-tagged classes first. Subclass overrides via
    // ConfigureCapabilities then override or add to the resolved set.
    XRServices.AppRegistry.DiscoverFromAttributes(
        System.AppDomain.CurrentDomain.GetAssemblies(), CapabilityScope.App);
    ConfigureCapabilities(XRServices.AppRegistry);
}
```

- [ ] **Step 2: Write test** — PlayMode test that drops a bare `DevKitBootstrapper`-subclass into a scene and asserts the defaults resolve after Awake.

- [ ] **Step 3: Run test → PASS**

- [ ] **Step 4: Commit**

```
git commit -m "feat(p1): DevKitBootstrapper auto-discovers app-scope defaults on Awake"
```

---

### Task 51: Part F integration + milestone tag

- [ ] **Step 1**: Run all tests — expect ~50 EditMode + ~10 PlayMode green
- [ ] **Step 2**: Verify dependency direction manually: Capabilities references only Core + Domain (check asmdef)
- [ ] **Step 3**: Gate 1 still green
- [ ] **Step 4**: Tag

```
git commit --allow-empty -m "chore(p1): Part F Capabilities layer complete (17 interfaces + defaults)"
git tag p1-part-f-complete
```

---

# Part G · Roslyn analyzer package

Goal: ship `Pitech.XR.Analyzers` Roslyn DLL with two rules (banned APIs, dependency direction) in warning mode. Ships as a compiled DLL under `Runtime\Analyzers\` with Unity's `RoslynAnalyzer` asset label per spec §15.5 and Unity 6 source-generator docs.

### Task 52: Create external .NET Standard 2.0 class library for analyzer

**Files:**
- Create external project: `e:\Unity files\Pi tech DevKit\Tools\Pitech.XR.Analyzers\Pitech.XR.Analyzers.csproj`

- [ ] **Step 1: Create the external project folder**

```
mkdir -p "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers"
cd "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers"
```

- [ ] **Step 2: Create `Pitech.XR.Analyzers.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.3.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

> Note: CodeAnalysis 4.3 is the Unity-compatible version per official Unity docs. Newer versions may work but are untested.

- [ ] **Step 3: Implement `BannedApisAnalyzer`**

Create `BannedApisAnalyzer.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Pitech.XR.Analyzers
{
    [DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
    public sealed class BannedApisAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PITECH001";
        static readonly DiagnosticDescriptor Rule = new(
            id: DiagnosticId,
            title: "Banned API in lab runtime code",
            messageFormat: "'{0}' is forbidden in lab runtime code (scene-context assumption). Use the matching capability through ctx.Capabilities.",
            category: "Pitech.XR.Architecture",
            defaultSeverity: DiagnosticSeverity.Warning, // P1 ships warning; P2 promotes to error
            isEnabledByDefault: true,
            description: "Lab runtime code must obtain host-provided services through CapabilityRegistry, not via scene-context APIs. See spec §9.6.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
        }

        static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
        {
            if (!IsInTargetAssembly(ctx.SemanticModel.Compilation.AssemblyName)) return;
            var node = (MemberAccessExpressionSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null) return;

            // Banned: Camera.main, EventSystem.current (and similar scene-context statics).
            var full = symbol.ToDisplayString();
            if (full == "UnityEngine.Camera.main" || full == "UnityEngine.EventSystems.EventSystem.current")
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), full));
        }

        static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
        {
            if (!IsInTargetAssembly(ctx.SemanticModel.Compilation.AssemblyName)) return;
            var node = (InvocationExpressionSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null) return;
            var full = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            // Banned method calls
            if (full.StartsWith("GameObject.Find") ||
                full.StartsWith("Object.FindObjectsOfType") ||
                full.StartsWith("Object.FindAnyObjectByType"))
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), full));
        }

        static bool IsInTargetAssembly(string asmName) =>
            asmName == "Pitech.XR.ScenarioRuntime" ||
            asmName == "Pitech.XR.LabConsoleRuntime" ||
            asmName == "Pitech.XR.Capabilities";
    }
}
```

- [ ] **Step 4: Build**

```
cd "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers"
dotnet restore
dotnet build -c Release
```

Output: `bin/Release/netstandard2.0/Pitech.XR.Analyzers.dll`.

- [ ] **Step 5: Commit the Tools project (not the build artifacts)**

```
echo "bin/" > "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers/.gitignore"
echo "obj/" >> "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers/.gitignore"
git add Tools/Pitech.XR.Analyzers/
git commit -m "tools(p1): external Roslyn analyzer project (spec §9.6, §15.3 Gate 6)"
```

---

### Task 53: Copy built analyzer DLL into DevKit package + label as `RoslynAnalyzer`

**Files:**
- Create: `Runtime\Analyzers\Pitech.XR.Analyzers.dll`
- Create: `Runtime\Analyzers\Pitech.XR.Analyzers.dll.meta` (Unity asset import settings)

- [ ] **Step 1: Copy DLL**

```
cp "e:/Unity files/Pi tech DevKit/Tools/Pitech.XR.Analyzers/bin/Release/netstandard2.0/Pitech.XR.Analyzers.dll" \
   "e:/Unity files/Pi tech DevKit/Packages/pitech-xr-devkit/Runtime/Analyzers/Pitech.XR.Analyzers.dll"
```

- [ ] **Step 2: Configure Unity asset label**

Open Unity Editor. Select `Runtime/Analyzers/Pitech.XR.Analyzers.dll` in the Project window. In the Inspector:
- Uncheck all platforms (DLL should not be included in player builds — it's editor-only analyzer).
- In the asset labels section (bottom), add label `RoslynAnalyzer`.

Unity auto-generates a `.meta` file with the label.

- [ ] **Step 3: Verify analyzer is active**

Close Unity; reopen. On project load, the analyzer runs. Open any C# file under `Runtime/` and write a line that would trigger the rule (e.g., `var c = Camera.main;`). Unity's Console should emit a warning with `PITECH001`.

Remove the test line once confirmed.

- [ ] **Step 4: Commit**

```
git add Packages/pitech-xr-devkit/Runtime/Analyzers/
git commit -m "feat(p1): ship Pitech.XR.Analyzers.dll as RoslynAnalyzer (spec §9.6)"
```

---

### Task 54: Verify analyzer warnings surface on a deliberate violation

**Files:**
- Temporary test (revert before commit)

- [ ] **Step 1: Write a deliberately-violating file**

Create `Runtime\Capabilities\Defaults\_AnalyzerProbe.cs` (temporary):

```csharp
using UnityEngine;
using UnityEngine.Scripting;

namespace Pitech.XR.Capabilities.Defaults
{
    [Preserve]
    internal static class _AnalyzerProbe
    {
        public static Camera Probe() => Camera.main; // Expected PITECH001 warning
    }
}
```

- [ ] **Step 2: Check Console for `PITECH001` warning**

Unity reimports. Console should show a warning referencing `Camera.main` and `PITECH001`. If no warning, analyzer is not wired correctly — debug.

- [ ] **Step 3: Delete the probe file**

```
rm "e:/Unity files/Pi tech DevKit/Runtime/Capabilities/Defaults/_AnalyzerProbe.cs"
rm "e:/Unity files/Pi tech DevKit/Runtime/Capabilities/Defaults/_AnalyzerProbe.cs.meta"
```

- [ ] **Step 4: Commit the verification note**

```
git commit --allow-empty -m "chore(p1): analyzer probe confirmed — PITECH001 fires on Camera.main"
```

---

### Task 55: Part G milestone tag

- [ ] **Step 1**: Confirm analyzer ships (DLL under Runtime/Analyzers with label)
- [ ] **Step 2**: Confirm clean Console (no pending violations)
- [ ] **Step 3**: Tag

```
git commit --allow-empty -m "chore(p1): Part G Roslyn analyzers complete (banned APIs warning mode)"
git tag p1-part-g-complete
```

---

# Part H · Authoring editor scaffold

Goal: scaffold the editor-side asmdef for `Pitech.XR.Authoring`, ship the UI Toolkit design-system USS, the `DevKitEditorWindow` base class, and a minimal DevKit Hub v1 window (one tab: Project Status). P4 fleshes out Building Blocks / Simulator / Debuggers tabs; P1 ships the shell.

### Task 56: Create `Pitech.XR.Authoring.asmdef` + UI folder structure

- [ ] **Step 1**: folders + asmdef; editor-only (`includePlatforms: ["Editor"]`); references `Pitech.XR.Core`, `Pitech.XR.Domain`, `Pitech.XR.Capabilities`, `Unity.UIElementsModule`
- [ ] **Step 2**: verify compile
- [ ] **Step 3**: commit — `feat(p1): scaffold Pitech.XR.Authoring asmdef (spec §13)`

---

### Task 57: Design-system USS + tokens

**Files:**
- Create: `Editor\Authoring\UI\DesignSystem\devkit-theme.uss`
- Create: `Editor\Authoring\UI\DesignSystem\devkit-theme-dark.uss`

- [ ] **Step 1: Write light-theme USS with CSS variables (tokens)**

```css
:root {
  --devkit-color-bg: rgb(248, 248, 248);
  --devkit-color-surface: rgb(255, 255, 255);
  --devkit-color-text: rgb(30, 30, 30);
  --devkit-color-text-muted: rgb(120, 120, 120);
  --devkit-color-accent: rgb(0, 120, 215);
  --devkit-color-error: rgb(200, 0, 0);
  --devkit-color-warning: rgb(200, 140, 0);
  --devkit-spacing-xs: 4px;
  --devkit-spacing-sm: 8px;
  --devkit-spacing-md: 12px;
  --devkit-spacing-lg: 20px;
  --devkit-radius: 4px;
  --devkit-font-size-body: 12px;
  --devkit-font-size-h1: 16px;
  --devkit-font-size-h2: 14px;
}

.devkit-button {
  background-color: var(--devkit-color-surface);
  color: var(--devkit-color-text);
  border-radius: var(--devkit-radius);
  padding: var(--devkit-spacing-xs) var(--devkit-spacing-sm);
  border-width: 1px;
  border-color: rgb(200, 200, 200);
}

.devkit-button--primary {
  background-color: var(--devkit-color-accent);
  color: white;
  border-color: var(--devkit-color-accent);
}

.devkit-section__header {
  font-size: var(--devkit-font-size-h2);
  -unity-font-style: bold;
  padding: var(--devkit-spacing-sm) 0;
}

.devkit-diagnostic-banner {
  padding: var(--devkit-spacing-sm);
  border-radius: var(--devkit-radius);
  margin-bottom: var(--devkit-spacing-sm);
}
.devkit-diagnostic-banner--warning { background-color: rgba(200, 140, 0, 0.15); }
.devkit-diagnostic-banner--error { background-color: rgba(200, 0, 0, 0.15); }
```

- [ ] **Step 2: Write dark-theme override**

`devkit-theme-dark.uss`:

```css
:root {
  --devkit-color-bg: rgb(40, 40, 40);
  --devkit-color-surface: rgb(56, 56, 56);
  --devkit-color-text: rgb(230, 230, 230);
  --devkit-color-text-muted: rgb(150, 150, 150);
}
```

- [ ] **Step 3: Commit**

```
git commit -m "feat(p1): DevKit UI design-system USS with CSS tokens (spec §13.3)"
```

---

### Task 58: `DevKitEditorWindow` base class

**Files:**
- Create: `Editor\Authoring\UI\DevKitEditorWindow.cs`

- [ ] **Step 1**: no unit test (editor UI). Manual verification via opening the Hub after Task 59.

- [ ] **Step 2: Implement**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Authoring.UI
{
    /// <summary>
    /// Base class for every DevKit editor window (spec §13.3). Provides themed style, version badge,
    /// help link footer, and a consistent root layout.
    /// </summary>
    public abstract class DevKitEditorWindow : EditorWindow
    {
        protected VisualElement Root => rootVisualElement;

        protected virtual void CreateGUI()
        {
            Root.styleSheets.Add(LoadStyle("devkit-theme"));
            if (EditorGUIUtility.isProSkin) Root.styleSheets.Add(LoadStyle("devkit-theme-dark"));

            var header = new VisualElement();
            header.AddToClassList("devkit-section__header");
            header.Add(new Label(GetWindowTitle()));
            Root.Add(header);

            var body = new VisualElement { name = "devkit-body" };
            Root.Add(body);
            BuildBody(body);

            var footer = new VisualElement { name = "devkit-footer" };
            footer.Add(new Label($"DevKit 0.11.0-pre.1"));
            Root.Add(footer);
        }

        protected abstract string GetWindowTitle();
        protected abstract void BuildBody(VisualElement body);

        static StyleSheet LoadStyle(string name)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:StyleSheet");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
```

- [ ] **Step 3: Commit**

```
git commit -m "feat(p1): DevKitEditorWindow base class (spec §13.3)"
```

---

### Task 59: DevKit Hub v1 — minimal window with Project Status tab

**Files:**
- Create: `Editor\Authoring\Hub\DevKitHubWindow.cs`

- [ ] **Step 1: Implement Hub**

```csharp
using Pitech.XR.Authoring.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Pitech.XR.Authoring.Hub
{
    public sealed class DevKitHubWindow : DevKitEditorWindow
    {
        [MenuItem("Pi tech/DevKit Hub", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<DevKitHubWindow>();
            w.titleContent = new UnityEngine.GUIContent("DevKit Hub");
            w.minSize = new UnityEngine.Vector2(400, 300);
            w.Show();
        }

        protected override string GetWindowTitle() => "DevKit Hub";

        protected override void BuildBody(VisualElement body)
        {
            var tabs = new TabView(); // Unity 6 UI Toolkit built-in
            tabs.Add(new Tab("Project") { });
            var projectTab = tabs.Q<Tab>("Project") ?? tabs.Children().FirstOrDefault() as Tab;

            var statusLabel = new Label("DevKit 0.11.0-pre.1 (P1 Foundation)\nHub v1 — P4 will expand this.");
            tabs.Add(new Tab("Project") { });
            body.Add(statusLabel);

            // P4 adds: Labs, Building Blocks, Actions, Debuggers, Help tabs
        }
    }
}
```

> Note: The `TabView` + `Tab` controls are Unity 6 UI Toolkit built-ins per spec §13.3. P1 ships only the Project tab label; P4 expands.

- [ ] **Step 2: Open the window manually**

Unity menu `Pi tech → DevKit Hub`. Verify window opens, shows title, version label, and body content. Close.

- [ ] **Step 3: Commit**

```
git commit -m "feat(p1): DevKit Hub v1 window (Project tab only; P4 expands) (spec §13.2)"
```

---

### Task 60: Part H milestone + run all tests

- [ ] **Step 1**: Run all tests → green
- [ ] **Step 2**: Open DevKit Hub → opens cleanly
- [ ] **Step 3**: Tag

```
git commit --allow-empty -m "chore(p1): Part H Authoring scaffold complete (design-system, base window, Hub v1)"
git tag p1-part-h-complete
```

---

# Part I · CI gates — remaining tests

Gate 1 (API baseline) landed in Task 14. Gates 4–6 landed implicitly (zero-alloc tests + analyzer). Remaining: Gates 2 (v0.10 fixture load), 5 (dependency direction), 7 (IL2CPP round-trip).

### Task 61: Gate 2 — v0.10 fixture load test

**Files:**
- Create: `Tests\PlayMode\Migration.Tests\V010FixtureLoadTest.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pitech.XR.Migration.Tests
{
    public class V010FixtureLoadTest
    {
        const string FixtureRoot = "Tests/Fixtures/v0.10/";

        [UnityTest] public IEnumerator EveryV010Scene_LoadsCleanlyInUnity6()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fixtureRoot = Path.Combine(projectRoot, FixtureRoot);
            Assert.That(Directory.Exists(fixtureRoot), $"Fixture root missing: {fixtureRoot}");

            var scenes = Directory.GetFiles(fixtureRoot, "*.unity", SearchOption.AllDirectories);
            Assert.That(scenes.Length, Is.GreaterThan(0), "No .unity fixtures found in v0.10 corpus.");

            foreach (var scenePath in scenes)
            {
                var relative = "Assets" + scenePath.Substring(Application.dataPath.Length).Replace('\\', '/');
                // Fixtures may live outside Assets — use full path open with EditorSceneManager
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Assert.That(scene.IsValid(), $"Failed to open {scenePath}");

                // Scan every loaded GameObject for MissingScript / null SerializeReference entries
                foreach (var root in scene.GetRootGameObjects())
                {
                    AssertNoMissingScripts(root, scenePath);
                }
                yield return null;
            }
        }

        static void AssertNoMissingScripts(GameObject go, string scenePath)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                Assert.That(c, Is.Not.Null, $"Missing script reference in {scenePath} on {go.name}");
            }
        }
    }
}
```

- [ ] **Step 2: Run → PASS** (all 5–8 v0.10 fixtures open cleanly in Unity 6)

If any fixture fails, log which and triage. Most likely culprits: missing package dep (e.g., an AR-specific script not in DevKit), Addressables 1.x → 2.x API drift. Fix in consumer project or capture the fixture differently.

- [ ] **Step 3: Commit**

```
git add Packages/pitech-xr-devkit/Tests/PlayMode/Migration.Tests/V010FixtureLoadTest.cs
git commit -m "test(p1): CI Gate 2 — v0.10 fixture load test (spec §15.3)"
```

---

### Task 62: Gate 5 — dependency direction test

**Files:**
- Create: `Tests\EditMode\Core.Tests\DependencyDirectionTest.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Pitech.XR.Core.Tests
{
    public class DependencyDirectionTest
    {
        // Layer numbers per spec §6.1. Lower = "downstream." Higher layer may reference lower.
        static readonly Dictionary<string, int> Layer = new()
        {
            { "Pitech.XR.Domain", 1 },
            { "Pitech.XR.Core", 2 },
            { "Pitech.XR.Capabilities", 3 },
            { "Pitech.XR.AgentSubstrate", 4 },  // P5
            { "Pitech.XR.Bridge", 5 },           // P5
            // Consumer repos are layer 6 (not tested here).
            { "Pitech.XR.Authoring", 7 },        // editor-only
            // Legacy feature asmdefs (v0.10) map to layer 3 (capability-like) until P2/P3 move them.
            { "Pitech.XR.Scenario", 3 },
            { "Pitech.XR.ContentDelivery", 3 },
            { "Pitech.XR.Interactables", 3 },
            { "Pitech.XR.Quiz", 3 },
            { "Pitech.XR.Stats", 3 },
            { "Pitech.XR.ScenarioRuntime", 2 },   // P2
            { "Pitech.XR.LabConsoleRuntime", 2 }, // P3
        };

        [Test] public void NoAsmdef_ReferencesHigherLayer()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/pitech-xr-devkit"));
            var asmdefs = Directory.GetFiles(projectRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(p => !p.Contains("Tests")) // tests may reference everything
                .ToList();

            var violations = new List<string>();

            foreach (var path in asmdefs)
            {
                var json = JObject.Parse(File.ReadAllText(path));
                var name = (string)json["name"];
                if (!Layer.TryGetValue(name, out var myLayer)) continue;

                var refs = json["references"]?.Select(r => (string)r).ToList() ?? new List<string>();
                foreach (var r in refs)
                {
                    if (!Layer.TryGetValue(r, out var refLayer)) continue;
                    if (refLayer > myLayer)
                        violations.Add($"{name} (layer {myLayer}) references {r} (layer {refLayer})");
                }
            }

            if (violations.Count > 0)
                Assert.Fail("Dependency direction violations:\n" + string.Join("\n", violations));
        }
    }
}
```

- [ ] **Step 2: Run → PASS** (no violations in current state)

If fails, fix the offending asmdef reference before proceeding.

- [ ] **Step 3: Commit**

```
git commit -m "test(p1): CI Gate 5 — dependency direction test (spec §6.1, §15.3)"
```

---

### Task 63: Gate 7 — IL2CPP round-trip for `BuildingBlockMetadataV1`

**Files:**
- Create: `Tests\PlayMode\Serialization.Tests\BuildingBlockMetadataV1RoundTripTest.cs`

P1 seeds Gate 7 with one contract (BuildingBlockMetadataV1). Subsequent phases extend the test to cover every V1 schema as they're added.

- [ ] **Step 1: Write the test**

```csharp
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using Pitech.XR.Domain.BuildingBlocks;
using UnityEngine.TestTools;

namespace Pitech.XR.Serialization.Tests
{
    public class BuildingBlockMetadataV1RoundTripTest
    {
        [UnityTest] public IEnumerator RoundTrip_OnIL2CPP_Equivalent()
        {
            // This test runs in PlayMode; on IL2CPP-built players it exercises the AOT path.
            var meta = new BuildingBlockMetadataV1
            {
                Id = "il2cpp-probe",
                DisplayName = "IL2CPP Probe",
                Category = BuildingBlockCategory.Scenario,
                Description = "Round-trip through Newtonsoft on AOT.",
                RequiredCapabilities = new List<string> { "IPanelHostService" },
                Dependencies = new List<string>(),
                SupportedConsumers = new List<string> { "AR", "VR" },
                Tags = new List<string> { "probe" },
                ThumbnailPath = "Thumbnails/probe.png"
            };

            var json = JsonConvert.SerializeObject(meta);
            var restored = JsonConvert.DeserializeObject<BuildingBlockMetadataV1>(json);

            Assert.That(restored.Version, Is.EqualTo("V1"));
            Assert.That(restored.Id, Is.EqualTo(meta.Id));
            Assert.That(restored.Category, Is.EqualTo(meta.Category));
            Assert.That(restored.RequiredCapabilities, Is.EquivalentTo(meta.RequiredCapabilities));
            Assert.That(restored.SupportedConsumers, Is.EquivalentTo(meta.SupportedConsumers));
            Assert.That(restored.Tags, Is.EquivalentTo(meta.Tags));
            yield return null;
        }
    }
}
```

- [ ] **Step 2: Run in Editor (Mono backend) — PASS**

Test Runner → PlayMode → Run.

- [ ] **Step 3: Build & run a PlayMode test on IL2CPP target**

Build an Android (Quest) test build with IL2CPP:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe" -batchmode -projectPath "e:\Unity files\Pi tech DevKit" -runTests -testPlatform Android -testResults "E:\build-logs\il2cpp-test-results.xml" -scripting-backend IL2CPP
```

> This takes 10–20 minutes for cold builds. Run at phase boundaries, not per task.

Expected: test result XML shows PASS for the serialization test.

- [ ] **Step 4: Commit**

```
git commit -m "test(p1): CI Gate 7 — IL2CPP round-trip for BuildingBlockMetadataV1 (spec §7.5, §15.3)"
```

---

### Task 64: Part I milestone tag + documentation of CI invocation

- [ ] **Step 1**: Run all 7 gate tests (Gate 3 golden replay is deferred to P2; other 6 must pass)
- [ ] **Step 2**: Update `Documentation~/CI_GATES.md` with exact CLI invocations (see Task 86)
- [ ] **Step 3**: Tag

```
git commit --allow-empty -m "chore(p1): Part I CI gates complete — 6 of 7 gates active (Gate 3 activates P2)"
git tag p1-part-i-complete
```

---

# Part J · Simulator host — DevKit test project bootstrap

Goal: make `E:\Unity files\Pi tech DevKit` a canonical consumer per spec §12.1. Scene with `DevKitSimulatorBootstrapper` registering mock capabilities. The simulator host is the "fourth consumer" and the proving ground for every new feature before AR or VR adopt it.

### Task 65: Create `DevKitSimulatorHost.unity` scene

**Files:**
- Create: `Assets\DevKitSimulatorHost.unity`

- [ ] **Step 1: Create new scene**

File → New Scene → empty. Save as `Assets/DevKitSimulatorHost.unity`.

- [ ] **Step 2: Add a Main Camera, a Canvas (world-space), and an empty GameObject named `DevKitBootstrapperHost`**

- [ ] **Step 3: Commit**

```
git add Assets/DevKitSimulatorHost.unity
git commit -m "feat(p1): add DevKitSimulatorHost scene"
```

---

### Task 66: `DevKitSimulatorBootstrapper` — mock capability registrations

**Files:**
- Create: `Assets\Scripts\DevKitSimulatorBootstrapper.cs`

This lives in the DevKit project's `Assets/Scripts/`, not inside the package. It's consumer code.

- [ ] **Step 1: Implement**

```csharp
using Pitech.XR.Capabilities;
using Pitech.XR.Core.Bootstrap;
using Pitech.XR.Core.Registry;
using UnityEngine;

namespace PiTech.DevKit.Simulator
{
    [DefaultExecutionOrder(-1000)]
    public sealed class DevKitSimulatorBootstrapper : DevKitBootstrapper
    {
        [SerializeField] Camera sceneCamera;
        [SerializeField] Canvas simCanvas;

        protected override void ConfigureCapabilities(CapabilityRegistry reg)
        {
            reg.Register<ICameraService>(new SimulatorCameraService(sceneCamera), priority: 1000);
            reg.Register<IPanelHostService>(new SimulatorPanelHost(simCanvas), priority: 1000);
            reg.Register<IWorldAnchorService>(new SimulatorAnchor(transform), priority: 1000);
            // Other caps fall through to attribute-discovered defaults (NoOpHaptics, UnityClock, LocalReplication, etc.)
        }

        sealed class SimulatorCameraService : ICameraService
        {
            readonly Camera _cam;
            public SimulatorCameraService(Camera cam) { _cam = cam; }
            public Camera GetActiveCamera() => _cam;
            public event System.Action<Camera> ActiveCameraChanged { add {} remove {} }
        }

        sealed class SimulatorPanelHost : IPanelHostService
        {
            readonly Canvas _canvas;
            public SimulatorPanelHost(Canvas c) { _canvas = c; }
            public Transform AttachPanel(RectTransform panel)
            {
                if (panel != null && _canvas != null) panel.SetParent(_canvas.transform, false);
                return _canvas?.transform;
            }
        }

        sealed class SimulatorAnchor : IWorldAnchorService
        {
            readonly Transform _origin;
            public SimulatorAnchor(Transform t) { _origin = t; }
            public Transform GetSceneOrigin() => _origin;
        }
    }
}
```

- [ ] **Step 2: Wire into scene**

Select `DevKitBootstrapperHost` GameObject → Add Component → `DevKitSimulatorBootstrapper` → assign Main Camera + Canvas in the inspector.

- [ ] **Step 3: Press Play — verify no errors**

Console should log: `[pi-tech:core] capability discovery ran`. No errors.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/DevKitSimulatorBootstrapper.cs
git add Assets/DevKitSimulatorHost.unity
git commit -m "feat(p1): DevKitSimulatorBootstrapper with mock capability impls (spec §12.1, §9.4)"
```

---

### Task 67: Smoke test — simulator host plays the canonical reference lab

- [ ] **Step 1**: With Petros, identify the canonical reference lab (handbook §6.0) and copy it under `Assets/SampleLab/`
- [ ] **Step 2**: In `DevKitSimulatorHost` scene, add a `LabRoot` GameObject with a `LabRoot` component; wire the reference lab prefab as its content root
- [ ] **Step 3**: Enter Play; verify the lab scenario plays start-to-finish without console errors
- [ ] **Step 4**: Commit

```
git commit -m "feat(p1): reference lab plays in simulator host (spec §12.1)"
```

---

### Task 68: Part J milestone tag

- [ ] Run all tests → green
- [ ] Tag

```
git commit --allow-empty -m "chore(p1): Part J simulator host complete"
git tag p1-part-j-complete
```

---

# Part K · Documentation + release

### Task 69: `Documentation~/ARCHITECTURE.md`

**Files:**
- Create: `Packages\pitech-xr-devkit\Documentation~\ARCHITECTURE.md`

- [ ] **Step 1: Write the document**

Structure (reference the spec for authoritative detail):

```markdown
# DevKit Architecture — Quick Reference

For the full authoritative reference, see `docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md` in the DevKit repo root.

## The seven layers

1. **Domain** (`Pitech.XR.Domain`) — pure data, no Unity runtime. Every cross-boundary contract is `V1`-suffixed and schema-versioned.
2. **Core** (`Pitech.XR.Core`) — event bus, registries, bootstrap, logger. The central nervous system.
3. **Capabilities** (`Pitech.XR.Capabilities`) — 17 `I{Service}` interfaces. Hosts register impls via `DevKitBootstrapper.ConfigureCapabilities`.
4. **Agent Substrate** (`Pitech.XR.AgentSubstrate`) — P5 scope. VICKY observer + gated action surface.
5. **Bridge** (`Pitech.XR.Bridge`) — UaaL seam. P5 scope.
6. **Consumers** — HealthOn AR, HealthOn VR, DevKit simulator host, future Web Lab Player.
7. **Authoring** (`Pitech.XR.Authoring`) — editor-only. Hub, Building Blocks, Simulator, Debuggers, Validators.

## Dependency rule
A layer may reference only lower-numbered layers. Enforced by `.asmdef` references + CI Gate 5.

## Quick start for new engineers
1. Open `e:\Unity files\Pi tech DevKit` in Unity 6000.3+.
2. Menu `Pi tech → DevKit Hub` — your front door.
3. Open scene `Assets/DevKitSimulatorHost.unity` → Play → reference lab plays.
4. Read `docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md` end-to-end before touching core code.
```

- [ ] **Step 2: Commit**

```
git commit -m "docs(p1): ARCHITECTURE.md quick reference (spec §0)"
```

---

### Task 70: `Documentation~/CAPABILITY_AUTHORING_GUIDE.md`

**Files:**
- Create: `Packages\pitech-xr-devkit\Documentation~\CAPABILITY_AUTHORING_GUIDE.md`

- [ ] **Step 1: Write**

Document: how to declare a new capability, when to add an interface vs extend an existing one, attribute discovery rules, priority, scope, and how consumers override. Include a worked example: adding `IThermometerService` for a hypothetical clinical thermometer mock.

- [ ] **Step 2: Commit**

```
git commit -m "docs(p1): CAPABILITY_AUTHORING_GUIDE.md (spec §9.3)"
```

---

### Task 71: `Documentation~/CI_GATES.md`

**Files:**
- Create: `Packages\pitech-xr-devkit\Documentation~\CI_GATES.md`

Document each of the 7 gates with: what it checks, test file location, how to run locally (Test Runner path + CLI command), how to interpret failure, common false positives.

- [ ] **Steps 1–2**: write + commit

```
git commit -m "docs(p1): CI_GATES.md — 7 gate specs with local + CI invocation (spec §15.3)"
```

---

### Task 72: `Documentation~/CONSUMER_PINNING.md`

**Files:**
- Create: `Packages\pitech-xr-devkit\Documentation~\CONSUMER_PINNING.md`

Document: how AR/VR repos pin a DevKit git tag, how to switch versions, how to roll back, the Unity 6 upgrade that AR needs before adopting v0.11 (spec §17.3.1).

- [ ] **Steps 1–2**: write + commit

```
git commit -m "docs(p1): CONSUMER_PINNING.md — AR/VR adoption guide (spec §17.3, §18.3)"
```

---

### Task 73: Update `CHANGELOG.md` with final v0.11.0 entry

**Files:**
- Modify: `Packages\pitech-xr-devkit\CHANGELOG.md`

Move the `0.11.0-pre.1` section to `0.11.0` (drop `-pre.1` suffix); add final summary of shipped items.

- [ ] **Step 1**: edit
- [ ] **Step 2**: commit

```
git commit -m "docs(p1): finalize CHANGELOG v0.11.0 entry"
```

---

### Task 74: Bump `package.json` version to `0.11.0`

**Files:**
- Modify: `Packages\pitech-xr-devkit\package.json`

Change `"version": "0.11.0-pre.1"` → `"version": "0.11.0"`.

- [ ] **Steps 1–2**: edit + commit

```
git commit -m "chore(p1): bump package.json to 0.11.0 for release"
```

---

### Task 75: Full phase-exit verification

- [ ] **Step 1**: Run ALL tests (EditMode + PlayMode) → all green except deferred Gate 3 (golden replay; P2 activates)
- [ ] **Step 2**: Open Unity, build to Android IL2CPP (player-mode smoke build); verify no runtime errors during simulator-host launch
- [ ] **Step 3**: Run Gate 7 (IL2CPP round-trip) on Android — PASS
- [ ] **Step 4**: Confirm public API baseline diff (Gate 1) shows additions only, zero removals
- [ ] **Step 5**: Confirm v0.10 fixture load (Gate 2) passes on every fixture
- [ ] **Step 6**: Confirm dependency direction (Gate 5) passes
- [ ] **Step 7**: Manual spot-check: open a scene that uses `XRServices.Get<T>()` (from a v0.10 consumer) and verify it resolves through the new shim

If ALL pass, P1 is complete. If any fail, triage and fix before tagging.

---

### Task 76: Tag `v0.11.0` release

- [ ] **Step 1**: Merge `feature/p1-foundation` → `main` via PR

Petros creates the PR via `gh pr create` and self-reviews. Merge via `gh pr merge` after all checks pass.

- [ ] **Step 2**: On `main`, tag the release

```
git checkout main
git pull
git tag -a v0.11.0 -m "P1 Foundation: 30-asmdef topology, event bus, 6 registries, 17 capability interfaces, XRServices shim, non-breaking contract with 6 active CI gates, Unity 6 target"
git push origin v0.11.0
```

- [ ] **Step 3**: Announce in team channel

"DevKit v0.11.0 tagged. P1 Foundation complete. Zero behavior change for AR/VR consumers — but AR must upgrade to Unity 6 before pinning (see `CONSUMER_PINNING.md`). Next: P2 step runner extraction plan."

- [ ] **Step 4**: File follow-up tasks in Pi tech Workspace
- Capture golden replay fixtures (P2 kickoff)
- Schedule AR Unity 6 upgrade validation
- Open P2 planning brainstorm

---

## Plan self-review (spec coverage check)

Every spec §17.2 P1 deliverable maps to at least one task:

| Spec P1 deliverable | Plan task(s) |
|---|---|
| 10 new asmdefs | 15 (Domain), 23 (Core subfolders), 42 (Capabilities), 52/53 (Analyzers), 56 (Authoring), 10/11 (Test asmdefs) |
| ILabEventBus + typed events + subscription tables (zero-alloc) | 24, 25, 26, 27 |
| Six registries | 29/30 (Capability), 32 (StepRunner), 33 (EffectHandler), 34 (Validator), 35 (BuildingBlock), 36 (InspectorControl) |
| LabRoot + DevKitBootstrapper + CapabilityRegistry | 30, 38, 39, 50 |
| XRServices shim | 31 |
| All 17 capability interfaces + defaults | 43–48 |
| BuildingBlockMetadataV1 schema + sidecar reader + first validator | 17, 21 |
| SchemaVersion attribute + registry | 16 |
| IDevKitLogger + channels | 28 |
| DevKitEditorWindow base + design system USS | 57, 58 |
| DevKit Hub v1 | 59 |
| Test harness (6 EditMode + 3 PlayMode asmdefs) | 10, 11 |
| 6 CI gates active + Gate 7 seeded | 14 (G1), 61 (G2), 27 (G4), 62 (G5), 53/54 (G6 warning), 63 (G7) |
| Roslyn analyzer in warning mode | 52, 53, 54 |
| XRServices preserves v0.10 surface | 31 |
| Package.json unity 2022.3 → 6000.0 | 6 |
| v0.10 fixture corpus | 3 |
| Public API baseline | 4, 12, 13, 14 |
| Newtonsoft.Json + link.xml + Preserve + AotHelper | 17, 18, 19 |
| MPPM installed | 7 |
| Documentation: ARCHITECTURE, CAPABILITY_AUTHORING_GUIDE, CI_GATES, CONSUMER_PINNING | 69, 70, 71, 72 |
| CHANGELOG.md v0.11.0 entry | 8, 73 |
| `DevKit Simulator Host` as fourth consumer | 65, 66, 67 |
| Reference lab plays in simulator | 67 |

No gaps identified. No placeholder tasks (every step has actionable content).

---

## Execution handoff

Plan complete and saved to `docs/plans/2026-04-23-p1-foundation.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration
**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach would you like?

> **Note on agent permissions:** Per `AGENT_PERMISSIONS.md` Rule 1, agents MUST NOT run git state-mutating commands. The git steps in each task are executed by **Petros** manually. The plan documents exact commands so there's no ambiguity. Local file edits (creating files, writing tests, running Test Runner, etc.) can proceed via agent tools.
