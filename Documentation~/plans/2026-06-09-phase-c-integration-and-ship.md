---
title: Phase C - Integrate & Ship (DevKit launch, integrate-and-ship)
status: RATIFIED by Petros (board) 2026-06-10 - pending Stergios final review; dispatch on his sign-off
date: 2026-06-09
owner: Alexandros & Stergios & Phoebos
phase: C
gate: PIT-369 store-submission window - Apple TestFlight + Google Play internal track IN by 2026-08-15 (no slip room)
owners:
  - Alexandros (phase lead - DevKit integration + store builds)
  - Stergios (DevKit integration + editor automations)
  - Phoebos (lab content: analytics config + localization keys per lab)
  - Alex (store-build + submission support; absorbs store lead during Petros vacation windows)
  - Lovable (cloud ingest + portal dashboards - cross-repo lane)
  - Cursor (Mobile App UaaL embed + casting entry point)
launch_date: 2026-09-10
devkit_lock_target: 2026-09-07
references:
  - 2026-06-09-devkit-launch-plan.md (umbrella / index)
  - 2026-06-09-phase-a-refactor-and-foundation.md (Phase A - refactor + foundation; WS A1..A8)
  - 2026-06-09-phase-b-analytics.md (Phase B - analytics; WS B1..B8)
  - _after-launch/2026-06-09-after-launch-plan.md (post-launch Phases D..I + the four domain systems)
  - ../specs/2026-04-23-devkit-1.0-target-architecture-design.md (architecture; §8 Layer 2 - Runtime: ScenarioRunner / LabConsoleRuntime; §28 addendum - domain & content systems)
  - _archive/2026-04-23-p1-foundation.md (superseded prior P1 - archived)
  - _archive/2026-05-08-p2-behavior-roadmap.md (archived post-launch roadmap - consolidated into the after-launch plan)
---

