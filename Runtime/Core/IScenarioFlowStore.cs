using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The flow-store seam BENEATH the runner (map sec-7 / sec-10) - distinct from the control seam
    /// <see cref="ISceneRunnerControl"/>. An append-only path of ENTERED step guids the runner
    /// WRITES (drive) and READS (follow): the driver appends each entered guid; followers track the
    /// frontier (<see cref="Last"/>) and jump to it, never re-deciding - so a branch records itself
    /// and a stale local stat can't diverge peers (map sec-10.1). Flow-sync rides THIS seam, not the
    /// LabEventBus.
    ///
    /// Two impls: <c>LocalScenarioPath</c> (single-player, always compiled, single-driver
    /// passthrough) + <c>FusionScenarioPath</c> (<c>#if PITECH_HAS_FUSION</c>, in Pitech.XR.Networking).
    /// INTERNAL at launch (DevKit-only via [InternalsVisibleTo]) so it stays off the Proof-B public
    /// surface and can be reshaped freely; it GRADUATES to a frozen <c>public</c> API post-launch
    /// (Phase E) for custom transports / replay. INERT in Phase B.1 - the runner gains its write/read
    /// in WS B1.7 / Phase B.2.
    /// </summary>
    internal interface IScenarioFlowStore
    {
        /// <summary>True when this peer drives (single-player: always). Followers are false.</summary>
        bool IsDriver { get; }

        /// <summary>Number of entered guids appended so far.</summary>
        int Count { get; }

        /// <summary>The frontier - the most recently entered guid, or "" when the path is empty.</summary>
        string Last { get; }

        /// <summary>Read the entered guid at a path position.</summary>
        string GetEntered(int index);

        /// <summary>Append an entered step guid (drive). Raises <see cref="Changed"/>.</summary>
        void AppendEntered(string stepGuid);

        /// <summary>Raised whenever the path grows (followers react to advance the frontier).</summary>
        event Action Changed;
    }
}
