---
status: planned
date: 2026-05-08
author: Heisenberg (CTO)
ticket: PIT-183
supersedes: none
references:
  - E:/Unity files/Pi tech DevKit/docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md (§17 migration plan, §13.8 runtime substrate, §9.7 content delivery)
  - E:/Unity files/Pi tech DevKit/docs/plans/2026-04-23-p1-foundation.md
  - C:/Users/petpa/Projects/Web Portal/healthon-reactify/docs/ai/VICKY_MASTER_STRATEGY_AND_ROADMAP.md (§13.8 World-Aware Agent Bridge, §19 World-Aware VICKY invariants, §14.5 Embedded XR Instructor)
  - C:/Users/petpa/Projects/Web Portal/healthon-reactify/docs/ai/plans/2026-04-25-vicky-architecture-v10.md
---

# DevKit Phase 2 Behavior Roadmap (post-P1)

> **Scope.** This document is the post-P1 roadmap for DevKit. P1 (the v0.11 Foundation, currently in flight per `2026-04-23-p1-foundation.md`) ships the 30-asmdef topology with **zero behavior change** for HealthOn AR/VR consumers. This plan covers what ships after P1 and the cross-area contracts that gate each step. It is **not** a re-design — DevKit's 7-phase migration plan is already locked in `2026-04-23-devkit-1.0-target-architecture-design.md` §17. This document consolidates that plan, fills the gaps the audit on 2026-05-08 surfaced (Content Delivery Manager pattern, per-scenario cost tracking, runtime/observation plugin spec details), and defines the cross-area dependencies on the active VICKY V10 refactor and VICKY Phase 2.

---

## 1. Source of truth and what this plan is not

The DevKit 1.0 target architecture design (`docs/specs/2026-04-23-devkit-1.0-target-architecture-design.md`) is authoritative for the 7-phase migration plan, the layer model, the schema contracts, the capability inventory, and the non-breaking evolution contract. **This plan does not override the spec.** Where this plan adds detail, the spec's invariants still apply.

This plan is what changes:

- consolidates the post-P1 phase ordering with explicit behavior-change boundaries
- fills three gaps the 2026-05-08 audit (PIT-183) called out: Content Delivery Manager pattern around `ContentDeliverySpawner`, per-scenario cost tracking, runtime/observation plugin spec wire format
- defines the cross-area dependency boundary between DevKit P5+ and VICKY V10 close + VICKY Phase 2 §13.8 (the World-Aware Agent Bridge MVP)
- is the document that becomes the **DevKit Phase 2 Goal** in Paperclip once ratified (per ticket PIT-183 closure note)

This plan is not:

- a substitute for per-phase implementation plans. Each of P2…P6 will get its own implementation plan in `docs/plans/` at the start of its phase, modeled on the P1 plan.
- a redesign of the architecture. The 7 layers and the non-breaking contract stand.
- an authority over what VICKY ships in V10 or Phase 2. It states what DevKit needs from VICKY; the VICKY side governs sequencing on its end.

---

## 2. Behavior-change roadmap — ordered phases

The spec defines 7 phases, P1 → P7. P1 is in flight. The behavior-change properties of each subsequent phase, expressed as the consumer-visible delta, are:

