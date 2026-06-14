using System;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // ---------- Base data ----------
    [Serializable]
    public abstract class Step
    {
        public string guid;          // used by the graph to connect steps
        public Vector2 graphPos;     // node position in the graph
        protected Step() { guid = Guid.NewGuid().ToString(); }
        public abstract string Kind { get; }
    }
}
