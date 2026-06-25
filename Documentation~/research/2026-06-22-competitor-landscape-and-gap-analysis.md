<!-- Generated 2026-06-22 by a 25-agent research workflow (devkit-competitive-gap-analysis). Competitor claims are web-sourced and fact-checked but time-sensitive; confidence levels noted inline. Our-side capabilities are code-grounded against com.pitech.xr.devkit v0.11.0. -->

# Pi-tech XR DevKit — Competitive Landscape & Gap Analysis

*Audience: DevKit lead designer/architect. Basis: code-grounded internal capability audit of `com.pitech.xr.devkit` v0.11.0 + verified competitor profiles across four clusters (medical/surgical XR, no-code/low-code XR authoring, enterprise XR delivery/analytics, generic Unity baseline). Confidence levels and stub/absent classifications from the source audits are preserved throughout.*

---

## 1. Executive Summary

**What the DevKit is today.** The Pi-tech XR DevKit v0.11.0 is a small, code-owned, engineer-facing Unity SDK whose *built* strengths are concentrated in three places: a mature, complete `[SerializeReference]` step-graph **scenario authoring** system (11 step types + GroupStep, dual Inspector + GraphView authoring, sophisticated branching, play-mode QA tooling); a well-engineered **content-build + runtime cohort-pinned delivery** pipeline (4-stage Addressables orchestration, 14-check validation, content hashing, a 13-state publish-transaction reporting model, and a runtime URL rewriter that pins all bundles to one CCD release); and two production-ready **runtime telemetry substrates** — a step/attempt `RuntimeTelemetryAdapter` and a fail-closed, consent-gated `AgentObservation` pipeline purpose-built as the "eyes" for the VICKY grounded tutor. Around these sit good DX scaffolding (the 6-page DevKit Hub cockpit, the Evaluate Changes equivalence gate, project-health/guided-setup services) and a disciplined set of **intentionally empty reserved modules** — Analytics (higher-order scoring), Networking/Multiplayer, Localization, and Vitals — each a clean namespace + asmdef seam with version-define gating but **zero shipped behavior**.

**The 6 most important gaps:**

1. **No non-coder / professor-facing authoring path** — authoring requires editing C# in the Unity Inspector. The dominant med-ed buyer expectation (UbiSim, SimX, Body Interact ship no-code clinician case creators) is structurally unmet.
2. **No assessment/scoring/dashboard spine** — the higher-order `AnalyticsModule` is an empty stub; quiz scoring exists but is local-only and not wired to telemetry. Competitors lead hardest here (MAGES per-action 0–100 scoring, FundamentalVR gaze metrics, Cognitive3D session replay).
3. **No standards-based LMS interoperability** (xAPI/SCORM/cmi5/LTI) — both telemetry subsystems use proprietary schemas. Greek/EU universities run Moodle/Canvas; this is table stakes in the SaaS/delivery cluster.
4. **No publish-to-CDN execution** — the publish transaction state machine is a validator only; nothing uploads to CCD or activates a release. The path from "built" to "live cohort-pinned content" is currently manual.
5. **No multiplayer / collaboration** — only a frozen `flow.step.{guid}` naming contract ships; the closest analog (VR Builder) is also weak here, but med-school buyers increasingly expect instructor-led/spectator multi-user (SimX, UbiSim, Acadicus).
6. **No embodied AI tutor as a feature, and no concrete `IAgentStateSource`** — we have the observation *hook* (state-out) but the tutoring loop (STT/TTS, proctor agents, document grounding) lives in the Web Portal/VICKY, and the hook ships inert (no reference state source).

**Headline recommendations:**

- **Protect the launch (2026-09-10), don't chase parity.** Keep launch authoring on the existing engineer-facing step-graph; Pi-tech (not professors) authors the initial labs for 5 known universities.
- **Three must-do launch hardening items:** (a) SerializeReference migration/upgrade tooling + live routing-integrity validation; (b) a headless/CI build entry point + a decided publish-to-CCD ownership boundary; (c) a reference `IAgentStateSource` so the AI substrate works day-one, plus fixing the Evaluate Changes gate to cover AgentSubstrate and resolving the `emitLifecycleJson` telemetry mis-routing.
- **Lean into the genuine moat:** tenant isolation + cohort-pinned content + a fail-closed consent-gated AI observation substrate + an explicit EU AI Act high-risk posture — a combination **no competitor in any cluster targets**.
- **Consume, don't rebuild:** adopt Unity Localization, Unity XRI, a buyable transport (Photon Fusion 2), and integrate Cognitive3D for spatial analytics rather than reinventing them.

---

## 2. What Our DevKit Is (Code-Grounded)

Maturity labels are taken directly from the internal audit: **real** (implemented and shipping), **partial** (infrastructure/abstract, host must complete), **stub** (intentionally empty reserved namespace), **absent** (no code).

| Module / area | Maturity | Reality (code-grounded) |
|---|---|---|
| **Scenario authoring (step graph)** | **real** | `[SerializeReference]` `List<Step>` in `Scenario.cs`; 11 concrete step types + `GroupStep`; per-node GUIDs auto-assigned in `OnValidate`; guid-based routing safe against reorder/domain reloads. Dual authoring: custom Inspector (`ScenarioEditor.cs`) + full GraphView editor (`ScenarioGraphWindow*.cs`) with auto-layout, notes/groups, undo/redo, modal step edit windows. |
| **Branching** | **real** | First-class: Question choices, Quiz (AnyAnswer / BranchOnCorrectness), Selection right/wrong, ConditionsStep (8 compare ops + reflection value extraction), MiniQuiz score ranges, GroupStep 6 completion modes (All/Any/Specific/Required/N-of-M/MultiCondition) + nested proxy branching. |
| **Subsystem integration** | **real** | Stats (StatEffect auto-sync), Quiz (QuizAsset+QuizSession), Selection (SelectionLists), Timeline (PlayableDirector), UI animator triggers. |
| **Play-mode QA** | **real** | `EditorSkipFromGraph` deterministic branch forcing for rapid testing without UI interaction. |
| **In-scene interaction** | **real** | Dual-path Selectable system (EventSystem+PhysicsRaycaster desktop/AR/mobile + reflection-based Meta VR binding). Auto/ForceDesktop/ForceVRMeta modes. |
| **Quiz / Stats / Selection components** | **real** | QuizAsset (SO) + QuizSession scoring + QuizUIController; StatsRuntime (untyped float KV) + StatsConfig + StatsUIController (auto-discovery) — note StatsUIController inspector labeled **partial**; SelectionLists world-space multi-list controller. |
| **Make Grabbable wizard** | **real (scaffolding only)** | Pure reflection-based: adds Collider/Rigidbody/Meta Grabbable/Fusion NetworkObject when SDKs present, then **defers to Meta's own grab wizard**. **Zero native grab logic.** |
| **DevBlocks / Scene Categories / editor tooling** | **real** | Browsable prefab library (categories/tags/search, auto-instantiate, suggests missing managers); custom inspectors with validation/quick-fix. |
| **Content build pipeline** | **real** | 4-stage Addressables (Setup→Map→Validate→Build) via `AddressablesBuilderWindow`; 14+ validation checks; post-build catalog/content SHA256 hashing + bundle-size; build-preset catalog; player-version override. |
| **Publish transaction model** | **real (reporting) / stub (execution)** | 13-state machine (`PublishTransactionStateMachine`) + JSON `publish_transaction.v1` reports (idempotencyKey = SHA256 of tenant:lab:version:contentHash, full audit trail). **Validator-only — no auto-advance, no actual CCD upload/activation.** |
| **Runtime cohort pinning** | **real** | `AddressablesRemoteUrlRewriter` rewrites bundle URLs so all assets load from the same CCD release in `LaunchContext.runtimeUrl`; breaks dependence on mutable `latest`. LaunchContext contract v1.1.0 + validation. |
| **Content delivery spawner** | **real** | 3 source modes (LocalOnly/OnlineOnly/AutoOnlineWithLocalFallback), download-size check, prompt-before-download, retry loop, offline-cache/version-fallback policies. |
| **Status overlay** | **partial** | `ContentDeliveryStatusOverlay` abstract base; **no concrete UI ships** — host project must implement. |
| **RuntimeTelemetryAdapter** | **real** | step/attempt events, batching, reflection-based step auto-track, contract v1.2.0, Android bridge. ⚠️ **Verified defect:** `EmitTelemetryBatchJson` calls native `emitLifecycleJson` (telemetry routed via lifecycle method name). |
| **AgentObservation substrate** | **real** | Complete client: frozen V1 envelopes, fail-closed consent gating (`IConsentGate`, `DenyAllConsentGate` default), drop-oldest queue, exp-backoff retry (2^n, max 64s, 3 retries), JWT-per-attempt, typed 4xx/5xx classification → Web Portal edge fn. **But ships NO concrete `IAgentStateSource` — inert without host code.** |
| **AttemptIdentity** | **real** | Local-first GUID identity + server reconciliation. |
| **ScenarioFactKeys** | **real (consts only)** | Frozen vocabulary (`step.done`, `flow.step.{guid}`); emits nothing — Phase B foundation. |
| **DevKit Hub cockpit** | **real** | 6-page UIToolkit EditorWindow (Setup/Author/Localization/Deliver/Maintain/Reference); observer-only. |
| **Evaluate Changes gate** | **real** | EditMode equivalence proofs + headless `-executeMethod` (exit 0/1). ⚠️ **Deliberately excludes `AgentSubstrate.Editor.Tests` (known failure).** |
| **Higher-order Analytics module** | **stub** | `namespace Pitech.XR.Analytics {}` — Phase B WS B1–B6. No scoring/dashboard/action-tracking. |
| **Networking / Multiplayer** | **stub** | Empty namespace; `PITECH_HAS_FUSION` gate; only the `flow.step.{guid}` contract is real. After-launch. |
| **Localization** | **stub** | Empty namespace; `PITECH_HAS_LOCALIZATION` gate; Hub page is observer-only "coming in Phase B". Phase B WS B7. |
| **Vitals** | **stub** | Empty namespace. Phase B WS B8. |
| Avatars, recording/replay, LMS/SCORM/xAPI, marketplace, embodied AI tutor, STT/TTS, gaze/eye-tracking | **absent** | No code. |

**Net read:** scenario authoring, content build, and runtime telemetry/cohort-pinning are genuinely strong and battle-tested; the medical-domain specialization, the higher-order analytics/assessment layer, multiplayer, localization, and the AI *tutoring* loop are reserved or absent — not built.

---

## 3. Competitor Landscape

The field splits by architecture: **medical/surgical SDKs + SaaS** (closest analogs), **no-code/low-code XR authoring tools**, **enterprise XR delivery/analytics platforms** ("plumbing + measurement"), and the **generic Unity baseline** (table-stakes floor). Only **ORamaVR MAGES** and **FundamentalVR Fundamental Core** are true developer-facing Unity SDKs in the medical space — both far more productized than a small in-house DevKit.

