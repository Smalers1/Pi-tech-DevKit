# DevKit + Web Portal Hardening Plan

> Companion to [devkit-web-portal-scalability-review.md](devkit-web-portal-scalability-review.md).
> This is **not** an architecture rewrite. It is a prioritized, additive, reversible
> hardening plan that strengthens the foundation while keeping the shipping
> HealthOn AR/VR labs working at every step.

---

## Guiding Principles

1. **Working paths stay working.** Never replace a live path; add beside it, prove equivalence, then switch.
2. **Gates before changes.** No structural change ships until a test proves the old behavior still holds.
3. **Additive and reversible first.** Do the cheap, high-leverage, low-risk work now. Defer expensive structural work until a concrete driver exists.
4. **No speculative abstraction.** Don't build extension points for usage that doesn't exist yet.
5. **One source of truth per data shape.** Shared contracts, not parallel definitions that drift.

### Sequencing rule

```
Stabilize  ->  Contracts  ->  Telemetry interface  ->  (defer structural work until a driver appears)
```

---

## Phase 0 — Stabilization & Safety Net  *(DO FIRST — blocks everything else)*

**Goal:** Make change safe. Today any refactor is risky because behavior is under-tested. This phase is not a refactor; it is insurance.

**Tasks**
- [ ] Inventory the "must-not-break" surface: public DevKit API, v0.10 scene/prefab load paths, scenario step types, telemetry payload shapes accepted by Web Portal.
- [ ] Add compatibility gates (per review §2):
  - old v0.10 scenes still load
  - old prefabs still deserialize
  - old public API still exists (API-surface snapshot test)
  - old scenario steps still execute
  - Unity 6 can compile and open the project
  - telemetry payloads still accepted by Web Portal ingest
  - ContentDelivery tests still pass
- [ ] Wire these gates into CI so a red gate blocks merge.

**Acceptance criteria:** A single CI run proves all of the above green on the current `main`.

**Effort:** M · **Risk:** Low · **Driver:** Always needed. Start here even if nothing else is done.

---

## Phase 1 — Shared Contract Layer

**Goal:** One versioned definition per cross-boundary data shape so DevKit and Web Portal can't drift (review §1).

**Tasks**
- [ ] Define versioned contracts: `LaunchContextV1`, `TelemetryBatchV1`, `StepEventV1`, `LabAttemptV1`, `PublishTransactionV1`, `UnityLifecycleEventV1`, `WebGLBridgeMessageV1`.
- [ ] For each: required fields, optional fields, allowed event names, allowed platform names, version number, backward-compat rules.
- [ ] Decide the source-of-truth location and how both sides consume it (shared package / generated types / schema file).
- [ ] Add a contract-conformance test on both sides (Unity emits → validates; Web Portal ingest → validates).

**Acceptance criteria:** The `attempt_id` vs `attemptId` class of mismatch is caught by a test, not in production.

**Effort:** M · **Risk:** Low (additive) · **Driver:** Now — high value, near-zero blast radius.

---

## Phase 2 — Telemetry Standardization (interface only)

**Goal:** Put `ITelemetryService` in front of existing telemetry without changing what's emitted (review §3).

**Tasks**
- [ ] Define `ITelemetryService` covering current events (attempt started/completed/abandoned, step completed, hint used, reset used, interaction, download progress, critical error).
- [ ] Implement it as a thin wrapper over the **existing** ContentDelivery analytics path — no new emission yet.
- [ ] Route one or two systems through the interface as a pilot (e.g. Scenario, Quiz).
- [ ] **Dedup guard:** ensure old `RuntimeTelemetryAdapter` and new `ITelemetryService` never both emit the same `step_completed` — explicit ownership per event.

**Acceptance criteria:** Telemetry output is byte-for-byte equivalent before/after the interface is introduced (verified by Phase 0 telemetry gate).

**Effort:** M · **Risk:** Medium (duplicate-event hazard) · **Driver:** Now, but only the interface. Broader rollout follows naturally.

---

## Phase 3 — WebGL / Web Portal Bridge Hardening  *(DEFERRED — driver: Web Lab Player)*

**Goal:** Typed, validated, origin-checked bridge for the future Web Lab Player (review §5).

