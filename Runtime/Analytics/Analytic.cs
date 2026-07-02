using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Analytics: scored groupings of metrics (map sec-11.3) ----------
    // An Analytic groups metrics into one score (a normalized weighted mean over its metrics). Two
    // kinds: StepAnalytic (one step's metrics -> that step's score) and SceneAnalytic (a scene-wide
    // category - time, safety). Step + scene analytics merge ONLY at the Objective (there is no
    // physical merge in the scene). Polymorphic [SerializeReference], mirroring Step / AnalyticsMetric.
    // Inert serialized schema (Phase B.1); the Score() reducer is Phase B.2.

    /// <summary>
    /// Abstract base for a scored grouping of metrics. Held in a polymorphic
    /// <c>[SerializeReference] List&lt;Analytic&gt;</c> on the <see cref="LabConfig"/> (the
    /// measurement layer). Objectives reference an analytic by <see cref="id"/> (the grading layer).
    /// </summary>
    [Serializable]
    public abstract class Analytic
    {
        [Tooltip("Stable id for this analytic within the config. Objectives reference it by this id.")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the readout.")]
        public string label;

        [Tooltip("The metrics scored into this analytic (a normalized weighted mean - weights are relative).")]
        [SerializeReference] public List<AnalyticsMetric> metrics = new List<AnalyticsMetric>();

        /// <summary>Stable kind tag for this analytic ("Step" / "Scene"); mirrors the <c>Step.Kind</c> convention.</summary>
        public abstract string Kind { get; }
    }

    /// <summary>One step's metrics scored into that step's score. The sidecar "brick" tied to a step
    /// node (keyed by <see cref="stepGuid"/>, map sec-11.0).</summary>
    [Serializable]
    public sealed class StepAnalytic : Analytic
    {
        public const string KindId = "Step";

        [Tooltip("GUID of the step this analytic is the sidecar for (Scenario.Step.guid).")]
        public string stepGuid;

        public override string Kind => KindId;
    }

    /// <summary>A scene-wide category (e.g. total time, safety) scored across the graded bracket.</summary>
    [Serializable]
    public sealed class SceneAnalytic : Analytic
    {
        public const string KindId = "Scene";

        [Tooltip("Free-form category name for this scene-wide grouping (e.g. \"Time\", \"Safety\").")]
        public string category;

        public override string Kind => KindId;
    }
}
