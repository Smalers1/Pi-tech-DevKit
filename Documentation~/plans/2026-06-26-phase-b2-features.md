---
title: Phase B.2 - Features (behaviour-additive) - analytics, multiplayer turn-on, localization, vitals
status: CODE IMPLEMENTED 2026-06-29; POST-REVIEW FIXES 2026-06-30 (Stergios' 6-item review: state<->param store unification #1, LabConsole params UI + live values #2, explicit SignalMetric #3, Greek mojibake->code-points #4, NetworkedParamStore pre-spawn guards #6, + a CS0118 fix; #5 metas were Unity-generated). All DevKit WSs B2.1/B2.2/B2.4/B2.5/B2.6/B2.7 authored + reviewed. Verification = Stergios' Unity pass (guides/2026-06-29-B2-unity-testing-guide.md). NOT done: B2.3 cloud (gated G2+ownership); post-B2 items + the 6-item review details in guides/2026-06-29-B2-open-decisions.md (section F).
date: 2026-06-26
author: Claude Code
owner: Claude Code (DevKit repo + the Web Portal / cloud lane)
phase: B.2
gate: EditMode "DevKit > Evaluate Changes" net (Proofs A/B/C) on every change + the dev-playtest checklist (single-player) + on-device 2-client proofs (multiplayer) + the analytics equivalence golden fixture (events -> grade, identical in Unity and the portal)
references:
  - ../proposed plans/devkit-architecture-map-phase-b.md (the architecture map = SINGLE SOURCE OF TRUTH; decision log 2026-06-19 -> 2026-06-26b)
  - 2026-06-26-phase-b1-structural.md (B.1 - the behaviour-neutral foundation this builds on; the frozen schema + the DRIVE/FOLLOW table)
  - 2026-06-09-phase-c-integration-and-ship.md (Phase C - roll into AR/VR + ship)
  - _archive/2026-06-09-phase-b-analytics.md (SUPERSEDED - mined only for: cloud-lane structure, queued transports, consent state, exit-criteria format)
companion: 2026-06-26-phase-b1-structural.md
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
the param store, the path-store, the frozen config schema) - turning it on, not re-architecting it. The launch grade is
**author-defined and reproducible** (no model in the scoring loop); AI-judging is post-launch. **Analytics is fully authored
in-lab via the DevKit at launch; Web-Portal tuning (pick from a dropdown; set weight + target) is post-launch** (decision 35).

**Authority / spec reference.** `devkit-architecture-map-phase-b.md` - §11 (the analytics hierarchy + the ratified §11.8
grade math + the report), §10 (multiplayer turn-on + the DRIVE/FOLLOW table), §12 (localization content), §8 (the `Vital`
primitive). B.1 provides the frozen schema (config types, `SessionStart/Stop`, roles, tenant+user id) every WS consumes.

**Duration / window.** B.2 runs after the B.1 surface freezes (**07-07**); the **cloud + portal lane runs in parallel from
the G2 schema freeze (06-29)**. On-device end-to-end smoke folds into the Phase C integration window before the store gate.

**Exit criteria (measurable).**
- End-to-end analytics green in ONE real lab on **VR (Quest) + AR (mobile UaaL)** from the same scenario: author the config -> run -> **on-device lab-end readout renders** -> session report ingests -> portal shows the readout.
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
| B2.7 | Authoring UX: visual params editor + graduate the networked-state trigger/listener components | B1.2 params, B1.3 stores | Stergios request 2026-06-29 |

> **WS tags.** **B2.1-B2.5 = LAUNCH_BLOCKER · B2.6 = CAN_TRAIL** (slip-eligible foundation - never blocks B2.1-B2.5,
> decision 41). B2.4's two-client on-device proof folds into the Phase C window. A tagged slip is **dispositioned in the
> Status & Progress Log - never silently skipped.**

> **Lane note.** B2.1 / B2.2 / B2.4 / B2.5 / B2.6 are **DevKit-repo**. **B2.3 is Web Portal / cloud-repo work** - the DevKit
> freezes the **session-report schema** (B2.1) and hands it across; the cloud rebuilds ingest/DDL/RLS/portal to it (this
> replaces the old per-event `AnalyticsEventV1` + Flow-A/B model). The consent **state** is supplied by the host app
> (VR Shell / mobile), read by the DevKit; the consent **UI** lives in the surfaces, not the DevKit.

