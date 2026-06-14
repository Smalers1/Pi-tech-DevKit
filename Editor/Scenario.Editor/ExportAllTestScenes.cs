#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Batch counterpart to <see cref="ExportLabAsTestFixture.ExportOpenScene"/>: re-export every scene in
    /// the curated <see cref="TestScenesList"/> in one pass (the list auto-seeds from the scenes matching
    /// your committed lab fixtures). Each scene runs through the same single-open-with-restore
    /// <see cref="ExportLabAsTestFixture.ExportSceneCore"/>, so the user's scene setup is preserved and a
    /// re-export is graph-content (snapshot) neutral for an unchanged lab - prefab fileIDs still churn
    /// (export is not byte-stable across sessions); only the .deps.json is genuinely new. Pre-existing lab
    /// graph notes are captured faithfully and logged - the gate catches any DevKit-introduced drift, so
    /// the batch does not stop per-scene to ask. Deliberate-only: it overwrites the committed references,
    /// so review the git diff before committing.
    /// </summary>
    internal static class ExportAllTestScenes
    {
        [MenuItem("Pi tech/Tools/Export All Test Scenes", false, 24)]
        internal static void ExportAll()
        {
            var scenePaths = TestScenesList.ScenePaths(out var missing);

            if (scenePaths.Count == 0)
            {
                bool open = EditorUtility.DisplayDialog("Export All Test Scenes",
                    "No resolvable scenes in your test-scenes list.\n\n"
                    + (missing.Count > 0 ? $"{missing.Count} stored entry/entries no longer resolve.\n\n" : "")
                    + "Add scenes via 'Manage Test Scenes List' (or 'Reset to detected labs' to re-seed from "
                    + "the scenes matching your committed fixtures), or open a lab and use 'Export Open Scene'.",
                    "Open list manager", "Close");
                if (open) TestScenesListWindow.Open();
                return;
            }

            // Precondition: every loaded scene must be saved - the core opens each scene's copy SINGLE
            // and restores the user's scene setup from disk afterward, so an unsaved scene cannot return.
            if (!AllOpenScenesSaved(out string why))
            {
                EditorUtility.DisplayDialog("Export All Test Scenes", why, "OK");
                return;
            }

            string list = string.Join("\n  ", scenePaths.ConvertAll(Path.GetFileNameWithoutExtension));
            if (!EditorUtility.DisplayDialog("Export All Test Scenes",
                $"Re-export {scenePaths.Count} scene(s) as committed fixtures? This OVERWRITES each scene's "
                + "fixture prefab + Proof A baseline + deps declaration:\n\n  " + list + "\n\n"
                + "Pre-existing lab graph notes are captured faithfully (logged to the Console). Do this "
                + "ONLY on known-good (main) DevKit code, then review the git diff before committing.\n\n"
                + (missing.Count > 0 ? $"({missing.Count} stored entry/entries no longer resolve - skipped.)" : ""),
                "Export all", "Cancel"))
                return;

            var exported = new List<string>();
            var refused = new List<string>();
            var withNotes = new List<string>();
            bool cancelled = false;

            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string scenePath = scenePaths[i];
                    string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    string fixtureName = ExportLabAsTestFixture.Sanitize(sceneName);

                    if (EditorUtility.DisplayCancelableProgressBar("Export All Test Scenes",
                            $"Exporting {sceneName}  ({i + 1}/{scenePaths.Count})",
                            (float)(i + 1) / scenePaths.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    bool hadNotes = false;
                    string result = ExportLabAsTestFixture.ExportSceneCore(scenePath, fixtureName,
                        carried =>
                        {
                            hadNotes = carried.Count > 0;
                            foreach (var v in carried)
                                Debug.LogWarning($"[DevKit] {sceneName}: lab graph note (pre-existing, "
                                                 + "captured as-is): " + v);
                            return true;   // faithful recapture; the gate catches any introduced drift
                        },
                        out string reason);

                    if (result == null)
                        refused.Add(sceneName + ": " + (reason ?? "export refused - see Console"));
                    else
                    {
                        exported.Add(fixtureName);
                        if (hadNotes) withNotes.Add(fixtureName);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var sb = new StringBuilder();
            sb.Append($"Exported {exported.Count}/{scenePaths.Count} scene(s) as fixtures.");
            if (cancelled) sb.Append("\n\nCancelled - the remaining scenes were not exported.");
            if (withNotes.Count > 0)
                sb.Append($"\n\nCaptured pre-existing lab notes for: {string.Join(", ", withNotes)} (see Console).");
            if (refused.Count > 0)
                sb.Append("\n\nNot exported:\n  " + string.Join("\n  ", refused));
            if (missing.Count > 0)
                sb.Append($"\n\n{missing.Count} stored list entry/entries no longer resolve (skipped).");
            sb.Append("\n\nReview 'git status'/'git diff' on Tests/ before committing - a real lab change "
                      + "shows as fixture/baseline edits; a clean re-export adds only the .deps.json files.");

            Debug.Log("[DevKit] Export All Test Scenes: " + sb.ToString().Replace("\n", " "));
            EditorUtility.DisplayDialog("Export All Test Scenes", sb.ToString(), "OK");
        }

        // Mirrors the open-scenes-saved precondition in ExportLabAsTestFixture.ExportOpenScene (the core
        // restores the caller's scene setup from disk, so every loaded scene must be saved + have a path).
        static bool AllOpenScenesSaved(out string reason)
        {
            reason = null;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.isDirty || string.IsNullOrEmpty(s.path))
                {
                    reason = $"Scene '{(string.IsNullOrEmpty(s.name) ? "(untitled)" : s.name)}' is unsaved or "
                             + "has pending changes. Save every open scene first - the batch export opens each "
                             + "scene single and restores your scene setup from disk afterward, so unsaved "
                             + "scenes cannot be brought back.";
                    return false;
                }
            }
            return true;
        }
    }
}
#endif
