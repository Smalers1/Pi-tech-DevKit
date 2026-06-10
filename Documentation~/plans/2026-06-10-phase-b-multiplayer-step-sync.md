---
title: Phase B (addendum) - Multiplayer step-sync - replacing the UIStateTrigger switchboard (behaviour-additive, launch-minimal)
status: FOLDED INTO 2026-06-09-phase-b-analytics.md as WS B9 (2026-06-10, Stergios review) - this doc remains the detail/rationale companion; canonical checkbox tracking lives in the phase doc; pending Petros (board) ratification of the addition
date: 2026-06-10
author: Stergios (Claude) - DevKit runtime + cross-surface architecture
owner: Stergios (DevKit runtime + the VR-side bridge) + Alexandros (DevKit lifecycle hook, if any)
phase: B (addendum; proposed WS B9)
gate: Phase A WS A3 net (EditMode equivalence harness + "DevKit > Evaluate Changes") green before this behaviour-additive change lands
references:
  - 2026-06-09-phase-a-refactor-and-foundation.md (Phase A - the WS A3 net that gates this; reserves the Networking module slot; the step-completion lifecycle this publishes from)
  - 2026-06-09-phase-b-analytics.md (Phase B - this is a proposed parallel DevKit-lane WS; §7 currently DEFERS "Multiplayer-into-DevKit" to after-launch - see "Scope honesty" below)
  - 2026-06-09-phase-c-integration-and-ship.md (Phase C - the two-client VR proof + the AR no-Fusion equivalence assertion land here)
  - _after-launch/2026-06-09-after-launch-plan.md (POST-LAUNCH - the full IScenarioFlowStore / flow.* / typed-Fusion graduation this bridge forward-aligns to)
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md (§9.8 authority re-gating; §14 replication; §28.2 Networking module)
  - "_workbench/devkit/PROPOSAL-scenario-v1.0-additions.md §2.1/§2.3 (Stergios SSoT for the after-launch end state: flow.* facts, switchboard retirement)"
  - "HealthOn VR/Assets/Scripts/Multiplayer/NetworkedStates/ (the system this replaces: NetworkStateManager.cs, UIStateTrigger.cs, EventStateListener.cs, TimelineStateListener.cs)"
companion: 2026-06-09-phase-b-analytics.md (the phase this addendum extends)
---

# Phase B (addendum) - Multiplayer step-sync

> **For agentic workers:** this addendum follows the same completion discipline as the Phase B umbrella - every
> small step ticked, none skipped, [HUMAN] steps actively surfaced. Every change here is behaviour-additive and is
> validated by the Phase A **WS A3 net** before it lands.

**Goal.** Replace the hand-wired "EventStep -> `UIStateTrigger.SetStateTrue("Step7Done")`" multiplayer step-sync
pattern with a **minimal, forward-aligned bridge** that publishes step-completion **facts** keyed by the step's own
**guid**, reusing the existing `NetworkStateManager` as the launch transport. It must (1) drop the per-step hand-typed
string wiring, (2) work **exactly as before in non-Fusion labs (AR / mobile UaaL)**, and (3) freeze a contract that the
after-launch `IScenarioFlowStore` / `flow.*` / typed-Fusion graduation swaps the **backend** under, never the labs.

**Governing principle - freeze the CONTRACT now, swap the BACKEND later.** The expensive-to-change thing is the
authoring shape and the publish contract; the cheap-to-change thing is the networking backend. Phase B freezes the
contract; Phase D/E (after-launch) swaps the backend. Nothing authored against this WS is ever re-authored.

---

## Scope honesty (factual heads-up, not a gate)

The Phase B umbrella (`2026-06-09-phase-b-analytics.md` §7) currently lists **"Multiplayer-into-DevKit"** as
**after-launch**. This addendum does **NOT** pull that whole graduation forward. What it lands in Phase B is the
**launch-minimal bridge only** - guid-keyed facts published from the runner lifecycle, backed by the *existing*
`NetworkStateManager`. The full `IScenarioFlowStore` + `flow.*` + typed-Fusion + Barrier + typed late-join graduation
**stays after-launch (Phase D/E)**, exactly as planned.

Because the launch-minimal bridge can live **entirely VR-consumer-side** (see WS B9 scope), it does **not** require a
DevKit serialized change and does **not** compete with the analytics launch-blockers B1-B6. Recommended tag, mirroring
B8: **POST_LAUNCH_IF_RISK / SLIP-ELIGIBLE** - the existing switchboard already works, so this is a quality +
forward-alignment improvement, never a launch-DoD gate. Disposition is the board's call at review.

---

## 1. What this replaces

