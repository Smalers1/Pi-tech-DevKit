using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Penalties: scene-wide, negative-only grade-point deductions (v3 model 2026-07-02) ----------
    // Replaces the old SceneAnalytic. A penalty is the run-wide SAFETY NET: it counts occurrences ANYWHERE in
    // the bracket (including during steps that have no analytics brick) and subtracts ABSOLUTE grade points
    // (0-100 scale), never a 0-1 fraction. (Deliberately a NEW plain type - not a metric/band - so the wire
    // never carries two unit systems under one key.)
    //
    // GRADE = clamp01( baseScore - penaltyPoints/100 + bonusPoints/100 ). The serialized surface freezes
    // 2026-07-07.

    /// <summary>What a penalty counts.</summary>
    public enum PenaltyKind
    {
        /// <summary>Items dropped anywhere in the run. Severity per drop: relevant subject -> error, distractor -> warning.</summary>
        Drop = 0,
        /// <summary>Interactions with a wrong target anywhere. Severity: unknown subject -> error, known distractor -> warning.</summary>
        WrongInteraction = 1,
        /// <summary>Out-of-order interactions anywhere (warning severity).</summary>
        Order = 2,
        /// <summary>An authored failure signal (matched by <see cref="PenaltyRule.signalId"/>); error severity.</summary>
        Signal = 3,
        /// <summary>Total time across the bracket - deducts by crossed duration tier (not per-occurrence).</summary>
        TotalDuration = 4
    }

    /// <summary>One duration tier for a <see cref="PenaltyKind.TotalDuration"/> penalty: "over N seconds -> minus P points".
    /// The HIGHEST crossed tier applies (not stacked).</summary>
    [Serializable]
    public sealed class PenaltyTier
    {
        [Tooltip("Deduct when total time is at least this many seconds. <= 0 = inactive.")]
        [Min(0f)] public float overSeconds;

        [Tooltip("Grade points to deduct (0-100 scale) when this tier is the highest crossed.")]
        [Min(0)] public int points;

        public PenaltyTier() { }
        public PenaltyTier(float overSeconds, int points) { this.overSeconds = overSeconds; this.points = points; }
    }

    /// <summary>
    /// A scene-wide, negative-only grade-point deduction. Count kinds (Drop/Wrong/Order/Signal) deduct
    /// <see cref="pointsPerWarning"/> / <see cref="pointsPerError"/> per occurrence (severity from the shared
    /// evaluator), capped at <see cref="maxDeduction"/>. The TotalDuration kind deducts by the highest crossed
    /// <see cref="tiers"/> entry. <see cref="failScenario"/> makes an error-severity occurrence (or a crossed
    /// tier) fail the whole scenario (grade 0).
    /// </summary>
    [Serializable]
    public sealed class PenaltyRule
    {
        [Tooltip("Stable id for this penalty within the config (and the JSON contract).")]
        public string id;

        [Tooltip("Human-readable label shown on the readout (e.g. \"Dropped instruments\").")]
        public string label;

        [Tooltip("What this penalty counts.")]
        public PenaltyKind kind = PenaltyKind.Drop;

        [Tooltip("Signal kind only: the authored signal id this penalty matches (AnalyticsSignalEmitter.EmitSignal id).")]
        public string signalId = string.Empty;

        [Tooltip("Grade points deducted per WARNING-severity occurrence (count kinds). Default 2.")]
        [Min(0)] public int pointsPerWarning = 2;

        [Tooltip("Grade points deducted per ERROR-severity occurrence (count kinds). Default 5.")]
        [Min(0)] public int pointsPerError = 5;

        [Tooltip("Maximum total points this penalty can deduct (0 = uncapped). Default 20. Set a cap so one bad run can't floor the whole grade unexpectedly.")]
        [Min(0)] public int maxDeduction = 20;

        [Tooltip("TotalDuration kind only: the 'over N seconds -> minus P points' tiers. The highest crossed tier applies.")]
        public List<PenaltyTier> tiers = new List<PenaltyTier>();

        [Tooltip("If an error-severity occurrence (or, for duration, any crossed tier) happens, FAIL the whole scenario (grade 0).")]
        public bool failScenario = false;

        [Tooltip("Fire the in-scene toast when this penalty is incurred.")]
        public bool notifyInScene = true;

        /// <summary>Maps this penalty's kind to the matching captured event kind (TotalDuration has none).</summary>
        public bool TryEventKind(out AnalyticsEventKind eventKind)
        {
            switch (kind)
            {
                case PenaltyKind.Drop: eventKind = AnalyticsEventKind.Drop; return true;
                case PenaltyKind.WrongInteraction: eventKind = AnalyticsEventKind.WrongInteraction; return true;
                case PenaltyKind.Order: eventKind = AnalyticsEventKind.OrderViolation; return true;
                case PenaltyKind.Signal: eventKind = AnalyticsEventKind.Signal; return true;
                default: eventKind = default; return false;   // TotalDuration
            }
        }
    }
}
