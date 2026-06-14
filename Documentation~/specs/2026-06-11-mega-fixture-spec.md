---
title: Mega-fixture - the census-superset synthetic corpus (WS A3 Step 11 + 12)
status: APPROVED 2026-06-11 (Stergios - decisions D1-D4 exactly as recommended; Step 12 skip-predicate hardened per Stergios' rule) - IN BUILD
date: 2026-06-11
owner: Stergios (idea + review) / Claude (spec + build)
parent: ../plans/2026-06-09-phase-a-refactor-and-foundation.md (WS A3 Steps 11-12)
companion: ../testing-and-fixtures.md (THE process; updated by this spec's §8 on landing)
provenance: v1 was grounded in a 4-way disk inventory (2026-06-11) of (a) the full Step data model,
  (b) all 7 committed graph baselines, (c) ScenarioGraphSnapshot + ExportLabAsTestFixture mechanics,
  (d) the plan census + fixture-discovery/gate code. v2 (same day) folds in a 3-skeptic adversarial
  verification (coverage / Unity-feasibility / process-coherence): 1 blocker + 8 majors + 12 minors
  confirmed and fixed below; the skeptics also independently RE-VERIFIED the load-bearing claims
  (242 listener rows all RuntimeOnly, zero fully-empty rows, the 2 clean-null-with-method rows, the
  DIPAE island, the sibling twins, sibling-folder invisibility to discovery, orphan-check
  directionality, and that readable non-GUID step guids survive every code path - Scenario.cs:695).
---

# Mega-fixture spec (v2)

**One hand-designed, generator-built fixture - zero third-party SDKs, zero project assets - that is
a strict SUPERSET of everything the WS A1 census and the 7 committed baselines contain**: every
step type, every routing family, every GroupStep mode, every listener shape, every identity and
topology weirdness real labs ship. It covers the real labs *by construction* and runs in any Unity
6 project carrying the package's implicit Unity-package deps (§5). It becomes the gate's primary
synthetic corpus: **the easy, more-than-complete test a developer runs on every DevKit change**,
with the real-scene corpus inside HealthOn VR remaining the final pre-ship tier (§7).

**Why a superset or nothing:** the net only protects what a committed fixture exercises. Proven
holes today: `passedNextGuid`/`failedNextGuid` + QuizStep/QuizResultsStep have **zero** serialized
instances; GroupStep modes `AnyChildCompletes`/`RequiredChildrenComplete`/`NOfMChildrenComplete`
have **zero** coverage; listener modes `Int`/`EventDefined` and call-states
`Off`/`EditorAndRuntime` appear in **no** baseline; the current synthetic wires **zero** listeners.
A "representative" fixture re-creates exactly this class of blind spot; only an explicit superset
closes it.

**Step 12 is part of this work item, not a follow-up.** The bare gate project runs the WHOLE
covered-assembly suite (the gate filters by assembly only - there is no per-fixture selection), so
the 6 committed real-scene fixtures make it RED there today and after the mega lands. The
"missing-deps -> Inconclusive skip" guard (plan Step 12) ships **with** the mega; until both land,
tier 1 runs in HealthOn (§7).

---

## §0 Decisions for review - **ALL FOUR APPROVED as recommended (Stergios, 2026-06-11)**

| # | Decision | Recommendation | Why |
|---|---|---|---|
| **D1** | Does the mega-fixture replace `synthetic_routing_families`? | **Replace the FIXTURE, keep the SURFACE.** The prefab + baseline are deleted in the same commit (the orphan check's prescribed path; no test code names the synthetic - grep-verified). The **public symbol `GenerateSyntheticFixture()` is RETAINED** (it is Proof-B-baselined - renaming/deleting it = a public-API removal = Proof B red + a negative-gate exception); its body builds the mega. The **menu path is UNCHANGED** (`Pi tech/Tools/Generate Synthetic Scenario Fixture` - the mega IS a synthetic fixture), so the Maintain card's hardcoded `ExecuteMenuItem` keeps resolving. Only dialog text, fixture name, the Maintain card description, and docs change. | Anti-sprawl + Proof B additions-only + no silent Hub-card no-op. **Superset precondition (v2):** the matrix now explicitly carries the three states ONLY the synthetic had - Selection `completion=AutoWhenRequirementMet` + null-heavy + `wrongNextGuid` empty-with-`correctNextGuid`-set, and MiniQuiz `completion=AutoWhenAllAnswered` (T1 second instances). Without those rows the "strict superset" claim was false (skeptic-confirmed blocker). |
| **D2** | Does the prefab-VARIANT twin (§4.2) live in the green gate folder or the mechanics folder? | **Green gate folder, conservative overrides only** (never anything under `steps`), gated by a build-time feasibility check that runs **all four** green-gate checks on it: invariants, snapshot-vs-baseline, reserialize idempotency + committed-bytes, and **override-no-churn (the harder one - it fails RED, not Inconclusive)** incl. the nearest-instance-root path assert. ANY misbehaviour -> demote to `Tests/Fixtures/Mechanics/` + a dedicated test. | Variants + `[SerializeReference]` are the known-brittle area - exactly why we want coverage AND exactly why the gate must be the full check set, not idempotency alone. |
| **D3** | Step guids: readable deterministic constants (`mega-timeline-01`)? | **Yes - RESOLVED by code review.** Every guid-assignment site is backfill-on-empty only (`Scenario.EnsureGuidsRecursive` Scenario.cs:695-696; same for the editor paths); `CheckInvariants` requires non-empty + unique + resolving, never format. Readable constants are committed-stable and make every future drift diff human-reviewable. | Verified 2026-06-11 (feasibility skeptic). No fallback needed. |
| **D4** | Ship the LegacyForms + KnownBad companions (§4.3/§4.4) in the same work item? | **Yes.** Small, and they are the only honest coverage of "old serialized states" and "the detector actually detects". | A net never proven to fire is faith, not proof. |

---

## §1 Ground truth this spec stands on (corrected v2; every item independently re-verified)

1. **11 concrete Step types** (grep-verified): TimelineStep, CueCardsStep, QuestionStep,
   MiniQuizStep, SelectionStep, InsertStep, EventStep, ConditionsStep, GroupStep, QuizStep,
   QuizResultsStep. Real labs serialize 7; the synthetic adds 2; **QuizStep/QuizResultsStep appear
   nowhere**.
2. **Routing is derived generically** by the snapshot (`*NextGuid` suffix + `specificStepGuid` +
   `childRequirements[].guid`) - the mega's job is to make every family **non-vacuous** (non-empty
   AND resolving) at least once, plus the empty-string ("fall-through") case per family.
3. **Real-lab listener truth** (242 rows across 6 real baselines): modes Bool(**182**)/Void(37)/
   Float(11)/String(6)/Object(6); call-state **RuntimeOnly for all 242** (Off/EditorAndRuntime
   never); **zero fully-empty rows** - the detritus that actually ships is the
   **clean-null-target-WITH-method** row (exactly 2: Delirium, Ekpa - both `SetActive(false)`),
   plus one byte-identical duplicate row pair (Delirium). `Int` and `EventDefined` modes occur
   nowhere. (`SetActive` = 176 of the 242.)
4. **Identity weirdness that actually ships:** Greek names (`Σωστό image`, `Panel ΜΑΠ`), Greeklish,
   trailing-space names (`Panel allagi ΜΑΠ `, `---ENVIRONMENT--- `), same-named siblings both
   referenced (Loimokseis `Panel epomeno domatio[3]` vs `[8]`; Delirium `Buttons Parent[1]` vs
   `[3]`), decorative separator roots, **`asset:` identity rows** (Loimokseis: 6 Object-mode
   listener args referencing a project Material and a Meta-SDK Material). Null refs: **30% of
   step-field refs, 49% of listener refs (41% of all 840 ref rows)**.
5. **Topology weirdness that actually ships:** 52 back-edges corpus-wide (largest contributor:
   Pharmacy's systematic wrong-answer loops, 17); a genuinely unreachable 2-step island whose
   internal link is **fall-through** (DIPAE steps 6-7: BOTH `nextGuid=""`, nothing routes in);
   duplicate-target choices (DIPAE step 0); mixed empty/set guids within one choice list; fan-in
   hubs (Delirium step 16, fan-in 10).
6. **The real G-Multi shape (corrected v2 - the v1 "verbatim/identical duplicates" reading was
   wrong):** Loimokseis groups carry 2 branches each with the SAME childRequirement guid order as
   the group-level list but **complementary `required` flags** (branch A: 1,1,0,0; branch B:
   0,0,1,1 - the flags ARE the branch semantics), **distinct labels**, branch **modes
   `RequiredChildrenComplete(3)` and `NOfMChildrenComplete(4)` with `requiredCount=1`**; the
   "Brisi" branch pair shares mode + the same `nextGuid` (two branches -> one exit) but differs in
   flags. Group-level `specificStepGuid` present-but-empty; group `nextGuid=""` (relies on branch
   exits); one group reachable ONLY via the other's branch. Branch mode/requiredCount/required
   flags are **snapshot-invisible** (only guid strings are routing leaves) - Proof C bytes are
   their only net, so the mega must pin the shipped values.
7. **OnValidate force-populates `childRequirements` on EVERY group** (all modes, all nesting
   levels - `EnsureChildRequirements` adds `{guid, required=true}` per child; branch lists are
   force-filled for MultiCondition via a HashSet walk). Empirically visible in the committed
   synthetic (a SpecificChild group carries an auto-added row). Consequence: every mega group's
   committed form includes the full normalized list; the generator must pre-author them in
   steps-list order (never trusting HashSet order) and assert OnValidate-idempotence before
   capture (§6).
8. **Fixture discovery**: recursive **within** `Tests/Fixtures/Scenarios/`; SIBLING folders
   (`Tests/Fixtures/KnownBad/`, `LegacyForms/`, `Mechanics/`) are invisible to the green gate. The
   orphaned-baseline reverse check fails any `Tests/Baseline/GraphSnapshots/*.graph.json` with no
   matching fixture **in Scenarios/** - so companion-folder fixtures must NEVER commit snapshots
   there (§4.3).
9. **Round-trip constraints** (Proof C): reserialize idempotency (RED on churn), committed-bytes
   match (Inconclusive until normalized), override-no-churn on an instantiated fixture (RED on any
   `steps`/`managedReferences` modification leak).
10. **States Unity normalizes away CANNOT live in a green fixture:** an orphaned
    `ChildRequirement`, a branch `mode=MultiCondition` (coerced back). Excluded (§5); their
    normalization behaviour is locked by `GroupStepRequirementTests`.
11. **The only `[FormerlySerializedAs]` in the model** is on SceneManager (`defaultQuiz`<-`quiz`,
    `quizPanel`<-`quizUI`, `quizResultsPanel`<-`quizResultsUI`). Step types have none - §4.3 is all
    the "old serialized states" coverage that honestly exists.
12. **Generator determinism:** with step guids pinned to constants, the **baseline JSON is
    byte-reproducible** across regenerations. Prefab-internal fileIDs still churn - acceptable
    because mega + variant regenerate **only deliberately, always together in one run** (§6.4; the
    variant's `m_Modifications` target the base's fileIDs, so regenerating the base alone would
    orphan them).
13. **OnValidate renames a holder GameObject named exactly `Scenario`** to the scenario title - the
    Scenario host object is therefore named a fixed constant that is not "Scenario" (§2 T5).
14. **`Sanitize()` touches only the fixture FILE name**; Greek/trailing-space object names flow raw
    into YAML + baseline (UTF-8, ordinal compare - stable). The fixture root GameObject takes the
    scene name, so ExportSceneCore pins it to the fixtureName constant (§6.2).

---

## §2 The coverage contract (superset matrix, v2)

One Scenario in one multi-root scene (gathered by the real export pipeline, §6), roughly **50
steps** (~26 top-level + 7 groups + ~17 group children) - the same size class as Delirium (40) /
Loimokseis (44).

> **Rule-row (v2, from §1.7):** EVERY group (all 7, incl. nested) carries the full
> OnValidate-normalized `childRequirements` list ({guid, required=true} per child) in steps-list
> order; G-Required and G-Multi's branches are merely where `required=false` appears. The §9.3
> matrix audit must expect these rows on every group.

### T1 - Step types (11/11; second instances carry the states real labs / the old synthetic ship)

| Step type | Mega instances | Field-population rules (superset of every real + synthetic use) |
|---|---|---|
| TimelineStep | 2 | #1 `director` -> scene PlayableDirector (no playable asset), `rewindOnEnter`/`waitForEnd` non-default; #2 `director` null |
| CueCardsStep | 2 | #1 fully wired: `cards[3]` (one inactive GameObject), `cueTimes[3]`, `tapHint`, `advanceMode=OnButton`, `nextButton`, `extraObject` + non-default `extraShowAtIndex`/`hideExtraWithFinalTap`/`useRenderersForExtra`, one authored `fadeCurve` + default `scaleCurve`, non-default fade/pop scalars; #2 minimal: all refs null, `TapAnywhere`, **`cueTimes` length 1 with `cards` length 3** (the documented applies-to-all shape) |
| QuestionStep | 2 | #1: `panelRoot` (RectTransform under `--- UI ---`), `panelAnimator` (Animator, no controller), non-default `showTrigger`/`hideTrigger`/`fallbackHideSeconds`, `choices[4]` covering: forward route, **back-edge route**, **empty route mixed into a routed list**, **duplicate-target choice** (two choices -> same guid); choice c0 carries `effects`; #2 minimal (panel refs null, 2 plain choices) |
| MiniQuizStep | 2 | #1: `questions[2]` x `choices[2]` with buttons on **same-named sibling parents** (the Delirium `Buttons Parent` shape), one `isCorrect`; `completion=OnSubmitButton` + `submitButton`; `outcomes[2]` with non-default `minCorrect`/`maxCorrect` (one routed forward, one **routed back**); **non-empty `defaultNextGuid`**. #2 (the shipped + synthetic states): **`completion=AutoWhenAllAnswered`**, `defaultNextGuid=""`, **≥1 `MiniQuizChoice.onSelected` listener row** + **≥1 `MiniQuizChoice.effects` entry** (Delirium ships 12 such rows + 1 such list; v1 had zero) |
| SelectionStep | 2 | #1 fully wired: `lists` -> SelectionLists component, `listKey`, `completion=OnSubmitButton`, `submitButton`, `requiredSelections=2`, `requireExactCount=true`, **`allowedWrong=1`**, `timeoutSeconds=30`, `correctNextGuid` + `wrongNextGuid` BOTH set, `panelRoot`, `hint`, wired `onCorrect`/`onWrong` + **`onCorrectEffects`/`onWrongEffects` entries** (first-ever coverage of all four). #2 (the old synthetic's unique states - D1 precondition): null-heavy (`lists`/`panelRoot`/`submitButton` null), **`completion=AutoWhenRequirementMet`**, **`listIndex` used instead of `listKey`**, `correctNextGuid` SET + **`wrongNextGuid` EMPTY** (the asymmetric route - the only empty instance of this family anywhere) |
| InsertStep | 2 | #1: `item` -> Transform with **trailing-space name**, `targetTrigger` -> BoxCollider, `attachTransform` -> deep-path Transform, non-default tolerances/speeds; #2 minimal (nulls). Collider variety across the fixture: Box + Sphere + Capsule |
| EventStep | many (incl. group children) | the listener-pattern carrier - see T4; one instance with non-default `waitSeconds` |
| ConditionsStep | 3 | one per `valueSource`: **Stat** (`statKey`), **Component** (`source` + `memberName`), **ListByLabel** (`source` + all four list-field strings); the 3 steps' `outcomes` together cover **all 8 CompareOp values** with routes mixing forward / back / one empty |
| GroupStep | 7 | one per CompleteWhen mode (6) + one **nested group-in-group** - see T3 |
| QuizStep | 2 | #1: `quiz` -> **the package-internal QuizAsset** (§4.5 - produces the `asset:` identity form), `questionIndex` set, `completion=BranchOnCorrectness`, `correctNextGuid`+`wrongNextGuid` set, `submitMode=OnSubmitButton`, `feedback=ForSeconds` + non-default `feedbackSeconds`; #2: `quiz` null, `questionId` set (the alternative addressing path), **`completion=AnyAnswer`** + `nextGuid` set, **`submitMode=ImmediateSelection`**, **`feedback=None`** |
| QuizResultsStep | 2 | #1: `quiz` null, `completion=BranchOnPassed`, **`passedNextGuid` + `failedNextGuid` set** (one a back-edge) - closes the #1 corpus hole; **`whenComplete=AfterSeconds` + non-default `completeAfterSeconds`**; #2: **`completion=OnContinue`** + `nextGuid` set, **`whenComplete=AfterContinueButtonPressed`** (v1 omitted `whenComplete` entirely - skeptic-confirmed) |

> **Enum-coverage rule:** across instances, EVERY value of every step-level serialized enum is
> committed at least once (AdvanceMode 2/2, MiniQuiz CompleteMode 2/2, Selection CompleteMode 2/2,
> Quiz CompleteMode+AnswerSubmitMode+FeedbackMode 2/2+2/2+3/3, QuizResults WhenComplete+CompleteMode
> 2/2+2/2, ConditionValueSource 3/3, CompareOp 8/8, CompleteWhen 6/6).
> **StatEffect rule:** effects entries across their four sites (Question choice, MiniQuiz choice,
> Selection onCorrectEffects/onWrongEffects) collectively cover **all 5 StatOp values** with
> non-default key/value (real labs ship only Add+Subtract; the op enum is snapshot-invisible, so
> Proof C bytes are its only net).

### T2 - Routing families (each non-vacuous AND each with an empty-string case)

| Family | Non-empty + resolving | Empty ("fall-through") |
|---|---|---|
| `nextGuid` | many, incl. back-edges | dead-end final step; island steps (BOTH - §1.5); all group children (the Loimokseis norm) |
| `choices[].nextGuid` | Question #1 c0/c1/c3 | Question #1 c2 (mixed into a routed list) |
| `outcomes[].nextGuid` (MiniQuiz + Conditions) | both | one Conditions outcome `""` |
| `defaultNextGuid` | MiniQuiz #1 | MiniQuiz #2 (`""` - the Delirium shape, now inside the mega) |
| `correctNextGuid`/`wrongNextGuid` | Selection #1 both; Quiz #1 both | Selection #2 `wrongNextGuid=""` with `correctNextGuid` set (no real lab serializes this family at all - the old synthetic was its only carrier) |
| `passedNextGuid`/`failedNextGuid` | QuizResults #1 both (one back-edge) | - (single carrier; family never shipped anywhere) |
| `specificStepGuid` | G-Specific (group level) + one G-Multi branch | present-but-empty at G-Multi group level (the Loimokseis shape) |
| `childRequirements[].guid` | EVERY group (rule-row above); `required=false` on G-Required + G-Multi branches | - |
| `multiConditionBranches[].nextGuid` | G-Multi: both branches -> the SAME target (the Brisi two-branches-one-exit shape) | - |
| `multiConditionBranches[].specificStepGuid` | one branch set | other branches `""` |
| `multiConditionBranches[].childRequirements[].guid` | same guid order as group list, **complementary `required` flags** (§1.6) | - |

### T3 - GroupStep modes (6/6 + nesting + the real shipped oddities)

| Group | Mode | Contents / oddity encoded |
|---|---|---|
| G-All | AllChildrenComplete | 2 leaf children + **one nested GroupStep child** (depth 3 - legal + runtime-supported via `RunGroupInternal` recursion; only the graph-window drag path blocks authoring it). **Fallback (D2-style):** if depth-3 churns idempotency or leaks instance mods, the nested group demotes to a dedicated `Tests/Fixtures/Mechanics/` fixture and G-All keeps two leaf children - the mega ships without blocking on it |
| G-Any | AnyChildCompletes | 2 children - first-ever serialized coverage |
| G-Specific | SpecificChildCompletes | `specificStepGuid` -> child 2 |
| G-Required | RequiredChildrenComplete | `childRequirements` with mixed `required=true/false` - first-ever |
| G-NofM | NOfMChildrenComplete | `requiredCount=2` of 3 children - first-ever |
| G-Multi | MultiCondition | the REAL Loimokseis shape (§1.6): 2 branches, same childRequirement guid order as the group list with **complementary required flags**, **distinct labels**, branch modes **`RequiredChildrenComplete` + `NOfMChildrenComplete`** with `requiredCount` set, **both branches -> the same `nextGuid`**; group-level `specificStepGuid` present-but-empty; group `nextGuid=""` |
| G-Multi-2 | MultiCondition | reachable ONLY via G-Multi's branch (the Loimokseis reachability shape) |

All group children: `nextGuid=""`; `stopOthersOnComplete` varied across groups.

### T4 - Listener shapes (superset of the 242-row reality + the never-seen modes)

Targets are plain-Unity components only; the one asset-typed value is the package-internal asset
(§4.5). Custom-script targets are a real-scene-tier concern (§5).

| Shape | Where | Covers |
|---|---|---|
| `SetActive(bool)` Bool mode | several onEnter/onSelected | the dominant real method (176/242) |
| `CanvasGroup.set_alpha(0.7f)` Float mode | EventStep onEnter | property-setter names + **float round-trip noise** (`0.699999988079071` - matches `doubleValue.ToString("R")` exactly) |
| `AudioSource.Play()` / `PlayableDirector.Play()/Stop()` Void mode | EventStep onEnter | Void mode, audio/director targets |
| `Animator.SetTrigger("Hide")` String mode | Selection #1 onWrong | String args (DIPAE `SetTrigger('Exit')` shape) |
| `Behaviour.set_enabled(bool)` Bool | MiniQuiz #2 choice onSelected | **MiniQuizChoice.onSelected rows** (Delirium ships 12; v1 had zero) |
| `Transform.SetParent(Transform)` Object mode, **scene-object arg** | EventStep onEnter | Object mode with an intra-fixture arg |
| **Object mode, `m_ObjectArgument` -> the package-internal asset** | EventStep onEnter | the **`asset:` StableId identity form** in tier 1 (Loimokseis ships 6 such rows; v1 produced zero - skeptic-confirmed) |
| `Transform.SetSiblingIndex(int)` **Int mode** | EventStep onEnter | a mode no baseline exercises |
| **dynamic bind (`EventDefined` mode 0) pinned to `AudioSource.Play`** | EventStep onEnter | the remaining mode; void()-match guaranteed; build-time verify stays |
| one row `m_CallState=Off`, one `EditorAndRuntime` | EventStep onEnter | call-state monoculture broken |
| **clean-null-target + method** (`SetActive(false)`, `m_TargetAssemblyTypeName` set) | Selection #1 onWrong | THE shipped detritus shape (Delirium/Ekpa) - benign under the dangling-only rule |
| **fully-empty row** | Question #1 c2 onSelected | second benign shape (ships nowhere - superset insurance) |
| **target-set + EMPTY method** | EventStep onEnter | third benign shape (invariants don't check method emptiness) |
| **byte-identical duplicate row pair** | Selection #1 onCorrect | the Delirium duplicate-listener shape |

### T5 - Identity & naming weirdness

| Pattern | Mega encoding |
|---|---|
| Greek names | `Σωστό image`, `Panel ΜΑΠ` under `--- UI ---`; a Greek `displayName` (T7) |
| Trailing-space names | root `---ENVIRONMENT--- ` + child `Item ` (InsertStep ref) |
| Same-named siblings, both referenced | two `Panel epomeno domatio` siblings; the MiniQuiz `Buttons Parent` twin shape |
| Same-type components on one GameObject, both referenced | 2 AudioSources on one GO, both listener targets (the `#<index>` StableId suffix) |
| Decorative separator roots | `--- SCENE MANAGERS ---` (holds the Scenario host), `--- UI ---`, `-----CANVASES-----`, `--- Timelines ---`, `---ENVIRONMENT--- ` |
| Scenario host name | fixed constant, **NOT `Scenario`** (OnValidate renames that to the title - §1.13); e.g. `MegaScenarioHost` |
| Deep hierarchy | one referenced object ≥5 levels deep |
| Inactive objects | one referenced GameObject inactive; one referenced component disabled |
| Cleanly-null refs | ~30% of step-field refs null + several null listener targets (real ratios: 30% / 49% - §1.4) |
| `asset:` identity | the package-internal asset (§4.5) via QuizStep.quiz + one Object-mode arg |

**StableId forms ledger (v2):** hierarchy-path (everywhere), `#<index>` (the dual-AudioSource GO),
`null` (everywhere), **`asset:`** (§4.5), `MISSING` (**KnownBad-only** - a violation in a green
fixture), `obj:` and bare `<root>` (**dead-by-construction** through the real export path - no
mega element; documented so their absence from the baseline is understood, not discovered).

### T6 - Topology weirdness

| Pattern | Mega encoding |
|---|---|
| Back-edges | ≥5, incl. a Pharmacy-style wrong-answer loop, a MiniQuiz outcome back-edge, a QuizResults `failedNextGuid` back-edge |
| Unreachable island | 2 EventSteps, **BOTH `nextGuid=""`** - head reaches tail only by list-order fall-through, nothing routes in (the TRUE DIPAE shape; v1's explicit internal edge was a different family). As superset insurance, ALSO one unreachable step with a SET internal edge - labelled as beyond-DIPAE insurance |
| Fan-in hub | one EventStep with fan-in ≥4 |
| Duplicate-target choices | Question #1 c0 + c3 -> same guid |
| Mixed empty/set in one routed list | Question #1 choices; one Conditions outcome |
| Dead ends | final step + island steps `nextGuid=""` |

### T7 - Editor-only serialized surface (Proof C riders; snapshot-invisible by design)

- 2 `GraphNote`s - one free, one tethered (`attachedStepGuid` -> a real step).
- 1 `GraphGroup` cosmetic box.
- `stepGraphDisplays`: Greek `displayName` (`Διαδρομή Α`) on one step + custom `size` on another.
- No orphan side-table entry (stability under OnValidate unverified; buys little - the walk never
  visits the side-table).

---

## §3 What the mega-fixture proves (and how it locks)

1. **Proof A invariants stay green** over the densest legal graph the model permits.
   **Verified against the rule set as coded (2026-06-11):** every deliberate weirdness in §2 -
   island, back-edges, duplicate-target choices, empty routes, verbatim-order branch lists, the
   three benign listener shapes, Off/EditorAndRuntime states, Int/EventDefined/Object modes - is
   invariant-invisible, so the faithful-capture export runs with zero dialogs and a future
   invariant tightening (e.g. "fixing" the benign listener shapes) turns the mega red immediately:
   the over-tightening regression becomes visible, which is the point.
2. **Proof A snapshot baseline** records every routing family, every listener fingerprint leaf
   (**all 7 m_Modes**, all 3 call-states), every reachable identity form per the T5 ledger - ANY
   DevKit change that drops/rewires/renames any of them diffs against the committed baseline.
3. **Proof C round-trip** now exercises: nested-group `[SerializeReference]` depth 3 (or its
   Mechanics fallback), all 11 managed types, every enum value, AnimationCurves, editor-only
   side-lists, Greek/trailing-space strings, a prefab variant, and the OnValidate-normalized
   `childRequirements` on every group.
4. **The export pipeline itself** - the mega is born through the real multi-root gather +
   faithful-capture path, so an export regression hard-refuses during regeneration.

---

## §4 Artifacts

### 4.1 `mega_fixture.prefab` + `mega_fixture.graph.json` (the green-gate superset)
In `Tests/Fixtures/Scenarios/` + `Tests/Baseline/GraphSnapshots/`. Auto-discovered; zero test-code
changes needed for it to enforce.

### 4.2 `mega_fixture_variant.prefab` + baseline (prefab-variant mechanics twin) - per D2
A prefab Variant of `mega_fixture.prefab` (base is package-internal -> resolves anywhere) with
conservative overrides only: `title`, one child GameObject renamed, one transform moved. **Never
anything under `steps`.** Feasibility gate per D2 (all four checks; demote on ANY misbehaviour).
**Regeneration coupling (v2):** the generator rebuilds the variant from the just-saved base **in
the same run** (instantiate fresh base, re-apply the three overrides in code, SaveAsPrefabAsset)
and recaptures both baselines atomically - the variant's `m_Modifications` target the base's
fileIDs, so a base-only regen would orphan them. Hand-maintained variants are forbidden.

### 4.3 `Tests/Fixtures/LegacyForms/` + `LegacySerializedFormTests` (old serialized states) - per D4
Sibling folder - invisible to the green gate. Contents: **TWO prefabs** - `legacy_form.prefab`
(the old-generation YAML) and `legacy_form_current.prefab` (the same 3-step slice + a SceneManager
referencing the package QuizAsset + Quiz UI controller components, in current form).
**Authoring rule (v3 - generator-derived, no hand YAML):** the mega build run ALSO generates the
current twin (PrefabUtility-saved, so the `managedReferences` rid table is Unity's own - never
hand-invented), then derives the legacy twin **textually in the same run**: read the saved current
twin, rename exactly the three SceneManager fields to their pre-FSA names (`defaultQuiz:` ->
`quiz:`, `quizPanel:` -> `quizUI:`, `quizResultsPanel:` -> `quizResultsUI:`), strip the three
editor-only list lines (`graphNotes`/`graphGroups`/`stepGraphDisplays` - serialized empty on the
current twin by construction, so each is a single strippable line; pre-448301b form), write +
import. Deterministic, regenerated only with the mega, deliberately. The dedicated test loads BOTH
by explicit path and asserts: (a) FSA lands the old names' values in the new members; (b) absent
editor-only lists default cleanly; (c) **`BuildSnapshotJson(legacy) == BuildSnapshotJson(current)`
compared IN MEMORY** - no committed `.graph.json` anywhere (a snapshot under
`Baseline/GraphSnapshots/` would trip the orphaned-baseline hard-fail). Never re-saved by any
tool. **CI scope rule:** the future CI `ForceReserializeAssets` byte-backstop is scoped to
`Tests/Fixtures/Scenarios/` ONLY - a wider sweep would rewrite the legacy names, silently
evaporating the coverage.

### 4.4 `InvariantDetectionTests` (the detector detects) - per D4 (v3: IN-MEMORY, no committed artifacts)
**Refined at build approval:** the five poisons are constructed **in memory in a preview scene**
inside the test itself - no committed prefabs, no `KnownBad/` folder, therefore no hand-tuned rid
tables, no CI-sweep exposure, and no import-layer uncertainty (v2's "hand-tuned fileID imports as
clean null" problem disappears: the test creates the dangling state directly). The detector
operates on `SerializedObject` state, so in-memory construction exercises exactly the code under
test. Each case asserts `CheckInvariants` reports **that specific violation** (step locator +
violation kind, loosely matched - WS A9's future message work must not break it):

| Poison case | Violation asserted | In-memory mechanism |
|---|---|---|
| dangling listener target | the ONE genuine listener violation | wire `SetActive` on a scene GameObject, then `DestroyImmediate` the target; assert `objectReferenceInstanceIDValue != 0` on the row's `m_Target` (non-vacuity, inline) before running the detector |
| broken route | non-empty `nextGuid` -> no such step | set a guid that matches no step |
| blank step slot | null `[SerializeReference]` entry | `steps.Add(null)` - stable: OnValidate's no-null-strip guard never removes it |
| duplicate step guid | two steps share a guid | assign the same constant to two steps - `EnsureGuidsRecursive` backfills empty only, never dedupes |
| dangling step ref (non-listener) | `InsertStep.item` dangling | assign a Transform, then `DestroyImmediate` its GameObject; same inline non-vacuity assert |

Plus a **negative control**: a clean scenario yields zero violations (guards against a detector
that fires on everything).

### 4.5 The package-internal test asset (v2 - new)
One tiny asset (recommendation: a minimal `QuizAsset`, doubling as QuizStep #1's `quiz` value)
committed under `Tests/Fixtures/Assets/` (sibling - invisible to fixture discovery, which filters
`t:GameObject` anyway). Referenced by QuizStep #1 and one Object-mode listener arg, it makes the
**`asset:` StableId branch non-vacuous in tier 1** (it ships in Loimokseis via SDK/project
Materials; without this the asset-path branch has zero bare-project coverage). Package-internal ->
resolves identically in every consumer; the asset-free constraint becomes "no PROJECT or SDK
assets" - external-asset args remain a real-scene-tier concern (§5).

### 4.6 Test-class home (v2 - new)
`InvariantDetectionTests` + `LegacySerializedFormTests` live in `Tests/Editor/Scenario/` inside
**`Pitech.XR.Scenario.Editor.Tests`** (beside `ConditionsEvaluatorTests` et al.) - so
`DevKitChecks.CoveredAssemblies` is untouched and they run in every gate invocation by
construction (a new asmdef would either silently never run in the gate, or force a DevKitChecks
edit).

---

## §5 Explicit non-goals (covered elsewhere, excluded BY DESIGN)

| Excluded | Why | Where it IS covered |
|---|---|---|
| Third-party-SDK component refs (OVR/Meta/Fusion/MAGES) + project/SDK asset args (Meta `.mat`, project materials) | don't resolve in the bare project | the 6 real-scene fixtures, tier 2 (§7) |
| Custom Assembly-CSharp listener targets (`UIStateTrigger.SetStateTrue`, `SceneHandler.OpenScene`, …) | project scripts don't exist in the package | real-scene tier |
| TMP component refs / project fonts / sprites | project-local GUIDs differ per project | real-scene tier |
| Orphaned `ChildRequirement`; branch `mode=MultiCondition` | OnValidate normalizes both away -> not a stable committed state | `GroupStepRequirementTests` |
| Dangling refs in the GREEN fixture | dangling = violation | `KnownBad/` (§4.4) |
| Runtime behaviour | Phase A is EditMode-static | Phase D golden-trace corpus (note: `GoldenTraceTests` self-ignores while no `*.trace.json` exists - the mega is never EXECUTED, so its island/MultiCondition topology can't hang a runtime driver) |
| Self-loop routes | legal per invariants, zero evidence in any baseline, implies runtime infinite loop | not covered; revisit with evidence |
| Default-only scalars not listed in T1's non-default assignments | pure Proof-C byte surface, each pinned once via T1's instance rules; exhaustive per-field default permutations add bytes, not protection | T1 instances + the enum-coverage rule |

**Dependency honesty (v2):** "runs anywhere" = any Unity 6 project with `com.unity.ugui` 2.x
(TMP), `com.unity.timeline`, `com.unity.inputsystem` present - the package's existing implicit
asmdef deps (`SelectionLists` imports TMPro; `package.json` declares no deps until the Phase D
cutover). Both the bare gate project and HealthOn VR carry all three (manifests verified).

---

## §6 Generation mechanics (the generator IS the lab)

1. **Builder**: the body of the RETAINED public `GenerateSyntheticFixture()` (D1 - symbol + menu
   path unchanged; Proof B green by construction). New helpers are `internal`.
2. **Build in a real temp SCENE through the real export core**: construct the multi-root hierarchy
   with all cross-root references, save as a temp scene asset, then run **the same export core as
   real labs** (single-open-with-restore, unpack(no-op), multi-root gather, pre/post
   faithful-capture diff, baseline write). Requires a small additive refactor: extract an
   **`internal` `ExportSceneCore(scenePath, fixtureName)`** from `ExportOpenScene` - the public
   `ExportOpenScene()` signature is unchanged (it is Proof-B-baselined), and the mid-flow
   carried-violations "Export anyway?" decision is **hoisted out of the core** via a result/
   callback so the menu flow's behaviour is preserved verbatim. ExportSceneCore also **pins the
   fixture root GameObject's name to the fixtureName constant** (today it takes the temp scene's
   name - §1.14).
3. **Determinism**: every step guid a fixed readable constant (D3 - resolved); names/hierarchy/
   wiring/order fixed in code; **`childRequirements` pre-authored for ALL groups and all branch
   lists explicitly, in steps-list order** (never relying on the normalizer's HashSet order); the
   generator asserts the saved prefab is **OnValidate-idempotent** (load -> validate -> reserialize
   -> bytes unchanged) before capturing the baseline. Regenerating on unchanged code reproduces
   `mega_fixture.graph.json` byte-identically.
4. **Regen discipline**: mega + variant regenerate ONLY deliberately and **always together in one
   run** (§4.2). Not part of the export-before-every-test loop; the regen confirm dialog applies.
5. **Superset maintenance rule**: any future Step type / field / enum value / mode added to the
   model MUST add a §2 matrix row + a mega element in the same change.

## Pre-build verification checklist (v2 - updated)

- [x] ~~`EnsureGuidsRecursive` backfill-only~~ **RESOLVED by code review** (Scenario.cs:695-696;
      editor paths identical; no format enforcement anywhere) - readable constants safe; duplicate-
      guid poison won't self-heal; blank-slot poison protected by the no-null-strip guard.
- [ ] `UnityEventTools` authoring: Int mode, Object mode w/ scene arg + w/ package asset arg,
      EventDefined dynamic bind **pinned to `AudioSource.Play`**, per-row `m_CallState`
      (`SetPersistentListenerState`), clean-null-target-with-method, fully-empty row,
      target-with-empty-method. (SerializedProperty editing is the fallback for any shape the
      typed API can't express.)
- [ ] Variant twin: ALL FOUR green-gate checks pass (D2) - incl. override-no-churn + the
      nearest-instance-root path assert.
- [ ] Nested group-in-group: reserialize-idempotent + OnValidate-untouched; on failure use the T3
      Mechanics fallback.
- [x] ~~Dangling poisons non-vacuity~~ **absorbed into the tests themselves** (§4.4 v3: in-memory
      construction with the `instanceID != 0` assert inline - no committed poison artifacts exist).
- [ ] Trailing-space GameObject names survive YAML round-trip byte-stably (Unity quotes them;
      expected stable).
- [ ] Mega prefab OnValidate-idempotence (§6.3) - the committed form must already contain every
      normalizer-added `childRequirements` row.

---

## §7 The two-tier run process (the user's pre-ship rule, encoded - v2 honest version)

| Tier | When | Where | What runs | Cost |
|---|---|---|---|---|
| **1 - every change** | after any DevKit edit, before any push | HealthOn VR today; **the bare gate project once Step 12 lands (same work item)** | `Evaluate Changes` = the FULL covered-assembly suite - there is no per-fixture selection; in the bare project the 6 real-scene fixtures' cases **Inconclusive-skip via Step 12** (their SDK refs read as missing there), enforce in HealthOn | seconds-to-minutes; no third-party SDKs (§5 dependency note) |
| **2 - pre-ship** | **before updating the git-tracked DevKit reference HealthOn VR ships with** | **inside HealthOn VR** (DevKit on `file:`) | full gate incl. the 6 real-scene fixtures enforced; re-export per the re-export rule; `git status Tests/` clean | minutes; needs the lab project |

Green tier 1 is necessary for every push; green tier 2 is the gate for **shipping** (moving
consumers' pinned git reference). The mega makes tier 1 more-than-complete on serialization/graph
surface; tier 2 remains authoritative for SDK/asset/project reality (§5). Tier-1 PlayMode is a
no-op until Phase D (the golden-trace seed self-ignores) - nobody should expect the mega to
execute.

### §7.1 The Step 12 skip predicate (EXACT rules - Stergios' hardening, 2026-06-11)

A vague "missing refs -> skip" would hide real serialization failures behind the skip. The rule:
**the skip is keyed to a committed, export-time DECLARATION - never inferred from the observed
failure.**

1. **The declaration.** `Tests/Baseline/FixtureDeps/<fixtureName>.deps.json` - written ONLY by the
   export tool, at export time, in the project where the lab lives (HealthOn - everything
   resolves there). Content: the fixture's external dependencies per
   `AssetDatabase.GetDependencies(fixturePath, recursive)` minus the fixture itself, minus
   everything under this package, minus Unity built-ins - as `{guid, pathAtExport}` entries,
   sorted, LF, `schemaVersion: 1`. **A fixture with zero external deps gets NO file** (a stale one
   is deleted by the export). Committed and reviewed like the baseline. Hand-authoring one to
   silence a red is the same laundering the re-export rule forbids.
2. **The predicate.** A fixture's gate cases skip (Inconclusive) **iff**: a declaration exists
   **AND** it has >= 1 entry **AND** >= 1 entry's GUID does not resolve in the current project
   (`AssetDatabase.GUIDToAssetPath(guid)` empty). If ALL entries resolve (HealthOn), or the
   declaration is absent/empty: the fixture is **enforced in full** - any dangling ref is RED.
   Consequence: a DevKit change that introduces a dangling ref can never hide behind the skip
   (the mega/variant/synthetic have no declaration and cannot acquire one through the tool).
3. **The skip is LOUD.** `Assert.Inconclusive` naming each skipped fixture + exactly which
   declared deps are unmet (`pathAtExport (guid)`) + "enforce in HealthOn VR (tier 2)". A skipped
   fixture is never counted green; an evaluated-fixture failure is RED regardless of how many
   siblings skipped; ALL fixtures skipped reads Inconclusive, never green.
4. **Never-skip set.** Mega, variant, synthetic: no declaration by construction (the builder
   asserts none was written). `InvariantDetectionTests` + `LegacySerializedFormTests` **never
   consult the predicate** - hard rule.
5. **Hygiene checks** (same pattern as the orphaned-baseline rule): a deps file whose stem matches
   no discovered fixture = FAIL (orphaned declaration); a declaration all of whose entries resolve
   is inert (enforced path) - no skip, no warning.
6. **Where it applies:** every fixture-driven test - `ScenarioGraphIntegrityTests` (invariants +
   snapshot) and `SerializedFixtureRoundTripTests` (both tests; `ForceReserializeAssets` over a
   missing-script prefab copy is itself unsafe, so the round-trip MUST honor the same predicate).
7. **One-time migration (DONE 2026-06-12):** the 6 committed real-scene fixtures had no declarations
   yet. They were re-exported in HealthOn (deliberate, rule-3a compatible) and 6 deps files appeared.
   The original "bytes must come back UNCHANGED" acceptance was WRONG - export output is not byte-stable
   across sessions (prefab fileIDs churn on every export); the corrected §9.5 acceptance is GRAPH-CONTENT
   (snapshot) equivalence + attribution of any content drift (the 2026-06-12 drift was the user's own
   authored lab edits). Until a lab's re-export is committed, the bare project reads it RED - the
   predicate has nothing to key on.

## §8 Doc/plan/code hooks on landing (v2 - complete touch-list)

- **Plan WS A3 Step 11**: tick + REWRITE its text to enumerate what actually ships (mega + variant
  twin + LegacyForms/`LegacySerializedFormTests` + KnownBad/`InvariantDetectionTests` + the
  package-internal test asset + the `ExportSceneCore` extraction + the D1 fixture replacement) -
  the current one-liner describes a quarter of the scope.
- **Plan WS A3 Step 12**: tick WITH Step 11 (same work item - §7 depends on it).
- **Plan Appendix I.3**: dated annotation - the mega supersedes the mandatory
  `synthetic_routing_families` (its four families carried forward as a strict superset; T1
  second-instances carry its three unique states).
- **`testing-and-fixtures.md`**: §0 diagram + §2 corpus list (stale - still names the census 5) ->
  the 6 shipped scenes; add the §7 two-tier table; §4 "synthetic fixture" paragraph -> the mega;
  §5 tool-inventory row description updated (path unchanged); CI scope rule (§4.3) recorded in §7.
- **`MaintainPage.cs`**: the Generate card's title/description (lines ~64-65) still describe the
  4-family synthetic - update text; menu const UNCHANGED (D1).
- **`README.md`** (~line 59): update the synthetic-fixture description.
- **`ScenarioGraphIntegrityTests` header comment**: still says "HALF-WIRED... method named at a
  missing target" - stale vs the dangling-only rule (and vs the mega's committed benign shapes);
  reword to dangling-only in the same change (a future maintainer "fixing the code to match the
  comment" is exactly the over-tightening §3.1 guards against).
- **`CHANGELOG.md`**: one entry (test-only, additive).

## §9 Acceptance (v2)

1. Mega + variant green in **both** projects on unmodified code - bare-project green **requires
   Step 12** (same work item, §7).
2. Committed baseline byte-reproducible on deliberate regeneration (same code -> same JSON),
   including the OnValidate-normalized `childRequirements` rows on every group.
3. Coverage matrix §2 fully realized - audited by mapping each matrix row (incl. the rule-rows) to
   a property path in the committed baseline.
4. `InvariantDetectionTests`: all 5 in-memory poisons assert their violation + the negative
   control passes; `LegacySerializedFormTests`: the 3 FSA mappings + in-memory snapshot
   equivalence green.
5. **Export-output neutrality (graph-content level)**: after the `ExportSceneCore` extraction, one
   deliberate re-export of a real lab in HealthOn on unchanged code shows the graph **snapshot**
   (`.graph.json`) content-unchanged - NOT a clean raw `git status Tests/` (CORRECTED 2026-06-12: export
   output is not byte-stable; prefab fileIDs churn on every export, so the prefab bytes always diff). The
   extraction rewires the path that produces every committed fixture - a SNAPSHOT-content change on
   unchanged code is the regression class the re-export rule exists to catch.
6. The 3-tool inventory unchanged; menu paths unchanged; `GenerateSyntheticFixture()` +
   `ExportOpenScene()` public signatures unchanged (Proof B additions-only by construction).
7. All Phase A negative gates hold (test/editor-only; no runtime code touched; no serialized field
   changes; no version bump; no emission). **One sanctioned, declared exception (build review
   2026-06-11):** `Pitech.XR.Scenario.Editor.asmdef` gained an ADDITIVE reference to
   `Pitech.XR.Stats` (the builder constructs `StatEffect`/`StatOp` per the T1 StatEffect rule;
   asmdef refs are non-transitive). Editor-only, intra-package, no consumer dependency-resolution
   change, invisible to Proof B - recorded here so the "no asmdef changes" reading is never
   litigated against this line.
8. **Skip-predicate acceptance (§7.1):** in the bare project, the mega/variant are ENFORCED and
   each declared real-scene fixture reads a loud Inconclusive naming its unmet deps; in HealthOn,
   zero skips - every fixture enforced; the one-time re-export of the 6 labs returns graph-snapshot
   content UNCHANGED (= §9.5, corrected 2026-06-12 from "bytes UNCHANGED") + 6 new committed deps declarations.

## §10 Build + review record (2026-06-11)

Implemented same-day by a 5-agent file-disjoint build, then a 3-reviewer adversarial pass
(compile-correctness ground-truthed against the consumer project's actual compiler `.rsp`;
spec-fidelity audited the §2 matrix row-by-row and verified all 56 step guids fixed/unique;
neutrality verified the menu export byte-identical guard-by-guard and root-name sanitize-identity
for all 6 committed labs). **2 blockers found + fixed:** the Stats asmdef reference (above) and
`ComputeExternals` not treating `Packages/com.unity.*` as built-ins (ugui Button MonoScripts would
have given the self-contained mega a deps declaration, firing its own §7.1.4 assert). Also fixed:
K1 double regen dialog (menu keeps the only confirm - it carries the laundering warning; the
builder is dialog-free), the builder's §7.1.4 asserts now ABORT + delete the bad declaration
(previously log-only with a success log still printing), the D2 four-check variant feasibility
gate now runs IN-BUILD (was deferred to the test suite), `WriteDeclaration` is `internal` (export
tool only, per §7.1.1), the duplicated Tests/-locator helpers collapsed onto the export tool's
internals, and `MegaFixtureBuilder.cs.meta` added.

**Field fix (first in-Unity run, 2026-06-11): NORMALIZE-THEN-CAPTURE.** The first generation run
surfaced a same-session capture bug: a freshly authored prefab is not yet in Unity's canonical
serialized form (the next import rewrites e.g. UnityEvent `m_TargetAssemblyTypeName` from the
in-memory AssemblyQualifiedName to the short two-part form), so baselines captured in the creating
session drift against every later post-import re-extraction ([mega_fixture] snapshot drift), and
the committed bytes fail the normalized-form check ([mega_fixture_variant] Inconclusive). Real-lab
exports never hit this - their scenes are already canonical - which is why the 6 real fixtures were
green in the same run. Fix: the builder now runs `ForceReserializeAssets` + `ImportAsset(ForceUpdate)`
and RE-captures the baseline after saving the mega, the variant, and the legacy current twin
(the textual derivation now reads canonical bytes). `ExportSceneCore` itself stays untouched
(real-lab path unchanged; NOTE 2026-06-12: a re-export is graph-content/snapshot-neutral, NOT
byte-neutral - prefab fileIDs churn on every export).

**Per-lab reporting + batch-export follow-on (2026-06-11, post-1st-green).** After the mega/variant drift
cleared, two usability gaps were closed (Stergios' ask). (1) The orphan `synthetic_routing_families` baseline
was deleted - its prefab was already retired (D1), and the §7.1.5-style orphan check correctly red'd on the
dangling baseline. (2) The four fixture checks were PARAMETRIZED per lab via a shared `FixtureCorpus` source,
so the gate's §7.1 skip is now applied **per lab** (a skipped lab is its own loud Inconclusive) and the
`Evaluate Changes` window reports each lab individually with its full message - the old monolithic checks
collapsed every red to its first line, hiding sibling labs. In-test baseline auto-capture was retired (a
missing baseline is now a per-lab Inconclusive pointing at the export tool) to remove a test-order hazard the
parametrization would introduce; capture stays exclusively in the export tool (§6.x / this spec's generator +
`ExportSceneCore`). A batch `Export All Test Scenes` + `Manage Test Scenes List` re-export a curated,
auto-seeded scene list through the same `ExportSceneCore`. All editor/test-only, Proof-B/C-neutral
(`FixtureDependencies` was also moved off the public surface via `InternalsVisibleTo`). A 3-skeptic review
found 1 BLOCKER (the public `FixtureDependencies` - fixed) + 1 MINOR (progress-bar math - fixed).

**Accepted readings (record for the §9.3 matrix audit):** QuizStep ships 3 instances (the
enum-coverage rule's FeedbackMode 3/3 outranks T1's "2"); T4's "PlayableDirector.Play()/Stop()"
realized as Stop() (Play() covered on AudioSource; the row's mode/target coverage is complete);
T5's "several null listener targets" realized as exactly 2 rows (the two benign shapes - step-field
null coverage separately exceeds ~30%); T6's insurance unreachable step routes into the reachable
graph (a set, resolving edge out of an unreachable node - the BOTH-empty island is the DIPAE
shape, this one is labelled beyond-DIPAE insurance).
