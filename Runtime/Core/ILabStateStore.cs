using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The narrow bool-view over the one param store (map sec-8): named boolean scene-state with
    /// change notification. The old NetworkStateManager switchboard (named bools + triggers +
    /// listeners) unifies into this - a "WaterFlowing" bool is a networked boolean param, a trigger
    /// is a writer, a listener subscribes to <see cref="StateChanged"/>. Scene-authored: one store
    /// per lab on the scene-managers root, resolved via <c>GetComponentInParent&lt;ILabStateStore&gt;()</c>
    /// (never a static Instance). Primary impl (P5): <c>LabConsole</c> implements this directly over its one
    /// param store, so a lab needs NO separate component (the root LabConsole IS the store). The optional
    /// <c>LocalLabStateStore</c> remains as a standalone bool-view for a console-less scene or a sub-tree.
    ///
    /// The NETWORKED impl GRADUATES INTO THIS PACKAGE (decision B1.3 S5, 2026-06-29 - (b)):
    /// <c>NetworkedLabStateStore : NetworkBehaviour</c> (<c>#if PITECH_HAS_FUSION</c>, in
    /// Pitech.XR.Networking) is the one canonical Fusion store every consumer shares - the same way
    /// labs already compose Scenario / Stats / Quiz from the DevKit (map sec-10.2). It supersedes VR's
    /// <c>NetworkStateManager</c>; the VR copy ships a <c>[Obsolete]</c> facade forwarding to the
    /// resolved store during scene migration. INERT at launch (multiplayer turns on in B.2) - no live
    /// writer until then.
    /// </summary>
    public interface ILabStateStore
    {
        /// <summary>Current value of a named boolean state (false if unset).</summary>
        bool GetState(string id);

        /// <summary>Set a named boolean state. Raises <see cref="StateChanged"/> with the id.</summary>
        void SetState(string id, bool value);

        /// <summary>Flip a named boolean state.</summary>
        void Toggle(string id);

        /// <summary>Raised with the state id whenever a value changes (subscribe instead of polling).</summary>
        event Action<string> StateChanged;
    }
}
