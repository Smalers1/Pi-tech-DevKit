using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Goals: pass/fail BONUS on top of the grade (v3 model 2026-07-02) ----------
    // Renamed from Objective. A goal is EXTRA CREDIT: pass its one condition -> earn its bonusPoints (added to
    // the grade, 0-100 scale); fail -> earn nothing. A goal can ONLY ADD points - it can never require anything
    // (use a PenaltyRule or a critical metric for must-not-happen). Bonuses are voided entirely if ANY step
    // failed a critical gate.
    //
    // Each kind has a pass condition in its OWN, EXPLICIT unit (never inferred): a score %, seconds, or a count.
    // The serialized surface freezes 2026-07-07.

    /// <summary>What a goal's pass condition measures.</summary>
    public enum GoalKind
    {
        /// <summary>Pass when the (weighted) average score of the referenced step analytics is at least
        /// <see cref="Goal.threshold"/> percent (0-100). Empty <see cref="Goal.stepAnalyticIds"/> = all steps.</summary>
        StepsScore = 0,
        /// <summary>Pass when total bracket time is at most <see cref="Goal.threshold"/> seconds.</summary>
        TotalTimeUnder = 1,
        /// <summary>Pass when the raw count of <see cref="Goal.countKind"/> occurrences is at most
        /// <see cref="Goal.threshold"/>.</summary>
        MaxOccurrences = 2
    }

    /// <summary>The occurrence kind a <see cref="GoalKind.MaxOccurrences"/> goal counts (raw, severity-blind).</summary>
    public enum CountKind
    {
        Drop = 0,
        WrongInteraction = 1,
        Order = 2,
        Signal = 3
    }

    /// <summary>
    /// A pass/fail bonus line. On pass, adds <see cref="bonusPoints"/> to the grade (unless voided by a failed
    /// step). The pass condition and its unit are chosen by <see cref="kind"/>; <see cref="threshold"/> is read
    /// in that kind's fixed unit (percent 0-100 / seconds / count).
    /// </summary>
    [Serializable]
    public sealed class Goal
    {
        [Tooltip("Stable id for this goal within the config (and the JSON contract).")]
        public string id;

        [Tooltip("Human-readable name shown on the readout (e.g. \"Finish under 2 minutes\").")]
        public string label;

        [Tooltip("Grade points added when this goal passes (0-100 scale). Voided if any step failed a critical gate.")]
        [Min(0)] public int bonusPoints = 10;

        [Tooltip("What this goal's pass condition measures.")]
        public GoalKind kind = GoalKind.StepsScore;

        [Tooltip("Pass threshold, in this kind's fixed unit: StepsScore = percent 0-100 (>=); TotalTimeUnder = seconds (<=); MaxOccurrences = count (<=).")]
        public float threshold = 70f;

        [Tooltip("StepsScore only: the step analytics averaged. Empty = ALL step analytics (resolved live at grade time).")]
        public List<string> stepAnalyticIds = new List<string>();

        [Tooltip("MaxOccurrences only: which occurrence kind to count.")]
        public CountKind countKind = CountKind.Drop;

        [Tooltip("MaxOccurrences + Signal only: the signal id to count.")]
        public string signalId = string.Empty;
    }
}
