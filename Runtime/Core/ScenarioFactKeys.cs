// Step-fact vocabulary - the ONE pre-baked contract seam admitted in Phase A
// (WS A2 Step 8 / accelerator sec-H#6). CONSTS + pure key builders ONLY:
// NO emission, NO ledger, NO IScenarioFlowStore/ScenarioStepFact type (those stay
// deferred - Phase B / after-launch; pre-baking the TYPES is an explicit Phase A trap).
//
// This is the single source of truth for scenario step-fact strings, so the Phase B
// analytics facts, the after-launch flow store, and the WS B9 multiplayer step-sync
// bridge never drift into hand-typed duplicates. Treat the string values as FROZEN once
// a consumer binds to them - additive changes only (new members), never re-spell a value.
namespace Pitech.XR.Core
{
    /// <summary>
    /// Stable string vocabulary for scenario step facts. Behaviour-neutral: nothing here
    /// emits, stores, or replicates a fact - callers (Phase B onward) build the keys/names
    /// from these constants. Held in <c>Pitech.XR.Core</c> (the leaf assembly) so every
    /// consumer - Analytics, the flow store, and the consumer-side multiplayer bridge -
    /// can reference one definition without a circular dependency.
    /// </summary>
    public static class ScenarioFactKeys
    {
        // ---- Fact / event names (what happened; not a per-step key) ----

        /// <summary>A scenario step was completed.</summary>
        public const string StepCompleted = "step.completed";

        // ---- Per-step rich-fact keys: "scenario.step.<guid>.<field>" ----
        // The facts the after-launch flow store records and Phase B analytics reads.
        // Replicate the FACTS, never the runner's current step index (spec sec-1.4).

        /// <summary>Prefix for per-step fact keys: <c>scenario.step.&lt;guid&gt;</c>.</summary>
        public const string StepKeyPrefix = "scenario.step.";

        public const string DoneSuffix = ".done";
        public const string OutcomeSuffix = ".outcome";
        public const string CompletedBySuffix = ".completedBy";
        public const string CompletedAtTickSuffix = ".completedAtTick";

        /// <summary><c>scenario.step.&lt;guid&gt;.done</c> - the step's completion fact.</summary>
        public static string StepDone(string stepGuid) => StepKeyPrefix + stepGuid + DoneSuffix;

        /// <summary><c>scenario.step.&lt;guid&gt;.outcome</c> - the step's recorded outcome.</summary>
        public static string StepOutcome(string stepGuid) => StepKeyPrefix + stepGuid + OutcomeSuffix;

        /// <summary><c>scenario.step.&lt;guid&gt;.completedBy</c> - who completed the step.</summary>
        public static string StepCompletedBy(string stepGuid) => StepKeyPrefix + stepGuid + CompletedBySuffix;

        /// <summary><c>scenario.step.&lt;guid&gt;.completedAtTick</c> - when the step completed.</summary>
        public static string StepCompletedAtTick(string stepGuid) => StepKeyPrefix + stepGuid + CompletedAtTickSuffix;

        // ---- Multiplayer step-sync bridge key: "flow.step.<guid>" (WS B9 seam) ----
        // The launch-minimal boolean completion fact the WS B9 bridge syncs over the
        // consumer-side NetworkStateManager. A separate namespace from authored scene
        // states (e.g. "PuzzleSolved") so the two can never collide. For a 32-char Unity
        // GUID this is ~42 chars - under the NetworkString<_64> cap. The const is INERT
        // until a consumer reads it; it lives here so the bridge never hand-types the prefix.
        // (WS B9 is pending board ratification - see the Phase B plan.)

        /// <summary>Prefix for the multiplayer step-sync bridge key: <c>flow.step.&lt;guid&gt;</c>.</summary>
        public const string FlowStepKeyPrefix = "flow.step.";

        /// <summary><c>flow.step.&lt;guid&gt;</c> - the synced boolean completion fact for a step.</summary>
        public static string FlowStep(string stepGuid) => FlowStepKeyPrefix + stepGuid;
    }
}
