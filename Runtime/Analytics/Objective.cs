using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Objectives: the grading layer (map sec-11.3 / sec-11.8) ----------
    // The teacher-owned grading algorithm: weights + targets over the (fixed) analytics. Tunable
    // anytime, including post-hoc in the Web Portal (pick an analytic from the DevKit-defined
    // dropdown, set only weight + target). Because the bundled raw report re-computes, editing
    // objectives re-grades every stored session - which is why metrics (measurement) and objectives
    // (grading) are split. Inert serialized schema (Phase B.1); the Score() reducer is Phase B.2.
    //
    // Final formula (RATIFIED 2026-06-26, map sec-11.8) - a normalized weighted mean with an
    // applicability mask at every level:
    //   metric    x_m = clamp01(1 - Penalty_m(rawValue))
    //   analytic  X_A = sum(a.w.x) / sum(a.w)       over the metrics in A
    //   objective X_o = sum(a.sw.X_A) / sum(a.sw)   over the analytics feeding o
    //   grade     G   = sum(a.W.X_o) / sum(a.W)     ("incomplete" if the denominator is 0)
    // target_o is a PASS-BAR LABEL (X_o >= target_o), never a divisor.

    /// <summary>One analytic feeding an objective, with its relative sub-weight. References the
    /// analytic by id (the measurement layer is owned by <see cref="LabRubric.analytics"/>; the
    /// grading layer only points at it) so editing grading never mutates measurement.</summary>
    [Serializable]
    public sealed class ObjectiveInput
    {
        [Tooltip("Id of the analytic feeding this objective (matches an Analytic.id in the rubric).")]
        public string analyticId;

        [Tooltip("Relative sub-weight (0-1) of this analytic within the objective (relative, not a percentage).")]
        [Range(0f, 1f)] public float subWeight = 1f;
    }

    /// <summary>
    /// A teacher's grading bucket: analytic inputs x sub-weights, a pass-bar <see cref="target"/>
    /// (label only), and a <see cref="weight"/> = its share of the grade (the 70 / 20 / 10).
    /// </summary>
    [Serializable]
    public sealed class Objective
    {
        [Tooltip("Stable id for this objective within the rubric (and the JSON contract).")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the readout (e.g. \"Procedure Correctness\").")]
        public string label;

        [Tooltip("This objective's share of the final grade (relative weight 0-1 across objectives - the 70/20/10).")]
        [Range(0f, 1f)] public float weight = 1f;

        [Tooltip("Pass-bar LABEL only: the score at/above which this objective is 'passed' (X_o >= target). Not a grade divisor.")]
        [Range(0f, 1f)] public float target = 0.9f;

        [Tooltip("The analytics that feed this objective, each with its relative sub-weight.")]
        public List<ObjectiveInput> inputs = new List<ObjectiveInput>();
    }
}
