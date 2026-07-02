using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- The lab config: the whole analytics config (map sec-11) ----------
    // The two ownership halves in one container: the MEASUREMENT layer (analytics + subjects,
    // author/dev-authored, fixed) and the GRADING layer (objectives, teacher-owned, Web-Portal-
    // tunable post-launch). Bundled RAW (not pre-scored) into the session report so every stored
    // session re-computes if scoring changes (map sec-11.5).
    //
    // Hosting: this is a plain [Serializable] container. It is HELD by an in-scene component
    // (the LabConsole; Phase B.1 WS B1.7) - in-scene, not a ScriptableObject asset, because
    // TrackedSubject.target is a scene reference. The [SerializeReference] lists serialize once the
    // host UnityEngine.Object exists. INERT in Phase B.1: nothing references this yet, so untouched
    // labs are zero-diff (Proof C). The serialized surface freezes 2026-07-07 (additive-only after).

    /// <summary>
    /// The complete analytics config for one lab: the measurement layer (<see cref="analytics"/> +
    /// <see cref="subjects"/>), the grading layer (<see cref="objectives"/>), and the per-lab
    /// <see cref="roleCapacities"/>. Authored in LabConsole; bundled raw into the session report.
    /// </summary>
    [Serializable]
    public sealed class LabConfig
    {
        // Infrastructure for the portable JSON contract / forward migration. Hidden from the inspector for
        // now (the dev never sets it) - the field + default stay so the wire contract is unaffected.
        [Tooltip("Schema version of this config (for the portable JSON contract / forward migration).")]
        [HideInInspector] public int schemaVersion = 1;

        [Header("Measurement layer (author-authored, fixed)")]
        [Tooltip("Scored groupings of metrics - step sidecars + scene-wide categories.")]
        [SerializeReference] public List<Analytic> analytics = new List<Analytic>();

        [Tooltip("The subjects registry - one list powering drops, wrong-interaction, and order.")]
        public List<TrackedSubject> subjects = new List<TrackedSubject>();

        [Header("Grading layer (teacher-owned, tunable - incl. post-hoc in the Web Portal)")]
        [Tooltip("The grading algorithm: objectives (weights + targets) over the analytics.")]
        public List<Objective> objectives = new List<Objective>();

        [Header("Session")]
        [Tooltip("Per-lab role capacities (Professors 0-inf, Participants 1-inf, Spectators 0-inf).")]
        public SessionRoleCapacities roleCapacities = new SessionRoleCapacities();
    }
}
