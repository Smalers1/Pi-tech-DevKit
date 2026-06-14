#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Tiny manager for the curated <see cref="TestScenesList"/> that drives
    /// <see cref="ExportAllTestScenes"/>. Add the open scene or pick one, remove entries, re-seed from the
    /// scenes matching your committed fixtures, or run the batch from here. IMGUI on purpose - no theme
    /// dependency, so the window adds no assembly reference. The list is per-project, per-user, and not
    /// source-controlled; the committed fixtures+baselines+deps remain the shared truth.
    /// </summary>
    internal sealed class TestScenesListWindow : EditorWindow
    {
        Vector2 _scroll;
        SceneAsset _toAdd;

        [MenuItem("Pi tech/Tools/Manage Test Scenes List", false, 26)]
        internal static void Open()
        {
            var w = GetWindow<TestScenesListWindow>();
            w.titleContent = new GUIContent("Test Scenes List");
            w.minSize = new Vector2(440, 340);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Test scenes (batch-exported by 'Export All Test Scenes')",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Per-project, per-user list (not source-controlled). Auto-seeded from the scenes whose name "
                + "matches a committed lab fixture. The committed fixtures + baselines + deps are the shared "
                + "truth - this is only which scenes the batch re-exports.", MessageType.None);

            var entries = TestScenesList.Entries();

            EditorGUILayout.Space();
            if (entries.Count == 0)
                EditorGUILayout.LabelField("(empty - add a scene below, or Reset to detected labs)",
                    EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120));
            string removeGuid = null;
            foreach (var e in entries)
            {
                EditorGUILayout.BeginHorizontal();
                var prev = GUI.color;
                if (!e.resolved) GUI.color = new Color(0.95f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(e.display);
                GUI.color = prev;
                using (new EditorGUI.DisabledScope(!e.resolved))
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(e.path));
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    removeGuid = e.guid;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (removeGuid != null) { TestScenesList.Remove(removeGuid); Repaint(); }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _toAdd = (SceneAsset)EditorGUILayout.ObjectField(_toAdd, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(_toAdd == null))
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    AddScene(_toAdd);
                    _toAdd = null;
                }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add open scene")) AddOpenScene();
            if (GUILayout.Button("Reset to detected labs")) ResetToDetected();
            if (GUILayout.Button("Clear"))
            {
                if (EditorUtility.DisplayDialog("Clear list",
                        "Remove all scenes from the test-scenes list? (Your committed fixtures are not affected.)",
                        "Clear", "Cancel"))
                {
                    TestScenesList.Clear();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(entries.Count == 0))
                if (GUILayout.Button("Export All Test Scenes Now", GUILayout.Height(28)))
                    ExportAllTestScenes.ExportAll();
        }

        void AddScene(SceneAsset scene)
        {
            string path = AssetDatabase.GetAssetPath(scene);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) { TestScenesList.Add(guid); Repaint(); }
        }

        void AddOpenScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!active.IsValid() || string.IsNullOrEmpty(active.path))
            {
                EditorUtility.DisplayDialog("Add open scene",
                    "The active scene has not been saved as a scene asset yet - save it first.", "OK");
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(active.path);
            if (!string.IsNullOrEmpty(guid)) { TestScenesList.Add(guid); Repaint(); }
        }

        void ResetToDetected()
        {
            var seeded = TestScenesList.ReSeed(out var unmatched, out var collisions);
            string msg = $"Re-seeded {seeded.Count} scene(s) from your committed fixtures.";
            if (unmatched.Count > 0)
                msg += $"\n\nNo matching scene found for: {string.Join(", ", unmatched)} "
                       + "(open the lab scene and use 'Add open scene', or it may not exist in this project).";
            if (collisions.Count > 0)
                msg += "\n\nName collisions (first match seeded - verify it is the right scene):\n- "
                       + string.Join("\n- ", collisions);
            EditorUtility.DisplayDialog("Reset to detected labs", msg, "OK");
            Repaint();
        }
    }
}
#endif