**Trigger to start:** A Web Lab Player is actually scheduled. Until then, do not build this.

**Tasks (when triggered)**
- [ ] Typed messages: `unity_ready`, `launch_context`, `auth_context`, `telemetry_batch`, `lifecycle_event`, `progress_event`, `error_event`, each with `{type, version, requestId, payload}`.
- [ ] Allowed-origin checking (no `targetOrigin: "*"`).
- [ ] Short-lived launch tokens instead of broad auth tokens into the iframe.
- [ ] Incoming-message validation against the Phase 1 `WebGLBridgeMessageV1` contract.
- [ ] Automated end-to-end WebGL launch + telemetry test.

**Effort:** L · **Risk:** Medium · **Driver:** Future (WebGL player).

---

## Phase 4 — Server-Side Analytics Scale  *(DEFERRED — driver: data volume)*

**Goal:** Move broad-fetch + browser-filter to server/DB aggregation (review §6). Web Portal only; does not affect how labs run.

**Trigger to start:** Tenant/attempt/event volume measurably degrades portal analytics responsiveness.

**Tasks (when triggered)**
- [ ] Tenant-scoped SQL views; Supabase RPC functions.
- [ ] Indexed analytics tables; precomputed summaries.
- [ ] Filtered queries by `tenant_id`, `lab_id`, `attempt_id`, `created_at`.

**Effort:** L · **Risk:** Low–Medium (read-path only) · **Driver:** Future (scale).

---

## Phase 5 — Gradual Split of Large Files  *(DEFERRED — driver: maintenance friction, gated on Phase 0)*

**Goal:** Reduce responsibility load on `SceneManager` and large Web Portal components (review §7). The only structurally-invasive phase.

**Trigger to start:** Phase 0 gates are green **and** you are already touching the file for a real feature/fix. Don't do it speculatively.

**Tasks (incremental, one runner at a time)**
- [ ] DevKit: extract step runners from `SceneManager` → `QuizStepRunner`, `CueCardsStepRunner`, `SelectionStepRunner`, `TimelineStepRunner`, `GroupStepRunner`, `ConditionsStepRunner`. Keep old public API intact.
- [ ] Web Portal: split `Lab3DViewer` → launch resolver, iframe bridge, telemetry bridge, auth/permission, UI state.

**Acceptance criteria:** Each extraction lands behind green Phase 0 gates; no public API change.

**Effort:** L · **Risk:** High if done early/untested · **Driver:** Future + opportunistic.

---

## Ownership Per Phase (avoid duplicate-system confusion — review §4)

```
Phase 0: Old behavior runs. Safety net exists.
Phase 1: Contracts exist. Both sides validate against them.
Phase 2: Telemetry interface wraps existing path. Old adapter still owns emission until cutover.
Phase 3+: New foundation pieces plug in only when their real driver arrives.
```

---

## What NOT To Do

- Do **not** start Phases 3–5 before Phase 0 is green.
- Do **not** rewrite the architecture wholesale — there is no present forcing function; the system is production-capable for the current HealthOn use case.
- Do **not** run old and new telemetry emitters in parallel without a deduplication rule.
- Do **not** build the WebGL bridge or server-side analytics ahead of a real driver.

---

## Open Validation (before executing Phases 1–2)

The source review notes it did not fully trace the end-to-end flows. Confirm against actual code first:
- real size/responsibilities of `SceneManager`
- how `RuntimeTelemetryAdapter` is wired and what it emits
- whether DevKit and Web Portal payload shapes actually drift today
- the exact publish → launch → telemetry → WebGL paths

---

## At-a-Glance

| Phase | Work | Effort | Risk | Start when |
|------|------|--------|------|-----------|
| 0 | Compatibility gates + tests | M | Low | **Now** |
| 1 | Shared versioned contracts | M | Low | Now |
| 2 | `ITelemetryService` (interface only) | M | Med | Now |
| 3 | WebGL bridge hardening | L | Med | WebGL player scheduled |
| 4 | Server-side analytics | L | Low–Med | Data volume hurts |
| 5 | Split large files | L | High if early | Phase 0 green + touching the file |
