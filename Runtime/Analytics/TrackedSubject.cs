using System;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Subjects registry (map sec-11.2) ----------
    // ONE list powers drops + wrong-interaction + order. Each interaction is judged by
    // (in registry? . scenarioRelevant? . ownerStep == current?): not-in-registry / irrelevant ->
    // wrong-interaction; relevant & current -> correct; relevant & a different step -> order
    // violation. Severity is DERIVED from this registry (+ interaction kind), not hardcoded.
    // Authored on its own LabConsole tab; pre-filled from InsertStep / SelectionStep interactables,
    // distractors added by hand. Inert serialized schema (Phase B.1); scoring is Phase B.2.

    /// <summary>
    /// A trackable physical object in the lab. The catalog entry the drop / wrong-interaction / order
    /// metrics classify against. A <c>scenarioRelevant = false</c> entry is a distractor.
    /// </summary>
    [Serializable]
    public sealed class TrackedSubject
    {
        [Tooltip("Stable id for this subject within the rubric (referenced by the JSON contract and the auto-wiring).")]
        public string id;

        [Tooltip("Human-readable label shown in LabConsole and on the readout.")]
        public string label;

        [Tooltip("The scene object this subject tracks (grab/drop hooks auto-wire to it). Scene reference - resolved to a stable id on JSON export.")]
        public GameObject target;

        [Tooltip("True for an item the scenario expects the learner to use. False marks a distractor (an interaction with it is a warning/none, never an error).")]
        public bool scenarioRelevant = true;

        [Tooltip("GUID of the step that 'owns' this subject (the step where using it is correct). Empty = no owner. Used to derive out-of-order violations.")]
        public string ownerStepGuid;
    }
}
