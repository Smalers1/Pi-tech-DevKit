#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pitech.XR.Core.Editor
{
    internal sealed class ScenarioService
    {
        const string ManagersRootName = SceneRootNames.ManagersRoot;   // canonical name for CREATE

        static Transform EnsureManagersRoot()
        {
            var s = SceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded) return null;
            var root = s.GetRootGameObjects().FirstOrDefault(g => SceneRootNames.IsManagersRoot(g.name));
            if (!root)
            {
                root = new GameObject(ManagersRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create scene root");
                EditorSceneManager.MarkSceneDirty(s);
            }
            return root.transform;
        }

        // Verb-named command (WS A2 Step 5 surface-type discipline: verbs for commands).
        // Adds a Scenario GameObject under the managers root and selects it. Thin alias over
        // CreateScenarioGameObject - behaviour identical, no duplication.
        public void AddScenarioToScene() => CreateScenarioGameObject();

        public void CreateScenarioGameObject()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(a => a.GetTypes())
                     .FirstOrDefault(x => x.FullName == "Pitech.XR.Scenario.Scenario" ||
                                          x.FullName == "Pitech.XR.ScenarioKit.Scenario" ||
                                          x.Name == "Scenario");
            if (t == null) { EditorUtility.DisplayDialog("Scenario", "Scenario component not found.", "OK"); return; }

            var parent = EnsureManagersRoot();
            var go = new GameObject("Scenario");
            Undo.RegisterCreatedObjectUndo(go, "Create Scenario");
            var scenarioComponent = go.AddComponent(t);
            if (parent) go.transform.SetParent(parent, false);

            // Mirror the inspector flow (SceneManagerEditor.CreateAndAssignScenario): if a LabConsole
            // exists in the scene, wire the new Scenario onto its 'scenario' field so the lab is ready
            // to run. Resolve LabConsole by FullName via reflection so Core.Editor keeps no hard
            // compile reference to the Pitech.XR.Scenario assembly; assign through SerializedObject for
            // free Undo + prefab-override correctness. If no LabConsole exists, fall back to today's
            // behaviour (an orphan Scenario) - never throw.
            var labConsole = FindFirstComponentInActiveScene("Pitech.XR.Scenario.LabConsole");
            if (labConsole)
            {
                var so = new SerializedObject(labConsole);
                var prop = so.FindProperty("scenario");
                if (prop != null)
                {
                    Undo.RecordObject(labConsole, "Assign Scenario");
                    so.Update();
                    prop.objectReferenceValue = scenarioComponent;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(labConsole);
                }
            }

            Selection.activeGameObject = go;
        }

        // Resolves the first component of the given type (by reflection FullName) in the active scene.
        // Kept self-contained and string-typed so Core.Editor takes no compile reference to the
        // Pitech.XR.Scenario assembly. Returns null if the type is absent or no instance is in-scene.
        static Component FindFirstComponentInActiveScene(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return null;

            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(x => x.FullName == fullTypeName);
            if (type == null) return null;

            var s = SceneManager.GetActiveScene();
            if (!s.IsValid() || !s.isLoaded) return null;

            foreach (var root in s.GetRootGameObjects())
            {
                var comp = root.GetComponentInChildren(type, true);
                if (comp) return comp;
            }
            return null;
        }

        public void OpenGraph()
        {
            var winType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "ScenarioGraphWindow" && typeof(EditorWindow).IsAssignableFrom(t));
            if (winType == null) { EditorUtility.DisplayDialog("Scenario Graph", "Window not found.", "OK"); return; }
            var w = EditorWindow.GetWindow(winType); w.titleContent = new GUIContent("Scenario Graph"); (w as EditorWindow).Show();
        }
    }
}
#endif
