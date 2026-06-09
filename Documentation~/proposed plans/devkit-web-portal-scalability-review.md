# DevKit + Web Portal Scalability Review

## Short Answer

If the suggestions are implemented carefully, the whole system should cooperate **better than now**.

The important condition is that the changes must be done gradually:

1. Keep current DevKit and Web Portal behavior working.
2. Add compatibility tests first.
3. Add shared contracts.
4. Standardize telemetry.
5. Then refactor internals slowly.

Do **not** replace the working paths all at once.

---

## My Current Understanding

The system appears to work like this:

1. **DevKit** is the Unity package used to build XR/lab experiences.
2. DevKit contains scenario steps, quiz, stats, selections, content delivery, Addressables/CCD publishing, runtime telemetry, and Unity-side lab logic.
3. **Web Portal** handles users, tenants, lab versions, launch metadata, publishing ingest, telemetry ingest, VICKY/AI features, field sessions, analytics, and admin/professor/student workflows.
4. DevKit publishes or describes lab builds.
5. Web Portal stores lab versions and resolves which version should launch.
6. Unity runtime loads and plays the lab.
7. Unity sends telemetry/attempt/step data back.
8. Web Portal stores and uses that data for analytics, progress, audit, and possibly VICKY context.

So the main connection is:

```text
DevKit / Unity Lab
  -> publish metadata / build info
  -> Web Portal
  -> launch metadata
  -> Unity runtime
  -> telemetry / attempts / step events
  -> Web Portal analytics
```

---

## Important Honesty Note

I do **not** have complete knowledge of every part of the system.

I have a good architectural understanding from:

- Reading the P1 Foundation plan.
- Checking DevKit package metadata.
- Checking key DevKit runtime files like `Scenario`, `SceneManager`, ContentDelivery analytics, and Stats.
- Checking Web Portal structure and integration direction.
- Reviewing both codebases at a high level.

But I have **not** fully reviewed:

- every Unity scene and prefab
- every HealthOn AR/VR consumer integration
- every Supabase function
- every telemetry database table
- every Web Portal UI flow
- every production deployment workflow
- every manual process used by the team

So my recommendations are strong architectural recommendations, but before implementation I would still review the exact publish, launch, telemetry, and WebGL flows end-to-end.

---

# 1. Create A Shared Contract Layer

## The Problem

DevKit and Web Portal both deal with the same types of data:

- launch context
- lab version metadata
- telemetry events
- step events
- lab attempts
- publish transactions
- lifecycle events
- WebGL bridge messages

If these shapes are defined separately, they can drift.

For example, Unity may send:

```json
{
  "attempt_id": "abc",
  "lab_id": "lab-1",
  "event_type": "step_completed"
}
```

But Web Portal may expect:

```json
{
  "attemptId": "abc",
  "labId": "lab-1",
  "type": "step_completed"
}
```

That kind of mismatch can break analytics, launches, or AI features.

## Suggestion

Define versioned contracts such as:

- `LaunchContextV1`
- `TelemetryBatchV1`
- `StepEventV1`
- `LabAttemptV1`
- `PublishTransactionV1`
- `UnityLifecycleEventV1`
- `WebGLBridgeMessageV1`

Each contract should clearly define:

- required fields
- optional fields
- allowed event names
- allowed platform names
- version number
- backward compatibility rules

## Expected Result

This should make DevKit and Web Portal cooperate **better** because both sides agree on the same data language.

---

# 2. Add Compatibility Gates Before Big Refactors

## The Problem

DevKit has important behavior inside large existing classes like `SceneManager`.

That class currently handles many things:

- scenario flow
- step execution
- quiz steps
- selection steps
- stats
- timelines
- groups
- conditions
- UI interactions

If this is changed without tests, old labs may break.

## Suggestion

Before major refactoring, add gates that prove old behavior still works:

- old v0.10 scenes still load
- old prefabs still deserialize
- old public API still exists
- old scenario steps still work
- Unity 6 can compile and open the project
- telemetry payloads are still accepted
- ContentDelivery tests still pass

## Expected Result

This should make the system safer.

You can modernize the architecture while proving that old HealthOn AR/VR projects still work.

---

# 3. Make Telemetry A First-Class Service

## The Problem

Telemetry already exists in DevKit, mostly inside ContentDelivery analytics.

It tracks things like:

- attempt started
- attempt completed
- attempt abandoned
- step completed
- hint used
- reset used
- interaction
- download progress
- critical error

That is useful, but as the system grows, telemetry should not belong only to ContentDelivery.

## Suggestion

Create a standard telemetry service:

```text
ITelemetryService
```

Then any system can send events through it:

- Scenario system
- Quiz system
- Selection system
- ContentDelivery
- Multiplayer
- Localization
- VICKY observer
- Simulator host
- WebGL player

## Expected Result

Analytics becomes more consistent across:

- AR
- VR
- WebGL
- simulator
- future multiplayer sessions

This should make the system cooperate **better**, especially with Web Portal analytics.

## Important Warning

During migration, avoid duplicate telemetry.

For example, do not let both old `RuntimeTelemetryAdapter` and new `ITelemetryService` emit the same `step_completed` event unless there is a clear deduplication rule.

---

# 4. Avoid Duplicate Systems During The Transition

## The Problem

P1 creates new systems next to old systems.

Examples:

