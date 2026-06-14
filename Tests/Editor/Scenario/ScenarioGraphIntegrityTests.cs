#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;
using Pitech.XR.Scenario.Editor;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof A - the primary net (Appendix I.0). Read-only, runs against the committed fixture prefabs
    /// in Tests/Fixtures/Scenarios. PARAMETRIZED PER LAB (via <see cref="FixtureCorpus.Cases"/>): each
    /// fixture is its own NUnit case, so the gate reports pass/fail/skip per lab - a red surfaces EVERY
    /// affected lab, not just the first, and the passing labs are visible too. Two per-lab assertions:
    ///   (1) graph integrity INVARIANTS hold (no null entries, unique guids, routes resolve, no dangling
    ///       object refs; for UnityEvent listeners the ONLY violation is a DANGLING target - a reference
    ///       that WAS assigned and whose object (or its script) is now gone. Fully-empty rows, a
    ///       clean-null target with a method named, and a target with no method are benign authored
    ///       detritus - recorded by the snapshot, never failed. See ScenarioGraphSnapshot.CheckInvariants);
    ///   (2) the per-lab SNAPSHOT (routing topology + object-ref identities + event fingerprint) matches
    ///       the committed baseline byte-for-byte - catching a dropped or silently rewired ref/listener.
    /// Plus two SUITE-level checks: the orphaned-baseline / orphaned-deps reverse direction, and a
    /// corpus-present backstop so an empty corpus reads Inconclusive, never silently green.
    ///
    /// Baseline capture is NOT done here. A missing baseline is a per-lab Inconclusive that points at the
    /// export tool (Export Lab as Test Fixture / Generate), the only sanctioned capture path - keeping
    /// writes out of a multi-case run where execution order would make a mid-test capture unsafe.
    ///
    /// Step 12 (spec §7.1): a fixture whose committed deps declaration names &gt;=1 external dependency
    /// that does NOT resolve in this project is SKIPPED per lab (Assert.Inconclusive, never counted
    /// green). No or empty declaration = enforced. The declaration is written only by the export tool in
    /// the project where the lab resolves (HealthOn VR), never inferred from an observed failure - so a
    /// DevKit change that introduces a dangling ref can never hide behind the skip.
    /// </summary>
    public class ScenarioGraphIntegrityTests
    {
        [TestCaseSource(typeof(FixtureCorpus), nameof(FixtureCorpus.Cases))]
        public void InvariantsHold(string fixtureName)
        {
            FixtureCorpus.RequireFixture(fixtureName);
            string path = FixtureCorpus.PathFor(fixtureName);
            FixtureCorpus.SkipIfUnmetDeps(fixtureName, path);
            var (_, _, scenario) = FixtureCorpus.Resolve(fixtureName);

            var violations = ScenarioGraphSnapshot.CheckInvariants(scenario);
            if (violations.Count > 0)
                Assert.Fail($"[{fixtureName}] graph-integrity violation(s):\n  "
                            + string.Join("\n  ", violations));
        }

        [TestCaseSource(typeof(FixtureCorpus), nameof(FixtureCorpus.Cases))]
        public void Snapshot_MatchesCommittedBaseline(string fixtureName)
        {
            FixtureCorpus.RequireFixture(fixtureName);
            string path = FixtureCorpus.PathFor(fixtureName);
            FixtureCorpus.SkipIfUnmetDeps(fixtureName, path);
            var (_, _, scenario) = FixtureCorpus.Resolve(fixtureName);

            string actual = ScenarioGraphSnapshot.BuildSnapshotJson(scenario);
            string baselineAsset = TestPaths.GraphSnapshotsDir() + "/" + fixtureName + ".graph.json";
            string baselineDisk = TestPaths.DiskPath(baselineAsset);

            if (baselineDisk == null || !File.Exists(baselineDisk))
                Assert.Inconclusive($"[{fixtureName}] has no committed baseline snapshot. Capture it via "
                    + "'Export Lab as Test Fixture' (real lab) or 'Generate Synthetic Scenario Fixture' "
                    + "(synthetic), commit the .graph.json, then re-run. Baselines are captured ONLY by "
                    + "the export tool - never written mid-test.");

            string expected = File.ReadAllText(baselineDisk);
            if (!string.Equals(expected, actual, System.StringComparison.Ordinal))
                Assert.Fail($"[{fixtureName}] graph snapshot drifted from its committed baseline. If the "
                    + "change is intentional, re-capture via Export Lab as Test Fixture / Generate (--regen) "
                    + "and review the git diff before committing.");
        }

        // Reverse direction (suite-level): a committed baseline OR deps declaration whose fixture
        // vanished (deleted/renamed) means reviewed protection silently dropped - fail, don't ignore.
        // Mirrors the baseline-driven checks in ScriptGuidStabilityTests / PublicApiBaselineTests.
        // Built from ALL discovered fixtures, skipped included - a skipped fixture still has its prefab
        // + baseline present, so its baseline is NOT orphaned.
        [Test]
        public void NoOrphanedBaselineOrDepsDeclaration()
        {
            var fixtureNames = new HashSet<string>(FixtureCorpus.AllFixtureNames(), System.StringComparer.Ordinal);
            var orphans = new List<string>();

            string snapDiskDir = TestPaths.DiskPath(TestPaths.GraphSnapshotsDir());
            if (snapDiskDir != null && Directory.Exists(snapDiskDir))
            {
                foreach (var f in Directory.GetFiles(snapDiskDir, "*.graph.json"))
                {
                    string baseName = Path.GetFileName(f);
                    string fixtureName = baseName.Substring(0, baseName.Length - ".graph.json".Length);
                    if (!fixtureNames.Contains(fixtureName))
                        orphans.Add($"ORPHANED baseline: {baseName} has no matching fixture prefab. If the "
                                    + "fixture was deliberately removed, delete its baseline in the same commit.");
                }
            }

            foreach (var declared in FixtureDependencies.DeclaredFixtureNames())
                if (!fixtureNames.Contains(declared))
                    orphans.Add($"ORPHANED deps declaration: {declared}.deps.json has no matching fixture "
                                + "prefab. If the fixture was deliberately removed, delete it in the same commit.");

            if (orphans.Count > 0)
                Assert.Fail(string.Join("\n", orphans));
        }

        // Backstop (suite-level): an empty corpus reads Inconclusive, never silently green. The per-lab
        // cases above carry the actual enforcement; this guarantees the gate is never green with nothing
        // to test (the net is green-able before the corpus lands and enforces the moment a lab is added).
        [Test]
        public void Corpus_IsPresent()
        {
            if (FixtureCorpus.AllFixtureNames().Count == 0)
                Assert.Inconclusive("No scenario fixtures under Tests/Fixtures/Scenarios. Run 'Generate "
                    + "Synthetic Scenario Fixture' for the mega corpus and 'Export Lab as Test Fixture' "
                    + "(or 'Export All Test Scenes') for the labs, then commit and re-run.");
        }
    }
}
#endif