| Phase | Version | Consumer-visible behavior change | Cross-area dependency |
|---|---|---|---|
| **P2** | v0.12 | **None observable.** Step runner extraction with `[MovedFrom]`. `SceneManager` becomes a 150-line facade. Every v0.10 fixture replays identically (Gate 3 golden replay turns on). Internal restructure only. | None. Pure DevKit-internal. |
| **P3** | v0.13 | **Additive, opt-in.** Lab Console lift ships full parameter + effect + action types. VR consumer gets `LabConsoleMigrator` (per-lab, opt-in, dry-run). AR sees no change (no Lab Console today). Fusion replication ships in optional `Capabilities.VR.Fusion` sub-package. | None. DevKit-internal + VR consumer adoption. |
| **P4** | v0.14 | **Additive, productivity win.** Authoring layer polish (Hub v2, Building Blocks, Simulator, Live State Inspector, Action Log Viewer). **Objective evaluator goes live for Tier 1 + Tier 2** — `DefaultObjectiveEvaluator` publishes `ObjectiveMetEvent` / `ObjectiveFailedEvent` / `AttemptCompletedEvent`. Portal ingests `AttemptSummaryV1`. Tier 3 prompt inspector ships but routing deferred. | **Portal ingestion of `AttemptSummaryV1`** must land alongside this phase (Web Portal team work). |
| **P5** | v0.15 | **Additive, opt-in per learner.** Agent Substrate observer ships (substrate-observer emitter slice **shipped 2026-05-19** via [PIT-336](/PIT/issues/PIT-336) / [PIT-388](/PIT/issues/PIT-388); end-to-end smoke against PIT-NEW-A persistence pending). VICKY-as-Observer end-to-end through UaaL bridge to mobile app to portal. Tier 3 routing live (proposal → human-review queue → professor confirmation). VR direct-cloud telemetry available. | **Hard dependency on the runtime substrate contract from VICKY Phase 2 §13.8** — see §4 below. P5 cannot exit before the `agent_observation` event contract is live end-to-end on the edge function with verified tenant scope and consent state. |
| **P6** | v0.16 | **Additive, pilot-cohort feature flag.** Agent Substrate actuation. `GatedLabActionSurface` with all 5 gates. First 3 `ConsoleAction`s (`offer_hint_current_step`, `ask_check_question`, `replay_current_step`). One Tutor demo lab + one Examiner demo lab to one pilot cohort each. **6-month VICKY-in-AR/VR milestone closes here.** | **Hard dependency on the typed action channel** + **runtime permission model** from VICKY Phase 2 (per §19.5 "Runtime action category" revisit trigger). |
| **P7** | v1.0 | **None.** API baseline rebases. CHANGELOG consolidates. Consumers pin v1.0. | None. |

### 2.1 The behavior-change boundaries that matter for governance

Three boundaries trigger consumer team action and/or board approval:

1. **P3 → P4 boundary (objective evaluator goes live).** The first phase where DevKit emits structured events that downstream systems (portal, audit) ingest as production data. Triggers: portal ingestion contract review; per-tenant rollout policy; first-touch validators ship.
2. **P4 → P5 boundary (VICKY enters the lab).** The first phase where VICKY observes learner runtime behavior. Triggers: consent UI ship in mobile app; transparency notice; revoke control; per-learner audit export wired in portal. **Cannot ship without VICKY runtime substrate contract** (see §4).
3. **P5 → P6 boundary (VICKY acts in the lab).** The first phase where VICKY mutates lab state. Triggers: pilot-cohort feature-flag rollout; per-action audit; rate limit + circuit breaker active; ethics review of the `ConsoleAction` set.

Every boundary above goes through the Pi tech 6-stage Engineering Review policy with the CTO at Stage 2 (plan-review) and Stage 4 (code-review join on HIGH-RISK slices).

---

## 3. Three audit gaps closed in this plan

The 2026-05-08 roadmap audit (PIT-183) called out items the P1 plan defers without specifying where they land. This plan slots each one explicitly.

### 3.1 Content Delivery Manager pattern around `ContentDeliverySpawner`

**Where it lands:** P2 (v0.12), as part of step runner extraction. Same `[MovedFrom]` discipline as `SceneManager`.

**What ships:**

- `ContentDeliverySpawner` (the 1,180-line MonoBehaviour) keeps its v0.10 public surface as a facade.
- Internally, the 4-way decomposition the spec already defines in §9.7 ships:
  - `Pitech.XR.Capabilities/IContentDeliveryRuntime` (capability interface)
  - `Pitech.XR.Capabilities.Default/AddressablesContentDeliveryRuntime` (default impl)
  - `Pitech.XR.Authoring/ContentDeliverySpawnerEditor` (existing, kept)
  - In-scene MonoBehaviour delegates to the capability via `XRServices` (P1 shim) → `CapabilityRegistry` (post-P1).
- Gate 2 (v0.10 fixture load) and Gate 3 (golden replay) verify zero behavior delta. Every v0.10 lab using `ContentDeliverySpawner` continues to function unchanged.

**Why P2 not later:** the decomposition is the same pattern as `SceneManager` extraction (facade preserves, runners extract). Doing them in the same phase keeps the non-breaking contract verification under one milestone and prevents two passes of fixture/golden-replay churn.

### 3.2 Per-scenario cost tracking

**Where it lands:** P5 (v0.15) — first phase where VICKY makes paid LLM calls inside a lab. Tracked in the same `tool_call_events` / observation pipeline.

**What ships:**

- A `ScenarioCostV1` schema contract in `Pitech.XR.Domain` (versioned, AOT-preserved per the Domain rules).
- The Agent Substrate observer reports per-attempt cost (input tokens + output tokens + cached-token hits + provider) along the existing `agent_observation` event channel — additively, not as a new transport.
- Portal ingestion lands the cost rows alongside the existing `tool_call_events`. Per-tenant + per-lab + per-scenario cost rollup is a Web Portal admin dashboard deliverable.
- Per-attempt cost ceiling enforcement (circuit-break on cost overrun) ships as part of the P5 rate-limit + circuit-breaker system per spec §10.7. **Warning-only at P5 ship; promotion to enforcement is a P6 decision based on real distribution.**

