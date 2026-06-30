using TMPro;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- SessionReadoutPanel: the lab-end readout UI (map sec-11.5) ----------
    // WS B2.1 (P3). The UI consumer of the on-device readout: it renders a computed GradeResult into TMP
    // text via a CanvasGroup, with per-objective pass/fail colouring. No cloud round-trip - the DevKit is
    // the canonical reducer, so this shows the grade straight from LabAnalytics.
    //
    // PLACEMENT: put this on the readout UI GameObject (ideally with a CanvasGroup). Wire
    // LabAnalytics.onReadout (a GradeResultEvent : UnityEvent<GradeResult>) -> Show(GradeResult) in the
    // inspector. LabAnalytics raises onReadout at SessionStop with the graded result (Participant) or a
    // presence-only GradeResult (Professor: isComplete == false, empty objectives); Spectator never raises it.
    //
    // NOTE: GradeResult has NO overall "passed" field - the overall pass/fail colour is DERIVED here (every
    // applicable objective passed). When isComplete == false the session is "incomplete": render
    // "Incomplete", never a score (map sec-11.3). Uses only TMP_Text + CanvasGroup (no UnityEngine.UI).

    /// <summary>
    /// Renders a <see cref="GradeResult"/> into TMP text (overall grade + per-objective pass/fail) and
    /// shows/hides via a <see cref="CanvasGroup"/>. Place on the readout UI object and wire
    /// <c>LabAnalytics.onReadout</c> -&gt; <see cref="Show"/> in the inspector.
    /// </summary>
    [AddComponentMenu("Pi tech/Analytics/Session Readout Panel")]
    [DisallowMultipleComponent]
    public sealed class SessionReadoutPanel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Optional CanvasGroup used to show/hide without disabling the GameObject (recommended). Resolved from this object in Awake if empty.")]
        public CanvasGroup canvasGroup;

        [Tooltip("The overall grade percentage / 'Incomplete' line.")]
        public TMP_Text overallText;

        [Tooltip("The per-objective breakdown (one line per objective).")]
        public TMP_Text objectivesText;

        [Header("Pass / fail colours")]
        [Tooltip("Colour for a passed objective and an all-passed overall grade.")]
        public Color passColor = new Color(0.2f, 0.7f, 0.3f);

        [Tooltip("Colour for a failed objective and a not-all-passed overall grade.")]
        public Color failColor = new Color(0.8f, 0.25f, 0.2f);

        void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            // Start hidden ONLY when we can do so without deactivating the GameObject (so the wired
            // onReadout -> Show still reaches this component). Without a CanvasGroup the author manages
            // visibility (a CanvasGroup is recommended above).
            if (canvasGroup) SetVisible(false);
        }

        /// <summary>Show the readout for a computed grade. UnityEvent-callable - wire
        /// <c>LabAnalytics.onReadout</c> here. Handles the presence-only / incomplete case (no score) and an
        /// empty objectives list without throwing.</summary>
        public void Show(GradeResult result)
        {
            SetVisible(true);

            if (result == null)
            {
                if (overallText) { overallText.text = "Incomplete"; overallText.color = Color.white; }
                if (objectivesText) objectivesText.text = string.Empty;
                return;
            }

            if (overallText)
            {
                if (result.isComplete)
                {
                    overallText.text = result.grade.ToString("P0");
                    overallText.color = AllApplicablePassed(result) ? passColor : failColor;
                }
                else
                {
                    overallText.text = "Incomplete";
                    overallText.color = Color.white;
                }
            }

            if (objectivesText)
                objectivesText.text = BuildObjectives(result);
        }

        /// <summary>Hide the readout panel. UnityEvent-callable.</summary>
        public void Hide() => SetVisible(false);

        // True only when at least one objective was applicable AND every applicable objective passed
        // (GradeResult has no overall 'passed' field, so the overall colour is derived).
        static bool AllApplicablePassed(GradeResult result)
        {
            if (result.objectives == null) return false;
            bool any = false;
            for (int i = 0; i < result.objectives.Count; i++)
            {
                ObjectiveScoreResult o = result.objectives[i];
                if (o == null || !o.applicable) continue;
                any = true;
                if (!o.passed) return false;
            }
            return any;
        }

        string BuildObjectives(GradeResult result)
        {
            if (result.objectives == null || result.objectives.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < result.objectives.Count; i++)
            {
                ObjectiveScoreResult o = result.objectives[i];
                if (o == null) continue;
                if (sb.Length > 0) sb.Append('\n');

                string label = string.IsNullOrEmpty(o.label) ? o.id : o.label;
                if (!o.applicable)
                {
                    sb.Append(label).Append(": n/a");
                    continue;
                }
                string hex = ColorUtility.ToHtmlStringRGB(o.passed ? passColor : failColor);
                sb.Append("<color=#").Append(hex).Append('>')
                  .Append(label).Append(": ").Append(o.score.ToString("P0"))
                  .Append(o.passed ? " (PASS)" : " (FAIL)")
                  .Append("</color>");
            }
            return sb.ToString();
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
    }
}