| Vendor / cluster | Category | Relevance to us |
|---|---|---|
| **ORamaVR — MAGES / Creator / JARIA** | Medical/surgical XR SDK + cloud platform | **Closest direct analog.** Greek-rooted academic spin-off, same B2B med-ed market. Out-features us on surgical sim, GA networking/deformation, in-VR authoring, and ships JARIA — the most direct analog to VICKY's grounded tutor. |
| **FundamentalVR (fundamental XR) — Fundamental Core** | Medical surgical SDK + haptics | Only other true Unity SDK in med-XR. Productized portal, licensing, sub-mm force haptics, eye-tracking gaze metrics. Same paradigm as us, more mature. |
| **SimX / UbiSim / Body Interact** | No-code clinician case-creator platforms | Set the **no-code authoring bar** med-ed buyers expect; physiology engines, EHR, multiplayer, localization (Body Interact 10+ languages). |
| **Osso VR / PrecisionOS / Vantari / Medical Realities / Level Ex** | Closed surgical training platforms | **Expose NO authoring/SDK** — content built in-house. The strategic gap an authoring-capable SDK can exploit. |
| **MindPort VR Builder** | Open-source Unity no-code authoring SDK | **Closest structural analog to our Scenario module** — node graph, behaviors/conditions, JSON serialization, Unity 6/XRI 3. The parity bar; weak on multiplayer. |
| **HyperSkill / Bodyswaps / Wonda / Uptale / CenarioVR / Warp VR / Talespin** | No-code/SaaS + LLM authoring | Lead on **AI-assisted/LLM-runtime authoring** and xAPI/SCORM/LMS. Bodyswaps/HyperSkill directly target med-ed. |
| **Cognitive3D** | XR spatial analytics | **Category-defining, buyable.** Gaze heatmaps, 1:1 session replay, xAPI/SCORM, MCP server. VR Builder integrates it rather than rebuilding. |
| **ArborXR / ManageXR** | XR MDM / fleet delivery | Own the **delivery/version-pinning** dimension: Release Channels (version pinning), binary-patch OTA, CI/CD CLI, SCORM/LTI to 500+ LMS. |
| **Innoactive / PIXO / Strivr** | Full-stack training/streaming | CloudXR streaming, AI low-code authoring, marketplaces, SOC 2/ISO posture, instructor spectator. |
| **Generic Unity baseline** (XRI, Meta SDK, MRTK3, NGO/Fusion, Addressables+CCD, Unity Localization, Meta Movement) | Table-stakes building blocks | Mature, free plumbing we should **adopt not reinvent**; deliberately generic (zero medical/anatomy content). |

**ORamaVR MAGES as the closest analog** deserves emphasis: it is a multi-year, research-backed (self-coined "Computational Medical Extended Reality," SIGGRAPH Asia 2024; site-claimed 65+ peer-reviewed publications), dual-engine (Unity + Unreal) commercial suite — Creator (no-code, customer owns IP), Custom SIM Service, the VTC cloud runtime, an 84+-title SIM Library marketplace, and JARIA. The honest read: **VICKY/HealthOn does not out-feature MAGES on core sim tech, multiplayer, or deformation.** Differentiation must come from tenant isolation, web-portal/non-surgical med-ed authoring, EU AI Act posture, curriculum fit, and price/contract fit for Greek med schools.

---

## 4. Per-Dimension Deep Dive

### 4.1 Scenario / Case Creation & Authoring

**How competitors do it.** Three archetypes: (1) **no-code clinician case creators** (SimX Case Creator, UbiSim Intuitive Editor with full no-code EHR, Body Interact) — the dominant med-ed buyer expectation; (2) **node-graph/structured-tree SDK authoring** (MAGES fixed 3-level Scenegraph + Action Prototypes + in-VR editor; VR Builder Unity node graph with 70+ blocks + experimental MCP/LLM authoring; FundamentalVR Core scenario/procedure-step framework); (3) **AI-assisted/LLM-runtime authoring**, rising fast (HyperSkill SimGenie collapsed a med-school branching case to 2 states with GPT-4 generating all patient dialogue at runtime; Uptale, Bodyswaps, Wonda, CenarioVR AI Wizard). The surgical procedural players expose **no authoring at all**.

**Best-in-class.** UbiSim Intuitive Editor (no-code EHR authoring bar); ORamaVR MAGES (Scenegraph + Action Prototypes + in-VR authoring, dual-engine); MindPort VR Builder (closest structural parity bar); HyperSkill SimGenie (lowest-authoring-cost LLM model).

**What we have.** A genuinely strong, **complete, engineer-facing** step-graph system at roughly structural parity with VR Builder — `[SerializeReference]` model, dual Inspector/GraphView authoring, first-class branching across 6 mechanisms, tight subsystem integration, auto-layout, validation, undo/redo, and deterministic play-mode QA. This is real and battle-tested.

**What we lack.** No non-coder/professor-facing path (the widest gap vs buyer expectations); **no AI-assisted authoring of any kind** (the "VICKY authoring" in steering docs is plan-stated, not in v0.11.0 code); no preset templates/wizards/starter library; no clinical domain model (Vitals is an empty stub — no virtual patient/physiology/EHR); no portable scenario-definition contract (each Scenario is a scene-bound singleton); no live routing-integrity lint (validation is post-hoc; can reference non-existent guids); no SerializeReference migration tooling (type renames orphan steps — only a manual "Clear Nulls" mitigation); authoring is Unity-Editor-only despite the Web Portal being the stated system of record; minor graph ergonomics gaps (nested GroupStep children render as tiles not nodes).

**Recommendations.**
- **[MUST]** SerializeReference migration/upgrade tooling + live dangling-guid validation + repair action (medium). Production lab content must survive schema evolution through Phase B and the Dec 2027 compliance work — orphaned-step data loss in a €220k-contract lab is a credibility event.
- **[SHOULD]** Curated scenario templates/starter prefabs + "New Scenario" wizard in the Hub Author page; **keep launch on the engineer-facing graph** (medium). Do not attempt a full no-code clinician builder for launch.
- **[SHOULD]** Define and freeze a JSON-serializable scenario-definition export/import contract, decoupling authored scenario from the in-scene component (medium). Precondition for future web-portal/no-code authoring and AI generation; freeze now (as ScenarioFactKeys was) to avoid migration cost.
- **[COULD/later]** Post-launch: AI authoring as a prompt-to-definition generator (LLM emits the frozen JSON; human reviews in the graph) gated behind the EU AI Act high-risk posture (mandatory human-in-the-loop, provenance logging) — the §28.5 "AI-authoring now, AI-judging post-launch" seam.
- **[later]** Post-launch web-portal-first no-code authoring surface editing the frozen contract — the strategic differentiator vs closed surgical platforms.

### 4.2 In-Scene Components & Tools

**How competitors do it.** The generic Unity baseline (XRI, Meta Interaction SDK + Building Blocks, MRTK3) is the mature, free table-stakes floor — poke/ray/grab interactors, hand-grab poses, in-editor pose recorders, bounds/solver libraries, controller/affordance haptics. **A small SDK cannot out-build this; it should wrap it.** Medical SDKs (MAGES, FundamentalVR) add what the baseline lacks: anatomy deformable-mesh (cut/tear/drill), tool interactors, surgical IK, **calibrated force-feedback haptics** (FundamentalVR sub-mm cutaneous+kinesthetic via Haply/HaptX), and curated clinical asset libraries. Closed platforms (SimX, Body Interact, Acadicus) ship rich physiology-driven virtual patients, vitals/EHR widgets, and digital-twin environments.

**Best-in-class.** FundamentalVR Fundamental Core/HapticVR (medical Unity component toolkit with real haptics — the direct analog bar); MAGES (anatomy deformables + tool interactors + medical asset library); Acadicus (curated clinical content); Body Interact/SimX (physiology-aware vitals — what our Vitals stub reserves); Unity baseline (the layer to wrap).

**What we have.** Solid **domain-neutral** components: dual-path Selectable interaction; a robust quiz stack; a flexible stats system; SelectionLists; good editor ergonomics (DevBlocks, Scene Categories, validating inspectors); and the Make Grabbable wizard (verified scaffolding-only, defers all grab logic to Meta). **Verified stub: the Vitals module is an empty namespace** (Phase B WS B8).

**What we lack.** Vitals/physiology components (the single most-expected medical component); native grab/hand interaction (Make Grabbable is Meta-only scaffolding; we don't wrap XRI's hardware-agnostic interactors); hand tracking/gesture; **any** haptics (not even controller-pulse affordance feedback XRI gives free); anatomy/medical visuals & deformables & instruments; curated clinical asset library; non-Meta VR coverage (Valve/OpenXR/Pico) beyond the EventSystem fallback; typed/structured stats (bare floats); persistence (everything resets on scene reload); richer selection modalities.

**Recommendations.**
- **[MUST]** Position honestly in docs and sales material: DevKit is a **code-owned, tenant-isolated, EU-AI-Act-oriented Unity SDK** whose interaction layer wraps Unity/Meta standards, with anatomy/haptics intentionally consumer-supplied or partner-provided (low). We will not out-feature MAGES/FundamentalVR on surgical haptics/deformation — protect the launch narrative, don't over-promise.
- **[SHOULD]** Wrap Unity XRI behind a hardware-agnostic grab/poke/ray facade (keep Make Grabbable as the Meta fast-path) so labs run on non-Meta headsets (medium). De-risks the next contracts; not a launch blocker if the 5 launch universities are on Meta/mobile-AR.
- **[SHOULD]** Build a minimal, **typed** Vitals component set for launch scope only (HR/BP/SpO2/RR/temp bound to StatsRuntime + a vitals-monitor prefab; **author-set values, not a physiology engine**) — defer the engine to Phase B (medium). Confirm with Stergios whether any launch lab actually needs vitals; if not, drop to later. Net-new build (Vitals is currently empty).
- **[COULD]** Surface XRI affordance haptics (controller-pulse confirmation) on Selectable/Quiz interactions — the cheap 80/20; **do not** attempt force-feedback (low).
- **[COULD]** Seed a small curated clinical DevBlocks layer (a few medical-instrument prefabs, vitals monitor, authored hand-grab poses, a "CLINICAL" category) — content cost, not engine work; differentiates demos (medium).
- **[COULD]** Lightweight persistence for quiz/stats/session state with a clean seam to the Supabase telemetry/assessment spine (low).
- **[later]** Promote StatsRuntime toward a typed/constrained model ahead of Phase B vitals (medium).

### 4.3 Automatic Pipelines (Content Build / Publish / Delivery)

**How competitors do it.** **XR MDM platforms own this dimension**: ArborXR/ManageXR ship OTA, binary-patch updates, Release Channels that lock a device group to an immutable Target Version (cohort/version pinning), staged rollout, scheduled installs, rollback by re-pointing, CI/CD CLI + REST API — battle-tested at 30,000+ device fleets. The generic baseline (Addressables + CCD) provides the exact primitive we wrap (Buckets → immutable Releases → reassignable Badges; moving a badge serves new content with no rebuild) but **has no tenant/cohort concept**. ORamaVR distributes via the hosted VTC (centralized updates, single-login) — a SaaS deploy layer, not granular pinning. FundamentalVR Core ships a named Distribution & Licensing path. Warp VR approximates cohort targeting via groups + per-device assignment.

**Best-in-class.** ArborXR (true cohort/version pinning + binary patch OTA + CI/CD); ManageXR (CLI/API for programmatic publish); Unity CCD (the off-the-shelf badge/release primitive we target); FundamentalVR (the one SDK with a real distribution channel).

**What we have.** Genuinely strong on the parts implemented: full 4-stage build orchestration; 14+ pre-build checks; post-build hashing; the `publish_transaction.v1` reporting layer with idempotency keys and full audit trail; CCD remote-path templating; **runtime cohort pinning via `AddressablesRemoteUrlRewriter`** (a real, differentiated strength); LaunchContext v1.1.0 + validation; spawner with 3 source modes + offline/fallback policies; convention adapter; build-preset catalog.

**What we lack.** **No CI/CD automation** (no CLI/programmatic Build, all manual button-clicks); **no publish-to-delivery execution** (Publishing/Activated states exist but nothing uploads to CCD or activates — the state machine is a validator only, never auto-advances); no dynamic environment promotion / badge management (URLs baked at Setup); no rollback/deployment-safety implementation; no delta/patching or streaming (full builds only); no cryptographic content attestation (HTTPS-only); abstract status overlay (no concrete UI); no SCORM/LTI/cmi5 delivery integration; UI-blocking synchronous retry; no forward-compatibility handling on contracts.

