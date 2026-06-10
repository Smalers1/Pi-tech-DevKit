using UnityEngine;

namespace Pitech.XR.AgentSubstrate.Observation
{
    public enum AgentObservationLogLevel
    {
        Verbose,   // Debug.Log — silent in default Unity console settings.
        Warning,   // Debug.LogWarning
        Error,     // Debug.LogError
        None,      // success path — no log
    }

    public sealed class AgentObservationResponseDisposition
    {
        public AgentObservationLogLevel LogLevel;
        public bool ShouldRetry;
        public string ErrorCode;   // typed error code, "UNKNOWN", or null on success.
        public string LogMessage;
    }

    /// <summary>
    /// Pure classifier mapping (httpStatus, responseBody) to a logging + retry
    /// disposition per plan §2.6 and §2.9. Independent of UnityWebRequest so it
    /// is fully unit-testable in EditMode without play-mode entry.
    /// </summary>
    public static class AgentObservationResponseClassifier
    {
        public static AgentObservationResponseDisposition Classify(long httpStatus, string responseBody)
        {
            if (httpStatus >= 200 && httpStatus < 300)
            {
                return new AgentObservationResponseDisposition
                {
                    LogLevel = AgentObservationLogLevel.None,
                    ShouldRetry = false,
                };
            }

            var parsed = TryParseError(responseBody);
            var code = parsed?.error;

            // 5xx — backoff and retry up to 3 times. Server-side typed error code
            // is not required for 5xx; transient failure semantics.
            if (httpStatus >= 500 && httpStatus < 600)
            {
                return new AgentObservationResponseDisposition
                {
                    LogLevel = AgentObservationLogLevel.Warning,
                    ShouldRetry = true,
                    ErrorCode = code,
                    LogMessage = "[AgentObservation] 5xx from edge function (" + httpStatus + "); will retry.",
                };
            }

            // Typed 4xx envelopes from the edge function.
            if (!string.IsNullOrEmpty(code))
            {
                switch (code)
                {
                    case AgentObservationErrorCodeV1.ConsentNotGranted:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Verbose,
                            ShouldRetry = false,
                            ErrorCode = code,
                            LogMessage = "[AgentObservation] CONSENT_NOT_GRANTED (expected pre-PIT-NEW-A); dropped.",
                        };
                    case AgentObservationErrorCodeV1.NotImplemented:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Verbose,
                            ShouldRetry = false,
                            ErrorCode = code,
                            LogMessage = "[AgentObservation] NOT_IMPLEMENTED (expected stub state); dropped.",
                        };
                    case AgentObservationErrorCodeV1.AuthRequired:
                    case AgentObservationErrorCodeV1.TenantNotBound:
                    case AgentObservationErrorCodeV1.SurfaceNotPermitted:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Warning,
                            ShouldRetry = true,
                            ErrorCode = code,
                            LogMessage = "[AgentObservation] " + code + "; will retry.",
                        };
                    case AgentObservationErrorCodeV1.SchemaInvalid:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Error,
                            ShouldRetry = false,
                            ErrorCode = code,
                            LogMessage = "[AgentObservation] SCHEMA_INVALID — own bug; dropped, no retry.",
                        };
                    case AgentObservationErrorCodeV1.MethodNotAllowed:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Error,
                            ShouldRetry = false,
                            ErrorCode = code,
                            LogMessage = "[AgentObservation] METHOD_NOT_ALLOWED; never retry.",
                        };
                    default:
                        return new AgentObservationResponseDisposition
                        {
                            LogLevel = AgentObservationLogLevel.Warning,
                            ShouldRetry = false,
                            ErrorCode = "UNKNOWN",
                            LogMessage = "[AgentObservation] Unknown error code '" + code + "'; dropped.",
                        };
                }
            }

            // Non-JSON 401/403 (likely auth proxy reject): warn, no retry.
            if (httpStatus == 401 || httpStatus == 403)
            {
                return new AgentObservationResponseDisposition
                {
                    LogLevel = AgentObservationLogLevel.Warning,
                    ShouldRetry = false,
                    ErrorCode = null,
                    LogMessage = "[AgentObservation] non-JSON " + httpStatus + " response; dropped.",
                };
            }

            return new AgentObservationResponseDisposition
            {
                LogLevel = AgentObservationLogLevel.Warning,
                ShouldRetry = false,
                ErrorCode = null,
                LogMessage = "[AgentObservation] unexpected status " + httpStatus + "; dropped.",
            };
        }

        public static void Apply(AgentObservationResponseDisposition d)
        {
            if (d == null || string.IsNullOrEmpty(d.LogMessage)) return;
            switch (d.LogLevel)
            {
                case AgentObservationLogLevel.Verbose: Debug.Log(d.LogMessage); break;
                case AgentObservationLogLevel.Warning: Debug.LogWarning(d.LogMessage); break;
                case AgentObservationLogLevel.Error: Debug.LogError(d.LogMessage); break;
            }
        }

        static AgentObservationErrorV1 TryParseError(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var parsed = JsonUtility.FromJson<AgentObservationErrorV1>(body);
                if (parsed != null && !string.IsNullOrEmpty(parsed.error)) return parsed;
            }
            catch
            {
                // not a typed error body — fall through.
            }
            return null;
        }
    }
}
