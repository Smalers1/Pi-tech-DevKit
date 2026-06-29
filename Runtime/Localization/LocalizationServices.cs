namespace Pitech.XR.Localization
{
    /// <summary>
    /// Process-wide install point + resolve facade for the localization seam (WS B1.5).
    ///
    /// Static BY DESIGN: the active locale is a single per-process choice (this mirrors
    /// UnityEngine.Localization's own static LocalizationSettings, and the VR LanguageSwitcher that
    /// sets LocalizationSettings.SelectedLocale). It is NOT lab-scoped state, so the "resolve via
    /// parent-walk, no static Instance" rule for lab stores does not apply here.
    ///
    /// DEFAULT = passthrough: with no <see cref="ILocalizationLookup"/> installed,
    /// <see cref="Resolve(string, string)"/> returns the fallback unchanged, so the seam is INERT
    /// and every lab renders its authored source text exactly as today. A consumer (VR/AR) installs
    /// a StringTable-backed lookup - optionally wrapped in a <see cref="CompositeLocalizationLookup"/>
    /// for the cloud overlay - in B.2; nothing here loads the localization package.
    /// </summary>
    public static class LocalizationServices
    {
        static ILocalizationLookup _lookup;

        /// <summary>True once a consumer has installed a real lookup (still off at launch).</summary>
        public static bool HasLookup => _lookup != null;

        /// <summary>Install the active lookup (last writer wins). Pass a
        /// <see cref="CompositeLocalizationLookup"/> to layer a cloud overlay over the baked base.</summary>
        public static void Install(ILocalizationLookup lookup) => _lookup = lookup;

        /// <summary>Clear the installed lookup (returns the facade to passthrough). For teardown/tests.</summary>
        public static void Reset() => _lookup = null;

        /// <summary>
        /// Resolve <paramref name="key"/> to localized text for the active locale, returning
        /// <paramref name="fallback"/> (the authored source string) when no lookup is installed or
        /// the key is unknown. Never substitutes an empty/null result for a non-null fallback, so a
        /// missing translation degrades to source text rather than a blank label.
        /// </summary>
        public static string Resolve(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            // Snapshot the field once: Install/Reset can race a resolve on a different thread.
            var lookup = _lookup;
            if (lookup != null && lookup.TryResolve(key, out var value) && !string.IsNullOrEmpty(value))
                return value;

            return fallback;
        }
    }
}
