#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using USceneManager = UnityEngine.SceneManagement.SceneManager; // avoid name clash

namespace Pitech.XR.Scenario.Editor
{
    [CustomEditor(typeof(Pitech.XR.Scenario.LabConsole), true)]
    public class SceneManagerEditor : UnityEditor.Editor
    {
        // Bind to your runtime fields here (adjust names if needed)
        SerializedProperty _scenarioProp;      // Pitech.XR.Scenario.Scenario
        SerializedProperty _statsConfigProp;   // Pitech.XR.Stats.StatsConfig
        SerializedProperty _statsUIProp;       // Pitech.XR.Stats.StatsUIController
        SerializedProperty _autoStartProp;     // bool
        SerializedProperty _selectablesProp;    // Pitech.XR.Interactables.SelectablesManager
        SerializedProperty _selectionListsProp; // Pitech.XR.Interactables.SelectionLists
        SerializedProperty _defaultQuizProp;      // Pitech.XR.Quiz.QuizAsset
        SerializedProperty _quizPanelProp;        // Pitech.XR.Quiz.QuizUIController
        SerializedProperty _quizResultsPanelProp; // Pitech.XR.Quiz.QuizResultsUIController
        SerializedProperty _contentDeliveryProp;  // Pitech.XR.ContentDelivery.ContentDeliverySpawner (as MonoBehaviour)
        SerializedProperty _parametersProp;        // Pitech.XR.Core.ConsoleParameter list (the typed param store)
        const string ManagersRootName = "--- SCENE SETUP ---";              // canonical (2026-06-30 rename)
        const string LegacyManagersRootName = "--- SCENE MANAGERS ---";     // pre-rename name, still resolved

        // ❌ DO NOT cache EditorStyles in static fields — causes NREs on domain reload
        static GUIStyle TitleStyle => EditorStyles.boldLabel;

        void OnEnable()
        {
            // NOTE: change these strings if your runtime fields are named differently
            _scenarioProp = serializedObject.FindProperty("scenario");
            _statsConfigProp = serializedObject.FindProperty("statsConfig");
            _statsUIProp = serializedObject.FindProperty("statsUI");
            _autoStartProp = serializedObject.FindProperty("autoStart");
            _selectablesProp = serializedObject.FindProperty("selectables");
            _selectionListsProp = serializedObject.FindProperty("selectionLists");
            _defaultQuizProp = serializedObject.FindProperty("defaultQuiz");
            _quizPanelProp = serializedObject.FindProperty("quizPanel");
            _quizResultsPanelProp = serializedObject.FindProperty("quizResultsPanel");
            _contentDeliveryProp = serializedObject.FindProperty("contentDelivery");
            _parametersProp = serializedObject.FindProperty("parameters");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var gm = (Pitech.XR.Scenario.LabConsole)target;

            DrawHeaderHelp();
            EditorGUILayout.Space(6);

            // ============ FEATURES ============
            EditorGUILayout.LabelField("Features", TitleStyle);
            EditorGUILayout.Space(2);

            DrawScenarioFeature();
            EditorGUILayout.Space(6);
            DrawStatsFeature();

            EditorGUILayout.Space(6);
            DrawParametersFeature();

            EditorGUILayout.Space(6);
            DrawInteractablesFeature();
            
            EditorGUILayout.Space(6);
            DrawQuizFeature();

            EditorGUILayout.Space(6);
            DrawContentDeliveryFeature();

            EditorGUILayout.Space(6);
            DrawAnalyticsFeature(gm);

            EditorGUILayout.Space(8);
            if (_autoStartProp != null)
                EditorGUILayout.PropertyField(_autoStartProp, new GUIContent("Auto Start"));

            // ============ OVERVIEW & RUNTIME ============
            EditorGUILayout.Space(10);
            DrawScenarioOverview(gm);
            EditorGUILayout.Space(8);
            DrawRuntimeControls(gm);

            EditorGUILayout.Space(8);

            serializedObject.ApplyModifiedProperties();
        }

