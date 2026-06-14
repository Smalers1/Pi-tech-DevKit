#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;
using Scene = UnityEngine.SceneManagement.Scene;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Mega-fixture spec §4.4 (D4) - "the detector actually detects". Five poison graphs are built
    /// IN MEMORY in a preview scene (v3: no committed prefabs, no KnownBad/ folder, no hand-tuned
    /// rid tables, no import-layer uncertainty) and each must make
    /// <see cref="ScenarioGraphSnapshot.CheckInvariants"/> report THAT specific violation; a clean
    /// 3-step scenario carrying the two benign listener shapes must report ZERO. A net never proven
    /// to fire is faith, not proof - and a net that fires on the benign shapes real labs ship is
    /// over-tightening; both directions are locked here.
    ///
    /// Two binding rules:
    ///   - NON-VACUITY: the dangling-ref poisons assert the serialized state really is dangling
    ///     (resolves null AND objectReferenceInstanceIDValue != 0) BEFORE running the detector -
    ///     if Unity ever stops preserving the dead instance id, these tests FAIL loudly instead of
    ///     silently passing on a clean-null that proves nothing.
    ///   - LOOSE MESSAGE MATCH: assertions check the field locator + violation kind via
    ///     case-insensitive fragments, never exact wording - WS A9 may rewrite the messages.
    ///
    /// These tests NEVER consult FixtureDependencies (spec §7.1.4 hard rule): the poisons are
    /// self-contained scene objects with nothing to skip on - they enforce in every project.
    /// </summary>
    public class InvariantDetectionTests
    {
        // Persistent-call rows live at <event>.m_PersistentCalls.m_Calls.Array.data[i] - the exact
        // path shape CheckInvariants' classifiers key on.
        const string Step0OnEnterRow0 = "steps.Array.data[0].onEnter.m_PersistentCalls.m_Calls.Array.data[0]";
        const string Step1OnEnterCalls = "steps.Array.data[1].onEnter.m_PersistentCalls.m_Calls";

        // ---- shared preview-scene plumbing ----------------------------------------------------

        // Every case runs inside its own preview scene so the user's open scene is never touched or
        // dirtied; ClosePreviewScene (in finally) destroys everything the case created.
        static void InPreviewScene(System.Action<Scene> test)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                test(previewScene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        static GameObject NewGameObjectIn(Scene previewScene, string name)
        {
            // new GameObject lands in the active scene; move it immediately so nothing ever leaks
            // into (or dirties) whatever scene the developer has open.
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, previewScene);
            return go;
        }

        static Scenario AddScenarioHost(Scene previewScene)
        {
            // NOT named "Scenario" - Scenario.OnValidate renames a holder with that exact name.
            return NewGameObjectIn(previewScene, "PoisonHost").AddComponent<Scenario>();
        }

        // ---- assertion helpers ----------------------------------------------------------------

        // NON-VACUITY guard for the dangling poisons. If the property is absent or the reference is
        // anything other than genuinely dangling (instanceID != 0, resolves null), the test FAILS -
        // a clean-null here would make the detector assertion below prove nothing.
        static void AssertDangling(Scenario scenario, string propertyPath)
        {
            var so = new SerializedObject(scenario);
            var prop = so.FindProperty(propertyPath);
            Assert.IsNotNull(prop,
                $"Serialized property '{propertyPath}' not found - the poison was not built where this test expects it.");
            Assert.IsTrue(prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0,
                $"NON-VACUITY: '{propertyPath}' must be DANGLING (resolves null, instanceID != 0) for this test "
                + $"to prove anything. Got resolves-null={prop.objectReferenceValue == null}, "
                + $"instanceID={prop.objectReferenceInstanceIDValue}.");
        }

        static string AssertSingleViolation(List<string> violations)
        {
            Assert.AreEqual(1, violations.Count,
                "Expected exactly ONE violation, got " + violations.Count
                + (violations.Count == 0
                    ? " - the detector did not fire on the poison."
                    : ":\n  " + string.Join("\n  ", violations)));
            return violations[0];
        }

        // Loose, case-insensitive fragment match: at least one alternative must appear. Keeps the
        // assertions stable across WS A9's future message rewrites while still pinning semantics.
        static void AssertMentionsAny(string violation, params string[] anyOf)
        {
            foreach (var token in anyOf)
                if (violation.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
            Assert.Fail($"Violation text should mention '{string.Join("' or '", anyOf)}' (loose match - "
                + $"wording may evolve), but was:\n  {violation}");
        }

        // ---- the five poisons -------------------------------------------------------------------

        [Test]
        public void DanglingListenerTarget_IsReportedAsTheOneListenerViolation()
        {
            InPreviewScene(scene =>
            {
                var scenario = AddScenarioHost(scene);
                var step = new EventStep();
                scenario.steps.Add(step);

                // Wire a real persistent listener (SetActive(true) on a second scene object) through
                // the same editor API authors use, then destroy the target: assigned-then-broken is
                // THE one genuine listener violation under the dangling-only rule.
                var target = NewGameObjectIn(scene, "Listener target");
                UnityEventTools.AddBoolPersistentListener(step.onEnter, target.SetActive, true);
                Object.DestroyImmediate(target);

                AssertDangling(scenario, Step0OnEnterRow0 + ".m_Target");

                string v = AssertSingleViolation(ScenarioGraphSnapshot.CheckInvariants(scenario));
                AssertMentionsAny(v, "onEnter");
                AssertMentionsAny(v, "listener");
                AssertMentionsAny(v, "missing");
            });
        }

        [Test]
        public void BrokenRoute_IsReported()
        {
            InPreviewScene(scene =>
            {
                var scenario = AddScenarioHost(scene);
                var step = new EventStep { nextGuid = "no-such-step" };
                scenario.steps.Add(step);

                string v = AssertSingleViolation(ScenarioGraphSnapshot.CheckInvariants(scenario));
                AssertMentionsAny(v, "connection", "route");
                // The id fragment survives Shorten() (first 8 chars are always kept).
                AssertMentionsAny(v, "no-such-");
            });
        }

        [Test]
        public void BlankStepSlot_IsReported()
        {
            InPreviewScene(scene =>
            {
                var scenario = AddScenarioHost(scene);
                scenario.steps.Add(new EventStep());
                // Stable poison: OnValidate's no-null-strip guard never removes a null slot.
                scenario.steps.Add(null);

                string v = AssertSingleViolation(ScenarioGraphSnapshot.CheckInvariants(scenario));
                AssertMentionsAny(v, "blank", "empty");
            });
        }

        [Test]
        public void DuplicateStepGuid_IsReported()
        {
            InPreviewScene(scene =>
            {
                const string sharedGuid = "dup-guid";   // <= 12 chars, so it appears unshortened

                var scenario = AddScenarioHost(scene);
                var a = new EventStep();
                var b = new EventStep();
                // EnsureGuidsRecursive backfills EMPTY guids only - it never dedupes, so this sticks.
                a.guid = sharedGuid;
                b.guid = sharedGuid;
                scenario.steps.Add(a);
                scenario.steps.Add(b);

                string v = AssertSingleViolation(ScenarioGraphSnapshot.CheckInvariants(scenario));
                AssertMentionsAny(v, "share", "duplicat", "same");
                AssertMentionsAny(v, sharedGuid);
            });
        }

        [Test]
        public void DanglingStepObjectRef_IsReported()
        {
            InPreviewScene(scene =>
            {
                var scenario = AddScenarioHost(scene);
                var step = new InsertStep();
                scenario.steps.Add(step);

                // Assign a real scene Transform to a step field, then destroy its GameObject - the
                // non-listener dangling-ref poison.
                var itemGo = NewGameObjectIn(scene, "Insert item");
                step.item = itemGo.transform;
                Object.DestroyImmediate(itemGo);

                AssertDangling(scenario, "steps.Array.data[0].item");

                string v = AssertSingleViolation(ScenarioGraphSnapshot.CheckInvariants(scenario));
                AssertMentionsAny(v, "item");
                AssertMentionsAny(v, "missing");
            });
        }

        // ---- the negative control ---------------------------------------------------------------

        // Guards against a detector that fires on everything AND locks the two benign listener
        // shapes (clean-null-target-WITH-method - the Delirium/Ekpa detritus - and the fully-empty
        // row) as benign: a future invariant tightening that flags either turns this red.
        [Test]
        public void CleanScenario_WithBenignListenerShapes_HasZeroViolations()
        {
            InPreviewScene(scene =>
            {
                var scenario = AddScenarioHost(scene);
                var a = new EventStep();
                var b = new EventStep();
                var c = new EventStep();
                scenario.steps.Add(a);
                scenario.steps.Add(b);
                scenario.steps.Add(c);

                // Fully wired routes; the final step falls through.
                a.nextGuid = b.guid;
                b.nextGuid = c.guid;
                c.nextGuid = "";

                // Benign shape 1 on step a: clean-null target WITH a method. Author the row through
                // the real API (so method/mode/type-name are Unity's own), then clear the target via
                // SerializedObject - assigning null writes instanceID 0, a CLEAN null, never dangling.
                var benignTarget = NewGameObjectIn(scene, "Benign target");   // stays alive
                UnityEventTools.AddBoolPersistentListener(a.onEnter, benignTarget.SetActive, false);

                var so = new SerializedObject(scenario);
                var row0Target = so.FindProperty(Step0OnEnterRow0 + ".m_Target");
                Assert.IsNotNull(row0Target, "listener row was not authored where this test expects it");
                row0Target.objectReferenceValue = null;

                // Benign shape 2 on step b: a fully-empty row (no target, no method).
                var bCalls = so.FindProperty(Step1OnEnterCalls);
                Assert.IsNotNull(bCalls, "step 1 has no persistent-call list");
                bCalls.arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Non-vacuity of the CONTROL: both benign rows must really exist in serialized form
                // (fresh SerializedObject - apply may have rebuilt the managed graph) or the zero-
                // violation assert below proves nothing.
                var check = new SerializedObject(scenario);
                var cleanRow = check.FindProperty(Step0OnEnterRow0);
                Assert.IsNotNull(cleanRow, "benign clean-null row missing from serialized state");
                Assert.AreEqual("SetActive", cleanRow.FindPropertyRelative("m_MethodName").stringValue,
                    "the clean-null row must keep its method - that IS the shipped detritus shape");
                var cleanTarget = cleanRow.FindPropertyRelative("m_Target");
                Assert.IsTrue(cleanTarget.objectReferenceValue == null
                              && cleanTarget.objectReferenceInstanceIDValue == 0,
                    "the benign row's target must be a CLEAN null (instanceID 0), not dangling");
                var emptyRow = check.FindProperty(Step1OnEnterCalls + ".Array.data[0]");
                Assert.IsNotNull(emptyRow, "benign fully-empty row missing from serialized state");
                Assert.IsTrue(string.IsNullOrEmpty(emptyRow.FindPropertyRelative("m_MethodName").stringValue),
                    "the fully-empty row must have no method");
                var emptyTarget = emptyRow.FindPropertyRelative("m_Target");
                Assert.IsTrue(emptyTarget.objectReferenceValue == null
                              && emptyTarget.objectReferenceInstanceIDValue == 0,
                    "the fully-empty row's target must be a clean null");

                var violations = ScenarioGraphSnapshot.CheckInvariants(scenario);
                Assert.AreEqual(0, violations.Count,
                    "A clean scenario carrying the benign listener shapes must produce ZERO violations "
                    + "(fires-on-everything / over-tightening guard), got:\n  "
                    + string.Join("\n  ", violations));
            });
        }
    }
}
#endif
