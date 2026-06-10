// Wire-format serializer for AgentObservationV1Envelope. Hand-rolled because
// Unity's UnityEngine.JsonUtility cannot serialize List<{key,jsonValue}> as a
// JSON object, and the Slice 1 constraint forbids engine imports beyond
// JsonUtility. Internal-only — no public API surface; the emitter uses it.

using System.Globalization;
using System.Text;

namespace Pitech.XR.AgentSubstrate.Observation
{
    internal static class AgentObservationEnvelopeWriter
    {
        public static string ToJson(AgentObservationV1Envelope envelope)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"version\":\"v1\",\"observations\":[");
            if (envelope?.observations != null)
            {
                for (int i = 0; i < envelope.observations.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteObservation(sb, envelope.observations[i]);
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static void WriteObservation(StringBuilder sb, AgentObservationV1 o)
        {
            sb.Append('{');
            WriteStringField(sb, "version", o.version ?? "v1", first: true);
            WriteStringField(sb, "observationId", o.observationId);
            WriteStringField(sb, "kind", o.kind);
            WriteStringField(sb, "observedAt", o.observedAt);
            WriteStringField(sb, "surface", o.surface);
            WriteNullableStringField(sb, "labId", o.labId);
            WriteNullableStringField(sb, "labVersionId", o.labVersionId);
            WriteNullableStringField(sb, "attemptId", o.attemptId);
            WriteNullableStringField(sb, "sessionId", o.sessionId);

            sb.Append(",\"semanticState\":");
            WriteSemanticState(sb, o.semanticState);

            sb.Append(",\"renderedState\":");
            if (o.renderedState == null)
            {
                sb.Append("null");
            }
            else
            {
                WriteRenderedState(sb, o.renderedState);
            }

            sb.Append(",\"engine\":");
            WriteEngine(sb, o.engine);

            sb.Append('}');
        }

        static void WriteSemanticState(StringBuilder sb, AgentObservationSemanticStateV1 s)
        {
            sb.Append('{');
            WriteStringField(sb, "summary", s?.summary ?? string.Empty, first: true);
            sb.Append(",\"attributes\":{");
            if (s?.attributes != null)
            {
                bool first = true;
                foreach (var attr in s.attributes)
                {
                    if (attr == null || string.IsNullOrEmpty(attr.key)) continue;
                    if (!first) sb.Append(',');
                    sb.Append(AgentObservationAttribute.EncodeJsonString(attr.key));
                    sb.Append(':');
                    sb.Append(string.IsNullOrEmpty(attr.jsonValue) ? "null" : attr.jsonValue);
                    first = false;
                }
            }
            sb.Append("}}");
        }

        static void WriteRenderedState(StringBuilder sb, AgentObservationRenderedStateV1 r)
        {
            sb.Append('{');
            WriteNullableStringField(sb, "text", r.text, first: true);
            WriteNullableStringField(sb, "transcript", r.transcript);
            sb.Append('}');
        }

        static void WriteEngine(StringBuilder sb, AgentObservationEngineV1 e)
        {
            sb.Append('{');
            WriteStringField(sb, "name", e?.name ?? "unity", first: true);
            WriteNullableStringField(sb, "version", e?.version);
            sb.Append('}');
        }

        static void WriteStringField(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(name).Append("\":");
            sb.Append(AgentObservationAttribute.EncodeJsonString(value ?? string.Empty));
        }

        static void WriteNullableStringField(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(name).Append("\":");
            if (value == null) sb.Append("null");
            else sb.Append(AgentObservationAttribute.EncodeJsonString(value));
        }
    }
}
