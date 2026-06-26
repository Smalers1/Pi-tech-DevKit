---
title: Phase B - Analytics, Localization & Vitals Foundation (first behaviour-additive)
status: RATIFIED by Petros (board) 2026-06-10 - pending Stergios final review; dispatch on his sign-off. WS B9 (multiplayer step-sync bridge) ADDED 2026-06-10 at Stergios review - pending Petros ratification of the addition
date: 2026-06-09
author: Claude (board)
owner: Stergios & Alexandros (DevKit runtime + SDK + Localization + Vitals foundation + B9 step-sync bridge) + Petros & Alex + Lovable (cloud ingest + portal / Web Portal)
phase: B
gate: Phase A WS A3 net (EditMode equivalence harness + "DevKit > Evaluate Changes") must be green before any behaviour-additive change in this phase lands
references:
  - 2026-06-09-devkit-launch-plan.md (umbrella / index)
  - 2026-06-09-phase-a-refactor-and-foundation.md (Phase A - the WS A3 net that gates every change here)
  - 2026-06-09-phase-c-integration-and-ship.md (Phase C - integrate + ship)
  - 2026-06-10-phase-b-multiplayer-step-sync.md (WS B9 detail/rationale companion - guid-keyed step-sync bridge; the exact sync flow, the AR no-Fusion guarantee proof, the A/B/C/after-launch binding, the measured state-budget baseline)
  - _after-launch/2026-06-09-after-launch-plan.md (POST-LAUNCH Phases D..I + domain systems; AgentObservation / VICKY-observer + cloud-localization + vitals-digital-twin detail; consolidates the archived 2026-05-08-p2-behavior-roadmap.md)
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md (architecture; §8 Layer 2 - Runtime: ScenarioRunner / LabConsoleRuntime; §28 domain & content systems addendum - §28.3 Localization, §28.4 Vitals, §28.5 AI-assisted authoring)
companion: 2026-06-09-phase-a-refactor-and-foundation.md (the WS A3 net) + 2026-06-09-phase-c-integration-and-ship.md (ship)
---

# Phase B - Analytics, Localization & Vitals Foundation (first behaviour-additive)