Step-sync in the VR labs today is a **hand-authored pub/sub over named networked bools** - three pieces, two
hand-typed strings per sync point that silently break if they drift:

| Role | Today | Pain |
|---|---|---|
| **Publish** | `EventStep.onEnter` UnityEvent -> `UIStateTrigger.SetStateTrue()` with a hand-typed `stateID = "Step7Done"` -> `NetworkStateManager.SetState("Step7Done", true)` | A throwaway EventStep node + a separate UIStateTrigger object + a typed string, per sync point |
| **React** | `EventStateListener` reads `GetState("Step7Done")` -> fires a UnityEvent; `TimelineStateListener` gates a timeline on `GetState(requiredStateID)` | The **same string** typed again on the other end - must match by hand |
| **Transport** | `NetworkStateManager` networked `GameStates` dict (Fusion); `[Networked, Capacity(64)] NetworkDictionary<NetworkString<_64>, NetworkBool>` | Works - this part stays |

---

## 2. How a step syncs - exactly

### 2.1 The one durable idea

**Stop hand-typing the name and hand-wiring the UnityEvent. Derive the key from the step's own guid, and publish from
the runner's completion lifecycle.** Everything else - including `NetworkStateManager` itself - stays.

```
key(guid) = "flow.step." + guid        // e.g. "flow.step.5f3e9c..."  (10 + 32-char Unity GUID = 42 chars; under the NetworkString<_64> cap)
```

Because both ends derive the key from the **same guid**, the string-matching problem disappears - there is nothing to
mistype, and no separate publisher node. This is the §2.1-PROPOSAL "authors never type IDs" win in its minimal launch
form, delivered on the `NetworkStateManager` you already trust.

### 2.2 Old -> new, one-to-one

| Today | Replacement |
|---|---|
| `EventStep.onEnter -> UIStateTrigger.SetStateTrue("Step7Done")` | The synced step's completion -> the runner raises its completion lifecycle -> **one** `ScenarioFlowBridge` calls `NetworkStateManager.SetStateTrue(key(guid))`. No extra node, no typed name. |
| `EventStateListener("Step7Done")` | A **guid-keyed** listener - reacts when a peer completes that step. Key derived, not typed. |
| `TimelineStateListener("Step7Done")` | Same: gate the timeline on `GetState(key(guid))`. |

### 2.3 The end-to-end flow

**Authoring.** Mark the step that should sync. In the launch-minimal version this is a **per-scene list of synced step
guids on the bridge** (consumer-side, no DevKit serialized change). The DevKit-side `synced` *flag* on the step is the
after-launch refinement.

**Runtime - publish (completing peer):**
1. The runner completes step S and **advances by its own `nextGuid` locally**. Local advance is NEVER gated on the
   network (the invariant in §3).
2. On completion, the runner raises the **step-completion lifecycle** (`StepExited` / `StepCompleted(guid)` on the
   `LabEventBus` - the ratified spec §8.1 hook the Phase A refactor exposes).
3. `ScenarioFlowBridge` (Fusion-build-only, consumer-side) subscribes to that event. For a synced step it calls
   `NetworkStateManager.Instance.SetStateTrue(key(guid))`.
4. `NetworkStateManager` networks the bool via Fusion's `GameStates` dict. A non-authority peer's write forwards to the
   authority via the existing `RPC_RequestSetState` (today a plain forward; authority **re-gating** is the after-launch
   §9.8 upgrade - noted, not done here).

**Runtime - react (other peers):**
5. Each other peer reacts to the flipped bool through **guid-keyed listeners** - the evolved
   `EventStateListener` / `TimelineStateListener` reading `GetState(key(guid))`. A gated timeline plays, a gated event
   fires; the scene stays consistent.
6. Each peer keeps its **own cursor**. The shared truth is the completion **FACT**, not the cursor (matches §2.1
   PROPOSAL: share append-only completion truth, not a shared pointer - local cursors may legitimately diverge).

**Semantics.** **AnyCompletes** at launch: the first peer to complete a shared step flips the fact, every peer reacts.
Barrier (all-arrive rendezvous) is the after-launch `flow.*` counter, not launch.

**Late join.** `GameStates` is a persisted networked dict, so a late joiner reading `GetState(key(guid))` sees facts
already true -> basic late-join **for free**, same as today. Typed fast-forward (replay to the correct step) is the
after-launch refinement.

### 2.4 The bridge - the whole thing (illustrative; C# 9)

