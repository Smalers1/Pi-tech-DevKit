using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Pitech.XR.Stats;
using Pitech.XR.Interactables;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public sealed class SelectionStep : Step
    {
        [Header("Source")]
        [Tooltip("Scene reference to the SelectionLists controller handling all selection lists in this scene.")]
        public SelectionLists lists;

        [Tooltip("Which list to test on. You can use either List Name or List Index.")]
        public string listKey;
        public int listIndex = -1;

        [Header("Flow")]
        [Tooltip("Reset/clear selections when this step begins.")]
        public bool resetOnEnter = true;

        public enum CompleteMode
        {
            AutoWhenRequirementMet,   // auto-advance once requirement is met
            OnSubmitButton            // wait until a submit click
        }
        public CompleteMode completion = CompleteMode.AutoWhenRequirementMet;

        [Tooltip("Optional submit button. Used only if completion == OnSubmitButton.")]
        public Button submitButton;

        [Header("Requirement")]
        [Min(0)] public int requiredSelections = 1;
        [Tooltip("If true, user must select exactly 'requiredSelections'. If false, 'at least' that many.")]
        public bool requireExactCount = false;

        [Tooltip("How many wrong selections are tolerated and still considered overall correct.")]
        [Min(0)] public int allowedWrong = 0;

        [Tooltip("Optional timeout (seconds). If > 0 and time elapses before completion, the step resolves as WRONG.")]
        [Min(0)] public float timeoutSeconds = 0f;

        [Header("Routing")]
        [Tooltip("Next step (GUID) when evaluation is CORRECT. If empty, runner may fall back to linear next.")]
        public string correctNextGuid = "";
        [Tooltip("Next step (GUID) when evaluation is WRONG. If empty, runner may fall back to linear next.")]
        public string wrongNextGuid = "";

        [Header("Optional UI")]
        public RectTransform panelRoot;
        public Animator panelAnimator;
        public string showTrigger = "Show";
        public string hideTrigger = "Hide";
        public GameObject hint;

        [Header("Events")]
        public UnityEvent onCorrect = new UnityEvent();
        public UnityEvent onWrong = new UnityEvent();

        [Header("Stat Effects")]
        public List<StatEffect> onCorrectEffects = new();
        public List<StatEffect> onWrongEffects = new();

        public override string Kind => "Selection";
    }
}
