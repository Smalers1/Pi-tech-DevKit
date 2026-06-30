using System.Diagnostics;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Analytics
{
    // ---------- Authored analytics signals (map sec-11.4) ----------
    // WS B2.2 Step 4. The UnityEvent-callable escape hatch for AUTHORED failures that aren't a generic
    // grab/drop/use - e.g. the scalpel's wrong-cut UnityEvent -> EmitSignal("wrong-incision"). The
    // signal id routes to the metric whose id matches (LabAnalytics / the engine). Reuses the existing
    // UnityEvent layer, so authors wire it in the inspector with no per-object analytics code.
    //
    // INERT without a LabAnalytics in the scene (the bus has no subscriber, Publish is a no-op).

    [AddComponentMenu("Pi tech/Analytics/Analytics Signal Emitter")]
    public sealed class AnalyticsSignalEmitter : MonoBehaviour
    {
        [Tooltip("Default signal id emitted by Emit() (the parameterless UnityEvent target). Matches a metric id.")]
        public string defaultSignalId = string.Empty;

        ILabEventBus _bus;
        bool _busResolved;

        void OnEnable() => ResolveBus();

        /// <summary>Emit the <see cref="defaultSignalId"/> (parameterless - the simplest UnityEvent hook).</summary>
        public void Emit() => EmitSignal(defaultSignalId);

        /// <summary>Emit a specific signal id (UnityEvent-callable with a string argument).</summary>
        public void EmitSignal(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return;
            ResolveBus();
            if (_bus == null) return;
            _bus.Publish(new LabEvent(ScenarioFactKeys.AnalyticsSignal, null, null,
                tick: Stopwatch.GetTimestamp(), number: LabEvent.NoNumber, text: signalId));
        }

        void ResolveBus()
        {
            if (_busResolved && _bus != null) return;
            LabRuntimeContext ctx = LabRuntimeContext.Find(this);
            _bus = ctx != null ? ctx.Bus : null;
            _busResolved = ctx != null;
        }
    }
}
