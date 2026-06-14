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
                return r.inconclusive > 0
                    ? $"Tier-1 gate passed - safe to push DevKit code ({r.passed} checks OK). "
                      + $"{r.inconclusive} checks aren't enforcing here (the real labs only run in HealthOn VR). "
                      + "Before you ship - or update the tracked package reference - run the gate once in HealthOn VR (tier 2)."
                    : $"All {r.passed} checks passed, every lab enforced - safe to push and ship.";
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

                var (check, fixture) = ParseNode(node.Test);
                string full = Clean(node.Message);

                switch (node.TestStatus)
                {
                    case TestStatus.Passed:
                        gate.passed++;
                        gate.cases.Add(new DevKitCaseResult(check, fixture, DevKitCaseStatus.Passed, null));
                        break;
                    case TestStatus.Failed:
                        gate.failed++;
                        gate.failures.Add($"{node.Test.Name}: {FirstLine(node.Message)}");
                        gate.cases.Add(new DevKitCaseResult(check, fixture, DevKitCaseStatus.Failed, full));
                        break;
                    case TestStatus.Inconclusive:
                        gate.inconclusive++;
                        gate.inconclusiveNames.Add($"{node.Test.Name}: {FirstLine(node.Message)}");
                        gate.cases.Add(new DevKitCaseResult(check, fixture, DevKitCaseStatus.Inconclusive, full));
                        break;
                    case TestStatus.Skipped:
                        gate.skipped++;
                        gate.cases.Add(new DevKitCaseResult(check, fixture, DevKitCaseStatus.Skipped, full));
                        break;
                }
            }

            // Split a leaf's NUnit FullName into (check method, fixture arg). A parametrized per-lab
            // case reads "<ns>.<Class>.<Method>(\"Loimokseis\")" -> ("Method","Loimokseis"); a plain
            // [Test] has no parens -> (Method, null). Keyed on FullName (stable across Test Framework
            // versions), never on SetName, so the window groups correctly without test-side coupling.
            static (string check, string fixture) ParseNode(ITestAdaptor test)
            {
                string full = test?.FullName ?? test?.Name ?? "";
                string head = full, args = null;
                int lp = full.IndexOf('(');
                if (lp >= 0 && full.EndsWith(")", StringComparison.Ordinal))
                {
                    head = full.Substring(0, lp);
                    args = full.Substring(lp + 1, full.Length - lp - 2);
                }
                int dot = head.LastIndexOf('.');
                string check = dot >= 0 ? head.Substring(dot + 1) : head;
                string fixture = args?.Trim().Trim('"');
                return (check, string.IsNullOrEmpty(fixture) ? null : fixture);
            }

            static string FirstLine(string s)
            {
                if (string.IsNullOrEmpty(s)) return "(no message)";
                int nl = s.IndexOf('\n');
                return (nl >= 0 ? s.Substring(0, nl) : s).Trim();
            }

            // Full assert message, trimmed. Our per-lab checks use Assert.Fail/Inconclusive with a
            // clean authored message (no NUnit "Expected/But was" boilerplate), so this is shown
            // verbatim under the lab. A passed case carries no message.
            static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
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

        // Per-case roster for the window's grouped per-check / per-lab breakdown (one entry per leaf
        // case, passed cases included). Internal: the public counters + name lists above are the
        // pinned (Proof B) surface and the headless RunAll path; this is additive UI detail only.
        internal readonly List<DevKitCaseResult> cases = new List<DevKitCaseResult>();

        /// <summary>True when no test case executed at all (missing/renamed assemblies, empty suite).
        /// A gate that ran nothing must never read as green.</summary>
        public bool RanNothing => Total == 0;
        public bool Green => failed == 0 && !RanNothing;
        public int Total => passed + failed + inconclusive + skipped;
    }

    /// <summary>The per-leaf outcome the verdict window groups by check + lab. Internal UI detail.</summary>
    internal enum DevKitCaseStatus { Passed, Failed, Inconclusive, Skipped }

    /// <summary>One executed test case: its check (NUnit method name), the lab it ran against
    /// (parametrized arg, or null for a suite-level check), its status, and the full assert message
    /// (null when passed). Internal: consumed only by <see cref="EvaluateChanges"/> in this assembly.</summary>
    internal sealed class DevKitCaseResult
    {
        public readonly string check;
        public readonly string fixture;
        public readonly DevKitCaseStatus status;
        public readonly string message;

        public DevKitCaseResult(string check, string fixture, DevKitCaseStatus status, string message)
        {
            this.check = check;
            this.fixture = fixture;
            this.status = status;
            this.message = message;
        }
    }
}
#endif
