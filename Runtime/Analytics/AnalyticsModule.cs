// Analytics module marker. Phase A reserved the assembly; Phase B.1 landed the INERT serialized
// schema (AnalyticsMetric / Analytic / ScoringBand / Objective / TrackedSubject / SessionRole /
// SessionRoleCapacities / LabRubric). WS B2.1 (2026-06-29) landed the BEHAVIOUR on top:
//   - AnalyticsGradeEngine  - the canonical reducer (events + rubric -> grade, map sec-11.8)
//   - AnalyticsEvent / SessionEventStream - the captured timed stream
//   - GradeResult           - the on-device readout model
//   - SessionReport / SessionReportJson / ISessionReportSink - the ONE session report + outbox seam
//   - LabAnalytics          - the in-scene recorder (subscribe -> capture -> gate by role -> emit)
//   - SessionRoleSelector   - the in-scene per-attempt role pick (UI built by the consumer)
//   - AnalyticsEquivalenceFixture - the (rubric+events)->grade golden the cloud mirror must match
//
// REFERENCE DIRECTION (corrected): this assembly references Pitech.XR.Core ONLY. It must NOT reference
// Pitech.XR.Scenario - Scenario ALREADY references Analytics (for SessionStart/StopStep + the rubric
// types), so an Analytics->Scenario edge would be a cycle. The runtime glue that needs both lives in
// Scenario (LabConsole) and reaches Analytics one-way; the report identity (tenant/user/lab/version)
// is read from Pitech.XR.Core.LabRuntimeContext (populated by ContentDelivery), NOT from a
// ContentDelivery reference. The step-fact vocabulary is Pitech.XR.Core.ScenarioFactKeys (never
// hand-typed). Distinct from the runtime telemetry under Runtime/ContentDelivery/Analytics/
// (RuntimeTelemetryAdapter - the Vicky-ingestion trace), which ships separately.
namespace Pitech.XR.Analytics
{
}
