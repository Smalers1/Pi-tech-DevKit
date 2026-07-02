using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- Shared severity + gate evaluator (v3, 2026-07-02) ----------
    // ONE place that encodes the rules the recorder (live toast + live gate detection), the grade engine
    // (scoring), and - normatively - the cloud re-compute all follow. Before v3 the severity table lived in
    // two hand-synced copies (LabAnalytics.NotifyForCount + AnalyticsGradeEngine.DeriveSeverity); the v3
    // critical-gate check would have been a third. This collapses them.
    //
    // Pure + dependency-free (no Unity refs, no config walk - callers pass the two subject booleans they
    // already resolve), so the Web Portal can mirror it from the same spec.

    /// <summary>The v3 severity table + duration/count gate rules. Stateless.</summary>
    public static class AnalyticsSeverity
    {
        /// <summary>The default per-occurrence severity (B2.2 Step 5 table): relevant-subject drop -> Error,
        /// distractor drop -> Warning; wrong-interaction on a known distractor -> Warning, else (unknown) -> Error;
        /// out-of-order -> Warning; authored signal -> Error. <paramref name="isRelevant"/> /
        /// <paramref name="isKnownDistractor"/> are the caller's subject-registry lookup (both false = unknown).</summary>
        public static BandSeverity Derive(AnalyticsEventKind kind, bool isRelevant, bool isKnownDistractor)
        {
            switch (kind)
            {
                case AnalyticsEventKind.Drop:
                    return isRelevant ? BandSeverity.Error : BandSeverity.Warning;
                case AnalyticsEventKind.WrongInteraction:
                    return isKnownDistractor ? BandSeverity.Warning : BandSeverity.Error;
                case AnalyticsEventKind.OrderViolation:
                    return BandSeverity.Warning;
                case AnalyticsEventKind.Signal:
                    return BandSeverity.Error;
                default:
                    return BandSeverity.None;
            }
        }

        /// <summary>A count/signal critical GATE trips on ANY error-severity occurrence (zero-tolerance).</summary>
        public static bool CountGateTrips(BandSeverity occurrenceSeverity) => occurrenceSeverity == BandSeverity.Error;

        /// <summary>The Error band's threshold (seconds) for a duration metric, or -1 if there is no ACTIVE Error
        /// band (threshold &lt;= 0 = inactive, matching the ceiling rule - so an untouched Error band never fires a
        /// gate at t=0). This is the seconds at which a critical duration metric fails its step.</summary>
        public static float DurationErrorSeconds(AnalyticsMetric m)
        {
            if (m == null || m.bands == null) return -1f;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b != null && b.name == BandSeverity.Error && b.threshold > 0f) return b.threshold;
            }
            return -1f;
        }

        /// <summary>A duration critical GATE trips when elapsed seconds reach the active Error threshold.</summary>
        public static bool DurationGateTrips(AnalyticsMetric m, float elapsedSeconds)
        {
            float err = DurationErrorSeconds(m);
            return err > 0f && elapsedSeconds >= err;
        }

        /// <summary>The ceiling penalty for a duration metric: the penaltyWeight of the HIGHEST-threshold active
        /// band (penaltyWeight &gt; 0 AND threshold &gt; 0) the value crosses. Bands with threshold &lt;= 0 are
        /// inactive (no footgun zeroing). <paramref name="worst"/> = that band's severity (None if none crossed).</summary>
        public static float CeilingPenalty(AnalyticsMetric m, float rawValue, out BandSeverity worst)
        {
            worst = BandSeverity.None;
            float penalty = 0f;
            float bestThreshold = -1f;
            if (m == null || m.bands == null) return 0f;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b == null || b.penaltyWeight <= 0f || b.threshold <= 0f) continue;
                if (rawValue >= b.threshold && b.threshold > bestThreshold)
                {
                    bestThreshold = b.threshold;
                    penalty = b.penaltyWeight;
                    worst = b.name;
                }
            }
            return Clamp01(penalty);
        }

        /// <summary>The penaltyWeight of the band whose name == <paramref name="severity"/> (0 if none). Used by
        /// count-kind scoring (per-occurrence severity -> band penalty).</summary>
        public static float BandPenaltyFor(AnalyticsMetric m, BandSeverity severity)
        {
            if (m == null || m.bands == null) return 0f;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b != null && b.name == severity) return b.penaltyWeight;
            }
            return 0f;
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
