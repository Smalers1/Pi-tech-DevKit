using System;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // -------- SessionStartStep (the graded bracket - open) --------
    // Map sec-11.5: SessionStart / SessionStop steps delimit where the GRADED part of the scenario
    // begins / ends. Completing the bracket is itself an analytics metric (the attempt/session). At
    // play these emit a session-started fact on the LabEventBus (Phase B.2 wires the runner to do so).
    // INERT in Phase B.1: a new step TYPE only - not added to any shipped lab, so untouched labs are
    // zero-diff (Proof C). Kind mirrors the Step.Kind convention; the portable JSON `kind` mapping is
    // settled at WS B1.6. The serialized surface freezes 2026-07-07.
    [Serializable]
    public sealed class SessionStartStep : Step
    {
        [Header("Routing")]
        [Tooltip("Next step (GUID). Empty = next item in list.")]
        public string nextGuid = "";

        // Spaced Title Case to match the existing Step.Kind display convention ("Cue Cards", "Mini
        // Quiz"). If WS B1.6 makes Kind the JSON discriminator, all kinds normalize together then.
        public override string Kind => "Session Start";
    }
}
