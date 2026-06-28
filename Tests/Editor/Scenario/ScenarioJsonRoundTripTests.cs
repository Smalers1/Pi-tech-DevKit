#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Tier-2 round-trip proof for the portable scenario JSON (Lane C): export the mega fixture's step
    /// graph via <see cref="ScenarioJsonExporter.ToJson"/>, import it onto a FRESH scenario via
    /// <see cref="ScenarioJsonImporter.Apply"/>, and assert the portable surface survived intact:
    ///   (1) step count matches (top level + nested, recursively);
    ///   (2) the per-step CLR short-type sequence matches (recursively, including GroupStep children);
    ///   (3) the ROUTING map produced by <see cref="ScenarioGraphSnapshot.BuildSnapshotJson"/> is
    ///       byte-identical between the imported scenario and the source.
    /// Routing is fully in scope, so (3) is the load-bearing assertion - a dropped or mis-pathed
    /// nextGuid/specificStepGuid/childRequirement.guid anywhere in the graph fails it. The objectRefs /
    /// events maps are deliberately NOT compared: object references and UnityEvents are out of the
    /// portable scope and are expected to differ (the import leaves them at defaults).
    ///
    /// The committed fixture is never mutated: export reads from an INSTANTIATED copy, and import targets
    /// a brand-new GameObject. Every created object is destroyed in <see cref="TearDown"/>.
    /// </summary>
    public class ScenarioJsonRoundTripTests
    {
        const string MegaFixtureName = "mega_fixture";

        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void MegaFixture_PortableJson_RoundTrips()
        {
            string path = FixtureCorpus.PathFor(MegaFixtureName);
            var asset = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                Assert.Inconclusive($"Mega fixture not found at '{path}'. Generate the synthetic corpus "
                                    + "(Pi tech/Tools/Generate Synthetic Scenario Fixture).");

            // Export from an instantiated COPY so the committed asset is never touched.
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            _created.Add(copy);
            var source = ScenarioGraphSnapshot.FindScenario(copy);
            Assert.IsNotNull(source, "No Scenario on the instantiated mega fixture copy.");

            string json = ScenarioJsonExporter.ToJson(source);
            Assert.IsFalse(string.IsNullOrEmpty(json), "Exporter produced empty JSON.");

            // Import onto a brand-new scenario.
            var freshGo = new GameObject("ScenarioJsonRoundTrip_Imported");
            _created.Add(freshGo);
            var imported = freshGo.AddComponent<Scenario>();
            ScenarioJsonImporter.Apply(json, imported);

            // (1) step count (recursive).
            int srcCount = CountSteps(source.steps);
            int impCount = CountSteps(imported.steps);
            Assert.AreEqual(srcCount, impCount,
                $"Step count differs after round-trip: source={srcCount}, imported={impCount}.");

            // (2) per-step CLR type sequence (recursive).
            var srcTypes = new List<string>();
            var impTypes = new List<string>();
            CollectTypeSequence(source.steps, srcTypes);
            CollectTypeSequence(imported.steps, impTypes);
            CollectionAssert.AreEqual(srcTypes, impTypes,
                "Per-step CLR type sequence differs after round-trip.\n"
                + "source:   " + string.Join(", ", srcTypes) + "\n"
                + "imported: " + string.Join(", ", impTypes));

            // (3) routing map identical (the load-bearing assertion).
            string srcRouting = RoutingSection(ScenarioGraphSnapshot.BuildSnapshotJson(source));
            string impRouting = RoutingSection(ScenarioGraphSnapshot.BuildSnapshotJson(imported));
            Assert.AreEqual(srcRouting, impRouting,
                "Routing map differs after round-trip (a nextGuid/specificStepGuid/childRequirement.guid "
                + "was dropped, added, or mis-pathed).");
        }

        // ---- helpers -------------------------------------------------------------------------

        static int CountSteps(List<Step> steps)
        {
            if (steps == null) return 0;
            int n = 0;
            foreach (var s in steps)
            {
                if (s == null) continue;   // exporter skips null slots; the rebuilt list has none
                n++;
                if (s is GroupStep g) n += CountSteps(g.steps);
            }
            return n;
        }

        static void CollectTypeSequence(List<Step> steps, List<string> into)
        {
            if (steps == null) return;
            foreach (var s in steps)
            {
                if (s == null) continue;
                into.Add(s.GetType().Name);
                if (s is GroupStep g) CollectTypeSequence(g.steps, into);
            }
        }

        // Extract just the "routing": { ... } object from a BuildSnapshotJson document. The format is
        // deterministic (ScenarioGraphSnapshot.AppendMap), with "objectRefs" immediately following
        // "routing" - so the routing block is the text from the "routing" key up to the "objectRefs" key.
        // Comparing this slice isolates routing (in scope) from objectRefs/events (out of scope).
        static string RoutingSection(string snapshotJson)
        {
            Assert.IsNotNull(snapshotJson, "BuildSnapshotJson returned null.");
            int start = snapshotJson.IndexOf("\"routing\":", System.StringComparison.Ordinal);
            int next = snapshotJson.IndexOf("\"objectRefs\":", System.StringComparison.Ordinal);
            Assert.Greater(start, -1, "Snapshot JSON has no routing section.");
            Assert.Greater(next, start, "Snapshot JSON has no objectRefs section after routing.");
            return snapshotJson.Substring(start, next - start);
        }
    }
}
#endif
