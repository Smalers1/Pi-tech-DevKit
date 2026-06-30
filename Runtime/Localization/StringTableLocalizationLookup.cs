#if PITECH_HAS_LOCALIZATION
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Pitech.XR.Localization
{
    // ---------- StringTable-backed lookup (WS B2.5, gated PITECH_HAS_LOCALIZATION) ----------
    // The runtime resolver that backs the B1.5 ILocalizationLookup seam with Unity Localization's baked
    // StringTables (the launch source; the cloud overlay is post-launch via CompositeLocalizationLookup).
    // Gated #if PITECH_HAS_LOCALIZATION so it compiles ONLY where com.unity.localization is present (VR);
    // on AR / bare the seam stays the inert passthrough (source text renders as-is).
    //
    // The host installs it at startup once the locale + tables are ready:
    //   LocalizationServices.Install(new StringTableLocalizationLookup("LabStrings"));
    // (or wrap it under a CompositeLocalizationLookup to layer a cloud overlay on top, post-launch).
    //
    // SYNCHRONOUS by contract (TryResolve): it reads the selected locale's table only when it is already
    // loaded (IsDone); if the table/locale isn't ready yet or the key is missing it returns false so the
    // caller falls back to the authored source string - never a blank label. Preload the table at startup
    // (LocalizationSettings + a PreloadBehavior of Preload All) so it is resident at play.

    /// <summary>Resolves keys against a Unity Localization StringTable for the selected locale.</summary>
    public sealed class StringTableLocalizationLookup : ILocalizationLookup
    {
        readonly string _tableName;

        public StringTableLocalizationLookup(string tableName)
        {
            _tableName = tableName;
        }

        public bool TryResolve(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(_tableName)) return false;
            if (!LocalizationSettings.InitializationOperation.IsDone) return false;

            var op = LocalizationSettings.StringDatabase.GetTableAsync(_tableName);
            if (!op.IsDone) return false;   // not resident yet -> fall back to source

            StringTable table = op.Result;
            if (table == null) return false;

            StringTableEntry entry = table.GetEntry(key);
            if (entry == null) return false;

            string s = entry.GetLocalizedString();
            if (string.IsNullOrEmpty(s)) return false;   // empty translation -> fall back to source

            value = s;
            return true;
        }
    }
}
#endif
