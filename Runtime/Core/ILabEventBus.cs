using System;

namespace Pitech.XR.Core
{
    // ---------- LabEventBus: the notification plane (map sec-7) ----------
    // The runner raises step/session facts onto the bus; consumers (analytics / UI / VICKY-observe)
    // subscribe. Replaces today's reflection-poll telemetry. Facts out only, fire-and-forget,
    // NON-BLOCKING, SYNC IN-PROCESS. Per-lab and local to ONE machine - it never crosses the network
    // (each peer has its own bus; cross-peer transport is Fusion, map sec-10; cloud is a subscriber's
    // job). Lives in Pitech.XR.Core (the leaf) so producer (Scenario) and consumers reference Core,
    // never each other (acyclic) - the decoupling point that a direct C# event cannot give.
    //
    // The bus fact SHAPE (LabEvent) is part of the DevKit SDK emit surface frozen 2026-07-07 (map
    // sec-13) - additive-only after that. INERT in Phase B.1: nothing publishes yet (the runner gains
    // emission in WS B1.7 / Phase B.2).

    /// <summary>
    /// One immutable fact on the <see cref="ILabEventBus"/>. The <see cref="Key"/> (built from
    /// <c>ScenarioFactKeys</c>) names what happened; the value snapshot (<see cref="Number"/> /
    /// <see cref="Text"/>) and <see cref="Tick"/> carry the payload; <see cref="AttemptId"/> +
    /// <see cref="LabInstanceId"/> scope it (every fact carries both, map sec-7). Passed by
    /// <c>in</c> reference so publishing allocates nothing.
    /// </summary>
    public readonly struct LabEvent
    {
        /// <summary>Sentinel for "no numeric snapshot" (see <see cref="HasNumber"/>).</summary>
        public const double NoNumber = double.NaN;

        /// <summary>The fact key (from <c>ScenarioFactKeys</c>) - what happened.</summary>
        public readonly string Key;

        /// <summary>The attempt this fact belongs to (map sec-7).</summary>
        public readonly string AttemptId;

        /// <summary>The lab instance this fact belongs to (map sec-7).</summary>
        public readonly string LabInstanceId;

        /// <summary>Monotonic host-stamped tick at emit (for duration deltas). 0 if unstamped.</summary>
        public readonly long Tick;

        /// <summary>Numeric value snapshot, or <see cref="NoNumber"/> (NaN) when there is none.</summary>
        public readonly double Number;

        /// <summary>Text value snapshot (e.g. a related guid / outcome), or null when there is none.</summary>
        public readonly string Text;

        public LabEvent(string key, string attemptId, string labInstanceId, long tick = 0,
            double number = double.NaN, string text = null)
        {
            Key = key;
            AttemptId = attemptId;
            LabInstanceId = labInstanceId;
            Tick = tick;
            Number = number;
            Text = text;
        }

        /// <summary>True when <see cref="Number"/> carries a real numeric snapshot (not the NaN sentinel).</summary>
        public bool HasNumber => !double.IsNaN(Number);
    }

    /// <summary>Receives a <see cref="LabEvent"/> by <c>in</c> reference (zero-copy, no boxing).</summary>
    public delegate void LabFactHandler(in LabEvent fact);

    /// <summary>
    /// The notification plane of a running lab (map sec-7). Facts out only - fire-and-forget,
    /// synchronous, in-process. Each subscriber is invoked in isolation: one throwing subscriber must
    /// never break the runner or the others. Instances are LAB-SCOPED (one per attempt), never a
    /// global singleton - resolve via <see cref="LabRuntimeContext"/> by parent-walk, never the
    /// global <c>XRServices</c> map (the multi-runner mis-bind risk, map sec-5 / sec-7).
    /// </summary>
    public interface ILabEventBus
    {
        /// <summary>Raise a fact to all subscribers, synchronously and in order. Never throws to the
        /// caller (each subscriber is wrapped). Allocates nothing.</summary>
        void Publish(in LabEvent fact);

        /// <summary>Subscribe to every fact. Dispose the returned handle to unsubscribe (subscribe in
        /// OnEnable, dispose in OnDisable). Re-entrant subscribe/unsubscribe during a publish is safe
        /// but only takes effect on the next publish.</summary>
        IDisposable Subscribe(LabFactHandler handler);
    }
}
