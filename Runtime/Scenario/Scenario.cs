using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // ---------- Holder on the scene ----------
    [DisallowMultipleComponent]
    [AddComponentMenu("Pi tech/Scenario/Scenario")]
    public class Scenario : MonoBehaviour
    {
        // human–friendly name for this scenario (used in inspectors, logs, dashboards)
        [SerializeField, Tooltip("Human-friendly name for this scenario")]
        private string title = "Main Scenario";
        public string Title => title;

        [SerializeReference] public List<Step> steps = new();

#if UNITY_EDITOR
        // Editor-only graph notes (saved with the scene on the Scenario component).
        [Serializable]
        public sealed class GraphNote
        {
            public string guid;
            public Rect rect = new Rect(80, 80, 240, 160);
            [TextArea] public string text = "Note…";
            // When set, the note is tethered to the step node with this guid:
            // it follows the node when moved and draws a connector line to it.
            public string attachedStepGuid = "";
            public Vector2 attachOffset;   // note position relative to the attached node
        }

        [SerializeField] List<GraphNote> graphNotes = new();
        public List<GraphNote> GraphNotes => graphNotes;

        // Editor-only visual organizing groups (purely cosmetic; no effect on flow/runtime).
        // Unrelated to GroupStep, which is a runtime construct.
        [Serializable]
        public sealed class GraphGroup
        {
            public string guid;
            public string title = "Group";
            public Rect rect = new Rect(60, 60, 340, 260);
        }

        [SerializeField] List<GraphGroup> graphGroups = new();
        public List<GraphGroup> GraphGroups => graphGroups;

        // Editor-only per-step graph display overrides (manual node size + custom header name),
        // keyed by Step.guid. Side-table on the Scenario (NOT fields on Step) so the runtime
        // step serialization stays untouched; entries are pruned when they return to defaults.
        [Serializable]
        public sealed class StepGraphDisplay
        {
            public string stepGuid = "";
            public Vector2 size = Vector2.zero;   // user-set node size; zero => auto-size
            public string displayName = "";       // optional custom name shown next to the step type
        }

        [SerializeField] List<StepGraphDisplay> stepGraphDisplays = new();
        public List<StepGraphDisplay> StepGraphDisplays => stepGraphDisplays;

        public StepGraphDisplay FindStepGraphDisplay(string stepGuid)
        {
            if (string.IsNullOrEmpty(stepGuid)) return null;
            return stepGraphDisplays.Find(d => d != null && d.stepGuid == stepGuid);
        }

        public StepGraphDisplay GetOrAddStepGraphDisplay(string stepGuid)
        {
            var d = FindStepGraphDisplay(stepGuid);
            if (d == null && !string.IsNullOrEmpty(stepGuid))
            {
                d = new StepGraphDisplay { stepGuid = stepGuid };
                stepGraphDisplays.Add(d);
            }
            return d;
        }

        // Drop the entry when it no longer overrides anything (keeps scene serialization minimal).
        public void PruneStepGraphDisplay(string stepGuid)
        {
            var d = FindStepGraphDisplay(stepGuid);
            if (d != null && d.size == Vector2.zero && string.IsNullOrEmpty(d.displayName))
                stepGraphDisplays.Remove(d);
        }

        public void RemoveStepGraphDisplay(string stepGuid)
        {
            if (string.IsNullOrEmpty(stepGuid)) return;
            stepGraphDisplays.RemoveAll(d => d == null || d.stepGuid == stepGuid);
        }

        // Remove entries for this step AND any nested GroupStep children - for editor delete
        // paths (graph window + inspector), so overrides die with the step.
        public void RemoveStepGraphDisplayRecursive(Step step)
        {
            if (step == null) return;
            RemoveStepGraphDisplay(step.guid);
            if (step is GroupStep g && g.steps != null)
                foreach (var st in g.steps)
                    RemoveStepGraphDisplayRecursive(st);
        }
#endif

        void OnValidate()
        {
#if UNITY_EDITOR
            // Avoid touching SerializeReference data while scripts recompile; partial loads are unsafe.
            if (UnityEditor.EditorApplication.isCompiling)
                return;
#endif
            if (steps == null) return;

            // Never strip null entries from SerializeReference lists here. Unity can report null slots
            // transiently during prefab import, Apply, or domain reload before managed references
            // deserialize — removing them marks the asset dirty and permanently deletes the step graph.
            // Use the Scenario inspector "Clear Nulls" only when you intentionally discard broken refs.

            EnsureGuidsRecursive(steps);

            if (!string.IsNullOrEmpty(title) && gameObject.name == "Scenario")
                gameObject.name = title;
        }

        static void EnsureGuidsRecursive(List<Step> list)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;

                if (string.IsNullOrEmpty(s.guid))
                    s.guid = Guid.NewGuid().ToString();

                if (s is GroupStep g && g.steps != null)
                {
                    // Same as root steps: do not RemoveAt nulls — preserves prefab / import safety.

                    EnsureGuidsRecursive(g.steps);
                    g.EnsureChildRequirements();
                    if (g.completeWhen == GroupStep.CompleteWhen.MultiCondition)
                        g.EnsureMultiConditionBranchRequirements();
                }
            }
        }
    }
}
