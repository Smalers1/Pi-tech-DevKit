---
title: Phase B.2 - Features (behaviour-additive) - analytics, multiplayer turn-on, localization, vitals
status: READY to dispatch 2026-06-26 (derived from the architecture map; builds on the B.1 foundation). Pending final sign-off.
date: 2026-06-26
author: Claude Code
owner: Claude Code (DevKit repo + the Web Portal / cloud lane)
phase: B.2
gate: EditMode "DevKit > Evaluate Changes" net (Proofs A/B/C) on every change + the dev-playtest checklist (single-player) + on-device 2-client proofs (multiplayer) + the analytics equivalence golden fixture (events -> grade, identical in Unity and the portal)
references:
  - devkit-architecture-map-phase-b.md (the architecture map = SINGLE SOURCE OF TRUTH; decision log 2026-06-19 -> 2026-06-26b)
  - devkit-phase-b1-structural-implementation-plan.md (B.1 - the behaviour-neutral foundation this builds on; the frozen schema + the DRIVE/FOLLOW table)
  - ../plans/2026-06-09-phase-c-integration-and-ship.md (Phase C - roll into AR/VR + ship)
  - ../plans/_archive/2026-06-09-phase-b-analytics.md (SUPERSEDED - mined only for: cloud-lane structure, queued transports, consent state, exit-criteria format)
companion: devkit-phase-b1-structural-implementation-plan.md
---

# Phase B.2 - Features (behaviour-additive)

> **For implementers (Claude Code):** implement WS-by-WS. Steps use `- [ ]` checkbox syntax; add a row to the **Status &
> Progress Log** (bottom) on every WS start/close and every milestone. This doc is the EXECUTION projection of the
> architecture map's **B.2 column** (map §13) + decision log through **2026-06-26b**. **Authority order: code > map > this
> plan.** B.2 turns ON the behaviour that B.1 laid inert - every WS depends on a B.1 deliverable.
>
> **Completion discipline: every WS completes IN FULL - every step ticked, none skipped.** Steps tagged **[HUMAN]** require a
> physical action Claude Code cannot perform (on-device headset runs, the host-app consent UI, git) - surface them, never
> silently pass over them, and do not declare a WS done while one is open. **Edit discipline: local-only edits; the user runs git.**

**Goal.** Ship the launch features on the B.1 foundation: (1) the **analytics behaviour** - metric observers, the ratified
grade math, the **on-device scoring/readout engine** (lab-end results with no cloud round-trip), the role-gated emitter, and
the **one self-contained session report** shipped at `SessionStop`; (2) the **subjects-registry runtime** (drops /
wrong-interaction / order) auto-detected + auto-wired; (3) the **cloud + portal** rebuilt to the session-report schema;
(4) **multiplayer turn-on** (path-list FOLLOW + branch); (5) **Greek + English** localization content; (6) the **Vitals
foundation** (slip-eligible). End-to-end green in one real lab on AR + VR is the analytics exit bar.

**Architecture stance.** Behaviour-ADDITIVE, gated on the Evaluate-Changes net. Each feature rides a **B.1** seam (the bus,
the param store, the path-store, the frozen rubric schema) - turning it on, not re-architecting it. The launch grade is
**author-defined and reproducible** (no model in the scoring loop); AI-judging is post-launch. **Analytics is fully authored
in-lab via the DevKit at launch; Web-Portal tuning (pick from a dropdown; set weight + target) is post-launch** (decision 35).

**Authority / spec reference.** `devkit-architecture-map-phase-b.md` - §11 (the analytics hierarchy + the ratified §11.8
grade math + the report), §10 (multiplayer turn-on + the DRIVE/FOLLOW table), §12 (localization content), §8 (the `Vital`
primitive). B.1 provides the frozen schema (rubric types, `SessionStart/Stop`, roles, tenant+user id) every WS consumes.

