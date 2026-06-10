// Wire mirror of AgentObservationErrorV1 from agent-observation-v1.ts.
// Used as the JsonUtility deserialization target for error response bodies.

namespace Pitech.XR.AgentSubstrate.Observation
{
    [System.Serializable]
    public class AgentObservationErrorV1
    {
        public string version;
        public string error;
        public string message;
        public string seeAlso;
    }
}
