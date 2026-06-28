#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Minimal menu wrappers around <see cref="ScenarioJsonExporter"/> / <see cref="ScenarioJsonImporter"/>
    /// for the portal / authoring workflow: export the selected lab's step graph to a portable JSON file,
    /// or import such a file onto the selected lab. Operates on the <see cref="Scenario"/> found on the
    /// current selection (the selected object or any child). Import REBUILDS the step graph and leaves
    /// object references / UnityEvents at their defaults (see <see cref="ScenarioJsonImporter"/>); the
    /// confirm dialog says so. Deliberately a standalone file - it adds no items to ScenarioGraphWindow.
    /// </summary>
    public static class ScenarioJsonMenu
    {
        [MenuItem("Pi tech/Tools/Export Lab Scenario to JSON", false, 27)]
        static void ExportSelectedMenu()
        {
            var scenario = SelectedScenario();
            if (scenario == null)
            {
                EditorUtility.DisplayDialog("Export Lab Scenario to JSON",
                    "Select a GameObject with a Scenario component (or one of its parents) first.", "OK");
                return;
            }

            string suggested = SanitizeFileName(scenario.Title);
            string path = EditorUtility.SaveFilePanel("Export Lab Scenario to JSON",
                "", suggested + ".scenario.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                File.WriteAllText(path, ScenarioJsonExporter.ToJson(scenario));
                Debug.Log($"[DevKit] Exported scenario '{scenario.Title}' to {path}");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Export Lab Scenario to JSON",
                    "Could not write the JSON file: " + e.Message, "OK");
            }
        }

        [MenuItem("Pi tech/Tools/Import Lab Scenario from JSON", false, 28)]
        static void ImportSelectedMenu()
        {
            var scenario = SelectedScenario();
            if (scenario == null)
            {
                EditorUtility.DisplayDialog("Import Lab Scenario from JSON",
                    "Select a GameObject with a Scenario component (or one of its parents) first.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Import Lab Scenario from JSON",
                    "This REBUILDS the selected lab's step graph from the JSON file, replacing the current "
                    + "steps.\n\nScene/asset references (buttons, panels, directors, quiz assets) and "
                    + "UnityEvents are NOT in the portable JSON and will be reset to defaults - re-wire them "
                    + "in the scene afterwards.\n\nContinue?",
                    "Import", "Cancel"))
                return;

            string path = EditorUtility.OpenFilePanel("Import Lab Scenario from JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                ScenarioJsonImporter.Apply(json, scenario);
                Debug.Log($"[DevKit] Imported scenario from {path} onto '{scenario.Title}'");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Import Lab Scenario from JSON",
                    "Could not import the JSON file: " + e.Message, "OK");
            }
        }

        // The Scenario on the active selection (the object itself or any child), or null.
        static Scenario SelectedScenario()
        {
            var go = Selection.activeGameObject;
            return go == null ? null : go.GetComponentInChildren<Scenario>(true);
        }

        // Strip characters illegal in a file name; fall back to a stable default for an empty title.
        static string SanitizeFileName(string title)
        {
            if (string.IsNullOrEmpty(title)) return "scenario";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(title.Length);
            foreach (char c in title)
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
#endif
