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

        /// <summary>A scenario step was entered. Paired with <see cref="StepCompleted"/>: the
        /// StepDuration metric (map sec-11.1) diffs this fact's emit tick (LabEvent.Tick) against the
        /// completed fact's tick. Added 2026-06-26 (WS B1.1) - the enter-time the old telemetry never
        /// stamped (map sec-11.4); additive, no consumer binds yet.</summary>
        public const string StepEntered = "step.entered";

        /// <summary>A scenario step was completed.</summary>
        public const string StepCompleted = "step.completed";

        // ---- Analytics bracket + interaction facts (WS B2.1 / B2.2, 2026-06-29) ----
        // Additive (new members only - never re-spell a value). These are the keys the analytics
        // recorder (LabAnalytics) binds to. Emitted by: the runner (session.started/stopped, when it
        // runs a SessionStart/SessionStopStep) and the subjects-registry runtime + AnalyticsSignalEmitter
        // (the item/interaction/signal facts). Text carries the related id (subject id / signal id);
        // Tick carries the monotonic host stamp (durations). INERT until a graded lab authors the
        // SessionStart/Stop bracket + a LabAnalytics component (no bracket -> no capture).

        /// <summary>The graded bracket opened (a SessionStartStep ran). Begins analytics capture; its
        /// emit tick (LabEvent.Tick) is the bracket start for the TotalDuration metric.</summary>
        public const string SessionStarted = "session.started";

        /// <summary>The graded bracket closed (a SessionStopStep ran). Finalizes capture -> the session
        /// report + the on-device readout. Its tick closes the TotalDuration metric.</summary>
        public const string SessionStopped = "session.stopped";

        /// <summary>A tracked subject was grabbed (informational; Text = subject id). Drives nothing on
        /// its own - the drop/order classification is what scores (map sec-11.2).</summary>
        public const string ItemGrabbed = "item.grabbed";

        /// <summary>A tracked subject was dropped (Text = subject id). The DropMetric counts these; the
        /// reducer derives per-occurrence severity from the subject's scenarioRelevant flag.</summary>
        public const string ItemDropped = "item.dropped";

        /// <summary>A tracked subject was USED/interacted-with (Text = subject id). RAW - the recorder
        /// (LabAnalytics) is the single classifier: it derives correct / wrong-interaction / order
        /// violation from the registry (in-registry? relevant? ownerStep == current?), map sec-11.2.</summary>
        public const string InteractionUsed = "interaction.used";

        /// <summary>An authored analytics signal fired (Text = signal id). Routes to the metric whose id
        /// matches the signal id - the UnityEvent-callable escape hatch for authored failures
        /// (map sec-11.4, AnalyticsSignalEmitter).</summary>
        public const string AnalyticsSignal = "analytics.signal";

        // WS B1.1 Step 4 (2026-06-26): the dead per-step-bool keys were removed here -
        // "scenario.step.<guid>.{done,outcome,completedBy,completedAtTick}" and the MP step-sync
        // bridge prefix "flow.step.<guid>". They had ZERO consumers in the codebase (verified by
        // grep): the IScenarioFlowStore append-only ENTERED-guid path (map sec-7 / sec-10) supersedes
        // them - it replicates the FACTS / the frontier, never a per-step boolean. Removed pre-07-07
        // while still free; if a future need arises, re-adding a key is an additive (non-breaking)
        // change. Only the two fact NAMES above (step.entered / step.completed) survive - the bus
        // facts the analytics layer (map sec-11) actually binds to.
    }
}
