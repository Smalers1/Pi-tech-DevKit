#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.PlayMode.Tests
{
    /// <summary>
    /// Phase D-prep SEED (Appendix I.4/I.5) - NOT a Phase A gate. Proves the golden-trace HARNESS on one
    /// happy-path fixture: load the fixture prefab, instantiate into the play-mode scene, drive the runner
    /// deterministically via SceneManager.EditorSkipFromGraph (called DIRECTLY - it is unconditional
    /// public API, so a rename fails compilation instead of silently un-driving the trace), poll
    /// StepIndex per frame to emit a transition row, and serialize a deterministic trace (stable order,
    /// InvariantCulture, LF, trailing newline) for a byte-compare.
    ///
    /// Driver discipline (hardened per the 2026-06-10 adversarial review): a driver entry is consumed
    /// ONLY when its target step is the CURRENT step - EditorSkipFromGraph silently no-ops on a guid
    /// mismatch, so popping per-frame would burn entries during multi-frame transitions and make traces
    /// frame-timing dependent. Unconsumed entries at the step cap are exposed for the test to fail on.
    ///
    /// Rows carry the full I.4 v1 fields (seq/fromIndex/toIndex/stepGuid/kind/branchGuid); the seed
    /// emits branchGuid null and an empty sideEffects array - full side-effect fidelity (UnityEvent /
    /// stat probes) and the per-Kind corpus are Phase D.
    /// </summary>
    public sealed class GoldenTraceRecorder
    {
        public sealed class DriverStep { public string stepGuid; public int branchIndex; }

        readonly List<(int seq, int from, int to, string guid, string kind)> _transitions =
            new List<(int, int, int, string, string)>();

        public string FixtureName { get; private set; }

        /// <summary>Driver entries never applied because their target step never became current
        /// before the step cap. A recording with leftovers must not become a golden.</summary>
        public int UnconsumedDriverEntries { get; private set; }

        public IEnumerator Record(string fixtureAssetPath, IReadOnlyList<DriverStep> driver, int stepCap = 256)
        {
            FixtureName = System.IO.Path.GetFileNameWithoutExtension(fixtureAssetPath);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fixtureAssetPath);
            if (prefab == null) yield break;

            var instance = Object.Instantiate(prefab);
            try
            {
                var sm = instance.GetComponentInChildren<SceneManager>(true);
                if (sm == null) yield break;

                var steps = sm.scenario != null ? sm.scenario.steps : null;

                int last = sm.StepIndex;
                int seq = 0;
                int driverCursor = 0;
                int guard = 0;

                // Let the runner start (autoStart) / settle one frame.
                yield return null;

                while (guard++ < stepCap)
                {
                    int now = sm.StepIndex;
                    if (now != last)
                    {
                        StepInfo(steps, now, out string g, out string k);
                        _transitions.Add((seq++, last, now, g, k));
                        last = now;
                    }

                    if (now < 0) break;   // -1 = finished/idle

                    // PEEK, don't pop: apply the next driver entry only when its target step is the
                    // current step, otherwise retry next frame.
                    if (driver != null && driverCursor < driver.Count && steps != null
                        && now >= 0 && now < steps.Count && steps[now] != null
                        && steps[now].guid == driver[driverCursor].stepGuid)
                    {
                        var d = driver[driverCursor++];
                        sm.EditorSkipFromGraph(d.stepGuid, d.branchIndex);
                    }

                    yield return null;
                }

                UnconsumedDriverEntries = driver != null ? driver.Count - driverCursor : 0;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static void StepInfo(List<Step> steps, int index, out string guid, out string kind)
        {
            if (steps != null && index >= 0 && index < steps.Count && steps[index] != null)
            {
                guid = steps[index].guid ?? "";
                kind = steps[index].Kind ?? "";
            }
            else
            {
                guid = "";
                kind = "";
            }
        }

        /// <summary>Deterministic JSON per the I.4 v1 schema. Rows carry the full v1 field set;
        /// branchGuid is null in the seed (Phase D resolves it from the runner's branch handshake).</summary>
        public string ToJson(IReadOnlyList<DriverStep> driver)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schemaVersion\": 1,\n");
            sb.Append("  \"fixture\": ").Append(Str(FixtureName ?? "")).Append(",\n");

            sb.Append("  \"driver\": [");
            if (driver != null && driver.Count > 0)
            {
                sb.Append('\n');
                for (int i = 0; i < driver.Count; i++)
                {
                    sb.Append("    { \"stepGuid\": ").Append(Str(driver[i].stepGuid ?? ""))
                      .Append(", \"branchIndex\": ").Append(driver[i].branchIndex.ToString(CultureInfo.InvariantCulture))
                      .Append(" }").Append(i + 1 < driver.Count ? ",\n" : "\n");
                }
                sb.Append("  ");
            }
            sb.Append("],\n");

            sb.Append("  \"trace\": [");
            if (_transitions.Count > 0)
            {
                sb.Append('\n');
                for (int i = 0; i < _transitions.Count; i++)
                {
                    var t = _transitions[i];
                    sb.Append("    { \"seq\": ").Append(t.seq.ToString(CultureInfo.InvariantCulture))
                      .Append(", \"fromIndex\": ").Append(t.from.ToString(CultureInfo.InvariantCulture))
                      .Append(", \"toIndex\": ").Append(t.to.ToString(CultureInfo.InvariantCulture))
                      .Append(", \"stepGuid\": ").Append(Str(t.guid))
                      .Append(", \"kind\": ").Append(Str(t.kind))
                      .Append(", \"branchGuid\": null }")
                      .Append(i + 1 < _transitions.Count ? ",\n" : "\n");
                }
                sb.Append("  ");
            }
            sb.Append("],\n");

            sb.Append("  \"sideEffects\": []\n");   // seed: side-effect capture lands in Phase D
            sb.Append("}\n");
            return sb.ToString();
        }

        static string Str(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
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