# Phase C - Integrate & Ship (to 2026-09-10)

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` (or `superpowers:subagent-driven-development`)
> to implement WS-by-WS. Steps use `- [ ]` checkbox syntax for progress tracking. This is the integration-and-release
> phase, not new architecture - read the per-WS gate before opening any PR.
>
> **Completion discipline (Petros, 2026-06-10): every phase completes IN FULL - every small step ticked, none
> skipped.** Steps tagged **[HUMAN]** are human-owned; AI agents working this phase MUST remind the human owner of
> any unticked [HUMAN] step and must not declare a WS done while one is open.

> **What this is.** The third and final launch phase. Phase A (refactor + foundation, WS A1..A8) and Phase B
> (analytics, WS B1..B8) produce a new DevKit package and a working end-to-end analytics path. Phase C rolls that
> package into every shipping lab, proves nothing broke, gets analytics live cloud-to-portal, and **ships through the
> app stores by 2026-09-10**.
>
> **Status.** RATIFIED by Petros (board) 2026-06-10 - pending Stergios' final review; dispatch on his sign-off.
> Local edits only; Petros runs all git.
>
> **Terminology - one lettered sequence (Petros 2026-06-10).** Launch = Phases A / B / C; the letters continue
> post-launch: Phases D..I (see `_after-launch/2026-06-09-after-launch-plan.md`). The numbered "P1/P2/P3" naming is
> RETIRED; the spec's §17 internal numbering maps via spec §28.7.

**Goal:** Roll the new DevKit into every lab, prove nothing broke, get analytics live end-to-end, and **ship through
the stores by 2026-09-10** for the controlled commercial B2B launch (5 existing paid beta universities onboard day 1;
no public signup).

**Architecture stance:** integrate-and-ship. No new architecture lands in Phase C - it consumes the Phase A net and
the Phase B additive runtime, rolls them into the consuming Unity projects, and releases. Every behaviour-affecting
change is gated by the Phase A WS A3 equivalence net (the "Evaluate Changes" gate).

**Spec reference:** `../specs/2026-04-23-devkit-1.0-target-architecture-design.md` - §8 (Layer 2 Runtime:
ScenarioRunner / LabConsoleRuntime), §15.2-§15.3 (non-breaking contract + the equivalence gate the WS A3 net
implements), §28.3 (localization keyed Greek+English at launch via the build-baked pipeline; cloud pipeline
post-launch). Analytics transport contract = the frozen `AnalyticsEventV1` (§8 telemetry tap).

**Duration / window:** **~2026-07-15 -> 2026-09-10** (Petros 2026-06-10: Phase C starts when Phase B's DevKit side
completes ~Jul-15 - updating the AR + VR labs/UaaL/VR Shell AND working the bugs we meet run TOGETHER). Store
submissions bind at **2026-08-15** (Meta Horizon submitted **2026-08-13** per the Workspace milestone). DevKit v1.0
lock target 2026-09-07.

**Exit criteria (= LAUNCHED, see §5):**
- App store live - Apple App Store + Google Play + Meta Horizon, binaries submitted inside the 2026-08-15 window (Horizon 2026-08-13).
- Analytics visible in the portal for the 5 beta universities, end-to-end VR + AR -> cloud -> portal, tenant-scoped.
- All student-facing surfaces pass QA golden path with no regression.

**Lead with the store-gate-is-hard rule (refinement 1):** the store gate is the binding constraint, and it runs in
PARALLEL with / AHEAD of DevKit polish - it is NOT the tail of this phase. PIT-369 (Apple TestFlight + Google Play
internal track) must be **submitted by 2026-08-15** so review windows clear before 2026-09-10. There is no slip
room on that date. The common failure mode is to treat "submit to stores" as the last task after all polish is done;
on a ~13-week calendar that loses the launch. Phase C therefore pulls the store builds (WS C4) forward to run
alongside the lab-integration sequence, and treats authoring/usability polish (WS C5) as the work that absorbs slip -
never the store gate.

Three things must all be true on launch day:

1. **Integrated** - the new DevKit package is rolled into HealthOn AR + HealthOn VR and every lab passes the WS A3
   net (the "Evaluate Changes" equivalence gate) inside the real project after the package bump.
2. **Live** - analytics flows VR/AR -> cloud ingest -> portal dashboards for the 5 beta universities, with lab
   definition (all steps) joined to attempt events (the numbers).
3. **Shipped** - app store live on Apple App Store + Google Play + Meta Horizon, store binaries submitted inside
   the 2026-08-15 window (Horizon 2026-08-13).

---

## Plan structure

Each workstream lists owner, gate (what must be true to start / to call it done), and acceptance (the observable
proof). **WS C4 is the binding workstream** - it does not wait on the others.

| WS | Focus | Gate / depends-on | Source / spec § |
|---|---|---|---|
| C1 | Roll new DevKit into HealthOn AR + VR + verify EACH lab via the net | Phase A WS A3 net green; tagged DevKit package available | spec §15.2-§15.3 (non-breaking contract + equivalence gate) |
| C2 | Author Action/analytics config + LOCALIZATION KEYS (Greek+English) per lab (content) | C1 green for that lab; Phase B WS B1/B2 config schema + WS B7 localization module landed | spec §28.3 (localization keyed at launch), Phase B WS B1/B2/B7 |
| C3 | Wire AR/VR emit live; connect cloud + portal end-to-end | Phase B WS B3 emission hook + WS B4 ingest + WS B5 dashboards live | spec §8 (telemetry tap on the bus), Phase B WS B3-B6 |
| C4 | STORE BUILDS (UaaL + VR Shell + Addressables) + PIT-369 submission [HARD 2026-08-15 gate] | stable Vicky-branded shell + buildable UaaL/VR/Addressables; runs in PARALLEL | cross-surface RENAME_MIGRATION + IL2CPP real-ELF gotcha |
| C5 | Bug-fix + editor automations (usability polish; the slip sink) | anytime after C1; lowest scheduling priority | spec §13 (DevKit Hub / editor surfaces), §28.5 (apply-command seam) |
| C6 | QA - golden-path acceptance | C1+C2+C3 green for the launch lab set; C4 builds in review | DoD §5 |

> **WS tags (Codex pass 2026-06-10):** **C1/C2/C3/C4/C6 = LAUNCH_BLOCKER · C5 = CAN_TRAIL (the designated slip
> sink; cutline ~Aug-10 per Ratified Decision #6).** A tagged slip is dispositioned in the Status & Progress Log -
> never silently skipped. **WS C1 start gate also includes G3a (DevKit integration contract locked, 2026-07-15 -
> mobile + VR + AR depend on it).**

---

## WS C1 - Roll the new DevKit into HealthOn AR + HealthOn VR

**Goal:** make the package bump a *proven* operation, not a hope - every shipping lab loads unchanged under the new
DevKit, verified in the real project after the bump.

**Scope / files:** the consuming Unity projects (`E:/Unity files/HealthOn AR`, `E:/Unity files/HealthOn VR`) -
package manifest bump + per-lab Evaluate-Changes run. No DevKit-package edits here; this is the consume-and-verify
step.

| | |
|---|---|
| **Owner** | Petros / Alex (Unity projects) |
| **Gate (start)** | Phase A WS A3 net exists and is green; a tagged DevKit package (Phase A core landed, ideally Phase B analytics SDK landed) is available to consume. |
| **Gate (done)** | Each lab in BOTH HealthOn AR and HealthOn VR passes the WS A3 net (`DevKit > Evaluate Changes`) **in the real project, after the package bump** - Proof A (scenario graph integrity, no dangling routing guids), Proof B (public-API additions-only), Proof C (serialized + GUID integrity, zero open->save diff). |
| **Acceptance** | Evaluate-Changes report green for every shipping lab fixture in each project; no `.meta` GUID drift; no Missing UnityEvent listeners; no dangling `[SerializeReference]` routing refs. Any red lab is triaged before C2 touches it. |

This is **the migration-safety step**. The whole point of the WS A3 net is to make a package bump a *proven*
operation, not a hope. Bumping the DevKit version in the consuming Unity project is exactly when a regenerated
`.meta` GUID during a Phase A split (WS A6 pure file splits) would null a shipped lab's step graph (the top risk
Stergios flagged), or where the Core.Editor hard-coded FullName string contract could silently break. Running
Evaluate-Changes in the real project, after the bump, is what catches that before it reaches a student. Run C1
per-project, per-lab; do not batch-bump both projects blind.

**Steps (progress tracking):**
- [ ] Step 1: Confirm the Phase A WS A3 net is green on the tagged DevKit package; record the tag consumed.
- [ ] Step 2: Bump the DevKit package manifest in HealthOn AR; reimport; capture any compile/missing-script issues.
- [ ] Step 3: Run `DevKit > Evaluate Changes` per AR lab; record Proof A/B/C green or triage red.
- [ ] Step 4: Repeat Steps 2-3 for HealthOn VR (separate bump, separate verification - do not batch blind).
- [ ] Step 5: Triage every red lab to root-cause (which Phase A WS introduced the drift) before C2 touches it.

**Acceptance:** see table - green Evaluate-Changes report for every shipping lab in each project.
**Gate:** C2 may begin per-lab only once that lab is C1-green.

---

## WS C2 - Author Action / analytics config + localization keys on every existing lab

**Goal:** every shipping lab carries its tracked-Action / analytics config AND its Greek+English localization keys -
content work on top of a C1-proven lab.

**Scope / files:** per-lab authoring inside each consuming project (Action marking + binding sheet + localization
key assignment). Content, not engine.

| | |
|---|---|
| **Owner** | Content (Phoebos / Georgia) + VR/AR engineers for binding-sheet wiring |
| **Gate (start)** | C1 green for that lab (the lab loads unchanged under the new package); Phase B WS B1/B2 per-action config schema + the worldspace tooltip + auto-error engine landed; Phase B WS B7 Localization module landed in DevKit. |
| **Gate (done)** | Every shipping lab has (a) its tracked Actions marked (one row = one action) with the minimum author config (title + target + weight) plus timing/error/critical fields where exceptions apply and the binding sheet mapping each scene's stable string ids; AND (b) its user-facing lab + analytics text **keyed**, with Greek + English values supplied via the build-baked localization pipeline. |
| **Acceptance** | Each authored lab still passes the WS A3 net (analytics config + localization keys are additive, behaviour-preserving for the runner); a dry-run attempt produces a sane readout (per-action time / errors / score + totals) end-to-end in dev; lab/analytics strings resolve in both Greek and English. **(Optional)** the JSON export per lab round-trips (lab -> JSON -> lab) and the net proves the projection preserved every step/route/behaviour. |

This is **CONTENT work, not engine work**. The Phase B authoring model (WS B1) is built so the author writes only
the happy path (ordered required actions + targets); the engine auto-counts any meaningful off-target interaction as
an error and fires a worldspace tooltip. Authors add config only for exceptions (e.g. tag a specific interactable as
critical-if-premature -> that action scores 0%). The minimum to author an Action = title + target + weight;
everything else (timing warnings, maxTime, onWrong text, critical targets) has sensible defaults. The JSON export is
**optional at launch** - existing labs keep loading by GUID unchanged; the JSON is an additive projection, not a
cutover, and its round-trip is gated on the WS A3 net.

**Localization keys (added per spec §28.3).** Alongside the analytics config, every lab's user-facing text (and the
analytics-surfaced strings) must be **keyed** at launch and shipped Greek + English through the build-baked pipeline
that Phase B WS B7 relocates into DevKit. AR gets localization for the first time. This is the launch requirement
only - the cloud self-serve pipeline (Web Portal editor + VICKY translate + ElevenLabs audio + runtime
fetch-by-language) is post-launch (after-launch plan). Keying now is what lets the cloud resolver slot under the
same keys later, so do not skip the keying even where only Greek+English baked values ship.

**Steps (progress tracking):**
- [ ] Step 1: For each C1-green lab, mark its tracked Actions (one row = one action) with title + target + weight.
- [ ] Step 2: Add exception config only where it applies (timing/maxTime/onWrong/critical); wire the binding sheet's stable string ids.
- [ ] Step 3: Key the lab's user-facing + analytics-surfaced strings; supply Greek + English values via the build-baked pipeline (WS B7).
- [ ] Step 4: Dry-run an attempt in dev; confirm the readout (time/errors/score/totals) and that strings resolve in both languages.
- [ ] Step 5: Re-run the WS A3 net to prove the config + keys stayed behaviour-preserving. (Optional) round-trip the JSON export under the net.

**Acceptance:** see table - Actions authored + keys resolving Greek+English + net still green per lab.
**Gate:** C3 emit-smoke for a lab needs that lab's Actions to exist (this WS).

---

## WS C3 - Wire AR/VR emit live; connect cloud + portal end-to-end

**Goal:** a real attempt in VR and AR lands tenant-scoped rows in the cloud and renders in the portal dashboard,
joined on scenario/step/attempt.

**Scope / files:** DevKit/Unity owns the emit side + the transport services (Phase B WS B3); the cloud ingest +
`analytics_events` DDL + portal dashboards are **Lovable's lane** in the Web Portal repo (Phase B WS B4/B5). This
workspace does NOT edit the Web Portal repo.

| | |
|---|---|
| **Owner** | Alexandros (Unity emit) + Lovable (cloud ingest + portal dashboards - cross-repo lane) + Cursor (mobile bridge leg) |
| **Gate (start)** | Phase B WS B3 emission hook live on the locked runner (step enter/complete/error -> AnalyticsApi); Analytics SDK (C# `AnalyticsEventV1` port) landed; **Phase B WS B4 cloud ingest + `analytics_events` DDL + WS B5 portal dashboards must be live on Lovable's side**. |
| **Gate (done)** | A real attempt in VR (Quest standalone) and in AR (mobile UaaL) lands rows in `analytics_events` (tenant-scoped, RLS) and renders in the portal dashboard, joined on scenario_id / step_id / attempt_id. |
| **Acceptance** | End-to-end smoke per surface: VR path `DirectCloudQueuedTelemetryService -> HTTPS POST -> analytics-events-ingest -> analytics_events -> portal`; AR path `BridgeQueuedTelemetryService -> mobile bridge -> telemetry-queue.service.ts -> cloud -> portal`. Consent-gated emission verified. Offline queue + backoff + batch verified (UnityWebRequest, IL2CPP/Quest AOT-safe). Both portal data flows present: **(A)** lab definition published once per lab version (full step list + weights, so the portal renders structure even for unreached steps - Phase B WS B6) and **(B)** per-session attempt events. |

The cloud + portal legs are **Lovable's lane** (Web Portal repo). DevKit/Unity owns the emit side and the transport
services; it does not edit the Web Portal repo from this workspace. The contract is the frozen `AnalyticsEventV1`
(rev-3 already carries scenario_id / step_id / action_id / step_state / performance_metric / attempt_number /
semantic_state). Do **not** route analytics through the AgentObservation path - they are two sinks on the same source
events; see §4 and the after-launch plan (AgentObservation / VICKY observer rides Phase G).

**Steps (progress tracking):**
- [ ] Step 1: Confirm the Phase B WS B3 emission hook is live on the locked runner and the Analytics SDK is landed.
- [ ] Step 2: Confirm Lovable's cloud ingest + `analytics_events` DDL/RLS + portal dashboards are live (the firm cross-repo "live by" date - see Open decisions).
- [ ] Step 3: VR smoke - run a real Quest attempt; verify rows in `analytics_events` (tenant-scoped) and render in the portal.
- [ ] Step 4: AR smoke - run a real mobile-UaaL attempt; verify the bridge -> telemetry-queue -> cloud -> portal path.
- [ ] Step 5: Verify consent-gating, offline queue + backoff + batch (IL2CPP/Quest AOT-safe), and BOTH portal flows (lab-definition publish via WS B6 + per-session attempt events).

**Acceptance:** see table - end-to-end smoke green per surface, joined and consent-gated.
**Gate:** C6 QA needs this track green for the launch lab set.

---

## WS C4 - STORE BUILDS + submission [HARD GATE: IN BY 2026-08-15]

**Goal:** Apple TestFlight + Google Play internal-track builds submitted by 2026-08-15 so review windows clear
before 2026-09-10; Meta Horizon prepared on the same cadence.

**Scope / files:** UaaL Android embed (Mobile App repo, Cursor), VR Shell build (HealthOn VR, Quest), Addressables
content build, and the store-submission process. Carries the Vicky brand identity.

| | |
|---|---|
| **Owner** | **Alexandros (store-build + submission LEAD - Petros 2026-06-09)** + Alex (support; covers Petros vacation windows) + Cursor (UaaL Android embed) |
| **Gate (start)** | **Pull FORWARD. Runs in parallel to C1/C2/C3 and C5 - does not wait for lab-integration or polish to finish.** Needs: stable app shell (Vicky brand swap landed per the cross-surface rename), a buildable UaaL Android embed, a buildable VR Shell, and an Addressables build. |
| **Gate (done) - BINDING** | Apple TestFlight build submitted + Google Play internal track build submitted **by 2026-08-15**, so review windows clear before 2026-09-10. **Meta Horizon build submitted 2026-08-13** (the Workspace milestone). |
| **Acceptance** | Builds produced: (1) UaaL Android embed for the Mobile App, (2) VR Shell build for Quest, (3) Addressables content build. Submissions accepted into TestFlight + Play internal track. No placeholder binaries shipped. |

**This is the binding constraint of the whole plan.** Everything else in Phase C is sequenced around protecting this
date. Specific gotcha to verify before submitting the UaaL embed:

> **IL2CPP / `libil2cpp.so` real-ELF requirement.** `unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so` must
> be a **real** ELF from a full Unity IL2CPP Android build. If Unity exported **dummy** placeholders (tiny files,
> `bad ELF magic` / "Dumm" in logcat), the app SIGABRTs on open when Unity loads. Re-export after a proper Android
> IL2CPP build (or run Gradle `buildIl2Cpp` with the Unity toolchain). Do **not** commit placeholder `.so` files.
> If the app crashes on open, run `npm run verify:unity-il2cpp` in the Mobile App repo - it must pass before any
> store submission.

Coordinate the binary identity with the Vicky brand swap (product = Vicky; domain = vickyon.com): app icon, splash,
in-app strings, store metadata (title / screenshots / description), additive `vicky://` deep-link scheme. Brand
asset lock is the cross-surface G4 (2026-08-12); the store binary upload (2026-08-15) must carry the new identity.
Bundle / package IDs stay stable (no new store listings).

