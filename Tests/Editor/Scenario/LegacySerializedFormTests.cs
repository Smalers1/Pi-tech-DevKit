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
    /// The "old serialized states" net (mega-fixture spec §4.3, per D4). Loads BOTH LegacyForms twins
    /// by explicit path - Tests/Fixtures/LegacyForms is a SIBLING of Fixtures/Scenarios, invisible to
    /// fixture discovery BY DESIGN, so these prefabs never enter the green gate - and proves the only
    /// [FormerlySerializedAs] surface in the model still lands:
    ///   (1) the legacy twin's pre-rename YAML (quiz:/quizUI:/quizResultsUI:) deserializes into the
    ///       NEW SceneManager members (defaultQuiz/quizPanel/quizResultsPanel);
    ///   (2) the three editor-only Scenario lists, whose YAML lines the legacy twin strips
    ///       (pre-448301b form), default cleanly to present-but-empty;
    ///   (3) BuildSnapshotJson(legacy) == BuildSnapshotJson(current) - same graph, two serialized
    ///       generations - compared IN MEMORY only. No .graph.json is ever committed for the twins:
    ///       a snapshot under Tests/Baseline/GraphSnapshots would trip the orphaned-baseline check.
    ///
    /// Discipline (enforced by the guard test): this file only READS the twins and NEVER re-saves or
    /// reserializes them - any rewrite would replace the legacy field names with the current ones and
    /// silently evaporate this coverage. For the same reason the future CI ForceReserializeAssets
    /// byte-backstop is scoped to Tests/Fixtures/Scenarios/ ONLY (§4.3 CI scope rule); a wider sweep
    /// would rewrite the legacy names. These tests NEVER consult FixtureDependencies (§7.1.4
    /// never-skip set) - the twins are package-internal by construction and always enforced.
    /// </summary>
    public class LegacySerializedFormTests
    {
        // Twin asset paths, pinned by the spec (§4.3): generated together with the mega, by the same
        // tool run; the legacy twin is derived textually from the saved current twin.
        const string LegacyFormsFolder = "Fixtures/LegacyForms";
        const string LegacyPrefabName = "legacy_form";
        const string CurrentPrefabName = "legacy_form_current";

        // ---- twin loading (bootstrap-tolerant, read-only) -----------------------------------

        static string TwinPath(string prefabName)
        {
            string root = TestPaths.TestsRoot();
            return root == null ? null : root + "/" + LegacyFormsFolder + "/" + prefabName + ".prefab";
        }

        // Both prefabs, loaded by explicit path (never via fixture discovery). Missing either twin is
        // the net's standard bootstrap state, not a failure: Inconclusive until the generator has run.
        static (string legacyPath, GameObject legacy, string currentPath, GameObject current) LoadTwins()
        {
            string legacyPath = TwinPath(LegacyPrefabName);
            string currentPath = TwinPath(CurrentPrefabName);

            var legacy = legacyPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath);
            var current = currentPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(currentPath);

            if (legacy == null || current == null)
                Assert.Inconclusive("LegacyForms twins not generated yet - run Pi tech/Tools/Generate "
                                    + "Synthetic Scenario Fixture, commit, re-run.");
            return (legacyPath, legacy, currentPath, current);
        }

        // "QuizUIController 'Quiz Panel'" - identity by referenced-object name + type. The twins are
        // different assets, so instance identity can never match; name + type is the honest contract.
        static string Describe(Object o) => o.GetType().FullName + " '" + o.name + "'";

        static void AssertSameReference(string field, Object legacyValue, Object currentValue)
        {
            // Vacuity guard first: a null on the CURRENT twin means the generator stopped wiring the
            // field and this whole check proves nothing - fail loudly rather than pass on two nulls.
            Assert.IsTrue(currentValue != null,
                $"Current twin's '{field}' is null - the generator no longer wires it, so the "
                + "[FormerlySerializedAs] mapping check would be vacuous. Fix the generator first.");
            Assert.IsTrue(legacyValue != null,
                $"Legacy twin's '{field}' is null - the pre-rename YAML name did not land in the new "
                + "member, i.e. the [FormerlySerializedAs] mapping on SceneManager is broken.");
            Assert.AreEqual(Describe(currentValue), Describe(legacyValue),
                $"'{field}' resolved to a different object on the legacy twin than on the current twin.");
        }

        // ---- (2 in the spec) FSA mapping: old YAML names land in the new members -------------

        [Test]
        public void FormerlySerializedAs_MapsLegacyQuizFields_OntoNewMembers()
        {
            var (_, legacy, _, current) = LoadTwins();

            var legacySm = legacy.GetComponentInChildren<SceneManager>(true);
            var currentSm = current.GetComponentInChildren<SceneManager>(true);
            Assert.IsTrue(legacySm != null, "No SceneManager on the legacy twin.");
            Assert.IsTrue(currentSm != null, "No SceneManager on the current twin.");

            // The three renames are the model's entire [FormerlySerializedAs] surface (spec §1.11):
            // quiz -> defaultQuiz, quizUI -> quizPanel, quizResultsUI -> quizResultsPanel.
            AssertSameReference("defaultQuiz", legacySm.defaultQuiz, currentSm.defaultQuiz);
            AssertSameReference("quizPanel", legacySm.quizPanel, currentSm.quizPanel);
            AssertSameReference("quizResultsPanel", legacySm.quizResultsPanel, currentSm.quizResultsPanel);
        }

        // ---- (3) absent editor-only YAML lines default cleanly -------------------------------

        [Test]
        public void LegacyTwin_EditorOnlyLists_ArePresentButEmpty()
        {
            var (_, legacy, _, _) = LoadTwins();
            var scenario = ScenarioGraphSnapshot.FindScenario(legacy);
            Assert.IsTrue(scenario != null, "No Scenario on the legacy twin.");

            // The legacy twin strips the three editor-only list lines from its YAML; on load each must
            // come back as the field's default - a present, empty list - never a deserialization error.
            // The fields are private [SerializeField], so assert through SerializedObject.
            var so = new SerializedObject(scenario);
            foreach (var field in new[] { "graphNotes", "graphGroups", "stepGraphDisplays" })
            {
                var p = so.FindProperty(field);
                Assert.IsNotNull(p, $"'{field}' is not a serialized property on Scenario - the "
                                    + "editor-only side-lists moved or were renamed; update this test "
                                    + "and the legacy-twin derivation together.");
                Assert.IsTrue(p.isArray, $"'{field}' no longer serializes as a list.");
                Assert.AreEqual(0, p.arraySize,
                    $"'{field}' should default to empty when its YAML line is absent (pre-448301b "
                    + $"form) - the legacy twin deserialized {p.arraySize} entr(y/ies).");
            }
        }

        // ---- (4) snapshot equivalence: same graph, two serialized generations ----------------

        [Test]
        public void GraphSnapshot_LegacyEqualsCurrent_InMemory()
        {
            var (_, legacy, _, current) = LoadTwins();

            var legacyScenario = ScenarioGraphSnapshot.FindScenario(legacy);
            var currentScenario = ScenarioGraphSnapshot.FindScenario(current);
            Assert.IsTrue(legacyScenario != null, "No Scenario on the legacy twin.");
            Assert.IsTrue(currentScenario != null, "No Scenario on the current twin.");

            // Built and compared in memory only (ordinal; NUnit reports the divergence index). Never
            // write either JSON under Tests/Baseline/GraphSnapshots - the orphaned-baseline check
            // fails any snapshot there without a matching fixture in Fixtures/Scenarios.
            string currentJson = ScenarioGraphSnapshot.BuildSnapshotJson(currentScenario);
            string legacyJson = ScenarioGraphSnapshot.BuildSnapshotJson(legacyScenario);

            Assert.AreEqual(currentJson, legacyJson,
                "The legacy twin's graph snapshot differs from the current twin's - the pre-rename "
                + "serialized form no longer deserializes to the same graph. If a deliberate model "
                + "change caused this, regenerate the twins via Pi tech/Tools/Generate Synthetic "
                + "Scenario Fixture in the same change.");
        }

        // ---- (5) guard: read-only loads only - this file must never rewrite the twins --------

        [Test]
        public void ReadPaths_NeverDirtyOrRewrite_EitherTwin()
        {
            var (legacyPath, legacy, currentPath, current) = LoadTwins();

            // Committed bytes BEFORE exercising every read path the tests above use.
            string legacyBefore = File.ReadAllText(TestPaths.DiskPath(legacyPath));
            string currentBefore = File.ReadAllText(TestPaths.DiskPath(currentPath));

            // Exercise the exact read surface of this file: component lookups, public-field reads,
            // SerializedObject walks (snapshot + FindProperty). All are read-only by design; this
            // test is the teeth behind that claim. Deliberately NO SaveAssets/ForceReserializeAssets
            // anywhere in this file - flushing a dirtied twin would rewrite the legacy field names
            // and silently evaporate the coverage (the §4.3 CI scope rule exists for the same reason).
            var legacyScenario = ScenarioGraphSnapshot.FindScenario(legacy);
            var currentScenario = ScenarioGraphSnapshot.FindScenario(current);
            if (legacyScenario != null) ScenarioGraphSnapshot.BuildSnapshotJson(legacyScenario);
            if (currentScenario != null) ScenarioGraphSnapshot.BuildSnapshotJson(currentScenario);

            var legacySm = legacy.GetComponentInChildren<SceneManager>(true);
            if (legacySm != null)
            {
                _ = legacySm.defaultQuiz;
                _ = legacySm.quizPanel;
                _ = legacySm.quizResultsPanel;
            }
            if (legacyScenario != null)
            {
                var so = new SerializedObject(legacyScenario);
                so.FindProperty("graphNotes");
                so.FindProperty("graphGroups");
                so.FindProperty("stepGraphDisplays");
            }

            // Nothing may be dirty in memory (a later editor save would flush a dirty asset to disk)...
            var dirty = new List<string>();
            if (EditorUtility.IsDirty(legacy)) dirty.Add(LegacyPrefabName + " (root)");
            if (EditorUtility.IsDirty(current)) dirty.Add(CurrentPrefabName + " (root)");
            if (legacyScenario != null && EditorUtility.IsDirty(legacyScenario)) dirty.Add(LegacyPrefabName + " (Scenario)");
            if (currentScenario != null && EditorUtility.IsDirty(currentScenario)) dirty.Add(CurrentPrefabName + " (Scenario)");
            if (legacySm != null && EditorUtility.IsDirty(legacySm)) dirty.Add(LegacyPrefabName + " (SceneManager)");
            Assert.IsEmpty(dirty, "Read paths dirtied the LegacyForms twin(s) - a later save would "
                                  + "rewrite the legacy serialized form:\n  " + string.Join("\n  ", dirty));

            // ...and the committed bytes must be untouched on disk.
            Assert.IsTrue(string.Equals(legacyBefore, File.ReadAllText(TestPaths.DiskPath(legacyPath)),
                              System.StringComparison.Ordinal),
                "legacy_form.prefab bytes changed during a read-only test run.");
            Assert.IsTrue(string.Equals(currentBefore, File.ReadAllText(TestPaths.DiskPath(currentPath)),
                              System.StringComparison.Ordinal),
                "legacy_form_current.prefab bytes changed during a read-only test run.");
        }
    }
}
#endif
