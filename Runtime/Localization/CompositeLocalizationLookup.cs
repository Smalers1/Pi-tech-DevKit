using System.Collections.Generic;

namespace Pitech.XR.Localization
{
    /// <summary>
    /// The MERGE SEAM (WS B1.5 Step 4): resolve through an ordered chain of lookups under the SAME
    /// keys. The intended order is OVERLAY-first, BASE-last - a post-launch cloud string table is
    /// consulted before the build-baked StringTables, so a cloud correction wins without a rebuild;
    /// if the overlay has no entry, the baked base answers. First non-empty hit wins.
    ///
    /// Pure logic over <see cref="ILocalizationLookup"/> - it holds NO tables and loads no package;
    /// the cloud-overlay source itself is B.2/post-launch. Inert until installed via
    /// <see cref="LocalizationServices.Install"/>.
    /// </summary>
    public sealed class CompositeLocalizationLookup : ILocalizationLookup
    {
        readonly IReadOnlyList<ILocalizationLookup> _chain;

        /// <param name="orderedLookups">Highest priority first (overlay), lowest last (baked base).</param>
        public CompositeLocalizationLookup(params ILocalizationLookup[] orderedLookups)
            => _chain = orderedLookups ?? System.Array.Empty<ILocalizationLookup>();

        public bool TryResolve(string key, out string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                // Indexed loop, no LINQ/iterator allocation - this can be on a per-label path.
                for (int i = 0; i < _chain.Count; i++)
                {
                    var lookup = _chain[i];
                    if (lookup != null && lookup.TryResolve(key, out value) && !string.IsNullOrEmpty(value))
                        return true;
                }
            }

            value = null;
            return false;
        }
    }
}