> **Release control — binary freeze vs launch lock (Codex pass, Ratified Decision #3).** Two different locks:
> **(1) BINARY CODE FREEZE** — anything that must ship inside a store binary freezes BEFORE its submission: the VR
> Shell build ~**2026-08-05** (Horizon submission window opens 08-06), the mobile/UaaL build ~**2026-08-12**
> (Apple/Play upload 08-15). **(2) G3b "DevKit launch lock" 2026-09-07** = the **0.x launch-baseline tag** — version
> pinning (AR + VR pin the SAME DevKit version, cutline 08-15) + docs/content lock; it produces NO new binaries and
> is NOT package v1.0.0 (that is Phase I; spec §28.7b).
>
> **What the binary contains vs what stays remote-updatable.** Labs are **Addressables-delivered via remote
> CCD/catalog** (the shell resolves `runtimeUrl`/`addressKey`/`catalogUrl` at launch per the entitlements flow) — so
> the store binary ships the SHELL (+ UaaL embed) with **remote catalog pointers**, and **lab content can continue
> to land AFTER submission** via Addressables publishes (cohort/badge-pinned), up to a **content freeze at
> 2026-09-05** (the launch smoke window 09-05→09-08). Any content **baked into a binary** must be C1/C2-green
> before that binary's code freeze. This is why C4 legitimately does not wait for all lab integration — and why
> "no placeholder binaries" refers to the shell + embedded code, not to every lab being final at upload time.

**Steps (progress tracking):**
- [ ] Step 1: Confirm the Vicky brand swap (icon/splash/strings/store metadata/`vicky://` scheme) is landed by G4 (2026-08-12); coordinate with the cross-surface RENAME_MIGRATION.
- [ ] Step 2: Produce the three builds - UaaL Android embed, VR Shell (Quest), Addressables content build.
- [ ] Step 3: Verify `libil2cpp.so` is a real ELF; run `npm run verify:unity-il2cpp` in the Mobile App repo - must pass before submission.
- [ ] Step 4: Upload the store binary (2026-08-15) carrying the Vicky identity; submit Apple TestFlight + Google Play internal track by 2026-08-15.
- [ ] Step 5: Prepare the Meta Horizon build on the same cadence; pre-stage the submission checklist for the Petros-out windows (Alex absorbs the lead).

**Acceptance:** see table - both submissions accepted into TestFlight + Play internal track by 2026-08-15; no placeholder binaries.
**Gate:** BINDING - this gate, not polish, sets the calendar. Never let WS C5 or the Phase A tail push it past 2026-08-15.

---

## WS C5 - Bug-fix + editor automations (USABILITY polish - the slip sink)

**Goal:** make authoring a lab + its analytics + its localization keys as easy as the team can make it inside the
window - the DevKit north-star (usability, efficiency, scalability) at the editor level.

**Scope / files:** DevKit editor surfaces / the DevKit Hub cockpit (Phase A WS A2) + the apply-command seam
(spec §28.5). Lower-priority; explicit slip sink.

| | |
|---|---|
| **Owner** | Stergios / Alexandros (DevKit editor) + whoever has slack |
| **Gate (start)** | Anytime after C1 begins; lowest scheduling priority of the Phase C workstreams. |
| **Gate (done)** | DevKit lock target 2026-09-07: authoring a lab + its analytics is as easy as the team can make it inside the window. |
| **Acceptance** | Measurable authoring-friction reductions (fewer manual steps to mark an Action, to wire a binding sheet, to key a string, to publish a lab definition); no cosmetic regressions in the DevKit Hub / editor surfaces; lint/test green. |

This is the DevKit north-star work (usability, efficiency, scalability) at the editor level: make authoring a lab and
its analytics + localization config as easy as possible. **It is valuable but lower-priority than WS C4** - per
refinement 5, the deep-quality tail must NOT compete with the store deadline. C5 runs in parallel and is the explicit
place slip is absorbed: if the calendar tightens, C5 work slips, not C4. The Phase A file-splits/hygiene tail
(WS A4/A5/A6/A7 - formatting, dead-code, file splits, docs) lives under the same "background, may slip" discipline.

**Steps (progress tracking):**
- [ ] Step 1: Triage the authoring-friction backlog; rank by frequency-of-use, schedule strictly behind C4.
- [ ] Step 2: Land the high-leverage editor automations (Action marking, binding sheet, key authoring, lab-definition publish).
- [ ] Step 3: Fix bugs surfaced by C1/C2/C3 dogfooding; keep lint/test green and no cosmetic regressions.
- [ ] Step 4: Stop pulling C5 / the Phase A tail forward at the slip line (see Open decisions); freeze scope to protect C4.

**Acceptance:** see table - measurable friction reductions, no regressions, by the 2026-09-07 lock target.
**Gate:** none binding - C5 is the slip sink; it yields to C4, never the reverse.

---

## WS C6 - QA - golden-path acceptance

**Goal:** every student-facing surface passes its golden path with no regression - "perfect" is the bar Petros set.

**Scope / files:** cross-surface QA pass (labs list, lab detail, assessments, VICKY chat, assignments, AR lab launch,
VR lab run, portal analytics readout). Cursor covers the Mobile App surfaces.

| | |
|---|---|
| **Owner** | Petros / Alex + QA pass across surfaces (Cursor for Mobile App surfaces) |
| **Gate (start)** | C1+C2+C3 green for at least the launch lab set; C4 builds in review. |
| **Gate (done)** | Golden-path test of every student-facing surface passes: labs list, lab detail, assessments, VICKY chat, assignments, AR lab launch, VR lab run, and the analytics readout in the portal. |
| **Acceptance** | Every student-facing surface exercised on its golden path with no cosmetic or functional regression ("perfect" is the bar Petros set). Final acceptance sign-off feeds the Definition of Done in §5. |

**Steps (progress tracking):**
- [ ] Step 1: Confirm C1+C2+C3 green for the launch lab set and C4 builds in review.
- [ ] Step 2: Exercise the golden path on every student-facing surface (labs list, lab detail, assessments, VICKY chat, assignments, AR launch, VR run, portal readout).
- [ ] Step 3: Confirm Greek + English render correctly across the surfaces (WS C2 keys).
- [ ] Step 4: Log + triage any cosmetic or functional regression; re-test to clean.
- [ ] Step 5: Sign off acceptance; feed the result into the DoD (§5).

**Acceptance:** see table - golden path green on each surface, no regression.
**Gate:** final join; feeds DoD.

---

## 3. Critical path + parallelism

Two tracks run in parallel through Phase C. They share people but not dependencies, which is the whole mitigation for
the tight calendar.

**Track 1 - lab integration (sequential):**

```
C1 (roll package, prove each lab) -> C2 (author Actions/analytics + localization keys) -> C3 (wire emit live, cloud+portal e2e)
```

C1 -> C2 -> C3 is a genuine sequence: you cannot author analytics/keys on a lab that has not been proven to load
unchanged under the new package (C1), and you cannot smoke the live emit (C3) until the Actions exist (C2). C6 (QA)
gates on this track reaching the launch lab set.

**Track 2 - the store gate (parallel, binding):**

```
C4 (store builds + submission) ------------------------------------ [DONE by 2026-08-15] ----> review window -> 2026-09-10
```

C4 **runs alongside Track 1 and gates the launch.** It does not wait for C2/C3 to finish - the store binaries need a
stable shell + buildable UaaL/VR/Addressables, which exist independently of how many labs have analytics authored.
The order that matters (chronological): brand asset lock (G4 2026-08-12) -> store binary upload (2026-08-15) ->
DNS+SSL prod cutover (2026-08-22, post Apple review start) -> store submission accepted by 2026-08-15 -> DevKit lock
(2026-09-07) -> Apple/Play review clears -> launch (2026-09-10).

**C5 fills remaining time** and is the slip sink. **C6** is the final join: it needs Track 1's labs proven and
Track 2's builds in review.

The single most important scheduling rule: **never let C5 (or the Phase A WS A4..A7 tail) push C4 past 2026-08-15.**

---

## 4. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **Store review slips past 2026-09-10** (Apple/Play queue, rejection, resubmit cycle) | Medium | **Critical (no launch)** | Submit by **2026-08-15** - the gate is set early precisely to absorb one rejection+resubmit cycle. Treat C4 as binding; never let polish (C5) or the Phase A tail delay it. Pre-verify the IL2CPP real-ELF requirement and run `npm run verify:unity-il2cpp` before submission. Prepare Meta Horizon on the same cadence. |
| **Lovable cloud/portal not live in time** (ingest edge fn, `analytics_events` DDL, dashboards) | Medium | High (analytics not visible at launch -> fails DoD #2) | Cloud + portal is Lovable's lane (Phase B WS B4/B5) and on the C3 critical path; track it as a hard cross-repo dependency with a firm "live by" date ahead of C3 smoke. Analytics-SDK + Unity emit can land and be smoke-tested against a stub/staging ingest while the portal dashboards finish; keep `telemetry-queue.service.ts` + emit event shapes additive and reversible so a late contract change does not require a re-author. |
| **A lab fails the WS A3 net after the package bump** (regenerated `.meta` GUID nulls a step graph; Core.Editor FullName contract breaks; Missing UnityEvent listeners) | Medium | High (broken lab ships) | This is exactly what C1's Evaluate-Changes-in-real-project step exists to catch - it is a *detection* win, not a new risk. Triage any red lab before C2 touches it; root-cause to the Phase A WS that introduced the drift; do not author analytics on a red lab. Motivating precedent: LooPi's AgentObservation EditMode tests were authored but never compiled ("NOT_RUN - requires Unity Editor"), so a CS0122 shipped and only surfaced when Petros opened Unity. The WS A3 gate is the structural fix for that failure mode. |
| **Petros vacation 19-24 Aug + 28-30 Aug clashes with the store-submission gate** | High (dates known) | High (store lead unavailable at the binding moment) | **Alex absorbs the store-submission lead** for those windows (already the C4 owner of record). Pre-stage the store builds + submission checklist before 19 Aug so the 2026-08-15 submission does not depend on Petros being online. Phoebos is also out 31 Jul-10 Aug - front-load C2 content authoring (incl. localization keys) before that window. |
| **C5 / Phase A WS A4..A7 tail steals time from C4** | Medium | High | Refinement 5 governs: the deep file-splits/hygiene + usability polish must NOT compete with the store deadline. C5 is the designated slip sink; schedule it strictly behind C4. |
| **Analytics wired through the wrong sink** (analytics-SDK plan §4 says the observer should emit via AnalyticsApi.Emit, contradicting the as-built AgentObservation path) | Low | Medium | Ratified direction: two sinks on the same source events - `AnalyticsEventV1` = launch instructor dashboards; `AgentObservationV1` = post-launch VICKY observe path. PARK AgentObservation + LabConsole until post-launch (see §6 + the after-launch plan). Repairing the analytics §4 sentence + reconciling the DevKit spec is a **Web-Portal-repo edit (Lovable/Heisenberg lane), not done from the DevKit workspace.** |
| **Localization keys not authored at launch** (lab/analytics text un-keyed; only one language ships) | Low | Medium | C2 keys lab/analytics text and ships Greek + English via the build-baked pipeline (Phase B WS B7); keying is the launch requirement even where only baked values ship, so the post-launch cloud resolver (after-launch plan) slots under the same keys. Front-load before Phoebos's 31 Jul-10 Aug window. |
| **Brand-swap binary identity misaligned at upload** (icon/splash/strings/store metadata or `vicky://` scheme not landed by G4) | Low | Medium | Brand asset lock G4 = 2026-08-12, ahead of the 2026-08-15 binary upload; coordinate with the cross-surface rename migration. Bundle/package IDs stay stable so no new store listing is needed. |

---

## 5. Definition of done = LAUNCHED

Phase C is done when the launch is real. All three must hold on 2026-09-10:

- [ ] **App store live** - Apple App Store + Google Play + Meta Horizon. Binaries submitted inside the
      2026-08-15 window (C4; Horizon 2026-08-13); review cleared; listings carry the Vicky identity; bundle/package IDs stable.
- [ ] **Analytics visible in the portal for the 5 beta universities** - end-to-end VR + AR -> cloud ->
      portal, tenant-scoped (RLS), readout = lab definition (all steps + weights) joined to attempt events
      (time / errors / score per action + totals). Consent-gated emission verified (C3).
- [ ] **All student-facing surfaces pass QA** - labs list, lab detail, assessments, VICKY chat, assignments,
      AR lab launch, VR lab run, portal analytics readout; golden path on each with no regression (C6).

Supporting integration checks rolled up from the workstreams:

- [ ] Every shipping lab passes the WS A3 net in the real project after the package bump (C1).
- [ ] Every shipping lab has Action/analytics config authored; lab/analytics text is keyed and ships Greek + English; (optional) JSON export round-trips under the net (C2).
- [ ] DevKit v1.0 lock target met (2026-09-07); no cosmetic regressions in the editor surfaces (C5).
- [ ] `libil2cpp.so` is a real ELF; `npm run verify:unity-il2cpp` passes before submission (C4).

---

## 6. Post-launch handoff

Everything below is **POST-LAUNCH** (after 2026-09-10). It is parked during Phase C and picked up against the
after-launch plan once the launch is stable. Do not pull any of it forward into the launch window. The canonical
detail now lives in `_after-launch/2026-06-09-after-launch-plan.md` (which consolidates the archived
`_archive/2026-05-08-p2-behavior-roadmap.md` and the four domain systems); the pointers below are the at-a-glance map.

- **Phase D - runner extraction.** SceneManager today fuses a RUNNER (graph interpreter) + a CONTROL surface.
  Post-launch the runner is EXTRACTED and kept underneath; SceneManager-the-component survives as a thin facade
  through the transition. Server CI (deferred out of Phase A's WS A3) arrives here. The multiplayer flow-store work
  (NetworkedStates -> `IScenarioFlowStore` + runner-fact-driven late-join fast-forward; spec §28.2) rides this phase.
- **Phase E - LabConsole.** LabConsole sits IN FRONT of the extracted runner as the control plane (semantic state +
  parameters + professor controls + the only gated outside-in write path). The central hub is the **LabEventBus**,
  not LabConsole; LabConsole, Analytics/Telemetry, and Observation are all CLIENTS of the bus (siblings). Analytics
  stays a SEPARATE tap on the bus, decoupled - it shipped at launch while LabConsole is post-launch. The Vitals
  digital-twin full build (cascade rules + profiles + scene migration + ControlOptionManager-off-PUN convergence;
  spec §28.4) rides this phase as a LabConsole parameter. An OFFERED (never forced) migration converts labs to
  LabConsole-native at the 1.0 lock.
- **AgentObservation / VICKY observer.** `AgentObservationV1` is VICKY's post-launch real-time observe / Tier-3 read
  path - a SECOND sink on the same source events as the launch analytics path (see §4). The transport half is built
  (LooPi); the producer is unbuilt and consent is C3-locked. Lands post-launch (Phase G) alongside the LabConsole
  semantic-state read path.
- **Localization CLOUD pipeline.** At launch, localization is keyed Greek + English via the build-baked pipeline
  (C2 + Phase B WS B7). The full cloud content pipeline (Web Portal lab-content editor + VICKY translate + ElevenLabs
  audio + runtime fetch-by-language; the EU-scale moat; spec §28.3) is post-launch.
- **AI-assisted authoring system.** The launch ships the SEAM (schema'd JSON + deterministic editor apply-command +
  headless entry; spec §28.5). The full Hub apply-command library + eventual CLI/MCP bridge is post-launch.
- **Full JSON-source-of-truth migration.** At launch the JSON is an additive export/import projection of the
  existing `[SerializeReference]` scenario, not a cutover. The full migration to JSON-as-source-of-truth (and the
  AI-authorable scenario flow it unlocks) is post-launch.

Pointers: the after-launch plan (`_after-launch/2026-06-09-after-launch-plan.md`, Phases D..I + the four domain
systems), with the architecture detail in `../specs/2026-04-23-devkit-1.0-target-architecture-design.md` §8 (Layer 2
- Runtime) + §28 (domain & content systems), and the umbrella's post-launch section
(`2026-06-09-devkit-launch-plan.md`). The archived roadmap `_archive/2026-05-08-p2-behavior-roadmap.md` is the source
the after-launch plan was consolidated from - cite the after-launch plan, not the archive, for live post-launch work.

---

## Open decisions (for ratification)

- **Lovable "live by" dates - RATIFIED cadence with slack (Codex pass 2026-06-10; Lovable to confirm):** B4 ingest +
  B6 publish + **simulated-payload smoke 2026-08-05** -> B5 **portal readout 2026-08-08** -> **real AR+VR device
  e2e smoke 2026-08-12** -> **Aug-13/15 reserved for store uploads, not discovery**. Until Lovable confirms, this is
  the plan-of-record.
- ~~Lovable "live by" date~~ (proposed dates above; original ask retained) - the firm date that gates C3 smoke. Needs a
  commitment from the Web Portal lane so C3 is not discovered late.
- **RESOLVED (Codex review + board, 2026-06-09): `lab_scenarios` definition-publish is MANDATORY for every LAUNCH
  lab** (the portal readout needs Flow A to render structure); the JSON **import/round-trip** stays optional for
  non-launch labs. The original question is retained below for traceability.
- ~~JSON export per lab at launch: in or optional?~~ Resolved above. Original proposal: optional (existing labs load by GUID
  unchanged; round-trip gated on the net). Confirm we do not need the export for any launch lab.
- **RESOLVED - Meta Horizon submission cadence (Petros 2026-06-10):** Horizon submission window 2026-08-06 ->
  submitted **2026-08-13** (the Workspace milestone) - AHEAD of Apple/Play (<= 08-15), not a later track.
- **RESOLVED - C5 slip line (Ratified Decision #6):** anything not landed by **~2026-08-10** defers post-launch
  unless it unblocks C4.

## Deferred (out of Phase C scope)

- Phase D runner extraction, Phase E LabConsole, AgentObservation/VICKY observer, full JSON-source-of-truth
  migration, the localization CLOUD pipeline, the Vitals digital-twin full build, the AI-assisted authoring
  command library/CLI/MCP bridge - all POST-LAUNCH (see §6 + the after-launch plan).
- Repairing the analytics-SDK plan §4 "observer emits via AnalyticsApi.Emit" sentence + reconciling the DevKit spec
  - a Web-Portal-repo edit (Lovable/Heisenberg lane), not done from this workspace.
- Server CI for the equivalence net - arrives with Phase D (Phase A ships the manual "Evaluate Changes" gate only).
- EU AI Act CE-marking work - out of 1.0 scope; deadline 2 Dec 2027 leaves runway.

---

## Plan self-review (coverage check)

- [ ] Every WS (C1..C6) maps to a gate/acceptance and cites its Phase A/B dependency or spec § (C1 -> spec §15.2-§15.3; C2 -> spec §28.3 + WS B1/B2/B7; C3 -> spec §8 + WS B3-B6; C4 -> IL2CPP/RENAME; C5 -> spec §13/§28.5; C6 -> DoD §5).
- [ ] The store-gate-is-hard lead (refinement 1) is stated up front and C4 is marked the single binding gate that runs in parallel, never the tail.
- [ ] The two-track critical path is preserved with the corrected chronological chain (G4 2026-08-12 -> upload 2026-08-15 -> DNS+SSL 2026-08-22 -> 2026-08-15 submit -> 2026-09-07 lock -> review -> 2026-09-10).
- [ ] WS C2 adds localization-key authoring (Greek+English, build-baked, AR-first) alongside the analytics config, per spec §28.3; the cloud pipeline is explicitly deferred.
- [ ] The risk register, DoD = LAUNCHED, and the post-launch handoff are retained and re-pointed to the after-launch plan (archived roadmap noted as the source).
- [ ] No emission/extraction is introduced in Phase C beyond what Phase B already landed; Phase C only consumes, integrates, and releases (architecture stance honoured).

## Execution handoff

This plan is **RATIFIED by Petros 2026-06-10, pending Stergios sign-off** and is **board-staged**. Ratification path: Petros + Petros's Claude + LooPi ->
Heisenberg/Stergios -> dispatch to the human implementers (Petros/Alex for store + integration, Lovable for the cloud
+ portal lane, Cursor for the Mobile App UaaL + casting leg, Content for C2 authoring). **Local edits only; Petros
runs all git.** Per-WS owners are in each WS table; the binding gate (WS C4, 2026-08-15) is the calendar anchor every
other workstream is sequenced to protect.

---

## Status & Progress Log

> Update on EVERY WS start/close + every Evaluate-Changes green run on a milestone. Newest first. This is the
> at-a-glance progress view; the per-WS checkboxes are the detail.

| Date | WS | Event | By |
|---|---|---|---|
| 2026-06-10 | - | Window moved to ~Jul-15 -> Sep-10 (starts at Phase B DevKit-side completion; lab updates + bug-work together - Petros); store gate hardened to 2026-08-15 (Horizon 2026-08-13 per Workspace milestone); AR confirmed Unity 6 (no upgrade prerequisite); completion discipline added | Claude (board) |
| 2026-06-09 | - | Store-submission LEAD = Alexandros (Petros); `lab_scenarios` publish RESOLVED mandatory for launch labs; Lovable dated gates proposed; filed as PROPOSED (since RATIFIED 2026-06-10) | Claude (board) |

---

*Sibling phases: [Phase A - refactor + foundation](2026-06-09-phase-a-refactor-and-foundation.md) -
[Phase B - analytics](2026-06-09-phase-b-analytics.md). After-launch: [After-Launch Plan](_after-launch/2026-06-09-after-launch-plan.md).
Index: [DevKit Launch Plan](2026-06-09-devkit-launch-plan.md).*
