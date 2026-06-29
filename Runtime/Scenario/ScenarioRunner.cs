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
    // WS B1.7 (extraction + rename): the run-engine lifted out of the host (LabConsole, formerly
    // SceneManager) into a dedicated inner runner type (decision 34). LabConsole OWNS and directly
    // drives this runner; the runner reads the host's authoring state through the forwarding members
    // below, so the engine method BODIES are moved verbatim (behaviour-neutral - proven by the
    // dev-playtest, never the gate). The Run*/RunXGroup TWINS are intentionally kept divergent (no
    // dedup). Internal: off the Proof-B public surface; graduates to its own assembly later.
    internal sealed class ScenarioRunner
    {
        readonly LabConsole _console;
        public ScenarioRunner(LabConsole console) { _console = console; }

        // --- forwarding to the host's authoring state (so the moved bodies resolve their bare names) ---
        Scenario scenario => _console.scenario;
        QuizAsset defaultQuiz => _console.defaultQuiz;
        QuizUIController quizPanel => _console.quizPanel;
        QuizResultsUIController quizResultsPanel => _console.quizResultsPanel;
        StatsRuntime runtime { get => _console.runtime; set => _console.runtime = value; }
        StatsUIController statsUI => _console.statsUI;
        StatsConfig statsConfig => _console.statsConfig;
        bool _statsBound { get => _console._statsBound; set => _console._statsBound = value; }
        // WS B1.2 Step 4: the typed param store (the Stats successor) + its feature gate, forwarded from
        // the host. Effects/quiz/conditions read/write this; the legacy StatsRuntime is the UI mirror.
        Pitech.XR.Core.IParamStore Params => _console.Params;
        bool HasStatsFeature => _console.HasStatsFeature;
        SelectionLists selectionLists => _console.selectionLists;
        QuizSession GetOrCreateQuizSession(QuizAsset asset) => _console.GetOrCreateQuizSession(asset);
        Coroutine StartCoroutine(IEnumerator routine) => _console.StartCoroutine(routine);
        void StopCoroutine(Coroutine routine) => _console.StopCoroutine(routine);

        // --- engine state owned by the runner ---
        public int StepIndex { get; private set; } = -1;

        // --- WS B1.7 Increment 3 (decision 34/36): the runner's two outputs (map sec-7) ---
        // (1) facts onto the LabEventBus; (2) the entered-guid path on IScenarioFlowStore. BOTH are
        // behaviour-neutral at launch, but for DIFFERENT reasons now that WS B1.1 Step 2 has landed:
        //   * the bus resolves via LabRuntimeContext.Find(_console). ContentDeliverySpawner now
        //     attaches a LabRuntimeContext to every spawned lab root, so for ContentDelivery-spawned
        //     (Addressable) labs _ctx is NON-null and _ctx.Bus.Publish(...) DOES execute on every
        //     step entered/completed. It is still inert because the bus has ZERO subscribers at
        //     launch, so LabEventBus.Publish early-returns immediately (LabEventBus.cs: count == 0).
        //     For menu/direct labs (NOT ContentDelivery-spawned) _ctx resolves null and EmitStepFact
        //     returns at its null guard before touching the bus. (Resolved ONCE per run then cached,
        //     so it is not a per-step GetComponentInParent.) WS B1.1 Step 3 adds the first subscriber
        //     (telemetry-on-bus) - that flips the emit LIVE for spawned labs (the intended flip; its
        //     runtime equivalence is the deferred SP dev-playtest, never the gate);
        //   * no flow store is injected yet (_flow == null) -> AppendEntered never runs; the IsDriver
        //     guard is the INERT follower-suppression hook (decision 36) - in single-player the runner
        //     always DRIVES, never follows. B.2 turn-on injects a store (BindFlowStore) and adds the
        //     follower frontier-jump WITHOUT editing this engine. The LabEvent payload (step.guid in
        //     Text + a monotonic tick) is refined additively at the 2026-07-07 analytics freeze.
        Pitech.XR.Core.LabRuntimeContext _ctx;   // re-resolved each run in Run() (never cached-null-forever)
        Pitech.XR.Core.IScenarioFlowStore _flow;   // injected by B.2; null at launch (inert)

        /// <summary>B.2 injection seam: bind the flow store without editing the engine. Unbound at launch.</summary>
        internal void BindFlowStore(Pitech.XR.Core.IScenarioFlowStore flow) => _flow = flow;

        void EmitStepFact(string factName, string stepGuid)
        {
            if (_ctx == null) return;   // null only for menu/direct labs (not ContentDelivery-spawned); spawned labs have _ctx but the bus has no subscribers at launch (see field region)
            _ctx.Bus.Publish(new Pitech.XR.Core.LabEvent(
                factName, _ctx.AttemptId, _ctx.LabInstanceId,
                tick: System.Diagnostics.Stopwatch.GetTimestamp(),   // monotonic host tick (for StepDuration deltas)
                number: Pitech.XR.Core.LabEvent.NoNumber, text: stepGuid));
        }

        // Ignition: LabConsole.Restart()/Start() forward here (body identical to the old Restart()).
        public void Restart()
        {
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Run());
        }

        // ===== engine state + methods below are MOVED VERBATIM from the host (only `this` ->
        // `_console` for Debug-context args). Do not edit logic here. =====
        Coroutine _run;
        sealed class StepRunContext
        {
            public bool cancelRequested;
            public System.Action cancel;

            public void Cancel()
            {
                if (cancelRequested) return;
                cancelRequested = true;
                cancel?.Invoke();
            }
        }

        bool _editorSkip;
        int _editorSkipBranchIndex;

        bool _groupExitBranchResolved;
        string _groupExitNextGuid;
        IEnumerator Run()
        {
            if (scenario == null || scenario.steps == null || scenario.steps.Count == 0)
                yield break;

            // WS B1.7 Increment 3: resolve the lab context ONCE per run (cheap; not per-step). NON-null
            // for ContentDelivery-spawned labs (the spawner attaches it, WS B1.1 Step 2 landed); null for
            // menu/direct labs. Either way step-fact emits stay inert at launch - the bus has no
            // subscribers yet (see the field region above). Re-resolved on each Restart() so a context
            // attached between runs is picked up (no stale null).
            _ctx = Pitech.XR.Core.LabRuntimeContext.Find(_console);

            int idx = 0;

            while (idx >= 0 && idx < scenario.steps.Count)
            {
                StepIndex = idx;
                var step = scenario.steps[idx];
                if (step == null) { idx++; continue; }

                // WS B1.7 Increment 3: dormant outputs (both no-op at launch - see the region above).
                EmitStepFact(Pitech.XR.Core.ScenarioFactKeys.StepEntered, step.guid);
                if (_flow != null && _flow.IsDriver) _flow.AppendEntered(step.guid);

                // make sure only visuals of the current step can be seen or clicked
                DeactivateAllVisuals();

                string branchGuid = null;

                if (step is TimelineStep tl)
                {
                    yield return RunTimeline(tl);
                    branchGuid = tl.nextGuid;
                }
                else if (step is CueCardsStep cc)
                {
                    yield return RunCueCards(cc);
                    branchGuid = cc.nextGuid;
                }
                else if (step is QuestionStep q)
                {
                    yield return RunQuestion(q, guid => branchGuid = guid);
                }
                else if (step is SelectionStep sel)
                {
                    yield return RunSelection(sel, guid => branchGuid = guid);
                }
                else if (step is InsertStep ins)
                {
                    yield return RunInsert(ins);
                    branchGuid = ins.nextGuid;
                }
                else if (step is EventStep ev)
                {
                    yield return RunEvent(ev);
                    branchGuid = ev.nextGuid;
                }
                else if (step is GroupStep g)
                {
                    yield return RunGroup(g);
                    branchGuid = _groupExitBranchResolved ? _groupExitNextGuid : g.nextGuid;
                    _groupExitBranchResolved = false;
                    _groupExitNextGuid = null;
                }
                else if (step is QuizStep qz)
                {
                    yield return RunQuiz(qz, guid => branchGuid = guid);
                }
                else if (step is QuizResultsStep qrs)
                {
                    yield return RunQuizResults(qrs, guid => branchGuid = guid);
                }
                else if (step is MiniQuizStep mq)
                {
                    yield return RunMiniQuiz(mq, guid => branchGuid = guid);
                }
                else if (step is ConditionsStep cnd)
                {
                    yield return RunConditions(cnd, guid => branchGuid = guid);
                }

                // WS B1.7 Increment 3: step-completed fact (dormant - no-op at launch).
                EmitStepFact(Pitech.XR.Core.ScenarioFactKeys.StepCompleted, step.guid);

                // compute next index. empty guid means "next in list"
                if (string.IsNullOrEmpty(branchGuid))
                {
                    idx = idx + 1;
                }
                else
                {
                    int jump = FindIndexByGuid(branchGuid);
                    idx = jump >= 0 ? jump : idx + 1;
                }

                // reset editor skip flags after each step
                _editorSkip = false;
                _editorSkipBranchIndex = 0;

                yield return null;

            }


            DeactivateAllVisuals();
            StepIndex = -1;
            _run = null;
        }

        int FindIndexByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || scenario?.steps == null) return -1;
            for (int i = 0; i < scenario.steps.Count; i++)
                if (scenario.steps[i] != null && scenario.steps[i].guid == guid)
                    return i;
            return -1;
        }

        // ---------------- TIMELINE ----------------
        IEnumerator RunTimeline(TimelineStep tl)
        {
            var d = tl.director;
            if (!d) yield break;

            // Proper rewind
            if (tl.rewindOnEnter)
            {
                d.time = 0;
                d.Evaluate();           // ensure graph jumps to t=0 immediately
            }

            d.Play();
            yield return null;          // let it start this frame

            if (!tl.waitForEnd)
                yield break;

            bool done = false;
            void OnStopped(PlayableDirector _) => done = true;
            d.stopped += OnStopped;

            // Fallback polling: treat as finished when we’re at/after duration
            // and it's not looping (or state stopped playing).
            const double Eps = 1e-3;
            while (!done)
            {
                if (_editorSkip)
                {
                    done = true;
                    break;
                }

                // if timeline is not looping and we've reached (or passed) the end
                bool atEnd = d.duration > 0 &&
                             d.extrapolationMode != DirectorWrapMode.Loop &&
                             d.time >= d.duration - Eps;

                if (atEnd || d.state != PlayState.Playing)
                    done = true;

                yield return null;
            }

            d.stopped -= OnStopped;
        }


        // ---------------- CUE CARDS ----------------
        IEnumerator RunCueCards(CueCardsStep cc)
        {
            var cards = cc.cards;
            if (cards == null || cards.Length == 0) yield break;

            // Advance input
            bool useButton = cc.advanceMode == CueCardsStep.AdvanceMode.OnButton && cc.nextButton != null;
            bool advanceRequested = false;
            UnityAction nextCb = null;
            if (cc.advanceMode == CueCardsStep.AdvanceMode.OnButton && cc.nextButton == null)
                Debug.LogWarning("[Scenario] CueCardsStep: Advance Mode is OnButton but Next Button is not assigned. Falling back to TapAnywhere.", _console);
            if (useButton)
            {
                nextCb = () => advanceRequested = true;
                cc.nextButton.onClick.AddListener(nextCb);
            }

            // hide all first
            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            // use director only as an optional clock
            var d = cc.director;
            if (d && d.state != PlayState.Playing) d.Play();

            // wait release so a click from a previous step does not get consumed here
            yield return WaitForPointerRelease();

            int cur = cc.autoShowFirst ? 0 : -1;
            if (cur == 0) SafeSet(cards[cur], true);

            while (true)
            {
                if (_editorSkip)
                    break;

                // if not auto showing the first card wait for first click to reveal it
                if (cur < 0)
                {
                    if (!useButton)
                    {
                        yield return WaitForCleanClick();
                    }
                    else
                    {
                        while (!advanceRequested && !_editorSkip)
                            yield return null;
                        advanceRequested = false;
                    }
                    cur = 0;
                    SafeSet(cards[cur], true);
                }

                // card timeout (0 = no timeout)
                float timeout = 0f;
                if (cc.cueTimes != null && cc.cueTimes.Length > 0)
                    timeout = (cc.cueTimes.Length == 1) ? cc.cueTimes[0]
                              : (cur < cc.cueTimes.Length ? cc.cueTimes[cur] : 0f);

                // wait for click or timeout
                float t = 0f;
                while (true)
                {
                    if (!useButton)
                    {
                        if (JustClicked()) break;
                    }
                    else if (advanceRequested)
                    {
                        advanceRequested = false;
                        break;
                    }

                    if (timeout > 0f)
                    {
                        if (d && d.state != PlayState.Playing) break;
                        t += Time.deltaTime;
                        if (t >= timeout) break;
                    }

                    yield return null;
                }

                // consume click so it does not skip next card
                if (!useButton)
                    yield return WaitForPointerRelease();

                // advance
                SafeSet(cards[cur], false);

                if (cur >= cards.Length - 1) break;

                cur++;
                SafeSet(cards[cur], true);
            }

            // all off at end
            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            if (useButton && cc.nextButton && nextCb != null)
                cc.nextButton.onClick.RemoveListener(nextCb);
        }

        // ---------------- QUESTION ----------------
        IEnumerator RunQuestion(QuestionStep q, System.Action<string> onChoice, StepRunContext ctx = null)
        {
            // show and enable only now
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger))
                q.panelAnimator.SetTrigger(q.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            string nextGuid = null;

            void Cleanup()
            {
                if (wired.Count > 0)
                {
                    foreach (var (btn, fn) in wired) if (btn) btn.onClick.RemoveListener(fn);
                    wired.Clear();
                }
                if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);
            }

            if (ctx != null)
                ctx.cancel = Cleanup;

            if (q.choices != null)
            {
                for (int i = 0; i < q.choices.Count; i++)
                {
                    int idx = i;
                    var choice = q.choices[idx];
                    if (choice == null || choice.button == null) continue;

                    UnityAction fn = () =>
                    {
                        // per-choice events (SFX, animations, etc.)
                        choice.onSelected?.Invoke();

                        // apply stat effects (shared helper)
                        ApplyEffects(choice.effects);

                        // IMPORTANT: use FallbackGuid so "" means "linear next"
                        nextGuid = FallbackGuid(choice.nextGuid);

                        // hide
                        if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                            q.panelAnimator.SetTrigger(q.hideTrigger);
                        else if (q.panelRoot)
                            q.panelRoot.gameObject.SetActive(false);
                    };

                    choice.button.onClick.AddListener(fn);
                    wired.Add((choice.button, fn));
                }
            }

            // wait until *something* sets it (normal click or editor skip)
            while (nextGuid == null)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

                if (_editorSkip && _editorSkipBranchIndex >= 0 && q.choices != null && _editorSkipBranchIndex < q.choices.Count)
                {
                    var choice = q.choices[_editorSkipBranchIndex];
                    if (choice != null)
                    {
                        choice.onSelected?.Invoke();
                        ApplyEffects(choice.effects);
                        nextGuid = FallbackGuid(choice.nextGuid);
                    }
                    break;
                }

                yield return null;
            }

            // remove listeners to avoid double fires later
            Cleanup();

            if (nextGuid != null)
                onChoice?.Invoke(nextGuid);

            // debounce so the click that chose the option does not also click the next step
            if (nextGuid != null)
                yield return WaitForPointerRelease();
        }

        // ---------------- SELECTION ----------------
        IEnumerator RunSelection(SelectionStep s, System.Action<string> onComplete, StepRunContext ctx = null)
        {
            // prefer the step's local reference; fall back to the manager-level field
            var lists = s.lists != null ? s.lists : selectionLists;

            // show and enable only now
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);
            if (s.hint) s.hint.SetActive(true);

            // Activate requested list
            int active = -1;
            if (lists != null)
            {
                if (!string.IsNullOrEmpty(s.listKey))
                    active = lists.ShowList(s.listKey, s.resetOnEnter);
                else
                    active = lists.ShowList(s.listIndex, s.resetOnEnter);
            }

            if (active < 0)
            {
                Debug.LogWarning("[Scenario] SelectionStep: could not activate requested list. Will route WRONG.");
                yield return HideSelectionUI(s);
                onComplete?.Invoke(FallbackGuid(s.wrongNextGuid));
                yield break;
            }

            // Optional submit wiring
            bool submitted = false;
            UnityAction submitCb = null;
            if (s.completion == SelectionStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => submitted = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            void Cleanup()
            {
                if (submitCb != null && s.submitButton)
                    s.submitButton.onClick.RemoveListener(submitCb);
                if (lists != null && lists.selectables != null)
                    lists.selectables.pickingEnabled = false;
            }

            if (ctx != null)
                ctx.cancel = () =>
                {
                    Cleanup();
                    if (s.hint) s.hint.SetActive(false);
                    if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);
                };

            float t = 0f;
            bool done = false;
            bool isCorrect = false;

            while (!done)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

                // timeout => WRONG
                if (s.timeoutSeconds > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= s.timeoutSeconds)
                    {
                        isCorrect = false;
                        break;
                    }
                }
                // Editor graph skip override
                if (_editorSkip)
                {
                    if (_editorSkipBranchIndex == -2) // Correct
                    {
                        isCorrect = true;
                        done = true;
                    }
                    else if (_editorSkipBranchIndex == -3) // Wrong
                    {
                        isCorrect = false;
                        done = true;
                    }
                }

                // Always evaluate (cheap + robust even if no OnSelectionChanged is fired)
                var e = lists.EvaluateActive();

                bool countOK = s.requireExactCount
                    ? (e.selectedTotal == s.requiredSelections)
                    : (e.selectedTotal >= s.requiredSelections);

                if (s.completion == SelectionStep.CompleteMode.AutoWhenRequirementMet)
                {
                    if (countOK)
                    {
                        // correctness: within wrong tolerance
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = wrongOK;                 // <- removed the "e.selectedCorrect > 0" gate
                        done = true;
                    }
                }
                else // OnSubmitButton
                {
                    if (submitted)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = countOK && wrongOK;     // <- also no "must have ≥1 correct" gate
                        done = true;
                    }
                }

                yield return null;
            }

            Cleanup();

            if (ctx == null || !ctx.cancelRequested)
            {
                // Hide UI & disable picking to avoid spill into next step
                yield return HideSelectionUI(s);

                // Events
                try
                {
                    if (isCorrect) s.onCorrect?.Invoke();
                    else s.onWrong?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, _console);
                }

                // (Optional) Stats lists – still supported if you didn’t remove them
                if (isCorrect) ApplyEffects(s.onCorrectEffects);
                else ApplyEffects(s.onWrongEffects);

                // Route
                onComplete?.Invoke(isCorrect ? FallbackGuid(s.correctNextGuid) : FallbackGuid(s.wrongNextGuid));

                // debounce any final click (especially on submit button)
                yield return WaitForPointerRelease();
            }
        }

        // ---------------- QUIZ ----------------
        IEnumerator RunQuiz(QuizStep qz, System.Action<string> onComplete)
        {
            var asset = qz.quiz != null ? qz.quiz : defaultQuiz;
            if (asset == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: no QuizAsset assigned.");
                yield break;
            }

            QuizAsset.Question question = null;
            if (!string.IsNullOrEmpty(qz.questionId))
                question = asset.FindQuestion(qz.questionId);
            if (question == null && qz.questionIndex >= 0 && qz.questionIndex < asset.questions.Count)
                question = asset.questions[qz.questionIndex];

            if (question == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: question not found (check questionId/index).");
                yield break;
            }

            var session = GetOrCreateQuizSession(asset);

            if (quizPanel == null)
            {
                Debug.LogWarning("[Scenario] QuizStep: Quiz UI missing (QuizUIController).");
                yield break;
            }

            bool done = false;
            bool isCorrect = false;
            // Multi-choice always requires submit. Single-choice can be immediate or submit-button based on step setting.
            var submitMode = (question.type == QuizAsset.QuestionType.MultipleChoice)
                ? QuizUIController.SubmitMode.OnSubmitButton
                : (qz.submitMode == QuizStep.AnswerSubmitMode.OnSubmitButton
                    ? QuizUIController.SubmitMode.OnSubmitButton
                    : QuizUIController.SubmitMode.ImmediateSelection);

            var feedbackMode =
                qz.feedback == QuizStep.FeedbackMode.ForSeconds ? QuizUIController.FeedbackMode.ForSeconds :
                qz.feedback == QuizStep.FeedbackMode.UntilContinue ? QuizUIController.FeedbackMode.UntilContinue :
                QuizUIController.FeedbackMode.None;

            quizPanel.ShowQuestion(question, asset, session, result =>
            {
                isCorrect = result != null && result.isCorrect;
                ApplyQuizStats(session, asset);
                done = true;
            },
            submitMode,
            feedbackMode,
            qz.feedbackSeconds);

            while (!done)
            {
                // Editor skip support: allows jumping via graph without clicking UI.
                if (_editorSkip)
                {
                    // Branch index: -2 correct, -3 wrong, else advance
                    isCorrect = _editorSkipBranchIndex == -2;
                    quizPanel.Hide();
                    done = true;
                }
                yield return null;
            }

            string next = qz.completion == QuizStep.CompleteMode.BranchOnCorrectness
                ? (isCorrect ? FallbackGuid(qz.correctNextGuid) : FallbackGuid(qz.wrongNextGuid))
                : FallbackGuid(qz.nextGuid);
            onComplete?.Invoke(next);

            yield return WaitForPointerRelease();
        }

        void ApplyQuizStats(QuizSession session, QuizAsset asset)
        {
            // WS B1.2 Step 4: quiz magic keys now write to the param store (the Stats successor); the
            // legacy StatsRuntime mirrors them for the UI via LabConsole's bridge. Same gate as before.
            if (!HasStatsFeature || session == null) return;

            var store = Params;
            if (store == null) return;

            if (statsUI != null && !_statsBound)
            {
                if (runtime == null) runtime = new StatsRuntime();
                statsUI.Init(runtime, syncNow: true);
                _statsBound = true;
            }

            // Update stats frequently, but don't spam "quiz completed" events.
            var summary = session.BuildSummary(invokeEvent: false);
            store.SetFloat("Quiz.Score", summary.totalScore);
            store.SetFloat("Quiz.MaxScore", summary.maxScore);
            store.SetFloat("Quiz.CorrectCount", summary.correctCount);
            store.SetFloat("Quiz.WrongCount", summary.wrongCount);
            store.SetFloat("Quiz.AnsweredCount", summary.answeredCount);
            store.SetFloat("Quiz.TotalQuestions", asset != null && asset.questions != null ? asset.questions.Count : 0);
        }

        IEnumerator RunQuizResults(QuizResultsStep rs, System.Action<string> onComplete)
        {
            var asset = rs.quiz != null ? rs.quiz : defaultQuiz;
            if (asset == null)
            {
                Debug.LogWarning("[Scenario] QuizResultsStep: no QuizAsset assigned.");
                onComplete?.Invoke(FallbackGuid(rs.nextGuid));
                yield break;
            }

            var session = GetOrCreateQuizSession(asset);
            var summary = session != null ? session.BuildSummary(invokeEvent: true) : null;

            if (quizResultsPanel == null)
            {
                Debug.LogWarning("[Scenario] QuizResultsStep: Quiz Results UI missing (QuizResultsUIController).");
            }
            else
            {
                bool done = false;
                // Configure continue button visibility based on "When Complete".
                bool wantsContinue = rs.whenComplete == QuizResultsStep.WhenComplete.AfterContinueButtonPressed;
                if (quizResultsPanel.continueButton != null)
                    quizResultsPanel.continueButton.gameObject.SetActive(wantsContinue);

                quizResultsPanel.Show(asset, summary, () => done = true);

                if (wantsContinue)
                {
                    while (!done)
                    {
                        if (_editorSkip)
                        {
                            done = true;
                            quizResultsPanel.Hide();
                        }
                        yield return null;
                    }
                }
                else
                {
                    float seconds = Mathf.Max(0f, rs.completeAfterSeconds);
                    float t = 0f;
                    while (t < seconds)
                    {
                        if (_editorSkip)
                        {
                            done = true;
                            quizResultsPanel.Hide();
                            break;
                        }
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                quizResultsPanel.Hide();
                yield return WaitForPointerRelease();
            }

            bool passed = summary != null && (asset.passThresholdPercent <= 0f || summary.passed);
            string next = rs.completion == QuizResultsStep.CompleteMode.BranchOnPassed
                ? (passed ? FallbackGuid(rs.passedNextGuid) : FallbackGuid(rs.failedNextGuid))
                : FallbackGuid(rs.nextGuid);

            onComplete?.Invoke(next);
        }

        // ---------------- MINI QUIZ ----------------
        IEnumerator RunMiniQuiz(MiniQuizStep s, System.Action<string> onComplete, StepRunContext ctx = null)
        {
            if (s == null)
                yield break;

            // show and enable only now
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            UnityAction submitCb = null;

            int correct = 0;
            int qCount = s.questions != null ? s.questions.Count : 0;
            var answered = new bool[Mathf.Max(0, qCount)];

            bool done = false;
            string nextGuid = null;

            void Cleanup()
            {
                if (wired.Count > 0)
                {
                    foreach (var (btn, fn) in wired) if (btn) btn.onClick.RemoveListener(fn);
                    wired.Clear();
                }
                if (submitCb != null && s.submitButton)
                {
                    s.submitButton.onClick.RemoveListener(submitCb);
                    submitCb = null;
                }
                if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);
            }

            if (ctx != null)
                ctx.cancel = Cleanup;

            bool AllAnswered()
            {
                for (int i = 0; i < answered.Length; i++)
                    if (!answered[i]) return false;
                return true;
            }

            // wire all answer buttons
            if (s.questions != null)
            {
                for (int qi = 0; qi < s.questions.Count; qi++)
                {
                    int qIndex = qi;
                    var q = s.questions[qIndex];
                    if (q == null || q.choices == null) continue;

                    for (int ci = 0; ci < q.choices.Count; ci++)
                    {
                        var ch = q.choices[ci];
                        if (ch == null || ch.button == null) continue;

                        UnityAction fn = () =>
                        {
                            // first answer wins (unless user opted out of locking)
                            if (answered[qIndex] && s.lockQuestionAfterAnswer) return;
                            if (!answered[qIndex])
                            {
                                answered[qIndex] = true;
                                if (ch.isCorrect) correct++;
                            }

                            ch.onSelected?.Invoke();
                            ApplyEffects(ch.effects);

                            if (s.lockQuestionAfterAnswer)
                            {
                                // disable all buttons for that question after answering
                                if (q.choices != null)
                                    foreach (var other in q.choices)
                                        if (other != null && other.button != null)
                                            other.button.interactable = false;
                            }

                            if (s.completion == MiniQuizStep.CompleteMode.AutoWhenAllAnswered && AllAnswered())
                                done = true;
                        };

                        ch.button.onClick.AddListener(fn);
                        wired.Add((ch.button, fn));
                    }
                }
            }

            // submit mode
            if (s.completion == MiniQuizStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => done = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            // wait until complete
            while (!done)
            {
                if (ctx != null && ctx.cancelRequested)
                    break;

                // Editor skip (playmode testing from ScenarioGraph)
                if (_editorSkip)
                {
                    if (_editorSkipBranchIndex == -1)
                    {
                        done = true;
                        nextGuid = s.defaultNextGuid;
                    }
                    else if (_editorSkipBranchIndex >= 0 && s.outcomes != null && _editorSkipBranchIndex < s.outcomes.Count)
                    {
                        done = true;
                        nextGuid = s.outcomes[_editorSkipBranchIndex]?.nextGuid;
                    }
                    else
                    {
                        done = true;
                        nextGuid = s.defaultNextGuid;
                    }
                    break;
                }
                yield return null;
            }

            // route by score
            if (nextGuid == null && s.outcomes != null && s.outcomes.Count > 0)
            {
                // Pick the MOST SPECIFIC matching outcome (smallest range),
                // so authoring mistakes like having an "Any (0..-1)" outcome before an "Exact 0..0" outcome
                // still resolve to the intended exact match.
                MiniQuizOutcome best = null;
                int bestSpan = int.MaxValue;

                for (int i = 0; i < s.outcomes.Count; i++)
                {
                    var o = s.outcomes[i];
                    if (o == null) continue;

                    int min = Mathf.Max(0, o.minCorrect);
                    int max = o.maxCorrect;
                    if (max >= 0 && max < min) continue; // invalid range

                    bool minOk = correct >= min;
                    bool maxOk = max < 0 || correct <= max;
                    if (!minOk || !maxOk) continue;

                    int span = max < 0 ? int.MaxValue : Mathf.Max(0, max - min);
                    if (best == null || span < bestSpan)
                    {
                        best = o;
                        bestSpan = span;
                        if (bestSpan == 0) break; // exact match can't be beaten
                    }
                }

                if (best != null)
                    nextGuid = best.nextGuid;
            }

            if (nextGuid == null)
                nextGuid = s.defaultNextGuid;

            if (!string.IsNullOrEmpty(nextGuid))
                Debug.Log($"[Scenario] MiniQuiz: correct={correct}/{(s.questions != null ? s.questions.Count : 0)} -> next={nextGuid}", _console);

            Cleanup();

            onComplete?.Invoke(nextGuid);

            // debounce so last click doesn't also hit something behind
            yield return WaitForPointerRelease();
        }

        IEnumerator HideSelectionUI(SelectionStep s)
        {
            if (s.hint) s.hint.SetActive(false);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.hideTrigger))
                s.panelAnimator.SetTrigger(s.hideTrigger);
            else if (s.panelRoot)
                s.panelRoot.gameObject.SetActive(false);
            yield return null; // settle one frame
        }

        static string FallbackGuid(string prefer)
        {
            // empty => linear next
            return string.IsNullOrEmpty(prefer) ? "" : prefer;
        }

        // ---------------- CONDITIONS ----------------
        IEnumerator RunConditions(ConditionsStep cnd, System.Action<string> onComplete)
        {
            if (cnd == null)
            {
                onComplete?.Invoke(FallbackGuid(""));
                yield break;
            }

            var outcomes = cnd.outcomes;
            if (outcomes == null || outcomes.Count == 0)
            {
                onComplete?.Invoke(FallbackGuid(""));
                yield break;
            }

            string nextGuid = null;

#if UNITY_EDITOR
            if (_editorSkip)
            {
                if (_editorSkipBranchIndex >= 0 && outcomes != null && _editorSkipBranchIndex < outcomes.Count)
                {
                    var o = outcomes[_editorSkipBranchIndex];
                    if (o != null) nextGuid = o.nextGuid;
                }
                if (nextGuid == null) nextGuid = "";
                onComplete?.Invoke(FallbackGuid(nextGuid));
                yield break;
            }
#endif

            float value = GetConditionValue(cnd);
            foreach (var o in outcomes)
            {
                if (o == null) continue;
                if (ConditionsEvaluator.EvalCompare(value, o.compareOp, o.compareValue))
                {
                    nextGuid = o.nextGuid;
                    break;
                }
            }
            if (nextGuid == null)
                nextGuid = "";
            onComplete?.Invoke(FallbackGuid(nextGuid));
        }

        float GetConditionValue(ConditionsStep step)
        {
            if (step.valueSource == ConditionValueSource.Stat)
            {
                // WS B1.2 Step 4: read from the param store (the Stats successor). GetFloat returns the
                // fallback for an unset key - same result as the legacy runtime.TryGet(...) ? v : 0f.
                var key = string.IsNullOrEmpty(step.statKey) ? step.memberName : step.statKey;
                var store = Params;
                // Normalize like the legacy runtime.TryGet did (StatsRuntime trimmed via NormalizeKey).
                return store != null ? store.GetFloat(StatsConfig.NormalizeKey(key), 0f) : 0f;
            }
            if (step.valueSource == ConditionValueSource.ListByLabel)
            {
                if (step.source == null || string.IsNullOrEmpty(step.listFieldName) || string.IsNullOrEmpty(step.listEntryLabel))
                    return 0f;
                var labelField = string.IsNullOrEmpty(step.listLabelFieldName) ? "label" : step.listLabelFieldName;
                var valueField = string.IsNullOrEmpty(step.listValueFieldName) ? "count" : step.listValueFieldName;
                return GetValueFromLabeledList(step.source, step.listFieldName, step.listEntryLabel, labelField, valueField);
            }
            if (step.source == null || string.IsNullOrEmpty(step.memberName))
                return 0f;
            return GetValueFromComponent(step.source, step.memberName);
        }

        static float GetValueFromComponent(Component comp, string memberName)
        {
            if (comp == null || string.IsNullOrEmpty(memberName)) return 0f;
            var t = comp.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            var f = t.GetField(memberName, flags);
            if (f != null)
            {
                var val = f.GetValue(comp);
                if (val is float fv) return fv;
                if (val is int iv) return iv;
                if (val is bool bv) return bv ? 1f : 0f;
                return 0f;
            }
            var p = t.GetProperty(memberName, flags);
            if (p != null)
            {
                var val = p.GetValue(comp);
                if (val is float fv) return fv;
                if (val is int iv) return iv;
                if (val is bool bv) return bv ? 1f : 0f;
                return 0f;
            }
            return 0f;
        }

        /// <summary>
        /// Reads a numeric value from a component field/property that holds an enumerable of rows:
        /// finds the row whose <paramref name="labelFieldName"/> string equals <paramref name="entryLabel"/>,
        /// then reads <paramref name="valueFieldName"/> from that row.
        /// </summary>
        static float GetValueFromLabeledList(Component comp, string listFieldName, string entryLabel, string labelFieldName, string valueFieldName)
        {
            if (comp == null || string.IsNullOrEmpty(listFieldName) || string.IsNullOrEmpty(entryLabel)) return 0f;
            var listObj = GetFieldOrPropertyValue(comp, listFieldName);
            if (listObj == null) return 0f;
            if (listObj is string || listObj is not IEnumerable enumerable) return 0f;

            foreach (var item in enumerable)
            {
                if (item == null) continue;
                var lab = ReadStringMember(item, labelFieldName);
                if (lab != entryLabel) continue;
                return ReadNumericMember(item, valueFieldName);
            }
            return 0f;
        }

        static object GetFieldOrPropertyValue(object target, string name)
        {
            if (target == null || string.IsNullOrEmpty(name)) return null;
            var t = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var f = t.GetField(name, flags);
            if (f != null) return f.GetValue(target);
            var p = t.GetProperty(name, flags);
            if (p != null) return p.GetValue(target);
            return null;
        }

        static string ReadStringMember(object item, string memberName)
        {
            if (item == null || string.IsNullOrEmpty(memberName)) return null;
            var t = item.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var f = t.GetField(memberName, flags);
            if (f != null && f.GetValue(item) is string s1) return s1;
            var p = t.GetProperty(memberName, flags);
            if (p != null && p.GetValue(item) is string s2) return s2;
            return null;
        }

        static float ReadNumericMember(object item, string memberName)
        {
            if (item == null || string.IsNullOrEmpty(memberName)) return 0f;
            var t = item.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var f = t.GetField(memberName, flags);
            if (f != null) return ConvertToConditionFloat(f.GetValue(item));
            var p = t.GetProperty(memberName, flags);
            if (p != null) return ConvertToConditionFloat(p.GetValue(item));
            return 0f;
        }

        static float ConvertToConditionFloat(object val)
        {
            if (val == null) return 0f;
            if (val is float fv) return fv;
            if (val is int iv) return iv;
            if (val is bool bv) return bv ? 1f : 0f;
            if (val is double dv) return (float)dv;
            return 0f;
        }

        void ApplyEffects(List<StatEffect> effects)
        {
            // WS B1.2 Step 4: effects now drive the typed param store (the Stats successor; map sec-8) -
            // the source of truth. The legacy StatsRuntime is a display mirror, fed by LabConsole's
            // ParamChanged bridge. ParamOp's member order == StatOp's, so the op migrates by value (a
            // rename, not a behaviour change); range clamp is enforced when the param is declared. Same
            // gate as before ("no stats feature -> skip"), now also true when parameters are declared.
            if (!HasStatsFeature || effects == null || effects.Count == 0)
                return;

            var store = Params;
            if (store == null) return;

            // Keep the legacy stats UI bound (display mirror) - idempotent via _statsBound.
            if (statsUI != null && !_statsBound)
            {
                if (runtime == null) runtime = new StatsRuntime();
                statsUI.Init(runtime, syncNow: true);
                _statsBound = true;
            }

            foreach (var eff in effects)
            {
                if (eff == null) continue;
                // Normalize the key with the SAME normalizer the legacy StatsRuntime used (Trim), so a
                // key authored with stray whitespace still matches its declaration (behaviour parity).
                store.Apply(StatsConfig.NormalizeKey(eff.key), (Pitech.XR.Core.ParamOp)(int)eff.op, eff.value);
            }
        }

        // ---------------- INSERT ----------------
        IEnumerator RunInsert(InsertStep ins)
        {
            if (ins == null || ins.item == null || ins.targetTrigger == null)
            {
                Debug.LogWarning("[Scenario] InsertStep requires Item and TargetTrigger.", _console);
                yield break;
            }

            // Ensure item & trigger visible
            SafeSet(ins.item.gameObject, true);
            SafeSet(ins.targetTrigger.gameObject, true);

            // Avoid consuming previous click
            yield return WaitForPointerRelease();

            // Find all item colliders
            var itemColliders = ins.item.GetComponentsInChildren<Collider>();
            if (itemColliders == null || itemColliders.Length == 0)
            {
                Debug.LogWarning("[Scenario] InsertStep: Item has no Colliders. Completing immediately.", _console);
            }
            else
            {
                bool hit = false;

                while (!hit)
                {
                    if (_editorSkip)
                        break;   // bail out early on editor skip

                    if (!ins.targetTrigger)
                        yield break;

                    foreach (var col in itemColliders)
                    {
                        if (!col) continue;
                        if (AreCollidersOverlapping(col, ins.targetTrigger))
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (!hit)
                        yield return null;
                }
            }

            // We consider it "inserted" here
            var body = ins.item.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                // Always stop any crazy motion
                StopBodyMotion(body);
                // BUT: do NOT touch isKinematic here
                // that depends on smoothAttach
            }

            // Only if smoothAttach is enabled we do the snapping / parenting style behaviour
            if (ins.smoothAttach)
            {
                Transform targetPose =
                    ins.attachTransform != null
                        ? ins.attachTransform
                        : ins.targetTrigger.transform;

                if (targetPose != null)
                {
                    // For smooth attach we usually want to freeze physics
                    if (body != null)
                        body.isKinematic = true;

                    if (ins.parentToAttach)
                        ins.item.SetParent(targetPose, true);

                    while (true)
                    {
                        ins.item.position = Vector3.MoveTowards(
                            ins.item.position,
                            targetPose.position,
                            ins.moveSpeed * Time.deltaTime
                        );

                        ins.item.rotation = Quaternion.Slerp(
                            ins.item.rotation,
                            targetPose.rotation,
                            ins.rotateSpeed * Time.deltaTime
                        );

                        float posDist = Vector3.Distance(ins.item.position, targetPose.position);
                        float ang = Quaternion.Angle(ins.item.rotation, targetPose.rotation);

                        if (posDist < 0.01f && ang < 1f)
                            break;

                        yield return null;
                    }
                }
            }

            // If smoothAttach == false we did NOT change isKinematic
            // The object just stays where the user left it after entering the trigger

            // Debounce any grab / click
            yield return WaitForPointerRelease();
        }

        // ---------------- EVENT ----------------
        IEnumerator RunEvent(EventStep ev)
        {
            if (ev == null)
                yield break;

            // Fire the event safely
            try
            {
                ev.onEnter?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, _console);
            }

            // Optional wait after onEnter (real-time seconds; not affected by Time.timeScale)
            float wait = Mathf.Max(0f, ev.waitSeconds);
            if (wait > 0f)
            {
                float t = 0f;
                while (t < wait)
                {
                    if (_editorSkip)
                        break; // skip cancels waiting

                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Then we simply return, and the main Run() loop advances to next step
        }

        // ---------------- helpers ----------------
        static void StopBodyMotion(Rigidbody body)
        {
            if (body == null) return;
#if UNITY_6000_0_OR_NEWER
            // Unity 6+
            body.linearVelocity = Vector3.zero;
#else
            // Unity 2022/2023/2024
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
        }

        static bool AnyPointerDown()
        {
#if ENABLE_INPUT_SYSTEM
            // If there are no pointer devices (VR-only), treat it as "nothing is pressed".
            bool hasMouse = Mouse.current != null;
            bool hasTouch = Touchscreen.current != null;

            if (hasMouse && Mouse.current.leftButton.isPressed) return true;

            if (hasTouch)
            {
                var ts = Touchscreen.current;
                foreach (var t in ts.touches)
                {
                    if (t.press.isPressed) return true;
                }
            }
            return false;
#else
            if (UnityEngine.Input.GetMouseButton(0)) return true;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var ph = UnityEngine.Input.GetTouch(i).phase;
                if (ph == UnityEngine.TouchPhase.Began ||
                    ph == UnityEngine.TouchPhase.Moved ||
                    ph == UnityEngine.TouchPhase.Stationary)
                    return true;
            }
            return false;
#endif
        }

        static bool JustClicked()
        {
#if ENABLE_INPUT_SYSTEM
            bool hasMouse = Mouse.current != null;
            bool hasTouch = Touchscreen.current != null;

            if (hasMouse && Mouse.current.leftButton.wasPressedThisFrame) return true;

            if (hasTouch)
            {
                var ts = Touchscreen.current;
                foreach (var t in ts.touches)
                {
                    if (t.press.wasPressedThisFrame) return true;
                }
            }
            return false;
#else
            if (UnityEngine.Input.GetMouseButtonDown(0)) return true;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                if (UnityEngine.Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began)
                    return true;
            }
            return false;