```csharp
// VR-consumer-side, Fusion-build only. ~30 lines. NOT in the DevKit package (NetworkStateManager is consumer-side).
#if PITECH_HAS_FUSION
public sealed class ScenarioFlowBridge : MonoBehaviour
{
    [SerializeField] private List<string> syncedStepGuids = new List<string>();  // launch-minimal: per-scene opt-in

    private HashSet<string> _synced;

    private void Awake() => _synced = new HashSet<string>(syncedStepGuids);

    private void OnEnable()  => LabEventBus.StepCompleted += OnStepCompleted;     // the Phase A lifecycle hook
    private void OnDisable() => LabEventBus.StepCompleted -= OnStepCompleted;

    private static string Key(string guid) => "flow.step." + guid;               // <= 64 chars; matched by guid, never typed

    private void OnStepCompleted(string guid)
    {
        if (!_synced.Contains(guid)) return;
        // Fire-and-forget side effect for peers. Local advance already happened (§3). No-op if no authority/session.
        if (NetworkStateManager.Instance != null)
            NetworkStateManager.Instance.SetStateTrue(Key(guid));
    }
}
#endif
```

The forward-aligned graduation is a **one-line body swap** in Phase D/E:

```csharp
// after-launch: same publish point, same key, same semantics - different backend
flowStore.CompleteStep(guid);   // IScenarioFlowStore -> flow.* over INetworkReplicationService -> typed Fusion
```

### 2.5 One switchboard, one budget (launch)

`NetworkStateManager` is a hard **singleton** (`Spawned()` despawns a second instance), so scenario step facts and
general scene states **co-locate in the same `GameStates` dict at launch**, sharing the one `Capacity(64)` budget.
They stay logically distinct by **namespace**: `"PuzzleSolved"` (authored scene state) vs `"flow.step.<guid>"` (step
fact). The prefix is the seam - it separates the concern logically now so the after-launch graduation can split it
physically (step facts move to their own compact `flow.*` bitmask surface; the switchboard retires per PROPOSAL §2.3).
Do NOT build a second string-dict manager for scenario in between - launch cost plus rework, worst of both.

**Measured baseline (2026-06-10, VR repo scenes):** busiest scene (`DIPAE Nosileutiki Meta`) = 34 wiring points but
only **16 distinct state names**; all other scenes far smaller; `defaultStates` lists ~empty. So worst-case headroom
today is **~48 slots** for synced step facts. Comfortable for opt-in checkpoint syncing; NOT safe to leave unwatched,
because **overflow is a runtime failure on-device** - Fusion networked collections are fixed-size (exceeding capacity
errors at `Set` time), and `SetState`'s `Debug.Assert` checks only string length, never capacity (and is stripped in
release builds). The budget must therefore be enforced at **design time** (the WS B9 budget validator), never
discovered at runtime.

**Mitigation ladder if a scene approaches the ceiling:** (1) sync-discipline - only checkpoint/rendezvous steps are
synced, never all steps; (2) prune stale scene states; (3) bump `Capacity(64) -> Capacity(128)` as a **recorded
stopgap** (one line, but a coordinated all-peers build change, and it grows every snapshot - tech-debt the bitmask
retires); (4) the real fix is the after-launch `flow.*` bitmask surface (no string keys, ~4 `[Networked] ulong` words
= 256 steps).

---

## 3. The non-Fusion / AR guarantee - "exactly as before"

This falls out of one invariant plus the existing null-guards; it is **provable, not hoped**.

