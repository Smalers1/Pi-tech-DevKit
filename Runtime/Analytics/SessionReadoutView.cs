using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- SessionReadoutView: the two-tab lab-end readout (v3 model, 2026-07-02) ----------
    // The richer readout: a RESULTS tab (the signed-points ledger) + a DETAILS tab (only the non-perfect items,
    // explained). Pure presentation over a GradeResult - it never touches the config or the scene. Wire
    // LabAnalytics.onReadout -> Show(GradeResult); wire two tab buttons' onClick -> ShowResults() / ShowDetails()
    // (kept UI-framework-agnostic - no UnityEngine.UI dependency; pages toggle via CanvasGroup).
    //
    // TEMPLATE-ROW pattern: each list holds ONE disabled child row (a ReadoutRow with two TMP labels). Show()
    // clones it per line and fills it, colouring by state. Rows are data, so one prefab serves every lab.

    [AddComponentMenu("Pi tech/Analytics/Session Readout View")]
    [DisallowMultipleComponent]
    public sealed class SessionReadoutView : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Optional CanvasGroup to show/hide the whole readout (recommended).")]
        public CanvasGroup canvasGroup;

        [Header("Headline (shown on both tabs)")]
        [Tooltip("The big grade % / 'SESSION FAILED - <cause>' / 'Incomplete' line.")]
        public TMP_Text headlineText;
        [Tooltip("The sub-headline (e.g. 'Base 72  -  -9 penalties  -  +10 bonus').")]
        public TMP_Text subheadText;
        [Tooltip("The session stats strip (time / drops / wrong) - shown on both tabs, even on FAILED / Incomplete.")]
        public TMP_Text statsText;

        [Header("Pages (toggled by the tab buttons)")]
        public CanvasGroup resultsPage;
        public CanvasGroup detailsPage;

        [Header("RESULTS tab - the ledger")]
        [Tooltip("Container the ledger rows are spawned under.")]
        public Transform resultsList;
        [Tooltip("A disabled template row (ReadoutRow) cloned per ledger line.")]
        public ReadoutRow resultsRowTemplate;

        [Header("DETAILS tab - the explanations")]
        public Transform detailsList;
        public ReadoutRow detailsRowTemplate;

        [Header("Colours")]
        public Color passColor = new Color(0.24f, 0.72f, 0.34f);
        public Color failColor = new Color(0.86f, 0.30f, 0.28f);
        public Color warnColor = new Color(0.92f, 0.70f, 0.24f);
        public Color neutralColor = new Color(0.86f, 0.88f, 0.92f);

        readonly List<ReadoutRow> _spawned = new List<ReadoutRow>();

        void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (resultsRowTemplate) resultsRowTemplate.gameObject.SetActive(false);
            if (detailsRowTemplate) detailsRowTemplate.gameObject.SetActive(false);
            if (canvasGroup) SetVisible(false);
        }

        /// <summary>Render a computed grade. UnityEvent-callable - wire LabAnalytics.onReadout here.</summary>
        public void Show(GradeResult r)
        {
            SetVisible(true);
            ClearRows();
            ShowResults();

            if (r == null || !r.isComplete)
            {
                SetHeadline(r == null ? "Incomplete" : (r.failed ? FailHeadline(r) : "Incomplete"),
                            r != null && r.failed ? failColor : neutralColor);
                if (subheadText) subheadText.text = string.Empty;
                SetStats(r);
                return;
            }

            SetStats(r);

            if (r.failed)
            {
                SetHeadline(FailHeadline(r), failColor);
                if (subheadText) subheadText.text = "Grade 0 - the rest is shown for review.";
                BuildDetails(r);   // still explain why, for learning value
                return;
            }

            int gradePct = Mathf.RoundToInt(r.grade * 100f);
            SetHeadline(gradePct + "%", gradePct >= 50 ? passColor : failColor);
            if (subheadText)
                subheadText.text = $"Base {Mathf.RoundToInt(r.baseScore * 100f)}   " +
                                   (r.penaltyPointsTotal > 0 ? $"-  -{r.penaltyPointsTotal} penalties   " : "") +
                                   (r.bonusPointsTotal > 0 ? $"-  +{r.bonusPointsTotal} bonus" : (r.bonusesVoided ? "-  bonuses void" : ""));

            BuildResults(r);
            BuildDetails(r);
        }

        /// <summary>Switch to the RESULTS (ledger) tab. Wire a tab button here.</summary>
        public void ShowResults() { SetPage(resultsPage, true); SetPage(detailsPage, false); }

        /// <summary>Switch to the DETAILS tab. Wire a tab button here.</summary>
        public void ShowDetails() { SetPage(resultsPage, false); SetPage(detailsPage, true); }

        /// <summary>Hide the whole readout. UnityEvent-callable.</summary>
        public void Hide() => SetVisible(false);

        // ---- RESULTS: the ledger ----
        void BuildResults(GradeResult r)
        {
            AddRow(resultsList, resultsRowTemplate, "Base score", Mathf.RoundToInt(r.baseScore * 100f).ToString(), neutralColor);
            if (r.penalties != null)
                for (int i = 0; i < r.penalties.Count; i++)
                {
                    PenaltyScoreResult p = r.penalties[i];
                    if (p == null || p.pointsDeducted == 0) continue;
                    string cause = Label(p.label, "Penalty") + Occ(p);
                    AddRow(resultsList, resultsRowTemplate, cause, "-" + p.pointsDeducted, failColor);
                }
            if (r.goals != null)
                for (int i = 0; i < r.goals.Count; i++)
                {
                    GoalScoreResult g = r.goals[i];
                    if (g == null) continue;
                    if (!g.earnable) AddRow(resultsList, resultsRowTemplate, Label(g.label, "Goal"), "n/a", neutralColor);
                    else if (g.passed) AddRow(resultsList, resultsRowTemplate, Label(g.label, "Goal") + "  PASS", "+" + g.bonusPoints, passColor);
                    else AddRow(resultsList, resultsRowTemplate, Label(g.label, "Goal") + "  FAIL", "+0", failColor);
                }
            if (r.bonusesVoided) AddRow(resultsList, resultsRowTemplate, "Bonuses void - a critical step failed", "", failColor);
            AddRow(resultsList, resultsRowTemplate, "TOTAL" + ClampNote(r), Mathf.RoundToInt(r.grade * 100f).ToString(), neutralColor);
        }

        // ---- DETAILS: only the non-perfect items ----
        void BuildDetails(GradeResult r)
        {
            int perfect = 0;
            if (r.steps != null)
                for (int i = 0; i < r.steps.Count; i++)
                {
                    AnalyticScoreResult s = r.steps[i];
                    if (s == null || !s.applicable) continue;
                    bool imperfect = s.stepFailed || s.score < 0.999f;
                    if (!imperfect) { perfect++; continue; }

                    string tag = s.stepFailed ? "  FAILED" : "";
                    AddRow(detailsList, detailsRowTemplate,
                        Label(s.label, "Step") + tag,
                        s.stepFailed ? "0%" : Mathf.RoundToInt(s.score * 100f) + "%",
                        s.stepFailed ? failColor : warnColor);

                    if (s.metrics != null)
                        for (int j = 0; j < s.metrics.Count; j++)
                        {
                            MetricScoreResult m = s.metrics[j];
                            if (m == null) continue;
                            bool bad = (m.isGate && m.gateFailed) || (!m.isGate && m.worstSeverity != BandSeverity.None);
                            if (!bad) continue;
                            string right = m.isGate ? "CRITICAL" : DescribeMetric(m);
                            AddRow(detailsList, detailsRowTemplate, "   " + Label(m.label, m.kind), right,
                                m.worstSeverity == BandSeverity.Error || (m.isGate && m.gateFailed) ? failColor : warnColor);
                        }
                }
            if (perfect > 0) AddRow(detailsList, detailsRowTemplate, perfect + " step" + (perfect == 1 ? "" : "s") + " perfect", "100%", passColor);

            if (r.penalties != null)
                for (int i = 0; i < r.penalties.Count; i++)
                {
                    PenaltyScoreResult p = r.penalties[i];
                    if (p == null || p.pointsDeducted == 0) continue;
                    AddRow(detailsList, detailsRowTemplate, Label(p.label, "Penalty") + Occ(p), "-" + p.pointsDeducted + (p.capped ? " (capped)" : ""), failColor);
                }

            if (r.goals != null)
                for (int i = 0; i < r.goals.Count; i++)
                {
                    GoalScoreResult g = r.goals[i];
                    if (g == null || !g.earnable) continue;
                    AddRow(detailsList, detailsRowTemplate, Label(g.label, "Goal"), GoalMeasured(g), g.passed ? passColor : failColor);
                }
        }

        // ---- helpers ----
        static string DescribeMetric(MetricScoreResult m)
        {
            if (m.kind == StepDurationMetric.KindId) return Mathf.RoundToInt(m.rawValue) + "s";
            return Mathf.RoundToInt(m.rawValue) + "x";
        }

        static string GoalMeasured(GoalScoreResult g)
        {
            switch (g.kind)
            {
                case GoalKind.TotalTimeUnder: return FormatMinSec(g.rawValue) + " / under " + FormatMinSec(g.threshold) + (g.passed ? "  PASS" : "  FAIL");
                case GoalKind.StepsScore: return Mathf.RoundToInt(g.rawValue) + "% / need " + Mathf.RoundToInt(g.threshold) + "%" + (g.passed ? "  PASS" : "  FAIL");
                default: return Mathf.RoundToInt(g.rawValue) + " / max " + Mathf.RoundToInt(g.threshold) + (g.passed ? "  PASS" : "  FAIL");
            }
        }

        static string Occ(PenaltyScoreResult p)
        {
            int n = p.warningCount + p.errorCount;
            return n > 0 ? "  x" + n : string.Empty;
        }

        static string ClampNote(GradeResult r)
        {
            int raw = Mathf.RoundToInt(r.baseScore * 100f) - r.penaltyPointsTotal + r.bonusPointsTotal;
            if (raw < 0) return "  (floored at 0)";
            if (raw > 100) return "  (capped at 100)";
            return string.Empty;
        }

        static string FailHeadline(GradeResult r) => "SESSION FAILED" + (string.IsNullOrEmpty(r.failCauseLabel) ? "" : " - " + r.failCauseLabel);

        void SetStats(GradeResult r)
        {
            if (!statsText) return;
            SessionStats s = r != null ? r.stats : null;
            statsText.text = s == null ? string.Empty
                : $"Session: {FormatMinSec(s.totalSeconds)}  -  {s.drops} drop(s)  -  {s.wrongInteractions} wrong  -  {s.stepsGraded}/{s.stepsTotal} steps";
        }

        void SetHeadline(string text, Color c) { if (headlineText) { headlineText.text = text; headlineText.color = c; } }

        void AddRow(Transform list, ReadoutRow template, string left, string right, Color c)
        {
            if (list == null || template == null) return;
            ReadoutRow row = Instantiate(template, list);
            row.gameObject.SetActive(true);
            row.Set(left, right, c);
            _spawned.Add(row);
        }

        void ClearRows()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i]) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
        }

        static void SetPage(CanvasGroup g, bool on)
        {
            if (!g) return;
            g.alpha = on ? 1f : 0f;
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        void SetVisible(bool on)
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = on ? 1f : 0f;
                canvasGroup.interactable = on;
                canvasGroup.blocksRaycasts = on;
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                return;
            }
            gameObject.SetActive(on);
        }

        static string Label(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

        static string FormatMinSec(float seconds)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (t / 60) + ":" + (t % 60).ToString("00");
        }
    }
}
