using System;
using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The computed grade (the on-device readout, v3 model 2026-07-02) ----------
    // The output of AnalyticsGradeEngine.Compute(config, stream). The lab-end readout renders straight from
    // this (no cloud round-trip - the DevKit is the canonical reducer). NOT bundled into the session report:
    // the report ships the RAW (config + events) so the cloud re-computes an identical GradeResult.
    //
    // v3 GRADE = clamp01( baseScore - penaltyPointsTotal/100 + bonusPointsTotal/100 ), single final clamp.
    // SCALE DISCIPLINE (the off-by-100 is the most likely device/cloud divergence): `grade` and `baseScore`
    // and per-step `score` are FRACTIONS [0,1]; `*PointsTotal`, `bonusPoints`, `pointsDeducted` are GRADE
    // POINTS [0,100]. Every field's scale is stated below.

    /// <summary>Per-metric result inside a step (the readout's "what dragged this step" detail).</summary>
    [Serializable]
    public sealed class MetricScoreResult
    {
        public string id;
        public string label;
        public string kind;
        public bool applicable;
        /// <summary>Raw measured value (seconds for the duration kind, an occurrence count for count kinds).</summary>
        public float rawValue;
        /// <summary>x_m = clamp01(1 - Penalty), a fraction [0,1]. 1 = perfect. (Unused for gate metrics.)</summary>
        public float score;
        /// <summary>The worst band the raw value crossed (None / Warning / Error) - readout styling.</summary>
        public BandSeverity worstSeverity;
        /// <summary>True when this metric is a critical GATE (does not contribute to the step score).</summary>
        public bool isGate;
        /// <summary>True when a gate metric FAILED (fails the step). Meaningful only when <see cref="isGate"/>.</summary>
        public bool gateFailed;
    }

    /// <summary>Per-step result: the equal mean of its scored metrics, plus its gate/fail state. ("Analytic"
    /// name kept for the [SerializeReference] type + wire stability; in v3 all analytics are step analytics.)</summary>
    [Serializable]
    public sealed class AnalyticScoreResult
    {
        public string id;
        public string label;
        public string kind;
        public bool applicable;
        /// <summary>X_s = equal mean of applicable scored (non-gate, non-notifyOnly) metrics, a fraction [0,1].
        /// 0 when the step failed a gate; 1 when all gates passed and there are no scored metrics.</summary>
        public float score;
        /// <summary>This step's weight (its share of the base weighted mean).</summary>
        public float weight;
        /// <summary>True when a critical metric on this step failed (X_s = 0; voids all goal bonuses).</summary>
        public bool stepFailed;
        /// <summary>Echo of StepAnalytic.failsScenario (this step failing fails the whole scenario) - for the readout.</summary>
        public bool failsScenario;
        public List<MetricScoreResult> metrics = new List<MetricScoreResult>();
    }

    /// <summary>Per-penalty result: how many occurrences fired and the grade points deducted.</summary>
    [Serializable]
    public sealed class PenaltyScoreResult
    {
        public string id;
        public string label;
        public string kind;
        /// <summary>Warning-severity occurrences counted (count kinds).</summary>
        public int warningCount;
        /// <summary>Error-severity occurrences counted (count kinds).</summary>
        public int errorCount;
        /// <summary>Grade points deducted [0,100] (after the cap).</summary>
        public int pointsDeducted;
        /// <summary>True when <see cref="pointsDeducted"/> hit the rule's maxDeduction cap.</summary>
        public bool capped;
    }

    /// <summary>Per-goal result: pass/fail + the measured value vs the threshold (in the goal's unit).</summary>
    [Serializable]
    public sealed class GoalScoreResult
    {
        public string id;
        public string label;
        public GoalKind kind;
        /// <summary>True when the goal's condition was met.</summary>
        public bool passed;
        /// <summary>False when the goal could not be evaluated (e.g. a referenced step never ran, or the bracket
        /// never closed for a time goal) - shown as "n/a", earns nothing.</summary>
        public bool earnable;
        /// <summary>Bonus points this goal is worth [0,100] (added only if passed AND not voided).</summary>
        public int bonusPoints;
        /// <summary>The measured value, in the goal's unit (percent 0-100 / seconds / count).</summary>
        public float rawValue;
        /// <summary>The pass threshold, in the goal's unit (percent 0-100 / seconds / count).</summary>
        public float threshold;
    }

    /// <summary>Config-independent raw session totals (always available for the readout, even on FAILED /
    /// Incomplete). Computed from the stream, so no authored metric is needed to show them.</summary>
    [Serializable]
    public sealed class SessionStats
    {
        public float totalSeconds;
        public int drops;
        public int wrongInteractions;
        public int orderViolations;
        /// <summary>Step analytics that actually ran (were entered).</summary>
        public int stepsGraded;
        /// <summary>Step analytics authored in the config.</summary>
        public int stepsTotal;
    }

    /// <summary>
    /// The whole computed grade for one attempt - the readout's data model (v3). <see cref="failed"/> is the
    /// terminal critical-fail state (grade 0). <see cref="isComplete"/> false = the session never closed and
    /// wasn't failed -> "Incomplete" (no meaningful grade).
    /// </summary>
    [Serializable]
    public sealed class GradeResult
    {
        /// <summary>True once the grade is meaningful: the bracket closed with a base (or a pure-penalty lab), OR
        /// the scenario was failed (a fail is a complete outcome). False = "Incomplete".</summary>
        public bool isComplete;

        /// <summary>The final grade, a fraction [0,1] = clamp01(base - penalties/100 + bonus/100). 0 when
        /// <see cref="failed"/>. Only meaningful when <see cref="isComplete"/>.</summary>
        public float grade;

        /// <summary>The role this readout was computed for (Participant = graded; Professor = presence only).</summary>
        public SessionRole role;

        // ---- v3 breakdown ----
        /// <summary>A critical gate fired -> grade 0, terminal. Overrides base/penalties/bonus in the headline.</summary>
        public bool failed;
        /// <summary>The metric/penalty id that caused the fail (empty unless <see cref="failed"/>).</summary>
        public string failCauseMetricId = string.Empty;
        /// <summary>The human label of the fail cause (for the readout banner).</summary>
        public string failCauseLabel = string.Empty;

        /// <summary>The base grade, a fraction [0,1] = weighted mean of step scores (1.0 for a pure-penalty lab).</summary>
        public float baseScore;

        /// <summary>Per-step breakdown (score, weight, gate state).</summary>
        public List<AnalyticScoreResult> steps = new List<AnalyticScoreResult>();

        /// <summary>Per-penalty breakdown (occurrences, points).</summary>
        public List<PenaltyScoreResult> penalties = new List<PenaltyScoreResult>();
        /// <summary>Total grade points deducted by penalties [0,100].</summary>
        public int penaltyPointsTotal;

        /// <summary>Per-goal breakdown (pass/fail, measured value).</summary>
        public List<GoalScoreResult> goals = new List<GoalScoreResult>();
        /// <summary>Total bonus points earned [0,100] (0 when voided).</summary>
        public int bonusPointsTotal;
        /// <summary>True when a failed step voided all goal bonuses (readout shows "Bonuses void").</summary>
        public bool bonusesVoided;

        /// <summary>Config-independent raw session totals.</summary>
        public SessionStats stats = new SessionStats();
    }
}
