#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof C (first half) - MonoScript GUID stability (Appendix I.7). Lab scenes/prefabs bind
    /// components by their MonoScript GUID; a regenerated .meta GUID during a WS A6 split would silently
    /// null every shipped lab's graph. This pins the GUID of every type referenced by m_Script and
    /// fails if any drifts. SceneManager's GUID is asserted against the load-bearing constant directly
    /// (Appendix B); the rest are pinned by a self-bootstrapping baseline.
    ///
    /// Types are resolved by FullName + matched to their MonoScript by class (no compile-time ref to
    /// ContentDelivery is needed), so this also exercises the same string-resolution contract Proof B pins.
    /// </summary>
    public class ScriptGuidStabilityTests
    {
        // Pinned in Appendix B / I.7 as the load-bearing GUID every lab scene references.
        const string SceneManagerType = "Pitech.XR.Scenario.SceneManager";
        const string SceneManagerGuid = "2d431a49d183e9c428369f7f758f75cd";

        // Every type referenced by m_Script in shipped prefabs/scenes (Appendix I.7).
        static readonly string[] PinnedTypes =
        {
            "Pitech.XR.Scenario.Scenario",
            "Pitech.XR.Scenario.SceneManager",
            "Pitech.XR.Quiz.QuizUIController",
            "Pitech.XR.Quiz.QuizResultsUIController",
            "Pitech.XR.Quiz.QuizAsset",
            "Pitech.XR.Stats.StatsUIController",
            "Pitech.XR.Stats.StatsConfig",
            "Pitech.XR.Interactables.SelectablesManager",
            "Pitech.XR.Interactables.SelectionLists",
            "Pitech.XR.Interactables.SelectableTarget",
            "Pitech.XR.ContentDelivery.ContentDeliverySpawner",
            "Pitech.XR.ContentDelivery.ContentDeliveryStatusOverlay",
        };

        [Test]
        public void SceneManager_KeepsItsLoadBearingScriptGuid()
        {
            var type = ResolveType(SceneManagerType);
            Assert.IsNotNull(type, $"Type '{SceneManagerType}' did not resolve in any loaded assembly.");
            string guid = FindScriptGuid(type);
            Assert.IsNotNull(guid, $"No MonoScript found for {SceneManagerType}.");
            Assert.AreEqual(SceneManagerGuid, guid,
                "SceneManager MonoScript GUID changed - shipped lab scenes bind by this GUID and would be severed.");
        }

        [Test]
        public void AllPinnedScriptGuids_MatchBaseline()
        {
            // Resolve the current map.
            var current = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var unresolved = new List<string>();
            foreach (var typeName in PinnedTypes)
            {
                var type = ResolveType(typeName);
                if (type == null) { unresolved.Add(typeName + " (type not found)"); continue; }
                string guid = FindScriptGuid(type);
                if (string.IsNullOrEmpty(guid)) { unresolved.Add(typeName + " (no MonoScript)"); continue; }
                current[typeName] = guid;
            }
            Assert.IsEmpty(unresolved, "Could not resolve pinned script(s):\n  " + string.Join("\n  ", unresolved));

            // Compare against, or bootstrap, the baseline.
            string baselineAsset = TestPaths.BaselineDir() + "/ScriptGuids.json";
            string baselineDisk = TestPaths.DiskPath(baselineAsset);
            if (baselineDisk == null)
                Assert.Inconclusive("Could not resolve the test package path (TestPaths.BaselineDir()).");

            if (!File.Exists(baselineDisk))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselineDisk));
                File.WriteAllText(baselineDisk, Serialize(current));
                AssetDatabase.ImportAsset(baselineAsset);
                Assert.Inconclusive("Captured Tests/Baseline/ScriptGuids.json. Commit it, then re-run to enforce.");
            }

            var baseline = Deserialize(File.ReadAllText(baselineDisk));
            var drift = new List<string>();
            foreach (var kv in baseline)
            {
                if (!current.TryGetValue(kv.Key, out var nowGuid))
                    drift.Add($"{kv.Key}: present in baseline, not resolvable now");
                else if (nowGuid != kv.Value)
                    drift.Add($"{kv.Key}: baseline {kv.Value} -> now {nowGuid}");
            }
            Assert.IsEmpty(drift, "MonoScript GUID drift (lab bindings would break):\n  " + string.Join("\n  ", drift));
        }

        // ---- helpers ------------------------------------------------------------------------

        static Type ResolveType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static string FindScriptGuid(Type type)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == type)
                    return AssetDatabase.AssetPathToGUID(path);
            }
            return null;
        }

        [Serializable] class Entry { public string type; public string guid; }
        [Serializable] class Map { public List<Entry> entries = new List<Entry>(); }

        static string Serialize(SortedDictionary<string, string> map)
        {
            var m = new Map();
            foreach (var kv in map) m.entries.Add(new Entry { type = kv.Key, guid = kv.Value });
            return JsonUtility.ToJson(m, true);
        }

        static Dictionary<string, string> Deserialize(string json)
        {
            var m = JsonUtility.FromJson<Map>(json) ?? new Map();
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in m.entries) if (e != null && e.type != null) d[e.type] = e.guid;
            return d;
        }
    }
}
#endif