> ⚠️ **SUPERSEDED (2026-06-26) by `../proposed plans/devkit-architecture-map-phase-b.md` (decision log 2026-06-26b).** This
> 2026-06-09 plan predates the evolved architecture — **do NOT implement from it as-is.** Superseded on: (1) **analytics
> wire** — the session-report schema replaces per-event `AnalyticsEventV1` + Flow-A/B; (2) **runner extraction** — FULL B.1
> extracts + renames `SceneManager → LabConsole` at launch (this plan said "no extraction"); (3) **multiplayer** — the
> path-list flow-store replaces the B9 bool-bridge; (4) **LabConsole** — ships at launch (this plan said post-launch);
> (5) **analytics model** — sidecar metrics/objectives/bands/subjects-registry, NOT per-Action config on the `Step`;
> (6) **Stats** — replaced by the param store. **KEPT (re-homed onto the map):** the cloud-lane structure (B4/B5/B6), the
> queued transports (`DirectCloud`/`Bridge`), the **consent [HUMAN] step** (B4 Step 4), Greek+English localization (B7), the
> exit-criteria format, the no-emoji hygiene. The map is the authority for B.1/B.2.

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` (or `superpowers:subagent-driven-development`)
> to implement WS-by-WS. Steps use `- [ ]` checkbox syntax for progress tracking. Every behaviour-additive change here is
> validated by the Phase A **WS A3 net** before it is admitted.
>
> **Completion discipline (Petros, 2026-06-10): every phase completes IN FULL - every small step ticked, none
> skipped.** Steps tagged **[HUMAN]** are human-owned work an AI cannot do; any AI agent working in this phase MUST
> actively REMIND the human owner of unticked [HUMAN] steps (do not silently pass over them) and must not declare a
> WS done while one is open.

**Goal:** Ship the launch's first behaviour-additive layers on top of the behaviour-neutral Phase A foundation: (1) a
deterministic, end-to-end **analytics** path - a lab author marks which steps are tracked, the engine records
time / errors / score per tracked step at runtime, those events reach the cloud, and an instructor sees a per-attempt
readout in the web portal; (2) a **Localization module** relocated into DevKit that keys lab/analytics text and ships
**Greek + English** via the build-baked pipeline (AR gets localization for the first time); (3) a **Vitals
FOUNDATION** - the typed `PatientVitals` component + 3D-binding model + `IAgentStateSource` seam, laid additively
alongside the existing scattered logic; and (4) a **multiplayer step-sync BRIDGE** (WS B9, added 2026-06-10) - synced
steps publish guid-keyed completion FACTS from the runner's completion lifecycle onto the existing consumer-side
`NetworkStateManager`, retiring the hand-typed `UIStateTrigger` switchboard wiring. End-to-end green in one real test
lab on both AR and VR is the analytics exit bar; Greek+English rendering and a typed vitals read into VICKY's state
seam are the localization and vitals exit bars; a two-client VR sync plus an AR/no-Fusion trace identical to the
Phase A golden is the step-sync exit bar.

**Architecture stance:** Behaviour-ADDITIVE, gated on the WS A3 net. Every addition rides the existing locked runner
(a THIN additive emission hook, NOT runner extraction). Existing labs keep loading by GUID, unchanged. Localization and
vitals are introduced ALONGSIDE the existing logic (build-baked localization; typed vitals foundation), never as a
cutover. Step-sync (B9) likewise rides the locked runner - a consumer-side bridge subscribed to B3's thin completion
emission; no runner extraction, no DevKit public flow type (the `IScenarioFlowStore` graduation stays post-launch, §7).
The full cloud-localization pipeline, vitals digital-twin, and AI-judging layer are POST-LAUNCH (§7, after-launch plan).

**Spec reference:** `../specs/2026-04-23-devkit-1.0-target-architecture-design.md` - §8 (Layer 2 - Runtime: ScenarioRunner /
LabConsoleRuntime), §28.2 (Networking - the multiplayer step model; launch = WS B9's guid-keyed bridge, the
`IScenarioFlowStore` graduation post-launch), §28.3 (Localization - extend `ILocalizationService` into a cloud content pipeline; launch = keyed
Greek+English build-baked), §28.4 (Vitals - the patient digital twin; launch = typed foundation + `IAgentStateSource`
seam), §28.5 (AI-assisted authoring - the JSON-first seam this analytics model already uses).

**Duration / window:** **2026-06-20 -> 2026-07-14 DevKit-side** (Workspace plan-of-record; starts on WS A3, OVERLAPPING
Phase A's tail). Bound gates: **G2 emit/consent/analytics contract spec FROZEN 2026-06-29**; **SDK emit-API freeze
2026-07-07** (post-freeze additive-only; breaking changes block the Jul-15 AR/VR emit dispatch). The Lovable
cloud/portal legs (B4/B5/B6) land by their dated gates (§5.1). Phase C starts ~Jul-15. Ratified by Petros 2026-06-10;
dispatch on Stergios' sign-off.

**Exit criteria (measurable):**
- End-to-end analytics green in ONE real test lab on **both VR (Quest) and AR (mobile UaaL)** from the SAME JSON: author -> mark Actions -> run -> events ingest -> instructor sees the §2.5 readout.
- Per-Action serialized config passes Proof C (open->save zero-diff on untouched labs; only opted-in steps gain fields).
- `lab -> JSON -> lab` round-trip survives "DevKit > Evaluate Changes" on a real lab.
- The Localization module lives in DevKit; lab/analytics text is **keyed**; **Greek + English** render via the build-baked pipeline on AR + VR.
- A typed `PatientVitals` component exists, drives at least one 3D binding additively, and exposes patient state through the `IAgentStateSource` seam.
- Synced steps sync via the WS B9 bridge: guid-keyed completion facts, two-client VR green (AnyCompletes + late-join), AR / no-Fusion trace identical to the Phase A golden. (SLIP-ELIGIBLE - a slip is dispositioned in the log, never silently skipped.)
- Every Phase B change passed "DevKit > Evaluate Changes" (Proofs A/B/C). No emoji / mojibake in shipped strings or tooltips.

---

## Plan structure

| WS | Focus | Gate / depends-on | Source / spec § |
|---|---|---|---|
| B1 | Action + scoring runtime (action atom, auto-errors, worldspace tooltip, deterministic scoring) | WS A3 net green | §2.1-2.5; spec §8 |
| B2 | Per-step analytics config (serialized) + JSON additive export/import (gated on the net) | B1; WS A3 net green | §2.3, §3; spec §8 |
| B3 | DevKit Analytics SDK + DirectCloud/Bridge transport + THIN additive emission hook on the locked runner + ScenarioGraphContextHelper | B1; A-phase WS A8 `ISceneRunnerControl` seam present | §4, §5; spec §8 |
| B4 | Cloud ingest + `analytics_events` DDL + RLS (Lovable) | B3 emit shape frozen | §4; cross-surface contract |
| B5 | Portal dashboards readout + cohort rollups (Lovable) | B4; B6 | §2.5, §4 |
| B6 | `lab_scenarios` definition-publish (Lovable + Alexandros publish hook). **MANDATORY for every LAUNCH lab** - the portal readout cannot render structure without Flow A. (JSON import/round-trip stays optional for non-launch labs.) | B2 (JSON export exists) | §3, §4 |
| B7 | Localization module into DevKit + key lab/analytics text + Greek+English (build-baked; AR gets localization; cloud pipeline deferred) | B1 (analytics text keys); WS A3 net green | spec §28.3 |
| B8 | Vitals FOUNDATION (typed `PatientVitals` component + 3D-binding model + `IAgentStateSource` seam; additive; full digital-twin deferred). **SLIP-ELIGIBLE (Petros 2026-06-09): a strategic foundation, NOT a launch-DoD gate** - it never competes with B1-B6 or the store deadline; if the calendar tightens, B8 slips post-launch. | WS A3 net green | spec §28.4 |
| B9 | Multiplayer step-sync bridge (guid-keyed completion facts on the existing consumer-side `NetworkStateManager`; retires hand-typed `UIStateTrigger` wiring from synced steps; AR / no-Fusion = compiled-out no-op). **SLIP-ELIGIBLE (Stergios 2026-06-10): quality + forward-alignment - the existing switchboard already works; never competes with B1-B6 or the store deadline.** | B3 Step 4 (completion emission); WS A3 net green | companion 2026-06-10-phase-b-multiplayer-step-sync.md; spec §28.2; workbench PROPOSAL §2.1/§2.3 (end state) |

> **Lane note.** B1/B2/B3 + B7 + B8 are DevKit-repo work (Alexandros). B4/B5/B6 cloud + portal are **Lovable's lane**
> (Web Portal repo). The DevKit workspace does not edit the Web Portal repo; it freezes the emit shape (B3) and the
> definition-publish shape (B6 hook) and hands them across. B9 is **VR-consumer-repo work (Stergios)** - the bridge +
> guid-keyed listeners live beside `NetworkStateManager` in HealthOn VR; its only DevKit-side need is B3 Step 4's
> completion emission (one raise, two consumers: the analytics emitter + the sync bridge).

> **Parallelism.** Execution can parallelize once the WS A3 net is green: B1/B2 (DevKit runtime + serialized config) and
> B4/B5/B6 (Lovable cloud + portal) are disjoint and run concurrently; B3 (SDK + transport + emission hook) is the seam
> that joins them. B7 (Localization) and B8 (Vitals) are independent DevKit modules disjoint from the Scenario.cs /
> SceneManager surface, so they run in PARALLEL with the analytics workstreams. B9 (step-sync) is consumer-side and
> disjoint from everything else; it joins once B3 Step 4's completion emission lands.

> **WS tags + DRIs (Codex pass 2026-06-10).** Tags: **B1-B7 = LAUNCH_BLOCKER · B8 = POST_LAUNCH_IF_RISK · B9 = POST_LAUNCH_IF_RISK.** A tagged
> slip is dispositioned in the Status & Progress Log - never silently skipped. DRIs (one mover per WS; backup in
> parens; Stergios may rebalance at his review): B1 Stergios (Alexandros) · B2 Stergios (Alexandros) · B3 Alexandros
> (Stergios - he owns the analytics-SDK plan-of-record) · B4 Lovable (Tony) · B5 Lovable · B6 Lovable + Alexandros
> (the publish hook) · B7 Stergios (Alexandros) · B8 Alexandros (Stergios) · B9 Stergios (Alexandros).

---

## 1. Goal + governing principle

**Goal.** Ship a deterministic, end-to-end analytics path for the launch, plus a relocated Localization module
(Greek+English keyed text, build-baked) and a typed Vitals foundation. End-to-end green in one real test lab on both AR
and VR is the analytics exit bar.

**Governing principle - deterministic FIRST, AI-judging is a post-launch additive layer.**

The word "AI" splits into two things that must not be conflated:

| | What it is | When | Source of the grade |
|---|---|---|---|
| **AI-AUTHORING** | Claude / an LLM writes the scenario + analytics JSON (the lab definition); VICKY drafts translations. | NOW (launch). | A human reviews the JSON; the runtime is fully deterministic. |
| **AI-JUDGING** | VICKY scores or interprets the attempt at runtime (semantic judgement of what the learner did). | POST-LAUNCH (deferred, §7). | An additive subscriber on the same events; AUGMENTS the deterministic grade, never replaces it. |

The launch grade is **author-defined and reproducible**: same actions in the same order produce the same score, every
time, with no model in the loop. AI-authoring is allowed at launch because it only produces a static artifact a human
signs off on. AI-judging is deferred because it puts a non-deterministic model on the scoring path, which is a
correctness, consent, and EU-AI-Act surface we do not open for 1.0.

**Why one clean event stream serves three purposes.** We emit the same well-formed JSON events (`AnalyticsEventV1`)
that simultaneously are: (1) the **launch readout** the instructor reads in the portal; (2) the **AI-readable
substrate** an LLM can reason over offline; and (3) the **future AI-observation source** the post-launch VICKY observer
subscribes to. Emit it clean once; do not build three pipelines.

**The same JSON-first seam carries localization and vitals.** Per spec §28.5, the analytics/Action config, the
localization key set, and the `PatientVitals` profile are all *typed data, referenced by a stable key, resolved
externally* - the one architectural shape that makes content AI-authorable. B7 and B8 lay the seam (keyed text; a typed
vitals model) so the post-launch cloud-localization pipeline and vitals digital-twin slot under the same keys later
without new architecture.

---

## 2. The analytics model

### 2.1 The Action atom - backend steps vs action steps

A lab scenario is a **graph of steps**. Steps split into two kinds for analytics:

- **BACKEND steps** (timeline / event / condition). These auto-advance and orchestrate the scene. They are **invisible
  in the readout** - they do not score, time, or count.
- **ACTION steps.** The tracked atom. An Action is **user-must-complete, weighted, timed, error-counted**. It is the
  only thing that appears in the instructor readout.

**The readout rule: ONE ROW = ONE ACTION.** "Enabling analytics on a step" means exactly one thing in the authoring
UI: **marking that step an Action.** There is no separate analytics toggle matrix; the Action flag IS the analytics
opt-in for that step.

### 2.2 AUTO-ERRORS - author the happy path, the engine counts the rest

This is the biggest authoring win in the model. The author authors **only the happy path**: the ordered list of
required Actions and, for each, its **target** (the interactable the learner must operate). The engine does the rest:

- Any **meaningful interaction that is not the current expected target** is auto-counted as an **error** on the current
  Action, and fires a **worldspace tooltip** to correct the learner.
- The author does **not** enumerate an error matrix. There is no "if they touch X show Y" table to maintain.
- The author only adds config for **exceptions** - e.g. tag specific interactables as **critical-if-premature**, so
  that touching them out of order forces that Action to **0%** (a hard fail), not just a counted error.

The default is: every wrong interaction = +1 error + a tooltip; only the tagged exceptions escalate.

### 2.3 Per-action config - sensible defaults, minimum to author

The **minimum** an author must supply for an Action is **title + target + weight**. Everything else has a sensible
default and is touched only when the lab needs it:

| Group | Field | Default | Meaning |
|---|---|---|---|
| (required) | `title` | - | Readout row label ("Wash hands"). |
| (required) | `target` | - | Stable id of the interactable that completes this Action. |
| (required) | `weight` | - | Gravity of this Action toward the total score. |
| timing | `firstWarningAfter` + `warningText` | off | After N seconds of inactivity, show a nudge tooltip. |
| timing | `maxTime` | off | Soft cap; exceeding it applies a scaled time-overrun penalty (does not auto-fail). |
| errors | `onWrong` | generic tooltip | Tooltip text shown on a wrong interaction. |
| errors | `critical[]` + `criticalText` | empty | Targets that, if operated prematurely, force this Action to 0%. |

> All readout-facing strings (`title`, `warningText`, `onWrong`, `criticalText`) are **keyed for localization** (B7) so
> the same Action config renders Greek or English without a second authoring pass.

### 2.4 Scoring formula (concrete default, configurable)

Per Action, deterministic:

```
score      = 100
score     -= errorCount * errorPenalty                 // each counted error
if (elapsed > maxTime) score -= timeOverrunPenalty * overrunFactor   // scaled by how far over
if (criticalErrorFired) score = 0                      // hard fail, overrides the above
score      = clamp(score, 0, 100)
contribution = weight * score                          // toward the total
```

Totals are the sums: **total time** = sum of per-Action elapsed; **total errors** = sum of per-Action error counts;
**total %** = `sum(weight*score) / sum(weight)`. `errorPenalty`, `timeOverrunPenalty`, and the overrun scaling are
analytics config with launch defaults; a lab may override them.

This produces the readout directly:

```
Wash hands        01:20   2 err    0%  (critical)
Auscultate chest  03:05   0 err  100%
Pulse oximeter    02:40   1 err   85%
...
Total            24:34   13 err   78%
```

### 2.5 Worked JSON example (wash-hands / stethoscope / oximeter)

The scenario logic + analytics is JSON the AI can author. Scene references are **stable string ids**, not GUIDs (see
§3):

```jsonc
{
  "scenario_id": "lab.respiratory_exam",
  "version": 3,
  "actions": [
    {
      "id": "action.wash_hands",
      "title": "Wash hands",
      "target": "interactable.wash_station",
      "weight": 1.0,
      "maxTime": 90,
      "firstWarningAfter": 30,
      "warningText": "Begin by washing your hands.",
      "critical": ["interactable.patient_chest"],
      "criticalText": "You touched the patient before washing your hands."
    },
    {
      "id": "action.auscultate",
      "title": "Auscultate chest",
      "target": "interactable.stethoscope",
      "weight": 2.0,
      "maxTime": 240
    },
    {
      "id": "action.oximeter",
      "title": "Pulse oximeter",
      "target": "interactable.oximeter",
      "weight": 1.0,
      "maxTime": 180,
      "onWrong": "That is not the pulse oximeter."
    }
  ]
}
```

Resulting attempt readout (one row per Action, joined with the attempt's recorded numbers):

| Action | Time | Errors | Score | Note |
|---|---|---|---|---|
| Wash hands | 01:20 | 2 | 0% | critical (chest touched first) |
| Auscultate chest | 03:05 | 0 | 100% | |
| Pulse oximeter | 02:40 | 1 | 85% | |
| **Total** | **24:34** | **13** | **78%** | weighted |

---

## 3. Storage + migration

### 3.1 JSON is an ADDITIVE projection, NOT a cutover

The scenario JSON is an **export / import PROJECTION** of the existing `[SerializeReference]` scenario graph - not a
replacement for it. At launch:

- **Existing labs keep loading by GUID, unchanged.** No shipped lab is migrated to "JSON-native." The
  `[SerializeReference]` step graph remains the runtime source of truth.
- The JSON is produced **from** that graph (export) and can be reimported **into** it (import). It is a second
  representation, layered additively.
- **Full JSON-source-of-truth migration is POST-LAUNCH** (§7) - this phase only proves the round-trip is faithful.

### 3.2 The round-trip is GATED on the WS A3 net

The `lab -> JSON -> lab` round-trip is **gated on the Phase A WS A3 net**. WS A3's equivalence proofs are exactly the
instrument that proves the projection preserved every step, every route, and every behaviour:

- **Proof A** (scenario graph integrity) proves no step / route / GroupStep child was dropped or dangled by the
  round-trip.
- **Proof C** (serialized + GUID integrity, open->save zero-diff) proves reimport did not perturb serialization.

A JSON round-trip that does not survive Evaluate-Changes is not admitted. This is why analytics is a Phase B item and
not a Phase A item: it is the first thing the net is allowed to validate that actually adds behaviour.

### 3.3 Stable ids + the Unity Binding Sheet

The JSON refers to scene objects by **stable string ids** (`"interactable.wash_station"`), never by raw GUID or
transform path. A Unity **Binding Sheet** maps each id to an actual `GameObject` in the scene - a human drags each
binding **once per scene**. Consequences:

- The JSON is **portable and AI-authorable**: an LLM can write `"interactable.wash_station"` without ever seeing a GUID.
- **Cross-surface reuse**: the **same lab JSON runs in AR and VR** if both scenes bind the same ids. One scenario
  definition, two surfaces, identical scoring logic.
- Re-binding survives scene edits: moving / replacing the GameObject only updates the Binding Sheet, not the JSON.

### 3.4 The ScenarioGraph window is the human editor

The existing **ScenarioGraph window** (the visual editor in the DevKit) is the human read/write surface for this JSON.
It is not a new tool - it reads and writes the same projection an LLM authors. Author by hand in the graph, or have
Claude draft the JSON and open it in the graph to review. Both paths converge on one artifact. (This is the §28.5
JSON-first authoring seam in concrete form.)

---

## 4. The two portal data flows

The portal must show **all** steps of a lab - even steps a given learner never reached - and overlay each attempt's
numbers. That requires **two** data flows, joined in the portal:

| Flow | What | Cadence | Carries |
|---|---|---|---|
| **(A) Lab Definition** | the scenario JSON published once per lab **version** (`lab_scenarios`). | once per lab version | the full ordered Action list + titles + weights + targets. |
| **(B) Attempt Analytics** | per-session `AnalyticsEventV1` events. | per step, per session | time / errors / score / state per Action, per attempt. |

The **readout = A (the rows / structure) + B (the numbers)**, joined on **`scenario_id` / `step_id` / `attempt_id`**.
Flow A is why the portal can render the structure of steps the learner never reached (it has the full definition); Flow
B fills in the numbers for the steps that were.

**The contract already carries the join keys.** `AnalyticsEventV1` **rev-3** already carries
`scenario_id`, `step_id`, `action_id`, `step_state`, `performance_metric`, `attempt_number`, and `semantic_state` - no
contract change is required to support this join. Phase B wires the producers and consumers around the existing fields.

---

## WS B1 - Action + scoring runtime

**Goal:** The deterministic Action-tracker exists end-to-end at runtime: per-Action time, auto-error counting,
worldspace tooltip, deterministic score, critical-tag 0% path - reproducing the §2.5 readout numbers exactly.

**Scope / files:** DevKit runtime (Alexandros). Action-tracker + scoring component(s) under the runtime; a worldspace-
tooltip primitive. Rides the existing SceneManager runner; no extraction.

**Steps (progress tracking):**
- [ ] Step 1: Implement the Action atom (user-must-complete, weighted, timed, error-counted) distinct from backend steps (§2.1).
- [ ] Step 2: Implement per-Action time tracking + **auto-error counting**: any non-target meaningful interaction -> +1 error on the current Action (§2.2).
- [ ] Step 3: Add the worldspace-tooltip primitive; fire it on a wrong interaction and on the inactivity nudge.
- [ ] Step 4: Implement the deterministic §2.4 scoring formula (error penalty, scaled time-overrun penalty, weighted contribution, clamp).
- [ ] Step 5: Implement the critical-tag 0% hard-fail path (premature operation of a `critical[]` target forces the Action to 0%).
- [ ] Step 6: Replay the §2.5 worked example; assert it reproduces the readout numbers deterministically (same input -> same output, every run).

**Acceptance:** The Action-tracker is live: per-Action time, auto-error counting (+1 error + worldspace tooltip on any
non-target meaningful interaction), deterministic §2.4 score, critical-tag 0% path. Replaying the worked example
produces the §2.5 readout numbers deterministically. No model in the scoring loop.

**Gate:** WS A3 net green - "DevKit > Evaluate Changes" passes (no shipped lab regressed).

---

## WS B2 - Per-step analytics config (serialized) + JSON export/import

**Goal:** Serialize per-Action config on the step (the first after-net serialized-diff change) and implement the
faithful `lab -> JSON -> lab` round-trip, gated on the net.

**Scope / files:** DevKit runtime + editor (Alexandros). Serialized fields on the Action step; JSON export/import
projection (§3); the Binding Sheet id mapping (§3.3).

**Steps (progress tracking):**
- [ ] Step 1: Serialize the per-Action config (title/target/weight + the §2.3 optional fields) on the step.
- [ ] Step 2: Run Proof C on untouched labs - open->save zero-diff; only opted-in steps gain fields (this is the FIRST after-net serialized-diff change).
- [ ] Step 3: Implement JSON **export** from the `[SerializeReference]` graph (stable string ids, never GUIDs; §3.1, §3.3).
- [ ] Step 4: Implement JSON **import** back into the graph (the additive projection, not a cutover).
- [ ] Step 5: Round-trip a real lab (`lab -> JSON -> lab`); run "DevKit > Evaluate Changes" - Proof A (graph integrity) + Proof C (serialization) stay green (§3.2).
- [ ] Step 6: Wire the round-trip through the existing ScenarioGraph window as the human review surface (§3.4).

**Acceptance:** Per-Action config is serialized and passes Proof C (open->save zero-diff on unchanged labs; only opted-in
steps gain fields). The `lab -> JSON -> lab` round-trip is implemented and gated on the net - round-trip a real lab,
Evaluate-Changes stays green.

**Gate:** B1; WS A3 net green.

---

## WS B3 - DevKit Analytics SDK + transport + thin emission hook

**Goal:** Land the C# `AnalyticsEventV1` SDK + both transports + a THIN additive emission hook on the still-locked
runner. NOT runner extraction.

**Scope / files:** DevKit runtime (Alexandros). `Runtime/ContentDelivery/Analytics/V1/`; `DirectCloudQueuedTelemetryService`
(VR) + `BridgeQueuedTelemetryService` (AR/mobile UaaL); a thin emission hook on the SceneManager runner; the manual
`ScenarioGraphContextHelper`.

**Steps (progress tracking):**
- [ ] Step 1: Port `AnalyticsEventV1` (rev-3 shape) to C# in `Runtime/ContentDelivery/Analytics/V1/` (carries `scenario_id`/`step_id`/`action_id`/`step_state`/`performance_metric`/`attempt_number`/`semantic_state` - §4).
- [ ] Step 2: Implement `DirectCloudQueuedTelemetryService` (VR) - `UnityWebRequest`-based (IL2CPP / Quest AOT-safe), offline queue + backoff + batch.
- [ ] Step 3: Implement `BridgeQueuedTelemetryService` (AR / mobile UaaL) over the bridge - same queue + backoff + batch semantics.
- [ ] Step 4: Add the **THIN ADDITIVE emission hook** on the still-locked runner (step enter / complete / error -> `AnalyticsApi.Emit`). Rides the existing SceneManager runner; extraction is Phase D (post-launch).
- [ ] Step 5: Implement the manual `ScenarioGraphContextHelper` (context plumbing for the emit).
- [ ] Step 6: Gate emit on consent (consent-gated emit, non-negotiable cross-surface contract).
- [ ] Step 7: Freeze the emit shape and hand it across to Lovable (B4 depends on a frozen shape).

**Acceptance:** C# port of `AnalyticsEventV1` lands in `Runtime/ContentDelivery/Analytics/V1/`.
`DirectCloudQueuedTelemetryService` (VR) + `BridgeQueuedTelemetryService` (AR/mobile UaaL) transports, both
`UnityWebRequest`-based, with offline queue + backoff + batch. A thin additive emission hook on the still-locked runner
plus the manual `ScenarioGraphContextHelper`. Consent-gated emit. **NOT runner extraction.**

**Gate:** B1; A-phase WS A8 `ISceneRunnerControl` seam present.

---

## WS B4 - Cloud ingest + `analytics_events` DDL + RLS (Lovable)

**Goal:** Persist validated, tenant-scoped analytics events in the cloud.

**Scope / files:** Web Portal repo (Lovable's lane). `analytics-events-ingest` edge fn + `analytics_events` table + RLS.

**Steps (progress tracking):**
- [ ] Step 1: Author the `analytics_events` DDL (tenant-scoped) + RLS policies.
- [ ] Step 2: Implement `analytics-events-ingest` edge fn: validate uuid / size / surface / consent.
- [ ] Step 3: Cover reject paths (bad uuid, oversize, missing consent, wrong surface).
- [ ] **Step 4 [HUMAN - do NOT skip; AI agents working this phase MUST remind the human owner if unticked]:
      Define + wire the launch CONSENT UI/STATE.** The ingest REJECTS events without consent, so if nothing supplies
      consent state, the Aug-15 end-to-end smoke fails as "everything wired, zero events flow." Decisions + work,
      human-owned (Stergios/Alexandros + Petros + Lovable): (a) launch model = **org-level analytics consent** set
      during manual university enrollment (the B2B contract is the legal basis), carried in the launch context the
      apps already receive; (b) any per-user consent UI lives in the SURFACES - **VR Shell + mobile app** (like the
      existing camera-permission prompt) - **NOT in DevKit**; the DevKit SDK only reads host-provided consent state
      into `consent_acknowledged_at`; (c) Lovable confirms how the org flag reaches the ingest check. *(Note: the
      `DenyAllConsentGate` in the AgentSubstrate code is the PARKED Vicky-observation gate - unrelated to launch
      analytics; do not confuse the two.)*
- [ ] Step 5: Verify real VR + AR events from a test lab persist and read back tenant-scoped under RLS — **with the
      Step-4 consent state supplied** (this is the proof Step 4 actually happened).

**Acceptance:** `analytics-events-ingest` validates uuid / size / surface / consent; rows land in `analytics_events`,
tenant-scoped under RLS. Reject paths covered. Real VR + AR events from a test lab persist and read back tenant-scoped.

**Gate:** B3 emit shape frozen.

---

## WS B5 - Portal dashboards (readout + cohort rollups) (Lovable)

**Goal:** Render the per-attempt readout and cohort rollups in the web portal.

**Scope / files:** Web Portal repo (Lovable's lane). Readout table + cohort rollup views.

**Steps (progress tracking):**
- [ ] Step 1: Render the per-attempt **readout table** (§2.5 shape) by joining Flow A + Flow B on `scenario_id`/`step_id`/`attempt_id` (§4).
- [ ] Step 2: Render cohort rollups (per-cohort averages of time / errors / %).
- [ ] Step 3: Handle loading / empty / error states.

**Acceptance:** The per-attempt readout table renders by joining Flow A + Flow B on the join keys. Cohort rollups render.
Loading / empty / error states handled.

**Gate:** B4; B6.

---

## WS B6 - `lab_scenarios` definition-publish (Lovable + Alexandros publish hook)

**Goal:** Publish the scenario JSON (Flow A) once per lab version so the portal renders ALL steps, including unreached ones.

**Scope / files:** Web Portal repo (Lovable's lane) + a DevKit publish hook (Alexandros). `lab_scenarios` table + publish path.

**Steps (progress tracking):**
- [ ] Step 1: Define `lab_scenarios` storage (versioned per lab version).
- [ ] **Step 1b [2028-foresight - do BEFORE the shape freezes]: declare the published JSON's lineage = `ScenarioV1`**
      (spec §7.4 contract #3, "Authoring JSON export; future web portal") - or an explicitly versioned
      `ScenarioProjectionV1` with a written convergence rule into ScenarioV1. University dashboards consume this
      shape from day 1; freezing a divergent shape creates a third scenario contract and an unplanned migration -
      the single biggest prompt-to-simulation (2028) corner-cut risk. Same pass: Binding Sheet ids use the spec §7.3
      stable-id namespace so 2028 composition refs + analytics join keys are ONE namespace.
- [ ] Step 2: Publish the scenario JSON (Flow A) once per lab version into `lab_scenarios` (Alexandros freezes the publish-shape hook per Step 1b; Lovable wires the cloud side).
- [ ] Step 3: Re-publish on lab-version bump.
- [ ] Step 4: Verify the portal renders ALL steps - including steps no attempt reached.

**Acceptance:** The scenario JSON is published once per lab version into `lab_scenarios` so the portal renders all steps,
including steps no attempt reached. Versioned; re-publish on lab-version bump.

**Gate:** B2 (JSON export exists).

---

## WS B7 - Localization module into DevKit + Greek+English (build-baked)

**Goal:** Relocate the existing HealthOn VR localization pipeline into a DevKit **Localization module**, key the
lab/analytics text, and ship **Greek + English** via the build-baked pipeline. AR gets localization for the FIRST time.
The full CLOUD pipeline (Web-Portal editor + VICKY translate + ElevenLabs audio + runtime fetch-by-language) is deferred
to the after-launch plan.

**Scope / files:** DevKit (Alexandros). A DevKit Localization module (the WS A2-reserved Localization slot); the existing
`Assets/Scripts/Editor/Localization/` key model (`GlobalObjectId`-keyed) + the medical-terminology translate prompt
(`ManualTranslationIO`) relocated into DevKit per spec §28.1/§28.3. Build-baked StringTables. Greek + English.

**Steps (progress tracking):**
- [ ] Step 1: Relocate the existing HealthOn VR localization pipeline (`GlobalObjectId`-keyed manifest + `ManualTranslationIO` medical-translate prompt) into the DevKit Localization module slot reserved in Phase A WS A2 (spec §28.1, §28.3).
- [ ] Step 2: Key the lab text + the B1/B2 analytics-facing strings (`title`, `warningText`, `onWrong`, `criticalText`) so the cloud resolver can slot under the same keys post-launch (the launch requirement per §28.3).
- [ ] Step 3: Wire the build-baked StringTable pipeline as the launch source (cloud resolver is post-launch; baked tables become the offline fallback / launch bootstrap - avoid two sources of truth, §28.3 trap).
- [ ] Step 4: Author Greek + English string sets via the relocated translate prompt (VICKY drafts, human reviews; the §28.5 AI-authoring seam).
- [ ] Step 5: Verify Greek + English render at runtime on **VR (Quest)** and - for the first time - **AR (mobile UaaL)** from the same keyed source.
- [ ] Step 6: Run "DevKit > Evaluate Changes" - the relocation + keying is additive and does not regress a shipped lab.

**Acceptance:** The Localization module lives in DevKit (relocated, not duplicated). Lab text and analytics-facing
strings are **keyed**. Greek + English render at runtime on both AR and VR via the build-baked pipeline. The cloud
self-serve pipeline is explicitly deferred to the after-launch plan; the only launch requirement met here is that text
is keyed so the cloud resolver slots under the same keys later.

**Gate:** B1 (analytics text keys to localize); WS A3 net green.

---

## WS B8 - Vitals FOUNDATION (typed PatientVitals + IAgentStateSource seam)

**Goal:** Lay the **typed foundation** of the patient digital twin - a typed `PatientVitals` component, the 3D-binding
model, and the `IAgentStateSource` implementation - additively, alongside the existing scattered VR physiology logic.
The full digital-twin (cascade rules, ScriptableObject profile library, scene migration, ControlOptionManager-off-PUN
convergence) is deferred to the after-launch plan.

**Scope / files:** DevKit (Alexandros). A typed `PatientVitals` component + a `Vital` type (a typed `ConsoleParameter` +
a 3D binding) in the DevKit Vitals module slot (reserved in Phase A WS A2). Implements `IAgentStateSource` (the
`ILabObserver` feed, spec §10.5 / Appendix A #16) using the empty seam the P5 substrate-observer slice left.

> **Boundary note.** `ControlOptionManager` + avatars stay as VR scripts for now (out of DevKit scope this phase). They
> are NOT planned here. B8 is purely the typed-foundation seam; the convergence onto LabConsole/Fusion is after-launch.

**Steps (progress tracking):**
- [ ] Step 1: Define the `Vital` type = a typed `ConsoleParameter` + a 3D BINDING (blendshape / timeline-speed / material / audio-pitch driver) per spec §28.4.
- [ ] Step 2: Implement the `PatientVitals` component as the single typed model (pulse / breathing / BP / temp / wounds / skinColour ...), additive alongside the existing scattered logic.
- [ ] Step 3: Wire at least one real 3D binding through the typed model (e.g. the breathing-blendshape timeline-speed vital) to prove the binding model end-to-end.
- [ ] Step 4: Implement `IAgentStateSource` on `PatientVitals` using the empty seam the P5 substrate-observer slice left (spec §28.4, §10.5 / Appendix A #16) - VICKY reads structured patient state for free.
- [ ] Step 5: Verify the typed read: a VICKY-side `ILabObserver` consumer reads structured patient state off the seam (no scattered-script scraping).
- [ ] Step 6: Run "DevKit > Evaluate Changes" - the typed foundation is additive and does not regress a shipped lab.

**Acceptance:** A typed `PatientVitals` component exists with the `Vital` = typed-`ConsoleParameter` + 3D-binding model,
drives at least one real 3D binding additively, and exposes patient state through the `IAgentStateSource` seam so VICKY
reads structured state. The full digital-twin (cascade rules, profiles, scene migration, ControlOptionManager
convergence) is explicitly deferred to the after-launch plan.

**Gate:** WS A3 net green.

---

## WS B9 - Multiplayer step-sync bridge (guid-keyed completion facts)

**Goal:** Replace the hand-wired "EventStep -> `UIStateTrigger.SetStateTrue("Step7Done")`" multiplayer step-sync
pattern with a guid-keyed bridge that publishes step-completion FACTS from the runner's completion lifecycle onto the
existing `NetworkStateManager` - and prove AR / no-Fusion labs run exactly as before. Launch-minimal: AnyCompletes
semantics only; no DevKit public flow type; the `IScenarioFlowStore` / `flow.*` / typed-Fusion graduation stays
after-launch (§7).

> **Detail / rationale companion:** [2026-06-10-phase-b-multiplayer-step-sync.md](2026-06-10-phase-b-multiplayer-step-sync.md)
> (the exact sync flow, the AR no-Fusion guarantee proof, the contract-vs-backend phase binding, the measured
> state-budget baseline). Canonical checkbox tracking lives HERE; the companion is the why.

**Scope / files:** VR-consumer-repo (Stergios) - `ScenarioFlowBridge` (`#if PITECH_HAS_FUSION`, beside
`NetworkStateManager`) + guid-keyed evolution of `EventStateListener` / `TimelineStateListener`. DevKit side - NONE
beyond riding WS B3 Step 4's completion emission (one raise, two consumers); no serialized `Step` change, no public
flow type. Key: `key(guid) = "flow.step." + guid` (~42 chars, under the `NetworkString<_64>` cap), namespaced into the
singleton switchboard's shared `Capacity(64)` budget (measured 2026-06-10: worst scene = 16 distinct states ->
~48 headroom).

