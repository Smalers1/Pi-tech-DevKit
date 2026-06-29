namespace Pitech.XR.Localization
{
    /// <summary>
    /// The runtime localization SEAM (WS B1.5 Step 3/4). A lookup resolves a stable string KEY -
    /// the same key the editor keying pipeline stamps into the StringTables (e.g. "panel.title") -
    /// to the localized text for the currently selected locale.
    ///
    /// This decouples the resolve from any one backing store: a StringTable-backed lookup (consumer
    /// side, needs com.unity.localization), a cloud-overlay lookup (B.2/post-launch), and a test
    /// stub all implement the same two-line contract. <see cref="LocalizationServices"/> holds the
    /// installed lookup; with NONE installed the seam is inert (callers fall back to the source text)
    /// so no lab regresses at launch. <see cref="CompositeLocalizationLookup"/> chains several.
    /// </summary>
    public interface ILocalizationLookup
    {
        /// <summary>
        /// Resolve <paramref name="key"/> to localized text for the active locale. Returns false
        /// (and a null <paramref name="value"/>) when the key is unknown, so the caller can fall
        /// back to its source string rather than render an empty label.
        /// </summary>
        bool TryResolve(string key, out string value);
    }
}
