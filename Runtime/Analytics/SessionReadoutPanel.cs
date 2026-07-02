using TMPro;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- SessionReadoutPanel: the SIMPLE lab-end readout (v3 model) ----------
    // The minimal TMP consumer of the on-device GradeResult: an overall line + a compact ledger (base,
    // fired penalties, goals). For the richer two-tab Results/Details UI use SessionReadoutView. Pass/fail
    // is read straight off the engine's result (result.failed / result.grade) - no logic duplicated here.
    //
    // PLACEMENT: put this on the readout UI object (ideally with a CanvasGroup) and wire
    // LabAnalytics.onReadout -> Show(GradeResult) in the inspector. Uses only TMP_Text + CanvasGroup.

    /// <summary>Renders a <see cref="GradeResult"/> into TMP text (overall grade + a signed-points ledger) and
    /// shows/hides via a <see cref="CanvasGroup"/>.</summary>
    [AddComponentMenu("Pi tech/Analytics/Session Readout Panel")]
    [DisallowMultipleComponent]
    public sealed class SessionReadoutPanel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Optional CanvasGroup used to show/hide without disabling the GameObject (recommended).")]
        public CanvasGroup canvasGroup;

        [Tooltip("The overall grade / 'SESSION FAILED' / 'Incomplete' headline.")]
        public TMP_Text overallText;

        [Tooltip("The signed-points ledger (base, penalties, goals) - one line each.")]
        public TMP_Text ledgerText;

        [Header("Colours")]
        public Color passColor = new Color(0.2f, 0.7f, 0.3f);
        public Color failColor = new Color(0.8f, 0.25f, 0.2f);
        public Color neutralColor = Color.white;

        void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup) SetVisible(false);
        }

        /// <summary>Show the readout for a computed grade. UnityEvent-callable - wire onReadout here.</summary>
        public void Show(GradeResult result)
        {
            SetVisible(true);

            if (result == null || !result.isComplete)
            {
                if (overallText) { overallText.text = "Incomplete"; overallText.color = neutralColor; }
                if (ledgerText) ledgerText.text = result != null ? StatsLine(result) : string.Empty;
                return;
            }

            if (overallText)
            {
                if (result.failed)
                {
                    overallText.text = "SESSION FAILED" + (string.IsNullOrEmpty(result.failCauseLabel) ? "" : " - " + result.failCauseLabel);
                    overallText.color = failColor;
                }
                else
                {
                    overallText.text = result.grade.ToString("P0");
                    overallText.color = result.grade >= 0.5f ? passColor : failColor;
                }
            }

            if (ledgerText) ledgerText.text = result.failed ? StatsLine(result) : BuildLedger(result);
        }

        /// <summary>Hide the readout panel. UnityEvent-callable.</summary>
        public void Hide() => SetVisible(false);

        string BuildLedger(GradeResult r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Base score: ").Append(Mathf.RoundToInt(r.baseScore * 100f)).Append('\n');
            if (r.penalties != null)
                for (int i = 0; i < r.penalties.Count; i++)
                {
                    PenaltyScoreResult p = r.penalties[i];
                    if (p == null || p.pointsDeducted == 0) continue;
                    sb.Append(Label(p.label, "Penalty")).Append(": -").Append(p.pointsDeducted)
                      .Append(p.capped ? " (capped)" : "").Append('\n');
                }
            if (r.goals != null)
                for (int i = 0; i < r.goals.Count; i++)
                {
                    GoalScoreResult g = r.goals[i];
                    if (g == null) continue;
                    string state = !g.earnable ? "n/a" : (g.passed ? "PASS +" + g.bonusPoints : "FAIL +0");
                    sb.Append(Label(g.label, "Goal")).Append(": ").Append(state).Append('\n');
                }
            if (r.bonusesVoided) sb.Append("Bonuses void - a critical step failed\n");
            sb.Append("Total: ").Append(Mathf.RoundToInt(r.grade * 100f));
            sb.Append(ClampNote(r));
            sb.Append('\n').Append(StatsLine(r));
            return sb.ToString();
        }

        static string ClampNote(GradeResult r)
        {
            int raw = Mathf.RoundToInt(r.baseScore * 100f) - r.penaltyPointsTotal + r.bonusPointsTotal;
            if (raw < 0) return " (floored at 0)";
            if (raw > 100) return " (capped at 100)";
            return string.Empty;
        }

        static string StatsLine(GradeResult r)
        {
            SessionStats s = r.stats;
            if (s == null) return string.Empty;
            return $"Session: {FormatMinSec(s.totalSeconds)} - {s.drops} drop(s) - {s.wrongInteractions} wrong";
        }

        static string FormatMinSec(float seconds)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        static string Label(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

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
    }
}
