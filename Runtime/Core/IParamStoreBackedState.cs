namespace Pitech.XR.Core
{
    /// <summary>
    /// Optional seam (map sec-8 / sec-10): an <see cref="ILabStateStore"/> bool-view that can be BACKED BY
    /// the lab's one shared <see cref="IParamStore"/>, so its named booleans live in the SAME store the
    /// runner reads conditions/effects from. <see cref="LabConsole"/> resolves the lab's state store and,
    /// when it implements this, calls <see cref="Initialize"/> with its param store - unifying writers
    /// (triggers) and readers (ConditionsStep / effects) onto one source of truth.
    ///
    /// This is a SEPARATE interface (not a method on <see cref="ILabStateStore"/>) on purpose: adding a
    /// member to the public ILabStateStore would break any external implementer (e.g. a VR-side facade).
    /// Only the param-store-backed view (LocalLabStateStore) implements this; a standalone networked
    /// switchboard that owns its own replicated state simply does not, and is left untouched.
    /// </summary>
    public interface IParamStoreBackedState
    {
        /// <summary>Back this bool-view with the lab's shared param store (the Local store on single-player,
        /// or the networked/routed store on a multiplayer lab). Replaces any prior backing.</summary>
        void Initialize(IParamStore store);
    }
}
