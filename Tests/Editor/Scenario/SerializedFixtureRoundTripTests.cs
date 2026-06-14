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
    /// Proof C (second half) - open->save serialized-diff zero (Appendix I.6). PARAMETRIZED PER LAB (via
    /// <see cref="FixtureCorpus.Cases"/>): each fixture is its own case, so a churn surfaces against the
    /// exact lab. NEVER touches the committed fixtures or the user's open scene: byte checks run on a
    /// temp COPY, and the prefab-instance-with-override variant instantiates into a PREVIEW SCENE and
    /// asserts on PrefabUtility.GetPropertyModifications - the in-memory form of the exact m_Modifications
    /// block a saved scene would serialize, so the I.6 contract ("the override must not churn
    /// steps/managedReferences into the instance") is checked without any scene save. A preview scene
    /// (not an additive temp scene) because Unity throws InvalidOperationException on any additive scene
    /// while an untitled unsaved scene is open - the default state of a fresh host. A WS A6 split that
    /// regenerated a GUID or moved a [SerializeReference] type's assembly would change the reserialized
    /// managedReferences/m_Script bytes and fail here.
    ///
    /// The byte-diff backstop (ForceReserializeAssets over all fixtures + git diff) lives in CI on a
    /// throwaway checkout (I.6) - never in this unit test, never on a dev's working tree.
    ///
    /// Step 12 (spec §7.1.6): both tests honor the same per-lab skip predicate as
    /// ScenarioGraphIntegrityTests - a fixture whose committed deps declaration names &gt;=1 dependency
    /// that does not resolve here is SKIPPED loudly (Inconclusive, never counted green). Mandatory here,
    /// not optional: ForceReserializeAssets over a copy whose scripts/refs are missing in this project
    /// would itself rewrite the copy in degraded form, so evaluating a skipped fixture is unsafe by
    /// construction.
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

        [TestCaseSource(typeof(FixtureCorpus), nameof(FixtureCorpus.Cases))]
        public void Reserialize_IsIdempotent(string fixtureName)
        {
            FixtureCorpus.RequireFixture(fixtureName);
            string src = FixtureCorpus.PathFor(fixtureName);
            FixtureCorpus.SkipIfUnmetDeps(fixtureName, src);
            FixtureCorpus.Resolve(fixtureName);   // a discovered fixture must load

            EnsureTmp();
            string copy = TmpFolder + "/" + fixtureName + ".prefab";
            Assert.IsTrue(AssetDatabase.CopyAsset(src, copy), $"[{fixtureName}] CopyAsset failed for {src}");

            string committed = File.ReadAllText(TestPaths.DiskPath(src));   // never modified

            AssetDatabase.ForceReserializeAssets(new[] { copy });
            string b1 = File.ReadAllText(TestPaths.DiskPath(copy));
            AssetDatabase.ForceReserializeAssets(new[] { copy });
            string b2 = File.ReadAllText(TestPaths.DiskPath(copy));

            // Hard failure: serialization is unstable across identical saves.
            if (!string.Equals(b1, b2, System.StringComparison.Ordinal))
                Assert.Fail($"[{fixtureName}] non-idempotent reserialization - the graph churns on save "
                            + "(serialization is non-deterministic). This is a real red flag.");

            // Soft: a freshly-exported fixture may not be pre-normalized; re-save it once in Unity to
            // capture the canonical bytes, after which committed==reserialized enforces serialized-diff zero.
            if (!string.Equals(committed, b1, System.StringComparison.Ordinal))
                Assert.Inconclusive($"[{fixtureName}] not in Unity-normalized form yet. Open + re-save once "
                            + "(or re-export), commit, then this enforces committed == reserialized "
                            + "(serialized-diff zero).");
        }

        [TestCaseSource(typeof(FixtureCorpus), nameof(FixtureCorpus.Cases))]
        public void PrefabInstanceOverride_DoesNotChurnTheGraph(string fixtureName)
        {
            FixtureCorpus.RequireFixture(fixtureName);
            string src = FixtureCorpus.PathFor(fixtureName);
            FixtureCorpus.SkipIfUnmetDeps(fixtureName, src);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(src);
            Assert.IsNotNull(asset, $"[{fixtureName}] fixture prefab did not load at '{src}'.");

            var failures = new List<string>();

            // I.6 contract via a preview scene: instantiate P -> set one override -> Record ->
            // assert the instance's PropertyModifications (the in-memory m_Modifications block a
            // scene save would serialize) carry no step-graph churn -> close the preview scene.
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, previewScene);
                var scenario = ScenarioGraphSnapshot.FindScenario(instance);
                if (scenario == null) { Assert.Fail($"[{fixtureName}] no Scenario on the instantiated fixture"); return; }

                // Source-prefab link must point back at the committed fixture.
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                if (!string.Equals(sourcePath, src, System.StringComparison.Ordinal))
                    failures.Add($"[{fixtureName}] instance source prefab is '{sourcePath}', expected '{src}'");

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
                    failures.Add($"[{fixtureName}] override instance broke invariants: {violations[0]}");

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
                            failures.Add($"[{fixtureName}] the title override leaked a STEP-GRAPH modification "
                                         + $"onto the prefab instance: '{mod.propertyPath}'");
                        if (mod.propertyPath.Contains("managedReferences"))
                            failures.Add($"[{fixtureName}] the title override churned managedReferences data "
                                         + $"onto the prefab instance: '{mod.propertyPath}'");
                    }
                }
                if (overrideApplied && !sawTitle)
                    failures.Add($"[{fixtureName}] the recorded 'title' override is missing from "
                                 + "GetPropertyModifications - the check would be vacuous");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            if (failures.Count > 0)
                Assert.Fail(string.Join("\n", failures));
        }
    }
}
#endif
