using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // -------- GroupStep --------
    /// <summary>
    /// Runs multiple nested steps concurrently (optional advanced authoring).
    /// Default routing uses <see cref="nextGuid"/> as the single exit.
    /// With <see cref="CompleteWhen.SpecificChildCompletes"/> and a branching nested
    /// <see cref="QuestionStep"/> or <see cref="ConditionsStep"/>, the scenario graph exposes
    /// that child’s branch targets; runtime follows those per-choice / per-outcome nextGuids
    /// (see DevKit Scenario Graph), and <see cref="nextGuid"/> is only used when not in that mode.
    /// With <see cref="CompleteWhen.MultiCondition"/>, multiple independent conditions are evaluated
    /// in order; the first satisfied condition routes through its dedicated exit port.
    /// </summary>
    [Serializable]
    public sealed class GroupStep : Step
    {
        public enum CompleteWhen
        {
            AllChildrenComplete,
            AnyChildCompletes,
            SpecificChildCompletes,
            RequiredChildrenComplete,
            NOfMChildrenComplete,
            MultiCondition,
        }

        [Header("Group Steps")]
        [Tooltip("Nested steps that run together. For most completion modes routing uses the Group’s nextGuid. With Specific Child Completes + a Question or Conditions child, branch nextGuids on that child drive exits from the group (see Scenario Graph).")]
        [SerializeReference] public List<Step> steps = new();

        [Header("Completion")]
        public CompleteWhen completeWhen = CompleteWhen.AllChildrenComplete;

        [Tooltip("Used only when CompleteWhen == NOfMChildrenComplete.")]
        [Min(1)] public int requiredCount = 1;

        [Tooltip("Used only when CompleteWhen == SpecificChildCompletes. Must match a nested step guid.")]
        public string specificStepGuid = "";

        [Tooltip("If true, when the group completes early (timer/specific-step), other running steps will be stopped/cleaned up.")]
        public bool stopOthersOnComplete = true;

        [Serializable]
        public class ChildRequirement
        {
            public string guid;
            public bool required = true;
        }

        [Tooltip("Optional required flags per child (used by Required/N-of-M completion modes).")]
        public List<ChildRequirement> childRequirements = new();

        [Serializable]
        public class MultiConditionBranch
        {
            public string label = "";
            public CompleteWhen mode = CompleteWhen.AllChildrenComplete;
            public string specificStepGuid = "";
            [Min(1)] public int requiredCount = 1;
            public List<ChildRequirement> childRequirements = new();
            public string nextGuid = "";
        }

        [Tooltip("Branches evaluated in order when CompleteWhen == MultiCondition. First satisfied condition wins.")]
        public List<MultiConditionBranch> multiConditionBranches = new();

        [Header("Routing")]
        [Tooltip("Next step (GUID) when not using proxy branch ports. Empty = next item in list. With Specific Child Completes + Question/Conditions, branch links are stored on that child; this field is the fallback if no branch exit was resolved.")]
        public string nextGuid = "";

        public override string Kind => "Group";

        public void EnsureChildRequirements()
        {
            if (steps == null) return;
            if (childRequirements == null) childRequirements = new List<ChildRequirement>();

            var existing = new HashSet<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i];
                if (st == null) continue;
                if (string.IsNullOrEmpty(st.guid)) st.guid = Guid.NewGuid().ToString();
                existing.Add(st.guid);

                bool found = false;
                for (int k = 0; k < childRequirements.Count; k++)
                {
                    if (childRequirements[k] != null && childRequirements[k].guid == st.guid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    childRequirements.Add(new ChildRequirement { guid = st.guid, required = true });
            }

            for (int i = childRequirements.Count - 1; i >= 0; i--)
            {
                var c = childRequirements[i];
                if (c == null || string.IsNullOrEmpty(c.guid) || !existing.Contains(c.guid))
                    childRequirements.RemoveAt(i);
            }
        }

        public bool IsChildRequired(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return true;
            if (childRequirements == null || childRequirements.Count == 0) return true;
            for (int i = 0; i < childRequirements.Count; i++)
                if (childRequirements[i] != null && childRequirements[i].guid == guid)
                    return childRequirements[i].required;
            return true;
        }

        public static bool IsChildRequiredInList(List<ChildRequirement> reqs, string guid)
        {
            if (string.IsNullOrEmpty(guid)) return true;
            if (reqs == null || reqs.Count == 0) return true;
            for (int i = 0; i < reqs.Count; i++)
                if (reqs[i] != null && reqs[i].guid == guid)
                    return reqs[i].required;
            return true;
        }

        public void EnsureMultiConditionBranchRequirements()
        {
            if (steps == null || multiConditionBranches == null) return;

            var existing = new HashSet<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i];
                if (st != null && !string.IsNullOrEmpty(st.guid))
                    existing.Add(st.guid);
            }

            for (int b = 0; b < multiConditionBranches.Count; b++)
            {
                var branch = multiConditionBranches[b];
                if (branch == null) continue;
                if (branch.childRequirements == null)
                    branch.childRequirements = new List<ChildRequirement>();

                if (branch.mode == CompleteWhen.MultiCondition)
                    branch.mode = CompleteWhen.AllChildrenComplete;

                foreach (string guid in existing)
                {
                    bool found = false;
                    for (int k = 0; k < branch.childRequirements.Count; k++)
                    {
                        if (branch.childRequirements[k] != null && branch.childRequirements[k].guid == guid)
                        { found = true; break; }
                    }
                    if (!found)
                        branch.childRequirements.Add(new ChildRequirement { guid = guid, required = true });
                }

                for (int i = branch.childRequirements.Count - 1; i >= 0; i--)
                {
                    var c = branch.childRequirements[i];
                    if (c == null || string.IsNullOrEmpty(c.guid) || !existing.Contains(c.guid))
                        branch.childRequirements.RemoveAt(i);
                }
            }
        }
    }
}
