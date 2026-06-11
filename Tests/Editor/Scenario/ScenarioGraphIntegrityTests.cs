#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof A - the primary net (Appendix I.0). Read-only, runs against the committed fixture prefabs
    /// in Tests/Fixtures/Scenarios. Two assertions per corpus:
    ///   (1) graph integrity INVARIANTS hold (no null entries, unique guids, routes resolve, no dangling
    ///       object refs, no HALF-WIRED UnityEvent listeners - a method named at a missing target, or a
    ///       dangling target. A fully empty listener row is a benign authored placeholder, not a failure);
    ///   (2) the per-lab SNAPSHOT (routing topology + object-ref identities + event fingerprint) matches
    ///       the committed baseline byte-for-byte - catching a dropped or silently rewired ref/listener.
    ///
    /// Bootstrap: a missing baseline is written once and the case reported Inconclusive ("captured -
    /// re-run to enforce"); a deliberate change is re-captured via the Export tool's --regen, reviewed.
    /// With no fixtures yet, both cases are Inconclusive - the net is green-able before the corpus lands
    /// (WS A1 marked the labs) and enforces the moment a fixture is dropped in.
    /// </summary>
    public class ScenarioGraphIntegrityTests
    {
        static List<(string path, GameObject go, Scenario scenario)> LoadFixtures()
        {
            var result = new List<(string, GameObject, Scenario)>();
            string dir = TestPaths.FixturesDir();
            if (string.IsNullOrEmpty(dir) || !AssetDatabase.IsValidFolder(dir))
                return result;

            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var scenario = ScenarioGraphSnapshot.FindScenario(go);
                if (scenario != null)
                    result.Add((path, go, scenario));
            }
            return result;
        }

        static string FixtureName(string assetPath) => Path.GetFileNameWithoutExtension(assetPath);

        [Test]
        public void InvariantsHold_ForEveryFixture()
        {
            var fixtures = LoadFixtures();
            if (fixtures.Count == 0)
                Assert.Inconclusive("No scenario fixtures yet. Run 'Pi tech/Tools/Export Lab as Test Fixture' "
                                    + "on the WS A1 corpus to populate Tests/Fixtures/Scenarios.");

            var failures = new List<string>();
            foreach (var (path, _, scenario) in fixtures)
            {
                var violations = ScenarioGraphSnapshot.CheckInvariants(scenario);
                foreach (var v in violations)
                    failures.Add($"[{FixtureName(path)}] {v}");
            }

            Assert.IsEmpty(failures, "Scenario graph-integrity violations:\n  " + string.Join("\n  ", failures));
        }

        [Test]
        public void Snapshot_MatchesCommittedBaseline_ForEveryFixture()
        {
            var fixtures = LoadFixtures();
            if (fixtures.Count == 0)
                Assert.Inconclusive("No scenario fixtures yet - nothing to snapshot.");

            string snapDir = TestPaths.GraphSnapshotsDir();
            var pendingBootstrap = new List<(string name, string baselineAsset, string json)>();
            var mismatches = new List<string>();

            foreach (var (path, _, scenario) in fixtures)
            {
                string name = FixtureName(path);
                string actual = ScenarioGraphSnapshot.BuildSnapshotJson(scenario);
                string baselineAsset = snapDir + "/" + name + ".graph.json";
                string baselineDisk = TestPaths.DiskPath(baselineAsset);

                if (baselineDisk == null || !File.Exists(baselineDisk))
                {
                    // Do NOT write yet - a run that detects drift in any sibling fixture must not
                    // capture anything (the captured file would encode the demonstrably-drifted
                    // code state as reviewed truth).
                    pendingBootstrap.Add((name, baselineAsset, actual));
                    continue;
                }

                string expected = File.ReadAllText(baselineDisk);
                if (!string.Equals(expected, actual, System.StringComparison.Ordinal))
                    mismatches.Add($"[{name}] graph snapshot drifted from baseline. If intentional, "
                                   + $"re-capture via Export Lab as Test Fixture (--regen) and review the diff.");
            }

            // Reverse direction: a committed baseline whose fixture vanished (deleted/renamed) means
            // reviewed protection silently dropped - fail, don't ignore. (Mirrors the baseline-driven
            // direction checks in ScriptGuidStabilityTests / PublicApiBaselineTests.)
            var fixtureNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var (path, _, _) in fixtures) fixtureNames.Add(FixtureName(path));
            string snapDiskDir = TestPaths.DiskPath(snapDir);
            if (snapDiskDir != null && Directory.Exists(snapDiskDir))
            {
                foreach (var f in Directory.GetFiles(snapDiskDir, "*.graph.json"))
                {
                    string baseName = Path.GetFileName(f);
                    string fixtureName = baseName.Substring(0, baseName.Length - ".graph.json".Length);
                    if (!fixtureNames.Contains(fixtureName))
                        mismatches.Add($"[{fixtureName}] ORPHANED baseline: {baseName} has no matching fixture "
                                       + "prefab. If the fixture was deliberately removed, delete its baseline "
                                       + "in the same commit.");
                }
            }

            Assert.IsEmpty(mismatches, string.Join("\n", mismatches));

            // Only a fully clean run may capture new baselines.
            if (pendingBootstrap.Count > 0)
            {
                Directory.CreateDirectory(snapDiskDir);
                foreach (var (name, baselineAsset, json) in pendingBootstrap)
                {
                    File.WriteAllText(TestPaths.DiskPath(baselineAsset), json);
                    AssetDatabase.ImportAsset(baselineAsset);
                }
                Assert.Inconclusive("Captured baseline snapshot(s) for: "
                                    + string.Join(", ", pendingBootstrap.ConvertAll(p => p.name))
                                    + ". Commit them, then re-run to enforce.");
            }
        }
    }
}
#endif
