using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario
{
    // ---------- Mini Quiz (multiple questions on one panel; routes by score) ----------
    [Serializable]
    public class MiniQuizChoice
    {
        [Tooltip("UGUI Button for this answer option.")]
        public UnityEngine.UI.Button button;

        [Tooltip("If true, this selection counts as correct for the question.")]
        public bool isCorrect = false;

        [Header("Events")]
        [Tooltip("Invoked when this option is selected.")]
        public UnityEvent onSelected = new UnityEvent();

        [Header("Stat Effects")]
        [Tooltip("Optional stat changes when this option is selected.")]
        public List<StatEffect> effects = new();
    }

    [Serializable]
    public class MiniQuizQuestion
    {
        [Tooltip("Optional label for editor clarity (not shown automatically in UI).")]
        public string label;

        [Tooltip("Answer options for this question. Typically 2 (Yes/No) or more.")]
        public List<MiniQuizChoice> choices = new();
    }

    [Serializable]
    public class MiniQuizOutcome
    {
        [Tooltip("Optional label shown on the ScenarioGraph port.")]
        public string label;

        [Min(0)]
        [Tooltip("Minimum correct answers (inclusive) for this outcome.")]
        public int minCorrect = 0;

        [Tooltip("Maximum correct answers (inclusive). Use -1 for no maximum.")]
        public int maxCorrect = -1;

        [Tooltip("Next step (GUID) if score matches this outcome. Empty = next item in list.")]
        public string nextGuid = "";
    }

    [Serializable]
    public class MiniQuizStep : Step
    {
        [Header("Panel")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";

        [Header("Questions")]
        public List<MiniQuizQuestion> questions = new();

        public enum CompleteMode
        {
            AutoWhenAllAnswered,
            OnSubmitButton
        }

        [Header("Completion")]
        public CompleteMode completion = CompleteMode.AutoWhenAllAnswered;

        [Tooltip("Optional submit/complete button when completion == OnSubmitButton.")]
        public Button submitButton;

        [Tooltip("If true, once a question is answered its choice buttons are disabled.")]
        public bool lockQuestionAfterAnswer = true;

        [Header("Routing (by correct count)")]
        [Tooltip("Evaluate score (correct answers) and route to the first matching outcome.")]
        public List<MiniQuizOutcome> outcomes = new();

        [Tooltip("Fallback next step if no outcome matches. Empty = next item in list.")]
        public string defaultNextGuid = "";

        public override string Kind => "Mini Quiz";
    }
}
