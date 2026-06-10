---
title: After Launch - Phases D..I (the road to Vicky-in-labs 2027 and prompt-to-simulation 2028)
status: RATIFIED by Petros (board) 2026-06-10 - pending Stergios final review; per-phase dispatch post-launch
date: 2026-06-10
owner: Heisenberg (CTO) plan-of-record; DevKit lead executes per-phase
phase: post-launch (Phases D, E, F, G, H, I - the lettered continuation of the launch Phases A/B/C)
gate: 2026-09-10 controlled commercial B2B launch must have shipped (Phases A/B/C green); nothing here starts before launch (except the Phase D parallel localization-cloud track, which is Web-Portal-lane and may pre-stage)
references:
  - ../2026-06-09-devkit-launch-plan.md (umbrella / index - the A/B/C launch set this plan continues)
  - ../2026-06-09-phase-a-refactor-and-foundation.md (Phase A - the WS A3 net + seams these phases build on)
  - ../2026-06-09-phase-b-analytics.md (Phase B - the deterministic analytics stream the Phase G observer subscribes to)
  - ../2026-06-09-phase-c-integration-and-ship.md (Phase C - integrate + ship; launch close)
  - ../../specs/2026-04-23-devkit-1.0-target-architecture-design.md (target architecture; §8 Runtime, §10 Agent Substrate, §17 migration plan, §28 addendum incl. §28.7 naming map)
  - ../_archive/2026-05-08-p2-behavior-roadmap.md (SOURCE - consolidated here; its arch-P2..P7 numbering is RETIRED, see Terminology)
  - ../_archive/2026-05-08-p2-behavior-roadmap-slice-p5-substrate-observer-summary.md (the observer transport half already partly built - PIT-336 / PIT-388)
  - ../_archive/2026-04-23-p1-foundation.md (the OLD foundation plan - its LabEventBus/registries/LabRoot detail is the reference material for Phase D WS D1)
supersedes: the arch-P2..P7 framing of the archived roadmap (consolidated + renamed here). The lettered phases D..I are now the post-launch source of truth.
---

# After Launch - Phases D..I

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (or subagent-driven-development).
> Near-term steps use `- [ ]` checkboxes; far phases are roadmap-altitude tables that each get their own
> implementation plan at phase start (modelled on the Phase A doc). Update the Status & Progress Log on every
> WS/phase start/close. Local-only edits; Petros runs git.

> **Terminology - the old numbering is RETIRED.** The launch is **Phases A -> B -> C**; this plan continues the
> letters: **Phase D -> E -> F -> G -> H -> I**. The architecture spec's §17 still numbers its migration phases
> P1..P7 internally - the mapping is fixed in spec §28.7 and repeated here: **Phase A = spec-P1 · D = spec-P2 ·
> E = spec-P3 · F = spec-P4 · G = spec-P5 · H = spec-P6 · I = spec-P7.** Never write a bare "P2/P3" in plans;
> cite "spec §17 P-n" only when pointing into the spec.

**The throughline (Petros, 2026-06-10):**
**Phase A -> B -> C -> [LAUNCH 2026-09-10] -> Phases D..I -> 2027 Vicky inside AR/VR labs (observation, guidance,
Q/A, and - via the Director foundation - handling scenarios) -> 2028 Vicky generates labs from a prompt/PDF -> the
VICKY master strategy achieved.** Every phase below names what it contributes to that line.

**Goal:** Carry the DevKit from its launch baseline through six lettered phases: the runtime foundation + runner
extraction (D), the LabConsole control plane + multiplayer (E), authoring/objectives + the content systems (F), the
Vicky Observer (G), Vicky Interactive + the Director foundation (H), and the v1.0 API lock (I). The four §28 domain
systems are folded INTO these phases as workstreams (no separate track list).

**Architecture stance:** behaviour-CHANGING, but always **additive + gated**. Every consumer-visible delta is opt-in
(per-lab / per-cohort / per-learner) and every behaviour-change boundary goes through the 6-stage Engineering Review
with the CTO at Stage 2 + Stage 4 (HIGH-RISK slices). The 7-layer model, the 5 non-breaking rules (spec §15), and
the `[MovedFrom]` discipline still apply.

