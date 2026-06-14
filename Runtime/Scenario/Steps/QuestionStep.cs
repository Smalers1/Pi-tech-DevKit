using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public class Choice
    {
        [Tooltip("UGUI Button in your panel")]
        public UnityEngine.UI.Button button;

        [Header("Events")]
        [Tooltip("Invoked when this choice is selected (button pressed).")]
        public UnityEvent onSelected = new UnityEvent();

        [Tooltip("Stat changes when this is pressed")]
        public List<StatEffect> effects = new();

        [Tooltip("Next step (GUID) if this choice is picked. Empty = next item in list")]
        public string nextGuid = "";
    }

    [Serializable]
    public class QuestionStep : Step
    {
        [Header("Panel")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";
        public float fallbackHideSeconds = 50f;

        [Header("Choices")]
        public List<Choice> choices = new();

        public override string Kind => "Question";
    }
}
