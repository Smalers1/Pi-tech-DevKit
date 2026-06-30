using System.Collections.Generic;

namespace Pitech.XR.Localization
{
    // ---------- Dictionary-backed lookup (WS B2.5) ----------
    // A dependency-free ILocalizationLookup over an in-memory key->text map. Three uses:
    //   1. Test / verification: install a language map and confirm the seam renders it (no StringTable
    //      pipeline needed) - the "test that what we build works" path before the pipeline runs on labs.
    //   2. The cloud overlay: a post-launch cloud-corrections map layered over the baked base via
    //      CompositeLocalizationLookup (overlay-first wins).
    //   3. EditMode tests of the lookup contract.
    // No Unity Localization dependency, so it compiles everywhere (AR included).

    /// <summary>An <see cref="ILocalizationLookup"/> over an in-memory key-&gt;text dictionary.</summary>
    public sealed class DictionaryLocalizationLookup : ILocalizationLookup
    {
        readonly Dictionary<string, string> _map;

        public DictionaryLocalizationLookup(IDictionary<string, string> entries)
        {
            _map = entries != null
                ? new Dictionary<string, string>(entries)
                : new Dictionary<string, string>();
        }

        public bool TryResolve(string key, out string value)
        {
            if (!string.IsNullOrEmpty(key) && _map.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return true;
            value = null;
            return false;
        }
    }
}
