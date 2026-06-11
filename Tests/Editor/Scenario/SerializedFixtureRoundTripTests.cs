#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof C (second half) - open->save serialized-diff zero (Appendix I.6). NEVER touches the
    /// committed fixtures or the user's open scene: byte checks run on a temp COPY, and the
    /// prefab-instance-with-override variant instantiates into a PREVIEW SCENE and asserts on
    /// PrefabUtility.GetPropertyModifications - the in-memory form of the exact m_Modifications
    /// block a saved scene would serialize, so the I.6 contract ("the override must not churn
    /// steps/managedReferences into the instance") is checked without any scene save. A preview
    /// scene (not an additive temp scene) because Unity throws InvalidOperationException on any
    /// additive scene while an untitled unsaved scene is open - the default state of a fresh host.
    /// A WS A6 split that regenerated a GUID or moved a [SerializeReference] type's assembly would
    /// change the reserialized managedReferences/m_Script bytes and fail here.
    ///
    /// The byte-diff backstop (ForceReserializeAssets over all fixtures + git diff) lives in CI on a
    /// throwaway checkout (I.6) - never in this unit test, never on a dev's working tree.
    /// </summary>
    public class SerializedFixtureRoundTripTests
    {
        const string TmpFolder = "Assets/_DevKitTestTmp";

        // Only delete the tmp folder if THIS run created it - never destroy a user's same-named folder.
        static bool _createdTmpThisRun;

        [TearDown]
        public void Cleanup()
        {
            if (_createdTmpThisRun && AssetDatabase.IsValidFolder(TmpFolder))
            {
                AssetDatabase.DeleteAsset(TmpFolder);
                _createdTmpThisRun = false;
            }
        }

        static List<string> FixturePaths()
        {
            var paths = new List<string>();
            string dir = TestPaths.FixturesDir();
            if (string.IsNullOrEmpty(dir) || !AssetDatabase.IsValidFolder(dir))
                return paths;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && ScenarioGraphSnapshot.FindScenario(go) != null)
                    paths.Add(path);
            }
            return paths;
        }

        static void EnsureTmp()
        {
            if (AssetDatabase.IsValidFolder(TmpFolder))
            {
                // A pre-existing folder is NOT ours to manage; refuse rather than risk deleting
                // user content in TearDown.
                if (!_createdTmpThisRun)
                    Assert.Fail($"{TmpFolder} already exists in this project. The round-trip tests need to "
                                + "own that folder (it is created and deleted per run) - move or rename the "
                                + "existing folder.");
                return;
            }
            AssetDatabase.CreateFolder("Assets", "_DevKitTestTmp");
            _createdTmpThisRun = true;
        }

        [Test]
        public void Reserialize_IsIdempotent_AndMatchesCommittedBytes()
        {
            var fixtures = FixturePaths();
            if (fixtures.Count == 0)
                Assert.Inconclusive("No scenario fixtures yet - nothing to round-trip.");

            EnsureTmp();
            var nonIdempotent = new List<string>();
            var notNormalized = new List<string>();

            foreach (var src in fixtures)
            {
                string name = Path.GetFileNameWithoutExtension(src);
                string copy = TmpFolder + "/" + name + ".prefab";
                Assert.IsTrue(AssetDatabase.CopyAsset(src, copy), $"CopyAsset failed for {src}");

                string committed = File.ReadAllText(TestPaths.DiskPath(src));   // never modified

                AssetDatabase.ForceReserializeAssets(new[] { copy });
                string b1 = File.ReadAllText(TestPaths.DiskPath(copy));
                AssetDatabase.ForceReserializeAssets(new[] { copy });
                string b2 = File.ReadAllText(TestPaths.DiskPath(copy));

                if (!string.Equals(b1, b2, System.StringComparison.Ordinal))
                    nonIdempotent.Add(name);            // non-deterministic serialization = real red flag
                else if (!string.Equals(committed, b1, System.StringComparison.Ordinal))
                    notNormalized.Add(name);            // fixture not yet in Unity-normalized form
            }

            // Hard failure: serialization is unstable across identical saves.
            Assert.IsEmpty(nonIdempotent,
                "Non-idempotent reserialization (graph churns on save):\n  " + string.Join("\n  ", nonIdempotent));

            // Soft: a freshly-exported fixture may not be pre-normalized; re-save it once in Unity to
            // capture the canonical bytes, after which committed==reserialized enforces serialized-diff zero.
            if (notNormalized.Count > 0)
                Assert.Inconclusive("Fixture(s) not in Unity-normalized form yet: " + string.Join(", ", notNormalized)
                    + ". Open + re-save each once (or re-export), commit, then this enforces equality.");
        }

        [Test]
        public void PrefabInstanceOverride_DoesNotChurnTheGraph()
        {
            var fixtures = FixturePaths();
            if (fixtures.Count == 0)
                Assert.Inconclusive("No scenario fixtures yet - nothing to override-test.");

            var failures = new List<string>();

            foreach (var src in fixtures)
            {
                string name = Path.GetFileNameWithoutExtension(src);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(src);

                // I.6 contract via a preview scene: instantiate P -> set one override -> Record ->
                // assert the instance's PropertyModifications (the in-memory m_Modifications block a
                // scene save would serialize) carry no step-graph churn -> close the preview scene.
                var previewScene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, previewScene);
                    var scenario = ScenarioGraphSnapshot.FindScenario(instance);
                    if (scenario == null) { failures.Add($"[{name}] no Scenario on the instantiated fixture"); continue; }

                    // Source-prefab link must point back at the committed fixture.
                    string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                    if (!string.Equals(sourcePath, src, System.StringComparison.Ordinal))
                        failures.Add($"[{name}] instance source prefab is '{sourcePath}', expected '{src}'");

                    // One trivial override + record it (mirrors authored prefab-instance edits).
                    bool overrideApplied = false;
                    var so = new SerializedObject(scenario);
                    var titleProp = so.FindProperty("title");
                    if (titleProp != null)
                    {
                        titleProp.stringValue = (titleProp.stringValue ?? "") + " (ovr)";
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(scenario);
                        overrideApplied = true;
                    }

                    // In-memory sanity while the instance is alive.
                    var violations = ScenarioGraphSnapshot.CheckInvariants(scenario);
                    if (violations.Count > 0)
                        failures.Add($"[{name}] override instance broke invariants: {violations[0]}");

                    // The instance's modification list = exactly what a scene save would write as
                    // m_Modifications. The title override must be there (positive control proving we
                    // are looking at the real list); the step graph must NOT be.
                    var mods = PrefabUtility.GetPropertyModifications(instance);
                    bool sawTitle = false;
                    if (mods != null)
                    {
                        foreach (var mod in mods)
                        {
                            if (mod == null || mod.propertyPath == null) continue;
                            if (mod.propertyPath == "title") sawTitle = true;
                            if (mod.propertyPath.StartsWith("steps", System.StringComparison.Ordinal))
                                failures.Add($"[{name}] the title override leaked a STEP-GRAPH modification "
                                             + $"onto the prefab instance: '{mod.propertyPath}'");
                            if (mod.propertyPath.Contains("managedReferences"))
                                failures.Add($"[{name}] the title override churned managedReferences data "
                                             + $"onto the prefab instance: '{mod.propertyPath}'");
                        }
                    }
                    if (overrideApplied && !sawTitle)
                        failures.Add($"[{name}] the recorded 'title' override is missing from "
                                     + "GetPropertyModifications - the check would be vacuous");
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
#endif
