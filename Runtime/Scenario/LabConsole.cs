using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;
using Pitech.XR.Stats;
using Pitech.XR.Interactables;
using Pitech.XR.Quiz;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace Pitech.XR.Scenario
{
    [AddComponentMenu("Pi tech/Scenario/Lab Console")]
    public class LabConsole : MonoBehaviour, Pitech.XR.Core.ISceneRunnerControl, Pitech.XR.Core.ILabStateStore
    {
        /// <summary>The scenario graph this manager runs (the ordered <c>[SerializeReference]</c> step list). Required for step flow.</summary>
        [Header("Scenario")]
        [Tooltip("Required for step flow. For Addressable lab prefabs, assign on the prefab asset (CCD bundle), not only in a scene instance.")]
        public Scenario scenario;
        /// <summary>When true, <see cref="Restart"/> runs automatically from <c>Start()</c>. ContentDeliverySpawner clears this until the lab prefab has spawned.</summary>
        [Tooltip("If true, Restart() runs from Start(). When using ContentDeliverySpawner with defer Lab Console, autoStart is turned off until after spawn.")]
        public bool autoStart = true;

        /// <summary>Optional UI controller that displays the live stat values.</summary>
        public StatsUIController statsUI;
        /// <summary>Optional stat definitions (keys, ranges, defaults) for this lab.</summary>
        public StatsConfig statsConfig;
        internal bool _statsBound;   // internal so the extracted ScenarioRunner (same assembly) can read/set it

        /// <summary>Optional shared stats store. RETAINED as a display mirror for <see cref="statsUI"/>
        /// (the param store is the source of truth - see <see cref="Params"/>). If null, a plain instance
        /// is created on demand.</summary>
        [Header("Stats (optional)")]
        public StatsRuntime runtime;   // assign if you have one. if null we create a plain instance

        /// <summary>WS B1.2 Step 4 (map sec-8): typed parameter declarations for this lab - the successor
        /// to <see cref="statsConfig"/>. Seeds the runtime param store (<see cref="Params"/>); the
        /// multiplayer validators read these. Empty on legacy labs (the store falls back to
        /// <see cref="statsConfig"/>). Migrate a StatsConfig with the editor upgrader. Serialized
        /// (SerializedObject-visible to tooling) but not public, so the public-API surface is unchanged.</summary>
        [SerializeField] List<Pitech.XR.Core.ConsoleParameter> parameters = new List<Pitech.XR.Core.ConsoleParameter>();

        /// <summary>Default quiz asset used when a step does not specify its own.</summary>
        [Header("Quiz (optional)")]
        [FormerlySerializedAs("quiz")]
        public QuizAsset defaultQuiz;

        /// <summary>UI controller that presents quiz questions.</summary>
        [FormerlySerializedAs("quizUI")]
        public QuizUIController quizPanel;

        /// <summary>UI controller that presents the quiz results screen.</summary>
        [FormerlySerializedAs("quizResultsUI")]
        public QuizResultsUIController quizResultsPanel;
        /// <summary>The active quiz session (scoring/state). Created lazily via <see cref="GetOrCreateQuizSession"/>.</summary>
        public QuizSession quizSession;

        /// <summary>Catalog of selectable colliders/targets in the scene.</summary>
        [Header("Interactables (optional)")]
        public SelectablesManager selectables;     // the catalog of clickable colliders
        /// <summary>The selection quiz/controller that drives <see cref="selectables"/>.</summary>
        public SelectionLists selectionLists;      // the quiz/controller using that catalog

        /// <summary>Optional reference to the ContentDeliverySpawner (or compatible component) that spawned this lab.</summary>
        [Header("Content Delivery (optional)")]
        [Tooltip("Optional reference to ContentDeliverySpawner (or compatible component).")]
        public MonoBehaviour contentDelivery;

        /// <summary>Optional root for auto-finding Quiz UI controllers. Falls back to <c>transform.root</c> so discovery stays within this lab prefab instance (never binds shell UI).</summary>
        [Header("Prefab / Addressables lab")]
        [Tooltip(
            "Optional root for auto-finding Quiz UI controllers. If null, uses transform.root so discovery stays within this lab prefab instance (never binds shell UI).")]
        public Transform labContentRoot;

        /// <summary>The extracted run-engine this console owns and directly drives (WS B1.7, decision 34).</summary>
        ScenarioRunner _runner;

        /// <summary>WS B1.2 Step 4 (map sec-8): the per-client local parameter store. The runtime SOURCE OF
        /// TRUTH on single-player labs, and the Local-scope half on networked labs. Seeded from
        /// <see cref="parameters"/> (and, for back-compat, any legacy <see cref="statsConfig"/> entry not
        /// already declared) in <see cref="Awake"/>; the legacy <see cref="runtime"/> is kept only as a
        /// display mirror via the ParamChanged bridge.</summary>
        Pitech.XR.Core.LocalParamStore _params;
        /// <summary>WS B2.4 Step 4: the param store the runner actually reads/writes. EQUALS
        /// <see cref="_params"/> on single-player / no-Fusion labs (byte-identical to B.1). On a lab that
        /// carries a networked param store component it is a <see cref="RoutedParamStore"/> that sends
        /// Networked-scope ids to the replicated store and Local-scope ids to <see cref="_params"/>.</summary>
        Pitech.XR.Core.IParamStore _paramStore;
        /// <summary>The scope router - non-null only when a networked param store was resolved; kept so
        /// <see cref="OnDestroy"/> can detach its forwarders.</summary>
        RoutedParamStore _routed;
        /// <summary>Read-only view of the active runtime param store (<see cref="_paramStore"/>). Internal:
        /// read by the same-assembly runner; the public-API surface is unchanged.</summary>
        internal Pitech.XR.Core.IParamStore Params => _paramStore;
        /// <summary>True when this lab has any stats/param feature wired (UI, legacy config, or declared
        /// parameters). The runner gates effect application on this - preserving the legacy "no stats
        /// feature -> skip" behaviour (additive for the new declared-parameters case).</summary>
        internal bool HasStatsFeature => statsUI != null || statsConfig != null || (parameters != null && parameters.Count > 0);

        /// <summary>Current step index while running; <c>-1</c> when idle or finished. Forwards the run-engine's index.</summary>
        public int StepIndex => _runner != null ? _runner.StepIndex : -1;

        // --- ISceneRunnerControl (WS A8): behaviour-neutral typed seam. Forwards to existing members;
        // no field renamed, nothing made non-public, behaviour identical. Restart() (below) already
        // satisfies the interface's Restart(). ---
        /// <summary>Forwards <see cref="StepIndex"/> for the <see cref="Pitech.XR.Core.ISceneRunnerControl"/> seam.</summary>
        public int CurrentStepIndex => StepIndex;
        /// <summary>Forwards <see cref="autoStart"/> for the <see cref="Pitech.XR.Core.ISceneRunnerControl"/> seam.</summary>
        public bool AutoStart { get => autoStart; set => autoStart = value; }

        // --- ILabStateStore (P5 / review H4): LabConsole IS the lab's one bool-view state store, so a lab
        // needs NO separate LocalLabStateStore component. Triggers/listeners resolve the store via
        // GetComponentInParent<ILabStateStore>() and walk up to this root. The bool-view is sugar over the
        // SAME _paramStore the runner reads conditions/effects from, so a trigger's SetState("WaterFlowing",
        // true) is immediately visible to a ConditionsStep. StateChanged forwards _paramStore.ParamChanged
        // (the store fans out change-only), covering both local writes and replicated changes from a
        // networked backing store - one uniform path. ---
        /// <summary>Raised with the state id whenever a named boolean (in fact any parameter) changes;
        /// forwards the param store's <c>ParamChanged</c>. Subscribe instead of polling.</summary>
        public event Action<string> StateChanged;
        /// <summary>Current value of a named boolean state (false if unset, or before <see cref="Awake"/>
        /// builds the store).</summary>
        public bool GetState(string id) => _paramStore != null && _paramStore.GetBool(id);
        /// <summary>Set a named boolean state on the lab's shared store. The store fans out change-only, so a
        /// no-op write stays silent. No-op for an empty id or before <see cref="Awake"/> builds the store.</summary>
        public void SetState(string id, bool value)
        {
            if (_paramStore == null || string.IsNullOrEmpty(id)) return;
            _paramStore.SetBool(id, value);
        }
        /// <summary>Flip a named boolean state on the lab's shared store.</summary>
        public void Toggle(string id)
        {
            if (_paramStore == null || string.IsNullOrEmpty(id)) return;
            _paramStore.SetBool(id, !_paramStore.GetBool(id));
        }


        void Awake()
        {
            // WS B1.7: build the run-engine first - DeactivateAllVisuals() (below) and Start() drive it.
            _runner = new ScenarioRunner(this);

            // WS B1.2 Step 4 (map sec-8): build the typed param store (the Stats successor) - the runtime
            // source of truth for parameters/effects/conditions. The local store is always built; B2.4
            // (below) may front it with a scope router when a networked store is present.
            _params = new Pitech.XR.Core.LocalParamStore();

            // WS B2.4 Step 4 (map sec-8): if this lab carries a networked param store component (a
            // NetworkedParamStore - present only on a Fusion lab; it is the only component IParamStore and
            // is itself Fusion-gated), route Networked-scope parameters through it (replicated +
            // authority-sequenced) while Local-scope parameters stay client-local in _params. Resolved
            // within this lab instance (self + children; no FindObjectsOfType); IParamStore lives in Core
            // so this needs no Pitech.XR.Networking asmdef reference and compiles with Fusion absent. On
            // single-player / no-Fusion labs no such component exists, so the active store IS the
            // LocalParamStore and the run is byte-identical to B.1 (one extra GetComponentInChildren at
            // Awake, never in the per-frame trace).
            var networkedParams = GetComponentInChildren<Pitech.XR.Core.IParamStore>(true);
            if (networkedParams != null && !ReferenceEquals(networkedParams, _params))
            {
                _routed = new RoutedParamStore(_params, networkedParams);
                _paramStore = _routed;
            }
            else
            {
                _paramStore = _params;
            }

            // Declare into the active store: the router splits each declaration by scope; the plain local
            // store takes them all. Then (for back-compat) any legacy StatsConfig entry not already
            // declared, so an un-upgraded lab keeps working AND gains range clamp (min/max ENFORCED).
            // Seeded ONCE here - Restart() never re-seeds, matching the legacy StatsRuntime.
            if (parameters != null)
                foreach (var p in parameters)
                    _paramStore.Declare(p);
            if (statsConfig != null)
                foreach (var kv in statsConfig.All())
                {
                    if (_paramStore.IsDeclared(kv.Key)) continue;
                    _paramStore.Declare(new Pitech.XR.Core.ConsoleParameter
                    {
                        id = kv.Key,
                        type = Pitech.XR.Core.ParamType.Float,
                        defaultNumber = kv.Value.defaultValue,
                        min = kv.Value.min,
                        max = kv.Value.max,
                        scope = Pitech.XR.Core.ParamScope.Local
                    });
                }

            // Only set up stats if the feature is present (UI or config).
            // Only if feature present
            if (statsUI != null || statsConfig != null)
            {
                if (runtime == null) runtime = new StatsRuntime();
                if (statsConfig != null) runtime.Reset(statsConfig);      // seed defaults

                if (statsUI != null)
                {
                    if (statsConfig != null) statsUI.ApplyConfig(statsConfig, alsoSetDefaultsToUI: true); // ranges + default paint
                    statsUI.Init(runtime, syncNow: true);                                                  // subscribe + ensure paint
                    _statsBound = true;
                }
            }

            // WS B1.2 Step 4: bridge the param store (source of truth) into the legacy StatsRuntime so the
            // stats UI (which subscribes to StatsRuntime.OnChanged) keeps animating. One-way (store ->
            // runtime); no cycle, since runtime writes never feed back into the store. Subscribing to
            // _paramStore (the router, when routed) so Networked-scope changes also reach the UI.
            _paramStore.ParamChanged += OnParamChangedMirror;

            if (selectionLists != null)
            {
                if (selectionLists.selectables == null && selectables != null)
                    selectionLists.selectables = selectables;
            }
            if (selectables != null)
                selectables.pickingEnabled = false;

            _runner.DeactivateAllVisuals();

            Transform quizDiscoveryRoot = labContentRoot != null ? labContentRoot : transform.root;
            if (quizPanel == null)
            {
                quizPanel = quizDiscoveryRoot.GetComponentInChildren<QuizUIController>(true);
            }

            if (quizResultsPanel == null)
            {
                quizResultsPanel = quizDiscoveryRoot.GetComponentInChildren<QuizResultsUIController>(true);
            }

            // Hide (without disabling, if CanvasGroup is present)
            if (quizPanel != null) quizPanel.Hide();
            if (quizResultsPanel != null) quizResultsPanel.Hide();
        }

        // ------ Convenience bridges (so Timeline/UI can talk only to LabConsole) ------
        /// <summary>Activate the selection list at <paramref name="index"/> (no-op if no <see cref="selectionLists"/>).</summary>
        public void ActivateSelectionList(int index) => selectionLists?.ActivateList(index);
        /// <summary>Activate the selection list named <paramref name="listName"/> (no-op if no <see cref="selectionLists"/>).</summary>
        public void ActivateSelectionListByName(string listName) => selectionLists?.ActivateListByName(listName);
        /// <summary>Mark the active selection list complete (no-op if no <see cref="selectionLists"/>).</summary>
        public void CompleteSelection() => selectionLists?.CompleteActive();
        /// <summary>Reset/retry the active selection list (no-op if no <see cref="selectionLists"/>).</summary>
        public void RetrySelection() => selectionLists?.RetryActive();

        void Start()
        {
            // WS B2.4: bind the scenario flow store if a networked lab provides one. A FusionScenarioPath
            // (a NetworkBehaviour on a networked lab's root, resolved here AS the internal
            // IScenarioFlowStore) makes the runner multiplayer-aware; single-player labs have none, so
            // the runner stays bound to nothing and runs byte-identically to B.1 (the strongest trace-
            // safe guarantee). Resolved within this lab instance (no FindObjectsOfType).
            var flow = GetComponentInChildren<Pitech.XR.Core.IScenarioFlowStore>(true);
            if (flow != null) _runner.BindFlowStore(flow);

            // Back any SEPARATE bool-view stores with the SAME param store the runner reads conditions/effects
            // from, so writers (triggers) and readers (ConditionsStep/effects) share ONE source of truth.
            // P5 (review H4): LabConsole ITSELF now implements ILabStateStore over its own _paramStore, so a
            // separate component is no longer required - triggers resolve THIS root via
            // GetComponentInParent<ILabStateStore>(). LabConsole is deliberately NOT in the list below (it does
            // not implement IParamStoreBackedState - it owns _paramStore directly and is already backed). Any
            // extra param-store-backed views still present (e.g. a sub-tree's own LocalLabStateStore) are wired
            // to the shared store here so they cannot diverge. A store that owns its own replicated state
            // (NetworkedLabStateStore) does not implement the seam and is left untouched.
            foreach (var backed in GetComponentsInChildren<Pitech.XR.Core.IParamStoreBackedState>(true))
                backed.Initialize(Params);

            if (autoStart) Restart();
        }

        /// <summary>Restart the scenario from the first step. Forwards to the extracted run-engine.</summary>
        public void Restart() => _runner.Restart();

        /// <summary>Editor-only play-mode hook (the Scenario Graph's Branch/Skip/Outcome buttons). Forwards to the run-engine; the runner no-ops outside play mode. This is the deterministic driver the golden-trace recorder wraps.</summary>
        public void EditorSkipFromGraph(string stepGuid, int branchIndex)
        {
            if (_runner != null) _runner.EditorSkipFromGraph(stepGuid, branchIndex);
        }

        /// <summary>Return the active <see cref="quizSession"/>, creating one bound to <paramref name="asset"/> if needed. A null asset returns the existing session unchanged.</summary>
        public QuizSession GetOrCreateQuizSession(QuizAsset asset)
        {
            if (asset == null) return quizSession;

            if (quizSession == null)
                quizSession = new QuizSession(asset);
            else
                quizSession.SetAsset(asset);

            return quizSession;
        }

        // WS B1.2 Step 4: mirror a param-store change into the legacy StatsRuntime so the stats UI updates.
        // Reads through _paramStore so the value is correct whether the id is Local- or Networked-scope.
        void OnParamChangedMirror(string id)
        {
            if (runtime != null) runtime[id] = _paramStore.GetFloat(id);
            // P5 (review H4): LabConsole is the lab's ILabStateStore bool-view - forward the store's change to
            // state listeners (EventStateListener / TimelineStateListener etc.), which filter by id their side.
            StateChanged?.Invoke(id);
        }

        void OnDestroy()
        {
            if (_paramStore != null) _paramStore.ParamChanged -= OnParamChangedMirror;
            if (_routed != null) _routed.Dispose();   // detach the router's forwarders from both stores
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (scenario != null)
            {
                return;
            }

            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null)
            {
                return;
            }

            Debug.LogWarning(
                "[Scenario] LabConsole has no Scenario assigned. For Addressable lab prefabs, assign a Scenario on the prefab and wire Selectables / Selection Lists / Quiz as needed.",
                this);
        }
#endif
    }
}
