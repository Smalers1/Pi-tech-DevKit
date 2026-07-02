using System;
using UnityEngine;
using UnityEngine.Events;

namespace Pitech.XR.Analytics
{
    // ---------- In-scene session-role pick (map sec-11.5, Stergios 2026-06-29) ----------
    // WS B2.1 / roles. The role is chosen IN-SCENE, per attempt, BY THE LEARNER (decided design: roles
    // gate ANALYTICS ONLY, never flow/interaction - anyone, any role, completes steps and it counts).
    // This script is the runtime SEAM Stergios asked for: it holds the current role + the per-lab
    // capacities; the actual pick UI is BUILT BY STERGIOS and wires its buttons to the public Select*
    // methods (UnityEvent-callable). LabAnalytics reads CurrentRole to gate emission.
    //
    // Capacity note: at launch this enforces the LOCAL pick against the per-lab min/max as a single-peer
    // guard (IsSelectable). Cross-peer headcount enforcement ("at least 1 participant in the session")
    // is a multiplayer concern -> B2.4 (the networked session counts roles across peers). Flagged in the
    // decisions doc.

    /// <summary>UnityEvent carrying the chosen <see cref="SessionRole"/> (inspector-wirable).</summary>
    [Serializable] public sealed class SessionRoleEvent : UnityEvent<SessionRole> { }

    /// <summary>
    /// The in-scene, per-attempt role pick. Default <see cref="SessionRole.Participant"/> (the graded
    /// attempt). Resolve via <see cref="Find"/> (parent-walk) or assign on LabAnalytics. Build the pick
    /// UI on top and call <see cref="SelectParticipant"/> / <see cref="SelectProfessor"/> /
    /// <see cref="SelectSpectator"/> (or <see cref="SelectRole"/>).
    /// </summary>
    [AddComponentMenu("Pi tech/Analytics/Session Role Selector")]
    [DisallowMultipleComponent]
    public sealed class SessionRoleSelector : MonoBehaviour
    {
        [SerializeField, Tooltip("The role this attempt starts as (default Participant - the graded attempt).")]
        SessionRole role = SessionRole.Participant;

        [SerializeField, Tooltip("Per-lab capacities used by the LOCAL selectability guard. Authored HERE (the single source of truth, 2026-07-01); LabAnalytics mirrors these into the session report at start.")]
        SessionRoleCapacities capacities = new SessionRoleCapacities();

        [Tooltip("Raised whenever the role changes (wire UI / highlighting here).")]
        public SessionRoleEvent onRoleChanged = new SessionRoleEvent();

        /// <summary>The role chosen for this attempt.</summary>
        public SessionRole CurrentRole => role;

        /// <summary>The per-lab capacities the local selectability guard uses.</summary>
        public SessionRoleCapacities Capacities => capacities;

        /// <summary>Optional runtime override of the authored capacities (e.g. a host that computes per-session
        /// limits). No longer called by LabAnalytics - capacities are authored on this component directly.</summary>
        public void SetCapacities(SessionRoleCapacities caps)
        {
            if (caps != null) capacities = caps;
        }

        // UnityEvent-callable button targets (Stergios' UI wires these).
        public void SelectProfessor() => SelectRole(SessionRole.Professor);
        public void SelectParticipant() => SelectRole(SessionRole.Participant);
        public void SelectSpectator() => SelectRole(SessionRole.Spectator);

        /// <summary>Set the role for this attempt (no-op if the local capacity guard forbids it).</summary>
        public void SelectRole(SessionRole next)
        {
            if (!IsSelectable(next))
            {
                Debug.LogWarning($"[Analytics] Role '{next}' is not selectable under this lab's capacities (max reached/zero). Ignored.", this);
                return;
            }
            if (next == role) return;
            role = next;
            onRoleChanged.Invoke(role);
        }

        /// <summary>Local single-peer guard: is this role allowed at all by the lab capacities? (A max of
        /// 0 forbids the role; -1 is unlimited.) Cross-peer headcount is B2.4.</summary>
        public bool IsSelectable(SessionRole candidate)
        {
            if (capacities == null) return true;
            switch (candidate)
            {
                case SessionRole.Professor: return capacities.maxProfessors != 0;
                case SessionRole.Participant: return capacities.maxParticipants != 0;
                case SessionRole.Spectator: return capacities.maxSpectators != 0;
                default: return true;
            }
        }

        /// <summary>Resolve the nearest selector by parent-walk (includes inactive). Null if none.</summary>
        public static SessionRoleSelector Find(Component from)
        {
            return from ? from.GetComponentInParent<SessionRoleSelector>(true) : null;
        }
    }
}
