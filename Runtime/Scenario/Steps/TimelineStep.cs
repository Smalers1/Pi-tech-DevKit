using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Pitech.XR.Scenario
{
    [Serializable]
    public class TimelineStep : Step
    {
        [Tooltip("Director in the scene that already has the PlayableAsset + bindings")]
        public PlayableDirector director;
        public bool rewindOnEnter = true;
        public bool waitForEnd = true;

        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Timeline";
    }
}
