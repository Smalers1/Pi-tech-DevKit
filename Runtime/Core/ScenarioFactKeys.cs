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
