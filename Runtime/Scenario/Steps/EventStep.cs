using System;
using UnityEngine;
using UnityEngine.Events;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public sealed class EventStep : Step
    {
        [Header("Events")]
        [Tooltip("Invoked when this step starts")]
        public UnityEngine.Events.UnityEvent onEnter = new UnityEngine.Events.UnityEvent();

        [Header("Flow")]
        [Tooltip("Delay after On Enter fires before the scenario advances (real-time seconds, ignores Time.timeScale). 0 = advance immediately after events.")]
        public float waitSeconds = 0f;

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Event";
    }
}