**The invariant (freeze it; the WS A3 net protects it): a step's local advance may NOT read-gate on a shared fact.**
This is the dual of the §2.1-PROPOSAL write rule ("a local-region step may not write a shared `flow.*` fact another
peer gates on"). With it, the missing network has nothing to stall -> an AR / no-session hang is **structurally
impossible**. Add a **validator** that fails authoring if a step's advance condition reads a shared fact.

**Why AR is already safe.** In a non-Fusion build there is no Fusion, so `NetworkStateManager.Instance` is null, and
every NetworkedStates entry point already short-circuits on that:
- `UIStateTrigger.SetStateTrue()` -> guarded -> no-op
- `EventStateListener` / `TimelineStateListener` -> `if (Instance == null) return;` -> never fire

So the entire sync layer is **already inert in AR today**, and AR labs run purely on the local scenario flow. Anything
gated on `NetworkStateManager` therefore **already** does nothing in AR - meaning **no AR lab can depend on it**, so the
replacement cannot regress AR.

**At each layer:**

| Layer | Non-Fusion / AR behaviour |
|---|---|
| **Today** | `Instance == null` -> UIStateTrigger / listeners no-op; lab runs on local flow. |
| **Launch-minimal bridge (this WS)** | `ScenarioFlowBridge` is `PITECH_HAS_FUSION`-gated and consumer-side -> in an AR build it **is not compiled in**. The DevKit completion lifecycle simply has no subscriber -> no-op. AR labs no longer even carry the dead UIStateTrigger wiring. Strictly cleaner, identical observable behaviour. |
| **After-launch flow-store** | The runner calls `flowStore.CompleteStep(guid)` against `LocalScenarioFlowStore` - in-memory, `IsLocalAuthority` always true, no NetworkObject. Per §2.1 the write-then-read "collapses to exactly today's single-player advance - zero overhead." |

The DevKit package stays **Fusion-agnostic** - it only raises the completion lifecycle and knows the guid; it never
references `NetworkStateManager`. Only the consumer-side, Fusion-gated bridge does. That keeps the AR build clean and
the dependency direction correct (`NetworkStateManager.cs` is VR-local, no namespace, `: NetworkBehaviour`).

---

## 4. Binding with Petros's three phases

| Phase | Contribution to step-sync |
|---|---|
| **A - behaviour-neutral foundation** | Adds **no** sync. (a) Locks today's EventStep -> UIStateTrigger -> NetworkStateManager behaviour AND the AR local-flow behaviour under the **WS A3 net** -> the replacement is later provably equivalent. (b) The refactor exposes the clean **step-completion lifecycle** (`LabEventBus` §8.1) the bridge publishes from. (c) Reserves the `Pitech.XR.Networking` (Make-Multiplayer) module slot (§28.2). **TRAP avoided:** do NOT pre-bake `IScenarioFlowStore` here - this WS uses **no public flow type**. |
| **B - first behaviour-additive (this WS, gated on the A3 net)** | Introduce `key(guid)` + the consumer-side `ScenarioFlowBridge` publishing from the lifecycle; evolve the listeners to guid keys; replace the hand-wired UIStateTrigger pattern. **AnyCompletes only. NetworkStateManager backend. No flow-store.** If any DevKit-side serialized field is added (the optional `synced` flag), it passes **Proof C** (zero-diff on untouched labs). |
| **C - integrate + ship** | Prove in a real **two-client VR** lab; migrate existing labs off hand-typed `stateID`s (keep a compat shim during transition so old wiring still works); run the **AR no-Fusion equivalence assertion** (identical trace to the Phase A golden); "DevKit > Evaluate Changes" green. |
| **After-launch (D/E) - the graduation, NOT this WS** | Swap the bridge body `NetworkStateManager.SetStateTrue` -> `IScenarioFlowStore.CompleteStep(guid)` -> `flow.*` over `INetworkReplicationService` -> typed Fusion. Add **Barrier** + **typed late-join fast-forward** + authority **re-gating** (§9.8). Retire the `NetworkStateManager` switchboard (flow-surface-first sequencing per §2.3). Labs authored in Phase B are **unchanged** - only the backend swaps. SSoT for this end state: PROPOSAL §2.1/§2.3. |

**The binding glue in one line:** Phase B freezes the **contract** (publish-from-completion-lifecycle, key = guid,
AnyCompletes, local-advance-never-gates); Phase D/E swaps the **backend**. The publish point in §2.4 is exactly where
`IScenarioFlowStore.CompleteStep(guid)` will be called; the key is exactly the `flow.*` namespace; the semantics are
exactly the launch default. That is what makes the swap free.

---

## WS B9 - Multiplayer step-sync bridge (folded into the Phase B doc 2026-06-10 - canonical checkbox tracking lives THERE; this section is the reference copy)

**Goal:** the guid-keyed, lifecycle-published step-sync bridge exists; the hand-wired UIStateTrigger pattern is retired
from synced steps; AR is provably unchanged.

**Scope / files:** VR-consumer-side (Stergios) - `ScenarioFlowBridge` (Fusion-gated) + guid-keyed evolution of
`EventStateListener` / `TimelineStateListener`. DevKit side (Alexandros) - **only** the step-completion lifecycle event
if not already exposed by the Phase A refactor; **no** new public flow type. Rides the existing locked runner; no
extraction.

**Steps (progress tracking):**
- [ ] Step 1: Ride WS B3 Step 4's thin completion emission (one raise, two consumers: the analytics emitter + this bridge); if B3 wired it point-to-point to `AnalyticsApi.Emit`, generalize the raise onto `LabEventBus.StepCompleted(guid)` (additive, same gate). Phase A cannot emit (behaviour-neutral trap), so the raise is Phase B work either way.
- [ ] Step 2: Implement `key(guid) = "flow.step." + guid`; assert it respects the `NetworkString<_64>` 64-char cap.
- [ ] Step 3: Implement `ScenarioFlowBridge` (`#if PITECH_HAS_FUSION`, consumer-side): subscribe to completion, publish `SetStateTrue(key(guid))` for synced guids. AnyCompletes only. Fire-and-forget; null-guarded.
- [ ] Step 4: Evolve `EventStateListener` / `TimelineStateListener` to read `GetState(key(guid))` (keep the hand-typed `stateID` path as a compat shim during migration).
- [ ] Step 5: Add the **read-gate-forbidden validator** (§3): authoring fails if a step's advance condition reads a shared fact.
- [ ] Step 6: Migrate one real VR lab off the UIStateTrigger wiring to the bridge; verify two-client sync (AnyCompletes) and late-join (`GetState` already true).
- [ ] Step 7: Build the same lab for **AR / no-Fusion**; assert the trace is identical to the Phase A golden (the "exactly as before" proof).
- [ ] Step 8: Add the **state-budget validator** (editor-time, rides the Step 5 validator): per scene, count `defaultStates` + distinct trigger/listener state names + synced step guids against the shared `Capacity(64)`; **warn at 48 (75%), fail above 64.** Baseline 2026-06-10: worst scene = 16 distinct states -> ~48 headroom (§2.5). Overflow is a runtime on-device failure, so this is enforced at design time; a `Capacity(128)` bump is a recorded stopgap only - the bitmask graduation is the real ceiling-removal.

**Acceptance:** synced steps publish a guid-keyed fact from the runner completion lifecycle via `ScenarioFlowBridge`;
peers react through guid-keyed listeners; AnyCompletes + free late-join work in a two-client VR lab; the AR no-Fusion
build is byte-identical to the Phase A golden; the read-gate-forbidden validator is live; the `Capacity(64)` ceiling is
logged. No DevKit public flow type introduced. NetworkStateManager backend only.

**Gate:** Phase A WS A3 net green - "DevKit > Evaluate Changes" passes (no shipped lab regressed).

---

## 5. Exit checklist

- [ ] Synced steps sync by guid-keyed fact, published from the completion lifecycle - no hand-typed `stateID`, no throwaway EventStep/UIStateTrigger node.
- [ ] Two-client VR lab: AnyCompletes + late-join green on the `NetworkStateManager` backend.
- [ ] **AR / non-Fusion lab: trace identical to the Phase A golden** (the bridge is not compiled in; local flow unchanged).
- [ ] Read-gate-forbidden invariant enforced by a validator.
- [ ] No public `IScenarioFlowStore` / `flow.*` type introduced at launch (after-launch graduation un-blocked, not pre-baked).
- [ ] State-budget validator live (shared `Capacity(64)`; warn at 48, fail above 64); `Capacity` bump recorded as stopgap-only if ever taken.
- [ ] Every change passed "DevKit > Evaluate Changes" (Proofs A/B/C). No emoji / mojibake.

When green, the UIStateTrigger switchboard is retired from synced steps, AR is provably unchanged, and the contract is
frozen for the after-launch `IScenarioFlowStore` graduation to swap the backend under
([_after-launch/2026-06-09-after-launch-plan.md](_after-launch/2026-06-09-after-launch-plan.md); end-state spec in
PROPOSAL §2.1/§2.3).

---

## Status & Progress Log

> Newest first.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-10 | B9 | FOLDED into 2026-06-09-phase-b-analytics.md as WS B9 (plan-structure row + goal/(4) + architecture stance + spec §28.2 ref + exit criteria + lane/parallelism/tags/DRIs + full WS section + §5 alignment + §7 reconciliation + exit checklist + self-review + executors + status log; all additions, existing B1-B8 untouched). This doc stays the detail companion; Step 1 retargeted to ride B3 Step 4's emission. Pending Petros ratification | Stergios (Claude) |
| 2026-06-10 | B9 | Budget measured: worst scene = 16 distinct states of 64 -> ~48 headroom for step facts; §2.5 added (co-locate in the singleton switchboard, namespaced by `flow.step.`; split physically at graduation); Step 8 upgraded to a design-time state-budget validator (warn 48 / fail 64) - overflow is a runtime on-device failure, never discover it there | Stergios (Claude) |
| 2026-06-10 | B9 | Drafted: launch-minimal guid-keyed step-sync bridge replacing the UIStateTrigger switchboard; consumer-side + Fusion-gated; AR no-Fusion no-op proven via the read-gate invariant + Phase A golden; forward-aligned to the after-launch IScenarioFlowStore graduation (contract frozen, backend swapped). Proposed as Phase B WS B9, SLIP-ELIGIBLE. | Stergios (Claude) |
