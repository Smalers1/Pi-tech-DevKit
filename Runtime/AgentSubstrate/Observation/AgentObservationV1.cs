// C# mirror of AgentObservationV1 from
// supabase/functions/_shared/contracts/v1/agent-observation-v1.ts (Web Portal).
// V1 is FROZEN. Add fields only when the TypeScript source adds them.
//
// Serialization contract (Slice 1 §1 constraints):
//   - All payload-bearing types are [System.Serializable] CLASSES (reference types),
//     NOT structs. JsonUtility.ToJson serializes a null class-typed field as
//     "key":null (key present, value JSON null), which is what the edge function's
//     key-presence check at supabase/functions/agent-observation/index.ts:293-295
//     requires. Nullable<T> on a struct field does NOT produce the same wire shape.
//
//   - renderedState is the canonical nullable field. Null on purpose; serializes as
//     "renderedState":null. Verified by Slice 5 test Envelope_RenderedState_NullIsExplicit.
//
//   - semanticState.attributes is wire-typed as Record<string, unknown> (JSON object
//     with per-kind value schemas). JsonUtility cannot serialize a Dictionary, and
//     the plan constraint forbids Engine imports beyond UnityEngine.JsonUtility. We
//     therefore model attributes as a list of (key, rawJsonValue) pairs; the values
//     are pre-encoded JSON fragments (string, number, bool, null, nested object/array).
//     AgentObservationEnvelopeWriter (Slice 3) re-emits the list as a JSON object on
//     the wire. Helpers on AgentObservationAttribute construct correctly-encoded values
//     so callers do not have to escape strings by hand.

using System.Collections.Generic;
using System.Globalization;

namespace Pitech.XR.AgentSubstrate.Observation
{
    [System.Serializable]
    public class AgentObservationSemanticStateV1
    {
        public string summary;
        public List<AgentObservationAttribute> attributes;

        public AgentObservationSemanticStateV1()
        {
            attributes = new List<AgentObservationAttribute>();
        }
    }

    [System.Serializable]
    public class AgentObservationAttribute
    {
        public string key;
        // Raw JSON fragment for the attribute value. e.g. "\"wear_gloves\"", "4310",
        // "true", "null". AgentObservationEnvelopeWriter concatenates these verbatim
        // into the wire object: { "step": "wear_gloves", "elapsed_ms": 4310 }.
        public string jsonValue;

        public AgentObservationAttribute() { }

        public AgentObservationAttribute(string key, string jsonValue)
        {
            this.key = key;
            this.jsonValue = jsonValue;
        }

        public static AgentObservationAttribute OfString(string key, string value)
        {
            return new AgentObservationAttribute(key, value == null ? "null" : EncodeJsonString(value));
        }

        public static AgentObservationAttribute OfLong(string key, long value)
        {
            return new AgentObservationAttribute(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static AgentObservationAttribute OfDouble(string key, double value)
        {
            // R round-trips finite doubles; treat NaN/Inf as null (JSON has no representation).
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return new AgentObservationAttribute(key, "null");
            }
            return new AgentObservationAttribute(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static AgentObservationAttribute OfBool(string key, bool value)
        {
            return new AgentObservationAttribute(key, value ? "true" : "false");
        }

        public static AgentObservationAttribute OfNull(string key)
        {
            return new AgentObservationAttribute(key, "null");
        }

        internal static string EncodeJsonString(string raw)
        {
            var sb = new System.Text.StringBuilder(raw.Length + 2);
            sb.Append('"');
            foreach (var c in raw)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }

    [System.Serializable]
    public class AgentObservationRenderedStateV1
    {
        public string text;
        public string transcript;
    }

    [System.Serializable]
    public class AgentObservationEngineV1
    {
        public string name;
        public string version;
    }

    [System.Serializable]
    public class AgentObservationV1
    {
        public string version = "v1";
        public string observationId;
        public string kind;
        public string observedAt;
        public string surface;

        public string labId;
        public string labVersionId;
        public string attemptId;
        public string sessionId;

        public AgentObservationSemanticStateV1 semanticState;
        // Null is explicitly permitted. Serialized as "renderedState":null
        // (verified by Envelope_RenderedState_NullIsExplicit).
        public AgentObservationRenderedStateV1 renderedState;
        public AgentObservationEngineV1 engine;
    }

    [System.Serializable]
    public class AgentObservationV1Envelope
    {
        public string version = "v1";
        public List<AgentObservationV1> observations;

        public AgentObservationV1Envelope()
        {
            observations = new List<AgentObservationV1>();
        }
    }
}
