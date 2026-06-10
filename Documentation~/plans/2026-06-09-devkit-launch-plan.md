---
title: "DevKit -> Launch (Sep-10): Architecture + Phasing"
status: RATIFIED by Petros (board) 2026-06-10 - pending Stergios final review; dispatch on his sign-off
date: 2026-06-09
owner: Petros (CEO/CTO) + Claude (board)
review_chain:
  - Petros
  - Petros's Claude
  - LooPi
  - Heisenberg (CTO)
  - Stergios
authors:
  - Claude (board)
supersedes: launch framing of 2026-05-08-p2-behavior-roadmap.md (now ARCHIVED + consolidated into _after-launch/2026-06-09-after-launch-plan.md; only its implicit launch sequencing was superseded by this set, the rest folded into the after-launch plan)
references:
  - 2026-06-09-phase-a-refactor-and-foundation.md
  - 2026-06-09-phase-b-analytics.md
  - 2026-06-09-phase-c-integration-and-ship.md
  - _after-launch/2026-06-09-after-launch-plan.md
  - _archive/2026-04-23-p1-foundation.md
  - _archive/2026-05-08-p2-behavior-roadmap.md
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md
hard_constants:
  launch: 2026-09-10 (controlled commercial B2B; 5 paid beta universities day 1; no public signup)
  store_gate: 2026-08-15 (PIT-369 Apple TestFlight + Google Play internal track IN; Meta Horizon submitted 2026-08-13; hard, no slip - Petros 2026-06-10)
  devkit_lock: 2026-09-07
---

# DevKit -> Launch (Sep-10): Architecture + Phasing

> **What this is.** The single picture for getting the DevKit (and the labs it produces) to the
> 2026-09-10 controlled commercial B2B launch. It frames three launch phases — **A / B / C** — sets
> the launch architecture, parks the post-launch architecture, and pins the calendar against the one
> binding constraint: the **2026-08-15 store-submission gate (PIT-369)**. This doc holds the *picture*;
> the three Phase docs hold the *detail*.

---

## 1. Purpose + how to read this set

**This doc (umbrella/index).** The single picture: the three launch phases at a glance, the five board
refinements that shape the phasing, the launch dataflow (VR lab -> cloud -> portal), the post-launch
architecture we are deliberately *not* building before Sep-10, the contract-coherence call, the calendar,
and the cross-surface ownership. Read this first to know where everything sits.

**The three Phase docs hold the detail.** Each is the executable plan for its phase and the place to look
before opening a PR on any item:

- [`2026-06-09-phase-a-refactor-and-foundation.md`](2026-06-09-phase-a-refactor-and-foundation.md) — **Phase A** = behaviour-neutral professionalization + seams. **SELF-CONTAINED single source of truth** — Stergios' FINAL plan (2026-06-08) is fully MERGED into it (census = its Appendix A; the WS A3 ticket set = its Appendix I). Owner: Stergios & Alexandros.
- [`2026-06-09-phase-b-analytics.md`](2026-06-09-phase-b-analytics.md) — **Phase B** = analytics + localization (Greek+English keyed) + vitals foundation (the first behaviour-additive work). Owner: Stergios & Alexandros & Petros & Alex (+ Lovable + Web Portal).
- [`2026-06-09-phase-c-integration-and-ship.md`](2026-06-09-phase-c-integration-and-ship.md) — **Phase C** = integrate + ship: app builds (UaaL + VR Shell + Addressables), PIT-369 store submission, lab cutover, DevKit v1.0 lock. Owner: Alexandros & Stergios & Phoebos (+ Lovable, Cursor).

**Status of this set.** **RATIFIED by Petros (board) 2026-06-10 — pending Stergios' final review; dispatch on his
sign-off.** Phase A's WS bodies carry Stergios' FINAL detail (2026-06-08), authorship preserved; his original text is
archived at [`_archive/2026-06-08-p1-stergios-final.md`](_archive/2026-06-08-p1-stergios-final.md).

### Ratified Decisions (Petros, 2026-06-10) — no longer open