**Duration / window.** B.2 runs after the B.1 surface freezes (**07-07**); the **cloud + portal lane runs in parallel from
the G2 schema freeze (06-29)**. On-device end-to-end smoke folds into the Phase C integration window before the store gate.

**Exit criteria (measurable).**
- End-to-end analytics green in ONE real lab on **VR (Quest) + AR (mobile UaaL)** from the same scenario: author the rubric -> run -> **on-device lab-end readout renders** -> session report ingests -> portal shows the readout.
- The **analytics equivalence golden fixture** (events -> grade) produces **identical** results in Unity (EditMode) and the portal reducer.
- Drops / wrong-interaction / order are captured from the subjects registry; the **default bands `none 0 / warning 0.5 / error 1.0`** apply; warnings/errors fire the in-scene notification.
- **Role gating** works: Participant = full graded report, Professor = presence only, Spectator = none; per-lab capacities enforced.
- An unfinished session stores as **incomplete** (never lost, never "passed"); offline events flush on reconnect.
- Multiplayer: two-client VR green (AnyCompletes + branch + late-join); AR / no-Fusion **trace-identical** to the Phase A golden.
- **Greek + English** render at runtime on AR + VR from the keyed source.
- Vitals (if not slipped): typed `PatientVitals` drives >=1 real 3D binding + reads through `IAgentStateSource`.
- Every change passed "DevKit > Evaluate Changes." No emoji / mojibake.

---

## Plan structure

| WS | Focus | Gate / depends-on | Source / map § |
|---|---|---|---|
| B2.1 | Analytics behaviour: metric observers + grade math + **on-device scoring/readout engine** + role-gated emitter + session report + offline outbox | B1.1 bus, B1.4 schema | map §11 |
| B2.2 | Subjects-registry runtime + auto-detect (Insert/Selection) + auto-wire grab/drop + `AnalyticsSignalEmitter` | B1.4 schema | map §11.2/§11.4 |
| B2.3 | Cloud + portal: session-report ingest + DDL/RLS + portal readout + consent state | G2 schema (06-29); B2.1 emit shape | map §11 (cross-surface) |
| B2.4 | Multiplayer turn-on: path-list FOLLOW + branch on + declared-param wiring | B1.3 seams | map §10 |
| B2.5 | Localization content: Greek + English authored on the keyed source | B1.5 infra | map §12 |
| B2.6 | Vitals FOUNDATION: typed `PatientVitals` (param-backed) + 3D bindings + `IAgentStateSource` | B1.2 params | map §8 |

> **WS tags.** **B2.1-B2.5 = LAUNCH_BLOCKER · B2.6 = CAN_TRAIL** (slip-eligible foundation - never blocks B2.1-B2.5,
> decision 41). B2.4's two-client on-device proof folds into the Phase C window. A tagged slip is **dispositioned in the
> Status & Progress Log - never silently skipped.**

> **Lane note.** B2.1 / B2.2 / B2.4 / B2.5 / B2.6 are **DevKit-repo**. **B2.3 is Web Portal / cloud-repo work** - the DevKit
> freezes the **session-report schema** (B2.1) and hands it across; the cloud rebuilds ingest/DDL/RLS/portal to it (this
> replaces the old per-event `AnalyticsEventV1` + Flow-A/B model). The consent **state** is supplied by the host app
> (VR Shell / mobile), read by the DevKit; the consent **UI** lives in the surfaces, not the DevKit.

> **Parallelism.** B2.3 (cloud) runs from the G2 freeze in parallel with B2.1/B2.2 (DevKit analytics). B2.4 (MP), B2.5 (loc
> content), B2.6 (vitals) are disjoint and run concurrently once their B.1 seam is in.

---

## The gate + the analytics equivalence fixture

