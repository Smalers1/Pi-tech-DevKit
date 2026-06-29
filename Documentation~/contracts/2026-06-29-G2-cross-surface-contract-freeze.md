# G2 Cross-Surface Contract Freeze - 2026-06-29

**Workstream:** B1.4 Step 6 (analytics session-report schema + consent + LaunchContext freeze).
**Gate:** G2 (cross-surface), due 2026-06-29. This is the hand-off contract for the **Web Portal / cloud** side.
**Authority:** code is ground truth - every field below is transcribed from the committed DevKit source (cited file:line), not from a plan. Where the plan and code disagree, the code wins.
**Status of this doc:** the DevKit-side surface is frozen as captured here. The remaining G2 action is **cross-surface and human**: send this to the Web Portal / cloud owner and record their alignment acknowledgement (see section 8). `contractVersion` is **NOT** bumped today (section 6).

---

## 0. Scope - what freezes today vs what does not

**Frozen today (cross-surface, the cloud must align to these shapes):**
1. `LaunchContext` - the launch envelope (tenant / user / locale + lineage).
2. `LaunchLifecyclePayload` - the emitted launch lifecycle event JSON.
3. The **session-report schema** = `LabRubric` and its parts (bundled RAW into the eventual report so the cloud can re-compute grades). The shapes freeze; the report ENVELOPE that carries them is B.2.
4. The tenant-isolation assertion the cloud enforces (section 4).

**Explicitly NOT frozen today (deferred, do not treat as final):**
- The **session-report envelope / payload** type (the thing emitted to cloud on SessionStop) - **Phase B.2** ("on SessionStop the emitter assembles the session report -> outbox -> cloud", `SessionStopStep.cs:8-9`). It does not exist in code yet.
- The **reducer** (events -> rawValue -> score) and any **emit** path - **Phase B.2**.
- The broader **DevKit SDK emit-API surface** (rubric + bus fact shape + SessionStart/Stop + effect-scope + ConsoleParameter) - separate freeze **2026-07-07**.
- **Consent** has no field in the report schema yet - it is an **open decision** (section 5).

> Honesty note: at B.1 all of the rubric/bracket types are INERT serialized schema - nothing references them at runtime yet, so untouched labs are zero-diff (Proof C). We are freezing the *shape the cloud will receive later*, captured now so the cloud can build against a stable contract.

---

## 1. LaunchContext (launch envelope -> cloud)

Source: `Runtime/ContentDelivery/LaunchContext.cs:12-42`. `[Serializable]`, `contractVersion = "1.1.0"`.

| field | type | notes |
|---|---|---|
| `contractVersion` | string | **"1.1.0"** at launch. Copied into `LaunchLifecyclePayload`. Bump gated - see section 6. |
| `launchRequestId` | string | lineage |
| `attemptId` | string | the attempt (created local-first) |
| `idempotencyKey` | string | dedupe key for the cloud |
| `labId` | string | |
| `addressKey` | string | Addressables address |
| `resolvedVersionId` | string | CCD-pinned content version |
| `runtimeUrl` | string | |
| `launchedFromCache` | bool | |
| `allowOfflineCacheLaunch` | bool | |
| `allowOlderCachedSameLab` | bool | |
| `networkRequiredIfCacheMiss` | bool | |
| `source` | enum `LaunchSource` {ReactNativeBridge=0, UnityMenu=1, Direct=2} | `LaunchContext.cs:5-10` |
| `requestedAt` | string | ISO-8601 UTC |
| **`tenantId`** | string | **B.1 addition (sec-11.5).** Org isolation. Empty default (inert at launch). Cloud asserts report tenant == auth tenant (section 4). |
| **`userId`** | string | **B.1 addition.** Feeds the session report's user list. |
| **`locale`** | string | **B.1 addition (B1.5 Step 1).** Per-client UI locale; NEVER networked; empty = host default. |

Role + attempt are created **in-scene per attempt**, NOT in LaunchContext (`LaunchContext.cs:34`, `SessionRole.cs:8-10`).

---

## 2. LaunchLifecyclePayload (emitted launch event JSON)

Source: `Runtime/ContentDelivery/LaunchContextReporter.cs:19-29` (+ `LaunchResolvedData` :8-17). Serialized with `JsonUtility` (`:152`). Note the snake_case JSON field names.

