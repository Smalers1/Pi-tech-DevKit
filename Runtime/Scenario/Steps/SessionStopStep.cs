using System;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // -------- SessionStopStep (the graded bracket - close) --------
    // Map sec-11.5: the close of the graded bracket opened by SessionStartStep. At play this emits a
    // session-completed fact on the LabEventBus (Phase B.2); on SessionStop the emitter assembles the
    // session report -> outbox -> cloud. INERT in Phase B.1: a new step TYPE only - not added to any
    // shipped lab, so untouched labs are zero-diff (Proof C). Kind mirrors the Step.Kind convention;
    // the portable JSON `kind` mapping is settled at WS B1.6. The serialized surface freezes 2026-07-07.
    [Serializable]
    public sealed class SessionStopStep : Step
    {
        [Header("Routing")]
        [Tooltip("Next step (GUID). Empty = next item in list.")]
        public string nextGuid = "";

        // Spaced Title Case to match the existing Step.Kind display convention ("Cue Cards", "Mini
        // Quiz"). If WS B1.6 makes Kind the JSON discriminator, all kinds normalize together then.
        public override string Kind => "Session Stop";
    }
}