**Duration / sizing:** post-2026-09-10. The archived roadmap sized the spine at ~18-25 weeks assuming HUMAN
implementation; with AI agents coding (~2-3 days per workstream) the constraint shifts to **review, in-editor
verification, cross-repo lanes (Web Portal), pilot cohorts, and the teaching calendar** - each phase's
implementation plan re-estimates at phase start on that basis. The binding 2027 constraint is the **semester cliff**:
Phase H's pilot cohorts must run inside the **Feb-May 2027 teaching window**, so Phases D..G must close by ~Jan 2027
- which agent velocity makes comfortably feasible IF the external gates (portal ingestion, consent UI, the action
contract) are filed early. File them in Phase D, not when they block.

**Exit criteria (measurable):**
- Phase D: runtime foundation live (LabEventBus + registries + LabRoot); runner extracted (`SceneManager` a ~150-line
  facade); every fixture replays identically (golden trace = a GATE); server CI on; package-metadata cutover done.
- Phase E: LabConsole control plane + Fusion replication; multiplayer flow-store in DevKit; late-join fast-forward works.
- Phase F: objective evaluator (Tier 1+2) emitting `AttemptSummaryV1` the portal ingests; Hub v2 + Building Blocks;
  AI-authoring command library + CLI/MCP bridge; localization cloud pipeline live (the EU-scale moat); JSON
  source-of-truth decision executed.
- Phase G: AgentObservation producer wired end-to-end (consent-gated, tenant-scoped); Vicky OBSERVES real labs.
- Phase H: Vicky guidance/Q&A live to pilot cohorts behind the 5-gate pipeline; the Director foundation (flow-control
  action vocabulary) defined; **the 6-month VICKY-in-AR/VR milestone closes**.
- Phase I: API baseline rebased; consumers pin v1.0.

---

## Plan structure

| Phase | Focus | Gate / depends-on | Spec / source |
|---|---|---|---|
| **D** | Runtime foundation (LabEventBus/registries/LabRoot) + runner extraction + golden-replay gate + server CI + package-metadata cutover. **Parallel track: localization-cloud (Web Portal lane) starts immediately.** | Launch shipped (A/B/C green) | spec §8, §9.7, §17(P2); old foundation plan (archived) for the bus/registry detail |
| **E** | LabConsole control plane + Fusion replication + multiplayer-into-DevKit (NetworkedStates -> `IScenarioFlowStore`, late-join fast-forward, Make-Multiplayer) | Phase D exit | spec §8.6, §9.8, §17(P3), §28.2 |
| **F** | Authoring + objectives + content systems: Hub v2, Building Blocks (+ sidecar library growth), objective evaluator T1+T2 -> portal, AI-authoring command library + CLI/MCP bridge, localization-cloud completion, vitals digital-twin completion, JSON source-of-truth migration | Phase E exit; **portal ingestion ready** | spec §13, §17(P4), §28.3/.4/.5 |
| **G** | **Vicky Observer**: the producer half onto the already-built transport; consent UI (surfaces); Tier-3 routing; per-scenario cost | Phase F exit **AND** the VICKY runtime contract (GREEN - PIT-274) | spec §10.5/§10.7, §17(P5), §28.7 canonical-contract call |
| **H** | **Vicky Interactive + Director foundation**: gated actuation (5 gates), Tutor/Examiner pilots, first ConsoleActions; the Director flow-control vocabulary | Phase G exit **AND** `AgentActionRequestV1` contract **AND** ethics sign-off; pilot cohorts in the Feb-May 2027 window | spec §10.3/§10.4, §17(P6), §28.7 Director |
| **I** | v1.0 API lock: baseline rebase, CHANGELOG consolidation, consumers pin v1.0.0 | Phase H exit + 1wk soak | spec §17(P7), §18, §28.7 version semantics |

---

# Phase D - Runtime Foundation & Runner Extraction

**What it contributes to the throughline:** the event bus every later phase subscribes to, and the extracted runner
Vicky will eventually observe (G) and direct (H). Consumer-visible behaviour change: **none observable** - internal
restructure proven by the golden trace.