1. **EditMode "DevKit > Evaluate Changes"** (Proofs A/B/C) on every serialized change.
2. **Dev-playtest checklist** (single-player) per lab.
3. **On-device 2-client proofs** (MP): AnyCompletes + branch + loop + late-join + authority-drop.
4. **Analytics equivalence golden fixture (NEW):** a shared `(events -> grade)` fixture run in **both** Unity EditMode and the
   portal reducer - they must produce the **identical** grade. This is what keeps the DevKit (canonical) and the cloud (mirror)
   reducers in lockstep (map §11.0/§11.3).

---

## WS B2.1 - Analytics behaviour (observers + grade + report)

**Goal.** Turn the B.1 rubric schema into a running analytics path: observers -> raw events on the bus -> the ratified grade
math -> an **on-device lab-end readout** -> the **session report** at `SessionStop`. (map §11)

**Scope / files.** `Runtime/Analytics/` (observers + the reducer/grade engine + the report assembler); the role-pick runtime
UI; the offline outbox (host-owned, verified).

**Steps (progress tracking):**
- [ ] Step 1: Implement the per-kind **metric observers** (StepDuration / TotalDuration / Drop / WrongInteraction / Order) as pure **reducers** over the bus event stream (`Reduce(events) -> rawValue`).
- [ ] Step 2: Implement the **grade engine** = the ratified §11.8 math: `x = clamp01(1 - Penalty)`; **ceiling kinds** = highest-band-crossed, **count kinds** = per-occurrence sum; normalized weighted mean at metric -> analytic -> objective -> grade; **applicability mask** (skipped/non-participant drop from the denominator; all-masked -> "incomplete"); objective `target` = pass-bar label. **Default bands `none 0 / warning 0.5 / error 1.0`**, author-overridable.
- [ ] Step 3: Implement the **on-device scoring/readout engine** - render the lab-end results from `(events + rubric)` with **no cloud round-trip** (DevKit = the canonical reducer; decision 38).
- [ ] Step 4: Implement bands -> **in-scene notification** (warning/error fire the UI nudge per `notifyInScene`).
- [ ] Step 5: **Role gating** - in-scene per-attempt role pick (Professor/Participant/Spectator) within per-lab capacities; emitter runs **full** for Participants, **presence only** for Professors, **nothing** for Spectators.
- [ ] Step 6: Assemble the **ONE self-contained session report** = users + roles + the timed event stream + the **bundled raw rubric** (not pre-scored, so it re-computes); merge on-device; ship at **`SessionStop`** (+ flush on reconnect / next launch).
- [ ] Step 7: **Offline durability** - the report builds incrementally to a **host-owned disk-backed outbox**; an unfinished session stores as **incomplete** (never lost, never "passed"). Verify: survives restart; retries on reconnect.
- [ ] Step 8: Author the **equivalence golden fixture** (events -> grade) and assert it green in EditMode (the portal mirror is B2.3).

**Acceptance.** A real lab produces the on-device readout + a session report at `SessionStop`; role gating + capacities work; incomplete sessions are preserved; the grade matches the golden fixture.

**Gate.** B1.1 + B1.4; Evaluate-Changes + SP playtest + the equivalence fixture.

---

## WS B2.2 - Subjects-registry runtime + signals

**Goal.** Make the subjects registry live so drops / wrong-interaction / order produce facts, mostly auto-wired. (map §11.2/§11.4)

**Scope / files.** `Runtime/Analytics/` (the registry runtime + `AnalyticsSignalEmitter` + the auto-wirer extension); the `TrackedGrabbable`/subject component.

