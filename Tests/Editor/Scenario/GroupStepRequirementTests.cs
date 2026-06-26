using System.Collections.Generic;
using NUnit.Framework;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Locks GroupStep's child-requirement bookkeeping on UNMODIFIED code (WS A3 Step 2): the helpers
    /// the runner (LabConsole) and the inspector/graph rely on for Required / N-of-M / MultiCondition
    /// completion. Pure data logic - no scene, no serialization. The Step base ctor seeds each step a
    /// fresh guid, so nested steps are valid by construction.
    /// </summary>
    public class GroupStepRequirementTests
    {
        static GroupStep GroupWith(params Step[] children)
        {
            var g = new GroupStep();
            g.steps = new List<Step>(children);
            return g;
        }

        [Test]
        public void EnsureChildRequirements_AddsOneRequiredEntryPerChild()
        {
            var a = new TimelineStep();
            var b = new EventStep();
            var g = GroupWith(a, b);

            g.EnsureChildRequirements();

            Assert.AreEqual(2, g.childRequirements.Count);
            Assert.IsTrue(g.IsChildRequired(a.guid));
            Assert.IsTrue(g.IsChildRequired(b.guid));
        }

        [Test]
        public void EnsureChildRequirements_AssignsGuidToChildMissingOne()
        {
            var a = new TimelineStep();
            a.guid = "";                 // simulate a freshly-deserialized child with no guid yet
            var g = GroupWith(a);

            g.EnsureChildRequirements();

            Assert.IsFalse(string.IsNullOrEmpty(a.guid), "child guid should be backfilled");
            Assert.AreEqual(1, g.childRequirements.Count);
            Assert.AreEqual(a.guid, g.childRequirements[0].guid);
        }

        [Test]
        public void EnsureChildRequirements_RemovesStaleEntries()
        {
            var a = new TimelineStep();
            var g = GroupWith(a);
            g.childRequirements.Add(new GroupStep.ChildRequirement { guid = "no-such-step", required = true });

            g.EnsureChildRequirements();

            Assert.AreEqual(1, g.childRequirements.Count);
            Assert.AreEqual(a.guid, g.childRequirements[0].guid);
        }

        [Test]
        public void EnsureChildRequirements_PreservesAnExistingRequiredFlag()
        {
            var a = new TimelineStep();
            var g = GroupWith(a);
            g.childRequirements.Add(new GroupStep.ChildRequirement { guid = a.guid, required = false });

            g.EnsureChildRequirements();

            Assert.IsFalse(g.IsChildRequired(a.guid), "Ensure must not reset an authored required=false flag");
        }

        [Test]
        public void IsChildRequired_DefaultsTrueWhenUnconstrained()
        {
            var g = GroupWith(new TimelineStep());
            // No requirements computed yet -> everything is required by default.
            Assert.IsTrue(g.IsChildRequired("anything"));
            Assert.IsTrue(g.IsChildRequired(""));    // empty guid short-circuits to true
            Assert.IsTrue(g.IsChildRequired(null));
        }

        [Test]
        public void IsChildRequired_UnknownGuidWithEntriesStillDefaultsTrue()
        {
            var a = new TimelineStep();
            var g = GroupWith(a);
            g.EnsureChildRequirements();
            Assert.IsTrue(g.IsChildRequired("not-in-list"));
        }

        [Test]
        public void IsChildRequiredInList_MatchesTheInstanceMethod()
        {
            var reqs = new List<GroupStep.ChildRequirement>
            {
                new GroupStep.ChildRequirement { guid = "x", required = false },
                new GroupStep.ChildRequirement { guid = "y", required = true },
            };
            Assert.IsFalse(GroupStep.IsChildRequiredInList(reqs, "x"));
            Assert.IsTrue(GroupStep.IsChildRequiredInList(reqs, "y"));
            Assert.IsTrue(GroupStep.IsChildRequiredInList(reqs, "z"));   // unknown -> true
            Assert.IsTrue(GroupStep.IsChildRequiredInList(null, "x"));   // null list -> true
            Assert.IsTrue(GroupStep.IsChildRequiredInList(reqs, ""));    // empty guid -> true
        }

        [Test]
        public void EnsureMultiConditionBranchRequirements_PopulatesBranchAndDemotesMultiConditionMode()
        {
            var a = new TimelineStep();
            var b = new EventStep();
            var g = GroupWith(a, b);
            g.completeWhen = GroupStep.CompleteWhen.MultiCondition;
            var branch = new GroupStep.MultiConditionBranch { mode = GroupStep.CompleteWhen.MultiCondition };
            g.multiConditionBranches.Add(branch);

            g.EnsureMultiConditionBranchRequirements();

            // A branch may not itself be MultiCondition - it is demoted to AllChildrenComplete.
            Assert.AreEqual(GroupStep.CompleteWhen.AllChildrenComplete, branch.mode);
            Assert.AreEqual(2, branch.childRequirements.Count);
            CollectionAssert.AreEquivalent(
                new[] { a.guid, b.guid },
                branch.childRequirements.ConvertAll(c => c.guid));
        }

        [Test]
        public void EnsureMultiConditionBranchRequirements_PrunesStaleBranchEntries()
        {
            var a = new TimelineStep();
            var g = GroupWith(a);
            var branch = new GroupStep.MultiConditionBranch();
            branch.childRequirements.Add(new GroupStep.ChildRequirement { guid = "ghost", required = true });
            g.multiConditionBranches.Add(branch);

            g.EnsureMultiConditionBranchRequirements();

            Assert.AreEqual(1, branch.childRequirements.Count);
            Assert.AreEqual(a.guid, branch.childRequirements[0].guid);
        }
    }
}
