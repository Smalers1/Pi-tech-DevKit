#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;
using Pitech.XR.Interactables;
using Pitech.XR.Quiz;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// WS A3 Step 11 - builds the MEGA-FIXTURE: the census-superset synthetic corpus
    /// (Documentation~/specs/2026-06-11-mega-fixture-spec.md, matrix §2, mechanics §6).
    ///
    /// One deterministic, hand-designed scenario (~50 steps, all 11 step types, every routing
    /// family, every GroupStep mode, every listener shape, the shipped identity/topology
    /// weirdness) is constructed in a REAL temp scene with the real multi-root lab hierarchy and
    /// exported through the SAME pipeline as real labs (<see cref="ExportLabAsTestFixture.ExportSceneCore"/>:
    /// single-open-with-restore, unpack, multi-root gather, faithful-capture diff, baseline write).
    /// ZERO carried violations are expected - any finding means a builder bug, never "export anyway".
    ///
    /// The same run also produces (always together - §6.4 regen coupling):
    ///   - the prefab-Variant twin (§4.2): conservative overrides only (title / one child rename /
    ///     one transform move), never anything under steps;
    ///   - the LegacyForms twins (§4.3 v3): a current-form slice saved by PrefabUtility, then the
    ///     legacy twin derived TEXTUALLY (pre-FormerlySerializedAs field names, editor-only list
    ///     lines stripped) - never hand-authored YAML;
    ///   - the package-internal QuizAsset (§4.5) that makes the "asset:" StableId branch
    ///     non-vacuous in tier 1.
    ///
    /// Determinism (§6.3): every step guid is a fixed readable constant; names/order/wiring are
    /// fixed in code; childRequirements are PRE-AUTHORED for every group and every MultiCondition
    /// branch in steps-list order so OnValidate's normalizers add nothing; after export the
    /// builder asserts reserialize idempotence byte-for-byte. Step guids are wired by object
    /// reference (target.guid), so every authored route resolves by construction.
    ///
    /// Coverage note (enum-coverage rule §2 vs T1 instance counts): QuizStep FeedbackMode has 3
    /// values but T1 lists only 2 QuizStep instances (ForSeconds / None). The enum-coverage rule
    /// wins: a third QuizStep ships as a G-Any group child carrying FeedbackMode.UntilContinue.
    /// </summary>
    internal static class MegaFixtureBuilder
    {
        // ---- §4 artifact names (pinned by the cross-file contract) ---------------------------

        const string MegaFixtureName = "mega_fixture";
        const string VariantFixtureName = "mega_fixture_variant";
        const string QuizAssetLeaf = "Fixtures/Assets";
        const string QuizAssetFile = "mega_quiz.asset";
        const string LegacyFormsLeaf = "Fixtures/LegacyForms";
        const string LegacyCurrentFile = "legacy_form_current.prefab";
        const string LegacyOldFile = "legacy_form.prefab";
        const string FixturesLeaf = "Fixtures/Scenarios";
        const string TmpSceneFile = "_tmp_mega_build.unity";

        const string MegaTitle = "Mega Fixture";
        const string HostName = "MegaScenarioHost";   // never "Scenario" - OnValidate renames that (§1.13)

        // ---- entry point ----------------------------------------------------------------------

        internal static void BuildAndExport()
        {
            string fixturesDir = TestsSub(FixturesLeaf);
            if (fixturesDir == null)
            {
                Fail("could not locate the DevKit package Tests/ folder.");
                return;
            }
            EnsureFolder(fixturesDir);

            // The regen confirm lives in the MENU method (GenerateSyntheticFixture) - the builder
            // itself is dialog-free (K1, review 2026-06-11): non-dialog guards only below.
            string megaPath = fixturesDir + "/" + MegaFixtureName + ".prefab";

            // The builder opens a temp scene Single and restores the user's setup from disk at the
            // end - so, exactly like Export Lab as Test Fixture, every loaded scene must be saved.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.isDirty || string.IsNullOrEmpty(s.path))
                {
                    Fail($"scene '{(string.IsNullOrEmpty(s.name) ? "(untitled)" : s.name)}' is unsaved or "
                        + "has pending changes - save every open scene first (the builder restores your "
                        + "scene setup from disk, so unsaved scenes cannot be brought back).");
                    return;
                }
            }

            var quiz = EnsureQuizAsset();
            if (quiz == null) return;   // EnsureQuizAsset already logged

            var userSetup = EditorSceneManager.GetSceneManagerSetup();
            string tmpScenePath = fixturesDir + "/" + TmpSceneFile;
            bool keepTempForInspection = false;

            try
            {
                // Flow choice (§6.2): build directly in a fresh empty scene (the user's layout was
                // captured above and is restored in finally), save it as a temp scene asset, and
                // hand the SAVED asset to ExportSceneCore - which runs its own copy/open/restore.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var refs = BuildHierarchy(scene);
                var ctx = BuildSteps(refs, quiz);
                AddEditorOnlySurface(refs.scenario, ctx);
                ApplySurgicalListenerShapes(refs.scenario, ctx, refs, quiz);

                if (!EditorSceneManager.SaveScene(scene, tmpScenePath))
                {
                    Fail("could not save the temp build scene at " + tmpScenePath);
                    keepTempForInspection = true;
                    return;
                }

                // The real export path. Carried violations are NEVER expected from this builder -
                // the callback refuses, the core aborts, and we call it what it is: a builder bug.
                string exported = ExportLabAsTestFixture.ExportSceneCore(
                    tmpScenePath, MegaFixtureName, carried => false, out string failureReason);
                if (exported == null)
                {
                    Fail("export refused - this is a MEGA BUILDER BUG (zero carried violations are "
                        + "expected by construction). Reason: " + failureReason
                        + "\nThe temp scene is kept for inspection: " + tmpScenePath);
                    keepTempForInspection = true;
                    return;
                }
                megaPath = exported;

                // NORMALIZE-THEN-CAPTURE (field fix, 2026-06-11): a freshly authored prefab is not
                // yet in Unity's canonical serialized form - the next import rewrites details like
                // UnityEvent m_TargetAssemblyTypeName (in-memory AssemblyQualifiedName -> the short
                // two-part form). ExportSceneCore captured the baseline from the same-session state,
                // which would drift against every later (post-import) re-extraction. So: force the
                // canonical form NOW, reimport, and re-capture the baseline from the truth every
                // future load will see. Real-lab exports never need this (their scenes are already
                // canonical), which is why the core itself stays untouched.
                AssetDatabase.SaveAssets();
                AssetDatabase.ForceReserializeAssets(new[] { megaPath });
                AssetDatabase.ImportAsset(megaPath, ImportAssetOptions.ForceUpdate);
                if (!ExportLabAsTestFixture.CaptureBaselineFor(megaPath, out _, out string recapError))
                {
                    Fail("post-normalization baseline re-capture failed: " + recapError);
                    return;
                }

                // §6.3 - OnValidate idempotence: the committed bytes must already contain every
                // normalizer-added childRequirements row; a reserialize must change nothing.
                if (!AssertReserializeIdempotent(megaPath)) return;   // artifacts left for inspection

                if (!BuildVariant(megaPath, out string variantPath)) return;

                if (!BuildLegacyTwins(quiz, out string legacyCurrentPath, out string legacyOldPath)) return;

                // §7.1.4 - the never-skip set: mega + variant must have NO deps declaration.
                // A violated assert ABORTS (no success log) and removes the bad declaration so the
                // never-skip state can't be committed by accident.
                if (FixtureDependencies.HasDeclaration(megaPath))
                {
                    Fail("post-run assert violated (spec §7.1.4): '" + megaPath + "' acquired a deps "
                        + "declaration - the mega must have ZERO external dependencies. Builder bug. "
                        + "The unexpected declaration has been deleted.");
                    AssetDatabase.DeleteAsset(FixtureDependencies.DepsDirProjectPath() + "/"
                        + MegaFixtureName + ".deps.json");
                    return;
                }
                if (FixtureDependencies.HasDeclaration(variantPath))
                {
                    Fail("post-run assert violated (spec §7.1.4): '" + variantPath + "' acquired a deps "
                        + "declaration - the variant must have ZERO external dependencies. Builder bug. "
                        + "The unexpected declaration has been deleted.");
                    AssetDatabase.DeleteAsset(FixtureDependencies.DepsDirProjectPath() + "/"
                        + VariantFixtureName + ".deps.json");
                    return;
                }

                AssetDatabase.SaveAssets();
                Debug.Log("[DevKit] Mega fixture build complete:\n"
                    + "  - " + megaPath + " (+ baseline snapshot)\n"
                    + "  - " + variantPath + " (+ baseline snapshot)\n"
                    + "  - " + TestsSub(QuizAssetLeaf) + "/" + QuizAssetFile + "\n"
                    + "  - " + legacyCurrentPath + "\n"
                    + "  - " + legacyOldPath + " (textually derived pre-FSA twin)\n"
                    + "Regenerate only deliberately, always as one run (spec §6.4).");
            }
            finally
            {
                // Restore the user's exact scene layout from disk, then delete the temp scene
                // (unless an abort left it behind on purpose for inspection).
                try
                {
                    if (userSetup != null && userSetup.Length > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(userSetup);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    if (!keepTempForInspection)
                        AssetDatabase.DeleteAsset(tmpScenePath);
                }
            }
        }

        // ---- §4.5 the package-internal QuizAsset ----------------------------------------------

        static QuizAsset EnsureQuizAsset()
        {
            string dir = TestsSub(QuizAssetLeaf);
            if (dir == null) { Fail("could not locate the Tests/ folder for the quiz asset."); return null; }
            EnsureFolder(dir);

            string path = dir + "/" + QuizAssetFile;
            var existing = AssetDatabase.LoadAssetAtPath<QuizAsset>(path);
            if (existing != null) return existing;

            // Deterministic minimal content. Question ids are pinned (QuizAsset.OnValidate would
            // otherwise backfill random GUIDs); QuizStep #2 addresses "mega-q-02" by id.
            var asset = ScriptableObject.CreateInstance<QuizAsset>();
            var q1 = new QuizAsset.Question
            {
                id = "mega-q-01",
                prompt = "Ποιο είναι το σωστό;",
                type = QuizAsset.QuestionType.SingleChoice,
                points = 1f,
            };
            q1.answers.Add(new QuizAsset.Answer { text = "Σωστό", isCorrect = true, explanation = "Σωστή απάντηση." });
            q1.answers.Add(new QuizAsset.Answer { text = "Λάθος", isCorrect = false, explanation = "" });

            var q2 = new QuizAsset.Question
            {
                id = "mega-q-02",
                prompt = "Pick the two correct items.",
                type = QuizAsset.QuestionType.MultipleChoice,
                points = 2f,
                allowPartialCredit = true,
            };
            q2.answers.Add(new QuizAsset.Answer { text = "Alpha", isCorrect = true, explanation = "" });
            q2.answers.Add(new QuizAsset.Answer { text = "Beta", isCorrect = false, explanation = "" });
            q2.answers.Add(new QuizAsset.Answer { text = "Gamma", isCorrect = true, explanation = "" });

            asset.questions.Add(q1);
            asset.questions.Add(q2);
            asset.passThresholdPercent = 0.5f;

            AssetDatabase.CreateAsset(asset, path);
            if (AssetDatabase.LoadAssetAtPath<QuizAsset>(path) == null)
            {
                Fail("could not create the quiz asset at " + path);
                return null;
            }
            return asset;
        }

        // ---- T5 - scene hierarchy (identity & naming weirdness) -------------------------------

        /// <summary>Everything the step builder needs to wire cross-root references.</summary>
        sealed class SceneRefs
        {
            public Scenario scenario;

            public PlayableDirector director;
            public SelectionLists selectionLists;

            public RectTransform questionPanel;
            public Animator questionAnimator;        // referenced while DISABLED (T5)
            public Button choiceButtonA, choiceButtonB;

            public RectTransform miniQuizPanel;
            public Button btnNaiA, btnOxiA, btnNaiB, btnOxiB;

            public RectTransform selectionPanel;
            public Animator selectionAnimator;
            public Button submitButton;
            public GameObject hintPanel;

            public GameObject sostoImage;             // "Σωστό image"
            public GameObject panelMap;               // "Panel ΜΑΠ"
            public CanvasGroup mapCanvasGroup;
            public GameObject panelEpomenoA, panelEpomenoB;   // same-named siblings, both referenced

            public GameObject card1, card2, card3;    // card2 is INACTIVE and referenced (T5)
            public GameObject tapHint, extraObject;
            public Button nextButton;
            public Button deepSubmit;                 // >= 5 levels deep, referenced (T5)

            public Transform itemTrailing;            // "Item " - trailing space, referenced
            public BoxCollider insertSlot;
            public Transform attachPoint;

            public AudioSource audioA, audioB;        // TWO AudioSources on ONE GameObject, both targeted
            public Transform sphereProp, capsuleProp; // Sphere/Capsule collider variety
            public Transform parentTarget;
        }

        static SceneRefs BuildHierarchy(UnityEngine.SceneManagement.Scene scene)
        {
            var r = new SceneRefs();

            // Decorative separator roots - the real multi-root lab shape (T5), including the
            // TRAILING-SPACE environment root.
            var managers = NewRoot("--- SCENE MANAGERS ---", scene);
            var ui = NewRoot("--- UI ---", scene);
            var canvases = NewRoot("-----CANVASES-----", scene);
            var timelines = NewRoot("--- Timelines ---", scene);
            var environment = NewRoot("---ENVIRONMENT--- ", scene);

            // -- managers --
            var host = NewChild(managers, HostName);
            r.scenario = host.AddComponent<Scenario>();

            var listsGo = NewChild(managers, "Selection Lists");
            r.selectionLists = listsGo.AddComponent<SelectionLists>();

            // -- timelines --
            var directorGo = NewChild(timelines, "Mega Director");
            r.director = directorGo.AddComponent<PlayableDirector>();   // deliberately no playable asset

            // -- UI --
            var questionPanelGo = NewUIChild(ui, "Mega Question Panel");
            r.questionPanel = questionPanelGo.GetComponent<RectTransform>();
            r.questionAnimator = questionPanelGo.AddComponent<Animator>();
            r.questionAnimator.enabled = false;       // one referenced component DISABLED (T5)
            r.choiceButtonA = NewUIChild(questionPanelGo, "Apantisi A").AddComponent<Button>();
            r.choiceButtonB = NewUIChild(questionPanelGo, "Apantisi B").AddComponent<Button>();

            var miniQuizPanelGo = NewUIChild(ui, "Mini Quiz Panel");
            r.miniQuizPanel = miniQuizPanelGo.GetComponent<RectTransform>();
            // The Delirium "Buttons Parent" twin shape: same-named sibling parents, buttons on both.
            var buttonsParentA = NewUIChild(miniQuizPanelGo, "Buttons Parent");
            var buttonsParentB = NewUIChild(miniQuizPanelGo, "Buttons Parent");
            r.btnNaiA = NewUIChild(buttonsParentA, "Btn Nai").AddComponent<Button>();
            r.btnOxiA = NewUIChild(buttonsParentA, "Btn Oxi").AddComponent<Button>();
            r.btnNaiB = NewUIChild(buttonsParentB, "Btn Nai").AddComponent<Button>();
            r.btnOxiB = NewUIChild(buttonsParentB, "Btn Oxi").AddComponent<Button>();

            var selectionPanelGo = NewUIChild(ui, "Selection Panel");
            r.selectionPanel = selectionPanelGo.GetComponent<RectTransform>();
            r.selectionAnimator = selectionPanelGo.AddComponent<Animator>();
            r.submitButton = NewUIChild(selectionPanelGo, "Submit Button").AddComponent<Button>();
            r.hintPanel = NewChild(selectionPanelGo, "Hint Panel");

            r.sostoImage = NewChild(ui, "Σωστό image");                 // Greek name, referenced
            r.panelMap = NewChild(ui, "Panel ΜΑΠ");                     // Greek name, referenced
            r.mapCanvasGroup = r.panelMap.AddComponent<CanvasGroup>();
            r.panelEpomenoA = NewChild(ui, "Panel epomeno domatio");    // same-named siblings,
            r.panelEpomenoB = NewChild(ui, "Panel epomeno domatio");    // BOTH referenced (T5)

            r.card1 = NewChild(ui, "Card 1");
            r.card2 = NewChild(ui, "Card 2");
            r.card2.SetActive(false);                  // one referenced GameObject INACTIVE (T5)
            r.card3 = NewChild(ui, "Card 3");
            r.tapHint = NewChild(ui, "Tap Hint");
            r.nextButton = NewUIChild(ui, "Next Button").AddComponent<Button>();
            r.extraObject = NewChild(ui, "Extra Object");

            // -- canvases: one referenced object >= 5 levels deep (T5) --
            var canvasRoot = NewChild(canvases, "Mega Canvas Root");
            var layer2 = NewChild(canvasRoot, "Layer 2");
            var layer3 = NewChild(layer2, "Layer 3");
            var layer4 = NewChild(layer3, "Layer 4");
            var layer5 = NewChild(layer4, "Layer 5");
            r.deepSubmit = NewUIChild(layer5, "Deep Submit Button").AddComponent<Button>();

            // -- environment (trailing-space root) --
            r.itemTrailing = NewChild(environment, "Item ").transform;  // trailing-space child (T5)
            var slotGo = NewChild(environment, "Insert Slot");
            r.insertSlot = slotGo.AddComponent<BoxCollider>();
            r.insertSlot.isTrigger = true;
            var trapezi = NewChild(environment, "Megalo Trapezi");
            var rafi = NewChild(trapezi, "Rafi");
            var thiki = NewChild(rafi, "Thiki");
            r.attachPoint = NewChild(thiki, "Attach Point").transform;

            var audioRig = NewChild(environment, "Audio Rig");
            r.audioA = audioRig.AddComponent<AudioSource>();   // both targeted -> "#<index>" StableId
            r.audioB = audioRig.AddComponent<AudioSource>();

            var sphereGo = NewChild(environment, "Sphere Prop");
            sphereGo.AddComponent<SphereCollider>();
            r.sphereProp = sphereGo.transform;
            var capsuleGo = NewChild(environment, "Capsule Prop");
            capsuleGo.AddComponent<CapsuleCollider>();
            r.capsuleProp = capsuleGo.transform;

            r.parentTarget = NewChild(environment, "Parent Target").transform;
            NewChild(environment, "Prop A");           // variant rename target (unreferenced - §4.2)
            NewChild(environment, "Prop B");           // variant move target (unreferenced - §4.2)

            // SelectionLists content: collider variety (Sphere + Capsule) lives in a non-step
            // component - Proof C byte surface, snapshot-invisible by design.
            var wounds = new SelectionList { name = "Wounds" };
            wounds.correct.Add(sphereGo.GetComponent<SphereCollider>());
            wounds.correct.Add(capsuleGo.GetComponent<CapsuleCollider>());
            r.selectionLists.lists.Add(wounds);

            return r;
        }

        // ---- T1/T2/T3/T6 - steps, routing, groups, topology -----------------------------------

        /// <summary>Steps the post-construction passes (surgery, T7) need to address by index.</summary>
        sealed class StepCtx
        {
            public QuestionStep question1;
            public SelectionStep selection1;
            public EventStep carrier;
            public EventStep hub;
        }

        static StepCtx BuildSteps(SceneRefs r, QuizAsset quiz)
        {
            var ctx = new StepCtx();
            var steps = r.scenario.steps;

            // Construct every step FIRST (fixed readable guids - D3), then wire all routing by
            // object reference (target.guid) so every authored route resolves by construction.

            // -- T1 first instances + topology carriers --
            var timeline1 = MakeStep<TimelineStep>("mega-timeline-01");
            var cueCards1 = MakeStep<CueCardsStep>("mega-cuecards-01");
            var question1 = MakeStep<QuestionStep>("mega-question-01");
            var hub = MakeStep<EventStep>("mega-event-hub");                       // fan-in >= 4 (T6)
            var miniQuiz1 = MakeStep<MiniQuizStep>("mega-miniquiz-01");
            var selection1 = MakeStep<SelectionStep>("mega-selection-01");
            var insert1 = MakeStep<InsertStep>("mega-insert-01");
            var condStat = MakeStep<ConditionsStep>("mega-conditions-stat");
            var carrier = MakeStep<EventStep>("mega-event-carrier");               // T4 listener carrier
            var quiz1 = MakeStep<QuizStep>("mega-quiz-01");
            var unreachable = MakeStep<EventStep>("mega-event-unreachable");       // beyond-DIPAE insurance (T6)
            var quizResults1 = MakeStep<QuizResultsStep>("mega-quizresults-01");
            var island1 = MakeStep<EventStep>("mega-event-island-01");             // the TRUE DIPAE island:
            var island2 = MakeStep<EventStep>("mega-event-island-02");             // BOTH nextGuid="" (T6)

            // -- T3 groups (7 named; G-All additionally nests a group child = depth 3) --
            var groupAll = MakeStep<GroupStep>("mega-group-all");
            var groupAny = MakeStep<GroupStep>("mega-group-any");
            var groupSpecific = MakeStep<GroupStep>("mega-group-specific");
            var groupRequired = MakeStep<GroupStep>("mega-group-required");
            var groupNofM = MakeStep<GroupStep>("mega-group-nofm");
            var groupMulti = MakeStep<GroupStep>("mega-group-multi");
            var groupMulti2 = MakeStep<GroupStep>("mega-group-multi-2");

            // -- T1 second instances --
            var condComp = MakeStep<ConditionsStep>("mega-conditions-comp");
            var condList = MakeStep<ConditionsStep>("mega-conditions-list");
            var quiz2 = MakeStep<QuizStep>("mega-quiz-02");
            var quizResults2 = MakeStep<QuizResultsStep>("mega-quizresults-02");
            var cueCards2 = MakeStep<CueCardsStep>("mega-cuecards-02");
            var question2 = MakeStep<QuestionStep>("mega-question-02");
            var miniQuiz2 = MakeStep<MiniQuizStep>("mega-miniquiz-02");
            var selection2 = MakeStep<SelectionStep>("mega-selection-02");
            var insert2 = MakeStep<InsertStep>("mega-insert-02");
            var timeline2 = MakeStep<TimelineStep>("mega-timeline-02");
            var eventFinal = MakeStep<EventStep>("mega-event-final");              // dead end (T6)

            // -- group children (T3; all children nextGuid="" - the Loimokseis norm) --
            var allChild1 = MakeStep<EventStep>("mega-group-all-child-01");
            var allChild2 = MakeStep<EventStep>("mega-group-all-child-02");
            var nestedGroup = MakeStep<GroupStep>("mega-group-nested");            // group-in-group, depth 3
            var nestedChild1 = MakeStep<EventStep>("mega-group-nested-child-01");
            var nestedChild2 = MakeStep<EventStep>("mega-group-nested-child-02");
            var anyChild1 = MakeStep<EventStep>("mega-group-any-child-01");
            var quiz3 = MakeStep<QuizStep>("mega-quiz-03");                        // FeedbackMode.UntilContinue carrier
            var specChild1 = MakeStep<EventStep>("mega-group-specific-child-01");
            var specChild2 = MakeStep<EventStep>("mega-group-specific-child-02");
            var reqChild1 = MakeStep<EventStep>("mega-group-required-child-01");
            var reqChild2 = MakeStep<EventStep>("mega-group-required-child-02");
            var reqChild3 = MakeStep<EventStep>("mega-group-required-child-03");
            var nofmChild1 = MakeStep<EventStep>("mega-group-nofm-child-01");
            var nofmChild2 = MakeStep<EventStep>("mega-group-nofm-child-02");
            var nofmChild3 = MakeStep<EventStep>("mega-group-nofm-child-03");
            var multiChild1 = MakeStep<EventStep>("mega-group-multi-child-01");
            var multiChild2 = MakeStep<EventStep>("mega-group-multi-child-02");
            var multiChild3 = MakeStep<EventStep>("mega-group-multi-child-03");
            var multiChild4 = MakeStep<EventStep>("mega-group-multi-child-04");
            var multi2Child1 = MakeStep<EventStep>("mega-group-multi-2-child-01");
            var multi2Child2 = MakeStep<EventStep>("mega-group-multi-2-child-02");

            // ---- list order (fixed; island/unreachable sit after explicitly-routing steps so
            // nothing falls through into them - the DIPAE shape) ----
            steps.Add(timeline1);       //  0
            steps.Add(cueCards1);       //  1
            steps.Add(question1);       //  2
            steps.Add(hub);             //  3
            steps.Add(miniQuiz1);       //  4
            steps.Add(selection1);      //  5
            steps.Add(insert1);         //  6
            steps.Add(condStat);        //  7
            steps.Add(carrier);         //  8
            steps.Add(quiz1);           //  9  (routes correct/wrong explicitly)
            steps.Add(unreachable);     // 10  nothing routes in; SET edge out (insurance)
            steps.Add(quizResults1);    // 11  (routes passed/failed explicitly)
            steps.Add(island1);         // 12  island head - nextGuid=""
            steps.Add(island2);         // 13  island tail - nextGuid=""
            steps.Add(groupAll);        // 14
            steps.Add(groupAny);        // 15
            steps.Add(groupSpecific);   // 16
            steps.Add(groupRequired);   // 17
            steps.Add(groupNofM);       // 18
            steps.Add(groupMulti);      // 19
            steps.Add(groupMulti2);     // 20
            steps.Add(condComp);        // 21
            steps.Add(condList);        // 22
            steps.Add(quiz2);           // 23
            steps.Add(quizResults2);    // 24
            steps.Add(cueCards2);       // 25
            steps.Add(question2);       // 26
            steps.Add(miniQuiz2);       // 27
            steps.Add(selection2);      // 28
            steps.Add(insert2);         // 29
            steps.Add(timeline2);       // 30
            steps.Add(eventFinal);      // 31

            // ---- T1 field population ----

            // TimelineStep #1: scene director (no playable asset), non-default flags.
            timeline1.director = r.director;
            timeline1.rewindOnEnter = false;
            timeline1.waitForEnd = false;
            timeline1.nextGuid = cueCards1.guid;

            // TimelineStep #2: director null, defaults.
            timeline2.nextGuid = eventFinal.guid;

            // CueCardsStep #1: fully wired.
            cueCards1.cards = new[] { r.card1, r.card2, r.card3 };   // card2 is inactive
            cueCards1.cueTimes = new[] { 3f, 4.5f, 6f };
            cueCards1.tapHint = r.tapHint;
            cueCards1.advanceMode = CueCardsStep.AdvanceMode.OnButton;
            cueCards1.nextButton = r.nextButton;
            cueCards1.extraObject = r.extraObject;
            cueCards1.extraShowAtIndex = 2;
            cueCards1.hideExtraWithFinalTap = false;
            cueCards1.useRenderersForExtra = false;
            cueCards1.fadeDuration = 0.4f;
            cueCards1.popScale = 1.2f;
            cueCards1.popDuration = 0.3f;
            cueCards1.fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);   // authored; scaleCurve stays default
            cueCards1.nextGuid = question1.guid;

            // CueCardsStep #2: minimal - all refs null, TapAnywhere, cueTimes length 1 with
            // cards length 3 (the documented applies-to-all shape).
            cueCards2.cards = new GameObject[3];
            cueCards2.cueTimes = new[] { 4f };
            cueCards2.advanceMode = CueCardsStep.AdvanceMode.TapAnywhere;
            cueCards2.nextGuid = question2.guid;

            // QuestionStep #1: panel + DISABLED animator, non-default triggers, 4 choices covering
            // forward / back-edge / empty-mixed-into-routed / duplicate-target (T2/T6).
            question1.panelRoot = r.questionPanel;
            question1.panelAnimator = r.questionAnimator;
            question1.showTrigger = "ShowMega";
            question1.hideTrigger = "HideMega";
            question1.fallbackHideSeconds = 12.5f;
            var q1c0 = new Choice { button = r.choiceButtonA, nextGuid = hub.guid };          // forward
            q1c0.effects.Add(new StatEffect { key = "Ygeia", op = StatOp.Add, value = 10f });
            q1c0.effects.Add(new StatEffect { key = "Ygeia", op = StatOp.Set, value = 50f });
            var q1c1 = new Choice { button = r.choiceButtonB, nextGuid = timeline1.guid };    // back-edge
            var q1c2 = new Choice { nextGuid = "" };          // empty route in a routed list; gets the
                                                              // fully-empty listener row (surgery)
            var q1c3 = new Choice { nextGuid = hub.guid };    // duplicate-target with c0
            question1.choices.Add(q1c0);
            question1.choices.Add(q1c1);
            question1.choices.Add(q1c2);
            question1.choices.Add(q1c3);

            // QuestionStep #2: minimal - panel refs null, 2 plain choices; c0 is the
            // Pharmacy-style wrong-answer loop (back-edge).
            question2.choices.Add(new Choice { nextGuid = cueCards2.guid });      // back-edge loop
            question2.choices.Add(new Choice { nextGuid = miniQuiz2.guid });

            // Event hub: fan-in target; one dominant-shape listener.
            UnityEventTools.AddBoolPersistentListener(hub.onEnter, new UnityAction<bool>(r.tapHint.SetActive), false);
            hub.nextGuid = miniQuiz1.guid;

            // MiniQuizStep #1: twin "Buttons Parent" buttons, OnSubmitButton + deep submit,
            // outcomes forward + BACK, non-empty defaultNextGuid.
            miniQuiz1.panelRoot = r.miniQuizPanel;
            var mq1q0 = new MiniQuizQuestion { label = "Erotisi 1" };
            mq1q0.choices.Add(new MiniQuizChoice { button = r.btnNaiA, isCorrect = true });
            mq1q0.choices.Add(new MiniQuizChoice { button = r.btnOxiA });
            var mq1q1 = new MiniQuizQuestion { label = "Erotisi 2" };
            mq1q1.choices.Add(new MiniQuizChoice { button = r.btnNaiB });
            mq1q1.choices.Add(new MiniQuizChoice { button = r.btnOxiB });
            miniQuiz1.questions.Add(mq1q0);
            miniQuiz1.questions.Add(mq1q1);
            miniQuiz1.completion = MiniQuizStep.CompleteMode.OnSubmitButton;
            miniQuiz1.submitButton = r.deepSubmit;
            miniQuiz1.outcomes.Add(new MiniQuizOutcome { label = "Teleia", minCorrect = 2, maxCorrect = 2, nextGuid = selection1.guid });
            miniQuiz1.outcomes.Add(new MiniQuizOutcome { label = "Xana", minCorrect = 0, maxCorrect = 1, nextGuid = question1.guid });   // back-edge
            miniQuiz1.defaultNextGuid = selection1.guid;

            // MiniQuizStep #2: AutoWhenAllAnswered, defaultNextGuid="", choice listener + effects
            // (the Delirium rows v1 missed - D1 superset precondition).
            miniQuiz2.completion = MiniQuizStep.CompleteMode.AutoWhenAllAnswered;
            miniQuiz2.defaultNextGuid = "";
            var mq2q0 = new MiniQuizQuestion { label = "Γρήγορη ερώτηση" };
            var mq2c0 = new MiniQuizChoice { isCorrect = true };
            UnityEventTools.AddBoolPersistentListener(
                mq2c0.onSelected, Setter<bool>(r.selectionAnimator, "set_enabled"), false);   // Behaviour.set_enabled (T4)
            mq2c0.effects.Add(new StatEffect { key = "Ygeia", op = StatOp.Subtract, value = 5f });
            mq2q0.choices.Add(mq2c0);
            mq2q0.choices.Add(new MiniQuizChoice());
            miniQuiz2.questions.Add(mq2q0);

            // SelectionStep #1: fully wired - first-ever coverage of onCorrect/onWrong +
            // onCorrectEffects/onWrongEffects; correct forward, wrong back-edge.
            selection1.lists = r.selectionLists;
            selection1.listKey = "Wounds";
            selection1.completion = SelectionStep.CompleteMode.OnSubmitButton;
            selection1.submitButton = r.submitButton;
            selection1.requiredSelections = 2;
            selection1.requireExactCount = true;
            selection1.allowedWrong = 1;
            selection1.timeoutSeconds = 30f;
            selection1.correctNextGuid = insert1.guid;
            selection1.wrongNextGuid = hub.guid;                      // back-edge
            selection1.panelRoot = r.selectionPanel;
            selection1.panelAnimator = r.selectionAnimator;
            selection1.hint = r.hintPanel;
            // onCorrect: the Delirium byte-identical duplicate row pair (T4).
            UnityEventTools.AddBoolPersistentListener(selection1.onCorrect, new UnityAction<bool>(r.sostoImage.SetActive), true);
            UnityEventTools.AddBoolPersistentListener(selection1.onCorrect, new UnityAction<bool>(r.sostoImage.SetActive), true);
            // onWrong row 0: String mode (DIPAE SetTrigger shape). Row 1: authored with a target,
            // then target nulled by surgery -> clean-null-WITH-method (m_TargetAssemblyTypeName survives).
            UnityEventTools.AddStringPersistentListener(selection1.onWrong, new UnityAction<string>(r.selectionAnimator.SetTrigger), "Hide");
            UnityEventTools.AddBoolPersistentListener(selection1.onWrong, new UnityAction<bool>(r.hintPanel.SetActive), false);
            selection1.onCorrectEffects.Add(new StatEffect { key = "Asfaleia", op = StatOp.Multiply, value = 1.5f });
            selection1.onWrongEffects.Add(new StatEffect { key = "Asfaleia", op = StatOp.Divide, value = 2f });

            // SelectionStep #2: the old synthetic's unique states (D1 precondition) - null-heavy,
            // Auto, listIndex instead of listKey, correct SET + wrong EMPTY (the asymmetric route).
            selection2.completion = SelectionStep.CompleteMode.AutoWhenRequirementMet;
            selection2.listKey = "";
            selection2.listIndex = 1;
            selection2.correctNextGuid = insert2.guid;
            selection2.wrongNextGuid = "";

            // InsertStep #1: trailing-space item, BoxCollider trigger, deep attach, non-default scalars.
            insert1.item = r.itemTrailing;
            insert1.targetTrigger = r.insertSlot;
            insert1.attachTransform = r.attachPoint;
            insert1.smoothAttach = false;
            insert1.parentToAttach = false;
            insert1.moveSpeed = 2.5f;
            insert1.rotateSpeed = 8f;
            insert1.positionTolerance = 0.05f;
            insert1.angleTolerance = 0f;
            insert1.nextGuid = condStat.guid;

            // InsertStep #2: minimal (nulls).
            insert2.nextGuid = timeline2.guid;

            // Event carrier: non-default waitSeconds; typed T4 rows are wired below, surgical
            // shapes appended by ApplySurgicalListenerShapes.
            carrier.waitSeconds = 2.5f;
            carrier.nextGuid = quiz1.guid;
            WireCarrierTypedRows(carrier, r);

            // ConditionsStep x3 - one per valueSource; outcomes cover all 8 CompareOps with
            // forward / back / one empty route (T1/T2).
            condStat.valueSource = ConditionValueSource.Stat;
            condStat.statKey = "Asfaleia";
            condStat.outcomes.Add(new ConditionOutcome { label = "Low", compareOp = CompareOp.Less, compareValue = 50f, nextGuid = carrier.guid });
            condStat.outcomes.Add(new ConditionOutcome { label = "Critical", compareOp = CompareOp.LessOrEqual, compareValue = 25f, nextGuid = selection1.guid });   // back-edge
            condStat.outcomes.Add(new ConditionOutcome { label = "High", compareOp = CompareOp.Greater, compareValue = 75f, nextGuid = "" });   // the one empty outcome

            condComp.valueSource = ConditionValueSource.Component;
            condComp.source = r.audioA;
            condComp.memberName = "volume";
            condComp.outcomes.Add(new ConditionOutcome { label = "Loud", compareOp = CompareOp.GreaterOrEqual, compareValue = 0.5f, nextGuid = condList.guid });
            condComp.outcomes.Add(new ConditionOutcome { label = "Max", compareOp = CompareOp.Equal, compareValue = 1f, nextGuid = groupMulti.guid });   // back-edge
            condComp.outcomes.Add(new ConditionOutcome { label = "Other", compareOp = CompareOp.NotEqual, compareValue = 0.25f, nextGuid = quiz2.guid });

            condList.valueSource = ConditionValueSource.ListByLabel;
            condList.source = r.selectionLists;
            condList.memberName = "";
            condList.listFieldName = "lists";
            condList.listEntryLabel = "Wounds";
            condList.listLabelFieldName = "name";      // non-default ("label")
            condList.listValueFieldName = "plithos";   // non-default ("count")
            condList.outcomes.Add(new ConditionOutcome { label = "Nai", compareOp = CompareOp.IsTrue, compareValue = 0f, nextGuid = quiz2.guid });
            condList.outcomes.Add(new ConditionOutcome { label = "Oxi", compareOp = CompareOp.IsFalse, compareValue = 0f, nextGuid = hub.guid });   // back-edge

            // QuizStep #1: the package-internal QuizAsset (asset: identity), BranchOnCorrectness,
            // OnSubmitButton, ForSeconds feedback; wrong is a back-edge.
            quiz1.quiz = quiz;
            quiz1.questionIndex = 1;
            quiz1.completion = QuizStep.CompleteMode.BranchOnCorrectness;
            quiz1.correctNextGuid = quizResults1.guid;
            quiz1.wrongNextGuid = carrier.guid;                       // back-edge
            quiz1.submitMode = QuizStep.AnswerSubmitMode.OnSubmitButton;
            quiz1.feedback = QuizStep.FeedbackMode.ForSeconds;
            quiz1.feedbackSeconds = 3.5f;

            // QuizStep #2: quiz null, questionId addressing, AnyAnswer + nextGuid, ImmediateSelection, None.
            quiz2.questionId = "mega-q-02";
            quiz2.completion = QuizStep.CompleteMode.AnyAnswer;
            quiz2.nextGuid = quizResults2.guid;
            quiz2.submitMode = QuizStep.AnswerSubmitMode.ImmediateSelection;
            quiz2.feedback = QuizStep.FeedbackMode.None;

            // QuizStep #3 (G-Any child): carries FeedbackMode.UntilContinue - the enum-coverage
            // rule's third value (see class header note).
            quiz3.completion = QuizStep.CompleteMode.AnyAnswer;
            quiz3.feedback = QuizStep.FeedbackMode.UntilContinue;
            quiz3.nextGuid = "";

            // QuizResultsStep #1: BranchOnPassed with BOTH routes set (failed = back-edge),
            // AfterSeconds + non-default completeAfterSeconds.
            quizResults1.completion = QuizResultsStep.CompleteMode.BranchOnPassed;
            quizResults1.passedNextGuid = groupAll.guid;
            quizResults1.failedNextGuid = hub.guid;                   // back-edge (more hub fan-in)
            quizResults1.whenComplete = QuizResultsStep.WhenComplete.AfterSeconds;
            quizResults1.completeAfterSeconds = 4f;

            // QuizResultsStep #2: OnContinue + nextGuid, AfterContinueButtonPressed.
            quizResults2.completion = QuizResultsStep.CompleteMode.OnContinue;
            quizResults2.nextGuid = cueCards2.guid;
            quizResults2.whenComplete = QuizResultsStep.WhenComplete.AfterContinueButtonPressed;

            // T6 - insurance unreachable step WITH a set edge (beyond the DIPAE fall-through island).
            unreachable.nextGuid = eventFinal.guid;
            // T6 - island steps + final dead end: nextGuid stays "" (field default).

            // ---- T3 groups ----

            // G-All: 2 leaf children + one NESTED GroupStep child (depth 3).
            groupAll.completeWhen = GroupStep.CompleteWhen.AllChildrenComplete;
            groupAll.stopOthersOnComplete = true;
            groupAll.steps.Add(allChild1);
            groupAll.steps.Add(allChild2);
            groupAll.steps.Add(nestedGroup);
            groupAll.nextGuid = groupAny.guid;
            nestedGroup.completeWhen = GroupStep.CompleteWhen.AllChildrenComplete;
            nestedGroup.stopOthersOnComplete = true;
            nestedGroup.steps.Add(nestedChild1);
            nestedGroup.steps.Add(nestedChild2);
            nestedGroup.nextGuid = "";   // group child norm

            // G-Any: first-ever serialized AnyChildCompletes; hosts QuizStep #3.
            groupAny.completeWhen = GroupStep.CompleteWhen.AnyChildCompletes;
            groupAny.stopOthersOnComplete = false;
            groupAny.steps.Add(anyChild1);
            groupAny.steps.Add(quiz3);
            groupAny.nextGuid = groupSpecific.guid;

            // G-Specific: specificStepGuid -> child 2.
            groupSpecific.completeWhen = GroupStep.CompleteWhen.SpecificChildCompletes;
            groupSpecific.stopOthersOnComplete = true;
            groupSpecific.steps.Add(specChild1);
            groupSpecific.steps.Add(specChild2);
            groupSpecific.specificStepGuid = specChild2.guid;
            groupSpecific.nextGuid = groupRequired.guid;

            // G-Required: mixed required flags - first-ever.
            groupRequired.completeWhen = GroupStep.CompleteWhen.RequiredChildrenComplete;
            groupRequired.stopOthersOnComplete = false;
            groupRequired.steps.Add(reqChild1);
            groupRequired.steps.Add(reqChild2);
            groupRequired.steps.Add(reqChild3);
            groupRequired.nextGuid = groupNofM.guid;

            // G-NofM: requiredCount = 2 of 3 - first-ever.
            groupNofM.completeWhen = GroupStep.CompleteWhen.NOfMChildrenComplete;
            groupNofM.requiredCount = 2;
            groupNofM.stopOthersOnComplete = true;
            groupNofM.steps.Add(nofmChild1);
            groupNofM.steps.Add(nofmChild2);
            groupNofM.steps.Add(nofmChild3);
            groupNofM.nextGuid = groupMulti.guid;

            // G-Multi: the REAL Loimokseis shape (§1.6) - 2 branches, same childRequirement guid
            // order as the group list with COMPLEMENTARY required flags, distinct labels, modes
            // RequiredChildrenComplete + NOfMChildrenComplete(requiredCount=1), BOTH branches ->
            // the same nextGuid (G-Multi-2, reachable ONLY this way); group-level specificStepGuid
            // present-but-empty; group nextGuid="".
            groupMulti.completeWhen = GroupStep.CompleteWhen.MultiCondition;
            groupMulti.stopOthersOnComplete = true;
            groupMulti.steps.Add(multiChild1);
            groupMulti.steps.Add(multiChild2);
            groupMulti.steps.Add(multiChild3);
            groupMulti.steps.Add(multiChild4);
            groupMulti.specificStepGuid = "";
            groupMulti.nextGuid = "";
            var branchA = new GroupStep.MultiConditionBranch
            {
                label = "Krisi",
                mode = GroupStep.CompleteWhen.RequiredChildrenComplete,
                specificStepGuid = multiChild1.guid,   // the one set branch specificStepGuid (T2)
                nextGuid = groupMulti2.guid,
            };
            branchA.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild1.guid, required = true });
            branchA.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild2.guid, required = true });
            branchA.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild3.guid, required = false });
            branchA.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild4.guid, required = false });
            var branchB = new GroupStep.MultiConditionBranch
            {
                label = "Brisi",
                mode = GroupStep.CompleteWhen.NOfMChildrenComplete,
                requiredCount = 1,
                specificStepGuid = "",
                nextGuid = groupMulti2.guid,           // two branches -> one exit (the Brisi shape)
            };
            branchB.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild1.guid, required = false });
            branchB.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild2.guid, required = false });
            branchB.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild3.guid, required = true });
            branchB.childRequirements.Add(new GroupStep.ChildRequirement { guid = multiChild4.guid, required = true });
            groupMulti.multiConditionBranches.Add(branchA);
            groupMulti.multiConditionBranches.Add(branchB);

            // G-Multi-2: reachable ONLY via G-Multi's branches.
            groupMulti2.completeWhen = GroupStep.CompleteWhen.MultiCondition;
            groupMulti2.stopOthersOnComplete = false;
            groupMulti2.steps.Add(multi2Child1);
            groupMulti2.steps.Add(multi2Child2);
            groupMulti2.specificStepGuid = "";
            groupMulti2.nextGuid = "";
            var exitBranch = new GroupStep.MultiConditionBranch
            {
                label = "Exodos",
                mode = GroupStep.CompleteWhen.AllChildrenComplete,
                specificStepGuid = "",
                nextGuid = condComp.guid,
            };
            exitBranch.childRequirements.Add(new GroupStep.ChildRequirement { guid = multi2Child1.guid, required = true });
            exitBranch.childRequirements.Add(new GroupStep.ChildRequirement { guid = multi2Child2.guid, required = true });
            groupMulti2.multiConditionBranches.Add(exitBranch);

            // §1.7/§6.3 rule-row: PRE-AUTHOR the full childRequirements list on EVERY group, in
            // steps-list order, so OnValidate's normalizer (EnsureChildRequirements +
            // EnsureMultiConditionBranchRequirements) adds NOTHING. G-Required carries the only
            // group-level required=false flags.
            PreauthorChildRequirements(groupAll, true, true, true);
            PreauthorChildRequirements(nestedGroup, true, true);
            PreauthorChildRequirements(groupAny, true, true);
            PreauthorChildRequirements(groupSpecific, true, true);
            PreauthorChildRequirements(groupRequired, true, false, true);
            PreauthorChildRequirements(groupNofM, true, true, true);
            PreauthorChildRequirements(groupMulti, true, true, true, true);
            PreauthorChildRequirements(groupMulti2, true, true);

            // Deterministic graph positions (cosmetic, but committed bytes - keep them fixed).
            LayoutGraph(steps, 0);

            ctx.question1 = question1;
            ctx.selection1 = selection1;
            ctx.carrier = carrier;
            ctx.hub = hub;
            return ctx;
        }

        // T4 - the typed listener rows on the carrier EventStep. The shapes the typed API cannot
        // express (asset-arg Object row, fully-empty row, target-with-empty-method, clean-null
        // target) are applied afterwards by ApplySurgicalListenerShapes.
        static void WireCarrierTypedRows(EventStep carrier, SceneRefs r)
        {
            var evt = carrier.onEnter;

            // r0 - SetActive(bool) Bool mode: the dominant real method (176/242).
            UnityEventTools.AddBoolPersistentListener(evt, new UnityAction<bool>(r.panelMap.SetActive), true);
            // r1 - CanvasGroup.set_alpha(0.7f) Float mode: property-setter name + float round-trip noise.
            UnityEventTools.AddFloatPersistentListener(evt, Setter<float>(r.mapCanvasGroup, "set_alpha"), 0.7f);
            // r2 - AudioSource.Play() Void mode (first AudioSource on the dual-source GO).
            UnityEventTools.AddVoidPersistentListener(evt, new UnityAction(r.audioA.Play));
            // r3 - PlayableDirector.Stop() Void mode.
            UnityEventTools.AddVoidPersistentListener(evt, new UnityAction(r.director.Stop));
            // r4 - Transform.SetParent(Transform) Object mode with a SCENE-object argument.
            UnityEventTools.AddObjectPersistentListener(evt, new UnityAction<Transform>(r.sphereProp.SetParent), r.parentTarget);
            // r5 - Transform.SetSiblingIndex(int) Int mode: a mode no baseline exercises.
            UnityEventTools.AddIntPersistentListener(evt, new UnityAction<int>(r.capsuleProp.SetSiblingIndex), 1);
            // r6 - dynamic bind (EventDefined, m_Mode 0) pinned to AudioSource.Play; verified by
            // surgery and repaired there if the typed API ever changes its mode.
            UnityEventTools.AddPersistentListener(evt, new UnityAction(r.audioA.Play));
            // r7/r8 - call-state monoculture broken: one Off, one EditorAndRuntime, on the
            // same-named "Panel epomeno domatio" siblings (both referenced - T5).
            UnityEventTools.AddBoolPersistentListener(evt, new UnityAction<bool>(r.panelEpomenoA.SetActive), false);
            evt.SetPersistentListenerState(7, UnityEventCallState.Off);
            UnityEventTools.AddBoolPersistentListener(evt, new UnityAction<bool>(r.panelEpomenoB.SetActive), true);
            evt.SetPersistentListenerState(8, UnityEventCallState.EditorAndRuntime);
            // r9 - authored with target + method; surgery clears the METHOD -> target-set + empty method.
            UnityEventTools.AddBoolPersistentListener(evt, new UnityAction<bool>(r.extraObject.SetActive), true);
            // r10 (appended by surgery) - Object mode, m_ObjectArgument -> the package QuizAsset
            // ("asset:" StableId), targeting the SECOND AudioSource (both sources targeted - T5).
        }

        // ---- T4 surgery - shapes only SerializedProperty can author ---------------------------

        static void ApplySurgicalListenerShapes(Scenario scenario, StepCtx ctx, SceneRefs r, QuizAsset quiz)
        {
            var so = new SerializedObject(scenario);

            so.FindProperty("title").stringValue = MegaTitle;

            int selIdx = scenario.steps.IndexOf(ctx.selection1);
            int carrierIdx = scenario.steps.IndexOf(ctx.carrier);
            int questionIdx = scenario.steps.IndexOf(ctx.question1);

            // Selection #1 onWrong row 1 -> clean-null-target WITH method (the Delirium/Ekpa
            // detritus): null only m_Target; m_TargetAssemblyTypeName + m_MethodName survive.
            var selWrong = Calls(so, selIdx, "onWrong");
            selWrong.GetArrayElementAtIndex(1).FindPropertyRelative("m_Target").objectReferenceValue = null;

            var carrierCalls = Calls(so, carrierIdx, "onEnter");

            // Carrier r6 - VERIFY the dynamic bind landed as EventDefined (m_Mode 0); property
            // surgery is the sanctioned fallback if the typed API authored anything else.
            var dyn = carrierCalls.GetArrayElementAtIndex(6);
            var dynMode = dyn.FindPropertyRelative("m_Mode");
            if (dynMode.enumValueIndex != (int)PersistentListenerMode.EventDefined)
            {
                Debug.LogWarning("[DevKit] Mega builder: UnityEventTools.AddPersistentListener did not "
                    + "author m_Mode=EventDefined (got " + dynMode.enumValueIndex + ") - repairing via "
                    + "SerializedProperty surgery.");
                dynMode.enumValueIndex = (int)PersistentListenerMode.EventDefined;
                dyn.FindPropertyRelative("m_Target").objectReferenceValue = r.audioA;
                dyn.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(AudioSource).AssemblyQualifiedName;
                dyn.FindPropertyRelative("m_MethodName").stringValue = "Play";
            }

            // Carrier r9 -> target-set + EMPTY method (third benign shape).
            carrierCalls.GetArrayElementAtIndex(9).FindPropertyRelative("m_MethodName").stringValue = "";

            // Carrier r10 (append) - Object mode with the PACKAGE-INTERNAL asset as
            // m_ObjectArgument: the "asset:" StableId form (§4.5). Targets the second AudioSource.
            int assetRow = carrierCalls.arraySize;
            carrierCalls.arraySize = assetRow + 1;
            var rowProp = carrierCalls.GetArrayElementAtIndex(assetRow);
            FillCallRow(rowProp,
                target: r.audioB,
                targetTypeName: typeof(AudioSource).AssemblyQualifiedName,
                methodName: "set_clip",
                mode: PersistentListenerMode.Object,
                objectArg: quiz,
                objectArgTypeName: typeof(QuizAsset).AssemblyQualifiedName);

            // Question #1 c2 onSelected (append) - the FULLY-EMPTY row (superset insurance;
            // authored exactly like the inspector's "+" leaves it: RuntimeOnly + Void + all empty).
            var c2Calls = Calls(so, questionIdx, "choices.Array.data[2].onSelected");
            int emptyRow = c2Calls.arraySize;
            c2Calls.arraySize = emptyRow + 1;
            FillCallRow(c2Calls.GetArrayElementAtIndex(emptyRow),
                target: null, targetTypeName: "", methodName: "",
                mode: PersistentListenerMode.Void, objectArg: null, objectArgTypeName: "");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scenario);
        }

        // Explicitly writes EVERY field of a persistent-call row: growing a SerializedProperty
        // array can duplicate the previous element's values, so nothing may be left implicit.
        static void FillCallRow(SerializedProperty row, Object target, string targetTypeName,
            string methodName, PersistentListenerMode mode, Object objectArg, string objectArgTypeName)
        {
            row.FindPropertyRelative("m_Target").objectReferenceValue = target;
            row.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = targetTypeName;
            row.FindPropertyRelative("m_MethodName").stringValue = methodName;
            row.FindPropertyRelative("m_Mode").enumValueIndex = (int)mode;
            row.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEventCallState.RuntimeOnly;
            var args = row.FindPropertyRelative("m_Arguments");
            args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = objectArg;
            args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = objectArgTypeName;
            args.FindPropertyRelative("m_IntArgument").intValue = 0;
            args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
            args.FindPropertyRelative("m_StringArgument").stringValue = "";
            args.FindPropertyRelative("m_BoolArgument").boolValue = false;
        }

        static SerializedProperty Calls(SerializedObject so, int stepIndex, string eventPath)
        {
            var p = so.FindProperty("steps.Array.data[" + stepIndex + "]." + eventPath + ".m_PersistentCalls.m_Calls");
            if (p == null)
                throw new System.InvalidOperationException(
                    "[DevKit] Mega builder: persistent-calls property not found for step " + stepIndex + " / " + eventPath);
            return p;
        }

        // ---- T7 - editor-only serialized surface (Proof C riders) -----------------------------

        static void AddEditorOnlySurface(Scenario scenario, StepCtx ctx)
        {
            scenario.GraphNotes.Add(new Scenario.GraphNote
            {
                guid = "mega-note-01",
                rect = new Rect(120f, 40f, 240f, 160f),
                text = "Ελεύθερη σημείωση - free note.",
                attachedStepGuid = "",
            });
            scenario.GraphNotes.Add(new Scenario.GraphNote
            {
                guid = "mega-note-02",
                rect = new Rect(80f, 80f, 240f, 160f),
                text = "Tethered to the hub.",
                attachedStepGuid = ctx.hub.guid,
                attachOffset = new Vector2(40f, -30f),
            });

            scenario.GraphGroups.Add(new Scenario.GraphGroup
            {
                guid = "mega-graphgroup-01",
                title = "Φάση 1",
                rect = new Rect(40f, 40f, 520f, 320f),
            });

            // Side-table entries only for EXISTING steps (no orphans - spec T7).
            scenario.StepGraphDisplays.Add(new Scenario.StepGraphDisplay
            {
                stepGuid = ctx.question1.guid,
                displayName = "Διαδρομή Α",
                size = Vector2.zero,
            });
            scenario.StepGraphDisplays.Add(new Scenario.StepGraphDisplay
            {
                stepGuid = ctx.carrier.guid,
                size = new Vector2(420f, 260f),
                displayName = "",
            });
        }

        // ---- §6.3 - reserialize idempotence --------------------------------------------------

        static bool AssertReserializeIdempotent(string fixtureAssetPath)
        {
            string disk = DiskPath(fixtureAssetPath);
            string before = File.ReadAllText(disk);
            AssetDatabase.ForceReserializeAssets(new[] { fixtureAssetPath });
            string after = File.ReadAllText(disk);
            if (!string.Equals(before, after, System.StringComparison.Ordinal))
            {
                Fail("OnValidate-idempotence violated: ForceReserializeAssets changed '" + fixtureAssetPath
                    + "'. The builder failed to pre-author some normalizer output (most likely a "
                    + "childRequirements row or its order) - this is a MEGA BUILDER BUG (spec §6.3). "
                    + "Artifacts are left in place for inspection.");
                return false;
            }
            return true;
        }

        // ---- §4.2 - the prefab-Variant twin (same run, conservative overrides only) -----------

        static bool BuildVariant(string megaPath, out string variantPath)
        {
            variantPath = megaPath.Substring(0, megaPath.LastIndexOf('/') + 1) + VariantFixtureName + ".prefab";

            var megaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(megaPath);
            if (megaPrefab == null) { Fail("could not load the saved mega prefab at " + megaPath); return false; }

            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(megaPrefab, previewScene);
                if (instance == null) { Fail("could not instantiate the mega prefab into a preview scene."); return false; }

                // EXACTLY three conservative overrides - never anything under steps (D2).
                // 1) title += " (variant)".
                var scenario = instance.GetComponentInChildren<Scenario>(true);
                if (scenario == null) { Fail("variant instance has no Scenario component."); return false; }
                var so = new SerializedObject(scenario);
                so.FindProperty("title").stringValue = scenario.Title + " (variant)";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(scenario);

                // 2) rename ONE child GameObject (deliberately unreferenced by any step).
                var propA = FindChildByName(instance.transform, "Prop A");
                if (propA == null) { Fail("variant override target 'Prop A' not found."); return false; }
                propA.name = "Prop A (variant)";
                PrefabUtility.RecordPrefabInstancePropertyModifications(propA);

                // 3) move one transform (deliberately unreferenced by any step).
                var propB = FindChildByName(instance.transform, "Prop B");
                if (propB == null) { Fail("variant override target 'Prop B' not found."); return false; }
                propB.transform.localPosition = new Vector3(1.5f, 0f, -2.25f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(propB.transform);

                // Save WITHOUT unpacking: a prefab instance saved as an asset becomes a true Variant.
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out bool ok);
                if (!ok || saved == null) { Fail("SaveAsPrefabAsset failed for the variant - see the Console."); return false; }

                // Normalize-then-capture (same field fix as the mega): canonical form first, then
                // the baseline reflects what every future load reads.
                AssetDatabase.SaveAssets();
                AssetDatabase.ForceReserializeAssets(new[] { variantPath });
                AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceUpdate);

                if (!ExportLabAsTestFixture.CaptureBaselineFor(variantPath, out string baselinePath, out string error))
                {
                    Fail("variant baseline capture failed: " + error);
                    return false;
                }

                // D2 / §4.2 - the BUILD-TIME feasibility gate: all four green-gate checks run here,
                // in-run, so a Unity churn on variant-over-[SerializeReference] is a demote decision
                // now, not a red gate later. (Note: the override-no-churn check fails RED in the
                // gate, not Inconclusive - which is exactly why it must run here too.)
                if (!VariantFeasibilityGate(variantPath, baselinePath)) return false;

                return true;
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        // D2 gate: invariants, snapshot-vs-baseline, reserialize idempotency, override-no-churn.
        // ANY misbehaviour -> Fail with the demote-to-Mechanics instruction (spec §4.2).
        static bool VariantFeasibilityGate(string variantPath, string baselinePath)
        {
            const string demote = "\nDEMOTE PATH (spec §4.2/D2): move the variant out of the green gate - "
                + "delete Tests/Fixtures/Scenarios/" + VariantFixtureName + ".prefab + its baseline, "
                + "recreate it under Tests/Fixtures/Mechanics/ with a dedicated test, and record the "
                + "demotion in the spec.";

            // (1) Invariants on the saved variant asset.
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            var scenario = asset != null ? ScenarioGraphSnapshot.FindScenario(asset) : null;
            if (scenario == null)
            {
                Fail("variant gate: could not load the saved variant / find its Scenario." + demote);
                return false;
            }
            var violations = ScenarioGraphSnapshot.CheckInvariants(scenario);
            if (violations.Count > 0)
            {
                Fail("variant gate: " + violations.Count + " invariant violation(s) on the saved variant "
                    + "(first: " + violations[0] + ")." + demote);
                return false;
            }

            // (2) Snapshot re-extraction must byte-match the just-written baseline.
            string reExtracted = ScenarioGraphSnapshot.BuildSnapshotJson(scenario);
            string committed = System.IO.File.ReadAllText(DiskPath(baselinePath));
            if (!string.Equals(reExtracted, committed, System.StringComparison.Ordinal))
            {
                Fail("variant gate: snapshot re-extraction does not match the just-written baseline - "
                    + "the variant's effective state is unstable under load." + demote);
                return false;
            }

            // (3) Reserialize idempotency (same check the mega passes).
            if (!AssertReserializeIdempotent(variantPath))
            {
                Fail("variant gate: reserialize idempotency failed (see the error above)." + demote);
                return false;
            }

            // (4) Override-no-churn: instantiate the VARIANT, apply one unrelated override, and
            // assert Unity records nothing under steps/managedReferences (the I.6 contract the
            // round-trip test enforces RED).
            var gateScene = EditorSceneManager.NewPreviewScene();
            GameObject gateInstance = null;
            try
            {
                gateInstance = (GameObject)PrefabUtility.InstantiatePrefab(asset, gateScene);
                var gateScenario = gateInstance != null ? gateInstance.GetComponentInChildren<Scenario>(true) : null;
                if (gateScenario == null)
                {
                    Fail("variant gate: could not instantiate the variant for the override-no-churn check." + demote);
                    return false;
                }
                string nearest = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gateInstance);
                if (!string.Equals(nearest.Replace('\\', '/'), variantPath.Replace('\\', '/'), System.StringComparison.Ordinal))
                {
                    Fail("variant gate: nearest instance root is '" + nearest + "', expected the variant "
                        + "itself - the source-prefab link is wrong." + demote);
                    return false;
                }
                var so = new SerializedObject(gateScenario);
                so.FindProperty("title").stringValue += " (ovr)";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(gateScenario);
                var mods = PrefabUtility.GetPropertyModifications(gateInstance);
                if (mods != null)
                {
                    foreach (var m in mods)
                    {
                        if (m.propertyPath.StartsWith("steps", System.StringComparison.Ordinal)
                            || m.propertyPath.Contains("managedReferences"))
                        {
                            Fail("variant gate: an unrelated title override leaked a '" + m.propertyPath
                                + "' instance modification - Unity is churning the step graph through "
                                + "the variant chain." + demote);
                            return false;
                        }
                    }
                }
                return true;
            }
            finally
            {
                if (gateInstance != null) Object.DestroyImmediate(gateInstance);
                EditorSceneManager.ClosePreviewScene(gateScene);
            }
        }

        // ---- §4.3 v3 - LegacyForms twins (generator-derived, no hand YAML) --------------------

        static bool BuildLegacyTwins(QuizAsset quiz, out string currentPath, out string legacyPath)
        {
            currentPath = null;
            legacyPath = null;

            string legacyDir = TestsSub(LegacyFormsLeaf);
            if (legacyDir == null) { Fail("could not locate the Tests/ folder for LegacyForms."); return false; }
            EnsureFolder(legacyDir);
            currentPath = legacyDir + "/" + LegacyCurrentFile;
            legacyPath = legacyDir + "/" + LegacyOldFile;

            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                // The current-form slice: a SceneManager wired to the package QuizAsset + the two
                // Quiz UI controller components, plus a Scenario host with 3 simple steps. The
                // Scenario's editor-only lists stay EMPTY by construction, so each serializes as a
                // single "name: []" line the legacy twin then strips.
                root = new GameObject("LegacyFormCurrent");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);

                var hostGo = NewChild(root, "LegacyScenarioHost");
                var scenario = hostGo.AddComponent<Scenario>();
                var s1 = MakeStep<EventStep>("legacy-step-01");
                var s2 = MakeStep<EventStep>("legacy-step-02");
                var s3 = MakeStep<EventStep>("legacy-step-03");
                s1.nextGuid = s2.guid;
                scenario.steps.Add(s1);
                scenario.steps.Add(s2);
                scenario.steps.Add(s3);
                LayoutGraph(scenario.steps, 0);

                var quizPanelGo = NewChild(root, "Quiz Panel");
                var quizUi = quizPanelGo.AddComponent<QuizUIController>();
                var quizResultsGo = NewChild(root, "Quiz Results Panel");
                var quizResultsUi = quizResultsGo.AddComponent<QuizResultsUIController>();

                var smGo = NewChild(root, "Legacy Scene Manager");
                var sm = smGo.AddComponent<SceneManager>();
                sm.scenario = scenario;
                sm.defaultQuiz = quiz;                  // pre-FSA name: quiz
                sm.quizPanel = quizUi;                  // pre-FSA name: quizUI
                sm.quizResultsPanel = quizResultsUi;    // pre-FSA name: quizResultsUI

                var saved = PrefabUtility.SaveAsPrefabAsset(root, currentPath, out bool ok);
                if (!ok || saved == null) { Fail("SaveAsPrefabAsset failed for the legacy current twin."); return false; }
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            // Normalize-then-derive (same field fix as the mega/variant): the textual derivation
            // must read the CANONICAL bytes, or the legacy twin would inherit the same-session
            // pre-normalization form and drift on its first fresh import.
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { currentPath });
            AssetDatabase.ImportAsset(currentPath, ImportAssetOptions.ForceUpdate);

            return DeriveLegacyTwinTextually(currentPath, legacyPath);
        }

        // Textual derivation: rename exactly the three SceneManager fields to their
        // pre-FormerlySerializedAs names and strip the three (empty) editor-only list lines.
        static bool DeriveLegacyTwinTextually(string currentPath, string legacyPath)
        {
            string text = File.ReadAllText(DiskPath(currentPath));

            if (!ReplaceExactlyOnce(ref text, "defaultQuiz:", "quiz:")) return false;
            if (!ReplaceExactlyOnce(ref text, "quizPanel:", "quizUI:")) return false;
            if (!ReplaceExactlyOnce(ref text, "quizResultsPanel:", "quizResultsUI:")) return false;

            // Unity writes LF; split preserves the trailing newline as a final "" element.
            var lines = new List<string>(text.Split('\n'));
            int stripped = 0;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string trimmed = lines[i].Trim();
                if (trimmed == "graphNotes: []" || trimmed == "graphGroups: []" || trimmed == "stepGraphDisplays: []")
                {
                    lines.RemoveAt(i);
                    stripped++;
                }
            }
            if (stripped != 3)
            {
                Fail("legacy twin derivation expected to strip exactly 3 editor-only list lines, "
                    + "stripped " + stripped + " - the current twin's serialized form changed (builder bug).");
                return false;
            }

            File.WriteAllText(DiskPath(legacyPath), string.Join("\n", lines));
            AssetDatabase.ImportAsset(legacyPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath) == null)
            {
                Fail("the textually-derived legacy twin did not import as a prefab: " + legacyPath);
                return false;
            }
            return true;
        }

        static bool ReplaceExactlyOnce(ref string text, string oldToken, string newToken)
        {
            int first = text.IndexOf(oldToken, System.StringComparison.Ordinal);
            if (first < 0 || text.IndexOf(oldToken, first + oldToken.Length, System.StringComparison.Ordinal) >= 0)
            {
                Fail("legacy twin derivation expected exactly one occurrence of '" + oldToken
                    + "' in the current twin - found " + (first < 0 ? "none" : "more than one") + " (builder bug).");
                return false;
            }
            text = text.Substring(0, first) + newToken + text.Substring(first + oldToken.Length);
            return true;
        }

        // ---- small builders -------------------------------------------------------------------

        static T MakeStep<T>(string guid) where T : Step, new()
            => new T { guid = guid };

        // Pre-author the OnValidate-normalized childRequirements in steps-list order ({guid,
        // required} per child) so EnsureChildRequirements finds every row present and adds nothing.
        static void PreauthorChildRequirements(GroupStep group, params bool[] requiredFlags)
        {
            if (group.steps.Count != requiredFlags.Length)
                throw new System.InvalidOperationException(
                    "[DevKit] Mega builder: childRequirements flag count mismatch on group '" + group.guid + "'.");
            group.childRequirements.Clear();
            for (int i = 0; i < group.steps.Count; i++)
                group.childRequirements.Add(new GroupStep.ChildRequirement
                {
                    guid = group.steps[i].guid,
                    required = requiredFlags[i],
                });
        }

        // Deterministic node layout - cosmetic, but committed bytes, so it must be fixed in code.
        static void LayoutGraph(List<Step> steps, int depth)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                s.graphPos = new Vector2(i * 320f, depth * 200f);
                if (s is GroupStep g && g.steps != null)
                    LayoutGraph(g.steps, depth + 1);
            }
        }

        // UnityAction bound to a property SETTER (set_alpha / set_enabled): accessor methods are
        // not method groups in C#, so bind by name - exactly what the inspector serializes.
        static UnityAction<T> Setter<T>(object target, string setterName)
            => (UnityAction<T>)System.Delegate.CreateDelegate(typeof(UnityAction<T>), target, setterName);

        static GameObject NewRoot(string name, UnityEngine.SceneManagement.Scene scene)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // UI objects get a RectTransform up front (panels are RectTransform step fields; buttons
        // live on RectTransforms in every real lab).
        static GameObject NewUIChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // Deterministic depth-first search by exact (ordinal) name.
        static GameObject FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (string.Equals(c.name, name, System.StringComparison.Ordinal)) return c.gameObject;
                var deep = FindChildByName(c, name);
                if (deep != null) return deep;
            }
            return null;
        }

        // ---- package path helpers - thin forwards onto the export tool's internal helpers (one
        // Tests/-locator in the assembly; two copies could drift and silently resolve different
        // roots, breaking the §7.1.4 HasDeclaration asserts) --------------------------------------

        static string TestsSub(string leaf) => ExportLabAsTestFixture.TestsSub(leaf);

        static string DiskPath(string assetPath) => FileUtil.GetPhysicalPath(assetPath);

        static void EnsureFolder(string assetFolder) => ExportLabAsTestFixture.EnsureFolder(assetFolder);

        static void Fail(string message)
            => Debug.LogError("[DevKit] Mega fixture builder: " + message);
    }
}
#endif
