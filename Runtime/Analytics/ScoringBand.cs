using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Scoring bands (the warning/error mechanism) ----------
    // Map sec-11.1 / sec-11.8 (RATIFIED 2026-06-26). A band does two things: it subtracts a
    // penalty from the metric score AND (if notifyInScene) fires the in-scene UI notification.
    // "warning" vs "error" are two NAMED tiers on one continuous penalty scale - importance is
    // the penaltyWeight, not the name. This is inert serialized schema only (no scoring engine -
    // that is Phase B.2); freezes at the DevKit SDK emit-API gate, 2026-07-07.

    /// <summary>The named severity tier of a <see cref="ScoringBand"/>. Importance lives in
    /// <see cref="ScoringBand.penaltyWeight"/>; the name only tags the tier (and styles the UI).</summary>
    public enum BandSeverity
    {
        /// <summary>No penalty - within tolerance.</summary>
        None,
        /// <summary>A soft violation (default penalty 0.5 - halves the metric).</summary>
        Warning,
        /// <summary>A hard violation (default penalty 1.0 - zeroes the metric).</summary>
        Error
    }

    /// <summary>
    /// One scoring band on a metric's continuous penalty scale (map sec-11.1). The reducer
    /// (Phase B.2) compares the metric's raw value against <see cref="threshold"/>; a crossed band
    /// contributes its <see cref="penaltyWeight"/>. Ceiling kinds (duration) take the penalty of the
    /// highest band crossed; count kinds (drops/wrong/order) sum per-occurrence penalties, then clamp.
    /// </summary>
    [Serializable]
    public sealed class ScoringBand
    {
        [Tooltip("Named tier. Importance is the penalty weight, not the name; the name tags the tier and styles the in-scene UI.")]
        public BandSeverity name = BandSeverity.None;

        [Tooltip("Boundary this band is crossed at. Units depend on the metric kind: seconds for duration (ceiling) kinds, an occurrence count for drop/wrong/order (count) kinds.")]
        public float threshold;

        [Tooltip("Penalty subtracted from the metric score when this band is crossed. Default scale (sec-11.8): warning 0.5, error 1.0. Author-overridable.")]
        public float penaltyWeight;

        [Tooltip("If true, crossing this band fires the in-scene UI notification (the warning/error toast). Display-only on follower peers.")]
        public bool notifyInScene;

        public ScoringBand() { }

        public ScoringBand(BandSeverity name, float threshold, float penaltyWeight, bool notifyInScene)
        {
            this.name = name;
            this.threshold = threshold;
            this.penaltyWeight = penaltyWeight;
            this.notifyInScene = notifyInScene;
        }

        /// <summary>
        /// The ratified default band set (map sec-11.8, "for start"): none 0 / warning 0.5 / error 1.0.
        /// Thresholds default to 0 - the author sets them per metric (seconds for duration, counts for
        /// count kinds). A fresh metric is seeded with these; authors override per metric.
        /// </summary>
        public static List<ScoringBand> DefaultBands()
        {
            return new List<ScoringBand>
            {
                new ScoringBand(BandSeverity.None, 0f, 0f, false),
                new ScoringBand(BandSeverity.Warning, 0f, 0.5f, true),
                new ScoringBand(BandSeverity.Error, 0f, 1.0f, true)
            };
        }
    }
}