- old `XRServices`
- new `CapabilityRegistry`
- old telemetry adapter
- new `ITelemetryService`
- old `SceneManager`
- future step runners
- old direct ContentDelivery flow
- future capability-based content delivery

This is normal, but it can become confusing.

## Suggestion

Define clear ownership per phase.

Example:

```text
P1:
Old behavior still runs.
New architecture exists as scaffold.

P2:
Step runner extraction starts.
Old API remains compatible.

P3:
Multiplayer/lab console pieces plug into the new foundation.

P5:
Agent/VICKY observer and bridge systems plug into the new foundation.
```

## Expected Result

This keeps the current system working while improving the foundation.

If done carefully, cooperation becomes **better**.

If done carelessly, the system can become more complex than now.

---

# 5. Harden The WebGL / Web Portal Bridge

## The Problem

For a future Web Lab Player, Web Portal and Unity WebGL need to communicate through browser messages.

If that bridge is informal, it can become fragile or insecure.

Examples of risky patterns:

- unclear message shapes
- `targetOrigin: "*"`
- passing broad auth tokens into iframe
- no validation of incoming messages
- no test that launch and telemetry work end-to-end

## Suggestion

Create typed bridge messages:

- `unity_ready`
- `launch_context`
- `auth_context`
- `telemetry_batch`
- `lifecycle_event`
- `progress_event`
- `error_event`

Each message should include:

```json
{
  "type": "telemetry_batch",
  "version": "1.0",
  "requestId": "abc",
  "payload": {}
}
```

Also add:

- allowed origin checking
- short-lived launch tokens
- message validation
- automated WebGL launch test

## Expected Result

This makes future WebGL integration much safer and more scalable.

---

# 6. Move Analytics Queries Server-Side

## The Problem

Frontend-side filtering works for small data.

But if the platform grows to many:

- tenants
- professors
- students
- labs
- attempts
- telemetry events

then fetching broad data and filtering in the browser becomes inefficient.

## Suggestion

Use server/database-side analytics:

- tenant-scoped SQL views
- Supabase RPC functions
- indexed analytics tables
- precomputed summaries
- filtered queries by `tenant_id`, `lab_id`, `attempt_id`, `created_at`

## Expected Result

Web Portal analytics becomes faster and more scalable.

This does not change how Unity labs run. It improves how Web Portal reads and displays data.

---

# 7. Gradually Split Big Runtime And UI Files

## The Problem

Large files are hard to maintain.

In DevKit, `SceneManager` appears to be a major central class.

In Web Portal, large hooks/components around VICKY, lab player, admin pages, or analytics can become hard to evolve.

## Suggestion

Split gradually, only after tests exist.

DevKit example:

```text
SceneManager
  -> QuizStepRunner
  -> CueCardsStepRunner
  -> SelectionStepRunner
  -> TimelineStepRunner
  -> GroupStepRunner
  -> ConditionsStepRunner
```

Web Portal example:

```text
Lab3DViewer
  -> launch resolver
  -> iframe bridge
  -> telemetry bridge
  -> auth/permission handling
  -> UI state
```

## Expected Result

The code becomes easier to maintain, test, and extend.

This should improve long-term scalability, but only if done gradually.

---

# Is The System Efficient Right Now?

## Current Answer

For the current size and current HealthOn AR/VR use case:

```text
Mostly yes, it seems usable and production-capable.
```

For future scale:

```text
Not fully.
```

## Main Efficiency Concerns

- DevKit `SceneManager` has too many responsibilities.
- Some DevKit integration uses reflection and scene scanning.
- Scenario behavior has limited automated test coverage.
- Telemetry exists, but is not yet unified across all systems.
- Web Portal has strong backend/audit structure, but WebGL bridge and lab-player testing need hardening.
- Analytics may need more server-side aggregation as data grows.
- Large files in both systems may slow future development.

---

# Will The System Cooperate Same Or Better After These Suggestions?

## If Done Correctly

It should cooperate **better**.

Expected improvements:

- DevKit and Web Portal use the same contracts.
- Analytics becomes more reliable.
- Old labs stay compatible.
- Unity 6 migration becomes safer.
- WebGL/future player integration becomes safer.
- Multiplayer/localization/AI features get proper extension points.
- CI catches breaking changes earlier.
- Web Portal scales better with more users and more events.

## If Done Incorrectly

It could become worse.

Possible problems:

- duplicate telemetry events
- broken old scenes
- changed public API
- mismatched contract versions
- too many abstraction layers without real usage
- Web Portal expecting new payloads while old DevKit clients still send old ones

## Best Strategy

```text
Stabilize first.
Add contracts.
Add tests.
Standardize telemetry.
Then refactor behavior gradually.
```

---

# Final Opinion

The P1 Foundation direction is good.

It is not mainly a feature plan. It is a platform-strengthening plan.

It prepares the DevKit and Web Portal ecosystem for:

- Unity 6
- safer upgrades
- better analytics
- future WebGL lab player
- future multiplayer
- future localization
- future VICKY/agent observation
- better testing and CI
- cleaner architecture

My recommendation is to prioritize:

1. compatibility gates
2. shared contracts
3. telemetry standardization
4. WebGL bridge hardening
5. server-side analytics scale
6. gradual refactoring of large files

That would make the whole system more scalable and more reliable without throwing away what already works.
