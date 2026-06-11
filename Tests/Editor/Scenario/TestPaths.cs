#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Resolves the package's <c>Tests/</c> subfolders from inside the test run, independent of whether
    /// the package is embedded under <c>Packages/</c> or <c>Assets/</c>. Anchored on this test asmdef so
    /// it never hard-codes a project layout. Asset paths use forward slashes; use
    /// <see cref="DiskPath"/> to read/write the underlying file.
    /// </summary>
    internal static class TestPaths
    {
        const string AsmdefLeaf = "Pitech.XR.Scenario.Editor.Tests.asmdef";

        /// <summary>&lt;package&gt;/Tests as an AssetDatabase path, or null if not found.</summary>
        public static string TestsRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets("Pitech.XR.Scenario.Editor.Tests t:AssemblyDefinitionAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (p.EndsWith("/" + AsmdefLeaf, System.StringComparison.Ordinal))
                    return WalkUpToTests(p);
            }
            return null;
        }

        // Walk up from an asset path to the enclosing "Tests" folder. Depth-independent: the asmdef lives
        // under Tests/Editor/Scenario (one asmdef per folder), but this stays correct at any nesting.
        static string WalkUpToTests(string assetPath)
        {
            string dir = Parent(assetPath);
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (LastSegment(dir) == "Tests") return dir;
                dir = Parent(dir);
            }
            return null;
        }

        static string LastSegment(string p)
        {
            int i = p.LastIndexOf('/');
            return i >= 0 ? p.Substring(i + 1) : p;
        }

        public static string FixturesDir() => Combine(TestsRoot(), "Fixtures/Scenarios");
        public static string BaselineDir() => Combine(TestsRoot(), "Baseline");
        public static string GraphSnapshotsDir() => Combine(BaselineDir(), "GraphSnapshots");

        /// <summary>Absolute on-disk path for an AssetDatabase path, via Unity's documented
        /// virtual-to-physical resolver (correct for embedded, file:-referenced, and cached packages).</summary>
        public static string DiskPath(string assetPath)
            => assetPath == null ? null : FileUtil.GetPhysicalPath(assetPath);

        static string Parent(string assetPath) => Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        static string Combine(string a, string b) => a == null ? null : a + "/" + b;
    }
}
#endif