| # | Decision | Where it binds |
|---|---|---|
| 1 | **Dates (Workspace = plan-of-record):** A = Jun-02→Jun-27 (In Progress) · B = Jun-20→Jul-14 (starts on WS A3, overlapping A) · C = Jul-15→Sep-10 (core integration Jul-15→31; launch-readiness tail Aug-01→Sep-10) | all phase docs |
| 2 | **Gates:** G2 emit/consent/analytics contract freeze **Jun-29** · SDK emit-API freeze **Jul-07** · G3a integration contract **Jul-15** · Horizon submission **Aug-06→13** · Apple/Play **≤ Aug-15** · version-pin cutline **Aug-15** · G3b launch lock **Sep-07** · smoke **Sep-05→08** | Phase B/C |
| 3 | **Binary freeze ≠ launch lock:** anything needing a NEW store binary freezes before its submission (VR/Horizon ~Aug-05, mobile ~Aug-12); **Sep-07 G3b = the 0.x launch-baseline tag** (version pinning + docs/content lock — NOT package v1.0.0, which is Phase I; spec §28.7b) | Phase C WS C4 |
| 4 | **Two sinks, one source — RESOLVED:** `AnalyticsEventV1` = launch analytics; `AgentObservationV1` = post-launch Vicky observation (Phase G). Only the Web-Portal §4-sentence repair remains (Lovable/Heisenberg lane) | §6 |
| 5 | **Analytics model defaults:** strict-order Actions · one row = one Action · full §2.3 config shipped but default-off · Greek+English only at launch · B6 `lab_scenarios` publish MANDATORY for launch labs with **ScenarioV1 lineage** | Phase B |
| 6 | **B8 Vitals = slip-eligible** (foundation, not launch-DoD) · **C5 = the slip sink** (cutline ~Aug-10: un-landed C5/A-tail work defers unless it unblocks C4) | Phase B/C |
| 7 | **Consent:** org-level analytics consent at university enrollment; any per-user UI in the surfaces (VR Shell + mobile), DevKit reads host state — [HUMAN] step WS B4 | Phase B |
| 8 | **Package-metadata cutover (unity floor + deps block) = Phase D, post-launch** — no resolution-affecting metadata change inside the store window | Phase A §3 / after-launch |
| 9 | **`AgentActionRequestV1`** (gates Phase H) lives VICKY-side under the **World-Aware Agent Bridge** workstream, post-launch | after-launch Phase H |
| 10 | **Launch lab set:** owned by the VR team (they know the set); not enumerated in these docs. **Authoritative artifact = the Workspace** (HealthOn XR project: the AR LABS + VR SHELL epics' lab tasks + "AR: new Addressables content for updated DevKit" Jul-15->Aug-12); Phase C WS C2/C6 gate against that list | Phase C WS C2 |

> **Terminology — the numbered phases are RETIRED (Petros 2026-06-10).** One lettered sequence, end to end:
> **Phase A -> B -> C (launch) -> Phases D -> E -> F -> G -> H -> I (after-launch plan)**. "DevKit P1" = Phase A
> (fully merged into the Phase A doc); the old "arch-P2..P7" = Phases D..I (fully consolidated into the after-launch
> plan). The architecture spec's §17 still numbers P1..P7 internally — the mapping is fixed in spec §28.7
> (A=P1, D=P2, E=P3, F=P4, G=P5, H=P6, I=P7). Never write a bare "P1/P2/P3" in plans.

**Phase docs use the foundation format (WS + checkboxes + Status & Progress Log).** The phase docs are
**trackable implementation plans** — numbered **workstreams** (`WS A1..A8`, `WS B1..B8`, `WS C1..C6`) with
`- [ ]` checkbox steps, and a **Status & Progress Log** updated on every WS start/close, modeled on the
archived foundation plan ([`_archive/2026-04-23-p1-foundation.md`](_archive/2026-04-23-p1-foundation.md)).
**This umbrella stays a prose index** — read it for the picture, then drop into the relevant `WS` to execute.
**Completion discipline (Petros, 2026-06-10):** every phase completes **in full — every small step ticked, none
skipped**; steps tagged **[HUMAN]** are human-owned, and AI agents must actively remind the human owner of any
unticked [HUMAN] step (the Phase B consent UI/state step is the canonical example).

### Folder structure (final layout)

```
Documentation~/plans/
  2026-06-09-devkit-launch-plan.md            <- THIS umbrella / index (prose)
  2026-06-09-phase-a-refactor-and-foundation.md   (WS A1..A8 — behaviour-neutral; SELF-CONTAINED: census + net ticket set inside)
  2026-06-09-phase-b-analytics.md                 (WS B1..B8 — analytics + localization + vitals foundation)
  2026-06-09-phase-c-integration-and-ship.md      (WS C1..C6 — integrate + store + ship)
  _after-launch/
    2026-06-09-after-launch-plan.md           <- POST-LAUNCH (Phases D..I + the four §28 domain systems)
  _archive/
    2026-06-08-p1-stergios-final.md           (Stergios' original FINAL text — provenance; merged into Phase A)
    2026-04-23-p1-foundation.md               (the OLD foundation plan — superseded by Phase A)
    2026-05-08-p2-behavior-roadmap.md         (prior post-launch roadmap — consolidated into the after-launch plan)
    2026-05-08-p2-behavior-roadmap-slice-p5-substrate-observer-summary.md
Documentation~/specs/
  2026-04-23-devkit-1.0-target-architecture-design.md   (incl. §28 addendum: domain & content systems)
```

The post-launch roadmap (`2026-05-08-p2-behavior-roadmap.md`) is now **archived** and **consolidated**
into [`_after-launch/2026-06-09-after-launch-plan.md`](_after-launch/2026-06-09-after-launch-plan.md);
point all post-launch references at the after-launch plan (it cites the archived roadmap as its source).

---

## 2. The three launch phases at a glance

> **Terminology — one lettered sequence.** Launch = **Phases A / B / C**; after launch the letters continue:
> **Phase D** (runtime foundation + runner extraction), **Phase E** (LabConsole + multiplayer), **Phase F**
> (authoring/objectives + content systems), **Phase G** (Vicky Observer), **Phase H** (Vicky Interactive + the
> Director foundation), **Phase I** (v1.0 API lock) — detail in the after-launch plan. The architecture spec's §17
> internal numbering maps via §28.7. Never use bare "P1/P2/P3" for any phase.

| Phase | What | Target window | Gate / dependency | Owner |
|---|---|---|---|---|
| **A — Refactor + Foundation** | Stergios' Phase A behaviour-neutral professionalization (phase-doc `WS A1..A8`; execution order = census -> editor surfaces + Hub + **reserve module slots: Networking / Localization / Analytics / Vitals** -> the net -> hygiene -> splits -> docs -> `ISceneRunnerControl`) **plus** the behaviour-neutral analytics **seams only** (step-fact vocabulary consts, reserved module slots, `ISceneRunnerControl`, the safety net). **No emission, no ledger, no serialized analytics config.** | Jun-02 -> Jun-27 (Workspace plan-of-record; In Progress) | **The EditMode safety net (`WS A3`, the old `WS-0`) is the gate** — it must land first; the A4..A7 tail may trail (CAN_TRAIL). | Stergios & Alexandros |
| **B — Analytics + Localization + Vitals foundation** | First behaviour-**additive** work. **Analytics:** Action/backend step taxonomy; auto-errors; scoring; per-action config; AI-authorable JSON projection (+ Binding Sheet for stable scene-ref ids; the `[SerializeReference]` scenario stays the runtime source of truth, full migration post-launch); thin additive emission hook on the still-locked runner -> `AnalyticsApi.Emit(AnalyticsEventV1)`; DevKit Analytics SDK (C# port). **Localization (NEW, into DevKit):** move the VR localization pipeline into a DevKit Localization module; key the lab/analytics text; ship **Greek + English** build-baked (AR gets localization for the first time) — cloud content pipeline deferred (§28.3). **Vitals foundation (NEW):** typed `PatientVitals` component + 3D-binding model + `IAgentStateSource` seam, additive alongside existing scattered logic; full digital-twin deferred (§28.4). Cloud ingest + `analytics_events` DDL + portal dashboards = Lovable lane. | **Jun-20 -> Jul-14** (Workspace plan-of-record; G2 contract freeze Jun-29; emit-API freeze Jul-07; Lovable cloud/portal legs by their dated gates) | **Starts once WS A3 (the net) exists** (not after all of A). Rides the thin emission hook — **does NOT crack `SceneManager`.** Cloud/portal legs depend on `analytics-events-ingest` + DDL (Lovable). | Stergios & Alexandros & Petros & Alex (+ Lovable + Web Portal) |
| **C — Integrate + Ship** | App builds (UaaL Android + VR Shell + Addressables) green; **PIT-369 store submission** (Apple TestFlight + Google Play internal track); lab JSON round-trip cutover gated on the net; author Action/analytics config + **localization keys (Greek + English) per lab**; VICKY brand swap touchpoints in DevKit-produced assets; DevKit **v1.0 lock 2026-09-07**. | **~Jul-15 -> Sep-10** (starts when Phase B DevKit-side completes; AR/VR lab updates + Phase C bug-work run together — Petros 2026-06-10) | **Store-submission gate = 2026-08-15 (hard, no slip; Meta Horizon submitted 2026-08-13).** Runs in **parallel with / ahead of** the Phase A tail and DevKit polish. | Alexandros & Stergios & Phoebos (+ Lovable, Cursor) |
| **POST-LAUNCH (after Sep-10)** | Phase D runner extraction -> Phase E LabConsole -> Phase F objective evaluator -> Phase G AgentObservation / VICKY observer -> Phase H VICKY actuation -> Phase I v1.0 API lock (plus the full JSON-source-of-truth migration) **plus the four §28 domain systems** (multiplayer-into-DevKit full, localization cloud pipeline, vitals digital twin, AI-assisted authoring command library). | Sep-10 -> | Off the launch critical path by definition. Detail in [`_after-launch/2026-06-09-after-launch-plan.md`](_after-launch/2026-06-09-after-launch-plan.md) + spec §8 (Runtime) + §28 (domain & content systems). | DevKit lead + VICKY |

---

## 3. The 5 board refinements (why the phasing is shaped this way)

These are the board's 2026-06-09 refinements; they are the *reasons* the phases are cut the way §2 shows.

1. **The store submission is a HARD late-August gate, not the tail of Phase C.** The app builds (UaaL +
   VR Shell + Addressables) and **PIT-369** submission must run **in parallel with / ahead of** DevKit
   polish. Missing the Apple/Google review window = **no launch**. This is the binding constraint on the
   entire plan; everything else bends around it.
2. **Phase A stays behaviour-neutral.** Do **not** bolt the analytics *runtime* into Phase A. The
   "foundation for analytics" allowed in A is **behaviour-neutral SEAMS ONLY**: the step-fact vocabulary
   consts (Stergios plan §H#6 — consts-only, **no emission**), the reserved analytics module slot
   (WS-6 recipe), `ISceneRunnerControl` (WS-5), and the WS-0 net itself. **Emission, serialized analytics
   config, and any ledger are Phase B.** Bolting analytics into A breaks the equivalence-proof discipline
   *and* the migration net that Phase C's JSON cutover depends on. (Stergios' plan "Traps" forbids exactly
   this in Phase A.)
3. **Phase A need not finish before Phase B — only WS-0 must.** Land **WS-0 first as the gate**; Phase B
   analytics starts once the net exists, while the rest of Phase A's behaviour-neutral splits/hygiene
   proceed **in parallel**. They are disjoint: analytics lives in its own module; the splits touch
   `Scenario.cs` / `SceneManager`. Serializing all of A before B will not fit the calendar.
4. **Launch analytics does NOT require cracking `SceneManager`.** It rides a **thin additive emission
   hook** on the still-locked runner (step enter / complete / error -> `AnalyticsApi`) plus the manual
   `ScenarioGraphContextHelper`. The full runner extraction (Phase D) and LabConsole (Phase E) are
   **post-launch.**
5. **The launch-critical core of Phase A is WS-0 + WS-6 + the seams.** The deep file-splits/hygiene tail
   (WS-1 / WS-2 / WS-3 / WS-4) is a quality investment that must **not compete with the store deadline** —
   let it run in the background, or **slip it** if the calendar tightens. Protect the core; the tail is
   negotiable.

---

## 4. Launch architecture — the dataflow

The launch shipping path is a **thin additive emission hook** on the runner, not a re-architecture.

```
VR LAB (Unity / Quest standalone)
  Scenario runner (SceneManager today)
        |
        v
  Action-tracker  (NEW, Phase B: time + errors-auto + score + weight)
        |
        v
  AnalyticsApi.Emit(AnalyticsEventV1)        [consent-gated]
        |
        v
  DirectCloudQueuedTelemetryService          (UnityWebRequest; queue + backoff + batch; AOT-safe)
        |
        v  HTTPS POST
  CLOUD  analytics-events-ingest  (edge fn: validate uuid / size / surface / consent / Art-12)
        |
        +--> analytics_events   (tenant-scoped, RLS)            <- ATTEMPT ANALYTICS (the numbers)
        +--> lab_scenarios      (published scenario JSON)       <- LAB DEFINITION   (the row list)
        |
        v
  WEB PORTAL dashboard = lab definition (all steps)  +  attempt events (time / errors / score)
```

**AR (mobile UaaL) leg.** Same `AnalyticsEventV1`, different transport: `BridgeQueuedTelemetryService`
-> mobile bridge -> `telemetry-queue.service.ts` -> cloud. Same edge fn, same tables.

**The TWO portal data flows (why the portal can show ALL steps, even unreached ones):**

- **(A) LAB DEFINITION** — the scenario **JSON published once per lab version** (the full ordered step
  list + weights). The portal renders structure even for steps a learner never reached.
- **(B) ATTEMPT ANALYTICS** — per-session `AnalyticsEventV1` events (time / errors / score per step).

The readout = **A (rows) + B (numbers)**, joined on `scenario_id` / `step_id` / `attempt_id`.
`AnalyticsEventV1` rev-3 already carries `scenario_id` / `step_id` / `action_id` / `step_state` /
`performance_metric` / `attempt_number` / `semantic_state`.

**Readout rule (the authoring model):** *one row = one ACTION.* Backend steps (timeline / event /
condition) auto-advance and are invisible in the readout. "Enabling analytics on a step" = marking it an
Action. (Full model in the Phase B doc.)

> **Thin emission hook, NOT runner extraction.** The launch path adds a hook to the *locked* runner.
> Extracting the runner (Phase D) and putting LabConsole in front (Phase E) are post-launch — see §5.

---

## 5. Post-launch architecture — SceneManager -> runner + LabConsole

`SceneManager` today **fuses** a RUNNER (graph interpreter) **and** a CONTROL surface. Post-launch it
**splits**:

- the **runner is extracted** and kept underneath (Phase D);
- **LabConsole** sits **in front** as the control plane — semantic state + parameters + professor
  controls + the only gated outside-in write path (Phase E).

**The central hub is the `LabEventBus`, NOT LabConsole.** LabConsole, Analytics/Telemetry, and Observation
are all **clients of the bus** (siblings):

- **VICKY ACTS** through LabConsole (gated).
- **VICKY OBSERVES** by reading LabConsole's semantic state.
- **ANALYTICS is a SEPARATE tap on the bus** — *not* routed through LabConsole — and must stay decoupled,
  because **analytics ships at launch while LabConsole is post-launch.** This decoupling is the whole
  reason the launch emission hook (§4) can exist without LabConsole.

`SceneManager`-the-component survives as a **thin facade** through the transition, then an **OFFERED**
migration converts labs to LabConsole-native at the 1.0 lock. **Migration is never forced.**

Detail: spec **§8 (Layer 2 - Runtime)** ([`../specs/2026-04-23-devkit-1.0-target-architecture-design.md`](../specs/2026-04-23-devkit-1.0-target-architecture-design.md)) -
`ScenarioRunner` / `LabConsoleRuntime` / `IStepRunner` - plus the **after-launch plan**
([`_after-launch/2026-06-09-after-launch-plan.md`](_after-launch/2026-06-09-after-launch-plan.md)),
which consolidates the now-archived post-launch roadmap
([`_archive/2026-05-08-p2-behavior-roadmap.md`](_archive/2026-05-08-p2-behavior-roadmap.md)) and the four
§28 domain systems. The phase-by-phase SceneManager -> LabConsole *transition narrative* lives in Stergios'
the Phase A doc's Appendix B (with Stergios' original text archived at `_archive/2026-06-08-p1-stergios-final.md`).

---

## 5b. Domain & content systems (the four §28 decisions)

Four domain systems were settled by the board on 2026-06-09 and written into spec **§28** (the
addendum: *domain & content systems*). Each has a **launch foundation** (a seam or a thin slice that
ships in Phase A/B) and a **post-launch full build** (the after-launch plan). The unifying principle
(§28.5): every artifact is **typed data referenced by stable id** — schema'd JSON + a deterministic
editor apply-command + a headless entry point — so Claude drafts, a dev applies + reviews in the visual
tool, and VICKY is the translation engine.

| Domain system | Decision | Launch foundation (this set) | Full build (after-launch) |
|---|---|---|---|
| **Multiplayer in DevKit** (§28.2) | The HealthOn VR `NetworkedStates` (Fusion 2 Shared-Mode fact store) + its editor automations **move into a DevKit Networking module**, wrapped behind `IScenarioFlowStore`. Only VR-Shell-specific scripts stay outside DevKit. | **Phase A (WS A2) RESERVES the Networking module slot only.** No move yet. | Full move + flow-store wrap + runner-fact-driven late-join fast-forward + Make-Multiplayer authoring (rides Phases D/E). |
| **Localization cloud moat** (§28.3) | The VR localization pipeline (GlobalObjectId-keyed manifest + medical LLM-translate prompt) **moves into a DevKit Localization module.** | **Phase B (WS B7):** key the lab/analytics text + ship **Greek + English** build-baked. **AR gets localization for the first time.** | The cloud content pipeline (Web Portal editor + VICKY translate + ElevenLabs audio + runtime fetch-by-language) — the **EU-scale moat**. |
| **Vitals digital twin** (§28.4) | A typed `PatientVitals` component — a Vital = a typed `ConsoleParameter` + a 3D binding — that implements `IAgentStateSource` (VICKY reads patient state). | **Phase B (WS B8):** the typed **foundation** (component + binding model + `IAgentStateSource` seam), additive alongside existing scattered logic. | Full digital twin: cascade rules + ScriptableObject profiles + scene migration + `ControlOptionManager`-off-PUN convergence (rides Phase E LabConsole param). |
| **AI-assisted authoring** (§28.5) | Every artifact = schema'd JSON + deterministic apply-command + headless entry; the **seam is the JSON-first architecture already chosen** across analytics / localization / vitals. | **Already in the seam** — Phase B's AI-authorable JSON projection is the first instance. | The Hub apply-command library + eventual CLI/MCP bridge (Claude drafts, dev applies; VICKY translates). |

> **Out of scope (do not plan):** `ControlOptionManager` + avatars stay as **VR scripts** for now — not
> moved into DevKit, not planned in this set or the after-launch plan beyond the vitals/`ControlOptionManager`
> convergence note above.

Full sequencing — which domain system rides which arch-phase, plus the cross-area VICKY dependencies and
the behaviour-change-boundary governance — is in the **after-launch plan**
([`_after-launch/2026-06-09-after-launch-plan.md`](_after-launch/2026-06-09-after-launch-plan.md), Part 2)
and **spec §28**.

---

## 6. Contract coherence — two sinks, one source

Two cloud contracts both touch "observe the lab." They must stay distinct:

| Contract | Sink (edge fn) | Purpose | Status | Timing |
|---|---|---|---|---|
| **`AnalyticsEventV1`** | `analytics-events-ingest` | Instructor dashboards / rollups — **the launch analytics path** | Active launch target (Phase B) | **Launch** |
| **`AgentObservationV1`** | `agent-observation` | VICKY semantic observation (real-time observe / Tier-3 read-path) | Built **transport half only** (LooPi); producer unbuilt; consent **C3-locked** | **Post-launch** |

**RESOLVED (Petros, 2026-06-10 — Ratified Decision #4):** they stay **TWO SINKS on the SAME source
events** — `AgentObservationV1` = VICKY's post-launch real-time observe / Tier-3 read path; `AnalyticsEventV1`
= the launch instructor-dashboard rollups. **PARK `AgentObservation` + LabConsole until post-launch.**

**Known defect to repair (not from this workspace):** the analytics-SDK plan §4 wrongly says the substrate
observer should emit through `AnalyticsApi.Emit()`, which contradicts the as-built `AgentObservation` path.
Repairing that §4 sentence + reconciling the DevKit spec is a **Web-Portal-repo edit = Lovable / Heisenberg
lane**, **NOT** done from the DevKit workspace. Tracked here as an open item; see Phase B "deferred."

---

## 7. Calendar + critical path

| Window | Phase | Must be true by the end |
|---|---|---|
| **June -> early July** | **A** | **WS-0 net green first** (the gate); WS-6 Hub + seams landed; behaviour-neutral splits/hygiene tail in flight (may trail into July). |
| **Jul-01 -> ~Jul-15** | **B** | Action taxonomy + auto-errors + scoring; thin emission hook live; DevKit Analytics SDK shipped (DevKit-side done ~Jul-15; Lovable legs by 08-05/08-12 gates). |
| **~Jul-15 -> Sep-10** | **C** | AR + VR labs updated on the new DevKit (+ UaaL, VR Shell) with bug-work in parallel; **store submissions IN by 2026-08-15 (Horizon 2026-08-13)**; lab JSON cutover gated on the WS A3 net; DevKit **v1.0 lock 2026-09-07**; 5 universities onboarding-ready. |

**The binding constraint:** **store submission, 2026-08-15** (PIT-369 — Apple TestFlight + Google Play
internal track). Review windows must clear before Sep-10. **No slip room.**

**Honest read of the calendar.** Today 2026-06-09 is **~13 weeks to launch, ~11 to the store gate.** The
calendar is **tight.** **Parallelism is the mitigation, not a luxury:**

- **Protect WS-0 + WS-6 + the seams** — these are the launch-critical core of Phase A.
- **Let the Phase A tail (WS-1/2/3/4) slip if needed** — it is a quality investment, not a launch blocker
  (refinement 5).
- **Run Phase C app-build + store work in parallel with / ahead of DevKit polish** (refinement 1) — do not
  serialize it behind the Phase A tail.
- **Phase B starts on WS-0, not on "all of A"** (refinement 3).

If something has to give, it is the deep-refactor tail — **never** the store gate.

---

## 8. DevKit north star — usability / efficiency / scalability

Petros' north star for the DevKit: **USABILITY** (great editor UI), **EFFICIENCY**, **SCALABILITY**. Where
each lands across the phases:

| North star | Where it lands | What delivers it |
|---|---|---|
| **Usability** (great editor UI) | **Phase A (WS-6)** + **Phase B** | WS-6 "DevKit Hub" cockpit rebuild (task-first pages Setup/Author/Deliver/Maintain/Reference; single "Pi tech" menu root; mojibake fix). Phase B: author the **happy path only** — auto-errors mean **no error matrix to enumerate**; minimum to author an action = title + target + weight. |
| **Efficiency** | **Phase B** | an **AI-authorable JSON projection** (the existing `[SerializeReference]` scenario stays the runtime source of truth at launch; full JSON-source-of-truth migration is post-launch); stable string scene-ref ids via the **Binding Sheet** (human drags once per scene); **same lab JSON runs in AR + VR** if both scenes bind the same ids (cross-surface reuse). |
| **Scalability** | **Phase A (WS-0)** + **Phase B transport** | WS-0 net = the equivalence-proof harness that lets the codebase grow without silent regressions. Phase B transport = offline queue + backoff + batch, tenant-scoped RLS ingest — scales across 5 universities' cohorts at launch and more after. |

---

## 9. Cross-surface dependencies + owners

| Surface | Owner | Launch responsibility |
|---|---|---|
| **DevKit — refactor + seams (Phase A)** | **Stergios & Alexandros** | behaviour-neutral professionalization (the merged, self-contained Phase A doc) + analytics seams (consts / reserved slots / `ISceneRunnerControl` / the WS A3 net). |
| **DevKit — analytics SDK + runner hook (Phase B)** | **Stergios & Alexandros (+ Petros, Alex)** | Step taxonomy, auto-errors, scoring, JSON projection + Binding Sheet, thin emission hook, C# port of `AnalyticsEventV1` into `Runtime/ContentDelivery/Analytics/V1/`. |
| **Cloud + Portal** | **Lovable** | `analytics-events-ingest` edge fn, `analytics_events` DDL, `lab_scenarios` publish, portal dashboards. Also owns the analytics-SDK §4-sentence repair + DevKit-spec reconciliation (§6). |
| **Mobile / AR (UaaL)** | **Cursor** | `BridgeQueuedTelemetryService` -> mobile bridge -> `telemetry-queue.service.ts`; "view cast" entry point if the cross-surface contract needs it. |
| **VR / AR lab content** | **Phoebos / Georgia** | Authoring labs against the new analytics model; tagging Actions + critical interactables; Binding Sheet population per scene. |
| **Store submission (PIT-369)** | **Alex / Alexandros** | App builds (UaaL + VR Shell + Addressables) + Apple TestFlight + Google Play internal track by **2026-08-15** (note: clashes Aug store gate vs Petros-out window — Alex absorbs the submission lead). |
| **Board** | **Claude (board)** | This set's framing, cross-repo coordination, contract-coherence call, plan-flow staging. |
| **CTO** | **Heisenberg** | Architecture sign-off on the post-launch split (§5), the contract-coherence ratification (§6), Phase A equivalence-proof discipline. |

---

## 10. Index / links

**Launch phases (this set):**

- [`2026-06-09-phase-a-refactor-and-foundation.md`](2026-06-09-phase-a-refactor-and-foundation.md) — Phase A (self-contained: merged Stergios FINAL + board addendum + census + net ticket set).
- [`2026-06-09-phase-b-analytics.md`](2026-06-09-phase-b-analytics.md) — Phase B analytics.
- [`2026-06-09-phase-c-integration-and-ship.md`](2026-06-09-phase-c-integration-and-ship.md) — Phase C integrate + ship.

**Post-launch + architecture:**

- [`_after-launch/2026-06-09-after-launch-plan.md`](_after-launch/2026-06-09-after-launch-plan.md) — the **POST-LAUNCH plan**: Phases D..I (consolidated from the archived roadmap) + the four §28 domain systems (multiplayer-into-DevKit full, localization cloud pipeline, vitals digital twin, AI-assisted authoring).
- [`../specs/2026-04-23-devkit-1.0-target-architecture-design.md`](../specs/2026-04-23-devkit-1.0-target-architecture-design.md) — DevKit 1.0 target architecture (incl. **§8 Layer 2 - Runtime**: ScenarioRunner / LabConsoleRuntime; and **§28 addendum**: domain & content systems).

**Archived (superseded):**

- [`_archive/2026-04-23-p1-foundation.md`](_archive/2026-04-23-p1-foundation.md) — the OLD foundation plan, superseded by Phase A; history only.
- [`_archive/2026-06-08-p1-stergios-final.md`](_archive/2026-06-08-p1-stergios-final.md) — Stergios' original FINAL text, fully merged into the Phase A doc; kept for provenance.
- [`_archive/2026-05-08-p2-behavior-roadmap.md`](_archive/2026-05-08-p2-behavior-roadmap.md) — prior post-launch Phases D..I roadmap, **consolidated into the after-launch plan** (cited there as its source).
- [`_archive/2026-05-08-p2-behavior-roadmap-slice-p5-substrate-observer-summary.md`](_archive/2026-05-08-p2-behavior-roadmap-slice-p5-substrate-observer-summary.md) — Phase G substrate-observer slice summary.
