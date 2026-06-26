#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof B (the literal-resolution half, Appendix I.8). Pitech.XR.Core.Editor resolves runner/quiz/
    /// stats types by hard-coded string - an invisible contract the compiler can't see and a namespace
    /// move silently breaks (the QuizResultsStep / AgentObservation class of bug). This pins each literal
    /// AND the resolution MECHANISM: the FullName literals must resolve by FullName; ScenarioGraphWindow
    /// is resolved by simple type NAME (ScenarioService.OpenGraph uses t.Name), so it stays green across
    /// WS A6's namespace wrap. These code constants ARE the committed CoreEditorTypeLiterals baseline.
    /// </summary>
    public class CoreEditorTypeLiteralTests
    {
        // Resolved by FullName across loaded assemblies (mirrors the *Service FindType string lookups).
        static readonly string[] FullNameLiterals =
        {
            "Pitech.XR.Quiz.QuizAsset",
            "Pitech.XR.Stats.StatsConfig",
            "Pitech.XR.Quiz.QuizUIController",
            "Pitech.XR.Quiz.QuizResultsUIController",
            "Pitech.XR.Stats.StatsUIController",
            "Pitech.XR.Scenario.LabConsole",
            "Pitech.XR.Scenario.Scenario",
        };

        // Resolved by simple Name (ScenarioService.OpenGraph), so the WS A6 namespace wrap keeps it green.
        static readonly string[] ByNameLiterals =
        {
            "ScenarioGraphWindow",
        };

        [Test]
        public void EveryFullNameLiteral_ResolvesByFullName()
        {
            var unresolved = FullNameLiterals.Where(n => ResolveByFullName(n) == null).ToList();
            Assert.IsEmpty(unresolved,
                "Core.Editor FullName literal(s) no longer resolve (a namespace/assembly move broke an "
                + "invisible string contract):\n  " + string.Join("\n  ", unresolved));
        }

        [Test]
        public void EveryByNameLiteral_ResolvesByTypeName()
        {
            var unresolved = ByNameLiterals.Where(n => ResolveByName(n) == null).ToList();
            Assert.IsEmpty(unresolved,
                "Type(s) resolved by simple name (ScenarioService.OpenGraph) no longer found:\n  "
                + string.Join("\n  ", unresolved));
        }

        static Type ResolveByFullName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static Type ResolveByName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                    if (t.Name == simpleName) return t;
            }
            return null;
        }
    }
}
#endif
