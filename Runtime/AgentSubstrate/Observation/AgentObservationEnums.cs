// Mirror of supabase/functions/_shared/contracts/v1/agent-observation-v1.ts (Web Portal repo).
// V1 is FROZEN. Any change here must ship as a v2/ sibling type.
namespace Pitech.XR.AgentSubstrate.Observation
{
    public static class AgentObservationSurfaceV1
    {
        public const string Ar = "ar";
        public const string Vr = "vr";
    }

    public static class AgentObservationKindV1
    {
        public const string LabStepObserved = "lab_step_observed";
        public const string SceneStateSnapshot = "scene_state_snapshot";
        public const string UserInteractionObserved = "user_interaction_observed";
    }

    public static class AgentObservationErrorCodeV1
    {
        public const string SchemaInvalid = "SCHEMA_INVALID";
        public const string AuthRequired = "AUTH_REQUIRED";
        public const string TenantNotBound = "TENANT_NOT_BOUND";
        public const string ConsentNotGranted = "CONSENT_NOT_GRANTED";
        public const string SurfaceNotPermitted = "SURFACE_NOT_PERMITTED";
        public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
        public const string NotImplemented = "NOT_IMPLEMENTED";
    }
}
