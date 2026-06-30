#if UNITY_EDITOR
namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Canonical name of the scene "managers" root GameObject, plus the legacy name kept for
    /// backward-compatibility. Renamed 2026-06-30 (Stergios): "--- SCENE MANAGERS ---" -> "--- SCENE SETUP ---".
    /// All FIND / presence checks must match EITHER name (<see cref="IsManagersRoot"/>) so pre-rename scenes
    /// keep resolving; new roots are CREATED with the canonical <see cref="ManagersRoot"/>.
    /// </summary>
    internal static class SceneRootNames
    {
        /// <summary>Canonical managers-root name - used when CREATING the root and shown in the cockpit.</summary>
        public const string ManagersRoot = "--- SCENE SETUP ---";

        /// <summary>Legacy managers-root name (pre-2026-06-30), still accepted by resolvers/health checks.</summary>
        public const string LegacyManagersRoot = "--- SCENE MANAGERS ---";

        /// <summary>True if <paramref name="name"/> is the managers root under the canonical OR the legacy
        /// name. Use this for every find / presence check so existing scenes resolve.</summary>
        public static bool IsManagersRoot(string name)
        {
            return name == ManagersRoot || name == LegacyManagersRoot;
        }
    }
}
#endif