**Why P5 not earlier:** there is no LLM call from inside a lab before P5. P4's objective evaluator is deterministic (Tier 1 + Tier 2 — no LLM). Tier 3 routing lands in P5 with Examiner mode and is the first paid call.

**Why not P6:** observability before actuation. P5 reads cost; P6 acts on it (pilot-cohort cost cap enforcement).

### 3.3 Runtime / observation plugin spec — wire format and ownership

This is the cross-area item. It belongs to both DevKit and VICKY. The contract itself is owned by **VICKY** (it's a Web Portal Edge Function ingestion contract); DevKit is the first runtime implementation.

**The contract VICKY owns** (per VICKY master roadmap §13.8 MVP and §19.2 invariants):

- `agent_observation` event type, versioned, JSON, with tenant scope + consent state on every payload
- Edge function endpoint that validates tenant scope and consent state before persistence
- Persistence schema in Supabase with RLS policies
- Admin observability view for shipped observations
- A versioned `agent_action_request` follow-on for P6 (deferred until §13.8 MVP green)

**The runtime side DevKit owns** (per spec §10.5, §11.5):

- `Pitech.XR.AgentSubstrate.Observation.IObservationProjector` (zero-alloc, pull-not-push)
- `UaaLBridgeObserver` (Android UaaL transport from Unity → mobile app → edge function)
- `Capabilities.VR.Telemetry.DirectCloudQueuedTelemetryService` (VR direct-cloud transport, bypassing UaaL — needed because VR has no mobile host)
- The semantic projection: lab state → `agent_observation` payload that represents semantic state, not raw renderer text (per §19.2 invariant 3)
- The consumer-side consent gate (refuses to project if consent state absent — matches VICKY-side refusal at the edge)

**Wire format target (DevKit-side schema, mirrors VICKY edge contract):**

```jsonc
{
  "schema": "agent_observation/v1",
  "tenant_id": "<uuid>",                    // §19.2 invariant 2 — refused if missing
  "consent_token": "<opaque>",              // §19.2 invariant 1 — refused if missing/expired
  "lab_id": "<stable-id>",                  // §7.3 stable-id contract
  "attempt_id": "<uuid>",
  "step_id": "<stable-id>",
  "vicky_mode": "Observer|Tutor|Examiner|Helper|Absent",
  "occurred_at": "<rfc3339>",
  "semantic_state": {                       // §19.2 invariant 3 — semantic, not visual
    "active_step_kind": "Quiz|Insert|Selection|...",
    "step_progress": "<typed-by-step-runner>",
    "lab_console_parameters": { /* typed dict, mirrors LabConsoleRuntime */ },
    "objective_outcomes": [ /* ObjectiveOutcomeV1[] */ ],
    "recent_actions": [ /* last N ConsoleActions, no PII */ ]
  },
  "device_meta": {                          // typed, no free text
    "platform": "AR_Android|VR_Quest|Simulator",
    "device_class": "phone|standalone_headset",
    "build_id": "<unity-build-id>"
  }
}
```

**This is illustrative, not a hand-final contract.** The authoritative shape is locked when VICKY Phase 2 §13.8 MVP ships its edge-function contract; DevKit P5 implements against whatever that locks to.

**Status (this plan does NOT lock the contract):**

- DevKit P5 cannot start implementation against a hypothetical contract.
- VICKY Phase 2 §13.8 MVP must ship the contract end-to-end on the edge function before DevKit P5 goes from "planned" to "in flight."
- This plan flags the dependency. It does not propose to author the contract on DevKit's side — that would invert ownership.

---

## 4. Dependencies on V10 close + VICKY Phase 2

DevKit P2, P3, P4 are **independent of VICKY V10** — they are pure Unity-side work (extraction, Lab Console, objective evaluator). They can run in parallel with V10.

DevKit P5 is **gated on:**

1. **VICKY V10 close (architecture refactor of `vicky-unified` Edge Function).** The 22 lifecycle-extraction slices must land before VICKY Phase 2 §13.8 implementation begins in earnest. The `agent_observation` ingestion path is an Edge Function endpoint; introducing it on top of an unrefactored monolithic `index.ts` would make both refactors harder.
2. **VICKY Phase 2 §13.8 MVP.** The `agent_observation` event contract must be defined, versioned, and emitted end-to-end through the edge function with verified tenant scope and consent state on at least one runtime implementation. (DevKit P5 is the candidate first runtime.)
3. **Mobile app consent UI + revoke control.** Mobile is the first transport target (UaaL bridge from Unity → Android UaaL host → mobile app → edge function). Without consent UI, no observation ships.

DevKit P6 is **additionally gated on:**

4. **Typed action contract (`agent_action_request`)** — VICKY Phase 2 follow-on after the §13.8 MVP, per §19.5.
5. **Runtime permission model** — VICKY Phase 2 follow-on, per §19.5.
6. **Pi tech ethics review of the `ConsoleAction` set** — P6 ships actuation; needs sign-off on what VICKY can do mid-lab.

DevKit P7 is **independent** of VICKY (1.0 lock is API freeze + soak).

### 4.1 Ordering implication for VICKY-side scheduling

The VICKY Phase 2 master plan today places §13.8 as a "parallel workstream" — explicitly not gated on a specific WS sequencing. This plan asks for a stronger commitment from the VICKY side:

> **Ask of the VICKY side:** §13.8 World-Aware Agent Bridge MVP should land **between V10 close and Phase 2 close**, not as a Phase 3 spillover. DevKit P5's earliest start = VICKY [PIT-274](/PIT/issues/PIT-274) (PIT-196.2) contract close (NOT full §13.8 close); the PIT-196.2 sub-ticket was filed off [PIT-196](/PIT/issues/PIT-196) and is scheduled to begin ~2026-06-01. DevKit P5 is on the critical path to the 6-month VICKY-in-AR/VR milestone (P6 close per spec §17.1) — slipping §13.8 into Phase 3 would push the moat-relevant milestone by a quarter.

> **Update 2026-05-10.** VICKY §13.8 has been split into two slices: **PIT-196.1 (the surface-axis metadata + frozen decision doc)** landed 2026-05-10 (`VICKY_SURFACE_ARCHITECTURE.md` is the canonical anchor for the mode×surface composition contract). **PIT-196.2 (the runtime substrate contract document)** is the actual DevKit-P5 dependency — earliest-start moves from "VICKY §13.8 MVP green" to "VICKY PIT-196.2 contract green," which is a cheaper gate (contract doc ratification, not full MVP). This narrows DevKit P5's earliest start by removing the §13.8 MVP runtime-side dependency.

> **Update 2026-05-18 — PIT-196.2 gate is GREEN.** [PIT-274](/PIT/issues/PIT-274) shipped the frozen `AgentObservationV1` cross-surface contract (`docs/ai/3-CONTRACTS/AgentObservationV1.md` + `supabase/functions/_shared/contracts/v1/agent-observation-v1.ts` in the Web Portal repo) plus the `agent-observation` edge-function stub that returns `501 NOT_IMPLEMENTED` for shape+auth+tenant+consent+surface-valid requests. DevKit P5 can now integrate against the v1 wire signature today; persistence + RLS + admin observability + consent storage are the Web Portal team's parallel lane (PIT-NEW-A) and do not block P5's emitter implementation work. The P5 substrate observer first-emitter slice itself is tracked as PIT-NEW-B with `blockedByIssueIds: [PIT-274]` resolved at PIT-274 closure.

This is an architectural ask, not a fait accompli. CTO files this as a coordination item in the next architecture-spec reconciliation pass against `ARCHITECTURE.md` and `PLATFORM.md`.

---

## 5. Phase ownership and sizing

| Phase | Owner (DevKit) | Cross-team | Estimated duration | Earliest start |
|---|---|---|---|---|
| P2 | DevKit lead | None | 3–4 weeks | After P1 exit (currently in flight) |
| P3 | DevKit lead | VR consumer team (per-lab migration) | 3–4 weeks | After P2 exit |
| P4 | DevKit lead | Web Portal team (`AttemptSummaryV1` ingestion) + Mobile team (consent UI scaffold) | 3–4 weeks | After P3 exit; needs portal ingestion contract ready |
| P5 | DevKit lead + Agent Substrate sub-track | Web Portal team (edge function `agent_observation`); Mobile team (consent UI live) | 4–5 weeks | After P4 exit **AND** VICKY PIT-196.2 contract green ([PIT-274](/PIT/issues/PIT-274) closed 2026-05-18: `AgentObservationV1` frozen, edge stub returning 501 NOT_IMPLEMENTED — DevKit P5 binds against the v1 signature now) |
| P6 | DevKit lead + Agent Substrate sub-track | Web Portal team (action audit dashboards); Mobile team; ethics review | 4–6 weeks | After P5 exit **AND** typed action contract green **AND** ethics sign-off |
| P7 | DevKit lead | All consumer teams (pin v1.0) | 1–2 weeks | After P6 exit + 1-week soak |

Total DevKit Phase 2 duration: **18–25 weeks** (P2 → P7), spec §17.1 estimate stands at ~24 weeks.

If the VICKY-side §13.8 MVP slips into Phase 3, P5 cannot start. P2 / P3 / P4 still ship in parallel. The 6-month VICKY-in-AR/VR milestone slips one VICKY phase.

---

## 6. Risks specific to this roadmap (additive to spec §21)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| VICKY §13.8 MVP slips beyond V10 close | Medium | High (P5 cannot start) | Track §13.8 as a hard blocker on DevKit P5 in Paperclip via `blockedByIssueIds`; CTO joins VICKY architecture reviews to flag the dependency early; if §13.8 slips, P2/P3/P4 still ship |
| Mobile consent UI scope creep blocks P5 | Medium | High | Scope consent UI scaffold to P4 deliverable, not P5; production scope is small (consent screen + revoke control + transparency notice) — defer richer audit views to P6 |
| Cost-tracking ceiling triggers false circuit-breaks under normal Tier 3 use | Low | Medium | P5 ships ceilings as warning-only; promotion to enforcement is a P6 decision based on real distribution |
| `ContentDeliverySpawner` decomposition surfaces a hidden contract on `Editor` package | Low | Medium | Capture `ContentDeliverySpawner` as a v0.10 fixture-corpus member in P1 (P1 plan §3.1 already covers); Gate 2 catches |
| Per-scenario cost rollup needs schema evolution before P6 | Low | Low | `ScenarioCostV1` is V1-suffixed; future evolution uses `[MovedFrom]` per the non-breaking contract |
| Tier 3 routing in P5 surfaces a deadlock with Web Portal `pending_human_review` queue | Low | Medium | Stand up the queue surface as a P4 deliverable (decoupled from VICKY proposing); validates queue under deterministic-only load first |
| Ethics review on `ConsoleAction` set produces additional gate requirements at P6 | Low | Medium | Reserved `ContentSafetyGate` slot in §10.3 absorbs; new gates slot additively |

---

## 7. What this plan does NOT decide

- **The exact `ConsoleAction` set for P6 demo labs.** Spec §22 reserves this for per-phase plans during P5/P6.
- **The consent-UI visual design.** Mobile app design review during P4 (one phase earlier than spec §22 recommends — moved up because P5 cannot ship without it).
- **The portal `pending_human_review` queue UI.** Web Portal team deliverable. This plan asks the queue to land in P4 (one phase earlier than spec §22 recommends).
- **The exact `agent_observation` payload shape.** VICKY-side §13.8 MVP locks it. The shape in §3.3 above is illustrative only.
- **Whether `ScenarioCostV1` is the right name and granularity.** Open for P5 implementation plan review.
- **Whether VR direct-cloud telemetry uses HTTPS POST or Supabase REST.** P5 implementation plan decides.

---

## 8. Closure: what to do with this plan

Per ticket PIT-183: once ratified, this plan becomes the **DevKit Phase 2 Goal** in Paperclip. Goal title proposal:

> **DevKit P2–P7 — post-foundation behavior roadmap to v1.0 lock and 6-month VICKY-in-AR/VR milestone.**

Sub-issues queued under that Goal at ratification time (created when their phase becomes the next-up):

- DevKit P2 implementation plan (drafted at P1 exit)
- DevKit P3 implementation plan (drafted at P2 exit)
- DevKit P4 implementation plan (drafted at P3 exit; coordinates with portal team for `AttemptSummaryV1` ingestion + with mobile team for consent UI scaffold)
- DevKit P5 implementation plan (drafted when VICKY §13.8 MVP is on track; `blockedByIssueIds` includes the §13.8 tracking issue)
- DevKit P6 implementation plan (drafted when P5 is on track; `blockedByIssueIds` includes the typed-action-contract tracking issue + the ethics-review tracking issue)
- DevKit P7 implementation plan (drafted when P6 is on track)
- Cross-area: VICKY §13.8 MVP coordination (CTO-owned tracking issue with VICKY-side dependency)

Ratification path: CTO drafts (this document) → Petros (board) ratifies via `request_confirmation` on PIT-183 → Goal filed → sub-issues queued at the appropriate phase boundaries (not all at once).
