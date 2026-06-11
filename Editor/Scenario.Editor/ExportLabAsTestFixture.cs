#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// WS A3 Step 7 - turns a live lab into a self-contained, static-read test fixture and captures its
    /// Proof A baseline snapshot. THE process (2026-06-11 consolidation): run this in HealthOn VR with
    /// the DevKit referenced by local file: path, right before testing - the fixture + baseline land
    /// directly in the package source tree, where the developer's DevKit gate project (same file: folder)
    /// sees them immediately. One export mode, the whole open scene: real lab scenes spread the step
    /// graph's references across several top-level roots ("--- SCENE MANAGERS ---" holds the Scenario,
    /// "--- UI ---" holds the panels it points at), so no single selectable subtree contains everything.
    /// The export copies the SAVED scene asset, opens the copy additively, completely unpacks every
    /// prefab instance, gathers all roots under one fixture root, then captures the prefab + baseline -
    /// the user's open scene is never dirtied. The fixture is a FAITHFUL capture of the lab: a graph
    /// note the lab already had (e.g. a half-wired listener) is logged and the developer may proceed;
    /// only a violation the export ITSELF introduced (a cross-root reference that did not survive the
    /// gather - found by diffing the graph notes before vs after the unpack/gather) is a hard refuse.
    ///
    /// Also generates the ONE mandatory synthetic fixture covering the routing families absent from
    /// every real VR lab (SpecificChild specificStepGuid, ConditionsStep, SelectionStep allowedWrong>0,
    /// non-empty defaultNextGuid - Appendix I.3).
    /// After saving, dependencies outside the package are reported. Re-exporting an existing fixture
    /// is the deliberate, reviewed "--regen" (overwrite + recapture).
    /// </summary>
    public static class ExportLabAsTestFixture
    {
        const string FixturesLeaf = "Fixtures/Scenarios";
        const string SnapshotsLeaf = "Baseline/GraphSnapshots";

        // ---- entry points -------------------------------------------------------------------

        [MenuItem("Pi tech/Tools/Export Lab as Test Fixture", false, 23)]
        static void ExportOpenSceneMenu() => ExportOpenScene();

        [MenuItem("Pi tech/Tools/Generate Synthetic Scenario Fixture", false, 24)]
        static void GenerateSyntheticMenu() => GenerateSyntheticFixture();

        // ---- export the whole open scene (the real-lab path) ----------------------------------

        public static void ExportOpenScene()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!active.IsValid() || string.IsNullOrEmpty(active.path))
            {
                EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                    "The active scene has never been saved. Save it as a scene asset first - the export "
                    + "works on the SAVED scene file.", "OK");
                return;
            }
            if (active.isDirty)
            {
                EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                    "The active scene has unsaved changes. Save it first - the export copies the SAVED "
                    + "scene asset, so unsaved edits would be silently missing from the fixture.", "OK");
                return;
            }

            bool hasScenario = false;
            foreach (var root in active.GetRootGameObjects())
                if (root.GetComponentInChildren<Scenario>(true) != null) { hasScenario = true; break; }
            if (!hasScenario)
            {
                EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                    $"Scene '{active.name}' contains no Scenario component - nothing to export.", "OK");
                return;
            }

            // The export briefly loads ONLY the lab scene (single-open) and restores the user's scene
            // setup from disk afterwards, so every loaded scene must be saved + have an asset path.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.isDirty || string.IsNullOrEmpty(s.path))
                {
                    EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                        $"Scene '{(string.IsNullOrEmpty(s.name) ? "(untitled)" : s.name)}' is unsaved or has "
                        + "pending changes. Save every open scene first - the export restores your scene "
                        + "setup from disk, so unsaved scenes cannot be brought back.", "OK");
                    return;
                }
            }

            string name = Sanitize(active.name);
            string fixturesDir = TestsSub(FixturesLeaf);
            if (fixturesDir == null)
            {
                EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                    "Could not locate the DevKit package Tests/ folder.", "OK");
                return;
            }
            EnsureFolder(fixturesDir);

            string fixturePath = fixturesDir + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fixturePath) != null &&
                !EditorUtility.DisplayDialog("Re-export fixture (overwrites the committed reference)",
                    $"'{name}.prefab' and its baseline already exist - they are the committed reference the gate "
                    + "tests against. Re-exporting OVERWRITES both.\n\n"
                    + "Do this ONLY when:\n"
                    + "  • the real lab genuinely changed (then review the git diff and commit the new pair), or\n"
                    + "  • you are re-capturing on known-good (main) DevKit code.\n\n"
                    + "Do NOT re-export to silence a failing test while your own DevKit changes are loaded - that just "
                    + "bakes your change into the reference and hides exactly what the gate exists to catch.",
                    "Overwrite", "Cancel"))
                return;

            // Work on a throwaway COPY of the saved scene asset, opened SINGLE (not additive). Additive
            // would leave the user's scene loaded alongside the copy, so an edit-mode SDK singleton (e.g.
            // Meta OVRCameraRig, whose guard runs in edit mode) sees TWO live instances and errors during
            // the load. Single open keeps exactly one alive; the user's scene setup is restored from disk
            // in finally (which is why every loaded scene was required to be saved above).
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            string tmpScenePath = fixturesDir + "/_tmp_scene_export.unity";
            if (!AssetDatabase.CopyAsset(active.path, tmpScenePath))
            {
                EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                    "Could not copy the scene asset - see the Console.", "OK");
                return;
            }

            UnityEngine.SceneManagement.Scene copy = default;
            try
            {
                copy = EditorSceneManager.OpenScene(tmpScenePath, OpenSceneMode.Single);

                // Snapshot the lab's PRE-EXISTING graph notes before we touch anything, so we can tell
                // "the lab already shipped like this" (author's concern - log it, let them proceed)
                // apart from "our unpack/gather broke a reference" (export bug - hard refuse).
                var preExisting = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                foreach (var root in copy.GetRootGameObjects())
                    foreach (var s in root.GetComponentsInChildren<Scenario>(true))
                        foreach (var v in ScenarioGraphSnapshot.CheckInvariants(s))
                            preExisting.Add(v);

                // Completely unpack every outermost prefab instance so the saved fixture is a plain,
                // self-contained prefab (no variant, no nested-prefab GUID dependencies). Safe: this
                // is the throwaway copy.
                foreach (var root in copy.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var go = t.gameObject;
                        if (PrefabUtility.IsAnyPrefabInstanceRoot(go)
                            && PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                        {
                            PrefabUtility.UnpackPrefabInstance(
                                go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        }
                    }
                }

                // Gather every root under one fixture root so cross-root references (Scenario under
                // "--- SCENE MANAGERS ---" pointing at panels under "--- UI ---") stay intra-prefab.
                var fixtureRoot = new GameObject(active.name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixtureRoot, copy);
                foreach (var root in copy.GetRootGameObjects())
                {
                    if (root != fixtureRoot)
                        root.transform.SetParent(fixtureRoot.transform, true);
                }

                // Re-check on the assembled fixture, then split the findings against what the lab already
                // had. A fixture should FAITHFULLY capture the lab - the net detects DevKit-introduced
                // drift later, so the lab's own pre-existing imperfections are not ours to block on.
                var finalViolations = new System.Collections.Generic.List<string>();
                foreach (var s in fixtureRoot.GetComponentsInChildren<Scenario>(true))
                    finalViolations.AddRange(ScenarioGraphSnapshot.CheckInvariants(s));

                var introduced = finalViolations.FindAll(v => !preExisting.Contains(v));
                var carried = finalViolations.FindAll(v => preExisting.Contains(v));

                // Export-INTRODUCED breaks = the unpack/gather corrupted the graph (most likely a
                // cross-root reference that did not survive). The fixture would NOT match the lab - hard
                // refuse, no override; this is a fixture-corruption guard, not a lab problem.
                if (introduced.Count > 0)
                {
                    foreach (var v in introduced) Debug.LogError("[DevKit] Export-introduced graph break: " + v);
                    EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                        $"Export refused - the export process introduced {introduced.Count} graph break(s) that the "
                        + "lab does NOT have (most likely a cross-root reference that did not survive the gather):\n\n"
                        + Head(introduced) + "\n\nThis is a fixture-corruption guard, not a lab issue - please report it.",
                        "OK");
                    return;   // finally still closes the copy + deletes the tmp scene
                }

                // Pre-existing lab notes = the lab shipped like this (e.g. a half-wired listener). Faithful
                // to capture; surface as a Console log + let the developer proceed.
                if (carried.Count > 0)
                {
                    foreach (var v in carried) Debug.LogWarning("[DevKit] Lab graph note (pre-existing, captured as-is): " + v);
                    bool proceed = EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                        $"The lab has {carried.Count} pre-existing graph note(s). They ship in the lab and will be "
                        + "captured faithfully into the fixture (the net then guards them against DevKit-introduced "
                        + "change). Details are in the Console:\n\n"
                        + Head(carried) + "\n\nExport anyway, or cancel to clean the lab up first?",
                        "Export anyway", "Cancel");
                    if (!proceed) return;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(fixtureRoot, fixturePath, out bool ok);
                if (!ok || saved == null)
                {
                    EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                        "PrefabUtility.SaveAsPrefabAsset failed - see the Console.", "OK");
                    return;
                }

                CaptureBaseline(fixturePath, name);
                AssetDatabase.SaveAssets();
                WarnOnExternalDependencies(fixturePath);
                EditorGUIUtility.PingObject(saved);
                Debug.Log($"[DevKit] Exported scene '{active.name}' as fixture -> {fixturePath} (+ baseline snapshot).");
            }
            finally
            {
                // Restore the user's exact scene setup (multi-scene layout + active scene) from disk,
                // then delete the throwaway copy. Restore first so the copy is unloaded even if the
                // delete throws; guard restore so a failure there still attempts the delete.
                try
                {
                    if (sceneSetup != null && sceneSetup.Length > 0)
                        EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
                }
                finally
                {
                    AssetDatabase.DeleteAsset(tmpScenePath);
                }
            }
        }

        // ---- generate the mandatory synthetic fixture ---------------------------------------

        public static void GenerateSyntheticFixture()
        {
            const string name = "synthetic_routing_families";
            string fixturesDir = TestsSub(FixturesLeaf);
            if (fixturesDir == null)
            {
                EditorUtility.DisplayDialog("Generate Synthetic Fixture",
                    "Could not locate the DevKit package Tests/ folder.", "OK");
                return;
            }
            EnsureFolder(fixturesDir);
            string fixturePath = fixturesDir + "/" + name + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(fixturePath) != null &&
                !EditorUtility.DisplayDialog("Regenerate synthetic fixture",
                    $"'{name}.prefab' exists. Regenerate it and recapture its baseline?", "Regenerate", "Cancel"))
                return;

            // Build in a preview scene so the user's open scene is never touched or dirtied.
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("SyntheticRoutingFamilies");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
                var scenario = root.AddComponent<Scenario>();

                // Build the four families, then wire routing once every guid exists.
                var cond = new ConditionsStep();
                cond.outcomes.Add(new ConditionOutcome { label = "low", compareOp = CompareOp.Less, compareValue = 50f });

                var sel = new SelectionStep { allowedWrong = 1, requiredSelections = 2 };

                var mq = new MiniQuizStep();
                mq.outcomes.Add(new MiniQuizOutcome { label = "all", minCorrect = 1, maxCorrect = -1 });

                var child = new EventStep();
                var group = new GroupStep { completeWhen = GroupStep.CompleteWhen.SpecificChildCompletes };
                group.steps.Add(child);

                scenario.steps.Add(cond);
                scenario.steps.Add(sel);
                scenario.steps.Add(mq);
                scenario.steps.Add(group);

                // Non-vacuous routing: every route resolves to a real step guid.
                cond.outcomes[0].nextGuid = sel.guid;          // ConditionsStep outcome route
                sel.correctNextGuid = mq.guid;                 // correct/wrong routing (allowedWrong>0)
                sel.wrongNextGuid = "";                        // empty = linear next, valid
                mq.defaultNextGuid = group.guid;               // non-empty defaultNextGuid family
                group.specificStepGuid = child.guid;           // SpecificChild family
                group.nextGuid = "";

                var saved = PrefabUtility.SaveAsPrefabAsset(root, fixturePath, out bool ok);
                if (!ok || saved == null)
                {
                    EditorUtility.DisplayDialog("Generate Synthetic Fixture",
                        "SaveAsPrefabAsset failed - see the Console.", "OK");
                    return;
                }

                CaptureBaseline(fixturePath, name);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(saved);
                Debug.Log($"[DevKit] Generated synthetic fixture '{name}' (ConditionsStep + SpecificChild "
                          + "specificStepGuid + SelectionStep allowedWrong>0 + non-empty defaultNextGuid).");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        // ---- baseline capture (shared walk) -------------------------------------------------

        static void CaptureBaseline(string fixtureAssetPath, string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fixtureAssetPath);
            var scenario = ScenarioGraphSnapshot.FindScenario(go);
            if (scenario == null) return;

            string snapshotsDir = TestsSub(SnapshotsLeaf);
            if (snapshotsDir == null) return;
            EnsureFolder(snapshotsDir);

            string json = ScenarioGraphSnapshot.BuildSnapshotJson(scenario);
            string snapshotAsset = snapshotsDir + "/" + name + ".graph.json";
            File.WriteAllText(DiskPath(snapshotAsset), json);
            AssetDatabase.ImportAsset(snapshotAsset);
        }

        // A self-contained fixture must not depend on the consumer project's Assets/. Warn loudly when
        // it does (e.g. materials/textures the lab references) - the gate project will show those refs
        // as Missing.
        static void WarnOnExternalDependencies(string fixtureAssetPath)
        {
            var external = new StringBuilder();
            foreach (var dep in AssetDatabase.GetDependencies(fixtureAssetPath, true))
            {
                if (dep.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    external.Append("  - ").Append(dep).Append('\n');
            }
            if (external.Length > 0)
                Debug.LogWarning("[DevKit] Exported fixture still depends on consumer-project assets - these will "
                                 + "be Missing in the DevKit gate project (Proof A treats cleanly-null as OK but "
                                 + "dangling as a violation):\n" + external);
        }

        // ---- package path helpers (asset-search anchored; reference-free) -------------------

        // <package>/Tests/<leaf>, located via the test asmdef asset (no assembly reference needed).
        // Walks up to the "Tests" folder so it stays correct as the asmdef sits under Tests/Editor/Scenario.
        static string TestsSub(string leaf)
        {
            foreach (var guid in AssetDatabase.FindAssets("Pitech.XR.Scenario.Editor.Tests t:AssemblyDefinitionAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (p.EndsWith("/Pitech.XR.Scenario.Editor.Tests.asmdef", System.StringComparison.Ordinal))
                {
                    string dir = Path.GetDirectoryName(p).Replace('\\', '/');
                    for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
                    {
                        if (LastSegment(dir) == "Tests") return dir + "/" + leaf;
                        dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                    }
                }
            }
            return null;
        }

        static string LastSegment(string p)
        {
            int i = p.LastIndexOf('/');
            return i >= 0 ? p.Substring(i + 1) : p;
        }

        // Unity's documented virtual->physical resolver (handles Packages/<id> for embedded, file:,
        // and cache installs alike).
        static string DiskPath(string assetPath) => FileUtil.GetPhysicalPath(assetPath);

        static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // First few lines of a finding list for a dialog; the rest stay in the Console.
        static string Head(System.Collections.Generic.List<string> lines)
            => lines.Count <= 3
                ? string.Join("\n", lines)
                : string.Join("\n", lines.GetRange(0, 3)) + $"\n... and {lines.Count - 3} more (see Console)";

        static string Sanitize(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            string s = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(s) ? "lab_fixture" : s;
        }
    }
}
#endif
