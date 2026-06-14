#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Step 12 (spec §7.1) - the committed, export-time dependency DECLARATION that keys the gate's
    /// missing-deps skip. <c>Tests/Baseline/FixtureDeps/&lt;fixtureName&gt;.deps.json</c> lists a
    /// fixture's EXTERNAL dependencies ({guid, pathAtExport}, sorted by guid) as observed at export
    /// time in the project where the lab lives (HealthOn VR - everything resolves there). The gate
    /// skips a fixture (loud Inconclusive) iff its declaration exists, has at least one entry, and at
    /// least one declared guid does not resolve in the current project; an absent or empty declaration
    /// means the fixture is ENFORCED in full - so a DevKit change that introduces a dangling ref can
    /// never hide behind the skip. Written ONLY by the export tool (CaptureBaselineFor) - hand-authoring
    /// one to silence a red is the same laundering the re-export rule forbids. A fixture with ZERO
    /// external deps gets NO file (a stale declaration is deleted on re-export), which is how the
    /// package-internal mega/variant/synthetic stay in the never-skip set by construction.
    /// </summary>
    // Internal: declaration plumbing, kept OUT of the package's public API surface (Proof B) - the
    // EditMode net reaches it via InternalsVisibleTo (Editor/Scenario.Editor/AssemblyInfo.cs), mirroring
    // the ContentDelivery / AgentSubstrate pattern.
    internal static class FixtureDependencies
    {
        const int SchemaVersion = 1;
        const string DepsLeaf = "Baseline/FixtureDeps";
        const string DepsSuffix = ".deps.json";

        // Unity's two built-in pseudo-assets. GetDependencies reports them for any fixture touching a
        // default material/sprite/shader; they exist in every project, so never an external dependency.
        const string BuiltinExtra = "Resources/unity_builtin_extra";
        const string BuiltinDefault = "Library/unity default resources";

        // ---- the pinned public surface --------------------------------------------------------

        /// <summary><c>&lt;package&gt;/Tests/Baseline/FixtureDeps</c> as an AssetDatabase path, or null
        /// when the package Tests/ root cannot be located.</summary>
        public static string DepsDirProjectPath() => ExportLabAsTestFixture.TestsSub(DepsLeaf);

        /// <summary>
        /// Compute and write (or delete) the fixture's declaration. Externals =
        /// <c>AssetDatabase.GetDependencies(fixturePath, recursive)</c> minus the fixture itself, minus
        /// everything under the fixture's own package, minus Unity built-ins. ZERO externals: any stale
        /// declaration (and its .meta) is deleted and nothing is written. Otherwise the file is written
        /// deterministically: entries sorted by guid, 2-space indent, LF, trailing newline.
        /// </summary>
        // Internal: the declaration is written ONLY by the export tool (spec §7.1.1) - tests need
        // just the read surface below. Same assembly as ExportLabAsTestFixture/CaptureBaselineFor.
        internal static void WriteDeclaration(string fixtureAssetPath)
        {
            string depsDir = DepsDirProjectPath();
            if (depsDir == null)
            {
                Debug.LogError("[DevKit] Could not locate the DevKit package Tests/ folder - fixture deps "
                               + "declaration not written for '" + fixtureAssetPath + "'.");
                return;
            }
            string declAsset = depsDir + "/" + Path.GetFileNameWithoutExtension(fixtureAssetPath) + DepsSuffix;

            var externals = ComputeExternals(fixtureAssetPath);
            if (externals.Count == 0)
            {
                // No external deps -> NO declaration file (spec §7.1.1): the fixture is enforced
                // everywhere. Delete a stale one (DeleteAsset removes the .meta with it) so an old
                // declaration can never linger and grant an unearned skip.
                if (File.Exists(FileUtil.GetPhysicalPath(declAsset)))
                    AssetDatabase.DeleteAsset(declAsset);
                return;
            }

            ExportLabAsTestFixture.EnsureFolder(depsDir);

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schemaVersion\": ")
              .Append(SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"entries\": [\n");
            for (int i = 0; i < externals.Count; i++)
            {
                sb.Append("    {\n");
                sb.Append("      \"guid\": ").Append(JsonStr(externals[i].guid)).Append(",\n");
                sb.Append("      \"pathAtExport\": ").Append(JsonStr(externals[i].path)).Append('\n');
                sb.Append(i + 1 < externals.Count ? "    },\n" : "    }\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");

            File.WriteAllText(FileUtil.GetPhysicalPath(declAsset), sb.ToString());
            AssetDatabase.ImportAsset(declAsset);
        }

        /// <summary>
        /// The declared-but-unresolved dependencies, one line per entry, formatted
        /// "pathAtExport (guid)". EMPTY when the declaration is absent or has no entries (= the
        /// fixture is enforced, spec §7.1.2) or when every declared guid resolves here.
        /// </summary>
        public static IReadOnlyList<string> UnmetDependencies(string fixtureAssetPath)
        {
            var unmet = new List<string>();
            var decl = ReadDeclaration(fixtureAssetPath);
            if (decl == null || decl.entries == null || decl.entries.Count == 0) return unmet;

            foreach (var e in decl.entries)
            {
                if (e == null) continue;
                // An empty/garbage guid never resolves -> reads as unmet. That is the conservative
                // direction: a malformed entry surfaces loudly instead of silently enforcing-or-not.
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(e.guid ?? "")))
                    unmet.Add($"{e.pathAtExport} ({e.guid})");
            }
            return unmet;
        }

        /// <summary>True when a committed declaration file exists for this fixture (regardless of
        /// whether its entries resolve).</summary>
        public static bool HasDeclaration(string fixtureAssetPath)
        {
            string depsDir = DepsDirProjectPath();
            if (depsDir == null) return false;
            string declAsset = depsDir + "/" + Path.GetFileNameWithoutExtension(fixtureAssetPath) + DepsSuffix;
            string disk = FileUtil.GetPhysicalPath(declAsset);
            return !string.IsNullOrEmpty(disk) && File.Exists(disk);
        }

        /// <summary>The fixture names (file stems) of every committed declaration, sorted ordinally -
        /// for the orphaned-declaration hygiene check (spec §7.1.5: a deps file whose stem matches no
        /// discovered fixture is a FAIL).</summary>
        public static IReadOnlyList<string> DeclaredFixtureNames()
        {
            var names = new List<string>();
            string depsDir = DepsDirProjectPath();
            string disk = depsDir == null ? null : FileUtil.GetPhysicalPath(depsDir);
            if (string.IsNullOrEmpty(disk) || !Directory.Exists(disk)) return names;

            foreach (var f in Directory.GetFiles(disk, "*" + DepsSuffix))
            {
                string file = Path.GetFileName(f);
                if (file.EndsWith(DepsSuffix, System.StringComparison.Ordinal))
                    names.Add(file.Substring(0, file.Length - DepsSuffix.Length));
            }
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        // ---- externals computation ------------------------------------------------------------

        static List<(string guid, string path)> ComputeExternals(string fixtureAssetPath)
        {
            string fixture = fixtureAssetPath.Replace('\\', '/');
            string packageRoot = PackageRootOf(fixture);

            var externals = new List<(string guid, string path)>();
            foreach (var dep in AssetDatabase.GetDependencies(fixture, true))
            {
                string d = dep.Replace('\\', '/');
                if (string.Equals(d, fixture, System.StringComparison.Ordinal)) continue;
                if (d.StartsWith(packageRoot + "/", System.StringComparison.Ordinal)) continue;
                if (string.Equals(d, BuiltinExtra, System.StringComparison.Ordinal)) continue;
                if (string.Equals(d, BuiltinDefault, System.StringComparison.Ordinal)) continue;
                // Unity-maintained packages are built-ins for this predicate (spec §5 dependency
                // floor: ugui/TMP, timeline, inputsystem are assumed present in every consumer) -
                // without this, every UI Button's MonoScript under Packages/com.unity.ugui would
                // count as an external and the self-contained mega would acquire a declaration,
                // tripping its own §7.1.4 never-skip assert. NOTE: this `com.unity.*` prefix is
                // DELIBERATELY broader than the named floor - it also drops e.g. URP/render-pipeline
                // deps a lab may pull. That stays loud-safe: an undeclared dep that fails to resolve
                // in a consumer reads as ENFORCED (a real red), never a silent skip. A tighter exact
                // allowlist (ugui/timeline/inputsystem) is deferred to the next deliberate re-export
                // cycle - tightening it now would add entries to the just-captured .deps.json. Meta/
                // Fusion/project deps (Packages/com.meta.*, Assets/...) remain declared - the real externals.
                if (d.StartsWith("Packages/com.unity.", System.StringComparison.Ordinal)) continue;

                string guid = AssetDatabase.AssetPathToGUID(d);
                if (string.IsNullOrEmpty(guid))
                {
                    // Should not happen for a path GetDependencies just returned; surface rather than
                    // silently declare an entry the predicate could never resolve anywhere.
                    Debug.LogWarning("[DevKit] Fixture dependency without a guid skipped from the deps "
                                     + "declaration: " + d);
                    continue;
                }
                externals.Add((guid, d));
            }

            externals.Sort((a, b) => string.CompareOrdinal(a.guid, b.guid));
            return externals;
        }

        // The fixture lives under "<packageRoot>/Tests/..." (the export tool writes it there), so the
        // package root falls out of the fixture path itself - anything under the same root is internal
        // by definition. Falls back to the canonical package mount for an unexpected path shape.
        static string PackageRootOf(string fixtureAssetPath)
        {
            int i = fixtureAssetPath.IndexOf("/Tests/", System.StringComparison.Ordinal);
            return i > 0 ? fixtureAssetPath.Substring(0, i) : "Packages/com.pitech.xr.devkit";
        }

        // ---- declaration read (flat, self-owned format; DTOs keep the parse trivial) ------------

        [System.Serializable]
        sealed class DeclarationDto
        {
            public int schemaVersion;
            public List<EntryDto> entries;
        }

        [System.Serializable]
        sealed class EntryDto
        {
            public string guid;
            public string pathAtExport;
        }

        static DeclarationDto ReadDeclaration(string fixtureAssetPath)
        {
            string depsDir = DepsDirProjectPath();
            if (depsDir == null) return null;
            string declAsset = depsDir + "/" + Path.GetFileNameWithoutExtension(fixtureAssetPath) + DepsSuffix;
            string disk = FileUtil.GetPhysicalPath(declAsset);
            if (string.IsNullOrEmpty(disk) || !File.Exists(disk)) return null;

            try
            {
                return JsonUtility.FromJson<DeclarationDto>(File.ReadAllText(disk));
            }
            catch (System.Exception e)
            {
                // A corrupt declaration must not grant a skip: null reads as "no declaration" ->
                // the fixture is ENFORCED (the conservative direction), and the parse failure is loud.
                Debug.LogError($"[DevKit] Could not parse fixture deps declaration '{declAsset}' "
                               + $"({e.Message}) - treating the fixture as ENFORCED.");
                return null;
            }
        }

        // Writer-side JSON string escape (same shape as ScenarioGraphSnapshot.JsonStr - guids and asset
        // paths never carry control characters beyond these).
        static string JsonStr(string s)
        {
            var sb = new StringBuilder((s ?? "").Length + 2);
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
#endif
