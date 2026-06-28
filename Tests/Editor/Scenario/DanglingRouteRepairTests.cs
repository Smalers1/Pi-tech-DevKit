#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// WS B1.6 Step 1 - exercises the dangling-ROUTE lint count and the optional auto-repair on
    /// <see cref="ScenarioGraphSnapshot"/>. A two-step scenario is built IN MEMORY (a bare
    /// GameObject + <see cref="Scenario"/> component, never a committed fixture): step[0].nextGuid
    /// points at a guid that no step owns (dangling), step[1].nextGuid points at step[0] (a valid
    /// route). The tests lock three properties:
    ///   - <see cref="ScenarioGraphSnapshot.CountDanglingRoutes"/> reports EXACTLY the dangling count.
    ///   - <see cref="ScenarioGraphSnapshot.RepairDanglingRoutes"/> clears the dangling route to ""
    ///     while leaving the valid route untouched.
    ///   - the repair is IDEMPOTENT: a second count is 0 and a second repair clears nothing.
    /// Repaired values are read back through a fresh <see cref="SerializedObject"/> - the same lens
    /// the production code mutates - so the assertions never disagree with the serialized state.
    /// </summary>
    public class DanglingRouteRepairTests
    {
        const string DanglingTarget = "no-such-step";

        GameObject host;
        Scenario scenario;
        EventStep stepA;   // dangling nextGuid
        EventStep stepB;   // valid nextGuid -> stepA

        [SetUp]
        public void SetUp()
        {
            // NOT named "Scenario" - Scenario.OnValidate renames a holder with that exact name.
            host = new GameObject("DanglingRouteHost");
            scenario = host.AddComponent<Scenario>();

            stepA = new EventStep { nextGuid = DanglingTarget };
            stepB = new EventStep();
            scenario.steps.Add(stepA);
            scenario.steps.Add(stepB);

            // Step.guid is assigned by the constructor; backfill defensively in case it was empty.
            if (string.IsNullOrEmpty(stepA.guid)) stepA.guid = System.Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(stepB.guid)) stepB.guid = System.Guid.NewGuid().ToString();

            // The valid route: stepB points at stepA. stepA's route stays dangling.
            stepB.nextGuid = stepA.guid;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            host = null;
            scenario = null;
            stepA = null;
            stepB = null;
        }

        // Reads a step's nextGuid through a fresh SerializedObject - the lens RepairDanglingRoutes
        // writes through - so assertions reflect the serialized truth, not a stale managed snapshot.
        static string SerializedNextGuid(Scenario s, int stepIndex)
        {
            var so = new SerializedObject(s);
            var pr = so.FindProperty($"steps.Array.data[{stepIndex}].nextGuid");
            Assert.IsNotNull(pr, $"steps[{stepIndex}].nextGuid not found in serialized form");
            return pr.stringValue;
        }

        [Test]
        public void CountDanglingRoutes_CountsOnlyTheDanglingNextGuid()
        {
            // Non-vacuity: the valid route really points at an existing step before we count.
            Assert.AreEqual(stepA.guid, SerializedNextGuid(scenario, 1),
                "setup precondition: step[1].nextGuid must point at step[0]'s guid");

            Assert.AreEqual(1, ScenarioGraphSnapshot.CountDanglingRoutes(scenario),
                "exactly one route (step[0].nextGuid -> a non-existent step) is dangling");
        }

        [Test]
        public void RepairDanglingRoutes_ClearsDanglingRoute_AndLeavesValidRouteUntouched()
        {
            int cleared = ScenarioGraphSnapshot.RepairDanglingRoutes(scenario);
            Assert.AreEqual(1, cleared, "exactly one dangling route should be cleared");

            // The dangling route is now empty (fall-through to next step in list).
            Assert.AreEqual(string.Empty, SerializedNextGuid(scenario, 0),
                "the dangling route must be cleared to an empty string");

            // The valid route is untouched.
            Assert.AreEqual(stepA.guid, SerializedNextGuid(scenario, 1),
                "the valid route must be left exactly as it was");
        }

        [Test]
        public void RepairDanglingRoutes_IsIdempotent()
        {
            int first = ScenarioGraphSnapshot.RepairDanglingRoutes(scenario);
            Assert.AreEqual(1, first, "first repair clears the single dangling route");

            // After repair, nothing is dangling any more.
            Assert.AreEqual(0, ScenarioGraphSnapshot.CountDanglingRoutes(scenario),
                "after repair there must be no dangling routes left");

            string clearedRoute = SerializedNextGuid(scenario, 0);
            string validRoute = SerializedNextGuid(scenario, 1);

            // A second repair is a no-op: count returns 0, and no value moves.
            int second = ScenarioGraphSnapshot.RepairDanglingRoutes(scenario);
            Assert.AreEqual(0, second, "a second repair must clear nothing (idempotent)");

            Assert.AreEqual(clearedRoute, SerializedNextGuid(scenario, 0),
                "the already-cleared route must not change on a second repair");
            Assert.AreEqual(validRoute, SerializedNextGuid(scenario, 1),
                "the valid route must not change on a second repair");
        }
    }
}
#endif