**Steps (progress tracking):**
- [ ] Step 1: Runtime resolution of `TrackedSubject` (`scenarioRelevant`, `ownerStepGuid`); classify each interaction by *(in registry? / relevant? / ownerStep == current?)* -> correct / wrong-interaction / order violation (map §11.2).
- [ ] Step 2: **Auto-detect** the registry from the scenario - `InsertStep` (`item` / `targetTrigger`) + `SelectionStep` (its `SelectionLists`) pre-fill subject -> ownerStep; distractors + free grabbables added by hand.
- [ ] Step 3: **Auto-wire** subjects to grab (Meta `Select`) + drop (`RespawnOnDrop.WhenRespawned` - a Meta **sample** UnityEvent; don't hard-depend on the sample path, do the below-Y check in the DevKit's own subject component) - emit `item.grabbed` / `item.dropped` facts.
- [ ] Step 4: `AnalyticsSignalEmitter.EmitSignal(id)` (UnityEvent-callable) for authored failures - e.g. the scalpel's wrong-cut UnityEvent -> `EmitSignal("wrong-incision")` (an authored = error signal). Reuses the existing UnityEvent layer; no per-object analytics code.
- [ ] Step 5: Apply the **derived default severity** (relevant drop -> error / distractor -> warning/none / out-of-order -> warning / authored wrong-target -> error), author-overridable per metric.
- [ ] Step 6: Verify drops + wrong-interaction land in the event stream and score via B2.1.

**Acceptance.** Drops / wrong / order are captured from one registry; auto-detect + auto-wire work; authored signals fire; default severities apply.

**Gate.** B1.4; Evaluate-Changes + SP playtest.

---

## WS B2.3 - Cloud + portal (session-report schema)  *(Web Portal / cloud repo)*

**Goal.** Rebuild cloud ingest + storage + the portal readout to the **session-report schema** (replacing per-event
`AnalyticsEventV1` + Flow-A/B). (map §11, cross-surface)

**Scope / files.** Web Portal repo: a `session-report-ingest` edge fn + a tenant-scoped `session_reports` table + RLS; the
portal readout/render; the org-level consent flag.

**Steps (progress tracking):**
- [ ] Step 1: **[G2 - 2026-06-29]** Lock the session-report wire schema from B2.1 (users + roles + timed events + bundled rubric + tenant/session/lab/version envelope). This is the **cross-surface freeze** - do it FIRST.
- [ ] Step 2: `session_reports` DDL (tenant-scoped) + RLS; the report is **stored once per session** (group/session-level, not per-participant docs).
- [ ] Step 3: `session-report-ingest` edge fn: validate uuid / size / surface / **consent**; assert **report tenant == auth tenant** (RLS); reject paths covered.
- [ ] Step 4: **[HUMAN]** define + wire the launch **consent state** - org-level analytics consent set at enrollment, carried in the launch context the apps already receive; the consent **UI** lives in the VR Shell / mobile app; the DevKit only **reads** host-provided consent state. (The ingest rejects events without consent.)
- [ ] Step 5: Portal **renders** the readout by re-computing the grade from the bundled raw `(rubric + events)` - the **mirror** of the DevKit engine. Wire the **equivalence golden fixture** here too (must match Unity).
- [ ] Step 6: Verify real VR + AR session reports persist + render tenant-scoped under RLS, with consent supplied.

**Acceptance.** Session reports ingest + persist tenant-scoped; the portal renders the readout by re-computing from raw; the portal reducer matches the Unity reducer on the golden fixture; consent enforced.

**Gate.** G2 schema frozen; B2.1 emit shape; loading/empty/error states handled.

---

## WS B2.4 - Multiplayer turn-on (path-list FOLLOW + branch)

**Goal.** Flip on the B.1 path-store: DRIVE/FOLLOW per the B.1 table; branch records itself; declared-param wiring in LabConsole. (map §10)

**Scope / files.** `Runtime/Networking/` (turn on `FusionScenarioPath` follow); the runner follower path (B.1 inert hook); LabConsole declared-param wiring.

**Steps (progress tracking):**
- [ ] Step 1: Turn on the **follower path** - in a networked session, followers jump to the appended frontier guid (`FindIndexByGuid`); auto-resolving steps (ConditionsStep, no-wait Event/Timeline) **suppressed on followers** per the B.1 DRIVE/FOLLOW table.
- [ ] Step 2: **Branch + loop** ride the path-list (the appended guid IS the resolved branch); first-completion-wins, no decider (map §10.5).
- [ ] Step 3: Effects are **display-only on followers** (the driver owns the param writes).
- [ ] Step 4: Wire the declared networked params (the ex-switchboard states) from the LabConsole list (no hand-typed `stateID`).
- [ ] Step 5: **[HUMAN - on-device]** two-client VR proof: AnyCompletes + branch + loop + late-join + authority-drop. AR / no-Fusion: assert trace-identical to the Phase A golden (the Local passthrough).

