using System.Diagnostics;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Analytics
{
    // ---------- The tracked-subject runtime agent (map sec-11.2 / sec-11.4) ----------
    // WS B2.2. Placed on a subject's scene object (the editor auto-wirer adds it + fills subjectId from
    // the config). Emits the RAW item/interaction facts; the recorder (LabAnalytics) classifies them.
    // No hard Meta-Interaction dependency: the grab/drop/use entry points are PUBLIC and UnityEvent-
    // callable, so the auto-wirer (or the author) hooks them to whatever interaction layer is present
    // (Meta Select, the RespawnOnDrop sample, a button, etc.). An OPTIONAL below-Y check gives drop
    // detection with zero dependencies (the DevKit's own check, per the map).
    //
    // INERT without a LabAnalytics in the scene: the bus has no subscriber, so Publish is a no-op.

    [AddComponentMenu("Pi tech/Analytics/Analytics Subject")]
    [DisallowMultipleComponent]
    public sealed class AnalyticsSubject : MonoBehaviour
    {
        [Tooltip("The config subject id this object IS (matches a TrackedSubject.id). The editor auto-wirer fills this.")]
        public string subjectId = string.Empty;

        [Header("Drop detection (optional, dependency-free)")]
        [Tooltip("If true, emits a 'dropped' fact when this object falls below the Y plane below - no interaction SDK needed.")]
        public bool autoDetectDropBelowY;

        [Tooltip("World Y below which the object is considered dropped (when auto-detect is on).")]
        public float dropY = -1f;

        ILabEventBus _bus;
        bool _busResolved;
        bool _belowReported;

        void OnEnable()
        {
            ResolveBus();
        }

        void Update()
        {
            if (!autoDetectDropBelowY) return;
            float y = transform.position.y;
            if (!_belowReported && y < dropY)
            {
                _belowReported = true;
                ReportDropped();
            }
            else if (_belowReported && y >= dropY)
            {
                _belowReported = false;   // re-armed after it comes back up (e.g. respawn)
            }
        }

        /// <summary>Emit a 'grabbed' fact (informational). UnityEvent-callable - hook to your grab event.</summary>
        public void ReportGrabbed() => Emit(ScenarioFactKeys.ItemGrabbed);

        /// <summary>Emit a 'dropped' fact (-&gt; DropMetric). UnityEvent-callable - hook to your drop event.</summary>
        public void ReportDropped() => Emit(ScenarioFactKeys.ItemDropped);

        /// <summary>Emit a 'used' fact (the recorder classifies correct / wrong / order). UnityEvent-callable
        /// - hook to your "use/activate" event (grab-into-target, button, tool fire, ...).</summary>
        public void ReportUsed() => Emit(ScenarioFactKeys.InteractionUsed);

        void Emit(string key)
        {
            ResolveBus();
            if (_bus == null) return;
            _bus.Publish(new LabEvent(key, null, null, tick: Stopwatch.GetTimestamp(),
                number: LabEvent.NoNumber, text: subjectId));
        }

        void ResolveBus()
        {
            if (_busResolved && _bus != null) return;
            LabRuntimeContext ctx = LabRuntimeContext.Find(this);
            _bus = ctx != null ? ctx.Bus : null;
            _busResolved = ctx != null;   // retry until a context is resolvable
        }
    }
}