**Steps (progress tracking):**
- [ ] Step 1: Ride WS B3 Step 4's thin completion emission (one raise, two consumers: the analytics emitter + this bridge); if B3 wired it point-to-point to `AnalyticsApi.Emit`, generalize the raise onto `LabEventBus.StepCompleted(guid)` (additive, same gate).
- [ ] Step 2: Implement `key(guid)` + `ScenarioFlowBridge` (consumer-side, Fusion-gated): per-scene synced-guid opt-in list; completion of a synced step -> `NetworkStateManager.SetStateTrue(key(guid))`. Fire-and-forget; local advance NEVER gates on the network.
- [ ] Step 3: Evolve `EventStateListener` / `TimelineStateListener` to read `GetState(key(guid))` (keep the hand-typed `stateID` path as a compat shim during migration).
- [ ] Step 4: Add the read-gate-forbidden validator (a step's advance condition may not read a shared fact - the structural AR-hang impossibility) + the state-budget validator (shared `Capacity(64)`: warn at 48, fail above 64 - overflow is a runtime on-device failure, never discover it there).
- [ ] Step 5: Migrate one real VR lab off the `UIStateTrigger` wiring; verify two-client sync (AnyCompletes) + late-join (`GetState` already true on join).
- [ ] Step 6: Build the same lab AR / no-Fusion; assert the trace is identical to the Phase A golden (the bridge is compiled out - the "exactly as before" proof).
- [ ] Step 7: Run "DevKit > Evaluate Changes" - additive only; no shipped lab regressed.

**Acceptance:** Synced steps publish guid-keyed facts from the completion lifecycle via the consumer-side bridge; peers
react through guid-keyed listeners; AnyCompletes + free late-join green in a two-client VR lab; the AR / no-Fusion
build is trace-identical to the Phase A golden; both validators live; no hand-typed state strings on synced steps; no
DevKit public flow type. The contract is frozen for the after-launch graduation (the publish point = where
`IScenarioFlowStore.CompleteStep(guid)` lands; the key = the `flow.*` namespace; AnyCompletes = the default) - the
graduation swaps the backend, never the labs.

**Gate:** B3 Step 4 (completion emission) present; WS A3 net green.

---

## 5. Delivery-chain alignment (existing planned dates)

This phase consolidates already-planned workstreams; the dates below are the existing plan-of-record, not new
estimates:

- **DevKit Analytics SDK** (Alexandros) - planned **2026-07-01 -> 07-14**; C# port of `AnalyticsEventV1` into
  `Runtime/ContentDelivery/Analytics/V1/`. (= WS B3 SDK core.)
- **AR / VR Unity emit** - planned **2026-07-22**. (= WS B3 transports + emission hook wired in both surfaces.)
- **Cloud ingest + `analytics_events` DDL + portal dashboards** (Lovable) - **dated gates with slack (Codex pass
  2026-06-10; Lovable to confirm):** WS B4 ingest + WS B6 `lab_scenarios` publish live + **SIMULATED-payload cloud
  smoke green by 2026-08-05** -> WS B5 **portal readout rendering by 2026-08-08** -> **REAL AR+VR on-device
  end-to-end smoke by 2026-08-12**. **Aug-13/15 are reserved for store uploads, not discovery.** These dates gate
  Phase C WS C3 and the launch DoD.
- **Localization module + Greek+English** (WS B7) + **Vitals foundation** (WS B8) run in parallel with the analytics
  lane (disjoint DevKit modules); content authoring of localization keys per lab is a Phase C item (WS C2).
- **Step-sync bridge** (WS B9, Stergios) - undated / SLIP-ELIGIBLE; earliest start = B3 Step 4's completion emission
  landed; the two-client VR proof + the AR no-Fusion equivalence assertion fold into the Phase C integration window.

---

## 6. Ratified model defaults (Petros, 2026-06-10 - umbrella Ratified Decision #5)

**RESOLVED:** strict-order Actions at launch · one row = one Action · full §2.3 config shipped but **default-off** ·
Greek + English only. The items below are retained for their rationale - they are no longer open decisions. The only
remaining implementation call is WS B8's choice of FIRST vital binding (the B8 DRI decides).

1. **Action ordering - strict vs flexible.** Is the happy path **strict-order** (Action N must complete before N+1, any
   out-of-order touch is an error), or do we support **parallel / optional Actions** (a set the learner may complete in
   any order, or skippable Actions)? Strict-order is the launch-minimal default and matches the auto-error model
   cleanly. Parallel/optional is more expressive but complicates "current expected target" (the auto-error anchor).
   **Recommendation: strict-order for launch; parallel/optional post-launch.**

2. **Action granularity - one row vs sub-actions.** Is an Action always exactly one readout row, or can an Action be a
   **GroupStep with child-requirements** (a composite that scores from N child checks but still renders as one row, or
   optionally expands)? One-row is simplest and matches §2.1. Sub-actions via GroupStep buy richer labs (a "prepare
   tray" Action with 4 required items) at the cost of a more complex scoring rollup. **Recommendation: one-row at
   launch; GroupStep child-requirements as an additive post-launch extension** (the GroupStep type already exists in
   the graph, so this is non-breaking later).

3. **Launch-minimal vs fuller model.** Ship the minimum (title + target + weight + auto-errors + the §2.4 default
   scoring), or include the full optional config set (timing nudges, per-target `onWrong`, critical tags) at launch?
   The optional fields are cheap to serialize and default-off, so they do not add runtime cost - but each one is a
   surface to author, document, and QA. **Recommendation: ship the full §2.3 config but default everything except
   title/target/weight to off**, so labs that want only the minimum never see the rest.

4. **Localization scope at launch (WS B7).** Confirm **Greek + English only** for 1.0 via the build-baked pipeline, with
   the cloud self-serve pipeline (VICKY translate + ElevenLabs audio + runtime fetch-by-language) deferred to
   after-launch. **Recommendation: Greek + English build-baked at launch; cloud pipeline post-launch (§28.3).**

5. **Vitals binding surface at launch (WS B8).** Which single vital binding do we wire end-to-end to prove the model
   (recommend the breathing-blendshape timeline-speed vital, since the existing VR logic already drives it)? Confirm the
   foundation-only scope - no cascade rules / profile library / scene migration at launch. **Recommendation: one proven
   binding + the `IAgentStateSource` seam; full twin post-launch (§28.4).**

---

## 7. Deferred to post-launch

Explicitly OUT of Phase B. These are additive layers on the **same** clean seams - building the seams right at launch is
what keeps them cheap later. The consolidated post-launch home for all of these is the after-launch plan
([_after-launch/2026-06-09-after-launch-plan.md](_after-launch/2026-06-09-after-launch-plan.md)), which absorbs the
archived `2026-05-08-p2-behavior-roadmap.md`.

- **AI-JUDGING / VICKY-observer layer.** A post-launch **additive subscriber** on the same `AnalyticsEventV1` /
  observation events that lets VICKY interpret an attempt semantically. It **augments** the deterministic grade (adds
  a narrative / a Tier-3 read), it **never replaces** the author-defined score. Tracked in the after-launch plan
  (Phase G substrate observer).

- **`AgentObservationV1` as a second sink.** Two cloud contracts both touch "observe the lab":
  `AgentObservationV1` (-> `agent-observation` edge fn; VICKY semantic observation; built transport-half only by LooPi,
  producer unbuilt, consent C3-locked) and `AnalyticsEventV1` (-> `analytics-events-ingest`; the LAUNCH instructor
  dashboards). **RESOLVED (Petros 2026-06-10, umbrella Ratified Decision #4): TWO SINKS on the SAME source
  events** - `AnalyticsEventV1` = launch instructor-dashboard rollups; `AgentObservationV1` = VICKY's post-launch
  real-time observe / Tier-3 read-path. **PARK `AgentObservation` + LabConsole until post-launch (Phase G).**
  > The analytics-SDK plan's §4 wrongly says the substrate observer should emit **through** `AnalyticsApi.Emit()`,
  > which contradicts the as-built `AgentObservation` path. Repairing that sentence and reconciling the DevKit spec is a
  > **Web-Portal-repo edit (Lovable / Heisenberg lane), NOT done from the DevKit workspace.** Flagged here so it is not
  > lost; it is a Phase-B deferred item, not a Phase-B deliverable.

- **`SceneManager -> LabConsole` split.** The runner-extraction (Phase D) + LabConsole control plane (Phase E) are
  post-launch. Launch analytics deliberately rides a **thin additive emission hook on the still-locked runner** (WS B3)
  precisely so it does NOT depend on cracking SceneManager. Analytics stays a **separate tap on the LabEventBus**,
  decoupled from LabConsole, because analytics ships at launch while LabConsole does not. See
  [../specs/2026-04-23-devkit-1.0-target-architecture-design.md](../specs/2026-04-23-devkit-1.0-target-architecture-design.md)
  §8 (Layer 2 - Runtime).

- **Full JSON-source-of-truth migration.** At launch the `[SerializeReference]` graph stays the runtime source of
  truth and JSON is a projection (§3.1). Flipping JSON to be the authoritative source - with an OFFERED, never-forced
  migration of existing labs - is post-launch and itself gated on the same net.

- **Localization CLOUD pipeline (WS B7's post-launch half).** The Web-Portal lab-content editor + VICKY auto-translate
  edge function + ElevenLabs audio narration + runtime fetch-by-language `(contentKey, language) -> {text, audioUrl}` -
  the EU-scale moat ("Germany hospital, 4 labs, translated in minutes"). At launch only the KEYS exist (build-baked
  Greek+English); the cloud resolver slots under the same keys post-launch (spec §28.3). After-launch plan, Part 2 (ii).

- **Vitals DIGITAL-TWIN full (WS B8's post-launch half).** Cascade rules (low pulse -> pale skin) as derived bindings,
  ScriptableObject patient profiles (author a patient in minutes), migration of the ~8 scattered VR physiology scenes,
  and converging the `ControlOptionManager` off PUN onto the Fusion/LabConsole path. At launch only the typed FOUNDATION
  ships (spec §28.4). After-launch plan, Part 2 (iii).

- **Multiplayer-into-DevKit + AI-assisted authoring command library.** The NetworkedStates -> `IScenarioFlowStore`
  backend + runner-fact-driven late-join (§28.2) and the DevKit Hub apply-command library + CLI/MCP bridge (§28.5) are
  after-launch (Part 2 (i) + (iv)). Phase B only relies on the JSON-first / keyed / typed seam those systems will ride.
  **Reconciliation (2026-06-10): WS B9 lands only the launch-minimal bridge** - guid-keyed completion facts on the
  existing consumer-side `NetworkStateManager`, no DevKit public flow type - so this deferred item stays after-launch
  UNCHANGED. B9 freezes the contract (publish-from-completion-lifecycle, key = guid, AnyCompletes, local-advance-never-
  gates); the graduation here swaps the backend (`IScenarioFlowStore` -> `flow.*` -> typed Fusion + Barrier + typed
  late-join + §9.8 re-gating + switchboard retirement) under labs that are never re-authored.

---

## 8. Exit checklist + gate

**The gate (binding on every change in this phase):** every behaviour-additive change in Phase B passes the **Phase A
WS A3 net** - "DevKit > Evaluate Changes" green (Proofs A/B/C), proving the addition did not break a shipped lab. A
change that cannot pass the net is not admitted to launch.

> **Motivating failure this gate prevents.** LooPi's AgentObservation EditMode tests were authored but **never
> compiled** ("NOT_RUN - requires Unity Editor"), so a `CS0122` (an internal writer referenced across the test-assembly
> boundary) shipped and only surfaced when Petros opened Unity (since fixed via `InternalsVisibleTo`). That is exactly
> the failure mode WS A3's Evaluate-Changes gate exists to catch BEFORE it ships. Phase B is the first phase that adds
> code; it is the first real customer of that gate.

**Exit checklist:**

- [ ] **B1** Action-tracker live: per-Action time, auto-error counting + worldspace tooltip, deterministic §2.4
      scoring, critical-tag 0% path. Worked example (§2.5) reproduces the readout numbers deterministically.
- [ ] **B2** Serialized per-Action config passes Proof C (zero-diff on untouched labs); `lab -> JSON -> lab`
      round-trip green under the WS A3 net for a real lab.
- [ ] **B3** Analytics SDK in `Runtime/ContentDelivery/Analytics/V1/`; `DirectCloud` (VR) + `Bridge` (AR) transports,
      `UnityWebRequest`-based, queue + backoff + batch; thin additive emission hook on the locked runner; consent-gated.
- [ ] **B4** `analytics-events-ingest` validates + persists to `analytics_events` under RLS; reject paths covered
      (Lovable).
- [ ] **B5** Portal readout table (Flow A + Flow B joined on `scenario_id`/`step_id`/`attempt_id`) + cohort rollups,
      with loading/empty/error states (Lovable).
- [ ] **B6** `lab_scenarios` definition-publish renders ALL steps in the portal, including unreached ones (Lovable).
- [ ] **B7** Localization module lives in DevKit; lab/analytics text keyed; **Greek + English** render via the
      build-baked pipeline on **both AR and VR** (AR gets localization for the first time). Cloud pipeline deferred.
- [ ] **B8** Typed `PatientVitals` component + 3D-binding model drives at least one real binding additively; patient
      state reads through the `IAgentStateSource` seam. Full digital-twin deferred.
- [ ] **B9** Synced steps publish guid-keyed completion facts from the runner lifecycle via the consumer-side bridge;
      two-client VR green (AnyCompletes + late-join); AR / no-Fusion trace identical to the Phase A golden; read-gate +
      state-budget validators live (warn 48 / fail 64); no DevKit public flow type. (SLIP-ELIGIBLE - a slip is
      dispositioned in the log.)
- [ ] **End-to-end** in ONE real test lab: author -> mark Actions -> run on **VR (Quest)** and **AR (mobile UaaL)** ->
      events ingest -> instructor sees the readout. **Green on both surfaces from the same JSON.**
- [ ] **Net** every Phase B change passed "DevKit > Evaluate Changes."
- [ ] No emoji / mojibake in shipped strings or tooltips (carried from Phase A WS A4 hygiene).

When this checklist is green, analytics + launch localization + the vitals foundation are launch-ready and Phase C
([2026-06-09-phase-c-integration-and-ship.md](2026-06-09-phase-c-integration-and-ship.md)) integrates them into the app
build behind the store-submission gate. The umbrella is
[2026-06-09-devkit-launch-plan.md](2026-06-09-devkit-launch-plan.md).

---

## Plan self-review (coverage check)

- [ ] Every WS (B1..B9) maps to a spec §/source: B1-B6 = §2-§5 + spec §8 (Runtime); B7 = spec §28.3 (Localization); B8 = spec §28.4 (Vitals); B9 = spec §28.2 (Networking; launch-minimal bridge - detail in the 2026-06-10 companion); the JSON-first seam = spec §28.5.
- [ ] The deterministic-FIRST / AI-judging-deferred principle is preserved (§1, §7).
- [ ] All current content carried forward: Action atom (§2.1), auto-errors (§2.2), per-action config (§2.3), scoring (§2.4), worked JSON (§2.5), JSON-projection (§3), two portal flows (§4), open model decisions (§6), deferred-post-launch list (§7).
- [ ] Two new workstreams added: B7 (Localization into DevKit; Greek+English build-baked; cloud pipeline deferred) + B8 (Vitals foundation; typed `PatientVitals` + `IAgentStateSource` seam; full twin deferred). Each has checkbox Steps + Acceptance + Gate.
- [ ] WS B9 added 2026-06-10 (Stergios review): launch-minimal step-sync bridge (consumer-side; rides B3 Step 4's emission; no DevKit public flow type); §7's Multiplayer-into-DevKit deferral reconciled (graduation stays after-launch unchanged); checkbox Steps + Acceptance + Gate present; existing B1-B8 content untouched.
- [ ] The single gate is the Phase A **WS A3 net** (renamed from WS-0); WS A8 `ISceneRunnerControl` seam (renamed from WS-5) is B3's dependency; WS A2 reserved the Localization + Vitals module slots; WS A4 hygiene carries the no-emoji/mojibake rule.
- [ ] `ControlOptionManager` + avatars are noted as out-of-scope VR scripts, NOT planned (per the §28.4 boundary).
- [ ] Cross-links resolve: umbrella + sibling Phase A/C + the after-launch plan (`_after-launch/2026-06-09-after-launch-plan.md`) + the spec. Post-launch references point to the after-launch plan, citing the archived roadmap as its source.
- [ ] Real "§" characters used throughout; no emoji, no mojibake.

---

## Execution handoff

**Executors.** WS B1/B2/B3 + B7 + B8 = Alexandros (DevKit runtime + SDK + Localization + Vitals modules, DevKit repo).
WS B4/B5/B6 = Lovable (cloud ingest + portal, Web Portal repo). The DevKit workspace does not edit the Web Portal repo;
it freezes the emit shape (B3) and the definition-publish shape (B6 hook) and hands them across. WS B9 = Stergios
(VR-consumer-side bridge + guid-keyed listeners, HealthOn VR repo; rides B3's DevKit completion emission).

**Ratification path.** Status = **RATIFIED by Petros 2026-06-10, pending Stergios sign-off**. This plan is board-staged and does not dispatch until the
review chain clears: **Petros + Petros's Claude + LooPi** triage first, then **Heisenberg / Stergios** sign the
architecture, then slice tickets dispatch per the roadmap §0.5 owner matrix. The Open model decisions (§6) must be
resolved before WS B1 freezes the Action schema.

**Edit discipline.** Local-only edits; **Petros runs git** (agents do not `git add` / `commit` / `push`). Stage the
diff on disk; Petros reviews and pushes.

---

## Status & Progress Log

> Update on EVERY WS start/close + every Evaluate-Changes green run on a milestone. Newest first. This is the
> at-a-glance progress view; the per-WS checkboxes are the detail.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-10 | B9 | WS B9 added at Stergios review: launch-minimal multiplayer step-sync bridge (guid-keyed completion facts from the runner lifecycle onto the existing consumer-side NetworkStateManager; UIStateTrigger wiring retired from synced steps; AR/no-Fusion = compiled-out no-op; read-gate + state-budget validators, warn 48 / fail 64). SLIP-ELIGIBLE; rides B3 Step 4's emission (one raise, two consumers); §7's Multiplayer-into-DevKit graduation stays after-launch unchanged. Detail: 2026-06-10-phase-b-multiplayer-step-sync.md. Existing B1-B8 content untouched. Pending Petros ratification of the addition | Stergios (Claude) |
| 2026-06-10 | - | Window compressed to Jul-01 -> ~Jul-15 DevKit-side (Petros); consent UI/state added as [HUMAN] WS B4 Step 4 (surfaces own the UI, DevKit reads state); B6 Step 1b ScenarioV1-lineage added; completion discipline added | Claude (board) |
| 2026-06-09 | - | B6 marked MANDATORY for launch labs; B8 marked SLIP-ELIGIBLE (Petros); Lovable dated gates proposed (08-05 / 08-12 / 08-15); filed as PROPOSED (since RATIFIED 2026-06-10) | Claude (board) |