**Acceptance.** Two-client VR sync (AnyCompletes + branch + late-join) green; AR no-Fusion trace-identical; followers never re-decide.

**Gate.** B1.3; on-device 2-client proofs.

---

## WS B2.5 - Localization content (Greek + English)

**Goal.** Author Greek + English on the B.1 keyed source; render on AR + VR via the build-baked pipeline. (map §12)

**Scope / files.** The DevKit Localization module (B1.5); the baked StringTables.

**Steps (progress tracking):**
- [ ] Step 1: Key the remaining lab + analytics-facing strings (rubric labels, `notifyInScene` text) via the B1.5 pipeline.
- [ ] Step 2: Author the Greek + English string sets through the relocated translate prompt (drafted, human-reviewed).
- [ ] Step 3: Build-bake the StringTables as the launch source (cloud resolver is post-launch; baked = the offline fallback).
- [ ] Step 4: Verify Greek + English render at runtime on **VR + AR** from the same keyed source.

**Acceptance.** Greek + English render on AR + VR from the keyed, build-baked source.

**Gate.** B1.5; Evaluate-Changes.

---

## WS B2.6 - Vitals FOUNDATION  *(CAN_TRAIL - slip-eligible)*

**Goal.** Lay the typed patient-state foundation on the param store. NOT the full digital twin. (map §8, decision 41)

**Scope / files.** `Runtime/Vitals/` (typed `PatientVitals` + the `Vital` binding); `IAgentStateSource`.

**Steps (progress tracking):**
- [ ] Step 1: `Vital` = a typed `ConsoleParameter` (param-store-backed) + a **3D binding** (the binding kinds `ControlOptionManager` already does: slider->timeline-speed / animator-param / field).
- [ ] Step 2: `PatientVitals` as the single typed model (pulse / breathing / BP / temp ...), additive alongside the existing scattered VR logic (not a cutover).
- [ ] Step 3: Wire at least one real 3D binding through the typed model (e.g. the breathing-blendshape timeline-speed vital).
- [ ] Step 4: Implement `IAgentStateSource` on `PatientVitals` so VICKY-observe reads structured state off the seam.
- [ ] Step 5: Evaluate-Changes - additive, no lab regressed.

**Acceptance.** Typed `PatientVitals` drives >=1 real binding + exposes `IAgentStateSource`. (Full twin + the `ControlOptionManager` PUN->Fusion convergence = post-launch.)

**Gate.** B1.2; Evaluate-Changes. **(Slip-eligible: never blocks B2.1-B2.5.)**

---

## The cross-surface session-report contract (G2 - 2026-06-29)

The launch wire format is **one session report** (supersedes per-event `AnalyticsEventV1` + Flow-A/B). Frozen at G2 so the
cloud lane (B2.3) can build against it. Envelope: `{ tenant, session, lab, version, users[{userId, role}], events[ timed:
session-started/ended, step-entered, errors ], rubric (raw, bundled) }`. Stored **once** per session, tenant-scoped (RLS);
the grade is **re-computed** from `(rubric + events)` - never shipped pre-scored. **Per-individual scoring is out of scope**
(the lever exists if ever needed). LMS interop (xAPI/SCORM/cmi5/LTI) is deferred - VICKY is the system of record.

## Delivery-chain alignment + critical path

- **06-29 (G2):** freeze the session-report schema (B2.3 Step 1) - **the most time-critical item**; the cloud lane starts here.
- **07-07:** the B.1 rubric/emit surface freezes; B2.1/B2.2 build against it.
- **Cloud lane (B2.3)** runs in parallel from G2; **DevKit analytics (B2.1/B2.2)** after 07-07; they meet at the equivalence fixture.
- **On-device end-to-end + the 2-client MP proof** fold into the **Phase C** integration window before the store gate.

