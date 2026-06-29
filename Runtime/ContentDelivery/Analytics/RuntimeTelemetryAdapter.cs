using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Pitech.XR.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Pitech.XR.ContentDelivery
{
    [Serializable]
    public sealed class RuntimeTelemetryEventData
    {
        public string action = string.Empty;
        public string step_guid = string.Empty;
        public string step_type = string.Empty;
        public string detail = string.Empty;
        public float progress_percent = -1f;
        public long downloaded_bytes = -1L;
        public long total_bytes = -1L;
        public bool critical;
    }

    [Serializable]
    public sealed class RuntimeTelemetryStepEventPayload
    {
        public string attempt_id = string.Empty;
        public string launchRequestId = string.Empty;
        public string attempt_idempotency_key = string.Empty;
        public string idempotency_key = string.Empty;
        public string lab_id = string.Empty;
        public string event_type = string.Empty;
        public RuntimeTelemetryEventData event_data = new RuntimeTelemetryEventData();
        public string client_timestamp = string.Empty;
        public int sequence_number;
    }

    [Serializable]
    public sealed class RuntimeTelemetryAttemptSessionData
    {
        public string source = "unity_runtime";
        public string completion_reason = string.Empty;
        public int hints_used;
        public int resets_used;
        public int critical_error_count;
    }

    [Serializable]
    public sealed class RuntimeTelemetryAttemptPayload
    {
        public string attempt_id = string.Empty;
        public string launchRequestId = string.Empty;
        public string idempotency_key = string.Empty;
        public string lab_id = string.Empty;
        public string lab_version_id = string.Empty;
        public string scenario_id = string.Empty;
        public string scenario_schema_version = string.Empty;
        public string scenario_config_hash = string.Empty;
        public string started_at = string.Empty;
        public string completed_at = string.Empty;
        public int duration_seconds;
        public string completion_status = "in_progress";
        public int critical_error_count;
        public int hints_used;
        public int resets_used;
        public bool is_offline_submission;
        public string device_type = string.Empty;
        public RuntimeTelemetryAttemptSessionData session_data = new RuntimeTelemetryAttemptSessionData();
    }

    [Serializable]
    public sealed class RuntimeTelemetryBatchPayload
    {
        public string contractVersion = "1.2.0";
        public RuntimeTelemetryAttemptPayload[] attempts = new RuntimeTelemetryAttemptPayload[0];
        public RuntimeTelemetryStepEventPayload[] step_events = new RuntimeTelemetryStepEventPayload[0];
    }

    [AddComponentMenu("Pi tech/Analytics/Runtime Telemetry Adapter")]
    public sealed class RuntimeTelemetryAdapter : MonoBehaviour
    {
        private const float DownloadProgressEmitIntervalSeconds = 0.5f;
        private const float DownloadProgressEmitDelta = 0.05f;

        [Tooltip("Telemetry batch callback (JSON) for bridge upload handlers.")]
        public UnityEvent<string> onTelemetryBatchJson;

        [Tooltip("Logs emitted telemetry payloads.")]
        public bool logPayloads;

        [Tooltip("Flush queued step events on interval.")]
        public bool autoFlushStepEvents = true;

        [Min(0.5f)]
        [Tooltip("Step-event flush interval in seconds.")]
        public float stepFlushIntervalSeconds = 3f;

        [Min(1)]
        [Tooltip("Maximum step events per emitted batch.")]
        public int maxStepEventsPerBatch = 10;

        [Tooltip("Automatically emits an abandoned attempt when this object is destroyed.")]
        public bool emitAbandonedOnDestroy;

        [Tooltip("Device type value stamped in attempt payloads.")]
        public string deviceType = "unity_runtime";

        [Header("Scenario Config (parameterized labs)")]
        [Tooltip("Deterministic hash or identifier for the ScenarioConfig used in this attempt. Set by lab code for parameterized scenarios.")]
        public string scenarioId = string.Empty;

        [Tooltip("Version of the ScenarioConfig schema (e.g. '1.0', '2.1'). Set by lab code or resolved from cohort pinning.")]
        public string scenarioSchemaVersion = string.Empty;

        [Tooltip("Hash of the full ScenarioConfig payload for change detection. Set by lab code.")]
        public string scenarioConfigHash = string.Empty;

        [Tooltip("Optional Scenario runner (auto-detected when empty) for automatic step telemetry.")]
        public MonoBehaviour scenarioRunner;

        [Tooltip("Automatically emits step-entered and step-completed events from the Scenario runner.")]
        public bool autoTrackScenarioSteps = true;

        [Tooltip("Emit attempt completed when Scenario runner transitions from active step to finished (-1).")]
        public bool autoEmitCompletedOnScenarioFinish = true;

        [SerializeField]
        [Tooltip("OFF (default) = the legacy per-frame reflection poll drives step telemetry - the launch " +
                 "path, identical to today. ON = the LabEventBus subscription drives it - a HIGHER-FIDELITY " +
                 "trace (captures fast/intermediate transitions the poll drops), NOT byte-identical. The bus " +
                 "path is a Phase B.2 seam: keep OFF until the bind is finished and Vicky-ingestion signs off.")]
        private bool useEventBusStepTracking = false;

        private readonly List<RuntimeTelemetryStepEventPayload> pendingStepEvents = new List<RuntimeTelemetryStepEventPayload>();
        private readonly Dictionary<string, int> sequenceByAttemptId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> finalizedAttemptIds = new HashSet<string>(StringComparer.Ordinal);

        private IContentDeliveryService service;
        private float nextStepFlushAt;
        private float lastDownloadProgressEmitAt = -1f;
        private float lastDownloadProgressValue = -1f;

        private int hintsUsed;
        private int resetsUsed;
        private int criticalErrorCount;
        private string currentAttemptId = string.Empty;
        private int lastScenarioStepIndex = int.MinValue;
        private bool scenarioRunObserved;

        // WS B1.1 Step 3: bus-driven step tracking state. Resolved once at bind; the subscription
        // replaces the per-frame FindObjectsOfType poll when useEventBusStepTracking is true.
        private Pitech.XR.Core.LabRuntimeContext labContext;
        private IDisposable busSubscription;
        private bool busBound;
        // guid -> (stepType, index) registry, built ONCE from the root scenario.steps via reflection
        // (Scenario/Step types are not visible to this assembly - see asmdef note). Root steps only,
        // matching the runner's root-only StepIndex/EmitStepFact coverage (GroupStep children excluded).
        private readonly Dictionary<string, RuntimeTelemetryStepInfo> stepRegistryByGuid =
            new Dictionary<string, RuntimeTelemetryStepInfo>(StringComparer.Ordinal);
        // Tracks whether we have seen at least one entered fact, to mirror scenarioRunObserved and
        // drive EmitAttemptCompleted on finish.
        private bool busRunObserved;
        private string busLastEnteredGuid = string.Empty;
        private bool finishCheckPending;

        private struct RuntimeTelemetryStepInfo
        {
            public string stepType;
            public int index;
        }

        private void Start()
        {
            nextStepFlushAt = Time.unscaledTime + Mathf.Max(0.5f, stepFlushIntervalSeconds);
        }

        private void OnEnable()
        {
            // Only bind the bus when the bus path is selected; with the flag OFF (launch default) the
            // legacy poll in Update() is the sole telemetry source - binding here too would double-emit
            // if the bus ever resolved. UnbindEventBus() in OnDisable stays unconditional (idempotent).
            if (useEventBusStepTracking) TryBindEventBus();
        }

        private void OnDisable()
        {
            UnbindEventBus();
        }

        private void Update()
        {
            if (autoFlushStepEvents &&
                pendingStepEvents.Count > 0 &&
                Time.unscaledTime >= nextStepFlushAt)
            {
                FlushStepEvents();
            }

            if (useEventBusStepTracking)
            {
                // Bus path: no per-frame FindObjectsOfType, no per-frame StepIndex poll. Retry bind only
                // while unbound (context may attach after a deferred spawn), then run the deferred finish check.
                if (!busBound)
                {
                    TryBindEventBus();
                }

                if (finishCheckPending)
                {
                    finishCheckPending = false;
                    if (busRunObserved &&
                        autoEmitCompletedOnScenarioFinish &&
                        TryGetScenarioStepIndex(scenarioRunner, out int idxNow) &&
                        idxNow == -1)
                    {
                        EmitAttemptCompleted();
                        busRunObserved = false;
                    }
                }
            }
            else
            {
                AutoTrackScenarioSteps();   // legacy reflection-poll fallback (unchanged)
            }
        }

        private void OnDestroy()
        {
            if (emitAbandonedOnDestroy)
            {
                EmitAttemptAbandoned(-1f);
            }
        }

        public void TrackStepCompleted(string stepGuid, string stepType)
        {
            QueueStepEvent(
                "step_completed",
                "step_completed",
                stepGuid,
                stepType,
                string.Empty,
                -1f,
                -1L,
                -1L,
                false);
        }

        public void TrackHintUsed(string stepGuid, string detail)
        {
            hintsUsed++;
            QueueStepEvent(
                "hint_used",
                "hint_used",
                stepGuid,
                string.Empty,
                detail,
                -1f,
                -1L,
                -1L,
                false);
        }

        public void TrackReset(string stepGuid, string detail)
        {
            resetsUsed++;
            QueueStepEvent(
                "reset",
                "reset",
                stepGuid,
                string.Empty,
                detail,
                -1f,
                -1L,
                -1L,
                false);
        }

        public void TrackInteraction(string action, string stepGuid, string stepType, string detail)
        {
            QueueStepEvent(
                "interaction",
                action,
                stepGuid,
                stepType,
                detail,
                -1f,
                -1L,
                -1L,
                false);
        }

        public void TrackError(string code, string message, bool critical)
        {
            if (critical)
            {
                criticalErrorCount++;
            }

            string normalizedCode = string.IsNullOrWhiteSpace(code) ? "runtime_error" : code.Trim();
            string normalizedMessage = string.IsNullOrWhiteSpace(message) ? "unknown error" : message.Trim();
            string detail = $"{normalizedCode}: {normalizedMessage}";

            QueueStepEvent(
                "error",
                "runtime_error",
                string.Empty,
                string.Empty,
                detail,
                -1f,
                -1L,
                -1L,
                critical);
        }

        public void TrackDownloadProgress(long downloadedBytes, long totalBytes)
        {
            float progress = totalBytes > 0L
                ? Mathf.Clamp01((float)downloadedBytes / totalBytes)
                : 0f;
            float now = Time.realtimeSinceStartup;

            bool emitNow = lastDownloadProgressEmitAt < 0f ||
                           (now - lastDownloadProgressEmitAt) >= DownloadProgressEmitIntervalSeconds ||
                           Mathf.Abs(progress - lastDownloadProgressValue) >= DownloadProgressEmitDelta ||
                           progress >= 1f;

            if (!emitNow)
            {
                return;
            }

            lastDownloadProgressEmitAt = now;
            lastDownloadProgressValue = progress;

            QueueStepEvent(
                "interaction",
                "download_progress",
                string.Empty,
                "content_delivery",
                string.Empty,
                progress,
                downloadedBytes,
                totalBytes,
                false);
        }

        public void EmitAttemptCompleted()
        {
            EmitAttemptEnd("completed", "scenario_completed", null);
        }

        public void EmitAttemptFailed(string reason)
        {
            EmitAttemptEnd("failed", FirstNonEmpty(reason, "runtime_failed"), null);
        }

        public void EmitAttemptAbandoned(float sessionDurationSeconds)
        {
            float? durationOverride = sessionDurationSeconds >= 0f
                ? sessionDurationSeconds
                : (float?)null;
            EmitAttemptEnd("abandoned", "experience_abandoned", durationOverride);
        }

        public void FlushStepEvents()
        {
            if (pendingStepEvents.Count == 0)
            {
                nextStepFlushAt = Time.unscaledTime + Mathf.Max(0.5f, stepFlushIntervalSeconds);
                return;
            }

            int cappedBatchSize = Mathf.Max(1, maxStepEventsPerBatch);
            int batchCount = Mathf.Min(cappedBatchSize, pendingStepEvents.Count);
            RuntimeTelemetryStepEventPayload[] batch = new RuntimeTelemetryStepEventPayload[batchCount];
            pendingStepEvents.CopyTo(0, batch, 0, batchCount);
            pendingStepEvents.RemoveRange(0, batchCount);

            EmitBatch(new RuntimeTelemetryAttemptPayload[0], batch);
            nextStepFlushAt = Time.unscaledTime + Mathf.Max(0.5f, stepFlushIntervalSeconds);
        }

        private void FlushAllStepEvents()
        {
            while (pendingStepEvents.Count > 0)
            {
                FlushStepEvents();
            }
        }

        private void QueueStepEvent(
            string eventType,
            string action,
            string stepGuid,
            string stepType,
            string detail,
            float progress,
            long downloadedBytes,
            long totalBytes,
            bool critical)
        {
            if (!TryGetValidatedContext(requireResolvedVersionId: false, out LaunchContext context, out string reason))
            {
                Debug.LogWarning($"[Analytics] Skipping step event: {reason}", this);
                return;
            }

            EnsureAttemptState(context.attemptId);
            int sequence = NextSequence(context.attemptId);

            RuntimeTelemetryStepEventPayload payload = new RuntimeTelemetryStepEventPayload
            {
                attempt_id = context.attemptId,
                launchRequestId = context.launchRequestId,
                attempt_idempotency_key = context.idempotencyKey,
                idempotency_key = $"step:{context.attemptId}:{sequence}",
                lab_id = context.labId,
                event_type = string.IsNullOrWhiteSpace(eventType) ? "interaction" : eventType,
                client_timestamp = Timestamp.UtcNowIso8601(),
                sequence_number = sequence,
                event_data = new RuntimeTelemetryEventData
                {
                    action = string.IsNullOrWhiteSpace(action) ? "interaction" : action,
                    step_guid = FirstNonEmpty(stepGuid),
                    step_type = FirstNonEmpty(stepType),
                    detail = FirstNonEmpty(detail),
                    progress_percent = progress,
                    downloaded_bytes = downloadedBytes,
                    total_bytes = totalBytes,
                    critical = critical,
                },
            };

            pendingStepEvents.Add(payload);
            if (pendingStepEvents.Count >= Mathf.Max(1, maxStepEventsPerBatch))
            {
                FlushStepEvents();
            }
        }

        private void EmitAttemptEnd(string completionStatus, string completionReason, float? sessionDurationOverride)
        {
            if (!TryGetValidatedContext(requireResolvedVersionId: true, out LaunchContext context, out string reason))
            {
                Debug.LogWarning($"[Analytics] Skipping attempt payload: {reason}", this);
                return;
            }

            EnsureAttemptState(context.attemptId);
            if (finalizedAttemptIds.Contains(context.attemptId))
            {
                return;
            }

            FlushAllStepEvents();

            string completedAt = Timestamp.UtcNowIso8601();
            int durationSeconds = ResolveDurationSeconds(context.requestedAt, completedAt, sessionDurationOverride);

            RuntimeTelemetryAttemptPayload attempt = new RuntimeTelemetryAttemptPayload
            {
                attempt_id = context.attemptId,
                launchRequestId = context.launchRequestId,
                idempotency_key = context.idempotencyKey,
                lab_id = context.labId,
                lab_version_id = context.resolvedVersionId,
                scenario_id = FirstNonEmpty(scenarioId),
                scenario_schema_version = FirstNonEmpty(scenarioSchemaVersion),
                scenario_config_hash = FirstNonEmpty(scenarioConfigHash),
                started_at = FirstNonEmpty(context.requestedAt, completedAt),
                completed_at = completedAt,
                duration_seconds = durationSeconds,
                completion_status = NormalizeCompletionStatus(completionStatus),
                critical_error_count = criticalErrorCount,
                hints_used = hintsUsed,
                resets_used = resetsUsed,
                is_offline_submission = context.launchedFromCache,
                device_type = FirstNonEmpty(deviceType, "unity_runtime"),
                session_data = new RuntimeTelemetryAttemptSessionData
                {
                    source = "unity_runtime",
                    completion_reason = FirstNonEmpty(completionReason),
                    hints_used = hintsUsed,
                    resets_used = resetsUsed,
                    critical_error_count = criticalErrorCount,
                },
            };

            if (!LaunchContextValidation.TryValidateAttemptPayload(attempt, out string attemptValidationError))
            {
                Debug.LogWarning($"[Analytics] Attempt payload failed validation: {attemptValidationError}", this);
            }

            EmitBatch(
                new[] { attempt },
                new RuntimeTelemetryStepEventPayload[0]);
            finalizedAttemptIds.Add(context.attemptId);
        }

        private bool TryGetValidatedContext(bool requireResolvedVersionId, out LaunchContext context, out string reason)
        {
            reason = string.Empty;
            context = null;

            if (service == null)
            {
                service = XRServices.Get<IContentDeliveryService>();
            }

            if (service == null || !service.TryGetCurrentContext(out context) || context == null)
            {
                reason = "Launch context is unavailable.";
                return false;
            }

            if (!LaunchContextValidation.TryValidateLineage(context, requireResolvedVersionId, out reason))
            {
                return false;
            }

            return true;
        }

        private void EnsureAttemptState(string attemptId)
        {
            if (string.Equals(currentAttemptId, attemptId, StringComparison.Ordinal))
            {
                return;
            }

            currentAttemptId = attemptId;
            hintsUsed = 0;
            resetsUsed = 0;
            criticalErrorCount = 0;
            lastDownloadProgressEmitAt = -1f;
            lastDownloadProgressValue = -1f;
        }

        private int NextSequence(string attemptId)
        {
            if (!sequenceByAttemptId.TryGetValue(attemptId, out int next))
            {
                next = 1;
            }

            sequenceByAttemptId[attemptId] = next + 1;
            return next;
        }

        private void EmitBatch(RuntimeTelemetryAttemptPayload[] attempts, RuntimeTelemetryStepEventPayload[] stepEvents)
        {
            RuntimeTelemetryBatchPayload payload = new RuntimeTelemetryBatchPayload
            {
                contractVersion = "1.2.0",
                attempts = attempts ?? new RuntimeTelemetryAttemptPayload[0],
                step_events = stepEvents ?? new RuntimeTelemetryStepEventPayload[0],
            };

            string json = JsonUtility.ToJson(payload);
            if (logPayloads)
            {
                Debug.Log($"[Analytics] Telemetry batch: {json}", this);
            }

            AndroidUnityBridgeEmitter.EmitTelemetryBatchJson(json);
            onTelemetryBatchJson?.Invoke(json);
        }

        private void AutoTrackScenarioSteps()
        {
            if (!autoTrackScenarioSteps)
            {
                return;
            }

            if (scenarioRunner == null)
            {
                scenarioRunner = FindScenarioManagerLike();
                if (scenarioRunner == null)
                {
                    return;
                }
            }

            if (!TryGetScenarioStepIndex(scenarioRunner, out int currentStepIndex))
            {
                return;
            }

            if (lastScenarioStepIndex == int.MinValue)
            {
                lastScenarioStepIndex = currentStepIndex;
                if (currentStepIndex >= 0)
                {
                    scenarioRunObserved = true;
                    TryResolveScenarioStepInfo(scenarioRunner, currentStepIndex, out string stepGuid, out string stepType);
                    TrackInteraction("step_entered", stepGuid, stepType, $"index={currentStepIndex}");
                }
                return;
            }

            if (currentStepIndex == lastScenarioStepIndex)
            {
                return;
            }

            if (lastScenarioStepIndex >= 0)
            {
                TryResolveScenarioStepInfo(scenarioRunner, lastScenarioStepIndex, out string completedGuid, out string completedType);
                TrackStepCompleted(completedGuid, completedType);
            }

            if (currentStepIndex >= 0)
            {
                scenarioRunObserved = true;
                TryResolveScenarioStepInfo(scenarioRunner, currentStepIndex, out string enteredGuid, out string enteredType);
                TrackInteraction("step_entered", enteredGuid, enteredType, $"index={currentStepIndex}");
            }
            else if (currentStepIndex == -1 &&
                     scenarioRunObserved &&
                     autoEmitCompletedOnScenarioFinish)
            {
                EmitAttemptCompleted();
                scenarioRunObserved = false;
            }

            lastScenarioStepIndex = currentStepIndex;
        }

        private void TryBindEventBus()
        {
            if (!useEventBusStepTracking || !autoTrackScenarioSteps)
            {
                return;
            }

            if (busBound)
            {
                return;
            }

            // Resolve the per-attempt context. ContentDelivery attaches LabRuntimeContext on the spawned
            // lab root (WS B1.1 Step 2). For menu/direct labs there is no context -> stay unbound; Update
            // retries (cheap GetComponentInParent walk, not FindObjectsOfType). We walk up from THIS
            // adapter, which ContentDeliverySpawner parents under the lab root alongside the runner.
            if (labContext == null)
            {
                labContext = Pitech.XR.Core.LabRuntimeContext.Find(this);
            }

            if (labContext == null)
            {
                return;
            }

            // Build the guid -> {type,index} registry ONCE from the scenario runner found on the lab root.
            // Still reflection (Scenario/Step types live in Pitech.XR.Scenario which this assembly does not
            // reference), but iterated once at bind instead of indexed per frame.
            if (scenarioRunner == null)
            {
                scenarioRunner = FindScenarioManagerLike();   // one-shot; kept for legacy fallback parity
            }

            BuildStepRegistry(scenarioRunner);

            busSubscription = labContext.Bus.Subscribe(OnLabFact);
            busBound = true;
            busRunObserved = false;
            busLastEnteredGuid = string.Empty;
        }

        private void UnbindEventBus()
        {
            if (busSubscription != null)
            {
                busSubscription.Dispose();   // idempotent (LabEventBus.Subscription.Dispose)
                busSubscription = null;
            }

            busBound = false;
            labContext = null;
        }

        private void BuildStepRegistry(MonoBehaviour runner)
        {
            stepRegistryByGuid.Clear();
            if (runner == null)
            {
                return;
            }

            FieldInfo scenarioField = runner.GetType().GetField(
                "scenario",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (scenarioField == null)
            {
                return;
            }

            object scenario = scenarioField.GetValue(runner);
            if (scenario == null)
            {
                return;
            }

            FieldInfo stepsField = scenario.GetType().GetField(
                "steps",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (stepsField == null)
            {
                return;
            }

            if (!(stepsField.GetValue(scenario) is IList steps))
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                object step = steps[i];
                if (step == null)
                {
                    continue;   // mirror the runner's null-skip (Run() line 130)
                }

                string stepType = step.GetType().Name;
                string stepGuid = string.Empty;
                FieldInfo guidField = step.GetType().GetField(
                    "guid",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (guidField != null && guidField.GetValue(step) is string guid)
                {
                    stepGuid = FirstNonEmpty(guid);
                }

                if (string.IsNullOrEmpty(stepGuid))
                {
                    continue;
                }

                // First-wins on duplicate guids: the runner's FindIndexByGuid also returns the FIRST match
                // (ScenarioRunner.cs:224-226), so index semantics match.
                if (!stepRegistryByGuid.ContainsKey(stepGuid))
                {
                    stepRegistryByGuid[stepGuid] = new RuntimeTelemetryStepInfo { stepType = stepType, index = i };
                }
            }
        }

        private void OnLabFact(in Pitech.XR.Core.LabEvent fact)
        {
            // Bus facts carry guid in Text (ScenarioRunner.cs:81). Rebuild type+index from the registry.
            string stepGuid = fact.Text;
            RuntimeTelemetryStepInfo info = default;   // definitely-assigned on every path (the && short-circuit leaves the out-arg unset when the guid is empty)
            bool known = !string.IsNullOrEmpty(stepGuid) && stepRegistryByGuid.TryGetValue(stepGuid, out info);
            if (!known)
            {
                // Registry may have been empty at bind (scenario assigned late) - rebuild once and retry.
                if (stepRegistryByGuid.Count == 0)
                {
                    if (scenarioRunner == null)
                    {
                        scenarioRunner = FindScenarioManagerLike();
                    }

                    BuildStepRegistry(scenarioRunner);
                    known = stepRegistryByGuid.TryGetValue(stepGuid, out info);
                }
                else
                {
                    info = default;
                }
            }

            string stepType = known ? info.stepType : string.Empty;

            if (string.Equals(fact.Key, Pitech.XR.Core.ScenarioFactKeys.StepEntered, StringComparison.Ordinal))
            {
                busRunObserved = true;
                busLastEnteredGuid = stepGuid;
                // detail MUST be "index=N" with N = root list index, no spaces, invariant int formatting -
                // identical to $"index={currentStepIndex}" (legacy). Fallback "index=-1" only if the guid
                // is unknown (registry miss) - not expected for spawned labs. See behaviour contract.
                int indexValue = known ? info.index : -1;
                string detail = "index=" + indexValue.ToString(CultureInfo.InvariantCulture);
                TrackInteraction("step_entered", stepGuid, stepType, detail);
            }
            else if (string.Equals(fact.Key, Pitech.XR.Core.ScenarioFactKeys.StepCompleted, StringComparison.Ordinal))
            {
                TrackStepCompleted(stepGuid, stepType);
                // Finish detection: the runner publishes no "-1" / "scenario finished" fact (it only emits
                // entered/completed, ScenarioRunner.cs:133/194). The OLD poll fired EmitAttemptCompleted when
                // StepIndex flipped to -1 after the final completed. To preserve that we watch StepIndex
                // going -1 right after a completed fact, on the next Update frame.
                ScheduleFinishCheck();
            }
        }

        private void ScheduleFinishCheck()
        {
            finishCheckPending = true;   // evaluated in Update next frame (StepIndex settles to -1 after Run() exits)
        }

        private static MonoBehaviour FindScenarioManagerLike()
        {
            MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour behaviour = all[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().FullName == "Pitech.XR.Scenario.LabConsole")
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static bool TryGetScenarioStepIndex(MonoBehaviour runner, out int stepIndex)
        {
            stepIndex = -1;
            if (runner == null)
            {
                return false;
            }

            PropertyInfo stepIndexProperty = runner.GetType().GetProperty(
                "StepIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (stepIndexProperty == null)
            {
                return false;
            }

            object value = stepIndexProperty.GetValue(runner, null);
            if (value is int index)
            {
                stepIndex = index;
                return true;
            }

            return false;
        }

        private static bool TryResolveScenarioStepInfo(
            MonoBehaviour runner,
            int stepIndex,
            out string stepGuid,
            out string stepType)
        {
            stepGuid = string.Empty;
            stepType = string.Empty;

            if (runner == null || stepIndex < 0)
            {
                return false;
            }

            FieldInfo scenarioField = runner.GetType().GetField(
                "scenario",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (scenarioField == null)
            {
                return false;
            }

            object scenario = scenarioField.GetValue(runner);
            if (scenario == null)
            {
                return false;
            }

            FieldInfo stepsField = scenario.GetType().GetField(
                "steps",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (stepsField == null)
            {
                return false;
            }

            if (!(stepsField.GetValue(scenario) is IList steps) || stepIndex >= steps.Count)
            {
                return false;
            }

            object step = steps[stepIndex];
            if (step == null)
            {
                return false;
            }

            stepType = step.GetType().Name;
            FieldInfo guidField = step.GetType().GetField(
                "guid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (guidField != null)
            {
                object guidValue = guidField.GetValue(step);
                if (guidValue is string guid)
                {
                    stepGuid = FirstNonEmpty(guid);
                }
            }

            return true;
        }

        private static int ResolveDurationSeconds(string startedAtIso, string completedAtIso, float? overrideSeconds)
        {
            if (overrideSeconds.HasValue)
            {
                return Mathf.Max(0, Mathf.RoundToInt(overrideSeconds.Value));
            }

            if (!string.IsNullOrWhiteSpace(startedAtIso) &&
                !string.IsNullOrWhiteSpace(completedAtIso) &&
                DateTime.TryParse(startedAtIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime startedAt) &&
                DateTime.TryParse(completedAtIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime completedAt))
            {
                double seconds = (completedAt - startedAt).TotalSeconds;
                return Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            }

            return 0;
        }

        private static string NormalizeCompletionStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "abandoned";
            }

            string normalized = value.Trim().ToLowerInvariant();
            return normalized == "completed" || normalized == "failed" || normalized == "abandoned" || normalized == "in_progress"
                ? normalized
                : "abandoned";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }
    }
}