**Recommendations.**
- **[MUST]** Add a headless/programmatic build entry point — static `DevKitContentBuild.RunAll(args)` + per-step methods invokable via `-batchmode -executeMethod`, reusing the existing service layer; return exit 0/1 (medium). The logic is built; the only blocker is the hard dependency on manual clicks. A small team cannot manually rebuild every lab/version for 5 universities reliably, and deterministic builds matter for EU AI Act traceability. **Highest leverage.**
- **[MUST]** Decide and document the publish-to-CCD ownership boundary, then wire the minimum: either implement Building→Built→Published to actually upload + move a badge from the editor, **or** explicitly designate the Web Portal backend as publisher consuming the `publish_transaction.v1` report (medium). Do not ship a state machine implying automation no code performs. The runtime pinning already works once a release exists — the missing link is getting bundles into a known CCD release.
- **[SHOULD]** Harden and document cohort/tenant pinning end-to-end (the `runtimeUrl → rewriter → single-release` path, offline/`allowOlderCachedSameLab` policies) + a deterministic `(tenant, cohort) → CCD release` mapping the portal can drive (low). **None of the SDK competitors document strict per-cohort version pinning** — turn an existing strength into a demonstrable selling point supporting the tenant-isolation gate.
- **[SHOULD]** Add a state-machine orchestrator that advances transitions by invoking existing services, gated behind the headless entry point (medium). Converts the audit trail into a real, traceable publish run — exactly the provenance evidence the high-risk regime will want.
- **[SHOULD]** Ship a concrete default `ContentDeliveryStatusOverlay` (Canvas or UI Toolkit) so Mobile/VR Shell get working download UI out of the box (low).
- **[COULD]** Client-side catalog/bundle integrity verification reusing the existing content hash (low) — a meaningful pre-Dec-2027 hardening item.
- **[later]** Plan (don't build) a cmi5/SCORM/LTI 1.3 launch+tracking story (high); defer delta/incremental patching and multi-environment promotion (high) — acceptable cost at 5-university scale.

### 4.4 Analytics / Telemetry / Assessment

**How competitors do it.** This is the **most mature and most differentiated** dimension in the field — and where we are furthest behind on the "learning" half. Patterns: deep in-sim scoring (MAGES per-action 0–100 scoring factors → Azure dashboard; FundamentalVR eye-tracking surgical metrics; Osso ACGME-mapped "6 Domains of Excellence"; Body Interact/SimX physiology-scored deterioration); spatial analytics as a buyable commodity (**Cognitive3D** — 3D gaze heatmaps, 1:1 session replay, ~3,000 pts/min/user, xAPI/SCORM export, MCP query server); and standards + LMS as table stakes (ArborXR Insights one-line `EventAssessmentComplete` into 500+ LMS; CenarioVR/Warp/Uptale/PIXO/Bodyswaps all SCORM/xAPI/cmi5/LTI). Genuine field gaps: standards-based LRS export is **unconfirmed for most medical vendors** (MAGES is proprietary Azure), and **none advertise EU AI Act high-risk / per-tenant isolation**.

**Best-in-class.** Cognitive3D (buyable spatial analytics standard); ArborXR Insights/AbxrLib (low-friction assessment+LMS spine); MAGES (per-action scoring + supervisor dashboard); FundamentalVR (gaze-grounded behavioral assessment); Osso (accreditation-aligned competency); Body Interact/SimX (physiology-scored, record/playback debrief).

**What we have.** Two **production-ready runtime telemetry subsystems** but **zero assessment/scoring/dashboard layer**: `RuntimeTelemetryAdapter` (step/attempt events, batching, reflection auto-track, contract v1.2.0); the complete fail-closed consent-gated `AgentObservation` client (frozen V1, drop-oldest queue, exp-backoff retry, JWT-per-attempt, typed response classification); `AttemptIdentity` (local-first GUID + reconciliation); local-only `QuizSession`/`StatsRuntime` (not wired to telemetry); `ScenarioFactKeys` (consts only). **The higher-order `AnalyticsModule` is a verified empty stub** (`namespace Pitech.XR.Analytics {}`, Phase B WS B1–B6). **Verified defect:** `AndroidUnityBridgeEmitter.EmitTelemetryBatchJson` (line 17) routes telemetry batches through the native `emitLifecycleJson` method.

**What we lack.** Any assessment/scoring engine (empty stub); xAPI/SCORM/cmi5/LRS export (proprietary schemas only); web dashboard / instructor analytics / cohort readouts (deferred to Web Portal); gaze/eye-tracking + spatial heatmaps; session recording/replay; a concrete `IAgentStateSource` (pipeline is inert plumbing); offline event persistence (in-memory queues — crash loses events); QuizSession→telemetry wiring; persistent consent storage; AI-judged attempt interpretation (deferred post-launch).

**Recommendations.**
- **[MUST]** Fix or confirm the `EmitTelemetryBatchJson → emitLifecycleJson` mismatch against the native `UnityBridgeEvents` Java contract; if telemetry/lifecycle are distinct channels, add `emitTelemetryJson` both sides; else add a clarifying comment (low). A verified bug on the only on-device telemetry path — silently mis-routed/lost batches is a launch-quality defect cheap to resolve.
- **[MUST]** Ship at least one reference `IAgentStateSource` bridging SceneManager step lifecycle + QuizSession + StatsRuntime into the observation/telemetry pipelines, keyed off ScenarioFactKeys, as a DevKit sample (medium). Both subsystems emit **nothing** without a host source; this de-risks the Web Portal dashboard ingest (data must flow before it can be visualized).
- **[SHOULD]** Add disk-backed offline persistence for both queues, flushing on next launch (medium). "Did the student complete the lab" is the contractual deliverable; lost attempt data is a credibility risk.
- **[SHOULD]** Design the Phase B AnalyticsModule around an **xAPI/cmi5 emitter from day one** (statements → tenant-scoped LRS or Supabase `analytics_events`), with AgentObservation V1 + ScenarioFactKeys as the internal source (high). Standards export is table stakes; **no first-party Unity xAPI emitter exists in the field**, so a clean tenant-scoped one is genuine differentiation. Phase B scope, but the schema choice must be made now.
- **[SHOULD]** Wire QuizSession + StatsRuntime results into telemetry (emit `quiz_completed`/score/pass-fail) (low) — the cheapest path to real, defensible deterministic assessment data.
- **[SHOULD]** Document and harden the consent + tenant-isolation story as an explicit EU AI Act / GDPR evidence artifact (fail-closed gate, JWT-per-attempt, tenant-scoped edge fn) and position it as a sales differentiator (low). No competitor advertises this; obligations land Dec 2027.
- **[COULD]** Do **not** build gaze heatmaps/replay in-house — reserve a Cognitive3D/AbxrLib integration seam, post-launch (medium). VR Builder integrates rather than rebuilds; the defensible in-house work is the learning/assessment scoring + cohort gradebook.
- **[COULD]** Add a Hub Maintain-page diagnostic for whether a scene's telemetry is actually wired (LaunchContextReporter, adapter, consent gate, state source present) (low) — prevents "no data showed up" support incidents.
- **[later]** Defer AI-judged scoring; reserve the seam (high).

### 4.5 Multiplayer / Collaboration

**How competitors do it.** **Transport is a solved, buyable problem** (Photon Fusion 2/Quantum/Voice 2, Unity NGO, Normcore, Ubiq) — adopt, never build. The differentiation buyers pay for sits **above** transport: instructor-led/moderated sessions, spectator/"ghost" modes, co-located + remote multi-user, live standardized-patient operation, record/replay debrief. Medical leaders make multiplayer a named pillar: MAGES (GA networking, claimed ~300 active users, cross-device); SimX (patented co-located + remote, live moderator); UbiSim (instructor-led, up-to-3 active + invisible ghost spectator); Acadicus (live VR + non-VR desktop together + remote standardized patients). **The closest structural analog (VR Builder) is actually WEAK here** (local multi-user, Enterprise tier only).

**Best-in-class.** MAGES (research-grade networking + cross-device); SimX (patented co-located + remote + live moderator); UbiSim (ghost spectator); Acadicus (mixed VR/desktop + standardized patients); Photon (transport baseline); Ubiq (self-hostable GDPR-safe — EU-relevant).

**What we have.** **No working multiplayer.** Verified: `NetworkingModule.cs` is an intentionally empty namespace; the asmdef gates `PITECH_HAS_FUSION` (Fusion-agnostic, no hard dependency); the **only** shipping multiplayer-relevant code is the frozen `flow.step.{guid}` naming contract in `ScenarioFactKeys.cs` (sized under the `NetworkString<_64>` cap, namespaced apart from authored scene states). This is a clean, properly-gated **seam** — design groundwork, not a feature. Consumer-side VR multiplayer is handled externally by HealthOn VR's `NetworkStateManager`; AR is single-player by design.

**What we lack.** Any networked transport in the DevKit; the WS B9 step-sync bridge (`ScenarioFlowBridge`); a `LabEventBus.StepCompleted` lifecycle hook; `IScenarioFlowStore`; late-join/catch-up; instructor/moderator control; spectator/ghost mode; co-located + remote / AR↔VR; voice/spatial audio; networked avatar presence; a NetworkBool-budget validator; multiplayer-aware analytics.

**Recommendations.**
- **[MUST]** Hold multiplayer **out of the 2026-09-10 launch**; ship single-player AR/VR (low). Explicitly after-launch per the audit; the closest analog (VR Builder) is also weak here, so omitting it is not a competitive outlier for a controlled B2B launch.
- **[MUST]** Before launch, validate the frozen contract is launch-safe: confirm `flow.step.{guid}` stays under the `NetworkString<_64>` cap for the real GUID format, `FlowStepKeyPrefix` can never collide with HealthOn VR authored scene-state keys, and the vocabulary is the single source consumers bind to — then freeze it explicitly (low). A re-spell after a consumer binds is a breaking change; getting the cap math/namespacing right now is cheap insurance.
- **[SHOULD]** Pick the transport now as a documented decision (recommend Photon Fusion 2 Shared Mode, already implied by `PITECH_HAS_FUSION` + the Make Grabbable Fusion scaffolding) + a one-page architecture note (Shared-mode authority, facts-not-index replication rule, ~64 NetworkBool budget); no code (low).
- **[SHOULD]** First post-launch increment: the launch-minimal WS B9 step-sync bridge — `LabEventBus.StepCompleted` hook + `#if PITECH_HAS_FUSION ScenarioFlowBridge` writing `flow.step.{guid}` booleans over the consumer `NetworkStateManager`, with a design-time capacity validator (medium).
- **[SHOULD]** When multiplayer carries learner data, route it through the tenant-scoped, consent-gated, RLS + signed-URL spine, and treat multi-participant assessment/observation as in-scope for Annex III (medium). Designing tenant-aware from the first increment is far cheaper than retrofitting.
- **[COULD]** Late-join catch-up (replay the current fact set so peers fast-forward) (medium).
- **[later]** Instructor/spectator tooling + voice/avatar presence (adopt Photon Voice 2 or Ubiq for EU self-host), graduating to typed `IScenarioFlowStore` (high) — the heaviest lift, Phase E, tied to the "embedded instructor inside AR/VR labs" vision.

### 4.6 Localization

**How competitors do it.** Unevenly developed — **one of the few dimensions where the closest competitors are NOT all far ahead.** Most surgical-procedural vendors (Osso, PrecisionOS, Vantari, Medical Realities, Level Ex) are effectively **English-first** — a genuine opening for an EU/Greek-first product. The leaders localize at the **clinical** layer: Body Interact (10+ languages adapted to local clinical guidelines); Uptale (one-click auto-translation of text AND audio narration into 50+ languages with Microsoft neural voices). Unity-SDK comparators consume Unity's official Localization package (String/Asset Tables, Smart Strings, runtime switching, Addressables locale loading, RTL, TMP, Google Sheets sync) — VR Builder gets "free" parity this way. MAGES ships a competent but conventional key-based UI-text system (no RTL/voiceover/pipeline). **RTL and automated translation/voiceover pipelines are under-served field-wide** (only Uptale clearly leads).

**Best-in-class.** Body Interact (clinical localization — "localize the medicine, not just the words"); Uptale (translation+voiceover pipeline bar); Unity Localization package (the consume-don't-build foundation); MAGES (a working in-SDK keying model, though limited).

**What we have.** **A RESERVED SLOT with zero working implementation** (verified). `LocalizationModule.cs` is an intentionally empty namespace; the asmdef gates `PITECH_HAS_LOCALIZATION` (wired but gates nothing yet); `LocalizationPage.cs` is an observer-only "coming in Phase B" Hub card. The reserved-slot discipline is genuinely good, but there is **no** string-table infrastructure, locale resolution, language switching, translation, or voiceover today.

**What we lack.** Any string-table/keyed-string infrastructure; locale resolution / language switching; localized scenario/quiz text (`QuizUIController` hardcodes English `q.prompt`/`a.text`/literal "Correct"/"Wrong"; CueCard text lives in GameObjects); a translation pipeline (the HealthOn VR GlobalObjectId manifest + ManualTranslationIO prompt not relocated); voiceover/audio localization; analytics/lab text keying; Greek as a shipped capability; RTL (scarce field-wide, lower priority for Greek/EU Latin script); clinically-localized content (Body Interact bar).

**Recommendations.**
- **[MUST]** For launch, treat Greek+English as **build-baked, per-build-fixed-locale** (the Phase B WS B7 plan) and de-scope runtime switching; decide explicitly whether any localized text ships at launch or whether 1.0 is English-only with Greek fast-follow (low). A scope decision the launch SSOT (VICKY_1_0_LAUNCH) must own — shipping English-only first is a legitimate cutline. **Verify against VICKY_1_0_LAUNCH before committing.**
- **[SHOULD]** Adopt `com.unity.localization` as the foundation — **do not hand-roll** a string-table system; build the WS B7 module on String/Asset Tables behind the existing gate (medium). It's what VR Builder consumes for free parity; MAGES's bespoke system (no RTL/voiceover/pipeline) is the cautionary example.
- **[SHOULD]** Establish ONE unified keying standard across all authored text before authoring volume grows; replace the hardcoded `QuizUIController` literals with key lookups first (medium). Every week without a key model multiplies the retrofit cost.
- **[COULD]** Relocate the HealthOn VR GlobalObjectId manifest + ManualTranslationIO medical-translate prompt into the DevKit (VICKY drafts, human reviews — the §28.5 seam); keep cloud runtime fetch out of launch (medium). Mistranslated clinical terms are a patient-safety/trust risk; coordinate the cross-surface (DevKit + HealthOn VR consumer) blast radius.
- **[COULD]** Frame localization in EU AI Act / GDPR terms — keep medical translations human-reviewed (not auto-published); document the human-in-the-loop control now (low).
- **[later]** Localized voiceover (Web Portal + VICKY translate + ElevenLabs/neural-TTS + runtime fetch) as a post-launch differentiator scoped against Uptale's bar (high).

### 4.7 Other Distinctive Platform Features

**How competitors do it.** Almost every distinctive capability is a **product** feature, not an SDK feature. Five recurring frontiers: (1) **embodied AI tutoring/proctoring grounded in customer clinical docs** — ORamaVR JARIA (IFU-trained, STT+TTS, behavior-tree agents, in every product) is the most direct, threatening analog to VICKY; LLM virtual patients at HyperSkill/Bodyswaps/Wonda/VictoryXR; (2) recording/replay (XR Recorder, Acadicus, SimX, Cognitive3D); (3) content marketplaces (MAGES SIM Library, SimX, PIXO Apex, HyperSkill); (4) standards-based LMS interoperability; (5) enterprise security/compliance (SOC 2, ISO 27001, SSO/SAML/DRM). **Conspicuously, NO competitor advertises EU AI Act high-risk/Annex III compliance or hard per-tenant isolation as a feature — genuine whitespace.**

**Best-in-class.** ORamaVR JARIA (the grounded-tutor analog); ORamaVR XR Recorder; Cognitive3D (analytics + MCP); HyperSkill (LLM virtual patients); Bodyswaps (healthcare comms + LTI 1.3); ArborXR/ManageXR (delivery + assessment events to 500+ LMS); PIXO/Innoactive (marketplace + SOC 2 + CloudXR spectator).

**What we have.** A small but genuinely distinctive DX/platform set, several production-real: the **AgentSubstrate AI observation hook** (fail-closed, consent-gated, frozen-V1, retrying queue → cloud edge fn — purpose-built as VICKY's eyes, the closest thing to a JARIA-style hook, but **observation-only**: no STT/TTS, no tutoring/proctoring/scoring/embodied agent, and **ships no concrete `IAgentStateSource`**); the **DevKit Hub cockpit** (observer-only 6-page UIToolkit window); the **Evaluate Changes quality gate** (EditMode equivalence proofs + headless CI entry, **excludes AgentSubstrate tests due to a known failure**); Guided Setup + scene-wiring wizard; Project Health Checker; `RuntimeTelemetryAdapter`; 2 guided-setup samples. Reserved/stub: Localization, Vitals, Networking, higher-order Analytics. Absent: avatars, recording/replay, LMS/SCORM/xAPI, marketplace, embodied AI tutor, STT/TTS, gaze/eye-tracking.

**What we lack.** Embodied AI tutor as a product feature (we have the hook, not the loop); a concrete `IAgentStateSource` (inert out-of-box); recording/replay; standards-based LMS interop; content marketplace; gaze/eye-tracking + heatmaps; avatars/social presence; surfaced SOC 2/ISO posture; a shipped, demonstrable EU AI Act compliance feature set (transparency notices, AI-output logging/traceability, human-oversight affordances); Evaluate Changes coverage of the AI subsystem.

**Recommendations.**
- **[MUST]** Ship a minimal reference `IAgentStateSource` + wiring sample capturing step/branch/quiz/selection state into the emitter (medium). The sharpest gap — the consent-gated emitter (our most distinctive launch asset) emits nothing without bespoke per-project work; a hard prerequisite for any AI tutoring demo at launch. (Overlaps the §4.4 must.)
- **[MUST]** Fix Evaluate Changes to cover AgentSubstrate (resolve or quarantine the known failing test so the suite runs green with it included) (low). The most novel, compliance-relevant subsystem (AI eyes feeding a high-risk Annex III agent) is currently unprotected by the regression gate — the wrong risk to carry into commercial launch.
- **[SHOULD]** Add an explicit EU AI Act high-risk readiness seam around AgentObservation: tamper-evident local logging of what the AI observed and any tutor output surfaced, a documented human-oversight affordance (instructor mute/override), and a learner-facing AI transparency notice hook — reserved-but-real now, full implementation pre-Dec-2027 (medium). Compliance is a launch **gate**; **no competitor surfaces this**, so it is genuine differentiation. The consent gate exists; logging/traceability/oversight are the next pillars.
- **[SHOULD]** Bridge RuntimeTelemetry/AgentObservation to xAPI/cmi5 + thin LTI 1.3 grade-passback at the Web Portal edge, mapping the frozen ScenarioFactKeys vocabulary; keep it edge-side to avoid IL2CPP/stripping pitfalls (medium). (Aligns with §4.4.)
- **[COULD]** Lightweight session recording/replay by serializing the existing telemetry/observation event stream to a replayable timeline (Web Portal, not on-device) — ~70% of debrief value at a fraction of full motion+voice 3D capture (medium).
- **[COULD]** Document a buyable spatial-analytics (Cognitive3D) integration story for gaze heatmaps rather than building our own, gated behind tenant-isolation review (low).
- **[later]** Defer avatars, embodied in-headset tutor rendering, marketplace, SOC 2/ISO programs (high).

---

## 5. Prioritized Roadmap

Effort is rough (low / medium / high). "Launch-blocker" = should land before 2026-09-10; "post-launch" = sequenced after.

### MUST (launch-blockers / correctness)

| # | Recommendation | Dimension | Effort | Type |
|---|---|---|---|---|
| M1 | SerializeReference migration/upgrade tooling + live routing-integrity validation + repair action | Scenario | medium | Launch-blocker |
| M2 | Headless/CI build entry point (`-batchmode -executeMethod`, exit 0/1) reusing existing services | Pipelines | medium | Launch-blocker |
| M3 | Decide + wire the publish-to-CCD ownership boundary (editor upload **or** portal-as-publisher) | Pipelines | medium | Launch-blocker |
| M4 | Reference `IAgentStateSource` + wiring sample (makes AI substrate work day-one) | Analytics / Other | medium | Launch-blocker |
| M5 | Fix Evaluate Changes to cover AgentSubstrate (resolve/quarantine the known failing test) | Other | low | Launch-blocker |
| M6 | Fix/confirm `EmitTelemetryBatchJson → emitLifecycleJson` native bridge mismatch | Analytics | low | Launch-blocker |
| M7 | Validate + explicitly freeze the `flow.step.{guid}` contract (cap math, collision-safety) | Multiplayer | low | Launch-blocker |
| M8 | Decide launch localization scope (build-baked Greek+English vs English-only fast-follow) — verify vs VICKY_1_0_LAUNCH | Localization | low | Launch-blocker |
| M9 | Hold multiplayer out of launch; ship single-player AR/VR | Multiplayer | low | Launch-blocker (decision) |
| M10 | Honest positioning: code-owned, tenant-isolated, EU-AI-Act SDK that wraps Unity/Meta; anatomy/haptics consumer-supplied | In-scene | low | Launch-blocker (narrative) |

### SHOULD (high-value; mostly post-launch, some launch hardening)

| # | Recommendation | Dimension | Effort | Type |
|---|---|---|---|---|
| S1 | Scenario templates/starter prefabs + "New Scenario" wizard (keep launch on the graph) | Scenario | medium | Launch hardening |
| S2 | Freeze a JSON scenario-definition export/import contract | Scenario | medium | Post-launch enabler |
| S3 | Harden + document cohort/tenant pinning + `(tenant,cohort)→release` mapping | Pipelines | low | Launch hardening |
| S4 | State-machine orchestrator (auto-advance via existing services) behind headless entry | Pipelines | medium | Post-launch |
| S5 | Concrete default `ContentDeliveryStatusOverlay` UI | Pipelines | low | Launch hardening |
| S6 | Disk-backed offline persistence for both telemetry queues | Analytics | medium | Launch hardening |
| S7 | Phase B AnalyticsModule designed around xAPI/cmi5 emitter from day one | Analytics | high | Post-launch (decide schema now) |
| S8 | Wire QuizSession + StatsRuntime results into telemetry | Analytics | low | Launch hardening |
| S9 | Document + harden consent + tenant-isolation as EU AI Act / GDPR evidence artifact | Analytics / Other | low | Launch hardening |
| S10 | EU AI Act readiness seam (AI-output logging, human-oversight affordance, transparency hook) | Other | medium | Post-launch (pre-Dec-2027) |
| S11 | Wrap Unity XRI behind a hardware-agnostic grab/poke/ray facade | In-scene | medium | Post-launch |
| S12 | Minimal typed Vitals component set (author-set, no engine) — confirm launch need first | In-scene | medium | Conditional |
| S13 | Adopt `com.unity.localization`; build WS B7 module on String/Asset Tables | Localization | medium | Post-launch |
| S14 | Unified text-keying standard; de-hardcode `QuizUIController` strings | Localization | medium | Post-launch |
| S15 | Pick + document the transport decision (Fusion 2 Shared Mode) + 1-page architecture note | Multiplayer | low | Post-launch enabler |
| S16 | Launch-minimal WS B9 step-sync bridge + capacity validator | Multiplayer | medium | Post-launch |
| S17 | Route multiplayer learner data through tenant-scoped/consent-gated spine; Annex III scope | Multiplayer | medium | Post-launch |
| S18 | Bridge telemetry → xAPI/cmi5 + LTI 1.3 grade-passback at the Web Portal edge | Analytics / Other | medium | Post-launch |

### COULD (worthwhile, non-blocking)

| # | Recommendation | Dimension | Effort |
|---|---|---|---|
| C1 | XRI affordance haptics (controller-pulse confirmation) on Selectable/Quiz | In-scene | low |
| C2 | Curated clinical DevBlocks layer (instruments, vitals monitor, hand-grab poses, CLINICAL category) | In-scene | medium |
| C3 | Lightweight persistence for quiz/stats/session state with telemetry seam | In-scene | low |
| C4 | Client-side catalog/bundle integrity verification (reuse content hash) | Pipelines | low |
| C5 | Cognitive3D/AbxrLib integration seam for gaze heatmaps/replay (don't build in-house) | Analytics / Other | low–medium |
| C6 | Hub Maintain-page telemetry-wiring diagnostic | Analytics | low |
| C7 | Post-launch AI authoring: prompt → frozen scenario-definition, human-in-the-loop, audit-logged | Scenario | high |
| C8 | Relocate medical-translation manifest + VICKY-drafts/human-reviews prompt into DevKit | Localization | medium |
| C9 | EU AI Act framing for medical translations (human-reviewed, documented) | Localization | low |
| C10 | Late-join catch-up on the step-sync bridge | Multiplayer | medium |
| C11 | Lightweight session recording/replay from the existing event stream (Web Portal) | Other | medium |

### LATER (post-launch strategic / heavy)

| # | Recommendation | Dimension | Effort |
|---|---|---|---|
| L1 | No-code / professor-facing web-portal authoring surface (edits the frozen contract) | Scenario | high |
| L2 | Graph ergonomics polish (full GroupStep child nodes, edge-drop undo, persisted expand state) | Scenario | medium |
| L3 | Promote StatsRuntime to a typed/constrained model ahead of Phase B vitals | In-scene | medium |
| L4 | cmi5/SCORM/LTI launch+tracking delivery integration | Pipelines | high |
| L5 | Delta/incremental patching + multi-environment release promotion | Pipelines | high |
| L6 | AI-judged / model-based attempt scoring (reserve the seam) | Analytics | high |
| L7 | Instructor/spectator tooling + voice/avatar presence + typed `IScenarioFlowStore` | Multiplayer | high |
| L8 | Localized voiceover cloud pipeline (VICKY translate + neural TTS + runtime fetch) | Localization | high |
| L9 | Avatars, embodied in-headset tutor rendering, content marketplace, SOC 2/ISO programs | Other | high |

---

## 6. Strategic Takeaways for a Medical-XR B2B SDK

**Where to mirror ORamaVR (and the field).** ORamaVR is the proof that a Greek-rooted academic spin-off can build a durable, research-backed med-XR business — and its choices map directly onto credible product bets:

- **Treat the grounded AI tutor as the headline capability, not a side feature.** JARIA — IFU-grounded, in every product, wired to analytics — is the single most direct analog to VICKY and is *already shipping*. Our AgentSubstrate is the right architectural foundation, but it must close the loop (state source → grounding → in-world tutoring/feedback) to compete on the dimension that most defines this market.
- **Make assessment scoring + a supervisor/instructor dashboard first-class.** MAGES per-action 0–100 scoring, Osso's accreditation mapping, and the universal LMS/standards expectation all point the same way: *deterministic, author-defined scoring emitted to a dashboard* is the table-stakes learning story we currently lack entirely (empty AnalyticsModule). Build it standards-first (xAPI/cmi5) so EU universities' Moodle/Canvas can consume it.
- **Adopt, never rebuild, the commodity layers.** Transport (Photon Fusion 2 / Ubiq), interaction (XRI/Meta), localization (Unity Localization), spatial analytics (Cognitive3D), avatars/tracking (Meta Movement). A small team's runway is wasted reinventing solved problems; the field's leaders integrate them.

**Where to deliberately diverge.**

- **Do NOT chase surgical-sim depth.** GA soft-body deformation, sub-mm force haptics, ~300-user GA networking, and large validated SIM marketplaces are multi-year, R&D- and hardware-backed moats (MAGES, FundamentalVR). Competing head-on loses. Stay broader: **non-surgical med-ed authoring** over generic + curated-clinical primitives, not bespoke surgical physics.
- **Do NOT build a closed SaaS.** The defensible identity is a **code-owned, embeddable Unity SDK** with tenant isolation — the opposite of the closed surgical platforms (Osso/PrecisionOS/Vantari) that expose no authoring at all. Authoring-capability + code ownership is exactly the gap those players leave open.
- **Lead with the compliance + isolation posture no one else has.** **No competitor in any of the four clusters advertises EU AI Act high-risk / Annex III compliance or hard per-tenant data isolation as a feature.** Our first invariant (tenant isolation via RLS + JWT + signed URLs) and the fail-closed consent gate already in code are not overhead — they are the most defensible differentiation against US-origin vendors selling into EU universities. Document and sell them.

**What is genuinely differentiating about our approach (and worth protecting):**

1. **The AgentSubstrate / VICKY observation hook** — a fail-closed, consent-gated, frozen-V1, retrying telemetry substrate *purpose-built* as the eyes of a grounded medical tutor. No generic Unity baseline ships this; it is the architectural down-payment on the JARIA-class capability the market rewards. (Caveat: it must ship a reference state source and be regression-gated to be real, not latent.)
2. **Runtime cohort-pinned content delivery** — the `AddressablesRemoteUrlRewriter` pinning all bundles to one CCD release per launch context is something **no SDK competitor publicly documents**; combined with tenant-scoping it is a concrete, demonstrable answer to the field's opaque "versioning/cohort" stories (which are mostly device-group, not tenant, pinning).
3. **The DevKit Hub cockpit + Evaluate Changes quality gate** — a task-first, observer-only authoring/operations surface plus an EditMode equivalence/regression gate with a headless CI entry point. These are mature *DX and engineering-discipline* assets (the BEHAVIOR-NEUTRAL refactor spine) that let a small team evolve a high-risk SDK safely — exactly the kind of provenance/traceability discipline the Dec 2027 obligations will reward, and a maturity signal most closed competitors don't expose.

**Bottom line.** For the controlled 2026-09-10 launch to 5 known Greek universities, the existing engineer-authored step-graph + cohort-pinned delivery + consent-gated telemetry is *enough* — the launch risk is **data-safety and wiring** (SerializeReference migration, CI build, publish boundary, a working AI state source, the regression gate, the bridge defect), not feature breadth. Spend launch runway hardening what's real and honestly positioning the moat. Sequence AI authoring, standards-based assessment, multiplayer, localization, and the embodied tutor as post-launch work, each built on a *frozen contract decided now* so the later builds don't churn.

---

## 7. Sources

Deduplicated, grouped by competitor/cluster. Confidence notes from the source profiles are preserved in the per-dimension text above (e.g., MAGES "~300 active users" and "sub-10ms" are vendor/research claims, not independently benchmarked; cluster pipeline internals are low-confidence; localization absence is often absence-of-evidence).

**ORamaVR — MAGES / Creator / JARIA**
- https://docs.oramavr.com/en/latest/unity/manual/scenegraph/index.html
- https://docs.oramavr.com/en/4.0.2/mages/editor_vr.html
- https://docs.oramavr.com/en/latest/unity/manual/actions/insert_action.html
- https://docs.oramavr.com/en/3.2.1/unreal/manual/actions/combined_action.html
- https://mages-docs.oramavr.com/en/latest/
- https://mages-docs.oramavr.com/en/v5.3.4/about/index.html
- https://mages-docs.oramavr.com/en/v5.3.4/experimental_features/custom_jaria/index.html
- https://mages-docs.oramavr.com/en/5.0.0/manual/analytics/
- https://mages-docs.oramavr.com/en/latest/class_reference/class_MAGES_Analytics_Query.html
- https://docs.oramavr.com/en/4.2.4/unity/tutorials/action_analytics/index.html
- https://docs.oramavr.com/en/latest/cloud_services/index.html
- https://docs.oramavr.com/en/4.3.0/unity/manual/languages/index.html
- https://docs.oramavr.com/en/4.0.2/unity/manual/ctd/deformable.html
- https://docs.oramavr.com/en/3.2.1/unity/class_reference/mages_sdk/Utilities/PrefabManager.html
- https://docs.oramavr.com/en/4.2.2/unity/getting_started/step_by_step/build_instructions.html
- https://docs.oramavr.com/en/4.0.2/mages/multiplayer.html
- https://oramavr.com/products-2/ ; https://www.oramavr.com/platform ; https://oramavr.com/ ; https://oramavr.com/launch ; https://oramavr.com/about/ ; https://oramavr.com/publications/
- https://arxiv.org/pdf/2203.10988 ; https://arxiv.org/pdf/2209.08819 ; https://arxiv.org/pdf/2406.11560 ; https://arxiv.org/html/2108.04136v6 ; https://link.springer.com/article/10.1007/s00006-022-01253-9
- https://www.researchgate.net/publication/353838494_Deep_Cut_An_all-in-one_Geometric_Algorithm_for_Unconstrained_Cut_Tear_and_Drill_of_Soft-bodies_in_Mobile_VR

**FundamentalVR (fundamental XR)**
- https://www.prnewswire.com/news-releases/fundamentalvr-launches-fundamental-core--a-ground-breaking-sdk-that-accelerates-immersive-surgical-training-globally-301837240.html
- https://www.prnewswire.com/news-releases/celebrating-10-years-of-innovation--and-launching-the-future-with-fundamental-xr-302516893.html
- https://fundamentalsurgery.com/company-updates/eye-tracking-implementation/
- https://www.uploadvr.com/fundamentalvr-eye-tracking/
- https://www.robotics247.com/article/fundamentalvr_simulates_soft_tissue_capabilities_haptics_medical_education
- https://www.auganix.org/fundamentalvr-announces-new-simulated-soft-tissue-capabilities-with-kinesthetic-haptics-for-its-fundamental-surgery-platform/

**SimX / UbiSim / Body Interact**
- https://www.simxvr.com/platform/ ; /features/ ; /customizable-medical-simulation-solutions/ ; /virtual-reality-simulation-for-ems/
- https://www.ubisimvr.com/intuitive-editor ; /features/editor ; /in-the-news/ubisim-brings-ai-driven-insights-to-nursing-education
- https://www.healthysimulation.com/ubisim-nursing-simulation-virtual-reality-scenario-editor/ ; https://www.healthysimulation.com/vendor-product/ubisim/
- https://www.labster.com/blog/ubisim-acquisition
- https://bodyinteract.com/virtual-patient-simulator/ ; /blog/real-time-physiology/ ; /medical-us/

**Osso VR / PrecisionOS / Vantari / Medical Realities / Level Ex**
- https://www.ossovr.com/osso-academy ; https://www.ossovr.com/health-systems
- https://hitconsultant.net/2025/10/02/osso-vr-launches-osso-nurse-training-platform-to-accelerate-nursing-onboarding-with-virtual-reality/
- https://www.precisionostech.com/ ; https://www.vantarivr.com/vantari-platform
- https://www.fiercebiotech.com/medtech/brainlab-picks-up-level-ex-maker-video-games-for-surgeons ; https://hitconsultant.net/2020/06/29/brainlab-acquires-medical-video-game-innovator-level-ex/
- https://www.meta.com/experiences/pcvr/medical-realities-platform-desktop/1805899282773910/

**MindPort VR Builder**
- https://www.mindport.co/vr-builder ; /vr-builder-tutorials/process-editor ; /vr-builder-tutorials/vr-builder-setup ; /vr-builder-tutorials/text-to-speech-audio ; /vr-builder-manual/guidance-play-tts-audio ; /blog-articles/new-major-vr-builder-version-supporting-unity-6-and-xri-3-0
- https://mindport-gmbh.github.io/VR-Builder-Documentation/articles/core/introduction.html
- https://github.com/MindPort-GmbH/VR-Builder ; /VR-Builder/releases ; /MCP-tools-for-VR-Builder
- https://www.innoactive.io/integrations/mindport

**HyperSkill / Bodyswaps / Wonda / Uptale / CenarioVR / Warp VR / Talespin / Acadicus / Gemba**
- https://www.siminsights.com/hyperskill/ ; https://formative.jmir.org/2025/1/e65670 ; https://pmc.ncbi.nlm.nih.gov/articles/PMC12046251/
- https://bodyswaps.co/ ; /use-case/healthcare/education-institutions ; /features/lms-integration
- https://help.spaces.wondavr.com/en/articles/2625650-release-notes ; /articles/10153579-how-to-define-good-criteria ; https://www.wonda.pro/
- https://www.uptale.io/en/platform/ ; /en/education/features/
- https://www.cenariovr.com/ ; https://blog.iconlogic.com/weblog/2024/02/cenariovr-ai-wizard-makes-scene-creation-cooler-and-faster-than-ever.html ; https://community.elblearning.com/topics/cenariovr-new-publishing-feature-hybrid-scorm-ampamp-more-e7ef958f
- https://www.warpvr.com/ ; /platform
- https://www.talespin.com/copilot-designer ; https://www.cornerstoneondemand.com/resources/
- https://www.motive.io/ ; /motive-xms/
- https://acadicus.com/ ; /virtual-er/ ; /simulation-library/ ; /virtual-standardized-patients/ ; /virtual-simulation-update-2/
- https://thegemba.com/platform/ ; https://techcrunch.com/2023/01/25/gemba-a-corporate-vr-training-platform-used-by-coca-cola-and-pfizer-raises-18m/

**Cognitive3D**
- https://cognitive3d.com/ ; /product/explore/ ; /blog/mindport-cognitive3d/
- https://docs.cognitive3d.com/ ; /unity/minimal-setup-guide/ ; /unity/multiplayer/ ; /unity/components/ ; /dashboard/scene-explorer/ ; /mcp-server/getting-started/
- https://www.auganix.org/xr-news-cognitive3d-mcp-server/ ; https://blog.learnxr.io/xr-development/cognitive3d-sdk-tutorial-and-my-experience

**ArborXR / ManageXR**
- https://arborxr.com/product ; /product/insights ; https://developers.arborxr.com/docs/insights/ ; /docs/insights/quickstart/
- https://www.businesswire.com/news/home/20250501338129/en/ArborXR-Acquires-InformXR-... ; https://arborxr.com/blog/arborxr-acquires-informxr-...
- https://help.arborxr.com/en/articles/8562905-release-channels ; /9523013-version-management ; /6417975-configure-content-install-update-criteria
- https://www.managexr.com/ ; /product ; /solutions/developers ; /blog/9-ways-to-level-up-your-deployment-with-the-managexr-api
- https://help.managexr.com/en/articles/8292736-advanced-usage-of-release-channels ; /5770931-managexr-cli ; https://docs.managexr.com/cli-reference/introduction ; https://github.com/ManageXR/mxr-unity-sdk

**Innoactive / PIXO / Strivr / VictoryXR**
- https://innoactive.io/portal ; https://www.innoactive.io/features/roll-out-your-collaborative-vr-3d-apps ; /features/single-sign-on-sso ; /features/vr-cloud-streaming-with-nvidia-cloudxr ; https://developer.nvidia.com/blog/delivering-one-click-vr-streaming-using-innoactive-portal-and-nvidia-cloudxr/ ; https://aws.amazon.com/marketplace/pp/prodview-g54jqvv4jnezi
- https://pixovr.com/the-pixo-platform/ ; /pixo-launches-apex-xr-platform/ ; /security/ ; https://pixovr.happyfox.com/kb/article/45-understanding-xapi/
- https://www.strivr.com/platform ; /content-studio ; /blog/advancements-real-time-analytics-vr-training-platforms ; /blog/virtual-reality-skills-assessment ; https://support.strivr.com/creator-topics/strivr-creator-interface.htm
- https://www.victoryxr.com/ai/ ; /higher-education/ ; /arborxr/ ; /virtual-cadaver-lab/ ; /healthcare-health-sciences/

**Generic Unity baseline (XRI / Meta / MRTK3 / NGO / Photon / Addressables+CCD / Unity Localization / Meta Movement / xAPI libs)**
- https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/index.html ; /serialization.html ; /node-types.html
- https://docs.unity3d.com/Packages/com.unity.visualscripting@1.9/manual/vs-graph-types.html
- https://www.cgchannel.com/2025/08/unity-rolls-out-unity-ai-in-unity-6-2/ ; https://discussions.unity.com/c/muse/30
- https://www.articy.com/en/articydraft/free/ ; /en/importer-for-unity-tutorial-l2/
- https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@2.3/manual/whats-new-2.3.0.html ; https://github.com/needle-mirror/com.unity.xr.interaction.toolkit/blob/master/CHANGELOG.md
- https://developers.meta.com/horizon/documentation/unity/unity-isdk-hand-grab-interaction/ ; /unity-isdk-hand-pose-detection/ ; /move-overview/ ; /move-face-tracking/ ; https://developers.meta.com/horizon/blog/audio-to-expression-mixed-reality-blendshapes-movement-sdk-avatars/
- https://learn.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/mrtk3-overview/ ; .../mrtk3-spatialmanipulation/.../bounds-control ; .../solvers/solver
- https://docs.unity.com/en-us/ccd ; /ccd/dashboard ; https://docs.unity3d.com/Packages/com.unity.addressables@2.5/manual/AddressablesCCD.html ; https://docs.unity.com/ugs/en-us/manual/ccd/manual/UnityCCDWalkthrough
- https://docs.unity.com/en-us/analytics/events/custom-event
- https://github.com/gblxapi/UnityGBLxAPI ; https://www.learningguild.com/articles/use-the-gblxapi-library-to-send-xapi-statements-from-unity ; https://github.com/adlnet/Unity-xAPI-Wrapper ; https://rusticisoftware.com/products/scorm-cloud/lrs/ ; https://rusticisoftware.com/blog/an-explanation-of-cmi5-in-mostly-plain-english/
- https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.5/manual/basics/ownership.html ; @2.7/manual/learn/dealing-with-latency.html
- https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new ; /fusion/current/manual/advanced/encryption ; https://blog.photonengine.com/the-evolution-of-deterministic-multiplayer-photon-quantum-now-a-unity-verified-solution/ ; https://www.photonengine.com/voice ; https://normcore.io/documentation/guides/xr-avatars-and-voice-chat.html ; https://github.com/UCL-VR/ubiq/blob/master/README.md
- https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/QuickStartGuideWithVariants.html ; https://github.com/needle-mirror/com.unity.localization/blob/master/Documentation~/Smart/SmartStrings.md ; https://phrase.com/blog/posts/localizing-unity-games-official-localization-package/
- https://github.com/microsoft/Microsoft-Rocketbox ; https://caniplaythat.com/2025/09/05/unity-expands-native-screen-reader-support-and-accessibility-api/ ; https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html ; https://docs.unity3d.com/ScriptReference/Accessibility.VisionUtility.GetColorBlindSafePalette.html ; https://github.com/De-Panther/unity-webxr-export

*Note: per project policy, the Unity AR/VR consumer projects (HealthOn AR/VR) are not present in this workspace; statements about consumer-side multiplayer (`NetworkStateManager`) and the localization manifest relocation are taken from the internal audit's cross-references, not from reading that code on this machine.*

---

# Appendix A — ORamaVR MAGES SDK: Subsystem Mechanisms (Deep Dive)

*Added 2026-06-22. Source: a focused 9-agent mechanism-extraction pass over the MAGES primary docs (`docs.oramavr.com`, `mages-docs.oramavr.com`) and the underlying academic papers. This appendix explains **how each MAGES subsystem actually works** (execution flow, key classes, control/data flow) — a different cut from §4's dimension-mapped competitor profile.*

> **Version caveat (applies to the whole appendix).** MAGES docs span generations. The mechanisms below are well-grounded for **3.2.x–4.3.x**; the current **5.x "NXT / Creator"** generation re-architected several surfaces (no-code authoring, XRIT-style interaction, swappable transport) and some 5.x pages are still "forthcoming." The networking interpolation engine (**GATE**) and JARIA's cloud retrieval are **closed-source** — those internals are inferred from papers + class references, not from source. Inferred/version-specific points are flagged inline.

## A.0 The big picture — how MAGES is organized

MAGES is four layers stacked around one spine:

- **The spine — the Scenegraph:** a 3-level tree (`Operation → Lesson → Stage → Action`) that *is* the scenario. Everything else plugs into it.
- **SDK runtime systems:** Interaction / Prefab / Device managers, the Deep Cut deformation engine, and the GATE networking engine.
- **Cloud platform (VTC):** Azure-hosted Login + Analytics + Web Portal, bound to a build by a **Product Code**.
- **AI + content:** JARIA (embodied AI proctor) and the content tiers (Creator no-code authoring, SIM Builder done-for-you, SIM Library marketplace).

The recurring pattern: **author a tree → each Action node arms an "Event Manager" → a physical interaction trips it → the Action `Perform()`s → the Scenegraph advances.**

## A.1 Scenegraph & operation lifecycle (the spine)

**What it is:** a depth-3 tree that defines and *drives* a procedure ("operation"). Root = `Operation`; depth-1 = `Lesson` (organizational), depth-2 = `Stage` (groups related steps), depth-3 = `Action` (**only Action nodes execute behavior**). It is a *dynamic* graph — lessons can be added/deleted/alternated at runtime.

**How it works:**
1. **Author** in `MAGES ▸ SceneGraph Editor` — drag Operation/Lesson/Stage/Action nodes, wire output→input, give each a `NodeID` (0-based ordinal among siblings). Saved as **XML** (`<ArrayOfLessons>…<ActionClassName>…<Tag>Normal|Optional`).
2. The XML is assigned to `MAGESSettings.asset` ("Operation XML"). At scene start the runtime **parses it and instantiates** the node GameObjects under a "Scene Graph" object; the singleton `Operation` becomes root.
3. Each Action GameObject carries a script overriding `Initialize()` / `Perform()` / `Undo()`. On-disk convention mirrors the tree as `LessonX/StageY/ActionZ`.
4. `Operation.Perform()` is "the main function for traversing the graph" — walks Actions in NodeID order within a Stage, then next Stage, then next Lesson; returns `false` on the final lesson. It initializes the *next* Action (spawns its holograms/interactables) before it goes active.
5. `Undo()` steps the cursor back one Action and resets it (re-performable = redo). `SkipCurrentAction(bool)` advances but logs the skip for analytics.
6. **Multiplayer:** `PerformByServer()` / `UndoByServer()` make the server authoritative so every client's cursor stays in lockstep (prevents traversal desync).
7. **Branching:** decision/`Question` Actions or `Optional` nodes mutate the live tree — Optional actions stay unspawned until triggered (`optionalActions`/`destroyOnPerform` in the XML).

**Key types:** `Operation` (singleton root; 5.x namespace `MAGES::SceneGraph`, with `GraphRunner`/`MAGESSceneGraph`/`SceneGraphData` in NXT); lifecycle hooks `AddActionOnPerform/OnUndo/OnStagePerform/AfterInitialize`; `SetNextActionForInitialization(lessonID,stageID,actionID)`.

## A.2 Action Prototypes (the core authoring/execution unit)

**What it is:** an `Action` is the atomic performable step (the Scenegraph leaf). **Action Prototypes** are a small catalog of ready-made base classes encoding common training behaviors, so a developer subclasses one and overrides **one method** instead of writing interaction logic from scratch. Catalog: **Insert, Remove, Use, Tool, Combined, Optional (Unreal: "Parallel"), Animation, Question, Cut**, plus Non-Prototyped.

**How it works:**
1. Every action obeys interface **`IAction`** (`Initialize`, `InitializeHolograms`, `DifficultyRestrictions`, `Perform`, `Undo`, `DestroyAction`, `SetNextModule`). Prototypes derive from abstract **`BasePrototype`**.
2. You write e.g. `class CleanAction : UseAction` and override **only `Initialize()`** — call typed setters (`SetInsertPrefab`, `SetUsePrefab`, `SetToolActionPrefab(…, ToolsEnum.Drill)`, `SetQuestionPrefab`, `SetCutPrefabs`, `SetHoloObject`) then `base.Initialize()` **last**.
3. **What `BasePrototype` does for you:** on `Initialize` it spawns the prefabs and **arms the Event Manager** (registers the collider/interaction listeners that detect completion); on `Perform` it destroys prefabs and **clears the Event Manager**. A Non-Prototyped action implements `IAction` directly and must do all this manually ("nothing is automated").
4. **Completion signaling:** the user satisfies the interaction condition (Use/Tool: hold in the collider for a "Stay Time"; Insert: place at final placement; Question: pick the right answer → performs after a delay; Cut: cut along the target). The armed Event Manager invokes `Perform()` — **`Perform()` *is* the completion** (finalize + clean) — then control returns to the Scenegraph.
5. **Combined action:** holds an ordered `IAction` list (`AddComponent<…>()` + `InsertIActions`), advanced via `SetNextModule`; reports as ONE node, completes on the last sub-action.
6. **Optional/Parallel (branching):** candidate actions registered with `InsertIActionToDictionary(pathID, action)`; on perform it fires every candidate and whichever the user finishes first selects that path — then code in that action's `Perform()` mutates the live Scenegraph (adds/removes Lessons).

> **Gotcha:** two different `Perform`s — `Scenegraph.Perform()` (graph traversal) vs `IAction.Perform()` (one action finalizing itself). The docs warn "Don't use [`Scenegraph.Perform`] outside of scenegraph!"

## A.3 Interaction, Prefab Manager & Device Manager (in-scene input → completion)

Three cooperating subsystems that turn controller input into Action completion:

- **Device Manager** — a Unity Input System layer (`MAGESControls.inputactions` + `MAGESInputController.asset` + `InputController`) mapping physical buttons to device-agnostic Select/Activate/locomotion events (action maps: LeftHand/RightHand Interaction + Locomotion). XR backend chosen via XR Plug-in Management.
- **Interaction module (5.x, XRIT-style)** — `HandInteractor` (`IInteractor→BaseInteractor→BaseControllerInteractor→HandInteractor`) detects `Grabbable`s by **palm collider** (direct) or **raycast** (distance/UI). An **`InteractionManager`** bridges them (both must register to the *same* manager) and emits Hover/Select/Activate Entered/Exited. **Grab without parenting:** on Select, a `FixedJointTransformProvider`/`VelocityBasedTransformProvider` drives the rigidbody toward the hand (parenting would break physics); `HandPoser` snaps a `HandPose`; `Drop Distance` auto-releases.
- **Prefab Manager (`PrefabSpawnManager`, singleton)** — preloads prefabs and on an interval **auto-resets** any interactable that is unattached AND has drifted past an offset (`ResetPrefab()` restores the stored start transform) — keeps a thrown scalpel from being lost.

**The completion wiring (the key mechanism):** when an Action spawns a prefab, a **Prefab Constructor** (subclass of `GenericPrefabConstructor`) stores its start transform and calls `FindEventKey()` to set `EventManagerTriggerKey = action name` — binding the object to that action's completion trigger. Specialized constructors implement each interaction:
- `UseColliderPrefabConstructor` — accumulates "Stay Time" against an accepted-object list, then fires the Event Manager.
- `InteractableFinalPlacementPrefabConstructor` (+ `PrefabLerpPlacement`) — the **Insert** pattern: waits for the correct clone within `Max Angle Degree`, detaches it from the hand, lerps it to the final pose, destroys the held copy, notifies the Event Manager.
- `FinalizePrefabAction()` / `FinalizeByNetwork()` mark the Action done (multiplayer-aware); `prefabDetachFeature` decides what happens if the object is thrown away.

> **Gotcha:** 3.x/4.x (`PrefabSpawnManager` + `MAGESInteractableItem` + constructors) vs 5.x (XRIT-style `Grabbable`/`InteractionManager`) are different generations; exactly how 5.x's grab layer feeds the older constructor/Event-Manager path is inferred. Two-hand interaction is flagged experimental.

## A.4 Deep Cut — runtime deformation engine (Conformal Geometric Algebra)

**What it is:** the technical moat. Free **cut / tear / drill** of a rigged, soft-body anatomy mesh in VR with no pre-authored animations and no constrained cut paths. Surfaces in Unity as a **`Deformable Mesh`** component + separate **`Cut` / `Tear` / `Drill`** components (`Add Component ▸ MAGES ▸ Mesh Deformations`).

**How it works:**
1. **Author:** add `Deformable Mesh` to a rigged renderer, add **Predicates** (collider hierarchies marking regions), click **Separate Mesh** (splits into sections with `Can Modify` flags), optionally **Save Sections** as a prefab.
2. **Tool as a CGA primitive:** a cut/tear plane is a **plane multivector** `Π = n + d·e∞`; a drill is a **sphere multivector**. Each mesh vertex is "up-projected" into conformal space `R^4,1`.
3. **Classify by inner-product sign:** `sign(X·Π)` tells which side of the plane each vertex is on (drill: `sign(X·S)` = inside/outside) — replaces explicit distance math.
4. **Find intersections:** for each triangle edge whose endpoints have opposite signs, solve for the crossing point (linear for a plane, quadratic for a sphere).
5. **Retriangulate:** cut → re-triangulate crossed faces and split into two submeshes; tear → **duplicate** boundary vertices and push them apart by `alpha`; drill → remove interior, re-triangulate the hole contour (`localDensing` subdivides faces 4-way to round small holes).
6. **Skinning-weight propagation (the trick that survives animation):** every new vertex inherits bone weights by barycentric interpolation from its parent triangle, capped at the GPU's 4-bones limit and renormalized — so the cut mesh can still be animated artifact-free.
7. **Deformation as versors:** all motion is the sandwich product `M c M*` (rotor/translator/dilator), so **one algebra does rotation + translation + uniform-scale** with no matrix↔quaternion↔dual-quaternion conversions.
8. **Soft-body layer:** vertices cluster into overlapping "particles" with spring-back-to-rest velocity, giving elastic tissue feel under the cut layer.
9. **Real-time/mobile:** incrementally-updated adjacency structures (separate for tear vs drill), per-face parallelization (Drill has a `parallelize` toggle). Papers report cut ~0.44s / drill ~0.20s / tear ~0.095s on ~15.8k verts (2021); a 2022 follow-up drives continuous tears **under 10ms** (and re-bases hot paths on *Euclidean* predicates + the particle layer — so the production engine may use Euclidean math even though the conceptual framework is CGA).

Each operation fires a `UnityEvent` (`OnCutPerformed`, etc.) so scoring / Action-completion can react. **IK for surgical robotics** exists but is experimental and undocumented at the solver level.

> **Gotcha:** component API confirmed from the 4.0.2 docs; the 5.x CTD page is "forthcoming"; the multivector math lives in the arXiv papers (2102.07499, 2108.05281; DOI 10.1007/s00006-022-01253-9), not the SDK manual. The hypothesized class names `CuttableMesh`/`TearableMesh` are **not** confirmed — docs show separately-added `Cut`/`Tear`/`Drill` components on a `Deformable Mesh` base.

## A.5 Networking — GATE / "Less Is More" (the M in MAGES)

**What it is:** collaborative VR layered **on top of a transport** (Photon Realtime/PUN in 3.x/4.x; swappable in 5.x). The distinguishing piece is **GATE** (Geometric-Algebra based inTerpolation Engine): send only sparse **key transforms**, reconstruct the in-between frames **locally** via GA interpolation. This backs the "~300 concurrent *active* users in one scene" claim.

**How it works:**
1. **Setup:** register a Photon AppID; the scene's `NetworkController` holds `Network Controller Photon` + `Photon Network Metrics` + `Player Numbering`; four `Network Start Position` children are avatar spawn slots.
2. **Ownership** = Photon **Takeover**: interactable prefabs get a `PhotonID`, ownership `Fixed→Takeover`; whoever grabs an object becomes the authoritative sender of its transform.
3. **Send loop:** the owner samples a transform and sends **fewer keyframes** (~20/sec down to ~5 on poor links), each still **7 floats** (3 position + 4 quaternion) for engine compatibility — the *rate* drops, not the floats. ~33% fewer bytes/user on a good link, up to ~58% on poor.
4. **Receive/reconstruct loop (the core):** each receiver buffers the last two keyframes and fabricates the missing frames every render frame via either:
   - **Dual-quaternion ScLERP** — `D = A + ε·B`, interpolate `D_prev·(D_prev⁻¹·D_curr)^a`, decompose back to position+rotation (~16.5% cheaper than separate LERP+SLERP, jitter-free); or
   - **Multivector/motor LERP** — encode the pose as a motor `M`, blend `(1−a)M_prev + a·M_curr`, normalize, extract — avoids the costly multivector log that SLERP needs.
5. **Scenario-state sync:** Action-completion events propagate over the same Photon session (discrete RPC-style, distinct from the continuous transform stream) so every peer advances the Scenegraph identically. *(The exact RPC is not in public docs.)*
6. **Recorder reuse:** the **VR Recorder** (`InteractionRecorder`, `PropagateRecording`, `RecordingWriter`) stores sparse keyframes and replays with the *same* GA interpolation, using a per-player `wait_time` to re-sync avatars + audio.

> **Reality check:** "300 users" is a 2020 paper claim ("to our knowledge"), not an independently benchmarked production limit; marketing's "4× reduced data" ≠ the paper's ~33–58%. The GATE wire/interpolation classes are closed-source — the public `GA-Unity` package (arXiv 2406.11560) is the open analogue, not a 1:1 view. Photon was bundled in 3.x, moved to Package Manager in 4.x, and is abstracted in 5.x.

## A.6 Analytics & assessment (scoring factors → Azure cloud)

**What it is:** per-Action performance scoring on a **0–100** scale from named **"scoring factors,"** plus error/objective tallies, written locally and (4.x) streamed to an Azure-backed Analytics API for supervisor dashboards.

**How it works:**
1. **Author:** select an Action in the Scenegraph Editor, click **Analytics**, configure scoring factors/errors/objectives/events (persisted under `Assets/Resources/Analytics/`).
2. **Runtime binding:** on Action init, MAGES auto-attaches an **Analytics Managed Object Component** to each tracked GameObject (makes Collisions/Rotations/Translations observable).
3. **Factors:** built-ins are **Time, Lerp Placement, Error Colliders, Stay Error Colliders, Hit Perform Colliders, Question, Velocity** (+ Custom via `AnalyticsManager.AddScoringFactor<T>`). Each `ScoringFactor` has `Initialize()/Perform()/GetReadableData()`.
4. **Scoring math:** each factor starts at its **Importance ceiling** (VeryLittle 15% → VeryBig 100%) and **deducts** on violation — e.g. Time = −10 pts/sec over limit; Lerp Placement = full while insertion angle stays within tolerance; Velocity = −(importance weight) over a speed threshold; Question = 0 or full. Factor ceilings sum, **capped at 100**, clamped to integer.
5. **Tallies:** violations classify as **Warning / Error / CriticalError** (each a popup, counted into the session overview). Events fire on Performed/Undone/Collision/Interaction/Session Start-End; custom data via the fluent **`MAGES::Analytics::Query`** API.
6. **Local persistence (session end):** a session-summary file (username, IP, date, end time, difficulty, handedness, total score, total time, total/critical errors, warnings, session ID) + per-action score/time/error files (NXT: under `AppData/LocalLow`).
7. **Cloud (4.x):** the VR module authenticates against the **Login service** (JWT) → POSTs files to the **Analytics API** (`Username`→user folder, `Operation`→product, `Files`→session) → stored in **Azure Blob** (raw block blobs + metadata append blobs) + **Azure SQL** (Users/Products/SessionSummary) → supervisors view dashboards in the **Web Portal** SPA.

> **Gaps:** **no xAPI/SCORM/LRS** (proprietary Azure stack); no gaze heatmaps. NXT 5.x docs describe cloud analytics as *forthcoming* (local files current), so treat the full cloud pipeline as the 4.x architecture.

## A.7 JARIA — embodied AI proctor

**What it is:** the in-headset AI tutor ("the expert who never leaves the room"). A Unity conversational agent that listens to the trainee, routes intent, queries a **per-customer LLM grounded on the customer's uploaded IFU/clinical protocol**, and speaks back through an animated avatar — reading **live scenario state and live analytics**. Code in `MAGES::Experimental::EmbodimentJARIA` (experimental → version-specific). This is the most direct competitor to VICKY's grounded-tutoring vision, and it already ships.

**How it works (the conversational loop):**
1. **Grounding (offline, per-customer):** customer uploads IFU docs → a "JARIA knowledge base" (sized in pages, ~1,500 chars/page). Retrieval + inference happen **server-side** in ORamaVR's cloud; the Unity side is a thin client (`EmbodiedAIRequest`). *("Trained on your IFU"; literal RAG is inferred.)*
2. **Boot:** `EmbodimentJARIAModule` (extends `HubModule`) initializes agents and tracks the **tenant token budget**.
3. **Agent init:** an `Agent` MonoBehaviour configured by an `AgentConfiguration` ScriptableObject — spawns the embodiment character, wires `CharacterAnimator`/`CharacterAudioSource`, binds UI, and assigns `ISpeechToTextHandler` + `ITextToSpeechHandler` (region+key+voice ⇒ **Azure Cognitive Services**).
4. **Listen:** user presses Mic → STT streams partials (`Recognizing`) and fires `UserInputRecorded()` with the final transcript.
5. **Route:** `Agent.ExecuteBehavior(input)` → **`RouterBehavior`** dispatches to a downstream behavior by intent.
6. **Context injection (per behavior):** each `IBehavior.RetrievePayload()` assembles grounding —
   - `StateAssistantBahavior` *(sic)* → `StateRetriever.Instance.CurrentActionJSON` (the current Scenegraph step),
   - `DebrieferBehaviour` → `AnalyticsRetriever` JSON (errors/metrics, post-session debrief),
   - `QuestionGenerationBehavior` → prior-turn context, plus `VRGamePlayBehavior`, `MAGESJariaBehavior`.
7. **Grounded LLM call:** behavior packages `{input + retrieved state/analytics + IFU knowledge}` → `EmbodiedAIRequest.HandleStreamingResponseAsync()` streams the answer; `ChargeTokensAsync()` meters **three billable channels (STT / LLM / TTS)** against tenant credit limits.
8. **Output:** streamed text → `AgentOutput` UI → `Agent.FilterMessage` post-processes → `ITextToSpeechHandler.HandleTextAsync()` synthesizes `AudioClips` + time-aligned `Subtitles` → `CharacterAudioSource` plays while `CharacterAnimator` animates the avatar → idle, await next turn.

> **Gotchas:** experimental (docs bake in typos you must match: `StateAssistantBahavior`, `ClearAgentHistoty()`); RAG internals are cloud-side/closed; Azure-bound speech; requires connectivity + a token/credit budget.

## A.8 Cloud platform — VTC / Login / licensing / Product Code

**What it is:** three Azure web services — **Login** (IdentityServer4 identity/license/SSO), **Analytics API**, **Web Portal** (Angular SPA) — plus in-headset glue. Commercially packaged as the **VTC (Virtual Training Center)** with Creator/SIM Builder/SIM Library/Launcher around it.

**How it works:**
1. **Build-time binding — the Product Code:** a built app loads its Scenegraph XML from `Documents/ORamaVR/Story/{product_code}/`; the **same code is the license key** and the analytics "Operation" field. (Rename the code but forget the folder → silent scenario-load failure.)
2. **Login gate:** a built VR app auto-spawns `UILicenseRequestSSO.prefab` offering username/password (**OAuth2 ROPC**, legacy), **SSO browser + 4-digit code**, or direct code entry.
3. **License checkout:** the Login service's `LicenseValidationService` confirms the user holds a non-expired license for that Product (created/marked Playable in the Portal); on success the login prefab self-destructs and `UserAccountManager` caches username + JWT.
4. **Run:** Scenegraph runs the LSA pipeline, branching on user choices/errors; scores/errors accumulate.
5. **Analytics upload:** `Scenegraph.cs` configures the upload (`OnlineURL = {AnalyticsAPI}/Upload`, `Authorization: Bearer <JWT>`); at session end the module POSTs files.
6. **Storage:** the Analytics API forwards the JWT back to Login for verification (it doesn't re-auth), writes Blob (block + append) + SQL summaries.
7. **Manage/view:** Admins/Supervisors/Users sign into the Web Portal via the same IdentityServer4 token — one token valid across all three services.
8. **Commercial:** VTC sells distribution+updates, single-login-any-device, multiplayer, JARIA, recordings, analytics (CCU-billed). Content comes from **Creator** (no-code, one-click publish, customer owns 100% IP), **SIM Builder** (done-for-you), or the **SIM Library** marketplace; the **MAGES Launcher** is the deployment home-app.

> **Gotchas:** class/endpoint detail is from the 3.2-era self-hosted dev sample; hard Azure dependency (other clouds "feasible but unsupported"); VTC-internal distribution mechanics aren't documented at code level.

## A.9 Build pipeline, SDK packaging & localization

**Build/packaging (wizard-driven, per-platform):**
- **Distribution differs by generation:** Unity 4.x = ORamaVR **scoped npm registry** (account required); **MAGES 5 NXT = Unity Asset Store**; Unreal 4.x = **engine plugin** from the ORamaVR Portal (installed into `Engine/Plugins`, exactly UE 4.27).
- **Flow:** switch platform → enable an XR backend via **XR Plug-in Management** (Oculus / Wave XR / OpenXR) → drop the **`Universal_XR_Rig`** (`MAGES ▸ Cameras ▸ Universal XR`) → **`Android Manifest Generator`** (`Third Party SDK Manager`) emits a device-specific manifest → `File ▸ Build`. Targets: Quest, VIVE Focus Plus, HoloLens 2 (UWP/OpenXR), Magic Leap, Windows; macOS is Desktop-3D only.

**Localization — the MAGES Languages system:**
- `MAGES ▸ Generate MAGES Languages File` creates a **Languages ScriptableObject** = a `(language × key) → message` store; "Active Language" selects the runtime language (language names truncated to 3 letters, e.g. ENGLISH→ENG).
- A loader on `SCENE_MANAGEMENT` binds the active asset (feeds both UI and analytics/error text).
- Two binding mechanisms: **`UILanguageSwap`** (swaps one text component by key) and **`MAGESTranslation`** (auto-discovers public text values on a MonoBehaviour for script-driven text).
- **JSON import/export** (`LanguageTranslationMsg.json`) — import **overwrites everything** (legacy-migration path).
- **It is a text-key system only:** no voiceover localization, no RTL, no built-in translation pipeline — the developer supplies translations. Marked "under development."

## A.10 The architectural gestalt (why it is built this way)

Three unifying ideas tie MAGES together — and each is a mirror to a choice we face:

1. **One spine, everything plugs in.** The Scenegraph's Action node is the universal hook — interaction completion, scoring, networking sync, and JARIA's state-grounding all key off the *current Action*. *(Our analog: the flat `[SerializeReference]` Step graph + the `ScenarioFactKeys` vocabulary as the seam all consumers bind to.)*
2. **One math, reused twice.** Geometric Algebra powers both the deformation engine *and* the networking/recorder interpolation — "send keyframes, reconstruct locally" is the same idea in both. *(We have no equivalent shared-primitive bet.)*
3. **Product Code binds the whole loop.** One string is simultaneously the scenario folder, the license key, and the analytics operation ID — coupling build → cloud → analytics. *(Our analog — and arguably cleaner — is `LaunchContext` + tenant/cohort-scoped delivery rather than a single coupled string.)*

**Net read for our DevKit (see §6 for the full strategic take):** MAGES is a far larger, two-engine, research-backed product suite. We will not out-feature it on surgical-sim tech, GA deformation, multiplayer, or a validated SIM library. The defensible divergences are the ones already latent in our architecture: strict **per-tenant/per-cohort version pinning**, the **consent-gated AgentObservation → VICKY** spine (vs JARIA's per-customer IFU grounding), web-portal authoring for non-surgical med-ed, and an explicit **EU AI Act high-risk** posture as a shipped feature.

### Appendix A sources (ORamaVR / MAGES primary docs + papers)

- Scenegraph & actions: `docs.oramavr.com/en/latest/unity/manual/scenegraph/index.html`; `/unity/manual/actions/insert_action.html`; `/3.2.1/unreal/manual/actions/combined_action.html`; `mages-docs.oramavr.com/en/latest/`; `/en/v5.3.4/about/index.html`
- Interaction/prefab: `docs.oramavr.com/en/3.2.1/unity/class_reference/mages_sdk/Utilities/PrefabManager.html`; `mages-docs.oramavr.com/en/5.0.0/`
- Deformation (Deep Cut): `docs.oramavr.com/en/4.0.2/unity/manual/ctd/deformable.html`; arXiv `2108.05281`, `2102.07499`, `2209.08531`; DOI `10.1007/s00006-022-01253-9`
- Networking (GATE): `docs.oramavr.com/en/4.0.2/mages/multiplayer.html`; arXiv `2203.10988` ("Less Is More"), `2406.11560` (GA-Unity)
- Analytics: `mages-docs.oramavr.com/en/5.0.0/manual/analytics/`; `/class_reference/class_MAGES_Analytics_Query.html`; `docs.oramavr.com/en/4.2.4/unity/tutorials/action_analytics/index.html`
- JARIA: `mages-docs.oramavr.com/en/v5.3.4/experimental_features/custom_jaria/index.html`; `oramavr.com/platform`
- Cloud/VTC: `docs.oramavr.com/en/latest/cloud_services/index.html`; `oramavr.com/products-2/`; `oramavr.com/platform`
- Build & localization: `docs.oramavr.com/en/4.2.2/unity/getting_started/step_by_step/build_instructions.html`; `/en/4.3.0/unity/manual/languages/index.html`; `/en/4.0.2/unreal/getting_started/step_by_step/download_mages_sdk.html`

