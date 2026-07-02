using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Metrics: a step's measurement criteria (map sec-11.1; v3 model 2026-07-02) ----------
    // Polymorphic [SerializeReference] hierarchy, mirroring Scenario.Step. A metric lives ONLY under a
    // StepAnalytic now (scene-wide measurement moved to PenaltyRule / Goal in v3). Each metric shapes
    // THAT step's 0-1 score, OR is a critical GATE (fails the step), OR is notify-only (in-scene toast,
    // excluded from the score).
    //
    // v3 CHANGES (2026-07-02, schemaVersion 2): per-metric `weight` was DELETED - scored metrics inside a
    // step split EQUALLY (the step carries the one weight, on StepAnalytic). Added `critical` (a hard gate:
    // failing it FAILS the step) and `notifyOnly` (fire the toast, don't score). `TotalDurationMetric` was
    // removed (total time is now a PenaltyRule tier and/or a TotalTimeUnder Goal). The serialized surface
    // freezes 2026-07-07 - additive-only after.

    /// <summary>
    /// Abstract base for a step metric - an atomic measurement plus its scoring bands. Held in a
    /// polymorphic <c>[SerializeReference] List&lt;AnalyticsMetric&gt;</c> on a <see cref="StepAnalytic"/>.
    /// </summary>
    [Serializable]
    public abstract class AnalyticsMetric
    {
        [Tooltip("Stable id for this metric within the config (referenced by goals/signals and the JSON contract).")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the lab-end readout.")]
        public string label;

        [Tooltip("CRITICAL gate: if this criterion fails, the whole STEP fails (score 0) and all goal bonuses are voided. " +
                 "A critical metric is a pass/fail gate, NOT a scored contributor. Count/signal kinds fail on any error-severity " +
                 "occurrence; a duration kind fails when the step reaches its Error-band seconds.")]
        public bool critical = false;

        [Tooltip("Notify-only: fire the in-scene toast for this metric but DON'T let it affect the step score (so adding a " +
                 "metric purely for a nudge doesn't dilute the step's other metrics). Ignored when 'critical' is on.")]
        public bool notifyOnly = false;

        [Tooltip("Scoring bands - the warning/error mechanism. Default scale: none 0 / warning 0.5 / error 1.0 (author-overridable).")]
        public List<ScoringBand> bands = ScoringBand.DefaultBands();

        /// <summary>Stable kind tag for this metric (e.g. "StepDuration"); mirrors the <c>Step.Kind</c> convention.</summary>
        public abstract string Kind { get; }
    }

    /// <summary>Time spent in one step (ceiling kind). The reducer diffs the step's entered/completed bus
    /// facts; the penalty is the highest duration band crossed. Scoped by the owning <see cref="StepAnalytic"/>.</summary>
    [Serializable]
    public sealed class StepDurationMetric : AnalyticsMetric
    {
        public const string KindId = "StepDuration";
        public override string Kind => KindId;
    }

    /// <summary>Count of items dropped during this step (a count kind: per-occurrence severity penalties sum,
    /// then clamp). Severity per drop is derived from the <see cref="TrackedSubject"/> registry at compute time
    /// (relevant-item drop -> error, distractor drop -> warning).</summary>
    [Serializable]
    public sealed class DropMetric : AnalyticsMetric
    {
        public const string KindId = "Drop";
        public override string Kind => KindId;
    }

    /// <summary>Count of interactions with the wrong target during this step (a count kind). Wrong = the subject
    /// is not in the registry or is a distractor (map sec-11.2).</summary>
    [Serializable]
    public sealed class WrongInteractionMetric : AnalyticsMetric
    {
        public const string KindId = "WrongInteraction";
        public override string Kind => KindId;
    }

    /// <summary>Count of out-of-order interactions during this step (a count kind): a registry-relevant subject
    /// used while its owner step is not the current step (map sec-11.2).</summary>
    [Serializable]
    public sealed class OrderMetric : AnalyticsMetric
    {
        public const string KindId = "Order";
        public override string Kind => KindId;
    }

    /// <summary>Count of authored failure SIGNALS raised for this metric during this step (a count kind). A signal
    /// is an explicit authored event (via <c>AnalyticsSignalEmitter</c>) matched to THIS metric by id - an event
    /// counts only when its <c>signalId</c> equals this metric's <see cref="AnalyticsMetric.id"/>.</summary>
    [Serializable]
    public sealed class SignalMetric : AnalyticsMetric
    {
        public const string KindId = "Signal";
        public override string Kind => KindId;
    }
}
