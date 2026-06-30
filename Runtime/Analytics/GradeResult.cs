using System;
using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The computed grade (the on-device readout, map sec-11.5 / sec-11.8) ----------
    // WS B2.1. The output of AnalyticsGradeEngine.Compute(rubric, stream). The lab-end readout renders
    // straight from this (no cloud round-trip - the DevKit is the canonical reducer, decision 38). It is
    // NOT bundled into the session report: the report ships the RAW (rubric + events) so the cloud
    // re-computes an identical GradeResult (the equivalence fixture keeps both reducers in lockstep).
    //
    // Applicability: an item with Applicable == false is MASKED (dropped from its parent's weighted
    // mean - skipped steps / non-participant roles / no measurable metrics). A grade with
    // IsComplete == false is "incomplete" - every objective masked, or the session never closed; it is
    // never 0 and never "passed" (map sec-11.3).

    /// <summary>Per-metric result: the raw measurement, the [0,1] score, and the worst band crossed
    /// (for the readout's warning/error styling).</summary>
    [Serializable]
    public sealed class MetricScoreResult
    {
        public string id;
        public string label;
        public string kind;
        public bool applicable;
        /// <summary>Raw measured value (seconds for duration kinds, an occurrence count for count kinds).</summary>
        public float rawValue;
        /// <summary>x_m = clamp01(1 - Penalty). 1 = perfect, 0 = fully penalized.</summary>
        public float score;
        /// <summary>The worst band the raw value crossed (None / Warning / Error) - readout styling.</summary>
        public BandSeverity worstSeverity;
    }

    /// <summary>Per-analytic result: the normalized weighted mean over its applicable metrics.</summary>
    [Serializable]
    public sealed class AnalyticScoreResult
    {
        public string id;
        public string label;
        public string kind;
        public bool applicable;
        /// <summary>X_A = sum(w.x) / sum(w) over applicable metrics.</summary>
        public float score;
        public List<MetricScoreResult> metrics = new List<MetricScoreResult>();
    }

    /// <summary>Per-objective result: the weighted mean over its applicable analytic inputs, plus the
    /// pass-bar verdict (X_o &gt;= target).</summary>
    [Serializable]
    public sealed class ObjectiveScoreResult
    {
        public string id;
        public string label;
        public bool applicable;
        /// <summary>X_o = sum(sw.X_A) / sum(sw) over applicable analytics feeding this objective.</summary>
        public float score;
        /// <summary>Pass-bar label only (X_o &gt;= target). False when not applicable.</summary>
        public bool passed;
        public float target;
        public List<AnalyticScoreResult> analytics = new List<AnalyticScoreResult>();
    }

    /// <summary>
    /// The whole computed grade for one attempt - the on-device readout's data model. When
    /// <see cref="isComplete"/> is false the session is "incomplete" (<see cref="grade"/> is not
    /// meaningful) - render "Incomplete", never a score (map sec-11.3 / sec-11.5).
    /// </summary>
    [Serializable]
    public sealed class GradeResult
    {
        /// <summary>False when no objective was applicable, or the session never closed -&gt; "incomplete".</summary>
        public bool isComplete;
        /// <summary>G = sum(W.X_o) / sum(W) over applicable objectives, in [0,1]. Only meaningful when
        /// <see cref="isComplete"/>.</summary>
        public float grade;
        /// <summary>The role this readout was computed for (Participant = graded; Professor = presence
        /// only; Spectator = none).</summary>
        public SessionRole role;
        public List<ObjectiveScoreResult> objectives = new List<ObjectiveScoreResult>();
    }
}
