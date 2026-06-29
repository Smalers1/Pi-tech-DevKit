#if PITECH_HAS_TESTFRAMEWORK
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>WS A3 Step 8 / Appendix I.11 - the one-click manual gate. Runs the EditMode net via the
    /// shared <see cref="DevKitChecks.RunEditModeGate"/> and shows a plain-language verdict, then a
    /// per-check / per-lab breakdown: every check is a foldout, every lab a row with its own status
    /// (pass / fail / not-enforcing / skipped) and full message - so a red surfaces EVERY affected lab,
    /// not just the first, and the passing labs are visible too. Same handler as the Hub Maintain button.
    /// Menu priority 22 keeps it in the Tools group. Gated on PITECH_HAS_TESTFRAMEWORK like
    /// <see cref="DevKitChecks"/>.</summary>
    public sealed class EvaluateChanges : EditorWindow
    {
        static readonly Color Green = new Color(0.46f, 0.80f, 0.50f);
        static readonly Color Red = new Color(0.92f, 0.45f, 0.45f);

        // Per-status dot/text colours for the breakdown.
        static readonly Color DotPass = new Color(0.34f, 0.85f, 0.52f);
        static readonly Color DotFail = new Color(0.93f, 0.42f, 0.42f);
        static readonly Color DotInconclusive = new Color(0.95f, 0.80f, 0.45f);
        static readonly Color DotSkipped = new Color(0.58f, 0.65f, 0.74f);

        // Friendly names for the scenario gate's checks; anything unmapped is humanized (underscores
        // to spaces) so checks from other covered assemblies still read cleanly.
        static readonly Dictionary<string, string> CheckLabels = new Dictionary<string, string>
        {
            { "InvariantsHold", "Graph integrity (invariants)" },
            { "LabValidators_Pass", "Lab validators (multiplayer / state budget)" },
            { "Snapshot_MatchesCommittedBaseline", "Snapshot vs committed baseline" },
            { "Reserialize_IsIdempotent", "Reserialize idempotence + committed bytes" },
            { "PrefabInstanceOverride_DoesNotChurnTheGraph", "Prefab-override does not churn the graph" },
            { "NoOrphanedBaselineOrDepsDeclaration", "No orphaned baseline / deps declaration" },
            { "Corpus_IsPresent", "Fixture corpus present" },
        };

        Label _verdict;
        VisualElement _details;
        Button _runButton;
        bool _running;
        bool _showAll;                 // verbose mode: list every passing check too
        DevKitGateResult _last;        // last result, re-rendered when the toggle flips

        [MenuItem("Pi tech/Tools/Evaluate Changes", false, 22)]
        public static void Open()
        {
            var w = GetWindow<EvaluateChanges>();
            w.titleContent = new GUIContent("Evaluate Changes");
            w.minSize = new Vector2(620, 420);
            w.Show();
            // Defer one tick so CreateGUI has built the UI before the auto-run renders into it.
            EditorApplication.delayCall += () => { if (w != null) w.Run(); };
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = DevkitTheme.Bg;

            var section = DevkitTheme.Section("Evaluate Changes (the gate)");
            section.style.paddingLeft = 14; section.style.paddingRight = 14;
            section.style.paddingTop = 12; section.style.paddingBottom = 12;
            section.Add(DevkitTheme.Body(
                "Runs the DevKit EditMode safety net (Proof A graph integrity, Proof B public API + type "
                + "literals, Proof C serialized/GUID stability, pure-logic locks). Push only on green.",
                dim: true));
            section.Add(DevkitTheme.Body(
                "Coverage: " + string.Join(" + ", DevKitChecks.CoveredAssemblies) + ". The AgentSubstrate "
                + "suite is excluded until its known pre-existing failure is resolved by its owner - run it "
                + "via Window > General > Test Runner.",
                dim: true));
            section.Add(DevkitTheme.VSpace(10));

            _verdict = new Label("Ready.")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal, fontSize = 14 }
            };
            _verdict.style.color = DevkitTheme.Text;
            section.Add(_verdict);
            section.Add(DevkitTheme.VSpace(8));

            _runButton = DevkitTheme.Primary("Run again", Run);
            section.Add(_runButton);
            section.Add(DevkitTheme.VSpace(6));
            section.Add(Legend());

            var showAll = new Toggle("Show all checks (incl. passed)") { value = _showAll };
            showAll.style.color = DevkitTheme.SubText;
            showAll.style.fontSize = 11;
            showAll.style.marginTop = 4;
            showAll.RegisterValueChangedCallback(evt => { _showAll = evt.newValue; RenderDetails(); });
            section.Add(showAll);
            section.Add(DevkitTheme.VSpace(8));

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            _details = scroll;
            section.Add(scroll);

            root.Add(section);
        }

        void Run()
        {
            if (_running) return;
            _running = true;
            if (_details != null) _details.Clear();
            if (_verdict != null) { _verdict.text = "Running the EditMode net..."; _verdict.style.color = DevkitTheme.Text; }
            if (_runButton != null) _runButton.SetEnabled(false);

            DevKitChecks.RunEditModeGate(OnComplete);
        }

        void OnComplete(DevKitGateResult r)
        {
            _running = false;
            if (_runButton != null) _runButton.SetEnabled(true);
            if (_verdict == null || _details == null) return;

            _last = r;
            _verdict.text = DevKitChecks.Summarize(r);
            _verdict.style.color = r.Green ? Green : Red;   // zero-run reads red via Green=false
            RenderDetails();
        }

        // Quiet by default: only checks that need attention are shown - FAILURES in full (expanded,
        // per-lab), "not enforcing" checks collapsed (the skipped labs are one click away), and every
        // fully-passing check folded into a single tally line. A green run is therefore ~one line, not a
        // 30-row inventory. "Show all checks" lists the passing checks too. Re-runs on the toggle.
        void RenderDetails()
        {
            if (_details == null) return;
            _details.Clear();
            if (_last == null) return;

            if (_last.RanNothing)
            {
                _details.Add(Line("The gate ran zero tests - the covered test assemblies are missing or "
                                  + "empty. Do NOT push.", Red));
                return;
            }

            int passedHidden = 0;
            bool renderedAny = false;
            foreach (var group in GroupByCheck(_last.cases))
            {
                bool allPassed = group.fail == 0 && group.inconclusive == 0 && group.skipped == 0;
                if (allPassed && !_showAll) { passedHidden++; continue; }

                // Expand only when there is a real failure to act on; "not enforcing" + all-passed stay collapsed.
                var fo = new Foldout { text = GroupHeader(group), value = group.fail > 0 };
                fo.style.color = DevkitTheme.Text;
                fo.style.marginBottom = 2;

                bool suiteLevel = group.cases.Count == 1 && group.cases[0].fixture == null;
                int ok = 0;
                foreach (var c in group.cases)
                {
                    if (c.status == DevKitCaseStatus.Passed) { ok++; continue; }   // never list passing labs individually

                    if (!suiteLevel)
                        fo.Add(DotRow(c.status, c.fixture ?? "(unnamed)", dim: false));
                    if (!string.IsNullOrEmpty(c.message))
                        fo.Add(MessageBlock(c.message, ColorFor(c.status)));
                }
                if (ok > 0) fo.Add(DotRow(DevKitCaseStatus.Passed, ok + " ok", dim: true));

                _details.Add(fo);
                renderedAny = true;
            }

            if (passedHidden > 0)
            {
                _details.Add(DevkitTheme.VSpace(4));
                string head = renderedAny ? $"{passedHidden} more check(s) passed"
                                          : $"All {passedHidden} checks passed";
                _details.Add(Line(head + (_showAll ? "" : " - tick 'Show all checks' to list them."), DotPass));
            }

            _details.Add(DevkitTheme.VSpace(8));
            _details.Add(Line($"Totals: {_last.passed} passed, {_last.failed} failed, {_last.inconclusive} "
                              + $"inconclusive, {_last.skipped} skipped.", DevkitTheme.Text));
        }

        // ---- grouping ------------------------------------------------------------------------

        const int RankFailed = 0, RankInconclusive = 1, RankSkipped = 2, RankPassed = 3;

        static int Rank(DevKitCaseStatus s)
        {
            switch (s)
            {
                case DevKitCaseStatus.Failed: return RankFailed;
                case DevKitCaseStatus.Inconclusive: return RankInconclusive;
                case DevKitCaseStatus.Skipped: return RankSkipped;
                default: return RankPassed;
            }
        }

        sealed class Group
        {
            public string check;
            public readonly List<DevKitCaseResult> cases = new List<DevKitCaseResult>();
            public int worstRank = RankPassed;
            public int pass, fail, inconclusive, skipped;
        }

        static List<Group> GroupByCheck(List<DevKitCaseResult> cases)
        {
            var byCheck = new Dictionary<string, Group>(System.StringComparer.Ordinal);
            var order = new List<Group>();
            foreach (var c in cases)
            {
                string key = c.check ?? "(check)";
                if (!byCheck.TryGetValue(key, out var g))
                {
                    g = new Group { check = key };
                    byCheck[key] = g;
                    order.Add(g);
                }
                g.cases.Add(c);
                int rank = Rank(c.status);
                if (rank < g.worstRank) g.worstRank = rank;
                switch (c.status)
                {
                    case DevKitCaseStatus.Passed: g.pass++; break;
                    case DevKitCaseStatus.Failed: g.fail++; break;
                    case DevKitCaseStatus.Inconclusive: g.inconclusive++; break;
                    case DevKitCaseStatus.Skipped: g.skipped++; break;
                }
            }

            // Sort cases within each group worst-first, then by lab name; sort groups worst-first
            // while keeping discovery order among equals (stable).
            foreach (var g in order)
                g.cases.Sort((a, b) =>
                {
                    int byRank = Rank(a.status).CompareTo(Rank(b.status));
                    return byRank != 0 ? byRank : string.CompareOrdinal(a.fixture ?? "", b.fixture ?? "");
                });
            order.Sort((a, b) => a.worstRank.CompareTo(b.worstRank));   // List.Sort is not stable, but
            return order;                                              // worstRank groups read fine unsorted-within
        }

        string GroupHeader(Group g)
        {
            string label = CheckLabels.TryGetValue(g.check, out var friendly) ? friendly : Humanize(g.check);
            var parts = new List<string>();
            if (g.fail > 0) parts.Add(g.fail + " failed");
            if (g.inconclusive > 0) parts.Add(g.inconclusive + " not enforcing");
            if (g.skipped > 0) parts.Add(g.skipped + " skipped");
            if (g.pass > 0) parts.Add(g.pass + " ok");
            return label + "  —  " + string.Join(" · ", parts);
        }

        static string Humanize(string method) => method.Replace('_', ' ');

        // ---- small UI builders ---------------------------------------------------------------

        Color ColorFor(DevKitCaseStatus s)
        {
            switch (s)
            {
                case DevKitCaseStatus.Failed: return DotFail;
                case DevKitCaseStatus.Inconclusive: return DotInconclusive;
                case DevKitCaseStatus.Skipped: return DotSkipped;
                default: return DotPass;
            }
        }

        VisualElement DotRow(DevKitCaseStatus status, string text, bool dim)
        {
            var row = DevkitTheme.Row();
            row.style.marginLeft = 4; row.style.marginTop = 1; row.style.marginBottom = 1;
            var dot = new VisualElement
            {
                style =
                {
                    width = 9, height = 9, marginRight = 7,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = ColorFor(status)
                }
            };
            row.Add(dot);
            row.Add(new Label(text) { style = { color = dim ? DevkitTheme.SubText : DevkitTheme.Text } });
            return row;
        }

        static VisualElement MessageBlock(string message, Color color)
        {
            var l = new Label(message)
            {
                style = { whiteSpace = WhiteSpace.Normal, marginLeft = 24, marginBottom = 4, fontSize = 11 }
            };
            l.style.color = color;
            return l;
        }

        VisualElement Legend()
        {
            var row = DevkitTheme.Row();
            row.Add(LegendDot(DotPass, "pass"));
            row.Add(DevkitTheme.HSpace(12));
            row.Add(LegendDot(DotFail, "fail"));
            row.Add(DevkitTheme.HSpace(12));
            row.Add(LegendDot(DotInconclusive, "not enforcing (e.g. unmet deps in the bare project - real labs enforce in HealthOn VR)"));
            return row;
        }

        static VisualElement LegendDot(Color c, string text)
        {
            var r = DevkitTheme.Row();
            r.Add(new VisualElement
            {
                style =
                {
                    width = 8, height = 8, marginRight = 5,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    backgroundColor = c
                }
            });
            r.Add(new Label(text) { style = { color = DevkitTheme.SubText, fontSize = 11 } });
            return r;
        }

        static Label Line(string text, Color color)
        {
            var l = new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 1 } };
            l.style.color = color;
            return l;
        }
    }
}
#endif
