#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof B - the public surface over Pitech.XR.* may only GAIN members (Appendix I.8). Reflects every
    /// Pitech.XR.* assembly (test assemblies excluded), enumerates public (+ protected-on-non-sealed)
    /// members as one stable sorted line each, and asserts every baseline line is still present. Removals
    /// fail (a serialized public field renamed, a reflected member gone); additions pass. The baseline
    /// (Tests/Baseline/PublicApi.Pitech.XR.txt) is updated only as a reviewed commit.
    /// </summary>
    public class PublicApiBaselineTests
    {
        [Test]
        public void PublicApi_IsAdditionsOnly_VersusBaseline()
        {
            var current = new HashSet<string>(CurrentSurface(), StringComparer.Ordinal);

            string baselineAsset = TestPaths.BaselineDir() + "/PublicApi.Pitech.XR.txt";
            string baselineDisk = TestPaths.DiskPath(baselineAsset);
            if (baselineDisk == null)
                Assert.Inconclusive("Could not resolve the test package path.");

            if (!File.Exists(baselineDisk))
            {
                var sorted = current.OrderBy(s => s, StringComparer.Ordinal);
                Directory.CreateDirectory(Path.GetDirectoryName(baselineDisk));
                File.WriteAllText(baselineDisk, string.Join("\n", sorted) + "\n");
                AssetDatabase.ImportAsset(baselineAsset);
                Assert.Inconclusive("Captured Tests/Baseline/PublicApi.Pitech.XR.txt ("
                                    + current.Count + " members). Commit it, then re-run to enforce.");
            }

            var removed = new List<string>();
            foreach (var line in File.ReadAllLines(baselineDisk))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!current.Contains(line)) removed.Add(line);
            }

            Assert.IsEmpty(removed,
                "Public API members REMOVED since baseline (not additions-only). If intentional, update the "
                + "baseline as a reviewed commit:\n  " + string.Join("\n  ", removed));
        }

        // ---- surface extraction -------------------------------------------------------------

        static IEnumerable<string> CurrentSurface()
        {
            foreach (var asm in TargetAssemblies())
            {
                Type[] types;
                try { types = asm.GetExportedTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    foreach (var m in MembersOf(type))
                        yield return type.FullName + " :: " + Describe(m);
                }
            }
        }

        static IEnumerable<Assembly> TargetAssemblies()
            => AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    string n = a.GetName().Name;
                    return n.StartsWith("Pitech.XR", StringComparison.Ordinal)
                           && !n.EndsWith(".Tests", StringComparison.Ordinal);
                })
                .OrderBy(a => a.GetName().Name, StringComparer.Ordinal);

        static IEnumerable<MemberInfo> MembersOf(Type type)
        {
            const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            const BindingFlags NonPub = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var m in type.GetMembers(Pub))
                if (!IsCompilerNoise(m)) yield return m;

            // Protected members are part of the inheritance contract on a non-sealed type.
            if (!type.IsSealed)
                foreach (var m in type.GetMembers(NonPub))
                    if (IsProtected(m) && !IsCompilerNoise(m)) yield return m;
        }

        static bool IsProtected(MemberInfo m)
        {
            switch (m)
            {
                case MethodBase mb: return mb.IsFamily || mb.IsFamilyOrAssembly;
                case FieldInfo f: return f.IsFamily || f.IsFamilyOrAssembly;
                case PropertyInfo p: return (p.GetMethod != null && (p.GetMethod.IsFamily || p.GetMethod.IsFamilyOrAssembly))
                                          || (p.SetMethod != null && (p.SetMethod.IsFamily || p.SetMethod.IsFamilyOrAssembly));
                case Type nt: return nt.IsNestedFamily || nt.IsNestedFamORAssem;
                default: return false;
            }
        }

        static bool IsCompilerNoise(MemberInfo m)
        {
            if (m.Name.IndexOf('<') >= 0) return true;                 // backing fields / local funcs
            // Property/event accessors are folded into their property/event line (which encodes
            // accessor visibility below); user-defined operators (op_*) stay as members so removing
            // one fails the baseline.
            if (m is MethodInfo mi && mi.IsSpecialName
                && !mi.Name.StartsWith("op_", StringComparison.Ordinal)) return true;
            return false;
        }

        static string Describe(MemberInfo m)
        {
            switch (m)
            {
                case MethodInfo mi:
                    return (mi.IsStatic ? "static method " : "method ")
                           + TypeName(mi.ReturnType) + " " + mi.Name + "(" + Params(mi.GetParameters()) + ")";
                case ConstructorInfo ci:
                    return "ctor(" + Params(ci.GetParameters()) + ")";
                case FieldInfo f:
                    return (f.IsStatic ? "static field " : "field ") + TypeName(f.FieldType) + " " + f.Name;
                case PropertyInfo p:
                    // Encode the accessor surface: removing/narrowing a public setter changes this
                    // line and fails the additions-only check (was a Proof B false-green hole).
                    return "property " + TypeName(p.PropertyType) + " " + p.Name + " " + Accessors(p);
                case EventInfo e:
                    return "event " + TypeName(e.EventHandlerType) + " " + e.Name;
                case Type nt:
                    return "nested type " + nt.Name;
                default:
                    return m.MemberType + " " + m.Name;
            }
        }

        static string Accessors(PropertyInfo p)
        {
            bool get = p.GetMethod != null && (p.GetMethod.IsPublic || p.GetMethod.IsFamily || p.GetMethod.IsFamilyOrAssembly);
            bool set = p.SetMethod != null && (p.SetMethod.IsPublic || p.SetMethod.IsFamily || p.SetMethod.IsFamilyOrAssembly);
            if (get && set) return "{ get; set; }";
            if (get) return "{ get; }";
            if (set) return "{ set; }";
            return "{ }";
        }

        static string Params(ParameterInfo[] ps) => string.Join(", ", ps.Select(p => TypeName(p.ParameterType)));

        static string TypeName(Type t)
        {
            if (t == null) return "void";
            if (t.IsGenericType)
            {
                string baseName = t.Name;
                int tick = baseName.IndexOf('`');
                if (tick >= 0) baseName = baseName.Substring(0, tick);
                return baseName + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
            }
            return t.Name;
        }
    }
}
#endif
