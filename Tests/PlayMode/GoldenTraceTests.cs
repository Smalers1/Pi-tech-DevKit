#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pitech.XR.Scenario.PlayMode.Tests
{
    /// <summary>
    /// Phase D-prep SEED (Appendix I.4/I.5) - NOT a Phase A gate. Stays Ignored until a runnable golden
    /// fixture + committed trace exist (authored in Phase D, when the runner is extracted). It exists now
    /// so the <see cref="GoldenTraceRecorder"/> harness compiles, runs, and is wired into the PlayMode
    /// Test Runner; the corpus + CI attach later with no rework.
    /// </summary>
    public class GoldenTraceTests
    {
        [UnityTest]
        public IEnumerator GoldenTrace_SeedHarness_MatchesCommittedTrace()
        {
            string goldenDir = TestsSub("Golden");
            string fixturesDir = TestsSub("Fixtures/Scenarios");
            if (goldenDir == null || fixturesDir == null)
            {
                Assert.Ignore("Could not locate the package Tests folder.");
                yield break;
            }

            string goldenDisk = Path.GetFullPath(goldenDir);
            string[] goldens = Directory.Exists(goldenDisk)
                ? Directory.GetFiles(goldenDisk, "*.trace.json")
                : new string[0];

            if (goldens.Length == 0)
            {
                Assert.Ignore("Phase D-prep: no seed golden trace yet. The harness is proven once a runnable "
                              + "fixture + driver land (Phase D). Phase A's net is Proofs A/B/C (EditMode).");
                yield break;
            }

            // Phase D path: drive the matching fixture and byte-compare. (Driver parsing from the golden is
            // a Phase D detail; the seed runs with an empty driver to exercise the harness end-to-end.)
            string golden = goldens[0];
            string fixtureName = Path.GetFileNameWithoutExtension(golden).Replace(".trace", "");
            string fixturePath = fixturesDir + "/" + fixtureName + ".prefab";
            if (!File.Exists(Path.GetFullPath(fixturePath)))
            {
                Assert.Ignore($"Golden '{fixtureName}' has no matching fixture prefab yet.");
                yield break;
            }

            var recorder = new GoldenTraceRecorder();
            var driver = new List<GoldenTraceRecorder.DriverStep>();
            yield return recorder.Record(fixturePath, driver);

            Assert.AreEqual(0, recorder.UnconsumedDriverEntries,
                "Driver entries were never applied (their target step never became current) - the trace is "
                + "truncated and must not be compared or committed as a golden.");

            string actual = recorder.ToJson(driver);
            string expected = File.ReadAllText(Path.GetFullPath(golden));
            Assert.AreEqual(expected, actual, $"Golden trace drift for '{fixtureName}'.");
        }

        // <package>/Tests/<leaf>, anchored on this PlayMode test asmdef asset (no assembly reference).
        static string TestsSub(string leaf)
        {
            foreach (var guid in AssetDatabase.FindAssets("Pitech.XR.Scenario.PlayMode.Tests t:AssemblyDefinitionAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (p.EndsWith("/Pitech.XR.Scenario.PlayMode.Tests.asmdef", System.StringComparison.Ordinal))
                {
                    string playModeDir = Path.GetDirectoryName(p).Replace('\\', '/');   // <pkg>/Tests/PlayMode
                    string testsRoot = Path.GetDirectoryName(playModeDir).Replace('\\', '/'); // <pkg>/Tests
                    return testsRoot + "/" + leaf;
                }
            }
            return null;
        }
    }
}
#endif
