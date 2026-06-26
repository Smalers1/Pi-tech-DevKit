using System;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- Session roles + capacities (map sec-11.5, decided 2026-06-23) ----------
    // Roles gate ANALYTICS ONLY - never flow / interaction (anyone, any role, can complete steps and
    // it counts). Chosen IN-SCENE, per attempt (not a pre-launch shell choice; the role binds to the
    // attempt, re-chosen each run). So LaunchContext carries tenant + user + session; the attempt +
    // role are created in-scene. Inert serialized schema (Phase B.1); enforcement is Phase B.2.

    /// <summary>The analytics role chosen in-scene for one attempt (map sec-11.5).</summary>
    public enum SessionRole
    {
        /// <summary>Presence only - session-completed + lab-presence-time, no graded analytics.</summary>
        Professor,
        /// <summary>Full graded analytics - the attempt.</summary>
        Participant,
        /// <summary>No analytics emitted.</summary>
        Spectator
    }

    /// <summary>
    /// Per-lab role capacities, authored in LabConsole (map sec-11.5): Professors 0-inf,
    /// Participants 1-inf, Spectators 0-inf. <see cref="Unlimited"/> (-1) on a max means no cap.
    /// </summary>
    [Serializable]
    public sealed class SessionRoleCapacities
    {
        /// <summary>Sentinel for an uncapped maximum.</summary>
        public const int Unlimited = -1;

        [Tooltip("Minimum professors required to start (default 0).")]
        [Min(0)] public int minProfessors = 0;

        [Tooltip("Maximum professors allowed. -1 = unlimited (default).")]
        public int maxProfessors = Unlimited;

        [Tooltip("Minimum participants required to start (default 1 - at least one graded attempt).")]
        [Min(0)] public int minParticipants = 1;

        [Tooltip("Maximum participants allowed. -1 = unlimited (default).")]
        public int maxParticipants = Unlimited;

        [Tooltip("Minimum spectators required to start (default 0).")]
        [Min(0)] public int minSpectators = 0;

        [Tooltip("Maximum spectators allowed. -1 = unlimited (default).")]
        public int maxSpectators = Unlimited;
    }
}
