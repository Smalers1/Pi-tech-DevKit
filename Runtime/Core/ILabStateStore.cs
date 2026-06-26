using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The narrow bool-view over the one param store (map sec-8): named boolean scene-state with
    /// change notification. The old NetworkStateManager switchboard (named bools + triggers +
    /// listeners) unifies into this - a "WaterFlowing" bool is a networked boolean param, a trigger
    /// is a writer, a listener subscribes to <see cref="StateChanged"/>. Scene-authored: one store
    /// per lab on the scene-managers root, resolved via <c>GetComponentInParent&lt;ILabStateStore&gt;()</c>
    /// (never a static Instance). Impls: <c>LocalLabStateStore</c> (always compiled) +
    /// <c>NetworkedLabStateStore : NetworkBehaviour</c> (<c>#if PITECH_HAS_FUSION</c>). INERT in
    /// Phase B.1 - no live writer until the post-launch door.
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
