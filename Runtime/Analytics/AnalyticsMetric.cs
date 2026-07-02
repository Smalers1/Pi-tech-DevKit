using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Metrics: the atomic measurement + scoring contract (map sec-11.1) ----------
    // Polymorphic [SerializeReference] hierarchy, mirroring Scenario.Step (map sec-9.2). This is
    // INERT serialized schema only: no reducer, no scoring, no emit (those are Phase B.2). The
    // surface freezes at the DevKit SDK emit-API gate, 2026-07-07 - additive-only after that.
    //
    // Each metric honors one contract (Phase B.2): emit (rawValue, score in [0,1] higher=better,
    // applicable in {0,1}, violations[]). The reducer (events -> rawValue) is a pure function over
    // the LabEventBus stream, so the Web Portal can recompute identically (DevKit = canonical
    // reducer, cloud = mirror). The drop/wrong/order kinds consult the lab's TrackedSubject registry
    // (see LabConfig.subjects) at compute time to derive per-occurrence severity.

    /// <summary>
    /// Abstract base for an analytics metric - an atomic measurement plus its scoring bands.
    /// Held in a polymorphic <c>[SerializeReference] List&lt;AnalyticsMetric&gt;</c> on an
    /// <see cref="Analytic"/>. <see cref="Kind"/> is a stable kind tag (mirrors the
    /// <c>Step.Kind</c> convention); whether the portable JSON contract keys on it (vs the CLR type
    /// name Unity already serializes) is settled at the WS B1.6 freeze.
    /// </summary>
    [Serializable]
    public abstract class AnalyticsMetric
    {
        [Tooltip("Stable id for this metric within the config (referenced by analytics/objectives, and by the JSON contract).")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the lab-end readout.")]
        public string label;

        [Tooltip("Relative weight (0-1) of this metric within its analytic (a normalized weighted mean - weights are relative, not percentages).")]
        [Range(0f, 1f)] public float weight = 1f;

        [Tooltip("Scoring bands - the warning/error mechanism. Default scale: none 0 / warning 0.5 / error 1.0 (author-overridable).")]
        public List<ScoringBand> bands = ScoringBand.DefaultBands();

        /// <summary>Stable kind tag for this metric (e.g. "StepDuration"); mirrors the <c>Step.Kind</c> convention.</summary>
        public abstract string Kind { get; }
    }

    /// <summary>Time spent in one step (ceiling kind). The reducer diffs the step's entered/completed
    /// bus facts; the penalty is the highest duration band crossed (step function at launch - curves
    /// are deferred, map sec-11.1). Scoped by the owning <see cref="StepAnalytic"/>.</summary>
    [Serializable]
    public sealed class StepDurationMetric : AnalyticsMetric
    {
        public const string KindId = "StepDuration";
        public override string Kind => KindId;
    }

    /// <summary>Total time across the graded bracket (SessionStart -> SessionStop), a ceiling kind.
    /// Scene-wide; lives under a <see cref="SceneAnalytic"/>.</summary>
    [Serializable]
    public sealed class TotalDurationMetric : AnalyticsMetric
    {
        public const string KindId = "TotalDuration";
        public override string Kind => KindId;
    }

    /// <summary>Count of items dropped (a count kind: per-occurrence penalties sum, then clamp).
    /// Severity per drop is derived from the <see cref="TrackedSubject"/> registry at compute time
    /// (relevant-item drop -> error, distractor drop -> warning/none).</summary>
    [Serializable]
    public sealed class DropMetric : AnalyticsMetric
    {
        public const string KindId = "Drop";
        public override string Kind => KindId;
    }

    /// <summary>Count of interactions with the wrong target (a count kind). An interaction is judged
    /// wrong when the subject is not in the registry, is a distractor, or an authored wrong-target
    /// signal fires (map sec-11.2 / sec-11.4).</summary>
    [Serializable]
    public sealed class WrongInteractionMetric : AnalyticsMetric
    {
        public const string KindId = "WrongInteraction";
        public override string Kind => KindId;
    }

    /// <summary>Count of out-of-order interactions (a count kind): a registry-relevant subject used
    /// while its owner step is not the current step (map sec-11.2). Ready-but-off until authors tag
    /// owner steps on the subjects registry.</summary>
    [Serializable]
    public sealed class OrderMetric : AnalyticsMetric
    {
        public const string KindId = "Order";
        public override string Kind => KindId;
    }

    /// <summary>Count of authored failure SIGNALS that fire for this metric (a count kind). Unlike the
    /// DERIVED kinds (drop / wrong / order, which the recorder classifies from interactions), a signal is
    /// an EXPLICIT authored event raised via <c>AnalyticsSignalEmitter</c>. It is matched to THIS metric by
    /// id - an event counts only when its <c>signalId</c> equals this metric's <see cref="AnalyticsMetric.id"/>
    /// - so authored-failure scoring is its OWN typed kind, not piggy-backed on an unrelated count metric.
    /// Default per-occurrence severity is Error (author-overridable via the bands). Scope under a
    /// <see cref="StepAnalytic"/> (signals raised while that step is current) or a <see cref="SceneAnalytic"/>
    /// (all signals in the bracket).</summary>
    [Serializable]
    public sealed class SignalMetric : AnalyticsMetric
    {
        public const string KindId = "Signal";
        public override string Kind => KindId;
    }
}
