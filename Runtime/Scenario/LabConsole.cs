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
    public class LabConsole : MonoBehaviour, Pitech.XR.Core.ISceneRunnerControl
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

        /// <summary>Optional shared stats store. If null, a plain instance is created on demand.</summary>
        [Header("Stats (optional)")]
        public StatsRuntime runtime;   // assign if you have one. if null we create a plain instance

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

        /// <summary>Current step index while running; <c>-1</c> when idle or finished. Forwards the run-engine's index.</summary>
        public int StepIndex => _runner != null ? _runner.StepIndex : -1;

        // --- ISceneRunnerControl (WS A8): behaviour-neutral typed seam. Forwards to existing members;
        // no field renamed, nothing made non-public, behaviour identical. Restart() (below) already
        // satisfies the interface's Restart(). ---
        /// <summary>Forwards <see cref="StepIndex"/> for the <see cref="Pitech.XR.Core.ISceneRunnerControl"/> seam.</summary>
        public int CurrentStepIndex => StepIndex;
        /// <summary>Forwards <see cref="autoStart"/> for the <see cref="Pitech.XR.Core.ISceneRunnerControl"/> seam.</summary>
        public bool AutoStart { get => autoStart; set => autoStart = value; }


        void Awake()
        {
            // WS B1.7: build the run-engine first - DeactivateAllVisuals() (below) and Start() drive it.
            _runner = new ScenarioRunner(this);

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