> **Parallelism.** B2.3 (cloud) runs from the G2 freeze in parallel with B2.1/B2.2 (DevKit analytics). B2.4 (MP), B2.5 (loc
> content), B2.6 (vitals) are disjoint and run concurrently once their B.1 seam is in.

> **Carried over from B.1 (deferred -> now in B.2 scope).** B.1 landed these inert/partial; B.2 turns them on. Each folds into the WS noted:
> - **Telemetry onto the bus** (B1.1 S3 + B1.8 S4): flip `useEventBusStepTracking` ON - first push the lab context to `RuntimeTelemetryAdapter` (a spawner `BindContext`; it sits ABOVE the spawned root so `LabRuntimeContext.Find` never binds it), move finish-detection onto a terminal bus fact, get Vicky-ingestion sign-off on the higher-fidelity trace, THEN delete the legacy per-frame `FindObjectsOfType` scan. -> **B2.1**.
> - **Networked param store** (B1.2 S1/S5): the Fusion `NetworkDictionary<id, ParamValue>` impl + authority-only **sequenced** relative ops (`Add`/`Multiply`) + provenance/actor. -> **B2.4**.
> - **`FusionScenarioPath`** (B1.3 S1): the `#if`-gated `[Networked, Capacity(256)]` flow-store ring - **not authored yet**, so B2.4 Step 1 must BUILD it before it can "turn it on". -> **B2.4**.
> - **Fusion weaver coverage** (B1.3): confirm Fusion's weaver processes `Pitech.XR.Networking` so `NetworkedLabStateStore`'s `[Networked]` dict works on device (VR's XRShared asmdefs get woven, so it should). -> **B2.4 prerequisite**.
> - **Localization runtime + pipeline** (B1.5 S2/S3): the StringTable-backed `ILocalizationLookup` impl + the `[Localize]` reflection scan + the pipeline relocation - B2.5 content depends on these. -> **B2.5 prerequisite** (or the post-B2 migration).

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

**Goal.** Turn the B.1 config schema into a running analytics path: observers -> raw events on the bus -> the ratified grade
math -> an **on-device lab-end readout** -> the **session report** at `SessionStop`. (map §11)

**Scope / files.** `Runtime/Analytics/` (observers + the reducer/grade engine + the report assembler); the role-pick runtime
UI; the offline outbox (host-owned, verified).

**Steps (progress tracking):** *(code 2026-06-29; verification = Stergios' Unity pass)*
- [x] Step 1: per-kind reducers (StepDuration / TotalDuration / Drop / WrongInteraction / Order) over the captured bus stream - `AnalyticsGradeEngine.ComputeMetric/ComputeCount` + `AnalyticsEvent`/`SessionEventStream`.
- [x] Step 2: the §11.8 grade engine - `AnalyticsGradeEngine` (ceiling=highest band crossed w/ threshold<=0 inactive; count=per-occurrence sum; weighted means; applicability mask; "incomplete" if all-masked OR unclosed bracket; target=pass-bar). Hand-verified to 0.25 on the fixture.
- [x] Step 3: on-device readout - `GradeResult` from (events + config), raised via `LabAnalytics.onReadout`, no cloud round-trip.
- [x] Step 4: bands -> in-scene notification - `LabAnalytics.NotifyForCount/NotifyForSignal` -> `onNotification` (per `notifyInScene`). *(duration-band notify fires at step-complete; live mid-step nudge = noted refinement.)*
- [x] Step 5: role gating - `SessionRoleSelector` (in-scene pick) + `Finalize` (Participant full / Professor presence / Spectator none).
- [x] Step 6: the ONE session report (`SessionReport` + `SessionReportJson`) shipped at SessionStop with users+roles+timed stream+bundled raw config. *(SP done; cross-peer MERGE + flush-on-reconnect = host/post-B2.)*
- [ ] Step 7: offline durability - **DevKit seam done** (`ISessionReportSink`; submit-at-stop + submit-incomplete-on-disable; "incomplete never passed"). **[HUMAN/host]** the disk-backed outbox impl + survives-restart/retries verification is HOST-OWNED (VR Shell / mobile).
- [x] Step 8: equivalence golden fixture (`AnalyticsEquivalenceFixture`) green in EditMode (`AnalyticsGradeEngineTests`, 5 tests). *(portal mirror is B2.3.)*

**Acceptance.** A real lab produces the on-device readout + a session report at `SessionStop`; role gating + capacities work; incomplete sessions are preserved; the grade matches the golden fixture.

**Gate.** B1.1 + B1.4; Evaluate-Changes + SP playtest + the equivalence fixture.

---

## WS B2.2 - Subjects-registry runtime + signals

**Goal.** Make the subjects registry live so drops / wrong-interaction / order produce facts, mostly auto-wired. (map §11.2/§11.4)

**Scope / files.** `Runtime/Analytics/` (the registry runtime + `AnalyticsSignalEmitter` + the auto-wirer extension); the `TrackedGrabbable`/subject component.

**Steps (progress tracking):** *(code 2026-06-29)*
- [x] Step 1: runtime classification - facts emit raw (`interaction.used`/`item.dropped`); the **recorder is the single classifier** (`LabAnalytics.ClassifyUse`: in-registry? relevant? ownerStep==current? -> correct / wrong / order).
- [x] Step 2: auto-detect - **InsertStep + SelectionStep done** (`LabAnalyticsEditor` pulls `InsertStep.item` AND each `SelectionStep`'s correct colliders -> relevant subjects, owner=that step; list resolved by `listIndex` else `listKey`, deduped by target/id). Distractors / free grabbables stay by-hand (the map's split). *(Closed 2026-06-30; needed adding `Pitech.XR.Interactables` to the `Analytics.Editor` asmdef - editor-only, no public API.)*
- [x] Step 3: subject runtime - `AnalyticsSubject` (below-Y drop check, dependency-free; `ReportGrabbed/Dropped/Used` UnityEvent-callable) + the editor auto-wire adds the component & sets `subjectId`. *(Hooking Meta `Select` / `RespawnOnDrop` to those methods is author-side UnityEvent wiring, per "don't hard-depend on the sample path".)*
- [x] Step 4: `AnalyticsSignalEmitter.EmitSignal(id)` / `Emit()` (UnityEvent-callable) -> `analytics.signal` -> routes to the metric whose id matches.
- [x] Step 5: derived default severity in the engine (`DeriveSeverity`) + the recorder (relevant drop->error / distractor->warning / out-of-order->warning / signal->error), author-overridable via band weights.
- [ ] Step 6: **[HUMAN verify]** drops + wrong land in the stream and score via B2.1 (SP playtest).

**Acceptance.** Drops / wrong / order are captured from one registry; auto-detect + auto-wire work; authored signals fire; default severities apply.

**Gate.** B1.4; Evaluate-Changes + SP playtest.

---

## WS B2.3 - Cloud + portal (session-report schema)  *(Web Portal / cloud repo)*

**Goal.** Rebuild cloud ingest + storage + the portal readout to the **session-report schema** (replacing per-event
`AnalyticsEventV1` + Flow-A/B). (map §11, cross-surface)

**Scope / files.** Web Portal repo: a `session-report-ingest` edge fn + a tenant-scoped `session_reports` table + RLS; the
portal readout/render; the org-level consent flag.

**Steps (progress tracking):** *(NOT BUILT - intentionally gated; the DevKit emit shape `SessionReport`/`SessionReportJson` is ready to hand over. Blocked on: (a) Stergios' G2 schema confirm; (b) cloud-lane ownership. Do not build against an unconfirmed wire format. See open-decisions doc A1/A4.)*
- [ ] Step 1: **[G2 - 2026-06-29]** Lock the session-report wire schema from B2.1 (users + roles + timed events + bundled config + tenant/session/lab/version envelope). This is the **cross-surface freeze** - do it FIRST.
- [ ] Step 2: `session_reports` DDL (tenant-scoped) + RLS; the report is **stored once per session** (group/session-level, not per-participant docs).
- [ ] Step 3: `session-report-ingest` edge fn: validate uuid / size / surface / **consent**; assert **report tenant == auth tenant** (RLS); reject paths covered.
- [ ] Step 4: **[HUMAN]** define + wire the launch **consent state** - org-level analytics consent set at enrollment, carried in the launch context the apps already receive; the consent **UI** lives in the VR Shell / mobile app; the DevKit only **reads** host-provided consent state. (The ingest rejects events without consent.)
- [ ] Step 5: Portal **renders** the readout by re-computing the grade from the bundled raw `(config + events)` - the **mirror** of the DevKit engine. Wire the **equivalence golden fixture** here too (must match Unity).
- [ ] Step 6: Verify real VR + AR session reports persist + render tenant-scoped under RLS, with consent supplied.

**Acceptance.** Session reports ingest + persist tenant-scoped; the portal renders the readout by re-computing from raw; the portal reducer matches the Unity reducer on the golden fixture; consent enforced.

**Gate.** G2 schema frozen; B2.1 emit shape; loading/empty/error states handled.

---

## WS B2.4 - Multiplayer turn-on (path-list FOLLOW + branch)

**Goal.** Flip on the B.1 path-store: DRIVE/FOLLOW per the B.1 table; branch records itself; declared-param wiring in LabConsole. (map §10)

**Scope / files.** `Runtime/Networking/` (turn on `FusionScenarioPath` follow); the runner follower path (B.1 inert hook); LabConsole declared-param wiring.

**Steps (progress tracking):** *(DATA PLANE built 2026-06-29; full interactive co-op = post-B2 on-device.)*
- [~] Step 1: follower path - **built:** `FusionScenarioPath` (replicated entered-guid ring) + `LabConsole` binds it (SP binds nothing -> runner byte-identical) + `ScenarioRunner.RunFollower` mirrors the frontier (jump + display-only AV + matching step facts), only reachable when bound AND `!IsDriver`. **post-B2 on-device:** full auto-resolving suppression for interactive steps + first-completion-wins.
- [~] Step 2: branch + loop ride the path-list - the driver appends the entered guid (the resolved branch); the follower follows it. **post-B2:** first-completion-wins RPC across peers.
- [~] Step 3: effects display-only on followers - `DisplayOnlyForFollower` (Timeline play / Event onEnter / session facts); followers never append (driver owns writes).
- [~] Step 4: declared-param wiring - **wired (2026-06-30):** LabConsole resolves an optional networked `IParamStore` component (self+children) and, when present, fronts the local store with `RoutedParamStore` - a scope router that sends **Networked-scope** ids to `NetworkedParamStore` (replicated + authority-sequenced Add/Multiply via its RPCs) and keeps **Local-scope** ids client-local; `ParamChanged` aggregates both for the StatsUI mirror. Resolves via Core's `IParamStore` so no Scenario->Networking asmdef dep; SP/no-Fusion resolves null -> store IS the LocalParamStore -> **byte-identical to B.1** (no new serialized field on LabConsole; router is `internal` -> no public API). **post-B2 on-device:** verification needs a 2-client Fusion lab; provenance/actor tracking is a follow-up. See open-decisions C3.
- [ ] Step 5: **[HUMAN - on-device]** two-client VR proof (AnyCompletes + branch + loop + late-join + authority-drop); AR no-Fusion trace-identical (binds nothing).

**Acceptance.** Two-client VR sync (AnyCompletes + branch + late-join) green; AR no-Fusion trace-identical; followers never re-decide.

**Gate.** B1.3; on-device 2-client proofs.

---

## WS B2.5 - Localization content (Greek + English)

**Goal.** Author Greek + English on the B.1 keyed source; render on AR + VR via the build-baked pipeline. (map §12)

**Scope / files.** The DevKit Localization module (B1.5); the baked StringTables.

**Steps (progress tracking):** *(RUNTIME built 2026-06-29; running the pipeline ON REAL LABS = post-B2, per Stergios.)*
- [~] Step 1: keying - the `[Localize]` **scan tool is built** (`LocalizeScan`, menu item, recurses data-asset fields). **post-B2:** running it on the real lab strings (config labels / `notifyInScene` text) + de-hardcoding `SelectionLists`. (Quiz strings already keyed in B1.5.)
- [~] Step 2: Greek + English - **sample EN/EL provided** (`SampleLocalizationStrings`) to verify rendering now. **post-B2:** authoring the full content through the translate round-trip.
- [ ] Step 3: build-bake StringTables - **resolver built** (`StringTableLocalizationLookup`, gated `PITECH_HAS_LOCALIZATION`, reads the baked tables). **post-B2:** baking the actual tables (the pipeline-on-labs).
- [~] Step 4: verify render - verifiable NOW via the sample (`LocalizationServices.Install(new DictionaryLocalizationLookup(SampleLocalizationStrings.Greek))` -> Quiz renders Greek). Full VR+AR from baked tables = post-B2.

**Acceptance.** Greek + English render on AR + VR from the keyed, build-baked source.

**Gate.** B1.5; Evaluate-Changes.

---

## WS B2.6 - Vitals FOUNDATION  *(CAN_TRAIL - slip-eligible)*

**Goal.** Lay the typed patient-state foundation on the param store. NOT the full digital twin. (map §8, decision 41)

**Scope / files.** `Runtime/Vitals/` (typed `PatientVitals` + the `Vital` binding); `IAgentStateSource`.

**Steps (progress tracking):** *(code 2026-06-29)*
- [x] Step 1: `Vital` = typed value + 3D binding (TimelineSpeed / AnimatorParameter / Field - the `ControlOptionManager` kinds).
- [x] Step 2: `PatientVitals` - the single typed model, owns its own `LocalParamStore` (decoupled from LabConsole), additive.
- [ ] Step 3: wire >=1 real 3D binding through the model (e.g. breathing-blendshape Timeline-speed) - **code supports it; the real scene binding is author-side/post-B2.**
- [x] Step 4: `IAgentStateSource` on `PatientVitals` (`Core/IAgentStateSource`) - VICKY-observe reads structured state.
- [ ] Step 5: **[HUMAN verify]** Evaluate-Changes - additive, no lab regressed.

**Acceptance.** Typed `PatientVitals` drives >=1 real binding + exposes `IAgentStateSource`. (Full twin + the `ControlOptionManager` PUN->Fusion convergence = post-launch.)

**Gate.** B1.2; Evaluate-Changes. **(Slip-eligible: never blocks B2.1-B2.5.)**

---

## WS B2.7 - Authoring UX: visual params + networked-state components

**Goal.** Make the B.1 systems AUTHORABLE - B.1 landed the inert data + runtime; this is the editor/visual layer authors actually use. (Stergios request 2026-06-29)

**Scope / files.** `Editor/Scenario.Editor/` (LabConsole params authoring); `Runtime/Networking/` + an editor folder (graduate the state trigger/listener components + their inspectors).

**Steps (progress tracking):** *(code 2026-06-29)*
- [x] Step 1: visual params editor - `ConsoleParameterDrawer` (type-aware row: id/type/conditional default/min-max/scope + inline validation: empty id, max<=min) + **`LabConsoleEditor`** (2026-06-30): a first-class **Parameters** section over the private `parameters` list (add/remove/reorder via the drawer). *(cross-sibling unique-id not enforced - minor.)*
- [x] Step 2: **play-mode live values - DONE 2026-06-30.** `LabConsoleEditor` shows each declared parameter's live runtime value in Play Mode, read from the internal `LabConsole.Params` via an editor `InternalsVisibleTo` grant (no public accessor needed). Closes the prior deferral (open-decisions C7/F7).
- [x] Step 3: graduate triggers - DevKit `PhysicsStateTrigger` + `UIStateTrigger` resolve `GetComponentInParent<ILabStateStore>()`, no static Instance.
- [x] Step 4: graduate listeners - DevKit `EventStateListener` (subscribes to `StateChanged`, no polling) + `TimelineStateListener` (Fusion-gated NetworkBehaviour, resolves the store).
- [x] Step 5: dropdown inspectors - `StateComponentEditors` pick `stateID` from the nearest LabConsole's declared **bool** params (free-text fallback).
- [ ] Step 6: **[HUMAN verify]** Evaluate-Changes - additive; AR (no Fusion) compiles (triggers/listeners resolve the Local store; Timeline listener compiles out).

**Acceptance.** Authors declare params and wire named states through DevKit components with dropdowns (no hand-typed ids); works in SP (Local store) and networked (Fusion store).

**Gate.** B1.2 params + B1.3 stores; Evaluate-Changes.

> **WS tag.** The component graduation (Steps 3-5) is LAUNCH-relevant - the post-B2 VR migration re-wires labs onto these; the params editor + live readout (Steps 1-2) are usability polish (CAN_TRAIL). This GRADUATES the components into the DevKit (makes them available + visual); RE-WIRING the existing VR labs onto them is the **post-B2 migration** (below).

---

## The cross-surface session-report contract (G2 - 2026-06-29)

The launch wire format is **one session report** (supersedes per-event `AnalyticsEventV1` + Flow-A/B). Frozen at G2 so the
cloud lane (B2.3) can build against it. Envelope: `{ tenant, session, lab, version, users[{userId, role}], events[ timed:
session-started/ended, step-entered, errors ], config (raw, bundled) }`. Stored **once** per session, tenant-scoped (RLS);
the grade is **re-computed** from `(config + events)` - never shipped pre-scored. **Per-individual scoring is out of scope**
(the lever exists if ever needed). LMS interop (xAPI/SCORM/cmi5/LTI) is deferred - VICKY is the system of record.

## Delivery-chain alignment + critical path

- **06-29 (G2):** freeze the session-report schema (B2.3 Step 1) - **the most time-critical item**; the cloud lane starts here.
- **07-07:** the B.1 config/emit surface freezes; B2.1/B2.2 build against it.
- **Cloud lane (B2.3)** runs in parallel from G2; **DevKit analytics (B2.1/B2.2)** after 07-07; they meet at the equivalence fixture.
- **On-device end-to-end + the 2-client MP proof** fold into the **Phase C** integration window before the store gate.

## Post-B2: HealthOn VR migration (the actual switch - Phase C window)

Per Stergios (2026-06-29): the **full HealthOn VR migration happens ONCE, after B1+B2** - not piecemeal. Until then VR consumes the DevKit but keeps its own components; B1/B2 are verified by test. The migration is **ask-first** (VR repo), in a Unity loop:
- **Networked state:** replace VR `NetworkStateManager` with a thin `[Obsolete]` facade forwarding to the resolved DevKit store; re-point the 4 networked scenes (AMEA / MoMt / Ioanninon / DIPAE) to `NetworkedLabStateStore` + re-enter `defaultStates`; re-wire triggers/listeners to the graduated B2.7 components. (closes B1.3 S5)
- **Localization:** GUID-carry the VR `Editor/Localization` pipeline + `LanguageSwitcher` / `DoNotLocalize` into the DevKit module; delete the VR copies; run the `[Localize]` scan. (closes B1.5 S2)
- **Params / analytics:** run the StatsConfig upgrader on the launch labs + author the config; wire labs onto the new systems.
- Confirm Fusion's weaver covers `Pitech.XR.Networking` on device.

## Deferred to post-launch

- **AI-JUDGING / VICKY-observe** (an additive subscriber on the same events; augments, never replaces the deterministic grade).
- **Web-Portal analytics tuning** (professors pick from a dropdown + set weight/target) - the grading layer goes portal-editable (decision 35).
- **Non-linear scoring curves** (replace the band step-function; the LUT/portal-eval concern - deferred with static weight).
- **The cloud localization pipeline** (portal editor + auto-translate + audio + fetch-by-language).
- **The Vitals digital twin** (cascade rules, profiles, scene migration) + the `ControlOptionManager` PUN->Fusion convergence.
- **JSON as source of truth**; the `IScenarioFlowStore` public graduation (Phase E); the LabConsole outside-in door turn-on.

## Exit checklist + gate

> Legend: `[x]` code complete + (where possible) reviewed · `[~]` code complete, Unity/on-device verify pending · `[ ]` not done / deferred. Code authored 2026-06-29; **verification is Stergios' Unity pass.**

- [~] **B2.1** On-device readout + session report at `SessionStop`; grade math = ratified; role gating + capacities; incomplete-never-lost; equivalence fixture green. *(code done + reviewed; host outbox impl = HUMAN/host; SP playtest pending.)*
- [~] **B2.2** Drops / wrong / order from one registry; auto-wire; authored signals; default severities; auto-detect (InsertStep + SelectionStep). *(All code done; SP playtest pending.)*
- [ ] **B2.3** Cloud ingest + RLS + portal. **NOT BUILT** - gated on G2 confirm + ownership.
- [~] **B2.4** Data plane (`FusionScenarioPath` + `NetworkedParamStore`) + follower mirror + declared-param wiring (`RoutedParamStore` in LabConsole) built. *(Two-client VR proof + interactive co-op = post-B2 on-device; SP byte-identical.)*
- [~] **B2.5** Runtime (resolver + dictionary + sample EN/EL + scan) built; Greek renders via the sample. *(Pipeline-on-real-labs + baked tables = post-B2.)*
- [~] **B2.6** Typed `PatientVitals` + `IAgentStateSource` + binding kinds. *(Real scene binding = author-side/post-B2.)*
- [~] **B2.7** Visual params drawer + the 4 components graduated (dropdown-wired; SP + networked). *(Play-mode live values deferred.)*
- [~] **B.1 carry-overs** Networked param store + `FusionScenarioPath` **built** (B1.2 / B1.3); localization runtime + scan **built** (B1.5). **telemetry-on-bus NOT flipped** (B1.1 S3 / B1.8 S4) - gated on Vicky-ingestion sign-off.
- [ ] **End-to-end** one real lab, AR + VR (author -> run -> readout -> ingest -> portal). **Pending** B2.3 + the Unity/on-device passes.
- [ ] **Gates** **[HUMAN]** every change through "Evaluate Changes" (regen Proof B baseline; Proof C may be Inconclusive = OK for additive B.2). No emoji/mojibake (Greek sample = correct UTF-8).

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
| 2026-06-29 | B2.1/B2.2/B2.4/B2.5/B2.6/B2.7 | **B.2 CODE IMPLEMENTED (Stergios: "implement all of b2, loop till done").** All DevKit-repo workstreams authored + adversarially reviewed (5-dimension workflow + verify pass: 0 confirmed blockers/majors; §11.8 math hand-verified to 0.25; SP byte-identical; asmdef topology sound). **B2.1:** AnalyticsGradeEngine (§11.8) + AnalyticsEvent/SessionEventStream + GradeResult readout + SessionReport/SessionReportJson + ISessionReportSink outbox + LabAnalytics recorder (opt-in component, NOT a LabConsole field -> Proof C safe) + in-scene notifications + AnalyticsEquivalenceFixture + EditMode test. Bracket facts (session.started/stopped) wired in the runner; report identity added to LabRuntimeContext (Core, additive) + stamped by the spawner. **Roles:** SessionRoleSelector (UI by Stergios). **B2.2:** AnalyticsSubject + AnalyticsSignalEmitter (raw facts) + recorder classification (interaction.used -> correct/wrong/order) + LabAnalyticsEditor auto-detect/auto-wire. **B2.4:** FusionScenarioPath + NetworkedParamStore (Fusion-gated, proven NetworkedLabStateStore patterns) + LabConsole flow-store bind (SP-safe) + RunFollower mirror (full interactive co-op = post-B2 on-device). **B2.7:** 4 graduated state components (resolve ILabStateStore, no static Instance) + dropdown inspectors + ConsoleParameter drawer (S2 live-values deferred). **B2.5:** StringTableLocalizationLookup (gated) + DictionaryLocalizationLookup + sample EN/EL + [Localize] scan (pipeline-on-labs = post-B2). **B2.6:** Vital + PatientVitals + IAgentStateSource. **NOT done:** B2.3 cloud (gated on G2 confirm + ownership); telemetry-on-bus carry-over left OFF (Vicky-ingestion sign-off). Verification = Stergios' Unity pass (guides/2026-06-29-B2-unity-testing-guide.md); open calls in guides/2026-06-29-B2-open-decisions.md. | Claude Code |
| 2026-06-29 | scope | **B.1 deferrals + visual-authoring folded into B.2 (Stergios).** Added the "Carried over from B.1" list (telemetry-on-bus B1.1 S3 + B1.8 S4 -> B2.1; Networked param store B1.2 -> B2.4; `FusionScenarioPath` must be BUILT B1.3 -> B2.4; Fusion weaver coverage; localization runtime/scan B1.5 -> B2.5). Added **WS B2.7 - Authoring UX** (visual params editor + live values; graduate the networked-state trigger/listener components with dropdown inspectors; work in SP via the Local store + networked via the Fusion store). Added the **Post-B2 HealthOn VR migration** section (the deferred one-shot switch: NetworkStateManager facade + 4-scene re-point, localization GUID-carry, lab param/config authoring, weaver check). | Claude Code |
| 2026-06-26 | - | Plan authored from the architecture map (decision log -> 2026-06-26b); B.2 column projected to WS B2.1-B2.6; the session-report schema is the G2 cross-surface freeze; built on the B.1 foundation. | Claude Code |
