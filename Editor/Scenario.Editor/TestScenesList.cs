#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// The curated "test scenes" list that backs <see cref="ExportAllTestScenes"/> - the scenes you want
    /// re-exported as fixtures in one pass. Stored PER PROJECT, PER USER, and NOT source-controlled
    /// (EditorUserSettings -> Library/EditorUserSettings.asset): the committed fixtures+baselines+deps in
    /// the package are the shared truth; this is just a local convenience roster. Scenes are stored by
    /// GUID (stable across renames/moves).
    ///
    /// First access AUTO-SEEDS from the scenes whose sanitized name matches an existing committed real-lab
    /// fixture - so the list arrives pre-filled with your labs and a one-click batch re-export "just
    /// works". Synthetic fixtures (mega_fixture*) are excluded from the seed: they come from Generate, not
    /// a scene. After the first seed the list is yours (add/remove); "Reset to detected labs" re-seeds.
    /// </summary>
    internal static class TestScenesList
    {
        const string ListKey = "Pitech.XR.DevKit.TestScenes";        // newline-joined scene GUIDs
        const string SeededKey = "Pitech.XR.DevKit.TestScenes.Seeded"; // "1" once auto-seeded/edited

        internal struct Entry
        {
            public string guid;
            public string path;     // may be empty if the scene was deleted/moved
            public bool resolved;
            public string display;
        }

        // ---- the list ------------------------------------------------------------------------

        /// <summary>Current scene GUIDs, auto-seeding from fixture-matching scenes on first use.</summary>
        internal static List<string> Guids()
        {
            if (EditorUserSettings.GetConfigValue(SeededKey) != "1")
            {
                var seeded = ComputeSeed(out _, out _);
                WriteStored(seeded);
                EditorUserSettings.SetConfigValue(SeededKey, "1");
                return seeded;
            }
            return ReadStored();
        }

        /// <summary>Force a re-seed from the scenes matching the committed real-lab fixtures, replacing
        /// the stored list. Returns the new GUIDs; <paramref name="unmatchedFixtures"/> lists fixtures
        /// that had no matching scene in this project, and <paramref name="collisions"/> lists labs whose
        /// sanitized name matched more than one scene (the first hit was seeded - verify it is the right
        /// one).</summary>
        internal static List<string> ReSeed(out List<string> unmatchedFixtures, out List<string> collisions)
        {
            var seeded = ComputeSeed(out unmatchedFixtures, out collisions);
            WriteStored(seeded);
            EditorUserSettings.SetConfigValue(SeededKey, "1");
            return seeded;
        }

        internal static void Add(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid)) return;
            var list = Guids();                       // ensures seeded first
            if (!list.Contains(sceneGuid)) { list.Add(sceneGuid); WriteStored(list); }
            EditorUserSettings.SetConfigValue(SeededKey, "1");
        }

        internal static void Remove(string sceneGuid)
        {
            var list = Guids();
            if (list.Remove(sceneGuid)) WriteStored(list);
            EditorUserSettings.SetConfigValue(SeededKey, "1");
        }

        internal static void Clear()
        {
            WriteStored(Array.Empty<string>());
            EditorUserSettings.SetConfigValue(SeededKey, "1");   // an explicit empty is a user choice, not "unseeded"
        }

        // ---- resolution for the batch + window ----------------------------------------------

        /// <summary>Resolve the list to scene asset paths. <paramref name="missingGuids"/> collects
        /// entries whose scene no longer resolves (deleted/moved), so the batch can report them.</summary>
        internal static List<string> ScenePaths(out List<string> missingGuids)
        {
            missingGuids = new List<string>();
            var paths = new List<string>();
            foreach (var g in Guids())
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(p) || !p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    missingGuids.Add(g);
                else
                    paths.Add(p);
            }
            return paths;
        }

        internal static List<Entry> Entries()
        {
            var list = new List<Entry>();
            foreach (var g in Guids())
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                bool ok = !string.IsNullOrEmpty(p) && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
                list.Add(new Entry
                {
                    guid = g,
                    path = p ?? "",
                    resolved = ok,
                    display = ok ? Path.GetFileNameWithoutExtension(p) : "(missing scene: " + g + ")"
                });
            }
            return list;
        }

        // ---- seed computation ----------------------------------------------------------------

        // For each committed real-lab fixture, find the project scene whose sanitized name matches the
        // fixture's name (the open-scene export names a fixture after its scene via the same Sanitize).
        static List<string> ComputeSeed(out List<string> unmatchedFixtures, out List<string> collisions)
        {
            unmatchedFixtures = new List<string>();
            collisions = new List<string>();
            var guids = new List<string>();

            var scenesBySanitizedName = new Dictionary<string, string>(StringComparer.Ordinal);
            var shadowedPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var sg in AssetDatabase.FindAssets("t:SceneAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(sg);
                if (string.IsNullOrEmpty(p)) continue;
                string key = ExportLabAsTestFixture.Sanitize(Path.GetFileNameWithoutExtension(p));
                if (scenesBySanitizedName.ContainsKey(key))
                {
                    // A second scene sanitizes to the same name: the first hit (arbitrary asset order)
                    // wins the seed. Record the shadowed path so a lab-relevant collision is surfaced
                    // instead of silently picking the wrong scene to re-export.
                    if (!shadowedPaths.TryGetValue(key, out var list))
                        shadowedPaths[key] = list = new List<string>();
                    list.Add(p);
                }
                else scenesBySanitizedName[key] = sg;
            }

            foreach (var fixtureName in RealLabFixtureNames())
            {
                if (scenesBySanitizedName.TryGetValue(fixtureName, out var sg))
                {
                    if (!guids.Contains(sg)) guids.Add(sg);
                    if (shadowedPaths.TryGetValue(fixtureName, out var shadowed))
                        collisions.Add($"{fixtureName}: seeded '{AssetDatabase.GUIDToAssetPath(sg)}', "
                                       + $"ignored {string.Join(", ", shadowed)}");
                }
                else unmatchedFixtures.Add(fixtureName);
            }
            return guids;
        }

        // Committed fixture file stems under Tests/Fixtures/Scenarios, minus the synthetic corpus
        // (mega_fixture*) which is generated, not exported from a scene.
        static List<string> RealLabFixtureNames()
        {
            var names = new List<string>();
            string dir = ExportLabAsTestFixture.TestsSub("Fixtures/Scenarios");
            if (dir == null || !AssetDatabase.IsValidFolder(dir)) return names;
            foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string stem = Path.GetFileNameWithoutExtension(p);
                if (stem.StartsWith("mega_fixture", StringComparison.Ordinal)) continue;   // synthetic - no scene
                if (!names.Contains(stem)) names.Add(stem);
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        // ---- storage (EditorUserSettings: project-local, per-user, uncommitted) --------------

        static List<string> ReadStored()
        {
            string raw = EditorUserSettings.GetConfigValue(ListKey);
            var list = new List<string>();
            if (!string.IsNullOrEmpty(raw))
                foreach (var line in raw.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length > 0 && !list.Contains(t)) list.Add(t);
                }
            return list;
        }

        static void WriteStored(IEnumerable<string> guids)
            => EditorUserSettings.SetConfigValue(ListKey, string.Join("\n", guids));
    }
}
#endif
