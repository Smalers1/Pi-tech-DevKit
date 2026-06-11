#if PITECH_HAS_TESTFRAMEWORK
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Pitech.XR.Core.Editor
{
    /// <summary>One run, two doors (Appendix I.9-I.11). The shared core both the <see cref="EvaluateChanges"/>
    /// window and the headless <see cref="RunAll"/> entry call - so a pre-push hook / Phase D CI attaches
    /// later with no rework. Runs the EditMode net via TestRunnerApi, filtered to the covered assemblies
    /// by NAME (no compile-time coupling to the test code), and reports a plain verdict.
    /// The whole file is gated on PITECH_HAS_TESTFRAMEWORK (versionDefine on com.unity.test-framework in
    /// the Core.Editor asmdef) so a consumer without the Test Framework package still compiles the Hub.</summary>
    public static class DevKitChecks
    {
        /// <summary>The assemblies the gate covers. AgentSubstrate's EditMode suite is DELIBERATELY
        /// excluded for now: it carries a known pre-existing failure (501 NOT_IMPLEMENTED classified
        /// Warning before the typed switch - dispositioned to the module owner 2026-06-10) that would
        /// falsely block every push. Add it here once the owner resolves that finding.</summary>
        public static readonly string[] CoveredAssemblies =
        {
            "Pitech.XR.Scenario.Editor.Tests",
            "Pitech.XR.ContentDelivery.Editor.Tests",
        };

        static TestRunnerApi _api;
        static Callbacks _callbacks;

        public static void RunEditModeGate(Action<DevKitGateResult> onComplete)
        {
            if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            if (_callbacks != null) _api.UnregisterCallbacks(_callbacks);
            _callbacks = new Callbacks(onComplete);
            _api.RegisterCallbacks(_callbacks);

            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = CoveredAssemblies,
            }));
        }

        /// <summary>Headless entry (<c>-executeMethod Pitech.XR.Core.Editor.DevKitChecks.RunAll</c>).
        /// The async run exits the editor 0 (green) / 1 (a check failed OR zero tests ran). The native
        /// <c>-runTests</c> line (I.9) remains the primary CI door; this exists so both doors share one core.</summary>
        public static void RunAll()
        {
            RunEditModeGate(r =>
            {
                Debug.Log("[DevKit] Evaluate Changes (headless): " + Summarize(r));
                foreach (var f in r.failures) Debug.LogError("[DevKit]   FAIL " + f);
                EditorApplication.Exit(r.Green ? 0 : 1);
            });
        }

        public static string Summarize(DevKitGateResult r)
        {
            if (r.RanNothing)
                return "Gate ran ZERO tests - the covered test assemblies are missing or empty. Do NOT push.";
            if (r.Green)
                return $"{r.passed} checks passed - safe to push"
                       + (r.inconclusive > 0 ? $" ({r.inconclusive} inconclusive)" : "");
            return $"{r.failed} check(s) FAILED - do not push.";
        }

        sealed class Callbacks : ICallbacks
        {
            readonly Action<DevKitGateResult> _onComplete;
            public Callbacks(Action<DevKitGateResult> onComplete) { _onComplete = onComplete; }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var gate = new DevKitGateResult();
                Collect(result, gate, isRoot: true);
                _onComplete?.Invoke(gate);
            }

            static void Collect(ITestResultAdaptor node, DevKitGateResult gate, bool isRoot = false)
            {
                bool hasChildren = false;
                foreach (var child in node.Children)
                {
                    hasChildren = true;
                    Collect(child, gate);
                }
                if (hasChildren) return;   // only count leaf cases
                if (isRoot) return;        // a childless ROOT means nothing ran - count nothing

                switch (node.TestStatus)
                {
                    case TestStatus.Passed:
                        gate.passed++;
                        break;
                    case TestStatus.Failed:
                        gate.failed++;
                        gate.failures.Add($"{node.Test.Name}: {FirstLine(node.Message)}");
                        break;
                    case TestStatus.Inconclusive:
                        gate.inconclusive++;
                        gate.inconclusiveNames.Add($"{node.Test.Name}: {FirstLine(node.Message)}");
                        break;
                    case TestStatus.Skipped:
                        gate.skipped++;
                        break;
                }
            }

            static string FirstLine(string s)
            {
                if (string.IsNullOrEmpty(s)) return "(no message)";
                int nl = s.IndexOf('\n');
                return (nl >= 0 ? s.Substring(0, nl) : s).Trim();
            }
        }
    }

    /// <summary>Plain-language roll-up of an EditMode gate run.</summary>
    public sealed class DevKitGateResult
    {
        public int passed;
        public int failed;
        public int inconclusive;
        public int skipped;
        public readonly List<string> failures = new List<string>();
        public readonly List<string> inconclusiveNames = new List<string>();

        /// <summary>True when no test case executed at all (missing/renamed assemblies, empty suite).
        /// A gate that ran nothing must never read as green.</summary>
        public bool RanNothing => Total == 0;
        public bool Green => failed == 0 && !RanNothing;
        public int Total => passed + failed + inconclusive + skipped;
    }
}
#endif
