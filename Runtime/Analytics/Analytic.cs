using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Analytics: a step's scored metric group (map sec-11.3; v3 model 2026-07-02) ----------
    // v3 collapses the analytic layer to ONE kind: StepAnalytic - the sidecar "brick" on a step node, holding
    // that step's metrics. (SceneAnalytic was DELETED: scene-wide measurement is now PenaltyRule - negative
    // grade points - and Goal - bonus points. See LabConfig.) The step's 0-1 score is the EQUAL mean of its
    // scored metrics; the step carries the ONE weight that sets its share of the base grade.
    //
    // Kept as an abstract base (not folded into StepAnalytic) so the [SerializeReference] list type + the JSON
    // "type" discriminator stay stable, and a future analytic kind is additive. The serialized surface freezes
    // 2026-07-07.

    /// <summary>Abstract base for a scored grouping of step metrics. Held in a polymorphic
    /// <c>[SerializeReference] List&lt;Analytic&gt;</c> on the <see cref="LabConfig"/>.</summary>
    [Serializable]
    public abstract class Analytic
    {
        [Tooltip("Stable id for this analytic within the config (referenced by StepsScore goals and the JSON contract).")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the readout.")]
        public string label;

        [Tooltip("The step's metrics. Scored metrics count EQUALLY toward this step's 0-1 score; a critical metric is a gate; a notify-only metric doesn't score.")]
        [SerializeReference] public List<AnalyticsMetric> metrics = new List<AnalyticsMetric>();

        /// <summary>Stable kind tag for this analytic ("Step"); mirrors the <c>Step.Kind</c> convention.</summary>
        public abstract string Kind { get; }
    }

    /// <summary>One step's metrics scored into that step's score. The sidecar "brick" tied to a step node
    /// (keyed by <see cref="stepGuid"/>, map sec-11.0). Carries the step's <see cref="weight"/> (its share of
    /// the base) and <see cref="failsScenario"/> (this step failing fails the whole scenario).</summary>
    [Serializable]
    public sealed class StepAnalytic : Analytic
    {
        public const string KindId = "Step";

        [Tooltip("GUID of the step this analytic is the sidecar for (Scenario.Step.guid).")]
        public string stepGuid;

        [Tooltip("This step's importance on a 1-5 scale (1 = minor, 5 = critical): its relative share of the BASE " +
                 "grade (weighted mean across step analytics). A 5-importance step counts 5x a 1-importance one. Default 3.")]
        [Range(1f, 5f)] public float weight = 3f;

        [Tooltip("If this step FAILS (a critical metric fired), fail the WHOLE scenario (grade 0). The step-level " +
                 "'how important is this step' switch; the metric-level 'critical' decides WHICH criterion fails the step.")]
        public bool failsScenario = false;

        public override string Kind => KindId;
    }
}
