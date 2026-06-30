---
title: Phase B.2 - open decisions + logic questions (for Stergios)
date: 2026-06-29
author: Claude Code
status: needs Stergios review; some answers may trigger small edits/refinements
---

# B.2 - decisions for you, and the calls I had to make

I implemented all of B.2. Along the way I hit choices that are yours, plus §11 details the spec left implicit
that I had to settle to write working code. Nothing here blocks your compile/playtest; these are for sign-off
and may produce small refinements.

## A. Decisions that are genuinely yours

1. **G2 session-report wire schema (your personal review).** The concrete shape now exists in
   `Runtime/Analytics/SessionReport.cs` + `SessionReportJson.cs`. Confirm the envelope:
   `{ schemaVersion, tenantId, sessionId, labId, labVersion, isComplete, users[{userId, role}], events[],
   rubric (raw) }`. Three things inside it to confirm:
   - **Event fidelity:** step durations are **derived** from `step.entered`/`step.completed` timestamps
     (ms). There is **no explicit dwell/exit event**. Fine for linear/branched flow; if you ever want true
     dwell (re-entered loop steps, idle-vs-active), an explicit exit event must be added **before the 07-07
     freeze** (it's un-removable after).
   - **Rubric bundled raw in every report** (so the cloud re-computes). Conscious storage cost.
   - **Discriminator = CLR short type name** (`GetType().Name`, e.g. "StepDurationMetric") - matches the
     ratified WS B1.6 convention. The cloud (B2.3) must key on the same.

2. **Role authority.** Roles are picked **in-scene by the learner** (your decision). That means a learner can
   pick **Spectator** and not be graded. Fine for formative practice; for a **summative/credit** lab you may
   want the role bound to entitlement instead of free choice. Confirm it's acceptable, or we gate it.

3. **Consent.** ~~`LaunchContext.consent` is not populated today (the field exists; nothing fills it).~~
   **CORRECTED + DONE 2026-06-30 (P8):** there was NO consent field - the original note was wrong (verified
   against the code). Added `Pitech.XR.Core.ConsentReceipt` (consentId / policyVersion / grantedAtUtc;
   `IsGranted` = non-empty consentId), carried `LaunchContext.consent -> LabRuntimeContext -> SessionReport`,
   and a FAIL-CLOSED gate in `LabAnalytics.Finalize` (no granted receipt -> report not emitted, loud; local
   readout still shows). STILL YOURS: who wires org/enrolment consent into the launch context on the HOST
   (mobile RN + VR Shell + enrolment), and the Web Portal alignment on the field names before the G2 freeze
   (then bump contractVersion 1.1.0 -> 1.2.0). Ingest-side enforcement remains B2.3 (host/cloud).

4. **Cloud lane (B2.3) ownership.** I did **not** build the cloud/Web-Portal side - it must be built against
   the **confirmed** G2 schema, and that's your review. Decide: do I implement B2.3 from here once you confirm
   the schema, or does a web/cloud dev own it? The DevKit emit shape is ready to hand over.

5. **Vitals (B2.6) slip call.** I delivered the foundation (`Vital` + `PatientVitals` + `IAgentStateSource`).
   It was CAN_TRAIL. Confirm keep, or slip if you'd rather not carry it.

## B. §11 interpretations I had to choose (please confirm; easy to change)

These were implicit in the spec; I picked the defensible reading and documented it in the code.

1. **Duration band threshold `<= 0` = INACTIVE.** A duration metric with un-set thresholds scores **1.0**
   (no penalty), not 0. The author sets thresholds (seconds) to activate Warning/Error. This avoids the
   footgun where the default bands (all threshold 0) would zero every duration metric.
2. **Count-kind threshold is unused at launch.** Drop/Wrong/Order score by **per-occurrence severity sum**
   (then clamp), per §11.8 "count kinds = per-occurrence sum." The band `threshold` field isn't consulted
   for count kinds (only the per-severity `penaltyWeight` is).
3. **Count-metric scoping.** A count metric under a **Step** analytic counts only occurrences whose current
   step matches that analytic's `stepGuid`; under a **Scene** analytic it counts all occurrences in the
   bracket.
4. **Step duration = first traversal.** If a step is entered more than once (a loop), the StepDuration metric
   uses the **first** entered→completed pair. (Loop-aware duration is a noted future refinement.)
5. **Default severity table** (author-overridable via the band weights): relevant-subject **drop → Error**,
   distractor drop → Warning; **wrong interaction** with a known distractor → Warning, else → Error;
   **out-of-order → Warning**; authored **Signal → Error**.
6. **Interaction classification.** I changed the fact model: components emit a raw **`interaction.used`**
   fact (subject id) and the **recorder is the single classifier** (in-registry? relevant? ownerStep ==
   current? → correct / wrong / order). This keeps the registry logic in one place (where the rubric +
   current step already live), instead of pre-classified facts.

## C. Deferred to post-B2 (the on-device / Phase C window) - by design

The actual VR lab migration is post-B1+B2 (your earlier decision), and these need a headset to validate:

1. **Full interactive multiplayer co-op.** I delivered the data plane (`FusionScenarioPath`,
   `NetworkedParamStore`) + a **follower mirror** (frontier sync + AV/coherence + matching analytics on
   followers). The *full* co-op semantics - every peer runs each interactive step, first-completion-wins via
   an authority RPC, abort-on-frontier for the racing peers - plus **mid-session authority migration** are
   the 2-client on-device design+validation task. They cannot be validated headless.
2. **MP shared `sessionId`.** For one merged cloud report per session, peers must share a session id
   (replicated). Today `sessionId` falls back to the attempt id (single-player). Wire the shared id with the
   MP turn-on.
3. **NetworkedParamStore → LabConsole wiring.** ~~post-B2~~ **DONE 2026-06-30.** LabConsole now resolves an
   optional networked `IParamStore` component (self+children) and, when present, fronts the local store with
   a `RoutedParamStore` that sends **Networked-scope** ids to `NetworkedParamStore` (replicated +
   authority-sequenced) and keeps **Local-scope** ids client-local; the runner's effects flow through it
   unchanged (it reads `LabConsole.Params`). Resolved via Core's `IParamStore` (no Scenario→Networking dep);
   SP/no-Fusion resolves null → the store IS the LocalParamStore → byte-identical to B.1. **Still on-device:**
   actually exercising it needs a 2-client Fusion lab (it cannot be validated headless), and
   **provenance/actor tracking** (who changed a networked param) remains a noted follow-up.
4. **Fusion API compile-confirm.** I matched the proven `NetworkedLabStateStore` patterns
   (`NetworkDictionary`, `[Networked]`, `NetworkString<_64>`, `[Rpc]`, `INetworkStruct`), but only your VR
   compile confirms the Fusion API for `FusionScenarioPath` / `NetworkedParamStore` / `TimelineStateListener`.
5. **Localization pipeline on real labs.** Per your call: the runtime resolver + sample render now; running
   the StringTable pipeline on the actual labs (and de-hardcoding `SelectionLists` strings) is post-B2.
6. **Vitals real 3D binding + full digital twin.** A real scene binding (e.g. the breathing-blendshape
   Timeline-speed vital) is author-side; the twin (cascade rules, profiles, ControlOptionManager PUN→Fusion)
   is post-launch.
7. **B2.7 S2 - play-mode live param values.** The visual params editor (the `ConsoleParameter` drawer)
   landed; the live-values readout needs a small public `LabConsole` accessor across the asmdef boundary -
   deferred as polish.

## D. Housekeeping you must do

1. **Regenerate the Proof-B baseline** after compiling (B.2 adds public API - expected). See the testing
   guide step 2.
2. **Telemetry-on-bus carry-over (B1.1 S3 / B1.8 S4) is intentionally NOT flipped.** I did **not** turn on
   `RuntimeTelemetryAdapter.useEventBusStepTracking`, fix its bind-gap, or delete the `FindObjectsOfType`
   scan - because flipping it changes the **Vicky-ingestion trace fidelity** (higher-fidelity, not
   byte-identical) and needs **Vicky-ingestion sign-off** before the legacy scan is removed. The **new**
   analytics path (`LabAnalytics`) is a separate, independent bus subscriber and is fully done. Decide when
   to schedule the adapter migration + sign-off.
3. **LabAnalytics placement:** it must be on the lab **root** (the LabConsole GameObject) so it shares the
   `LabRuntimeContext` (bus + report identity) with the runner. The testing guide says this; worth a note in
   author docs.

## E. What is NOT done (and why)
- **B2.3 cloud** - gated on A1 (G2 confirm) + A4 (ownership). Not built against an unconfirmed wire format.
- **The VR lab migration** - your post-B1+B2 decision; this whole B.2 is verified by test, not by migrating
  real labs.

## F. Post-review fixes (2026-06-30) - your 6-item review

1. **State store <-> param store unification (#1).** The bool-view (`ILabStateStore`) now DELEGATES to the
   lab's one `IParamStore` instead of owning a separate store. `LocalLabStateStore` is rewritten as a pure
   view (GetState/SetState/Toggle = GetBool/SetBool; StateChanged forwards the store's ParamChanged), and
   `LabConsole` backs it with `Params` on Start (via a new Core seam `IParamStoreBackedState`, so the public
   `ILabStateStore` interface is unchanged -> no break to a VR implementer). Net: triggers (writers) and
   ConditionsStep/effects (readers) share ONE store. **Decision for you:** the networked path now is
   `LocalLabStateStore` (bool-view) over a `NetworkedParamStore` (via the routed Params). `NetworkedLabStateStore`
   (the VR NetworkStateManager drop-in) is left standalone on purpose (so its replication isn't downgraded) -
   **reconciling/retiring it onto the unified path is part of your post-B2 VR migration.** Also possible if you
   prefer: make `LabConsole` itself implement `ILabStateStore` (no separate component) - say the word.
2. **SignalMetric (#3).** Authored signals were scored by any Drop/Wrong/Order metric sharing the signal's id.
   Added an explicit `SignalMetric` kind; the engine now scores a signal ONLY on a `SignalMetric` matched by
   id, and Drop/Wrong/Order no longer absorb signals. **Authoring change:** to score a signal, add a
   `SignalMetric` whose id == the emitter's `signalId` (4 new tests cover it). Public-API addition (Proof B).
3. **Greek localization (#4).** Was mojibake on the Windows/Unity compiler (no-BOM .cs read in the system
   codepage). Rebuilt the Greek strings from Unicode code points -> the file is now PURE ASCII and
   encoding-proof. Verified no non-ASCII bytes remain.
4. **NetworkedParamStore pre-spawn guards (#6).** `Set/Apply/TryGet/Render` now gate on a `Ready` check
   (`Object != null && Object.IsValid`); pre-spawn writes buffer into the local mirror and the authority
   flushes them in `Spawned()`. No more invalid `[Networked]`/RPC access from an early UnityEvent/trigger.
5. **CS0118 compile error** (LabAnalyticsEditor): `Scenario` bound to the namespace, not the type, inside
   `Pitech.XR.Analytics.Editor`. Fixed with a `using Scenario = ...Scenario;` alias inside the namespace block.
6. **.meta files (#5).** Unity generated them on import (that import is what surfaced the CS0118). Verified
   both `AssemblyInfo.cs.meta` and `RoutedParamStoreTests.cs.meta` exist; nothing to author by hand.
7. **Params UI (#2 / B2.7 S2).** A first-class Parameters section + a **play-mode live values** readout (reads
   the internal `LabConsole.Params` via an editor `InternalsVisibleTo` grant), closing the deferred B2.7 S2.
   **SUPERSEDED 2026-06-30 (P0):** this had lived in a new `LabConsoleEditor` that DUPLICATED
   `SceneManagerEditor`'s `[CustomEditor(typeof(LabConsole), true)]` (two editors -> params hidden). Merged into
   `SceneManagerEditor`; `LabConsoleEditor` deleted.

8. **One state store (#1 / H4) - DONE 2026-06-30 (P5).** `LabConsole` now implements `ILabStateStore` directly
   (bool-view over its own `Params`); triggers resolve the root via `GetComponentInParent<ILabStateStore>()` -
   no separate component, no possible disconnect. Optional `LocalLabStateStore` views are still back-wired to
   the shared store. Networked-dict reconciliation (`NetworkedLabStateStore`) stays post-B2 VR (a co-location
   caution was added to that file).

9. **Consent (P8) - DONE 2026-06-30.** New `Pitech.XR.Core.ConsentReceipt`; the host stamps `LaunchContext.consent`
   -> carried to `LabRuntimeContext` -> `LabAnalytics` fail-closes emission unless `IsGranted` and attaches the
   receipt to `SessionReport` for audit. Single analytics consent; receipt = consentId + policyVersion +
   grantedAtUtc. See the 2026-06-30 hardening plan P8 for full detail + the dev-consent convenience call.
