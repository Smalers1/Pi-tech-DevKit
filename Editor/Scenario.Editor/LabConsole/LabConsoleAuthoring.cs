#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using USceneManager = UnityEngine.SceneManagement.SceneManager; // avoid name clash

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Shared authoring backbone for a LabConsole lab: feature-presence detection + the create-and-assign
    /// operations, factored out of <see cref="SceneManagerEditor"/> so the Lab Console window and the (thin)
    /// inspector are TWO VIEWS over ONE code path - the "unify the backing service" rule from the DevKit
    /// surface-separation schema (_workbench/devkit/2026-06-30, section 7). Nothing here draws UI; every
    /// mutation is Undo-friendly and marks the owning scene dirty.
    ///
    /// Presence uses lab-ROOT component lookups (so a sibling "Analytics" object, a "Session Roles" object,
    /// etc. resolve from the console). Optional packages that this editor assembly does NOT reference
    /// (Vitals) are reached by reflection - the same decoupling LabConsole.contentDelivery already uses -
    /// so no asmdef edit is needed and the tool degrades gracefully if the package is absent.
    /// </summary>
    internal static class LabConsoleAuthoring
    {
        const string ManagersRootName = "--- SCENE SETUP ---";          // canonical (2026-06-30 rename)
        const string LegacyManagersRootName = "--- SCENE MANAGERS ---"; // pre-rename name, still resolved

        // ---------- type resolution (reflection; tolerates an optional package being absent) ----------
        static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        /// <summary>Resolve a type by simple name (+ optional namespace) across all loaded assemblies, cached.
        /// Returns null if the type is not present (e.g. an optional module did not compile / is not installed).</summary>
        internal static Type FindType(string simpleName, string @namespace = null)
        {
            string key = (@namespace ?? string.Empty) + "." + simpleName;
            if (_typeCache.TryGetValue(key, out var cached)) return cached;

            Type t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(x => x.Name == simpleName && (@namespace == null || x.Namespace == @namespace));
            _typeCache[key] = t;
            return t;
        }

        static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly a)
        {
            try { return a.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        /// <summary>Find or create the scene-root container that spawned managers live under. Null (with a
        /// dialog) if there is no open, loaded scene.</summary>
        internal static Transform EnsureManagersRoot()
        {
            var s = USceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded)
            {
                EditorUtility.DisplayDialog("No Scene", "Open a scene first.", "OK");
                return null;
            }

            var root = s.GetRootGameObjects()
                .FirstOrDefault(g => g.name == ManagersRootName || g.name == LegacyManagersRootName);
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        // ---------- presence (one source of truth for nav adaptivity AND page content) ----------
        internal static bool HasScenario(Pitech.XR.Scenario.LabConsole c) => c != null && c.scenario != null;
        internal static bool HasStats(Pitech.XR.Scenario.LabConsole c) => c != null && (c.statsUI != null || c.statsConfig != null);
        internal static bool HasParameters(Pitech.XR.Scenario.LabConsole c) => ParametersCount(c) > 0;
        internal static bool HasQuiz(Pitech.XR.Scenario.LabConsole c) => c != null && (c.defaultQuiz != null || c.quizPanel != null || c.quizResultsPanel != null);
        internal static bool HasInteractables(Pitech.XR.Scenario.LabConsole c) => c != null && (c.selectables != null || c.selectionLists != null);
        internal static bool HasContent(Pitech.XR.Scenario.LabConsole c) => HasQuiz(c) || HasInteractables(c) || HasStats(c);
        internal static bool HasDelivery(Pitech.XR.Scenario.LabConsole c) => c != null && c.contentDelivery != null;

        /// <summary>Count of declared typed parameters. <c>parameters</c> is a private [SerializeField] list, so
        /// it is read through a throwaway SerializedObject (no reflection on the field, survives renames via the
        /// serialized name).</summary>
        internal static int ParametersCount(Pitech.XR.Scenario.LabConsole c)
        {
            if (c == null) return 0;
            var so = new SerializedObject(c);
            var p = so.FindProperty("parameters");
            return p != null && p.isArray ? p.arraySize : 0;
        }

        /// <summary>The lab's LabAnalytics recorder anywhere under the lab ROOT (so a sibling "Analytics" object
        /// resolves the same as a co-located/legacy child). Null if the lab is ungraded.</summary>
        internal static Pitech.XR.Analytics.LabAnalytics ResolveAnalytics(Pitech.XR.Scenario.LabConsole c)
        {
            if (c == null) return null;
            return c.transform.root.gameObject.GetComponentInChildren<Pitech.XR.Analytics.LabAnalytics>(true);
        }
        internal static bool HasAnalytics(Pitech.XR.Scenario.LabConsole c) => ResolveAnalytics(c) != null;

        /// <summary>The lab's in-scene role selector anywhere under the lab ROOT. Null if roles are not set up.</summary>
        internal static Pitech.XR.Analytics.SessionRoleSelector ResolveRoles(Pitech.XR.Scenario.LabConsole c)
        {
            if (c == null) return null;
            return c.transform.root.gameObject.GetComponentInChildren<Pitech.XR.Analytics.SessionRoleSelector>(true);
        }
        internal static bool HasRoles(Pitech.XR.Scenario.LabConsole c) => ResolveRoles(c) != null;

        /// <summary>The lab's PatientVitals component (Pitech.XR.Vitals - NOT referenced by this assembly, so
        /// resolved by reflected type + the non-generic GetComponentInChildren). Null if vitals are absent or the
        /// Vitals module is not installed.</summary>
        internal static Component ResolveVitals(Pitech.XR.Scenario.LabConsole c)
        {
            if (c == null) return null;
            Type t = FindType("PatientVitals", "Pitech.XR.Vitals");
            if (t == null) return null;
            return c.transform.root.gameObject.GetComponentInChildren(t, true);
        }
        internal static bool HasVitals(Pitech.XR.Scenario.LabConsole c) => ResolveVitals(c) != null;

        // ---------- create-and-assign (Undo-friendly; mirrors the legacy inspector creators) ----------
        internal static void CreateScenario(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var parent = EnsureManagersRoot(); if (!parent) return;

            var t = FindType("Scenario", "Pitech.XR.Scenario");
            if (t == null) { Missing("Scenario", "Pitech.XR.Scenario.Scenario"); return; }

            var go = new GameObject("Scenario");
            Undo.RegisterCreatedObjectUndo(go, "Create Scenario");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            Undo.RecordObject(console, "Assign Scenario");
            console.scenario = comp as Pitech.XR.Scenario.Scenario;
            MarkDirty(console);
            Selection.activeObject = go;
        }

        internal static void CreateStatsUI(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var parent = EnsureManagersRoot(); if (!parent) return;

            var t = FindType("StatsUIController", "Pitech.XR.Stats");
            if (t == null) { Missing("Stats UI", "Pitech.XR.Stats.StatsUIController"); return; }

            var go = new GameObject("StatsUIController");
            Undo.RegisterCreatedObjectUndo(go, "Create StatsUIController");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            Undo.RecordObject(console, "Assign Stats UI");
            console.statsUI = comp as Pitech.XR.Stats.StatsUIController;
            MarkDirty(console);
            Selection.activeObject = go;
        }

        internal static void CreateStatsConfig(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;

            const string folder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var t = FindType("StatsConfig", "Pitech.XR.Stats");
            if (t == null) { Missing("Stats Config", "Pitech.XR.Stats.StatsConfig"); return; }

            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/StatsConfig.asset");
            var asset = ScriptableObject.CreateInstance(t);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(console, "Assign Stats Config");
            console.statsConfig = asset as Pitech.XR.Stats.StatsConfig;
            MarkDirty(console);
            EditorGUIUtility.PingObject(asset);
        }

        internal static void CreateSelectablesManager(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var parent = EnsureManagersRoot(); if (!parent) return;

            var t = FindType("SelectablesManager", "Pitech.XR.Interactables");
            if (t == null) { Missing("Interactables", "Pitech.XR.Interactables.SelectablesManager"); return; }

            var go = new GameObject("Selectables Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Selectables Manager");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            Undo.RecordObject(console, "Assign Selectables Manager");
            console.selectables = comp as Pitech.XR.Interactables.SelectablesManager;
            MarkDirty(console);
            Selection.activeObject = go;
        }

        internal static void CreateSelectionLists(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var parent = EnsureManagersRoot(); if (!parent) return;

            var t = FindType("SelectionLists", "Pitech.XR.Interactables");
            if (t == null) { Missing("Interactables", "Pitech.XR.Interactables.SelectionLists"); return; }

            var go = new GameObject("Selection Lists");
            Undo.RegisterCreatedObjectUndo(go, "Create Selection Lists");
            var comp = go.AddComponent(t) as Component;
            go.transform.SetParent(parent, false);

            // Auto-link an existing catalog so the new lists controller is usable immediately.
            var lists = comp as Pitech.XR.Interactables.SelectionLists;
            if (lists && console.selectables) lists.selectables = console.selectables;

            Undo.RecordObject(console, "Assign Selection Lists");
            console.selectionLists = lists;
            MarkDirty(console);
            Selection.activeObject = go;
        }

        internal static void CreateQuizAsset(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            new Pitech.XR.Core.Editor.QuizService().CreateAsset();
            var obj = Selection.activeObject as Pitech.XR.Quiz.QuizAsset;
            if (obj != null)
            {
                Undo.RecordObject(console, "Assign Default Quiz");
                console.defaultQuiz = obj;
                MarkDirty(console);
            }
        }

        /// <summary>Install the quiz UI prefab and wire its panels (delegates to the shared QuizService - the
        /// same path the legacy inspector used).</summary>
        internal static void InstallQuizUI() => new Pitech.XR.Core.Editor.QuizService().AddQuizToScene();

        /// <summary>Create the recorder on a SIBLING "Analytics" object (next to the console) + ensure a
        /// LabRuntimeContext on the lab ROOT (the common ancestor, so the shared bus resolves by parent-walk).
        /// Mirrors the Scenario Graph's flow exactly so graph- and console-created recorders are identical.</summary>
        internal static void AddAnalytics(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            Transform anchor = console.transform;
            GameObject labRoot = anchor.root.gameObject;

            if (labRoot.GetComponent<Pitech.XR.Core.LabRuntimeContext>() == null)
                Undo.AddComponent<Pitech.XR.Core.LabRuntimeContext>(labRoot);

            Transform parent = anchor.parent; // sibling => same parent as the console
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
                go.transform.SetParent(parent, false); // null parent => a scene-root sibling
            }

            if (go.GetComponent<Pitech.XR.Analytics.LabAnalytics>() == null)
                Undo.AddComponent<Pitech.XR.Analytics.LabAnalytics>(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        /// <summary>Create the in-scene role selector on a dedicated "Session Roles" object under the managers
        /// root. The pick UI itself is author-built (it wires buttons to the selector's Select* methods).</summary>
        internal static void AddRoles(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var parent = EnsureManagersRoot(); if (!parent) return;

            var go = new GameObject("Session Roles");
            Undo.RegisterCreatedObjectUndo(go, "Create Session Roles");
            go.transform.SetParent(parent, false);
            Undo.AddComponent<Pitech.XR.Analytics.SessionRoleSelector>(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
        }

        /// <summary>Create a PatientVitals component on a dedicated "Patient Vitals" object under the managers
        /// root (reflected type, since this assembly does not reference Pitech.XR.Vitals).</summary>
        internal static void AddVitals(Pitech.XR.Scenario.LabConsole console)
        {
            if (console == null) return;
            var t = FindType("PatientVitals", "Pitech.XR.Vitals");
            if (t == null) { Missing("Vitals", "Pitech.XR.Vitals.PatientVitals"); return; }

            var parent = EnsureManagersRoot(); if (!parent) return;

            var go = new GameObject("Patient Vitals");
            Undo.RegisterCreatedObjectUndo(go, "Create Patient Vitals");
            go.transform.SetParent(parent, false);
            Undo.AddComponent(go, t);

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
        }

        // ---------- small shared utilities ----------
        static void MarkDirty(Pitech.XR.Scenario.LabConsole console)
        {
            EditorUtility.SetDirty(console);
            if (console != null) EditorSceneManager.MarkSceneDirty(console.gameObject.scene);
        }

        static void Missing(string feature, string fullTypeName)
        {
            EditorUtility.DisplayDialog(
                feature,
                "Could not find " + fullTypeName + ".\n\nMake sure that module compiled successfully.",
                "OK");
        }
    }
}
#endif
