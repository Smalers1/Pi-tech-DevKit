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
    /// Shared corpus access for the per-lab parametrized Proof A/C tests. Discovery is recursive WITHIN
    /// Tests/Fixtures/Scenarios only (sibling folders - LegacyForms/, Assets/ - are invisible to the
    /// green gate by design). Each committed fixture prefab becomes one NUnit case named by its file
    /// stem, so the gate reports pass/fail/skip PER LAB instead of one monolithic node per check.
    ///
    /// Step 12 skip (spec §7.1) is applied per case: a fixture whose committed deps declaration
    /// (Tests/Baseline/FixtureDeps/&lt;name&gt;.deps.json) names &gt;=1 dependency that does not resolve
    /// in THIS project is SKIPPED loudly via Assert.Inconclusive - never inferred from a failure, never
    /// counted green. The self-contained synthetic corpus (mega/variant) carries no declaration, so it
    /// is always enforced. In-test baseline auto-capture was retired with the parametrization: a missing
    /// baseline is a per-lab Inconclusive that points at the export tool (the only sanctioned capture
    /// path), which keeps capture out of a multi-case run where ordering would make it unsafe.
    /// </summary>
    internal static class FixtureCorpus
    {
        // Empty-corpus sentinel: the parametrized source yields one case with this arg so the tests
        // report a single deterministic Inconclusive rather than relying on the Test Framework's
        // (version-dependent) empty-source handling.
        public const string NoneSentinel = "";

        /// <summary>One TestCaseData per committed fixture (file stem), sorted ordinally for a stable
        /// run order. Empty corpus -> a single sentinel case.</summary>
        public static IEnumerable<TestCaseData> Cases()
        {
            var names = AllFixtureNames();
            if (names.Count == 0)
            {
                yield return new TestCaseData(NoneSentinel);
                yield break;
            }
            foreach (var n in names)
                yield return new TestCaseData(n);
        }

        /// <summary>Every discovered fixture file stem under Tests/Fixtures/Scenarios, sorted ordinally.
        /// Used by the suite-level orphan check (which compares against committed baselines + deps).</summary>
        public static List<string> AllFixtureNames()
        {
            var names = new List<string>();
            string dir = TestPaths.FixturesDir();
            if (string.IsNullOrEmpty(dir) || !AssetDatabase.IsValidFolder(dir))
                return names;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && ScenarioGraphSnapshot.FindScenario(go) != null)
                    names.Add(Path.GetFileNameWithoutExtension(path));
            }
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        public static string PathFor(string fixtureName)
        {
            string dir = TestPaths.FixturesDir();
            return dir == null ? null : dir + "/" + fixtureName + ".prefab";
        }

        /// <summary>First line of every per-lab test: the empty-corpus sentinel reports Inconclusive
        /// (the corpus is genuinely absent), so the run is never silently green with nothing tested.</summary>
        public static void RequireFixture(string fixtureName)
        {
            if (string.IsNullOrEmpty(fixtureName))
                Assert.Inconclusive("No scenario fixtures discovered under Tests/Fixtures/Scenarios. "
                                    + "Generate the synthetic corpus and export the labs (see Corpus_IsPresent).");
        }

        /// <summary>Load the fixture prefab + its Scenario, or Assert.Fail if it cannot be resolved
        /// (a discovered fixture must always load).</summary>
        public static (string path, GameObject go, Scenario scenario) Resolve(string fixtureName)
        {
            string path = PathFor(fixtureName);
            var go = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Assert.Fail($"[{fixtureName}] fixture prefab did not load at '{path}'.");
                return (path, null, null);
            }
            var scenario = ScenarioGraphSnapshot.FindScenario(go);
            if (scenario == null)
                Assert.Fail($"[{fixtureName}] no Scenario component on the fixture prefab.");
            return (path, go, scenario);
        }

        /// <summary>Step 12 skip (spec §7.1.2): if the fixture's committed declaration names &gt;=1 GUID
        /// that does not resolve in THIS project, report Inconclusive (skipped) and stop the case. No or
        /// empty declaration -> returns, so the case is enforced in full. The message is intentionally a
        /// SINGLE compact line (count + one example + where the full list lives) - the enumerated paths
        /// are huge and live in the committed .deps.json; the window only needs the reason.</summary>
        public static void SkipIfUnmetDeps(string fixtureName, string path)
        {
            var unmet = FixtureDependencies.UnmetDependencies(path);
            if (unmet.Count == 0) return;
            string sample = SampleName(unmet[0]);
            Assert.Inconclusive($"SKIPPED - {unmet.Count} declared external "
                                + (unmet.Count == 1 ? "dependency" : "dependencies") + " not present in this project"
                                + (sample != null ? $" (e.g. {sample})" : "")
                                + ". Enforce in HealthOn VR (tier 2); full list in "
                                + $"Tests/Baseline/FixtureDeps/{fixtureName}.deps.json.");
        }

        // "Assets/.../r_hand_fist_anim.fbx (005ab0...)" -> "r_hand_fist_anim.fbx" for a one-word hint.
        static string SampleName(string unmetEntry)
        {
            if (string.IsNullOrEmpty(unmetEntry)) return null;
            int paren = unmetEntry.IndexOf(" (", System.StringComparison.Ordinal);
            string p = (paren > 0 ? unmetEntry.Substring(0, paren) : unmetEntry).Trim();
            string name = Path.GetFileName(p);
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }
}
#endif
