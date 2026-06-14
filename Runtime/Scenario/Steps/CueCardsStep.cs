using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public class CueCardsStep : Step
    {
        [Header("Timeline sync (optional)")]
        [Tooltip("Optional clock. If empty we use a local stopwatch.")]
        public PlayableDirector director;

        [Header("Cards in order")]
        [Tooltip("UI objects for each card (order matters)")]
        public GameObject[] cards;

        [Tooltip("Cue Times (sec) = max seconds each card stays before auto-advance if player doesn’t tap. " +
                 "Length can be 1 (applies to all) or match the number of cards. Leave empty for tap-only.")]
        public float[] cueTimes;

        [Header("Behavior")]
        public bool autoShowFirst = true;
        public GameObject tapHint;

        public enum AdvanceMode
        {
            TapAnywhere,
            OnButton
        }

        [Tooltip("How the learner advances to the next cue card.")]
        public AdvanceMode advanceMode = AdvanceMode.TapAnywhere;

        [Tooltip("Optional button used when Advance Mode == OnButton.")]
        public Button nextButton;

        [Header("Optional extra object")]
        public GameObject extraObject;
        public int extraShowAtIndex = 1;
        public bool hideExtraWithFinalTap = true;
        public bool useRenderersForExtra = true;

        [Header("Transitions")]
        public float fadeDuration = 0.25f;
        public float popScale = 1.06f;
        public float popDuration = 0.18f;
        public AnimationCurve fadeCurve = null;   // null = EaseInOut
        public AnimationCurve scaleCurve = null;  // null = EaseInOut

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Cue Cards";
    }
}
