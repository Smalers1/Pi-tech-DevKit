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
    /// The export copies the SAVED scene asset, opens the copy single (restoring the user's scene setup
    /// afterwards), completely unpacks every prefab instance, gathers all roots under one fixture root,
    /// then captures the prefab + baseline - the user's open scene is never dirtied. The fixture is a
    /// FAITHFUL capture of the lab: a graph note the lab already had (e.g. a half-wired listener) is
    /// logged and the developer may proceed; only a violation the export ITSELF introduced (a cross-root
    /// reference that did not survive the gather - found by diffing the graph notes before vs after the
    /// unpack/gather) is a hard refuse.
    ///
    /// Also generates the synthetic corpus: the MEGA census-superset fixture, its prefab variant and
    /// the LegacyForms twins (Documentation~/specs/2026-06-11-mega-fixture-spec.md) - superseding the
    /// old 4-family synthetic_routing_families fixture (spec D1: same public symbol, same menu path).
    /// After saving, dependencies outside the package are reported AND declared
    /// (Tests/Baseline/FixtureDeps - the spec §7.1 skip predicate keys on that declaration).
    /// Re-exporting an existing fixture is the deliberate, reviewed "--regen" (overwrite + recapture).
    /// </summary>
    public static class ExportLabAsTestFixture
    {
        const string FixturesLeaf = "Fixtures/Scenarios";
        const string SnapshotsLeaf = "Baseline/GraphSnapshots";

        // The mega fixture's committed name (spec §4.1); the variant/twin names live in
        // MegaFixtureBuilder. Used here only for the deliberate-regen confirm.
        const string MegaFixtureName = "mega_fixture";

        // ---- entry points -------------------------------------------------------------------

        [MenuItem("Pi tech/Tools/Export Lab as Test Fixture", false, 23)]
        static void ExportOpenSceneMenu() => ExportOpenScene();

        [MenuItem("Pi tech/Tools/Generate Synthetic Scenario Fixture", false, 25)]
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

            // The carried-violations decision belongs to the menu flow: log each pre-existing note,
            // then let the developer choose. The core itself never shows dialogs - it is also driven
            // programmatically (mega-fixture builder), where a callback decides instead.
            bool cancelledOnCarried = false;
            string exported = ExportSceneCore(active.path, name,
                carried =>
                {
                    foreach (var v in carried)
                        Debug.LogWarning("[DevKit] Lab graph note (pre-existing, captured as-is): " + v);
                    bool proceed = EditorUtility.DisplayDialog("Export Lab as Test Fixture",
                        $"The lab has {carried.Count} pre-existing graph note(s). They ship in the lab and will be "
                        + "captured faithfully into the fixture (the net then guards them against DevKit-introduced "
                        + "change). Details are in the Console:\n\n"
                        + Head(carried) + "\n\nExport anyway, or cancel to clean the lab up first?",
                        "Export anyway", "Cancel");
                    cancelledOnCarried = !proceed;
                    return proceed;
                },
                out string failureReason);

            if (exported == null)
            {
                // A cancel at the carried-notes prompt is the developer's own decision - no second
                // dialog. Every other null is a refusal/failure the core already logged.
                if (!cancelledOnCarried)
                    EditorUtility.DisplayDialog("Export Lab as Test Fixture", failureReason, "OK");
                return;
            }

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(exported));
            Debug.Log($"[DevKit] Exported scene '{active.name}' as fixture -> {exported} (+ baseline snapshot).");
        }

        // ---- the export core (menu + programmatic) -------------------------------------------

        /// <summary>
        /// The full single-open-with-restore export of the SAVED scene asset at
        /// <paramref name="scenePath"/>: copy + open Single, pre-capture invariants, unpack every
        /// prefab instance, gather all roots under one fixture root named
        /// <paramref name="fixtureName"/> (spec §1.14/§6.2 - the root name is part of the committed
        /// bytes, so it is the deterministic fixture name, never the temp scene's name), diff
        /// invariants (export-INTRODUCED breaks hard-refuse with <paramref name="failureReason"/>;
        /// CARRIED pre-existing notes go through <paramref name="proceedOnCarried"/> - return false
        /// to abort), save to Tests/Fixtures/Scenarios/&lt;fixtureName&gt;.prefab, capture the
        /// baseline + deps declaration, then restore the caller's scene setup + delete the temp copy
        /// in finally. Shows NO dialogs itself: the menu flow supplies a dialog callback, the
        /// mega-fixture builder drives it programmatically with a temp scene asset.
        /// Returns the fixture asset path, or null with <paramref name="failureReason"/> set.
        /// </summary>
        internal static string ExportSceneCore(
            string scenePath, string fixtureName,
            System.Func<System.Collections.Generic.IReadOnlyList<string>, bool> proceedOnCarried,
            out string failureReason)
        {
            failureReason = null;

            string fixturesDir = TestsSub(FixturesLeaf);
            if (fixturesDir == null)
            {
                failureReason = "Could not locate the DevKit package Tests/ folder.";
                return null;
            }
            EnsureFolder(fixturesDir);
            string fixturePath = fixturesDir + "/" + fixtureName + ".prefab";

            // Work on a throwaway COPY of the saved scene asset, opened SINGLE (not additive). Additive
            // would leave the caller's scene loaded alongside the copy, so an edit-mode SDK singleton (e.g.
            // Meta OVRCameraRig, whose guard runs in edit mode) sees TWO live instances and errors during
            // the load. Single open keeps exactly one alive; the caller's scene setup is restored from disk
            // in finally (which is why the menu flow requires every loaded scene to be saved).
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            string tmpScenePath = fixturesDir + "/_tmp_scene_export.unity";
            if (!AssetDatabase.CopyAsset(scenePath, tmpScenePath))
            {
                failureReason = "Could not copy the scene asset - see the Console.";
                return null;
            }

            try
            {
                var copy = EditorSceneManager.OpenScene(tmpScenePath, OpenSceneMode.Single);

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
                var fixtureRoot = new GameObject(fixtureName);
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
                // refuse, no override, the callback is never consulted; this is a fixture-corruption
                // guard, not a lab problem.
                if (introduced.Count > 0)
                {
                    foreach (var v in introduced) Debug.LogError("[DevKit] Export-introduced graph break: " + v);
                    failureReason =
                        $"Export refused - the export process introduced {introduced.Count} graph break(s) that the "
                        + "lab does NOT have (most likely a cross-root reference that did not survive the gather):\n\n"
                        + Head(introduced) + "\n\nThis is a fixture-corruption guard, not a lab issue - please report it.";
                    return null;   // finally still closes the copy + deletes the tmp scene
                }

                // Pre-existing lab notes = the lab shipped like this (e.g. a half-wired listener).
                // Faithful to capture - but the decision (and any dialog/log) belongs to the caller:
                // the menu flow asks the developer, the mega builder refuses (it must be born clean).
                if (carried.Count > 0)
                {
                    if (proceedOnCarried == null || !proceedOnCarried(carried))
                    {
                        failureReason = $"Export stopped at {carried.Count} pre-existing graph note(s).";
                        return null;
                    }
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(fixtureRoot, fixturePath, out bool ok);
                if (!ok || saved == null)
                {
                    failureReason = "PrefabUtility.SaveAsPrefabAsset failed - see the Console.";
                    return null;
                }

                if (!CaptureBaselineFor(fixturePath, out _, out string captureError))
                {
                    failureReason = "Baseline capture failed - " + captureError;
                    return null;
                }
                AssetDatabase.SaveAssets();
                WarnOnExternalDependencies(fixturePath);
                return fixturePath;
            }
            finally
            {
                // Restore the caller's exact scene setup (multi-scene layout + active scene) from disk,
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

        // ---- generate the synthetic corpus (the mega census-superset fixture) ----------------

        // Retained public symbol + menu path (spec D1: both are Proof-B-baselined / Maintain-card
        // targets). The body now builds the MEGA census-superset corpus - the old 4-family
        // synthetic_routing_families builder is superseded (its unique states live on as T1 second
        // instances; deleting the committed old fixture pair is the reviewed corpus commit, not ours).
        public static void GenerateSyntheticFixture()
        {
            string fixturesDir = TestsSub(FixturesLeaf);
            if (fixturesDir == null)
            {
                EditorUtility.DisplayDialog("Generate Synthetic Fixture",
                    "Could not locate the DevKit package Tests/ folder.", "OK");
                return;
            }
            string megaPath = fixturesDir + "/" + MegaFixtureName + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(megaPath) != null &&
                !EditorUtility.DisplayDialog("Regenerate the synthetic corpus (mega-fixture)",
                    $"This regenerates the MEGA census-superset fixture ('{MegaFixtureName}'), its prefab "
                    + "VARIANT and the LegacyForms twins - always together, in one run (the variant's "
                    + "overrides target the base's internal fileIDs, so a partial regen would orphan them).\n\n"
                    + "They are the committed reference the gate tests against. Regenerate ONLY deliberately - "
                    + "never to silence a failing test - then review the git diff and commit the whole set.\n\n"
                    + "See Documentation~/specs/2026-06-11-mega-fixture-spec.md (§6.4 regen discipline).",
                    "Regenerate", "Cancel"))
                return;

            MegaFixtureBuilder.BuildAndExport();
        }

        // ---- baseline capture (shared walk) -------------------------------------------------

        /// <summary>
        /// Capture the Proof A baseline for a saved fixture prefab: load it, find its Scenario,
        /// write BuildSnapshotJson to Tests/Baseline/GraphSnapshots/&lt;name&gt;.graph.json (name =
        /// fixture file name without extension), then write/refresh the fixture's external-deps
        /// declaration (spec §7.1 - zero externals deletes any stale declaration). Used by both the
        /// scene export and the mega builder's variant capture.
        /// </summary>
        internal static bool CaptureBaselineFor(string fixtureAssetPath, out string baselinePath, out string error)
        {
            baselinePath = null;
            error = null;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fixtureAssetPath);
            var scenario = ScenarioGraphSnapshot.FindScenario(go);
            if (scenario == null)
            {
                error = $"no Scenario component found on the fixture prefab at '{fixtureAssetPath}'.";
                return false;
            }

            string snapshotsDir = TestsSub(SnapshotsLeaf);
            if (snapshotsDir == null)
            {
                error = "could not locate the DevKit package Tests/ folder.";
                return false;
            }
            EnsureFolder(snapshotsDir);

            string name = Path.GetFileNameWithoutExtension(fixtureAssetPath);
            string json = ScenarioGraphSnapshot.BuildSnapshotJson(scenario);
            string snapshotAsset = snapshotsDir + "/" + name + ".graph.json";
            File.WriteAllText(DiskPath(snapshotAsset), json);
            AssetDatabase.ImportAsset(snapshotAsset);

            FixtureDependencies.WriteDeclaration(fixtureAssetPath);

            baselinePath = snapshotAsset;
            return true;
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
        // Internal: shared with FixtureDependencies + MegaFixtureBuilder.
        internal static string TestsSub(string leaf)
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

        // Internal: shared with FixtureDependencies + MegaFixtureBuilder.
        internal static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // First few lines of a finding list for a dialog; the rest stay in the Console.
        static string Head(System.Collections.Generic.IReadOnlyList<string> lines)
            => lines.Count <= 3
                ? string.Join("\n", lines)
                : lines[0] + "\n" + lines[1] + "\n" + lines[2] + $"\n... and {lines.Count - 3} more (see Console)";

        // Internal: shared with ExportAllTestScenes + TestScenesList so the batch path derives the
        // fixture name from a scene name exactly as the open-scene export does (the auto-seed matches
        // committed fixtures by this same sanitized form).
        internal static string Sanitize(string raw)
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