### WS D1 - The runtime foundation (absorbs the orphaned old-foundation deliverables)
The behaviour-neutral Phase A deliberately shipped only seams; the foundation the spec presupposes - **`ILabEventBus`
+ `LabEventBus` (zero-alloc), the registries (`StepRunnerRegistry`, `EffectHandlerRegistry`, validator/building-block/
inspector registries), `LabRoot` + `DevKitBootstrapper` + `CapabilityRegistry` (with `XRServices` as a delegating
shim), the capability interfaces, the V1 Domain contracts + serialization/link.xml discipline** - lands HERE, first.
The archived old foundation plan (`../_archive/2026-04-23-p1-foundation.md`) is the reference detail (its asmdef
topology + code sketches), re-scoped to the current package layout and Unity 6+.
- [ ] LabEventBus (zero-alloc publish; lab-scoped; disposed at despawn) + the event vocabulary (the Phase A step-fact
      consts become real events).
- [ ] Registries + `LabRoot`/bootstrap + `CapabilityRegistry`; `XRServices` delegates (no consumer break).
- [ ] V1 Domain contract discipline (SchemaVersion, AOT/link.xml, IL2CPP round-trip test).

### WS D2 - Runner extraction (the god-class cracks, golden-trace-proven)
- [ ] Complete the golden-trace fixture corpus (one per step Kind, all 6 GroupStep modes, the
      debounce-vs-first-click divergence) - the Phase A seed becomes the GATE.
- [ ] Extract the 24 `RunXxx`/`RunXxxGroup` runners -> an `IStepRunner` dispatch registry; `SceneManager` becomes a
      ~150-line facade (same type, GUID, serialized fields, public methods). Reconcile the debounce divergence as a
      deliberate, golden-trace-proven change.
- [ ] `ContentDeliverySpawner` 4-way decomposition (spec §9.7), same facade discipline; replace its string-reflection
      with `ISceneRunnerControl` (the Phase A WS A8 seam pays off here).
