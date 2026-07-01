#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// The (now thin) LabConsole inspector. Per the DevKit surface-separation schema (section 7) the rich
    /// per-lab authoring moved to the <see cref="LabConsoleWindow"/> - the Timeline/PlayableDirector
    /// precedent: the inspector is a SUMMARY + an "Open Lab Console" entry point, both VIEWS over the same
    /// data (no parallel store). What stays here:
    ///   - identity + feature-presence at a glance + top setup issues,
    ///   - the entry buttons (Lab Console window, Scenario Graph) and a play-mode run glance,
    ///   - Content Delivery (kept in the inspector for now - Stergios, 2026-06-30 - not yet relocated),
    ///   - a collapsed "Raw fields" escape hatch for power users.
    /// Feature create/bind logic lives in <see cref="LabConsoleAuthoring"/> so the window and this inspector
    /// share one code path.
    /// </summary>
    [CustomEditor(typeof(Pitech.XR.Scenario.LabConsole), true)]
    public class SceneManagerEditor : UnityEditor.Editor
    {
        SerializedProperty _contentDeliveryProp; // Pitech.XR.ContentDelivery.ContentDeliverySpawner (as MonoBehaviour)
        bool _showRaw;

        static GUIStyle TitleStyle => EditorStyles.boldLabel;

        void OnEnable()
        {
            _contentDeliveryProp = serializedObject.FindProperty("contentDelivery");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var c = (Pitech.XR.Scenario.LabConsole)target;

            DrawSummary(c);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open Lab Console", GUILayout.Height(28)))
                LabConsoleWindow.Open(c);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Scenario Graph", GUILayout.Height(20)))
                    ScenarioGraphWindow.OpenWindow();
            }

            DrawRuntimeGlance(c);

            // Content Delivery: intentionally retained in the inspector for now (not yet relocated to the
            // Lab Console window's Delivery page).
            EditorGUILayout.Space(8);
            DrawContentDeliveryFeature();

            // Power-user escape hatch: the full serialized surface, collapsed by default.
            EditorGUILayout.Space(8);
            _showRaw = EditorGUILayout.Foldout(_showRaw, "Raw fields (advanced)", true);
            if (_showRaw)
                DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }

        // --------------------------------------------------------------------
        // Summary
        // --------------------------------------------------------------------
        void DrawSummary(Pitech.XR.Scenario.LabConsole c)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Lab Console", TitleStyle);

                int steps = (c != null && c.scenario != null && c.scenario.steps != null) ? c.scenario.steps.Count : 0;
                EditorGUILayout.LabelField("Scenario", c != null && c.scenario != null ? (c.scenario.name + "  (" + steps + " steps)") : "(none)");

                EditorGUILayout.LabelField("Features", PresentList(c));

                if (c == null || c.scenario == null)
                    EditorGUILayout.HelpBox("No Scenario assigned. Open the Lab Console to set up the lab flow.", MessageType.Info);
            }
        }

        static string PresentList(Pitech.XR.Scenario.LabConsole c)
        {
            if (c == null) return "(none)";
            string s = "";
            Append(ref s, "Flow", LabConsoleAuthoring.HasScenario(c));
            Append(ref s, "Parameters", LabConsoleAuthoring.HasParameters(c));
            Append(ref s, "Content", LabConsoleAuthoring.HasContent(c));
            Append(ref s, "Analytics", LabConsoleAuthoring.HasAnalytics(c));
            Append(ref s, "Roles", LabConsoleAuthoring.HasRoles(c));
            Append(ref s, "Vitals", LabConsoleAuthoring.HasVitals(c));
            Append(ref s, "Delivery", LabConsoleAuthoring.HasDelivery(c));
            return string.IsNullOrEmpty(s) ? "(none yet)" : s;
        }

        static void Append(ref string s, string name, bool present)
        {
            if (!present) return;
            if (s.Length > 0) s += ", ";
            s += name;
        }

        void DrawRuntimeGlance(Pitech.XR.Scenario.LabConsole c)
        {
            if (!Application.isPlaying || c == null) return;

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int total = (c.scenario != null && c.scenario.steps != null) ? c.scenario.steps.Count : 0;
                int idx = c.StepIndex;
                string status = (idx < 0 || total == 0) ? "Idle / finished" : ("Step " + (idx + 1) + " of " + total);
                EditorGUILayout.LabelField("Runtime", status);
                if (GUILayout.Button("Restart Scenario"))
                    c.Restart();
            }
        }

        // --------------------------------------------------------------------
        // Content Delivery (retained from the legacy inspector)
        // --------------------------------------------------------------------
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

        void CreateAndAssignContentDelivery()
        {
            var parent = LabConsoleAuthoring.EnsureManagersRoot();
            if (!parent) return;

            var deliveryType = LabConsoleAuthoring.FindType("ContentDeliverySpawner", "Pitech.XR.ContentDelivery");
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

        // --------------------------------------------------------------------
        // Shared IMGUI helpers (used by the retained Content Delivery section)
        // --------------------------------------------------------------------
        static void MiniCaption(string text)
        {
            var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            r.y += 2f;
            EditorGUI.LabelField(r, text, EditorStyles.miniLabel);
        }

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

            // Resolve the type by name; if the package is missing, fall back to Object so this still compiles.
            Type objectType = typeof(UnityEngine.Object);
            if (!string.IsNullOrEmpty(simpleTypeName))
            {
                var t = LabConsoleAuthoring.FindType(simpleTypeName, ns);
                if (t != null) objectType = t;
            }

            var undoTarget = owner != null ? owner.targetObject : null;
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
