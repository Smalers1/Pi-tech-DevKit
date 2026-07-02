using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Pitech.XR.Analytics
{
    // ---------- SessionNotificationView: the in-scene toast (v3 model, 2026-07-02) ----------
    // Three AUTHORED variants - Warning / Error / Critical - one per NotificationLevel. Show(AnalyticsNotification)
    // enables ONLY the card matching notification.level, fills that card's dynamic message (+ optional heading),
    // and auto-hides. Nothing about "which variant" is hardcoded: the recorder tags each notification with a level
    // (LabAnalytics.LevelFor derives it from band severity + whether a critical gate fired), and each card's look
    // (icon, colour, layout) is authored in the scene - so the three variants differ by icon/colour/heading while
    // the message text is dynamic per event.
    //
    // Wire LabAnalytics.onNotification -> Show(AnalyticsNotification). Pure presentation: it never touches the
    // config, the grade, or the scene. TMP-only (no UnityEngine.UI dependency), matching SessionReadoutView.
    //
    // TWO authoring styles, both supported:
    //   * Three cards  - build three pre-styled card GameObjects (a red one, an amber one, ...), assign each to a
    //                    variant's `root` + `messageText`. Show() toggles which card is visible. Most design freedom.
    //   * One card     - assign the SAME root/messageText to all three variants and colour it yourself from the
    //                    notification. (This component doesn't tint; three cards is the intended path.)

    [AddComponentMenu("Pi tech/Analytics/Session Notification View")]
    [DisallowMultipleComponent]
    public sealed class SessionNotificationView : MonoBehaviour
    {
        /// <summary>One authored toast card, shown only for its level.</summary>
        [Serializable]
        public sealed class Variant
        {
            [Tooltip("The card shown ONLY when this level fires (the others are hidden). Style it freely in the scene - " +
                     "icon, colour, background. Required.")]
            public GameObject root;

            [Tooltip("This card's message label - the DYNAMIC text set per event. Required.")]
            public TMP_Text messageText;

            [Tooltip("Optional heading label on this card. If set, it's filled with the level heading below; leave " +
                     "unset to keep whatever you typed on the card in the scene.")]
            public TMP_Text headingText;
        }

        [Header("Root (the whole toast)")]
        [Tooltip("Optional CanvasGroup to fade the whole toast in/out (recommended - keeps this GameObject active so " +
                 "the auto-hide timer can run).")]
        public CanvasGroup canvasGroup;

        [Header("Variants (authored per level - nothing is hardcoded)")]
        public Variant warning = new Variant();
        public Variant error = new Variant();
        public Variant critical = new Variant();

        [Header("Headings (used only when a variant's headingText is assigned)")]
        public string warningHeading = "Careful";
        public string errorHeading = "Mistake";
        public string criticalHeading = "Critical";

        [Header("Behaviour")]
        [Tooltip("Seconds the toast stays before auto-hiding. Set 0 to keep it up until the next notification or Hide().")]
        [Min(0f)] public float autoHideSeconds = 3f;

        Coroutine _hideRoutine;

        void Awake()
        {
            HideAllCards();
            SetVisible(false);
        }

        void OnDisable()
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
        }

        /// <summary>Show the toast for a live notification. UnityEvent-callable - wire LabAnalytics.onNotification here.</summary>
        public void Show(AnalyticsNotification n)
        {
            if (n == null) return;
            Variant v = VariantFor(n.level);

            HideAllCards();
            if (v.root) v.root.SetActive(true);
            if (v.messageText) v.messageText.text = Compose(n);
            if (v.headingText) v.headingText.text = HeadingFor(n.level);

            SetVisible(true);

            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
            if (autoHideSeconds > 0f && isActiveAndEnabled) _hideRoutine = StartCoroutine(HideAfter(autoHideSeconds));
        }

        /// <summary>Hide the toast immediately. UnityEvent-callable.</summary>
        public void Hide()
        {
            if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
            HideAllCards();
            SetVisible(false);
        }

        Variant VariantFor(NotificationLevel level)
        {
            switch (level)
            {
                case NotificationLevel.Warning: return warning;
                case NotificationLevel.Error: return error;
                case NotificationLevel.Critical: return critical;
                default: return error;
            }
        }

        string HeadingFor(NotificationLevel level)
        {
            switch (level)
            {
                case NotificationLevel.Warning: return warningHeading;
                case NotificationLevel.Error: return errorHeading;
                case NotificationLevel.Critical: return criticalHeading;
                default: return errorHeading;
            }
        }

        // The dynamic body: the label of what fired, plus the subject when we have one.
        static string Compose(AnalyticsNotification n)
        {
            string body = string.IsNullOrEmpty(n.metricLabel) ? "Attention" : n.metricLabel;
            if (!string.IsNullOrEmpty(n.subjectId)) body += " - " + n.subjectId;
            return body;
        }

        void HideAllCards()
        {
            if (warning.root) warning.root.SetActive(false);
            if (error.root) error.root.SetActive(false);
            if (critical.root) critical.root.SetActive(false);
        }

        IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _hideRoutine = null;
            Hide();
        }

        // Visibility via the CanvasGroup when present; otherwise carried entirely by the per-variant cards
        // (Show enables one, Hide disables all). Never deactivates this GameObject, so the auto-hide timer survives.
        void SetVisible(bool on)
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = on ? 1f : 0f;
            canvasGroup.interactable = on;
            canvasGroup.blocksRaycasts = on;
        }
    }
}
