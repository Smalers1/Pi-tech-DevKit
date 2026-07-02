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
    /// The complete analytics config for one lab (v3): step analytics (the base grade), penalties (negative
    /// grade points), goals (bonus points), the subjects registry, and the per-lab role capacities. Authored
    /// in LabConsole / the Scenario Graph; bundled raw into the session report so the cloud re-computes.
    /// GRADE = clamp01( base - penalties/100 + bonus/100 ).
    /// </summary>
    [Serializable]
    public sealed class LabConfig
    {
        // The portable JSON contract / forward-migration version. schemaVersion 2 = the v3 grading model
        // (base - penalties + bonus). The cloud re-compute branches on this. Hidden from the inspector (auto).
        [Tooltip("Schema version of this config (for the portable JSON contract / forward migration). 2 = v3 model.")]
        [HideInInspector] public int schemaVersion = 2;

        [Header("Base grade - step analytics (authored on step nodes in the Scenario Graph)")]
        [Tooltip("The step sidecars whose weighted-mean 0-1 score IS the base grade. Only StepAnalytic lives here.")]
        [SerializeReference] public List<Analytic> analytics = new List<Analytic>();

        [Tooltip("The subjects registry - one list powering drops, wrong-interaction, and order (steps AND penalties).")]
        public List<TrackedSubject> subjects = new List<TrackedSubject>();

        [Header("Penalties - scene-wide negative-only grade-point deductions")]
        [Tooltip("Run-wide deductions in absolute grade points (drops / wrong / order / signals / total time).")]
        public List<PenaltyRule> penalties = new List<PenaltyRule>();

        [Header("Goals - pass/fail bonus points on top (teacher-tunable, incl. post-hoc in the Web Portal)")]
        [Tooltip("Extra credit: pass a goal's condition -> add its bonus points. Voided if any step failed a gate.")]
        public List<Goal> goals = new List<Goal>();

        [Header("Session")]
        [Tooltip("Per-lab role capacities (Professors 0-inf, Participants 1-inf, Spectators 0-inf).")]
        public SessionRoleCapacities roleCapacities = new SessionRoleCapacities();
    }
}
