#if PITECH_HAS_FUSION || FUSION_WEAVER
using Fusion;
using UnityEngine;
using UnityEngine.Playables;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>Timeline replication mode for <see cref="TimelineStateListener"/>.</summary>
    public enum TimelineSyncMode
    {
        SyncedToMasterClock,
        LocalPlayOnly
    }

    // ---------- TimelineStateListener (DevKit graduation, WS B2.7) ----------
    // The DevKit version of VR's TimelineStateListener. REMAINS a Fusion NetworkBehaviour (the networked
    // timeline-sync needs [Networked] state), gated #if PITECH_HAS_FUSION || FUSION_WEAVER. Graduated to
    // read the required state from ILabStateStore (resolved via GetComponentInParent in Spawned) instead
    // of the static NetworkStateManager.Instance. The networked playback-sync logic is faithful to the
    // VR original (StartTime / PausedAtElapsed / RestartCount + the master-clock sync in Render).

    [AddComponentMenu("Pi tech/Networking/Timeline State Listener")]
    [RequireComponent(typeof(PlayableDirector))]
    internal sealed class TimelineStateListener : NetworkBehaviour
    {
        [Header("Logic")]
        public string requiredStateID = "TourMode";
        public TimelineSyncMode syncMode = TimelineSyncMode.SyncedToMasterClock;
        public float syncThreshold = 0.15f;

        // Runner.SimulationTime when playback (re)started. -1 = never started.
        [Networked] float StartTime { get; set; }
        // Elapsed time captured when paused. -1 = currently playing.
        [Networked] float PausedAtElapsed { get; set; }
        // Incremented on every restart so Render() can detect it as a one-shot event.
        [Networked] int RestartCount { get; set; }

        PlayableDirector _director;
        ILabStateStore _store;
        int _lastRestartCount;

        void Awake()
        {
            _director = GetComponent<PlayableDirector>();
            if (_director != null) _director.playOnAwake = false;
        }

        public override void Spawned()
        {
            _store = GetComponentInParent<ILabStateStore>(true);
            if (Object.HasStateAuthority)
            {
                StartTime = -1f;
                PausedAtElapsed = 0f;
            }
        }

        /// <summary>Restart this timeline for all clients.</summary>
        public void Restart()
        {
            if (Object == null) return;   // guard: may be UnityEvent-invoked before Spawned()
            if (Object.HasStateAuthority) DoRestart();
            else RPC_Restart();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_Restart() => DoRestart();

        void DoRestart()
        {
            bool isPlaying = PausedAtElapsed < 0f;
            StartTime = Runner.SimulationTime;
            PausedAtElapsed = isPlaying ? -1f : 0f;
            RestartCount++;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (_store == null) _store = GetComponentInParent<ILabStateStore>(true);
            if (_store == null) return;

            bool shouldPlay = _store.GetState(requiredStateID);
            bool isCurrentlyPlaying = PausedAtElapsed < 0f;

            if (shouldPlay && !isCurrentlyPlaying)
            {
                if (StartTime < 0f) StartTime = Runner.SimulationTime;                  // first play ever
                else StartTime = Runner.SimulationTime - PausedAtElapsed;               // resume
                PausedAtElapsed = -1f;
            }
            else if (!shouldPlay && isCurrentlyPlaying)
            {
                PausedAtElapsed = StartTime >= 0f ? Runner.SimulationTime - StartTime : 0f;   // pause
            }
        }

        public override void Render()
        {
            if (_director == null) return;

            if (RestartCount != _lastRestartCount)
            {
                _director.time = 0f;
                _director.Evaluate();
                _lastRestartCount = RestartCount;
            }

            bool isPlaying = PausedAtElapsed < 0f;
            bool hasStarted = StartTime >= 0f;

            if (isPlaying && hasStarted)
            {
                if (_director.state != PlayState.Playing) _director.Play();

                if (syncMode == TimelineSyncMode.SyncedToMasterClock)
                {
                    double targetTime = Runner.SimulationTime - StartTime;
                    if (Mathf.Abs((float)(_director.time - targetTime)) > syncThreshold)
                        _director.time = targetTime;
                }
            }
            else
            {
                if (_director.state == PlayState.Playing)
                {
                    if (syncMode == TimelineSyncMode.SyncedToMasterClock && hasStarted && PausedAtElapsed >= 0f)
                        _director.time = PausedAtElapsed;
                    _director.Pause();
                }
            }
        }
    }
}
#endif