#endif
        }

        static System.Collections.IEnumerator WaitForPointerRelease()
        {
#if ENABLE_INPUT_SYSTEM
            // In VR (no mouse/touch), do not wait for anything - return immediately.
            if (Mouse.current == null && Touchscreen.current == null)
                yield break;
#endif
            while (AnyPointerDown()) yield return null;
        }

        static System.Collections.IEnumerator WaitForCleanClick()
        {
#if ENABLE_INPUT_SYSTEM
            // In VR (no mouse/touch), a "clean click" can never happen -> do not block.
            if (Mouse.current == null && Touchscreen.current == null)
                yield break;
#endif
            while (!JustClicked()) yield return null;
            while (AnyPointerDown()) yield return null;
        }

        static void SafeSet(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }

        static void HidePanelRoot(RectTransform rt)
        {
            if (!rt) return;
            var go = rt.gameObject;

            // Prefer CanvasGroup-based hiding (doesn't disable hierarchy)
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                if (!go.activeSelf) go.SetActive(true);
                return;
            }

            // Common authoring mistake: assigning the whole Canvas as "panelRoot".
            // Never disable a Canvas root; it would blank the entire UI and looks like a bug.
            if (go.GetComponent<Canvas>() != null)
            {
                Debug.LogWarning(
                    "[Scenario] panelRoot points to a Canvas. Please assign a child container GameObject instead (e.g. a Panel under the Canvas).",
                    go);
                return;
            }

            SafeSet(go, false);
        }

        /// Disable all visuals of all steps so nothing is interactable until its turn
        internal void DeactivateAllVisuals()   // internal: LabConsole.Awake() drives the initial reset cross-class
        {
            if (scenario?.steps == null) return;

            DeactivateAllVisualsRecursive(scenario.steps);

            if (quizPanel != null) quizPanel.Hide();
            if (quizResultsPanel != null) quizResultsPanel.Hide();
        }

        void DeactivateAllVisualsRecursive(List<Step> list)
        {
            if (list == null) return;

            foreach (var s in list)
            {
                if (s is CueCardsStep cc && cc.cards != null)
                {
                    foreach (var card in cc.cards) SafeSet(card, false);
                    if (cc.extraObject) SafeSet(cc.extraObject, false);
                    if (cc.tapHint) SafeSet(cc.tapHint, false);
                }
                else if (s is QuestionStep q)
                {
                    if (q.panelRoot) HidePanelRoot(q.panelRoot);
                }
                else if (s is SelectionStep sel)
                {
                    if (sel.panelRoot) HidePanelRoot(sel.panelRoot);
                    if (sel.hint) SafeSet(sel.hint, false);
                }
                else if (s is MiniQuizStep mq)
                {
                    if (mq.panelRoot) HidePanelRoot(mq.panelRoot);
                }
                else if (s is GroupStep g && g.steps != null)
                {
                    DeactivateAllVisualsRecursive(g.steps);
                }
            }
        }

        /// <summary>Editor-only play-mode hook: the Scenario Graph's Branch/Skip/Outcome buttons call this to advance or branch the running scenario at <paramref name="stepGuid"/> along <paramref name="branchIndex"/>. No-op outside play mode. This is the deterministic driver the Phase D golden-trace recorder wraps.</summary>
        public void EditorSkipFromGraph(string stepGuid, int branchIndex)
        {
            if (!Application.isPlaying) return;
            if (scenario == null || scenario.steps == null) return;

            if (StepIndex < 0 || StepIndex >= scenario.steps.Count) return;

            var current = scenario.steps[StepIndex];
            if (current == null || current.guid != stepGuid) return;

            // Linear-ish steps: just mark skip, RunX will read it
            if (current is TimelineStep ||
                current is CueCardsStep ||
                current is InsertStep ||
                current is EventStep ||
                current is GroupStep ||
                current is QuizResultsStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Question: choose specific choice by index
            if (current is QuestionStep q)
            {
                if (branchIndex < 0) return;
                if (q.choices == null) return;
                if (branchIndex >= q.choices.Count) return;

                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Selection: mark skip and branch type (-2 correct, -3 wrong)
            if (current is SelectionStep sel)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Quiz: mark skip and branch type (-2 correct, -3 wrong, else advance)
            if (current is QuizStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }

            // Mini Quiz: -1 = default, >= 0 = outcomes index
            if (current is MiniQuizStep)
            {
                _editorSkip = true;
                _editorSkipBranchIndex = branchIndex;
                return;
            }
        }

        // ---------------- GROUP ----------------
        sealed class GroupCancelToken
        {
            public bool Cancelled { get; private set; }
            public void Cancel() => Cancelled = true;
        }

        sealed class GroupChildHandle
        {
            public Step step;
            public string guid;
            public bool completed;
            public Coroutine coroutine;
            public Action cancel;
        }

        int _groupPickingRefs;

        void SetGroupPicking(SelectablesManager mgr, bool enable)
        {
            if (mgr == null) return;
            if (enable)
            {
                _groupPickingRefs++;
                mgr.pickingEnabled = true;
            }
            else
            {
                _groupPickingRefs = Mathf.Max(0, _groupPickingRefs - 1);
                if (_groupPickingRefs == 0)
                    mgr.pickingEnabled = false;
            }
        }

        IEnumerator RunGroup(GroupStep g)
            => RunGroupInternal(g, null);

        IEnumerator RunGroupInternal(GroupStep g, GroupCancelToken token)
        {
            _groupExitBranchResolved = false;
            _groupExitNextGuid = null;

            if (g == null || g.steps == null || g.steps.Count == 0)
                yield break;

            g.EnsureChildRequirements();

            var localToken = token ?? new GroupCancelToken();
            var handles = new List<GroupChildHandle>();

            foreach (var st in g.steps)
            {
                if (st == null) continue;
                if (string.IsNullOrEmpty(st.guid)) st.guid = System.Guid.NewGuid().ToString();
                handles.Add(StartGroupChild(st, localToken, g));
            }

            bool ShouldComplete()
            {
                if (_editorSkip || localToken.Cancelled) return true;

                int total = handles.Count;
                if (total == 0) return true;

                int completed = 0;
                int requiredTotal = 0;
                int requiredCompleted = 0;
                bool specificDone = false;

                for (int i = 0; i < handles.Count; i++)
                {
                    var h = handles[i];
                    if (h == null) continue;

                    if (h.completed) completed++;

                    bool required = g.IsChildRequired(h.guid);
                    if (required) requiredTotal++;
                    if (required && h.completed) requiredCompleted++;

                    if (!string.IsNullOrEmpty(g.specificStepGuid) && h.guid == g.specificStepGuid && h.completed)
                        specificDone = true;
                }

                switch (g.completeWhen)
                {
                    case GroupStep.CompleteWhen.AnyChildCompletes:
                        return completed > 0;
                    case GroupStep.CompleteWhen.SpecificChildCompletes:
                        return specificDone;
                    case GroupStep.CompleteWhen.RequiredChildrenComplete:
                        return requiredTotal == 0 || requiredCompleted >= requiredTotal;
                    case GroupStep.CompleteWhen.NOfMChildrenComplete:
                        return completed >= Mathf.Clamp(g.requiredCount, 1, total);
                    case GroupStep.CompleteWhen.MultiCondition:
                        return EvalMultiConditionBranches(g, handles);
                    case GroupStep.CompleteWhen.AllChildrenComplete:
                    default:
                        return completed >= total;
                }
            }

            while (!ShouldComplete())
                yield return null;

            // Stop unfinished children to prevent lingering UI/listeners.
            if (g.stopOthersOnComplete || localToken.Cancelled)
            {
                localToken.Cancel();
                for (int i = 0; i < handles.Count; i++)
                {
                    var h = handles[i];
                    if (h == null || h.completed) continue;
                    h.cancel?.Invoke();
                }
            }

            if (_editorSkip &&
                g.completeWhen == GroupStep.CompleteWhen.SpecificChildCompletes &&
                !string.IsNullOrEmpty(g.specificStepGuid) &&
                g.steps != null)
            {
                Step sub = null;
                for (int si = 0; si < g.steps.Count; si++)
                {
                    var st = g.steps[si];
                    if (st != null && st.guid == g.specificStepGuid)
                    {
                        sub = st;
                        break;
                    }
                }
                if (sub is QuestionStep qq &&
                    _editorSkipBranchIndex >= 0 &&
                    qq.choices != null &&
                    _editorSkipBranchIndex < qq.choices.Count)
                {
                    var choice = qq.choices[_editorSkipBranchIndex];
                    if (choice != null)
                    {
                        _groupExitBranchResolved = true;
                        _groupExitNextGuid = FallbackGuid(choice.nextGuid);
                    }
                }
                else if (sub is ConditionsStep cnd &&
                         _editorSkipBranchIndex >= 0 &&
                         cnd.outcomes != null &&
                         _editorSkipBranchIndex < cnd.outcomes.Count)
                {
                    var o = cnd.outcomes[_editorSkipBranchIndex];
                    if (o != null)
                    {
                        _groupExitBranchResolved = true;
                        _groupExitNextGuid = FallbackGuid(o.nextGuid);
                    }
                }
            }

            // Multi-Condition editor skip: resolve the branch at _editorSkipBranchIndex.
            if (_editorSkip &&
                g.completeWhen == GroupStep.CompleteWhen.MultiCondition &&
                g.multiConditionBranches != null &&
                _editorSkipBranchIndex >= 0 &&
                _editorSkipBranchIndex < g.multiConditionBranches.Count)
            {
                var branch = g.multiConditionBranches[_editorSkipBranchIndex];
                if (branch != null)
                {
                    _groupExitBranchResolved = true;
                    _groupExitNextGuid = FallbackGuid(branch.nextGuid);
                }
            }

            // Reset skip flag after group ends (like any other step).
            _editorSkip = false;
            _editorSkipBranchIndex = 0;
        }

        bool EvalMultiConditionBranches(GroupStep g, List<GroupChildHandle> handles)
        {
            if (g.multiConditionBranches == null || g.multiConditionBranches.Count == 0)
                return false;

            int total = handles.Count;
            if (total == 0) return true;

            int completed = 0;
            for (int i = 0; i < handles.Count; i++)
                if (handles[i] != null && handles[i].completed) completed++;

            for (int b = 0; b < g.multiConditionBranches.Count; b++)
            {
                var branch = g.multiConditionBranches[b];
                if (branch == null) continue;

                bool satisfied = false;
                switch (branch.mode)
                {
                    case GroupStep.CompleteWhen.AllChildrenComplete:
                        satisfied = completed >= total;
                        break;

                    case GroupStep.CompleteWhen.AnyChildCompletes:
                        satisfied = completed > 0;
                        break;

                    case GroupStep.CompleteWhen.SpecificChildCompletes:
                        if (!string.IsNullOrEmpty(branch.specificStepGuid))
                        {
                            for (int i = 0; i < handles.Count; i++)
                            {
                                var h = handles[i];
                                if (h != null && h.completed && h.guid == branch.specificStepGuid)
                                { satisfied = true; break; }
                            }
                        }
                        break;

                    case GroupStep.CompleteWhen.RequiredChildrenComplete:
                    {
                        int reqTotal = 0, reqDone = 0;
                        for (int i = 0; i < handles.Count; i++)
                        {
                            var h = handles[i];
                            if (h == null) continue;
                            bool req = GroupStep.IsChildRequiredInList(branch.childRequirements, h.guid);
                            if (req) reqTotal++;
                            if (req && h.completed) reqDone++;
                        }
                        satisfied = reqTotal == 0 || reqDone >= reqTotal;
                        break;
                    }

                    case GroupStep.CompleteWhen.NOfMChildrenComplete:
                        satisfied = completed >= Mathf.Clamp(branch.requiredCount, 1, total);
                        break;
                }

                if (satisfied)
                {
                    _groupExitBranchResolved = true;
                    _groupExitNextGuid = FallbackGuid(branch.nextGuid);
                    return true;
                }
            }

            return false;
        }

        GroupChildHandle StartGroupChild(Step st, GroupCancelToken token, GroupStep parentGroup)
        {
            var h = new GroupChildHandle { step = st, guid = st.guid };

            IEnumerator routine = null;
            Action cleanup = null;

            System.Action<string> onBranchExit = null;
            if (parentGroup != null &&
                parentGroup.completeWhen == GroupStep.CompleteWhen.SpecificChildCompletes &&
                !string.IsNullOrEmpty(parentGroup.specificStepGuid) &&
                st != null &&
                st.guid == parentGroup.specificStepGuid)
            {
                onBranchExit = next =>
                {
                    _groupExitBranchResolved = true;
                    _groupExitNextGuid = next ?? "";
                };
            }

            if (st is TimelineStep tl)
            {
                routine = RunTimelineGroup(tl, token);
                cleanup = () => { if (tl.director) tl.director.Stop(); };
            }
            else if (st is CueCardsStep cc)
            {
                routine = RunCueCardsGroup(cc, token);
                cleanup = () =>
                {
                    if (cc.cards != null) foreach (var card in cc.cards) SafeSet(card, false);
                    if (cc.extraObject) SafeSet(cc.extraObject, false);
                    if (cc.tapHint) SafeSet(cc.tapHint, false);
                };
            }
            else if (st is QuestionStep q)
            {
                routine = RunQuestionGroup(q, token, onBranchExit);
                cleanup = () => { if (q.panelRoot) HidePanelRoot(q.panelRoot); };
            }
            else if (st is ConditionsStep cnd)
            {
                routine = RunConditionsGroup(cnd, token, onBranchExit);
            }
            else if (st is SelectionStep sel)
            {
                routine = RunSelectionGroup(sel, token);
                cleanup = () =>
                {
                    if (sel.panelRoot) HidePanelRoot(sel.panelRoot);
                    if (sel.hint) SafeSet(sel.hint, false);
                    var lists = sel.lists != null ? sel.lists : selectionLists;
                    if (lists != null && lists.selectables != null) SetGroupPicking(lists.selectables, false);
                };
            }
            else if (st is MiniQuizStep mq)
            {
                routine = RunMiniQuizGroup(mq, token);
                cleanup = () => { if (mq.panelRoot) HidePanelRoot(mq.panelRoot); };
            }
            else if (st is InsertStep ins)
            {
                routine = RunInsertGroup(ins, token);
            }
            else if (st is EventStep ev)
            {
                routine = RunEventGroup(ev, token);
            }
            else if (st is GroupStep innerG)
            {
                routine = RunGroupInternal(innerG, token);
            }

            h.cancel = () => cleanup?.Invoke();

            if (routine != null)
            {
                h.coroutine = StartCoroutine(WrapGroupChild(routine, () => h.completed = true));
            }
            else
            {
                h.completed = true;
            }

            return h;
        }

        IEnumerator RunMiniQuizGroup(MiniQuizStep s, GroupCancelToken token)
        {
            if (s == null) yield break;
            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            UnityAction submitCb = null;
            int correct = 0;
            int qCount = s.questions != null ? s.questions.Count : 0;
            var answered = new bool[Mathf.Max(0, qCount)];
            bool done = false;

            bool AllAnswered()
            {
                for (int i = 0; i < answered.Length; i++)
                    if (!answered[i]) return false;
                return true;
            }

            if (s.questions != null)
            {
                for (int qi = 0; qi < s.questions.Count; qi++)
                {
                    int qIndex = qi;
                    var q = s.questions[qIndex];
                    if (q == null || q.choices == null) continue;
                    for (int ci = 0; ci < q.choices.Count; ci++)
                    {
                        var ch = q.choices[ci];
                        if (ch == null || ch.button == null) continue;

                        UnityAction fn = () =>
                        {
                            if (answered[qIndex] && s.lockQuestionAfterAnswer) return;
                            if (!answered[qIndex])
                            {
                                answered[qIndex] = true;
                                if (ch.isCorrect) correct++;
                            }

                            ch.onSelected?.Invoke();
                            ApplyEffects(ch.effects);

                            if (s.lockQuestionAfterAnswer)
                            {
                                if (q.choices != null)
                                    foreach (var other in q.choices)
                                        if (other != null && other.button != null)
                                            other.button.interactable = false;
                            }

                            if (s.completion == MiniQuizStep.CompleteMode.AutoWhenAllAnswered && AllAnswered())
                                done = true;
                        };

                        ch.button.onClick.AddListener(fn);
                        wired.Add((ch.button, fn));
                    }
                }
            }

            if (s.completion == MiniQuizStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => done = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            while (!done && !token.Cancelled)
                yield return null;

            foreach (var (btn, fn) in wired)
                if (btn) btn.onClick.RemoveListener(fn);
            if (submitCb != null && s.submitButton) s.submitButton.onClick.RemoveListener(submitCb);

            if (s.panelRoot) s.panelRoot.gameObject.SetActive(false);

            if (!token.Cancelled)
                yield return WaitForPointerRelease();
        }

        IEnumerator WrapGroupChild(IEnumerator routine, Action onDone)
        {
            yield return routine;
            onDone?.Invoke();
        }

        IEnumerator RunTimelineGroup(TimelineStep tl, GroupCancelToken token)
        {
            var d = tl.director;
            if (!d) yield break;

            if (tl.rewindOnEnter)
            {
                d.time = 0;
                d.Evaluate();
            }

            d.Play();
            yield return null;

            if (!tl.waitForEnd) yield break;

            bool done = false;
            void OnStopped(PlayableDirector _) => done = true;
            d.stopped += OnStopped;

            const double Eps = 1e-3;
            while (!done && !token.Cancelled)
            {
                if (d.duration > 0 &&
                    d.extrapolationMode != DirectorWrapMode.Loop &&
                    d.time >= d.duration - Eps)
                    done = true;

                if (d.state != PlayState.Playing)
                    done = true;

                yield return null;
            }

            d.stopped -= OnStopped;
        }

        IEnumerator RunCueCardsGroup(CueCardsStep cc, GroupCancelToken token)
        {
            var cards = cc.cards;
            if (cards == null || cards.Length == 0) yield break;

            // Advance input
            bool useButton = cc.advanceMode == CueCardsStep.AdvanceMode.OnButton && cc.nextButton != null;
            bool advanceRequested = false;
            UnityAction nextCb = null;
            if (cc.advanceMode == CueCardsStep.AdvanceMode.OnButton && cc.nextButton == null)
                Debug.LogWarning("[Scenario] CueCardsStep (Group): Advance Mode is OnButton but Next Button is not assigned. Falling back to TapAnywhere.", _console);
            if (useButton)
            {
                nextCb = () => advanceRequested = true;
                cc.nextButton.onClick.AddListener(nextCb);
            }

            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            var d = cc.director;
            if (d && d.state != PlayState.Playing) d.Play();

            yield return WaitForPointerRelease();

            int cur = cc.autoShowFirst ? 0 : -1;
            if (cur == 0) SafeSet(cards[cur], true);

            while (!token.Cancelled)
            {
                if (cur < 0)
                {
                    if (!useButton)
                    {
                        yield return WaitForCleanClick();
                    }
                    else
                    {
                        while (!advanceRequested && !token.Cancelled)
                            yield return null;
                        advanceRequested = false;
                    }
                    if (token.Cancelled) break;
                    cur = 0;
                    SafeSet(cards[cur], true);
                }

                float timeout = 0f;
                if (cc.cueTimes != null && cc.cueTimes.Length > 0)
                    timeout = (cc.cueTimes.Length == 1) ? cc.cueTimes[0]
                              : (cur < cc.cueTimes.Length ? cc.cueTimes[cur] : 0f);

                float t = 0f;
                while (!token.Cancelled)
                {
                    if (!useButton)
                    {
                        if (JustClicked()) break;
                    }
                    else if (advanceRequested)
                    {
                        advanceRequested = false;
                        break;
                    }
                    if (timeout > 0f)
                    {
                        if (d && d.state != PlayState.Playing) break;
                        t += Time.deltaTime;
                        if (t >= timeout) break;
                    }
                    yield return null;
                }
                if (token.Cancelled) break;

                if (!useButton)
                    yield return WaitForPointerRelease();

                SafeSet(cards[cur], false);
                if (cur >= cards.Length - 1) break;
                cur++;
                SafeSet(cards[cur], true);
            }

            for (int i = 0; i < cards.Length; i++) SafeSet(cards[i], false);

            if (useButton && cc.nextButton && nextCb != null)
                cc.nextButton.onClick.RemoveListener(nextCb);
        }

        IEnumerator RunQuestionGroup(QuestionStep q, GroupCancelToken token, System.Action<string> onBranchExit)
        {
            if (q.panelRoot) q.panelRoot.gameObject.SetActive(true);
            if (q.panelAnimator && !string.IsNullOrEmpty(q.showTrigger))
                q.panelAnimator.SetTrigger(q.showTrigger);

            var wired = new List<(Button btn, UnityAction fn)>();
            string nextGuid = null;

            if (q.choices != null)
            {
                for (int i = 0; i < q.choices.Count; i++)
                {
                    int idx = i;
                    var choice = q.choices[idx];
                    if (choice == null || choice.button == null) continue;

                    UnityAction fn = () =>
                    {
                        if (nextGuid != null) return;
                        choice.onSelected?.Invoke();
                        ApplyEffects(choice.effects);
                        nextGuid = FallbackGuid(choice.nextGuid);
                        if (q.panelAnimator && !string.IsNullOrEmpty(q.hideTrigger))
                            q.panelAnimator.SetTrigger(q.hideTrigger);
                        else if (q.panelRoot)
                            q.panelRoot.gameObject.SetActive(false);
                    };

                    choice.button.onClick.AddListener(fn);
                    wired.Add((choice.button, fn));
                }
            }

            while (nextGuid == null && !token.Cancelled)
                yield return null;

            foreach (var (btn, fn) in wired)
                if (btn) btn.onClick.RemoveListener(fn);

            if (q.panelRoot) q.panelRoot.gameObject.SetActive(false);

            if (nextGuid != null)
                onBranchExit?.Invoke(nextGuid);

            if (!token.Cancelled && nextGuid != null)
                yield return WaitForPointerRelease();
        }

        IEnumerator RunConditionsGroup(ConditionsStep cnd, GroupCancelToken token, System.Action<string> onBranchExit)
        {
            if (token.Cancelled)
                yield break;

            string nextGuid = null;

#if UNITY_EDITOR
            if (_editorSkip)
            {
                var outcomes = cnd?.outcomes;
                if (_editorSkipBranchIndex >= 0 &&
                    outcomes != null &&
                    _editorSkipBranchIndex < outcomes.Count)
                {
                    var o = outcomes[_editorSkipBranchIndex];
                    if (o != null)
                        nextGuid = o.nextGuid;
                }
                if (nextGuid == null)
                    nextGuid = "";
                onBranchExit?.Invoke(FallbackGuid(nextGuid));
                yield break;
            }
#endif

            if (cnd == null)
            {
                onBranchExit?.Invoke(FallbackGuid(""));
                yield break;
            }

            var oc = cnd.outcomes;
            if (oc == null || oc.Count == 0)
            {
                onBranchExit?.Invoke(FallbackGuid(""));
                yield break;
            }

            float value = GetConditionValue(cnd);
            nextGuid = null;
            foreach (var o in oc)
            {
                if (o == null) continue;
                if (ConditionsEvaluator.EvalCompare(value, o.compareOp, o.compareValue))
                {
                    nextGuid = o.nextGuid;
                    break;
                }
            }
            if (nextGuid == null)
                nextGuid = "";
            onBranchExit?.Invoke(FallbackGuid(nextGuid));
        }

        IEnumerator RunSelectionGroup(SelectionStep s, GroupCancelToken token)
        {
            var lists = s.lists != null ? s.lists : selectionLists;
            if (lists == null)
            {
                Debug.LogWarning("[Scenario] SelectionStep (Group): no SelectionLists assigned.", _console);
                yield break;
            }

            if (s.panelRoot) s.panelRoot.gameObject.SetActive(true);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.showTrigger))
                s.panelAnimator.SetTrigger(s.showTrigger);
            if (s.hint) s.hint.SetActive(true);

            int active = -1;
            if (!string.IsNullOrEmpty(s.listKey))
                active = lists.ShowList(s.listKey, s.resetOnEnter);
            else
                active = lists.ShowList(s.listIndex, s.resetOnEnter);

            if (lists.selectables != null)
                SetGroupPicking(lists.selectables, true);

            bool submitted = false;
            UnityAction submitCb = null;
            if (s.completion == SelectionStep.CompleteMode.OnSubmitButton && s.submitButton)
            {
                submitCb = () => submitted = true;
                s.submitButton.onClick.AddListener(submitCb);
            }

            bool done = false;
            bool isCorrect = false;
            float t = 0f;

            while (!done && !token.Cancelled)
            {
                if (active < 0)
                {
                    isCorrect = false;
                    break;
                }

                if (s.timeoutSeconds > 0f)
                {
                    t += Time.deltaTime;
                    if (t >= s.timeoutSeconds)
                    {
                        isCorrect = false;
                        break;
                    }
                }

                var e = lists.EvaluateActive();
                bool countOK = s.requireExactCount
                    ? (e.selectedTotal == s.requiredSelections)
                    : (e.selectedTotal >= s.requiredSelections);

                if (s.completion == SelectionStep.CompleteMode.AutoWhenRequirementMet)
                {
                    if (countOK)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = wrongOK;
                        done = true;
                    }
                }
                else
                {
                    if (submitted)
                    {
                        bool wrongOK = e.selectedWrong <= s.allowedWrong;
                        isCorrect = countOK && wrongOK;
                        done = true;
                    }
                }

                yield return null;
            }

            if (submitCb != null && s.submitButton)
                s.submitButton.onClick.RemoveListener(submitCb);

            if (lists.selectables != null)
                SetGroupPicking(lists.selectables, false);

            if (s.hint) s.hint.SetActive(false);
            if (s.panelAnimator && !string.IsNullOrEmpty(s.hideTrigger))
                s.panelAnimator.SetTrigger(s.hideTrigger);
            else if (s.panelRoot)
                s.panelRoot.gameObject.SetActive(false);

            if (token.Cancelled)
                yield break;

            try
            {
                if (isCorrect) s.onCorrect?.Invoke();
                else s.onWrong?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, _console);
            }

            if (isCorrect) ApplyEffects(s.onCorrectEffects);
            else ApplyEffects(s.onWrongEffects);

            yield return WaitForPointerRelease();
        }

        IEnumerator RunInsertGroup(InsertStep ins, GroupCancelToken token)
        {
            if (ins == null || ins.item == null || ins.targetTrigger == null)
            {
                Debug.LogWarning("[Scenario] InsertStep requires Item and TargetTrigger.", _console);
                yield break;
            }

            SafeSet(ins.item.gameObject, true);
            SafeSet(ins.targetTrigger.gameObject, true);

            yield return WaitForPointerRelease();

            var itemColliders = ins.item.GetComponentsInChildren<Collider>();
            if (itemColliders == null || itemColliders.Length == 0)
            {
                Debug.LogWarning("[Scenario] InsertStep: Item has no Colliders. Completing immediately.", _console);
            }
            else
            {
                bool hit = false;
                while (!hit && !token.Cancelled)
                {
                    if (!ins.targetTrigger)
                        yield break;

                    foreach (var col in itemColliders)
                    {
                        if (!col) continue;
                        if (AreCollidersOverlapping(col, ins.targetTrigger))
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (!hit)
                        yield return null;
                }
            }

            if (token.Cancelled)
                yield break;

            var body = ins.item.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                StopBodyMotion(body);
            }

            if (ins.smoothAttach)
            {
                Transform targetPose =
                    ins.attachTransform != null
                        ? ins.attachTransform
                        : ins.targetTrigger.transform;

                if (targetPose != null)
                {
                    if (body != null)
                        body.isKinematic = true;

                    if (ins.parentToAttach)
                        ins.item.SetParent(targetPose, true);

                    while (!token.Cancelled)
                    {
                        ins.item.position = Vector3.MoveTowards(
                            ins.item.position,
                            targetPose.position,
                            ins.moveSpeed * Time.deltaTime
                        );

                        ins.item.rotation = Quaternion.Slerp(
                            ins.item.rotation,
                            targetPose.rotation,
                            ins.rotateSpeed * Time.deltaTime
                        );

                        float posDist = Vector3.Distance(ins.item.position, targetPose.position);
                        float ang = Quaternion.Angle(ins.item.rotation, targetPose.rotation);

                        if (posDist < 0.01f && ang < 1f)
                            break;

                        yield return null;
                    }
                }
            }

            if (!token.Cancelled)
                yield return WaitForPointerRelease();
        }

        IEnumerator RunEventGroup(EventStep ev, GroupCancelToken token)
        {
            if (ev == null)
                yield break;

            try
            {
                ev.onEnter?.Invoke();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, _console);
            }

            float wait = Mathf.Max(0f, ev.waitSeconds);
            if (wait > 0f)
            {
                float t = 0f;
                while (t < wait && !token.Cancelled)
                {
#if UNITY_EDITOR
                    if (_editorSkip)
                        break;
#endif
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }



        static bool AreCollidersOverlapping(Collider a, Collider b)
        {
            if (!a || !b) return false;

            // Exact overlap check - essentially OnTriggerEnter but with polling
            return Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out _
            );
        }
    }
}