## Deferred to post-launch

- **AI-JUDGING / VICKY-observe** (an additive subscriber on the same events; augments, never replaces the deterministic grade).
- **Web-Portal analytics tuning** (professors pick from a dropdown + set weight/target) - the grading layer goes portal-editable (decision 35).
- **Non-linear scoring curves** (replace the band step-function; the LUT/portal-eval concern - deferred with static weight).
- **The cloud localization pipeline** (portal editor + auto-translate + audio + fetch-by-language).
- **The Vitals digital twin** (cascade rules, profiles, scene migration) + the `ControlOptionManager` PUN->Fusion convergence.
- **JSON as source of truth**; the `IScenarioFlowStore` public graduation (Phase E); the LabConsole outside-in door turn-on.

## Exit checklist + gate

- [ ] **B2.1** On-device readout + session report at `SessionStop`; grade math = ratified; role gating + capacities; incomplete-never-lost; equivalence fixture green.
- [ ] **B2.2** Drops / wrong / order from one registry; auto-detect + auto-wire; authored signals; default severities.
- [ ] **B2.3** Session-report ingest + RLS; portal re-computes from raw; portal reducer == Unity reducer on the fixture; consent enforced.
- [ ] **B2.4** Two-client VR (AnyCompletes + branch + late-join) green; AR no-Fusion trace-identical.
- [ ] **B2.5** Greek + English render on AR + VR.
- [ ] **B2.6** Typed `PatientVitals` + >=1 binding + `IAgentStateSource` (or dispositioned slip).
- [ ] **End-to-end** one real lab, AR + VR, from the same scenario: author -> run -> on-device readout -> ingest -> portal readout.
- [ ] **Gates** every change passed "DevKit > Evaluate Changes"; G2 + 07-07 surfaces honoured. No emoji/mojibake.

## Plan self-review (coverage check)

- [ ] Every WS rides a B.1 seam: B2.1 bus+schema · B2.2 schema · B2.3 G2 schema · B2.4 path-store · B2.5 loc infra · B2.6 params.
- [ ] The deterministic-FIRST / AI-judging-deferred principle holds (no model in the launch scoring loop).
- [ ] The ratified §11.8 grade math + the default bands (`none 0 / warning 0.5 / error 1.0`) are the implemented spec.
- [ ] The session report supersedes `AnalyticsEventV1`; the cloud lane builds to it; the equivalence fixture keeps both reducers in lockstep.
- [ ] Role gating (Participant full / Professor presence / Spectator none) + capacities are covered.
- [ ] B2.6 (Vitals) is the single CAN_TRAIL slip-point; everything else is LAUNCH_BLOCKER.
- [ ] Real "§" characters; no emoji, no mojibake.

## Execution handoff

**Executors.** Claude Code implements the DevKit-repo WSs (B2.1/B2.2/B2.4/B2.5/B2.6) and the Web Portal / cloud WS (B2.3),
WS-by-WS. The consent UI (B2.3 Step 4) is a host-app [HUMAN] item; the on-device proofs (B2.4 Step 5) are [HUMAN] headset runs.

**Ratification path.** Derived from the architecture map; on sign-off, execute WS-by-WS after the B.1 surface freezes.

**Edit discipline.** Local-only edits; **the user runs git**. Tick checkboxes + update the Status & Progress Log as you go;
run `Evaluate Changes` before every commit.

---

## Status & Progress Log

> Update on EVERY WS start/close + every milestone. Newest first.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-26 | - | Plan authored from the architecture map (decision log -> 2026-06-26b); B.2 column projected to WS B2.1-B2.6; the session-report schema is the G2 cross-surface freeze; built on the B.1 foundation. | Claude Code |