        // --------------------------------------------------------------------
        // Header
        // --------------------------------------------------------------------
        void DrawHeaderHelp()
        {
            EditorGUILayout.HelpBox(
                "Add only what your scene needs. Features are optional; the manager works fine with none.",
                MessageType.Info);
        }

        // --------------------------------------------------------------------
        // Features: Scenario
        // --------------------------------------------------------------------
        void DrawScenarioFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scenario", TitleStyle);

                bool hasScenario = _scenarioProp != null && _scenarioProp.objectReferenceValue != null;

                MiniCaption("Scenario");
                ObjectFieldWithPingClear(serializedObject, _scenarioProp, undoName: "Assign Scenario", simpleTypeName: "Scenario", ns: "Pitech.XR.Scenario");

                if (!hasScenario)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Scenario", GUILayout.Height(22)))
                        CreateAndAssignScenario();
                }
            }
        }




        // --------------------------------------------------------------------
        // Features: Stats (big buttons, like Scenario)
        // --------------------------------------------------------------------
        void DrawStatsFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stats", TitleStyle);

                bool hasUI = _statsUIProp != null && _statsUIProp.objectReferenceValue != null;
                bool hasConfig = _statsConfigProp != null && _statsConfigProp.objectReferenceValue != null;

                // UI Controller row
                MiniCaption("UI Controller");
                ObjectFieldWithPingClear(serializedObject, _statsUIProp, undoName: "Assign Stats UI", simpleTypeName: "StatsUIController", ns: "Pitech.XR.Stats");
                if (!hasUI)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign StatsUIController", GUILayout.Height(22)))
                        CreateAndAssignStatsUI();
                }

                // Config row
                MiniCaption("Config");
                ObjectFieldWithPingClear(serializedObject, _statsConfigProp, undoName: "Assign Stats Config", simpleTypeName: "StatsConfig", ns: "Pitech.XR.Stats");
                if (!hasConfig)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign StatsConfig asset", GUILayout.Height(22)))
                        CreateAndAssignStatsConfig();
                }
            }
        }

        // --------------------------------------------------------------------
        // Features: Parameters (the typed param store - Stats successor; bool params double as lab states)
        // --------------------------------------------------------------------
        void DrawParametersFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Parameters", TitleStyle);
                if (_parametersProp == null)
                {
                    EditorGUILayout.HelpBox("LabConsole.parameters not found. Recompile scripts and reopen the inspector.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.PropertyField(_parametersProp,
                    new GUIContent("Parameters",
                        "Typed parameters for this lab (the Stats successor). They seed the runtime store; bool params double as lab states for triggers/listeners."),
                    true);

                if (Application.isPlaying) DrawLiveParamValues();
                else EditorGUILayout.HelpBox("Enter Play mode to see each parameter's live runtime value.", MessageType.None);
            }
        }

        void DrawLiveParamValues()
        {
            var console = (Pitech.XR.Scenario.LabConsole)target;
            Pitech.XR.Core.IParamStore store = console.Params;   // internal; visible via InternalsVisibleTo
            if (store == null) return;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Live values (runtime)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
            {
                if (_parametersProp.arraySize == 0)
                    EditorGUILayout.LabelField("(no parameters declared)");

                for (int i = 0; i < _parametersProp.arraySize; i++)
                {
                    SerializedProperty idProp = _parametersProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                    string id = idProp != null ? idProp.stringValue : null;
                    if (string.IsNullOrEmpty(id)) continue;
                    string shown = store.TryGet(id, out Pitech.XR.Core.ParamValue v) ? FormatParamValue(v) : "(unset)";
                    EditorGUILayout.LabelField(id, shown);
                }
            }
            Repaint();   // keep the live values refreshing while playing
        }

        static string FormatParamValue(in Pitech.XR.Core.ParamValue v)
        {
            switch (v.Type)
            {
                case Pitech.XR.Core.ParamType.Bool: return v.AsBool() ? "true" : "false";
                case Pitech.XR.Core.ParamType.Int: return v.AsInt().ToString();
                case Pitech.XR.Core.ParamType.Float: return v.AsFloat().ToString("0.###");
                case Pitech.XR.Core.ParamType.Enum: return "enum " + v.AsInt();
                case Pitech.XR.Core.ParamType.String: return "\"" + v.AsString() + "\"";
                default: return v.AsString();
            }
        }

        void DrawInteractablesFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Interactables", TitleStyle);

                bool hasSelMgr = _selectablesProp != null && _selectablesProp.objectReferenceValue != null;
                bool hasSelList = _selectionListsProp != null && _selectionListsProp.objectReferenceValue != null;

                // Selectables Manager row
                MiniCaption("Selectables Manager");
                ObjectFieldWithPingClear(serializedObject, _selectablesProp, undoName: "Assign Selectables Manager", simpleTypeName: "SelectablesManager", ns: "Pitech.XR.Interactables");
                if (!hasSelMgr)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Selectables Manager", GUILayout.Height(22)))
                        CreateAndAssignSelectablesManager();
                }

                // Selection Lists row
                MiniCaption("Selection Lists");
                ObjectFieldWithPingClear(serializedObject, _selectionListsProp, undoName: "Assign Selection Lists", simpleTypeName: "SelectionLists", ns: "Pitech.XR.Interactables");
                if (!hasSelList)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign Selection Lists", GUILayout.Height(22)))
                        CreateAndAssignSelectionLists();
                }
            }
        }

        void DrawQuizFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Quiz", TitleStyle);

                bool hasQuiz = _defaultQuizProp != null && _defaultQuizProp.objectReferenceValue != null;
                bool hasPanel = _quizPanelProp != null && _quizPanelProp.objectReferenceValue != null;
                bool hasResults = _quizResultsPanelProp != null && _quizResultsPanelProp.objectReferenceValue != null;

                MiniCaption("Quiz Asset");
                ObjectFieldWithPingClear(serializedObject, _defaultQuizProp, undoName: "Assign Default Quiz", simpleTypeName: "QuizAsset", ns: "Pitech.XR.Quiz");
                if (!hasQuiz)
                {
                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Create & assign QuizAsset", GUILayout.Height(22)))
                        CreateAndAssignQuizAsset();
                }

                MiniCaption("Quiz Panel");
                ObjectFieldWithPingClear(serializedObject, _quizPanelProp, undoName: "Assign Quiz Panel", simpleTypeName: "QuizUIController", ns: "Pitech.XR.Quiz");

                MiniCaption("Quiz Results Panel");
                ObjectFieldWithPingClear(serializedObject, _quizResultsPanelProp, undoName: "Assign Quiz Results Panel", simpleTypeName: "QuizResultsUIController", ns: "Pitech.XR.Quiz");

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Install Quiz UI + Wire", GUILayout.Height(22)))
                    new Pitech.XR.Core.Editor.QuizService().AddQuizToScene();

                if (!hasPanel || !hasResults)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(
                        "Tip: Use a CanvasGroup-based panel (recommended) so the UI can hide/show without disabling GameObjects.",
                        MessageType.Info);
                }
            }
        }

        void DrawContentDeliveryFeature()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Content Delivery", TitleStyle);

                if (_contentDeliveryProp == null)
                {
                    EditorGUILayout.HelpBox(
                        "LabConsole.contentDelivery property was not found. Recompile scripts and reopen inspector.",
                        MessageType.Warning);
                    return;
                }

                bool hasDelivery = _contentDeliveryProp.objectReferenceValue != null;

                MiniCaption("Delivery Object");
                ObjectFieldWithPingClear(
                    serializedObject,
                    _contentDeliveryProp,
                    undoName: "Assign Content Delivery",
                    simpleTypeName: "ContentDeliverySpawner",
                    ns: "Pitech.XR.ContentDelivery");

                EditorGUILayout.Space(2);
                if (!hasDelivery)
                {
                    if (GUILayout.Button("Create & assign Content Delivery Object", GUILayout.Height(22)))
                        CreateAndAssignContentDelivery();
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Content Delivery object is assigned. Configure spawn parent and addressable prefab on that component.",
                        MessageType.Info);
                }
            }
        }

        // --------------------------------------------------------------------
        // Features: Analytics (the lab's LabAnalytics recorder - a SIBLING "Analytics" object, not on this console).
        // LabConsole holds no serialized link to it (the recorder self-resolves the bus), so this is a RESOLVED
        // navigation link: find it anywhere under the lab root and offer Select/Ping, or an Add button if absent.
        // --------------------------------------------------------------------
        void DrawAnalyticsFeature(Pitech.XR.Scenario.LabConsole console)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Analytics", TitleStyle);

                Pitech.XR.Analytics.LabAnalytics la = ResolveLabAnalytics(console);

                MiniCaption("Lab Analytics recorder");
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(GUIContent.none, la, typeof(Pitech.XR.Analytics.LabAnalytics), true);

                if (la != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select", GUILayout.Height(20)))
                        {
                            Selection.activeObject = la.gameObject;
                            EditorGUIUtility.PingObject(la.gameObject);
                        }
                        if (GUILayout.Button("Ping", GUILayout.Height(20)))
                            EditorGUIUtility.PingObject(la.gameObject);
                    }
                    EditorGUILayout.HelpBox(
                        "Authored on a sibling \"Analytics\" object. Step analytics live on the step nodes in the " +
                        "Scenario Graph; scene-wide analytics + objectives are on the Analytics object.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox("No Lab Analytics recorder in this lab yet (the lab is ungraded).", MessageType.None);
                    if (GUILayout.Button("Add Lab Analytics", GUILayout.Height(22)))
                        CreateAndAssignAnalytics(console);
                }
            }
        }

        // Resolve the lab's LabAnalytics anywhere under the lab ROOT (so a sibling "Analytics" object resolves).
        static Pitech.XR.Analytics.LabAnalytics ResolveLabAnalytics(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return null;
            GameObject labRoot = console.transform.root.gameObject;
            return labRoot.GetComponentInChildren<Pitech.XR.Analytics.LabAnalytics>(true);
        }

        // Create the recorder on a SIBLING "Analytics" object (next to this console) + ensure a LabRuntimeContext on
        // the lab ROOT (the common ancestor, so the shared bus resolves by parent-walk). Mirrors the graph's flow.
        void CreateAndAssignAnalytics(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            Transform anchor = console.transform;
            GameObject labRoot = anchor.root.gameObject;

            if (labRoot.GetComponent<Pitech.XR.Core.LabRuntimeContext>() == null)
                Undo.AddComponent<Pitech.XR.Core.LabRuntimeContext>(labRoot);

            Transform parent = anchor.parent;   // sibling => same parent as the console
            GameObject go = null;
            if (parent != null)
            {
                Transform existing = parent.Find("Analytics");
                if (existing != null) go = existing.gameObject;
            }
            if (go == null)
            {
                go = new GameObject("Analytics");
                Undo.RegisterCreatedObjectUndo(go, "Create Analytics object");
                go.transform.SetParent(parent, false);   // null parent => a scene-root sibling
            }

            var la = go.GetComponent<Pitech.XR.Analytics.LabAnalytics>();
            if (la == null) la = Undo.AddComponent<Pitech.XR.Analytics.LabAnalytics>(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        void CreateAndAssignSelectablesManager()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var t = FindType("SelectablesManager", "Pitech.XR.Interactables");
            if (t == null) { EditorUtility.DisplayDialog("Interactables", "Type Pitech.XR.Interactables.SelectablesManager not found.", "OK"); return; }

            var go = new GameObject("Selectables Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Selectables Manager");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            AssignSceneObjectProperty(_selectablesProp, comp, "Assign Selectables Manager");
            Selection.activeObject = go;
        }

        void CreateAndAssignSelectionLists()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var t = FindType("SelectionLists", "Pitech.XR.Interactables");
            if (t == null) { EditorUtility.DisplayDialog("Interactables", "Type Pitech.XR.Interactables.SelectionLists not found.", "OK"); return; }

            var go = new GameObject("Selection Lists");
            Undo.RegisterCreatedObjectUndo(go, "Create Selection Lists");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            // If Lab Console already has a SelectablesManager, auto-link it
            var sm = (Pitech.XR.Scenario.LabConsole)target;
            var lists = comp as Pitech.XR.Interactables.SelectionLists;
            if (lists && sm.selectables) lists.selectables = sm.selectables;

            AssignSceneObjectProperty(_selectionListsProp, comp, "Assign Selection Lists");
            Selection.activeObject = go;
        }

        void CreateAndAssignQuizAsset()
        {
            new Pitech.XR.Core.Editor.QuizService().CreateAsset();
            var obj = Selection.activeObject;
            if (obj) AssignSceneObjectProperty(_defaultQuizProp, obj, "Assign Default Quiz");
        }

        void CreateAndAssignContentDelivery()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var deliveryType = FindType("ContentDeliverySpawner", "Pitech.XR.ContentDelivery");
            if (deliveryType == null)
            {
                EditorUtility.DisplayDialog(
                    "Content Delivery",
                    "Could not find Pitech.XR.ContentDelivery.ContentDeliverySpawner.\n\n" +
                    "Make sure the ContentDelivery module compiled successfully.",
                    "OK");
                return;
            }

            var go = new GameObject("Content Delivery");
            Undo.RegisterCreatedObjectUndo(go, "Create Content Delivery");
            go.transform.SetParent(parent, false);
            var comp = go.AddComponent(deliveryType) as Component;

            // Create a dedicated spawn root so users can choose where content appears.
            var contentRoot = new GameObject("Lab Content Root");
            Undo.RegisterCreatedObjectUndo(contentRoot, "Create Lab Content Root");
            contentRoot.transform.SetParent(go.transform, false);

            if (comp != null)
            {
                var so = new SerializedObject(comp);
                var sceneManagerProp = so.FindProperty("sceneManager");
                var spawnParentProp = so.FindProperty("spawnParent");
                if (sceneManagerProp != null)
                    sceneManagerProp.objectReferenceValue = target as MonoBehaviour;
                if (spawnParentProp != null)
                    spawnParentProp.objectReferenceValue = contentRoot.transform;

                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(comp);
            }

            AssignSceneObjectProperty(_contentDeliveryProp, comp, "Assign Content Delivery");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
        }

        // Deprecated manual creator kept only for older versions; install via QuizService for best UX.

        static Pitech.XR.Scenario.Scenario GetScenarioFromManager(Pitech.XR.Scenario.LabConsole gm)
        {
            return gm != null ? gm.scenario : null;
        }

        // --------------------------------------------------------------------
        // Overview (restored)
        // --------------------------------------------------------------------
        void DrawScenarioOverview(Pitech.XR.Scenario.LabConsole gm)
        {
            var sc = GetScenarioFromManager(gm);
            if (!sc) return;

            EditorGUILayout.LabelField("Scenario Overview", TitleStyle);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (sc.steps == null || sc.steps.Count == 0)
                {
                    EditorGUILayout.LabelField("No steps yet.");
                    return;
                }

                for (int i = 0; i < sc.steps.Count; i++)
                {
                    var s = sc.steps[i];
                    if (s == null)
                    {
                        EditorGUILayout.LabelField($"{i:00}. <null>");
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (s is Pitech.XR.Scenario.TimelineStep tl)
                        {
                            var ok = tl.director ? "✓" : "✗";
                            EditorGUILayout.LabelField($"{i:00}. Timeline {ok}", GUILayout.Width(170));
                            EditorGUILayout.ObjectField(tl.director, typeof(PlayableDirector), true);
                            if (!tl.director) EditorGUILayout.HelpBox("Director not set", MessageType.Warning);
                        }
                        else if (s is Pitech.XR.Scenario.CueCardsStep cc)
                        {
                            var times = cc.cueTimes != null ? cc.cueTimes.Length : 0;
                            EditorGUILayout.LabelField($"{i:00}. Cue Cards", GUILayout.Width(170));
                            EditorGUILayout.LabelField(times == 0 ? "tap-only" : $"{times} cue time(s)");
                        }
                        else if (s is Pitech.XR.Scenario.QuestionStep q)
                        {
                            int btns = q.choices?.Count ?? 0;
                            EditorGUILayout.LabelField($"{i:00}. Question", GUILayout.Width(170));
                            EditorGUILayout.LabelField($"Buttons {btns}");
                        }
                        else if (s is Pitech.XR.Scenario.SelectionStep sel)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Selection", GUILayout.Width(170));
                            var mode = sel.completion.ToString();
                            EditorGUILayout.LabelField($"{mode} / Required {sel.requiredSelections}");
                        }
                        else if (s is Pitech.XR.Scenario.InsertStep ins)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Insert", GUILayout.Width(170));
                            string itemName = ins.item ? ins.item.name : "no item";
                            string targetName = ins.targetTrigger ? ins.targetTrigger.name : "no target";
                            EditorGUILayout.LabelField($"{itemName} → {targetName}");
                        }
                        else if (s is Pitech.XR.Scenario.EventStep ev)
                        {
                            EditorGUILayout.LabelField($"{i:00}. Event", GUILayout.Width(170));
                            string waitTxt = ev.waitSeconds > 0f
                                ? $"wait {ev.waitSeconds:0.##}s then next"
                                : "no wait, immediate next";
                            EditorGUILayout.LabelField(waitTxt);
                        }
                        else
                        {
                            // Fallback for any future step type
                            EditorGUILayout.LabelField($"{i:00}. {s.GetType().Name}");
                        }
                    }
                }
            }
        }


        // --------------------------------------------------------------------
        // Runtime (restored)
        // --------------------------------------------------------------------
        void DrawRuntimeControls(Pitech.XR.Scenario.LabConsole gm)
        {
            EditorGUILayout.LabelField("Runtime", TitleStyle);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var sc = GetScenarioFromManager(gm);
                int totalSteps = (sc != null && sc.steps != null) ? sc.steps.Count : 0;

                // Current index
                int currentIndex = gm.StepIndex;

                // Status line
                string status;
                if (!Application.isPlaying)
                    status = "Editor idle (enter Play mode)";
                else if (currentIndex < 0 || totalSteps == 0)
                    status = "Idle / finished";
                else
                    status = $"Step {currentIndex + 1} of {totalSteps}";

                EditorGUILayout.LabelField(status);

                // Progress bar (only when there are valid steps)
                if (totalSteps > 0)
                {
                    float progress = 0f;
                    if (currentIndex >= 0 && currentIndex < totalSteps)
                        progress = (currentIndex + 1) / (float)totalSteps;

                    var rect = GUILayoutUtility.GetRect(18, 18);
                    EditorGUI.ProgressBar(rect, progress, $"{Mathf.RoundToInt(progress * 100f)}%");
                }

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Restart Scenario"))
                        gm.Restart();

                    if (!Application.isPlaying)
                        EditorGUILayout.HelpBox("Enter Play mode to see live progress and restart.", MessageType.None);
                }
            }
        }


        // --------------------------------------------------------------------
        // Creation helpers (Undo-friendly + robust property set)
        // --------------------------------------------------------------------
        static Transform EnsureManagersRoot()
        {
            var s = USceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded)
            {
                EditorUtility.DisplayDialog("No Scene", "Open a scene first.", "OK");
                return null;
            }

            var root = s.GetRootGameObjects().FirstOrDefault(g => g.name == ManagersRootName || g.name == LegacyManagersRootName);
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        static Type FindType(string simpleName, string @namespace = null)
        {
            string key = $"{@namespace}.{simpleName}";
            if (_typeCache.TryGetValue(key, out var cached)) return cached;

            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(x => x.Name == simpleName && (@namespace == null || x.Namespace == @namespace));
            _typeCache[key] = t;
            return t;
        }

        void CreateAndAssignScenario()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var scenarioType = FindType("Scenario", "Pitech.XR.Scenario");
            if (scenarioType == null)
            {
                EditorUtility.DisplayDialog("Scenario not found",
                    "Could not find Pitech.XR.Scenario.Scenario.", "OK");
                return;
            }

            var go = new GameObject("Scenario");
            Undo.RegisterCreatedObjectUndo(go, "Create Scenario");
            var comp = go.AddComponent(scenarioType) as Component;
            go.transform.SetParent(parent, false);

            AssignSceneObjectProperty(_scenarioProp, comp, "Assign Scenario");

            Selection.activeObject = go;
        }

        void CreateAndAssignStatsUI()
        {
            var parent = EnsureManagersRoot();
            if (!parent) return;

            var uiType = FindType("StatsUIController", "Pitech.XR.Stats");
            if (uiType == null)
            {
                EditorUtility.DisplayDialog("Stats UI not found",
                    "Could not find Pitech.XR.Stats.StatsUIController.", "OK");
                return;
            }

            var go = new GameObject("StatsUIController");
            Undo.RegisterCreatedObjectUndo(go, "Create StatsUIController");
            var comp = go.AddComponent(uiType) as Component;
            go.transform.SetParent(parent, false);

            AssignSceneObjectProperty(_statsUIProp, comp, "Assign Stats UI");

            Selection.activeObject = go;
        }

        void CreateAndAssignStatsConfig()
        {
            const string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/StatsConfig.asset");

            var configType = FindType("StatsConfig", "Pitech.XR.Stats");
            if (configType == null)
            {
                EditorUtility.DisplayDialog("Stats Config not found",
                    "Could not find Pitech.XR.Stats.StatsConfig.", "OK");
                return;
            }

            var asset = ScriptableObject.CreateInstance(configType);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignSceneObjectProperty(_statsConfigProp, asset, "Assign Stats Config");
            EditorGUIUtility.PingObject(asset);
        }

        // Tiny caption above a row (e.g., "UI Controller", "Config").
        static void MiniCaption(string text)
        {
            var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            r.y += 2f;
            EditorGUI.LabelField(r, text, EditorStyles.miniLabel);
        }

        // Object field with right-aligned Ping / Clear. No label.
        // Object field with right-aligned Ping / Clear. No label ever.
        static void ObjectFieldWithPingClear(
    SerializedObject owner,
    SerializedProperty prop,
    string undoName,
    string simpleTypeName = null,
    string ns = null)
        {
            const float clearW = 54f;
            const float pingW = 50f;
            const float pad = 4f;

            var line = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var clear = new Rect(line.xMax - clearW, line.y, clearW, line.height);
            var ping = new Rect(clear.x - pad - pingW, line.y, pingW, line.height);
            var field = new Rect(line.x, line.y, ping.x - pad - line.x, line.height);

            // Try to resolve the type by name; if missing package, use Object so we still compile.
            Type objectType = typeof(UnityEngine.Object);
            if (!string.IsNullOrEmpty(simpleTypeName))
            {
                var t = FindType(simpleTypeName, ns);
                if (t != null) objectType = t;
            }

            var undoTarget = owner?.targetObject;
            EditorGUI.BeginChangeCheck();
            var newObj = EditorGUI.ObjectField(field, GUIContent.none, prop.objectReferenceValue, objectType, true);
            if (EditorGUI.EndChangeCheck())
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, undoName);
                prop.objectReferenceValue = newObj;
                owner.ApplyModifiedProperties();

                if (undoTarget != null)
                {
                    EditorUtility.SetDirty(undoTarget);
                    if (undoTarget is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                }
            }

            using (new EditorGUI.DisabledScope(prop.objectReferenceValue == null))
                if (GUI.Button(ping, "Ping")) EditorGUIUtility.PingObject(prop.objectReferenceValue);

            if (GUI.Button(clear, "Clear"))
            {
                if (undoTarget != null) Undo.RecordObject(undoTarget, undoName);
                prop.objectReferenceValue = null;
                owner.ApplyModifiedProperties();

                if (undoTarget != null)
                {
                    EditorUtility.SetDirty(undoTarget);
                    if (undoTarget is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                }
            }
        }

        void AssignSceneObjectProperty(SerializedProperty prop, UnityEngine.Object value, string undoName)
        {
            if (prop == null) return;

            Undo.RecordObject(target, undoName);
            prop.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (target is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }



    }
}
#endif