`LaunchLifecyclePayload`: `contractVersion` ("1.1.0"), `launchRequestId`, `attempt_id`, `idempotency_key`, `timestamp` (ISO-8601 UTC), `@event` (JSON key `event`; values observed: `"launch_resolved"`, `"experience_abandoned"`), `data: LaunchResolvedData`.

`LaunchResolvedData`: `labId`, `addressKey`, `resolvedVersionId`, `runtimeUrl`, `sessionDurationSeconds` (float), `durationMs` (long).

Emission requires lineage validation + (online) a `runtimeUrl` (`LaunchContextReporter.cs:116-125`).

> The per-event runtime telemetry path (`RuntimeTelemetryBatchPayload`, `contractVersion = "1.2.0"`, `RuntimeTelemetryAdapter.cs:75`) is the legacy poll output the **session report will supersede** in B.2. Frozen here only as "what the cloud receives today"; do not build new cloud features on it.

---

## 3. Session-report schema (LabRubric, bundled RAW)

Source: `Runtime/Analytics/`. All `[Serializable]`. Bundled raw (not pre-scored) so editing grading re-grades every stored session (`LabRubric.cs:10-11`).

### 3.1 LabRubric (`LabRubric.cs:24-44`)
- `schemaVersion` int = **1** (the rubric's own forward-migration version, distinct from `contractVersion`).
- `analytics` : `[SerializeReference] List<Analytic>` - measurement layer (author-owned, fixed).
- `subjects` : `List<TrackedSubject>` - the subjects registry.
- `objectives` : `List<Objective>` - grading layer (teacher-owned, Web-Portal-tunable post-launch).
- `roleCapacities` : `SessionRoleCapacities`.

### 3.2 Analytic (polymorphic, `Analytic.cs`)
Base: `id`, `label`, `metrics : [SerializeReference] List<AnalyticsMetric>`, abstract `Kind`.
- `StepAnalytic` (KindId **"Step"**) + `stepGuid`.
- `SceneAnalytic` (KindId **"Scene"**) + `category`.

### 3.3 AnalyticsMetric (polymorphic, `AnalyticsMetric.cs`)
Base: `id`, `label`, `weight` (float, relative), `bands : List<ScoringBand>`, abstract `Kind`.
Kinds (KindId): `StepDurationMetric` **"StepDuration"**, `TotalDurationMetric` **"TotalDuration"**, `DropMetric` **"Drop"**, `WrongInteractionMetric` **"WrongInteraction"**, `OrderMetric` **"Order"**.
Duration kinds = ceiling (highest band crossed); count kinds = sum per-occurrence then clamp.

### 3.4 ScoringBand (`ScoringBand.cs`)
`name` enum `BandSeverity` {None, Warning, Error}, `threshold` (float; seconds for duration, count for count kinds), `penaltyWeight` (float; default warning 0.5 / error 1.0), `notifyInScene` (bool).

### 3.5 Objective (grading layer, `Objective.cs`)
`Objective`: `id`, `label`, `weight` (float, share of grade), `target` (float [0,1], **pass-bar label only, never a divisor**), `inputs : List<ObjectiveInput>`.
`ObjectiveInput`: `analyticId` (string), `subWeight` (float).

**Ratified grading formula (`Objective.cs:14-20`, RATIFIED 2026-06-26)** - normalized weighted mean with applicability mask:
- metric `x_m = clamp01(1 - Penalty_m(rawValue))`
- analytic `X_A = sum(a.w.x) / sum(a.w)`
- objective `X_o = sum(a.sw.X_A) / sum(a.sw)`
- grade `G = sum(a.W.X_o) / sum(a.W)` ("incomplete" if denominator 0); `target_o` is a pass-bar label (`X_o >= target_o`).

The DevKit is the **canonical reducer**; the cloud is a **mirror** that must compute identically (`AnalyticsMetric.cs:13-16`).

### 3.6 TrackedSubject (`TrackedSubject.cs`)
`id`, `label`, `target` (GameObject - scene ref, **resolved to a stable id on JSON export**), `scenarioRelevant` (bool; false = distractor), `ownerStepGuid` (string).

### 3.7 Roles + capacities (`SessionRole.cs`)
`SessionRole` enum {Professor, Participant, Spectator} - roles gate **analytics only**, never flow/interaction; chosen in-scene per attempt.
`SessionRoleCapacities`: `minProfessors`=0, `maxProfessors`=-1, `minParticipants`=1, `maxParticipants`=-1, `minSpectators`=0, `maxSpectators`=-1. `Unlimited = -1`.

### 3.8 Graded bracket step types (`Runtime/Scenario/Steps/`)
`SessionStartStep` (`Kind => "Session Start"`) and `SessionStopStep` (`Kind => "Session Stop"`), each with `nextGuid`. Delimit the graded part; emit session-started / session-completed bus facts in B.2.

> **Discriminator decision RESOLVED (WS B1.6 Step 2, ratified 2026-06-26, implemented commit `d49bb64`):** the portable JSON `kind` keys on the **CLR short type name** (e.g. `"StepDurationMetric"`, `"SessionStartStep"`), NOT the mutable `Kind`/`KindId` display string - the `SessionStart -> "Session Start"` rename proved `Kind` is display copy. The scenario-step DTO already implements this (`ScenarioJsonDto`, `schemaVersion=1`); the **analytics-rubric emit serializer is B.2** and MUST follow the same convention. The `Kind`/`KindId` strings in sections 3.2-3.3 are display tags, carried as an optional `label`. **Cloud: key on the CLR short type name.**

---

## 4. Tenant isolation (the cloud reject rule)

The report envelope (B.2) is stamped tenant + session + lab/version. The cloud asserts **"report `tenantId` == authenticated tenant, else reject"** (RLS) - `LaunchContext.cs:31-35`. This is the first invariant (tenant isolation); the cross-surface contract depends on the cloud enforcing it. **Web Portal / cloud must confirm this assertion is implemented before any cohort data flows.**

---

## 5. Consent - OPEN ITEM (decision needed at G2)

The session-report / analytics path has **no consent field** in any frozen type above (verified: grep for consent across `Runtime/` matches only `Runtime/AgentSubstrate/Observation/` - `IConsentGate`, the *agent-observation* consent, a separate concern). 

**Decision required (owner: compliance + Web Portal):** does session-report emission gate on (a) the existing `IConsentGate`, (b) a new explicit report-level consent field added to the envelope in B.2, or (c) tenant/enrolment-level consent asserted at the Web Portal? VICKY is Annex III high-risk (sec 3(b)); record the chosen mechanism here before the B.2 emit path is built. **Do not ship session-report emission without this resolved.**

---

## 6. contractVersion disposition (G2 action, NOT silent)

- **Hold `LaunchContext.contractVersion` at "1.1.0"** today. The new `tenantId`/`userId`/`locale` fields are additive and default-empty -> existing payloads round-trip -> bumping is unnecessary AND not inert (`LaunchContextReporter` copies it into the emitted payload, `LaunchContext.cs:15`).
- **Bump to "1.2.0" only when BOTH:** (1) the new fields are actually populated at launch (auth wired), AND (2) the Web Portal / cloud has aligned and asks for the bump. That is a future, deliberate cross-surface action - not part of this freeze.
- The runtime-telemetry batch path remains at its own `"1.2.0"` (`RuntimeTelemetryAdapter.cs:75`); unchanged, and superseded by the B.2 session report.

---

## 7. Deferred (do not treat as frozen here)

| Item | Where |
|---|---|
| Session-report envelope/payload type | Phase B.2 |
| Reducer (events -> rawValue -> score) + emit/outbox | Phase B.2 |
| DevKit SDK emit-API surface freeze (rubric, bus fact shape, SessionStart/Stop, effect-scope, ConsoleParameter) | 2026-07-07 |
| JSON `kind` discriminator | **RESOLVED** - CLR short type name (sec 3.8, B1.6 Step 2) |
| Consent mechanism | section 5 |

---

## 8. Hand-off checklist to Web Portal / cloud (the human G2 action)

- [ ] Send sections 1-4 of this doc to the Web Portal / cloud owner.
- [ ] Cloud confirms it can store `LaunchContext` (incl. `tenantId`/`userId`/`locale`) and the raw `LabRubric` shape, and re-compute grades with the section 3.5 formula (canonical-reducer parity).
- [ ] Cloud confirms the tenant-isolation reject rule (section 4) is implemented.
- [ ] Resolve the consent decision (section 5) with compliance.
- [ ] Record the cloud's alignment acknowledgement (date + owner) in the B.1 plan Status Log.
- [ ] Leave `contractVersion` at 1.1.0; schedule the 1.2.0 bump for when auth-populated + cloud-aligned (section 6).