- [ ] `IScenarioFlowStore` + `LocalScenarioFlowStore` defined; the extracted runner consults the store (single-player
      = identical behaviour; the seam Phase E's multiplayer backend swaps under).
- [ ] Server CI on (GameCI + license): the same Evaluate-Changes suite + the PlayMode golden trace per PR.

### WS D3 - Package-metadata cutover (the ONE resolution-affecting change, spec §28.6)
- [ ] `package.json`: `unity` 2022.3 -> 6000.0 + the `dependencies` block (`com.unity.addressables`, TMP, ugui) in
      one commit; remove the dead `#if PITECH_ADDR` `#else` branches; run the with/without-`PITECH_*` compile matrix.
- [ ] First behaviour-permitted bug fix: the `AddressablesRemoteUrlRewriter` global-transform save/restore (+
      regression test) - the confirmed critical bug Phase A had to leave untouched.

### Parallel track (starts immediately post-launch; Web-Portal lane; does NOT wait for D2):
**Localization cloud pipeline - first slice** (full WS in Phase F): stand up the `(key, lang) -> {text, audioUrl}`
table + edge-fn projector + the VICKY-translate fn (reusing the existing medical prompt verbatim). Rationale:
"German hospital, 4 labs, translated in minutes" is a SALES demo Kelly can use ~Nov 2026; nothing in it depends on
the runner extraction. The Unity runtime-fetch leg waits for the Phase F content layer.

---

# Phase E - LabConsole & Multiplayer

**Throughline contribution:** the control plane Vicky will ACT through (H), and the multiplayer substrate; vitals
become typed, replicated parameters. Consumer-visible: **additive, opt-in** (`LabConsoleMigrator` per-lab dry-run;
AR unaffected).

- [ ] LabConsole control plane: typed `ConsoleParameter` store + `IEffectHandler<T>` + declared `ConsoleAction`s
      (auto-exposed later to professor panel + Vicky tool catalog); `LabConsoleMigrator` (opt-in, dry-run).
- [ ] Fusion replication of console parameters in optional `Capabilities.VR.Fusion` (spec §9.8 StateAuthority model).
- [ ] **Multiplayer-into-DevKit (was domain system D1; §28.2):** relocate `NetworkedStates` (`NetworkStateManager`
      fact store + `NetworkedInteractionRelay` + listeners/triggers) into the Phase A reserved `Networking` module;
      wrap behind `IScenarioFlowStore` (`IsStepComplete => GetState`, `CompleteStep => SetStateTrue`,
      `StepFactChanged` off the existing edge-detection); the extracted runner fast-forwards on late-join (today each
      peer runs from idx 0). Move the Make-Multiplayer author-time rig. Only VR-Shell scripts stay outside DevKit.
- [ ] **Design decision (Petros/CTO):** widen the boolean-only fact value (correct/wrong/score/branch) vs encode as
      multiple keys - decide BEFORE Vicky must reason over outcomes in Phase G/H.
- [ ] **Multiplayer single-writer rule** (spec §28.7): per-learner attempt analytics emit locally per peer;
      observation/scenario snapshots come from exactly ONE writer (the StateAuthority). Written into the authoring
      docs before any Fusion lab emits.
- [ ] Vitals replicate as LabConsole parameters; `ControlOptionManager` converges off PUN onto the Fusion path.

---

# Phase F - Authoring, Objectives & Content Systems

**Throughline contribution:** the authoring engine 2028's prompt-to-simulation drives, the deterministic objectives
Vicky's judgments sit beside, and the EU-scale localization moat. Consumer-visible: **additive, productivity win**.

- [ ] Hub v2 + Building Blocks + Simulator + Live State Inspector + Action Log Viewer (spec §13).
- [ ] **Building-block library growth** (content lane - Phoebos/Georgia): grow from starter blocks toward a real
      composition vocabulary; every new prefab carries its `BuildingBlockMetadataV1` sidecar (the launch Phase C
      cutover added stubs). This library IS the currency of 2028 prompt-to-simulation - it needs an owner + cadence.
- [ ] Objective evaluator live for Tier 1 + Tier 2 (deterministic, no LLM) -> `ObjectiveMetEvent` /
      `ObjectiveFailedEvent` / `AttemptCompletedEvent`; portal ingests `AttemptSummaryV1` (Web Portal lane must land
      alongside - governance boundary 1). Tier-3 prompt inspector ships; routing waits for G.
- [ ] **AI-authoring command library + CLI/MCP bridge (was domain system D4; §28.5):** standardize the apply-commands
      (Import Scenario, Load Patient Profile, Import Translation Set, Export-Lab-as-Fixture) into one Hub library;
      every command gets a headless entry; the bridge drives `generate -> apply -> Evaluate Changes -> report`.
      Document the JSON schemas (ScenarioV1, analytics/Action config, PatientVitals profile, translation set) as the
      contract surface Claude drafts against. *This loop is the prompt-to-simulation embryo.*
- [ ] **Localization cloud completion (was domain system D2; §28.3):** ElevenLabs audio per `(key, lang)`; runtime
      fetch-by-language (map the LabCardUI cover-texture coroutine to `UnityWebRequestMultimedia.GetAudioClip`);
      Web-Portal lab-content editor; cloud resolver becomes canonical, baked StringTables demote to offline fallback.
- [ ] **Vitals digital-twin completion (was domain system D3; §28.4):** cascade rules (low pulse -> pale skin);
      ScriptableObject patient profiles (a patient authored in minutes); migrate the ~8 scattered physiology scripts
      onto `PatientVitals` with parity proven on the net.
- [ ] **JSON source-of-truth migration (decision + execution):** flip the scenario from `[SerializeReference]`-canonical
      /JSON-projection to **JSON-canonical** with an OFFERED (never forced) per-lab migration, gated on the
      golden-trace net. 2028's Vicky must author against the truth, not a projection. If deferred beyond F, the
      deferral + new home must be written here explicitly.

---

# Phase G - Vicky Observer (Vicky enters the lab - read-only)

**Throughline contribution:** the first rung of 2027 - **observation**. Consumer-visible: **additive, opt-in per
learner**, consent-gated. Governance boundary 2 (consent UI, transparency notice, revoke control, per-learner audit
export) triggers HERE.

**Already built (do not re-scope as greenfield):** the transport half - `Pitech.XR.AgentSubstrate` (emitter + DI
seams + UnityWebRequest client + queue/classifier/retry + fail-closed config + 19 EditMode tests), bound to the
FROZEN `AgentObservationV1` -> `agent-observation` edge fn (PIT-274/336/388). The contract gate is **GREEN**.
Carry-forward gotchas: `JsonUtility` cannot serialize `Record<string,unknown>` (hand-rolled writer pattern); the
CS0122 lesson (tests must compile under the Evaluate-Changes gate).

- [ ] **The producer half:** `ObservationProjector` subscribing to the LabEventBus (Phase D) on a ~500ms pulse +
      semantically-significant events; composes the semantic snapshot; `PatientVitals` (Phase B foundation + Phase F
      twin) implements `IAgentStateSource` - the patient state Vicky reads.
- [ ] Producer-side validation (the parked review findings): validate-at-source (non-empty summary; UUID-or-null
      ids; normalized surface), retry-taxonomy split (permanent vs transient), OnEnable re-pump, static consent gate.
- [ ] Wire `AgentObservationBootstrap` into AR + VR bootstrap scenes (the deferred manual step).
- [ ] Consent: per-user observation consent UI in the SURFACES (mobile app + VR Shell - NOT DevKit; DevKit reads
      host-provided consent state); Web-Portal consent storage + persistence behind the (now-persisting) edge fn.
- [ ] Tier-3 routing live (proposal -> human-review queue -> professor confirmation); per-scenario cost
      (`ScenarioCostV1`) tracked warning-only along the observation channel.
- [ ] Vicky-as-Observer end-to-end demo: Vicky narrates/understands a live lab session read-only.

---

# Phase H - Vicky Interactive & the Director foundation (Vicky acts - gated)

**Throughline contribution:** rungs 2-4 of 2027 - **guidance, Q/A, and the foundation for handling scenarios**.
Consumer-visible: **additive, pilot-cohort feature flag**. Governance boundary 3 (per-action audit, rate limit +
circuit breaker, ethics review) triggers HERE. **The 6-month VICKY-in-AR/VR milestone closes here.**

- [ ] `GatedLabActionSurface` with all 5 gates (Consent -> Role -> Rate -> ContentSafety -> Audit); the
      `AgentToolCatalogBuilder` exposes declared ConsoleActions to Vicky's tool list.
- [ ] First 3 conservative ConsoleActions (`offer_hint_current_step`, `ask_check_question`, `replay_current_step`);
      one Tutor demo lab + one Examiner demo lab; **two pilot cohorts inside the Feb-May 2027 teaching window** (the
      semester cliff - a 6-week slip costs a semester).
- [ ] **External gate (Petros 2026-06-10: lives VICKY-side under the World-Aware Agent Bridge workstream,
      post-launch):** the `AgentActionRequestV1` typed action contract - mirror the PIT-274 contract-first pattern
      (freeze + 501 stub), target frozen ~Jan 2027 so the Feb-May pilot window holds. File it at Phase D start.
- [ ] Ethics review of the ConsoleAction set; runtime permission model.
- [ ] **The Director foundation (Petros, 2026-06-10 - "handling scenarios" gets a name):** define **VickyMode.Director**
      - the mode in which Vicky DRIVES a scenario (advance/branch/pause/parameterize), not just advises. In THIS
      phase: (a) the **flow-control action vocabulary** (`advance_step`, `branch_to`, `pause_scenario`,
      `set_parameter`) defined as gated ConsoleActions routed through LabConsole -> `ISceneRunnerControl` /
      `IScenarioFlowStore` - the seams Phases A/D/E built; (b) Director listed in the VickyMode enum + the mode x
      surface matrix; (c) one internal Director prototype on a demo lab. **Full Director ships post-H as its own
      plan** - this phase lays the foundation so nothing has to be rebuilt for it. No big extra work; the rails
      already point here.

---

# Phase I - v1.0 API lock

- [ ] Public-API baseline rebased; deprecations resolved or documented; CHANGELOG consolidated.
- [ ] Consumers (AR, VR, Mobile UaaL) pin **v1.0.0**. Per spec §28.7: the Sep-7 LAUNCH lock was a 0.x launch
      baseline; **this** is the real v1.0.0 (the spec §18 SemVer freeze).
- [ ] The offered (never forced) SceneManager -> LabConsole-native lab migration tool ships; HealthOn labs migrate on
      Petros's schedule; SceneManager retires after.
- [ ] 1-week soak; then the 2027 expansion (voice I/O, visual agent presence) and the 2028 prompt-to-simulation
      program plan off this baseline.

---

## After Phase I - the horizon (how this reaches the master strategy)

- **2027 - Vicky in AR/VR labs:** observation (G) -> guidance + Q/A (H, Tutor/Helper) -> handling scenarios
  (Director, fully shipped post-H on the H foundation). Voice I/O + visual presence ride as v1.x capability additions
  (spec §10.8 - dates there are stale; this plan's sequencing governs).
- **2028 - Vicky generates labs from a prompt/PDF:** `VickyMode.LabAuthor` composes `ScenarioV1` + `LabConsoleV1`
  against the `BuildingBlockMetadataV1` library (grown through F) -> the D4-built apply-command/CLI loop applies it ->
  the Evaluate-Changes + golden-trace net proves the generated lab safe -> Binding Sheet ids bind it to a scene ->
  the localization cloud delivers it in any language. Every piece exists by Phase I; 2028 is composition + scale, not
  new architecture.
- **= the VICKY master strategy** (Web Portal `docs/ai/1-STRATEGY/VICKY_MASTER_STRATEGY_AND_ROADMAP.md`).

---

## Open decisions for Petros (logged; decided at the owning phase's plan)

- Fact value type widening (Phase E) - widen `NetworkBool` vs multi-key encoding.
- `ScenarioCostV1` ceiling promotion warning -> enforcement (Phase H call, on real distribution).
- The exact ConsoleAction set for the H pilots (ethics review input).
- Localization cloud-resolver cutover timing (when baked tables demote; Phase F).
- VR direct-cloud transport: HTTPS POST vs Supabase REST (Phase G plan).
- JSON source-of-truth flip timing (Phase F default; explicit deferral note if moved).

---

## Status & Progress Log

| Date | Phase/WS | Event | By |
|---|---|---|---|
| 2026-06-10 | - | Restructured into lettered Phases D..I (arch-P2..P7 naming retired); foundation folded into WS D1; localization-cloud parallel track added; Director named in Phase H (Petros directives) | Claude (board) |
| 2026-06-09 | - | First consolidation of the archived roadmap + the four §28 domain systems; filed as PROPOSED (since RATIFIED 2026-06-10) | Claude (board) |

---

## Plan self-review (coverage check)

- [ ] The lettered phases D..I map 1:1 to spec §17 P2..P7 (mapping stated in Terminology + spec §28.7); no doc in the
      live set uses bare "P2/P3" for a phase.
- [ ] The orphaned old-foundation deliverables (LabEventBus, registries, LabRoot, capability interfaces, contract
      discipline) are explicitly homed in WS D1, with the archived foundation plan named as reference detail.
- [ ] The four §28 domain systems are folded into phases (multiplayer -> E; localization-cloud -> D-parallel + F;
      vitals twin -> F (+E replication, +G observer feed); AI-authoring -> F) - no separate D1..D4 track list survives.
- [ ] The already-built observer transport half is recorded as partly-shipped with the exact producer gap + gotchas.
- [ ] The three governance boundaries + the VICKY external gates (incl. `AgentActionRequestV1` with a file-NOW note)
      are carried; the semester cliff is named as the binding 2027 constraint.
- [ ] Director (handling scenarios) has a named home (Phase H foundation) and rides existing seams - no rebuild.
- [ ] The JSON source-of-truth migration has a home (Phase F) - the umbrella's promise is no longer dangling.
- [ ] Sizing is flagged as needing per-phase re-estimation under AI-agent velocity.

---

## Execution handoff

**Plan-of-record owner:** Heisenberg (CTO). **Per-phase executor:** the DevKit lead + the Agent Substrate sub-track
(G/H) + Web Portal/Lovable for the cloud lanes (localization cloud, portal ingestion, consent storage).
**Status:** RATIFIED by Petros 2026-06-10, pending Stergios sign-off; ratification: Petros + Petros's Claude + LooPi -> Heisenberg/Stergios -> dispatch.
Each phase gets its own implementation plan (foundation format: WS + checkboxes + log) at phase start. Local edits
only; **Petros runs git**. Nothing starts before the 2026-09-10 launch is green, except the named Phase D parallel
localization-cloud track (Web-Portal lane).
