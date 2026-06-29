#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Pitech.XR.Scenario;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using Pitech.XR.Interactables;

// Avoid Button clash (UGUI vs UIElements)
using UGUIButton = UnityEngine.UI.Button;
using UIEButton = UnityEngine.UIElements.Button;

public partial class ScenarioGraphWindow
{
    // ======== Node (with “Edit…” button & working edge connectors) ========
    class StepNode : Node
    {
        readonly ScenarioGraphWindow owner;
        public readonly Scenario scenario;
        public readonly Step step;
        public readonly int index;

        public Port inPort;
        public Port outNext;
        public List<Port> outChoices;
        public Port outCorrect;
        public Port outWrong;


        readonly ScenarioGraphView graph;
        readonly Action rebuild;
        readonly Action<Step, int> skipRequest;
        readonly Action<Step> deleteRequest;
        readonly Action<Step> duplicateRequest;

        bool _isActive;
        public bool IsNested { get; }
        public string ParentGroupGuid { get; }
        Foldout _foldout;
        public bool IsExpanded => _foldout != null && _foldout.value;
        public bool GroupSettingsExpanded => _foldout == null ? true : _foldout.value;
        bool _resizeQueued;
        Label _groupSummaryLabel;
        UIEButton _groupFoldBtn;
        VisualElement _resizeHandle;

        // Manual node size from the scenario's editor-only side-table (zero => auto-size).
        public Vector2 UserSize =>
            scenario != null ? (scenario.FindStepGraphDisplay(step?.guid)?.size ?? Vector2.zero) : Vector2.zero;

        // True once the user has manually resized this node (size persisted in the side-table).
        // Group/nested nodes keep their automatic sizing.
        public bool UserSized
        {
            get
            {
                if (IsNested || step == null || step is GroupStep) return false;
                var sz = UserSize;
                return sz.x > 1f && sz.y > 1f;
            }
        }

        public void SetExpanded(bool on)
        {
            if (IsNested) return;
            if (_foldout == null) return;
            _foldout.value = on;
        }

        // Fixed header prefix: the number + type (e.g. "01. Question").
        // The custom name lives in a separate, always-editable field in the header.
        string TitlePrefix() => $"{index:00}. {step.Kind}";

        // Derived, DISPLAY-ONLY node summary (computed from Kind + key params; never stored,
        // never mutates the step or the editor-only side-table). Appended to the node title for
        // at-a-glance readability. GroupStep carries its own _groupSummaryLabel, so it returns "".
        static string NodeSummary(Step s)
        {
            switch (s)
            {
                case QuestionStep q:    return q.choices != null ? $"  ·  {q.choices.Count} choice(s)" : "";
                case MiniQuizStep mq:   return $"  ·  {(mq.questions?.Count ?? 0)} Q, {(mq.outcomes?.Count ?? 0)} outcome(s)";
                case ConditionsStep c:  return c.outcomes != null ? $"  ·  {c.outcomes.Count} outcome(s)" : "";
                case SelectionStep se:  return !string.IsNullOrEmpty(se.listKey) ? $"  ·  list '{se.listKey}'"
                                               : se.listIndex >= 0 ? $"  ·  list #{se.listIndex}" : "";
                case CueCardsStep cc:   return cc.cards != null ? $"  ·  {cc.cards.Length} card(s)" : "";
                case TimelineStep tl:   return tl.director != null ? $"  ·  {tl.director.name}" : "";
                default:                return "";
            }
        }

        // NOTE on Step 6 "structural colouring by GroupStep membership": nested steps are NOT
        // GraphView nodes (see the file-top note) - they render as tiles via BuildNestedTile, which
        // already gives each tile a per-Kind accent (GetStepAccent) inside the group container, so
        // membership reads structurally. No separate nested-node tint is needed or reachable here.

        public StepNode(ScenarioGraphWindow ownerWindow, Scenario sc, Step s, int idx, ScenarioGraphView gv, Action rebuildLinks, Action<Step, int> onSkipRequest, Action<Step> onDeleteRequest, Action<Step> onDuplicateRequest, bool startExpanded, bool isNested = false, string parentGroupGuid = null)
        {
            owner = ownerWindow;
            scenario = sc; step = s; index = idx; graph = gv; rebuild = rebuildLinks; skipRequest = onSkipRequest; deleteRequest = onDeleteRequest;
            duplicateRequest = onDuplicateRequest;
            IsNested = isNested;
            ParentGroupGuid = parentGroupGuid;

            // Force compact width for non-group steps (GraphView can impose a larger min-width by default).
            // If the user has manually resized this node, honor that size instead.
            if (s is not GroupStep)
            {
                if (UserSized)
                {
                    var us = UserSize;
                    style.minWidth = 120f;
                    style.maxWidth = StyleKeyword.None;
                    style.width = us.x;
                    style.minHeight = 80f;
                    style.height = us.y;
                }
                else
                {
                    style.minWidth = StepNodeWidth;
                    style.maxWidth = StepNodeWidth;
                    style.width = StepNodeWidth;
                }
            }

            title = TitlePrefix() + NodeSummary(s);
            var tbox = this.Q("title");
            var titleLabel = tbox?.Q<Label>();

            if (s is TimelineStep) tbox.style.backgroundColor = new Color(0.20f, 0.42f, 0.85f);
            if (s is CueCardsStep) tbox.style.backgroundColor = new Color(0.32f, 0.62f, 0.32f);
            if (s is QuestionStep) tbox.style.backgroundColor = new Color(0.76f, 0.45f, 0.22f);
            if (s is MiniQuizStep) tbox.style.backgroundColor = new Color(0.62f, 0.34f, 0.16f);
            // Quiz colors: red palette (distinct from Timeline blue). Keep contrast strong for white text.
            if (s is QuizStep) tbox.style.backgroundColor = new Color(0.78f, 0.20f, 0.20f);
            if (s is QuizResultsStep) tbox.style.backgroundColor = new Color(0.62f, 0.16f, 0.16f);
            if (s is SelectionStep) tbox.style.backgroundColor = new Color(0.58f, 0.38f, 0.78f);
            if (s is InsertStep)
            {
                tbox.style.backgroundColor = new Color(0.90f, 0.75f, 0.25f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;   // black text on the yellow
            }
            if (s is EventStep)
            {
                tbox.style.backgroundColor = new Color(0.25f, 0.70f, 0.70f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;   // black text on the yellow
            }
            if (s is GroupStep)
            {
                tbox.style.backgroundColor = new Color(0.55f, 0.55f, 0.60f);
            }
            if (s is ConditionsStep)
            {
                // Darker amber for better contrast with black text
                tbox.style.backgroundColor = new Color(0.70f, 0.38f, 0.08f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;
            }
            if (s is SessionStartStep || s is SessionStopStep)
            {
                // Analytics-related steps: a very distinguishing near-white header (black text).
                tbox.style.backgroundColor = new Color(0.93f, 0.93f, 0.96f);
                if (titleLabel != null)
                    titleLabel.style.color = Color.black;
            }

            // Always-editable custom name, shown in the header right after the type.
            // Type away to name the node (e.g. "01. Question" + "testing path"); empty = type only.
            if (tbox != null)
            {
                bool darkHeaderText = s is InsertStep || s is EventStep || s is ConditionsStep || s is SessionStartStep || s is SessionStopStep;
                var headerName = new TextField { isDelayed = false, value = sc.FindStepGraphDisplay(s.guid)?.displayName ?? "" };
                headerName.style.flexGrow = 1;
                headerName.style.marginLeft = 6;
                headerName.style.marginRight = 4;
                headerName.style.fontSize = 12;
                headerName.tooltip = "Custom name shown after the type. Leave empty for type only.";
                var hi = headerName.Q(className: "unity-text-input");
                if (hi != null)
                {
                    hi.style.backgroundColor = Color.clear;
                    hi.style.color = darkHeaderText ? Color.black : Color.white;
                }
                headerName.RegisterValueChangedCallback(evt =>
                {
                    Dirty(scenario, "Rename Step");
                    var d = scenario.GetOrAddStepGraphDisplay(step.guid);
                    if (d != null) d.displayName = evt.newValue ?? "";
                    scenario.PruneStepGraphDisplay(step.guid);
                });
                // Typing in the header must not drag the node or trigger double-click-to-edit.
                headerName.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
                tbox.Add(headerName);
            }

            // In (nested steps do not participate in routing inside the main graph)
            if (!IsNested)
            {
            inPort = MakePort(Direction.Input, Port.Capacity.Multi, "In", -1);
            inputContainer.Add(inPort);
            }

            // top-right small “Edit…” button
            var headerButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            void OpenEditor()
            {
                if (step is TimelineStep tl) StepEditWindow.OpenTimeline(scenario, tl);
                else if (step is CueCardsStep cc) StepEditWindow.OpenCueCards(scenario, cc);
                else if (step is QuestionStep q) StepEditWindow.OpenQuestion(scenario, q, rebuild);
                else if (step is MiniQuizStep mq) StepEditWindow.OpenMiniQuiz(scenario, mq, () => owner?.Load(owner.scenario));
                else if (step is QuizStep qz) StepEditWindow.OpenQuiz(scenario, qz);
                else if (step is SelectionStep se) StepEditWindow.OpenSelection(scenario, se);
                else if (step is InsertStep ins) StepEditWindow.OpenInsert(scenario, ins);
                else if (step is EventStep ev) StepEditWindow.OpenEvent(scenario, ev);
                else if (step is GroupStep g) StepEditWindow.OpenGroup(scenario, g);
                else if (step is ConditionsStep) { /* inline editing in foldout */ }
            }

            var editBtn = new UIEButton(OpenEditor) { text = "Edit…" };

            editBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            editBtn.style.marginLeft = 6;
            titleContainer.Add(editBtn);

            // Bottom-right drag handle to resize the node (non-group, non-nested only).
            if (!IsNested && s is not GroupStep)
            {
                _resizeHandle = new VisualElement();
                _resizeHandle.style.position = Position.Absolute;
                _resizeHandle.style.right = 0;
                _resizeHandle.style.bottom = 0;
                _resizeHandle.style.width = 14;
                _resizeHandle.style.height = 14;
                _resizeHandle.style.backgroundColor = new Color(1f, 1f, 1f, 0.22f);
                _resizeHandle.style.borderTopLeftRadius = 4;
                _resizeHandle.tooltip = "Drag to resize · double-click to auto-size";
                hierarchy.Add(_resizeHandle); // anchor to the node's full bounds, not the inner content container
                SetupResize();
            }

            if (s is GroupStep gs)
            {
                _groupSummaryLabel = new Label();
                _groupSummaryLabel.style.fontSize = 10;
                _groupSummaryLabel.style.color = new Color(1f, 1f, 1f, 0.7f);
                _groupSummaryLabel.style.marginTop = 2;
                _groupSummaryLabel.style.marginBottom = 2;
                mainContainer.Add(_groupSummaryLabel);
                UpdateGroupSummaryLabel(gs);
            }

            // ConditionsStep: start expanded so user immediately sees branches editor
            bool foldStartOpen = s is ConditionsStep;

            // quick inline fields foldout (kept for speed)
            var fold = new Foldout { text = "Settings", value = foldStartOpen };
            _foldout = fold;
            mainContainer.Add(fold);

            // When Details is toggled, resize the node so inline editing doesn't get clipped.
            fold.RegisterValueChangedCallback(_ =>
            {
                owner?.ScheduleResizeGroup(step is GroupStep gg ? gg.guid : null);

                // Non-group nodes: resize immediately based on deterministic height calc.
                // Skip when the user has manually sized the node — their size wins.
                if (step is not GroupStep && !UserSized)
                {
                    if (fold.value)
                    {
                        // Expand width a bit for editing comfort.
                        float w = ExpandedWidthFor(step);
                        style.minWidth = w;
                        style.maxWidth = w;
                        style.width = w;
                        // IMPORTANT: Let the node auto-size to its actual IMGUI content.
                        // This avoids brittle height calculations (especially for nested lists like Question.Choices).
                        style.minHeight = GetCollapsedHeight();
                        style.height = new StyleLength(StyleKeyword.Auto);
                    }
                    else
                    {
                        var r = GetPosition();
                        // Restore compact width + height.
                        style.minWidth = StepNodeWidth;
                        style.maxWidth = StepNodeWidth;
                        style.width = StepNodeWidth;
                        style.minHeight = GetCollapsedHeight();
                        SetPositionSilent(new Rect(r.position, new Vector2(StepNodeWidth, GetCollapsedHeight())));
                    }
                }

                // Remember state so Refresh doesn't collapse nodes.
                owner?.SetExpanded(step != null ? step.guid : null, fold.value);

                if (step is GroupStep && _groupFoldBtn != null)
                    _groupFoldBtn.text = fold.value ? "▾" : "▸";
            });

            // When inline IMGUI content expands/collapses while the foldout is open, keep sizing in sync.
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (step is GroupStep) return;
                if (_foldout == null || !_foldout.value) return;
                if (_resizeQueued) return;
                _resizeQueued = true;
                EditorApplication.delayCall += () =>
                {
                    _resizeQueued = false;
                    if (this == null) return;
                    ResizeToFitDetails();
                };
            });

            // For Groups, Unity's default collapse toggle can become unclickable depending on layout.
            // Provide our own always-visible toggle in the title bar.
            if (s is GroupStep)
            {
                var builtinCollapse = this.Q("collapse-button");
                if (builtinCollapse != null)
                    builtinCollapse.style.display = DisplayStyle.None;

                var expBtn = new UIEButton() { text = "▾" };
                _groupFoldBtn = expBtn;
                expBtn.clicked += () =>
                {
                    // Toggle foldout content, never hide the header (so you can always reopen).
                    fold.value = !fold.value;
                    expBtn.text = fold.value ? "▾" : "▸";
                    if (step is GroupStep gg)
                        owner?.ScheduleResizeGroup(gg.guid);
                };
                expBtn.style.width = 22;
                expBtn.style.height = 18;
                expBtn.style.marginLeft = 6;
                expBtn.tooltip = "Collapse/Expand Group";
                titleContainer.Insert(0, expBtn);

                // Keep node expanded; only toggle fold visibility.
                fold.value = true;
                expBtn.text = "▾";
            }
            else
            {
                // Restore previous foldout state for non-group nodes.
                if (!IsNested)
                    fold.value = startExpanded;
            }

            // Double-click to edit (especially important for nested tiles).
            RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    OpenEditor();
                    e.StopPropagation();
                }
            });

            if (IsNested)
            {
                // Nested nodes stay compact to avoid overlap; edit via the "Edit…" button (modal StepEditWindow).
                fold.visible = false;
                extensionContainer.style.display = DisplayStyle.None;
                RefreshExpandedState();
                RefreshPorts();
                return;
            }

            if (s is TimelineStep tl)
            {
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    var newDir = (PlayableDirector)EditorGUILayout.ObjectField("Director", tl.director, typeof(PlayableDirector), true);
                    tl.rewindOnEnter = EditorGUILayout.Toggle("Rewind On Enter", tl.rewindOnEnter);
                    tl.waitForEnd = EditorGUILayout.Toggle("Wait For End", tl.waitForEnd);
                    if (EditorGUI.EndChangeCheck()) { Dirty(scenario, "Edit Timeline"); tl.director = newDir; }
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is CueCardsStep cc)
            {
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    cc.director = (PlayableDirector)EditorGUILayout.ObjectField("Clock (opt)", cc.director, typeof(PlayableDirector), true);
                    cc.autoShowFirst = EditorGUILayout.Toggle("Auto Show First", cc.autoShowFirst);
                    cc.tapHint = (GameObject)EditorGUILayout.ObjectField("Tap Hint", cc.tapHint, typeof(GameObject), true);
                    cc.advanceMode = (CueCardsStep.AdvanceMode)EditorGUILayout.EnumPopup("Advance", cc.advanceMode);
                    if (cc.advanceMode == CueCardsStep.AdvanceMode.OnButton)
                        cc.nextButton = (UGUIButton)EditorGUILayout.ObjectField("Next Button", cc.nextButton, typeof(UGUIButton), true);
                    cc.extraObject = (GameObject)EditorGUILayout.ObjectField("Extra Object", cc.extraObject, typeof(GameObject), true);
                    cc.extraShowAtIndex = EditorGUILayout.IntField("Extra Show At Index", cc.extraShowAtIndex);
                    cc.hideExtraWithFinalTap = EditorGUILayout.Toggle("Hide Extra With Final Tap", cc.hideExtraWithFinalTap);
                    cc.useRenderersForExtra = EditorGUILayout.Toggle("Use Renderers For Extra", cc.useRenderersForExtra);
                    cc.fadeDuration = EditorGUILayout.FloatField("Fade Duration", cc.fadeDuration);
                    cc.popScale = EditorGUILayout.FloatField("Pop Scale", cc.popScale);
                    cc.popDuration = EditorGUILayout.FloatField("Pop Duration", cc.popDuration);
                    EditorGUILayout.HelpBox("Open “Edit…” for full Cards & Cue Times editing.", MessageType.None);
                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Cue Cards");
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is QuestionStep q)
            {
                // Use SerializedProperty so we can show nested lists (Choices + their Effects) inline.
                var so = new SerializedObject(scenario);
                var self = this;
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    so.Update();
                    var stepProp = StepEditWindow.FindStepPropertyRecursive(so, q.guid);
                    if (stepProp == null) return;

                    var choicesProp = stepProp.FindPropertyRelative("choices");
                    int beforeChoices = choicesProp != null && choicesProp.isArray ? choicesProp.arraySize : -1;

                    EditorGUI.BeginChangeCheck();

                    // Panel
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fallbackHideSeconds"));

                    EditorGUILayout.Space(6);
                    if (choicesProp != null)
                    {
                        // Shows: Button + Effects list + NextGuid per choice (all inline)
                        EditorGUILayout.PropertyField(choicesProp, new GUIContent("Choices"), includeChildren: true);
                    }
                    EditorGUILayout.Space(12); // extra bottom breathing room in the node (GraphView can clip tight bottoms)

                    if (EditorGUI.EndChangeCheck())
                    {
                        Dirty(scenario, "Edit Question");
                        so.ApplyModifiedProperties();

                        // If the user added/removed choices, rebuild output ports.
                        int afterChoices = choicesProp != null && choicesProp.isArray ? choicesProp.arraySize : -1;
                        if (beforeChoices != afterChoices)
                            RecreateChoicePorts();

                        // Expansion/collapse inside the list can change the required height; resize on next tick.
                        self?.QueueResizeToFitDetails();
                    }
                }));

                RecreateChoicePorts();
            }
            else if (s is MiniQuizStep mq)
            {
                // Use SerializedProperty so we can show nested lists (Questions + Outcomes) inline.
                var so = new SerializedObject(scenario);
                var self = this;
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    so.Update();
                    var stepProp = StepEditWindow.FindStepPropertyRecursive(so, mq.guid);
                    if (stepProp == null) return;

                    var outcomesProp = stepProp.FindPropertyRelative("outcomes");
                    int beforeOutcomes = outcomesProp != null && outcomesProp.isArray ? outcomesProp.arraySize : -1;

                    EditorGUI.BeginChangeCheck();

                    // Panel
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));

                    EditorGUILayout.Space(6);
                    var completionProp = stepProp.FindPropertyRelative("completion");
                    if (completionProp != null)
                    {
                        EditorGUILayout.PropertyField(completionProp);
                        if (completionProp.enumValueIndex == (int)MiniQuizStep.CompleteMode.OnSubmitButton)
                            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("submitButton"));
                    }
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("lockQuestionAfterAnswer"));

                    EditorGUILayout.Space(6);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("questions"), new GUIContent("Questions"), includeChildren: true);

                    EditorGUILayout.Space(6);
                    EditorGUILayout.PropertyField(outcomesProp, new GUIContent("Outcomes"), includeChildren: true);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("defaultNextGuid"), new GUIContent("Default Next Guid"));

                    EditorGUILayout.Space(12);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Dirty(scenario, "Edit Mini Quiz");
                        so.ApplyModifiedProperties();

                        int afterOutcomes = outcomesProp != null && outcomesProp.isArray ? outcomesProp.arraySize : -1;
                        if (beforeOutcomes != afterOutcomes)
                        {
                            // Only reload the graph when the outcomes list size changes (ports count changes).
                            // Reloading on every keypress steals IMGUI focus while typing.
                            RecreateMiniQuizOutcomePorts();
                            EditorApplication.delayCall += () =>
                            {
                                if (owner != null && owner.scenario != null)
                                    owner.Load(owner.scenario);
                            };
                        }
                        else
                        {
                            // No graph reload: just update port labels in-place so typing keeps focus.
                            UpdateMiniQuizOutcomePortLabels();
                        }

                        self?.QueueResizeToFitDetails();
                    }
                }));

                RecreateMiniQuizOutcomePorts();
            }
            else if (s is SelectionStep sel)
            {
                // Use SerializedProperty so we can show nested lists (stat effects + events) inline.
                var so = new SerializedObject(scenario);
                var self = this;
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    so.Update();
                    var stepProp = StepEditWindow.FindStepPropertyRecursive(so, sel.guid);
                    if (stepProp == null) return;

                    EditorGUI.BeginChangeCheck();

                    // Source
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("lists"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listKey"), new GUIContent("List Name"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listIndex"), new GUIContent("(or) List Index"));

                    // Flow
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("resetOnEnter"));
                    var completionProp = stepProp.FindPropertyRelative("completion");
                    EditorGUILayout.PropertyField(completionProp);
                    if (completionProp != null && completionProp.enumValueIndex == (int)SelectionStep.CompleteMode.OnSubmitButton)
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("submitButton"));

                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Requirement", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requiredSelections"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requireExactCount"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("allowedWrong"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("timeoutSeconds"));

                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("UI (optional)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hint"));

                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Stat Effects", EditorStyles.boldLabel);
                    // These are hidden in the default inspector but still supported at runtime.
                    var correctEff = stepProp.FindPropertyRelative("onCorrectEffects");
                    var wrongEff = stepProp.FindPropertyRelative("onWrongEffects");
                    if (correctEff != null) EditorGUILayout.PropertyField(correctEff, new GUIContent("On Correct Effects"), includeChildren: true);
                    if (wrongEff != null) EditorGUILayout.PropertyField(wrongEff, new GUIContent("On Wrong Effects"), includeChildren: true);

                    EditorGUILayout.Space(6);
                    // UnityEvent drawer already renders an "Events" header, so don't duplicate it.
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onCorrect"), includeChildren: true);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onWrong"), includeChildren: true);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Dirty(scenario, "Edit Selection");
                        so.ApplyModifiedProperties();
                        // Expansion/collapse inside effects/events can change the required height.
                        self?.QueueResizeToFitDetails();
                    }
                }));



                // Two outputs
                outCorrect = MakePort(Direction.Output, Port.Capacity.Single, "Correct", -2);
                outWrong = MakePort(Direction.Output, Port.Capacity.Single, "Wrong", -3);
                outputContainer.Add(outCorrect);
                outputContainer.Add(outWrong);
            }

            else if (s is InsertStep ins)
            {
                // Inline mini-inspector for quick authoring
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();

                    ins.item = (Transform)EditorGUILayout.ObjectField("Item", ins.item, typeof(Transform), true);
                    ins.targetTrigger = (Collider)EditorGUILayout.ObjectField("Target Trigger", ins.targetTrigger, typeof(Collider), true);
                    ins.attachTransform = (Transform)EditorGUILayout.ObjectField("Attach Transform", ins.attachTransform, typeof(Transform), true);

                    EditorGUILayout.Space(4);
                    ins.smoothAttach = EditorGUILayout.Toggle("Smooth Attach", ins.smoothAttach);
                    ins.parentToAttach = EditorGUILayout.Toggle("Parent To Attach", ins.parentToAttach);
                    ins.moveSpeed = EditorGUILayout.FloatField("Move Speed", ins.moveSpeed);
                    ins.rotateSpeed = EditorGUILayout.FloatField("Rotate Speed", ins.rotateSpeed);

                    EditorGUILayout.Space(4);
                    ins.positionTolerance = EditorGUILayout.FloatField("Position Tolerance", ins.positionTolerance);
                    ins.angleTolerance = EditorGUILayout.FloatField("Angle Tolerance", ins.angleTolerance);

                    if (EditorGUI.EndChangeCheck()) Dirty(scenario, "Edit Insert");
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is EventStep ev)
            {
                // Cache SerializedObject + this step's SerializedProperty once
                var so = new SerializedObject(scenario);
                var stepsProp = so.FindProperty("steps");
                SerializedProperty stepProp = null;

                if (stepsProp != null)
                {
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        var el = stepsProp.GetArrayElementAtIndex(i);
                        var g = el.FindPropertyRelative("guid");
                        if (g != null && g.stringValue == ev.guid)
                        {
                            stepProp = el;
                            break;
                        }
                    }
                }

                // IMGUIContainer that actually draws the fields
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    if (stepProp == null) return; // nothing to draw

                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    var onEnterProp = stepProp.FindPropertyRelative("onEnter");
                    var waitProp = stepProp.FindPropertyRelative("waitSeconds");

                    if (onEnterProp != null)
                        EditorGUILayout.PropertyField(onEnterProp, new GUIContent("On Enter Events"));

                    if (waitProp != null)
                        EditorGUILayout.PropertyField(waitProp, new GUIContent("Wait Seconds Before Next"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        Dirty(scenario, "Edit Event");
                    }
                }));

                // Normal Next output like the other linear steps
                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is SessionStartStep || s is SessionStopStep)
            {
                // WS B1.4 graded-bracket markers. Linear single-output steps (only nextGuid), so they
                // render and route exactly like the other linear steps (Timeline/Event): one "Next" port
                // the edge writes back through, and the runner honours nextGuid.
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    EditorGUILayout.HelpBox(
                        s is SessionStartStep
                            ? "Marks the START of the graded session bracket. Connect 'Next' to the first graded step."
                            : "Marks the END of the graded session bracket. Connect 'Next' to continue.",
                        MessageType.None);
                }));

                outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                outputContainer.Add(outNext);
            }
            else if (s is ConditionsStep cnd)
            {
                var so = new SerializedObject(scenario);
                var self = this;
                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    so.Update();
                    var stepProp = StepEditWindow.FindStepPropertyRecursive(so, cnd.guid);
                    if (stepProp == null) return;

                    var outcomesProp = stepProp.FindPropertyRelative("outcomes");
                    int beforeOutcomes = outcomesProp != null && outcomesProp.isArray ? outcomesProp.arraySize : -1;

                    EditorGUI.BeginChangeCheck();

                    var labelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
                    var boldLabel = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.95f, 0.95f) } };

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Check (one value)", boldLabel);
                    EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("valueSource"), new GUIContent("Value Source",
                        "Where we read one number to compare.\n\n" +
                        "• Stat — scenario stats; set Stat Key.\n" +
                        "• Component — one public field on a script (e.g. score).\n" +
                        "• List by label — find a row in a list by name, then read a number from that row."), true);
                    var vs = stepProp.FindPropertyRelative("valueSource");
                    if (vs != null && vs.enumValueIndex == 0)
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("statKey"), new GUIContent("Stat Key",
                            "Only for Stat. The stat name to read (same spelling as elsewhere), e.g. Health or Money."), true);
                    else if (vs != null && vs.enumValueIndex == 2)
                    {
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("source"), new GUIContent("Component",
                            "Required. The script that has the list — drag the object and pick the component (e.g. your counters script)."), true);
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listFieldName"), new GUIContent("List Field",
                            "Required. Exact name of the public field or property on that script that is the list or array (e.g. counters)."), true);
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listEntryLabel"), new GUIContent("Match Label",
                            "Required. The text we search for. We pick the row whose label text equals this exactly (e.g. EmergencyCount)."), true);
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listLabelFieldName"), new GUIContent("Row Label Field",
                            "Usually leave as label. On each row, the name of the text field we compare to Match Label. Change only if your row uses another field name."), true);
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listValueFieldName"), new GUIContent("Row Value Field",
                            "Usually leave as count. On each row, the name of the number we read for the condition. Change only if your row uses another field name."), true);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("source"), new GUIContent("Component",
                            "The script with the value — drag the object and pick the component."), true);
                        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("memberName"), new GUIContent("Member",
                            "Required for Component. Exact name of one public field or property (e.g. score). Must be a single number, not a list."), true);
                    }
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Outcomes (add routes)", boldLabel);
                    if (outcomesProp != null)
                        EditorGUILayout.PropertyField(outcomesProp, new GUIContent(""), includeChildren: true);
                    EditorGUILayout.Space(12);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Dirty(scenario, "Edit Conditions");
                        so.ApplyModifiedProperties();

                        int afterOutcomes = outcomesProp != null && outcomesProp.isArray ? outcomesProp.arraySize : -1;
                        if (beforeOutcomes != afterOutcomes)
                            RecreateConditionOutcomePorts();
                        else
                            UpdateConditionOutcomePortLabels();
                        self?.QueueResizeToFitDetails();
                    }
                }));

                RecreateConditionOutcomePorts();
            }
            else if (s is QuizStep qz)
            {
                var so = new SerializedObject(scenario);
                var stepsProp = so.FindProperty("steps");
                SerializedProperty stepProp = null;

                if (stepsProp != null)
                {
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        var el = stepsProp.GetArrayElementAtIndex(i);
                        var g = el.FindPropertyRelative("guid");
                        if (g != null && g.stringValue == qz.guid)
                        {
                            stepProp = el;
                            break;
                        }
                    }
                }

                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    if (stepProp == null) return;

                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    var quizProp = stepProp.FindPropertyRelative("quiz");
                    var idProp = stepProp.FindPropertyRelative("questionId");
                    var idxProp = stepProp.FindPropertyRelative("questionIndex");
                    var submitModeProp = stepProp.FindPropertyRelative("submitMode");
                    var feedbackProp = stepProp.FindPropertyRelative("feedback");
                    var feedbackSecondsProp = stepProp.FindPropertyRelative("feedbackSeconds");
                    var completionProp = stepProp.FindPropertyRelative("completion");

                    int beforeMode = completionProp != null ? completionProp.enumValueIndex : 0;

                    if (quizProp != null)
                        EditorGUILayout.PropertyField(quizProp, new GUIContent("Quiz Asset"));

                    // Question picker
                    if (quizProp != null && idProp != null)
                    {
                        var quizObj = quizProp.objectReferenceValue;
                        if (quizObj != null)
                        {
                            var soQuiz = new SerializedObject(quizObj);
                            var questions = soQuiz.FindProperty("questions");
                            if (questions == null || !questions.isArray || questions.arraySize == 0)
                                return;

                            var labels = new List<string> { "Pick question…" };
                            var ids = new List<string> { "" };
                            for (int i = 0; i < questions.arraySize; i++)
                            {
                                var qq = questions.GetArrayElementAtIndex(i);
                                if (qq == null) continue;
                                var id = qq.FindPropertyRelative("id")?.stringValue ?? "";
                                var prompt = qq.FindPropertyRelative("prompt")?.stringValue ?? "";
                                var ptxt = !string.IsNullOrWhiteSpace(prompt)
                                    ? (prompt.Length > 28 ? prompt.Substring(0, 28) + "…" : prompt)
                                    : "(No prompt)";
                                string label = $"{i + 1}. {ptxt}";
                                labels.Add(label);
                                ids.Add(id);
                            }
                            int cur = Mathf.Max(0, ids.IndexOf(idProp.stringValue));

                            // Custom dark dropdown (GraphView IMGUI popups can render with a very bright background).
                            Rect rr = EditorGUILayout.GetControlRect();
                            rr = EditorGUI.PrefixLabel(rr, new GUIContent("Question"));
                            rr.height = EditorGUIUtility.singleLineHeight;

                            var bg = new Color(0.16f, 0.16f, 0.16f, 1f);
                            var border = new Color(0.28f, 0.28f, 0.28f, 1f);
                            EditorGUI.DrawRect(rr, bg);
                            EditorGUI.DrawRect(new Rect(rr.x, rr.y, rr.width, 1f), border);
                            EditorGUI.DrawRect(new Rect(rr.x, rr.yMax - 1f, rr.width, 1f), border);
                            EditorGUI.DrawRect(new Rect(rr.x, rr.y, 1f, rr.height), border);
                            EditorGUI.DrawRect(new Rect(rr.xMax - 1f, rr.y, 1f, rr.height), border);

                            string shown = (cur <= 0 || cur >= labels.Count) ? "Pick question…" : labels[cur];
                            var textStyle = new GUIStyle(EditorStyles.label)
                            {
                                alignment = TextAnchor.MiddleLeft,
                                clipping = TextClipping.Clip,
                                padding = new RectOffset(8, 18, 0, 0)
                            };
                            textStyle.normal.textColor = Color.white;
                            GUI.Label(rr, shown, textStyle);

                            var arrowStyle = new GUIStyle(EditorStyles.label)
                            {
                                alignment = TextAnchor.MiddleRight,
                                padding = new RectOffset(8, 8, 0, 0)
                            };
                            arrowStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
                            GUI.Label(rr, "▾", arrowStyle);

                            if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                            {
                                var menu = new GenericMenu();
                                for (int i = 0; i < labels.Count; i++)
                                {
                                    int ii = i;
                                    menu.AddItem(new GUIContent(labels[ii]), ii == cur, () =>
                                    {
                                        so.Update();
                                        idProp.stringValue = ids[Mathf.Clamp(ii, 0, ids.Count - 1)];
                                        if (idxProp != null) idxProp.intValue = ii > 0 ? ii - 1 : -1;
                                        so.ApplyModifiedProperties();
                                        Dirty(scenario, "Edit Quiz");
                                    });
                                }
                                menu.DropDown(rr);
                            }
                        }
                    }

                    if (completionProp != null)
                        EditorGUILayout.PropertyField(completionProp, new GUIContent("Routing"));

                    if (submitModeProp != null)
                        EditorGUILayout.PropertyField(submitModeProp, new GUIContent("Submit"));

                    if (feedbackProp != null)
                    {
                        EditorGUILayout.PropertyField(feedbackProp, new GUIContent("Explanation"));
                        if (feedbackProp.enumValueIndex == (int)QuizStep.FeedbackMode.ForSeconds && feedbackSecondsProp != null)
                            EditorGUILayout.PropertyField(feedbackSecondsProp, new GUIContent("Explanation Seconds"));
                        if (feedbackProp.enumValueIndex == (int)QuizStep.FeedbackMode.UntilContinue)
                            EditorGUILayout.HelpBox("Requires the Quiz Panel Continue button (recommended).", MessageType.Info);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        Dirty(scenario, "Edit Quiz");

                        int afterMode = completionProp != null ? completionProp.enumValueIndex : 0;
                        if (beforeMode != afterMode)
                        {
                            EditorApplication.delayCall += () =>
                            {
                                if (owner != null && owner.scenario != null)
                                    owner.Load(owner.scenario);
                            };
                        }
                    }
                }));

                if (qz.completion == QuizStep.CompleteMode.BranchOnCorrectness)
                {
                    outCorrect = MakePort(Direction.Output, Port.Capacity.Single, "Correct", -2);
                    outWrong = MakePort(Direction.Output, Port.Capacity.Single, "Wrong", -3);
                    outputContainer.Add(outCorrect);
                    outputContainer.Add(outWrong);
                }
                else
                {
                    outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                    outputContainer.Add(outNext);
                }
            }
            else if (s is QuizResultsStep qrs)
            {
                var so = new SerializedObject(scenario);
                var stepsProp = so.FindProperty("steps");
                SerializedProperty stepProp = null;

                if (stepsProp != null)
                {
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        var el = stepsProp.GetArrayElementAtIndex(i);
                        var g = el.FindPropertyRelative("guid");
                        if (g != null && g.stringValue == qrs.guid)
                        {
                            stepProp = el;
                            break;
                        }
                    }
                }

                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    if (stepProp == null) return;

                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    var quizProp = stepProp.FindPropertyRelative("quiz");
                    var completionProp = stepProp.FindPropertyRelative("completion");
                    var whenCompleteProp = stepProp.FindPropertyRelative("whenComplete");
                    var afterSecondsProp = stepProp.FindPropertyRelative("completeAfterSeconds");

                    int beforeMode = completionProp != null ? completionProp.enumValueIndex : 0;

                    if (quizProp != null)
                        EditorGUILayout.PropertyField(quizProp, new GUIContent("Quiz Asset"));

                    if (whenCompleteProp != null)
                    {
                        EditorGUILayout.PropertyField(whenCompleteProp, new GUIContent("When Complete"));
                        if (whenCompleteProp.enumValueIndex == (int)QuizResultsStep.WhenComplete.AfterSeconds && afterSecondsProp != null)
                            EditorGUILayout.PropertyField(afterSecondsProp, new GUIContent("Complete After Seconds"));
                    }

                    if (completionProp != null)
                        EditorGUILayout.PropertyField(completionProp, new GUIContent("Routing"));

                    EditorGUILayout.HelpBox(
                        "Use this after a sequence of Quiz steps to show score/correct/wrong/pass.\n" +
                        "Pass/Fail uses QuizAsset.passThresholdPercent.",
                        MessageType.Info);

                    if (EditorGUI.EndChangeCheck())
                    {
                        so.ApplyModifiedProperties();
                        Dirty(scenario, "Edit Quiz Results");

                        int afterMode = completionProp != null ? completionProp.enumValueIndex : 0;
                        if (beforeMode != afterMode)
                        {
                            EditorApplication.delayCall += () =>
                            {
                                if (owner != null && owner.scenario != null)
                                    owner.Load(owner.scenario);
                            };
                        }
                    }
                }));

                if (qrs.completion == QuizResultsStep.CompleteMode.BranchOnPassed)
                {
                    outCorrect = MakePort(Direction.Output, Port.Capacity.Single, "Passed", -2);
                    outWrong = MakePort(Direction.Output, Port.Capacity.Single, "Failed", -3);
                    outputContainer.Add(outCorrect);
                    outputContainer.Add(outWrong);
                }
                else
                {
                    outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                    outputContainer.Add(outNext);
                }
            }
            else if (s is GroupStep g)
            {
                // Make it feel like a container: settings on top + a visible drop zone area below.
                fold.text = "Group Settings";
                fold.value = true;
                fold.RegisterValueChangedCallback(_ =>
                {
                    owner?.ScheduleResizeGroup(g.guid);
                });

                fold.contentContainer.Add(new IMGUIContainer(() =>
                {
                    int beforeCompleteWhen = (int)g.completeWhen;
                    string beforeSpecific = g.specificStepGuid ?? "";
                    bool beforeProxy = GroupUsesProxyBranchPorts(g);
                    int beforeMcCount = g.multiConditionBranches != null ? g.multiConditionBranches.Count : 0;

                    EditorGUI.BeginChangeCheck();
                    g.completeWhen = (GroupStep.CompleteWhen)EditorGUILayout.EnumPopup("Complete When", g.completeWhen);

                    int count = g.steps != null ? g.steps.Count : 0;

                    if (g.completeWhen == GroupStep.CompleteWhen.NOfMChildrenComplete)
                    {
                        g.requiredCount = Mathf.Max(1, EditorGUILayout.IntField("Required Count", g.requiredCount));
                        if (count > 0 && g.requiredCount > count) g.requiredCount = count;
                    }
                    else if (g.completeWhen == GroupStep.CompleteWhen.SpecificChildCompletes)
                    {
                        // Pick from nested steps (numbered) instead of typing GUID
                        if (g.steps != null && g.steps.Count > 0)
                        {
                            var options = new List<string> { "None" };
                            var guids = new List<string> { "" };
                            for (int i = 0; i < g.steps.Count; i++)
                            {
                                var st = g.steps[i];
                                if (st == null || string.IsNullOrEmpty(st.guid)) continue;
                                options.Add($"{i + 1}. {st.Kind}");
                                guids.Add(st.guid);
                            }

                            int cur = 0;
                            if (!string.IsNullOrEmpty(g.specificStepGuid))
                            {
                                int idx = guids.IndexOf(g.specificStepGuid);
                                if (idx >= 0) cur = idx;
                            }

                            int next = EditorGUILayout.Popup("Specific Step", cur, options.ToArray());
                            g.specificStepGuid = guids[Mathf.Clamp(next, 0, guids.Count - 1)];
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Add nested steps to the Group to select a specific completion step.", MessageType.Info);
                            g.specificStepGuid = EditorGUILayout.TextField("Specific Step Guid", g.specificStepGuid);
                        }
                    }

                    if (g.completeWhen == GroupStep.CompleteWhen.RequiredChildrenComplete ||
                        g.completeWhen == GroupStep.CompleteWhen.NOfMChildrenComplete)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Required Children", EditorStyles.boldLabel);
                        if (g.steps != null && g.steps.Count > 0)
                        {
                            g.EnsureChildRequirements();
                            for (int i = 0; i < g.steps.Count; i++)
                            {
                                var st = g.steps[i];
                                if (st == null) continue;
                                bool req = g.IsChildRequired(st.guid);
                                bool next = EditorGUILayout.ToggleLeft($"{i + 1}. {st.Kind}", req);
                                if (next != req) SetGroupChildRequired(g, st, next);
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("No nested steps yet.");
                        }
                    }

                    if (g.completeWhen == GroupStep.CompleteWhen.MultiCondition)
                    {
                        DrawMultiConditionBranchList(g);
                    }

                    g.stopOthersOnComplete = EditorGUILayout.Toggle("Stop Others On Complete", g.stopOthersOnComplete);

                    EditorGUILayout.LabelField("Nested Steps", $"{count} step(s)");
                    if (count > 0)
                    {
                        for (int i = 0; i < Mathf.Min(count, 8); i++)
                        {
                            var st = g.steps[i];
                            EditorGUILayout.LabelField($"• {(st != null ? st.Kind : "<null>")}");
                        }
                        if (count > 8)
                            EditorGUILayout.LabelField($"… +{count - 8} more");
                    }
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Tip: Drop steps into the big box above these settings.", EditorStyles.miniLabel);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Dirty(scenario, "Edit Group");
                        UpdateGroupSummaryLabel(g);
                        owner?.ScheduleResizeGroup(g.guid);
                        bool afterProxy = GroupUsesProxyBranchPorts(g);
                        int afterMcCount = g.multiConditionBranches != null ? g.multiConditionBranches.Count : 0;
                        bool topologyChanged =
                            beforeCompleteWhen != (int)g.completeWhen ||
                            beforeSpecific != (g.specificStepGuid ?? "") ||
                            beforeProxy != afterProxy ||
                            beforeMcCount != afterMcCount;
                        if (topologyChanged)
                        {
                            EditorApplication.delayCall += () =>
                            {
                                if (owner != null && owner.scenario != null)
                                    owner.Load(owner.scenario);
                            };
                        }
                    }
                }));

                // Visual drop zone (container body; includes nested tiles)
                var dropZone = new VisualElement();
                dropZone.name = "group-drop-zone";
                // Do not steal clicks/drags from nested nodes.
                dropZone.pickingMode = PickingMode.Ignore;
                dropZone.style.marginTop = 6;
                dropZone.style.paddingLeft = 10;
                dropZone.style.paddingRight = 10;
                dropZone.style.paddingTop = 8;
                dropZone.style.paddingBottom = 8;
                dropZone.style.minHeight = 0;
                dropZone.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
                dropZone.style.borderTopWidth = 1;
                dropZone.style.borderBottomWidth = 1;
                dropZone.style.borderLeftWidth = 1;
                dropZone.style.borderRightWidth = 1;
                dropZone.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
                dropZone.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);

                var dzTitle = new Label("DROP AREA");
                dzTitle.pickingMode = PickingMode.Ignore;
                dzTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                dzTitle.style.fontSize = 10;
                dzTitle.style.color = new Color(1f, 1f, 1f, 0.55f);
                dzTitle.style.marginBottom = 4;
                dropZone.Add(dzTitle);

                var dzHint = new Label("Drag existing steps here to add them to the group.\nDrag a step out to remove it.");
                dzHint.pickingMode = PickingMode.Ignore;
                dzHint.style.fontSize = 10;
                dzHint.style.color = new Color(1f, 1f, 1f, 0.75f);
                dropZone.Add(dzHint);

                // If we already have nested steps, hide the instructional text so tiles don't overlap text.
                if (g.steps != null && g.steps.Count > 0)
                {
                    dzTitle.style.display = DisplayStyle.None;
                    dzHint.style.display = DisplayStyle.None;
                }

                // --- Nested tiles (REAL container UX; not GraphView nodes) ---
                var tiles = new VisualElement();
                tiles.style.flexDirection = FlexDirection.Row;
                tiles.style.flexWrap = Wrap.Wrap;
                tiles.style.alignContent = Align.FlexStart;
                // Critical: stretch to the full container width so Wrap can use all available space.
                // Without this, UIElements may size the container to its content and you get "2 columns + huge empty gap".
                tiles.style.flexGrow = 1;
                tiles.style.alignSelf = Align.Stretch;
                tiles.style.width = Length.Percent(100);
                tiles.style.marginTop = 6;
                dropZone.Add(tiles);

                if (g.steps != null)
                {
                    for (int i = 0; i < g.steps.Count; i++)
                    {
                        var sub = g.steps[i];
                        if (sub == null) continue;

                        var tile = BuildNestedTile(g, sub, i);
                        tiles.Add(tile);
                    }
                }

                // Put the drop zone into the extension area so it appears as part of the node body
                extensionContainer.Add(dropZone);

                RebuildGroupProxyOutputPorts();
            }


            RefreshExpandedState();
            RefreshPorts();
        }

        public void SetActiveHighlight(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;

            var mc = this.mainContainer;

            // borders (as you already do)
            if (active)
            {
                mc.style.borderTopWidth = 3;
                mc.style.borderBottomWidth = 3;
                mc.style.borderLeftWidth = 3;
                mc.style.borderRightWidth = 3;
                var c = new Color(0.3f, 0.7f, 1f);
                mc.style.borderTopColor = c;
                mc.style.borderBottomColor = c;
                mc.style.borderLeftColor = c;
                mc.style.borderRightColor = c;

                if (Application.isPlaying)
                    EnsureSkipButtons();
            }
            else
            {
                mc.style.borderTopWidth = 0;
                mc.style.borderBottomWidth = 0;
                mc.style.borderLeftWidth = 0;
                mc.style.borderRightWidth = 0;
                RemoveSkipButtons();
            }

            this.MarkDirtyRepaint();
        }
        VisualElement skipRow;
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Kill Unity’s default items (Delete, Cut, etc)
            evt.menu.ClearItems();

            evt.menu.AppendAction("Duplicate Step", _ =>
            {
                duplicateRequest?.Invoke(step);
            });

            // Our single source of truth for deletion
            evt.menu.AppendAction("Delete Step", _ =>
            {
                deleteRequest?.Invoke(step);
            });

            // Attach a sticky note to this node (follows it when moved, drawn with a connector line).
            evt.menu.AppendAction("Add Attached Note", _ => owner?.AddAttachedNote(step));

            // When several nodes are selected, offer to wrap the whole selection in a group box.
            int selCount = graph?.selection?.OfType<StepNode>().Count() ?? 0;
            if (selCount >= 2)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction($"Group {selCount} Nodes in Box", _ => owner?.CreateGroupBox());
            }

            // If you want, you can also add Disconnect all etc here
            // evt.menu.AppendAction("Disconnect all", _ => { ... });
        }

        void EnsureSkipButtons()
        {
            RemoveSkipButtons();

            skipRow = new VisualElement();
            skipRow.style.flexDirection = FlexDirection.Row;
            skipRow.style.marginTop = 4;
            skipRow.style.marginBottom = 2;

            if (step is TimelineStep || step is CueCardsStep || step is InsertStep || step is EventStep)
            {
                var btn = new UIEButton(() => skipRequest?.Invoke(step, -1))
                {
                    text = "Skip ▶"
                };
                skipRow.Add(btn);
            }
            else if (step is GroupStep grp)
            {
                if (GroupUsesMultiConditionPorts(grp))
                {
                    int mcCount = grp.multiConditionBranches != null ? grp.multiConditionBranches.Count : 0;
                    for (int i = 0; i < Mathf.Min(mcCount, 16); i++)
                    {
                        int idx = i;
                        var branch = grp.multiConditionBranches[i];
                        string lbl = branch != null && !string.IsNullOrWhiteSpace(branch.label)
                            ? branch.label
                            : $"Cond {idx + 1}";
                        var b = new UIEButton(() => skipRequest?.Invoke(step, idx)) { text = $"{lbl} \u25B6" };
                        if (i > 0) b.style.marginLeft = 2;
                        skipRow.Add(b);
                    }
                }
                else if (GroupUsesProxyBranchPorts(grp))
                {
                    var ch = TryGetGroupProxyBranchChild(grp);
                    if (ch is QuestionStep qq)
                    {
                        int qCount = qq.choices != null ? qq.choices.Count : 0;
                        for (int i = 0; i < qCount; i++)
                        {
                            int idx = i;
                            var b = new UIEButton(() => skipRequest?.Invoke(step, idx))
                            {
                                text = $"Choice {idx} \u25B6"
                            };
                            if (i > 0) b.style.marginLeft = 2;
                            skipRow.Add(b);
                        }
                    }
                    else if (ch is ConditionsStep cnd)
                    {
                        var btn = new UIEButton(() => skipRequest?.Invoke(step, -1)) { text = "Skip (Default) \u25B6" };
                        skipRow.Add(btn);
                        int cCount = cnd.outcomes != null ? cnd.outcomes.Count : 0;
                        for (int i = 0; i < Mathf.Min(cCount, 16); i++)
                        {
                            int idx = i;
                            var b = new UIEButton(() => skipRequest?.Invoke(step, idx)) { text = $"Branch {idx} \u25B6" };
                            b.style.marginLeft = 2;
                            skipRow.Add(b);
                        }
                    }
                }
                else
                {
                    var btn = new UIEButton(() => skipRequest?.Invoke(step, -1)) { text = "Skip \u25B6" };
                    skipRow.Add(btn);
                }
            }
            else if (step is SelectionStep)
            {
                var bCorrect = new UIEButton(() => skipRequest?.Invoke(step, -2)) { text = "Correct ▶" };
                var bWrong = new UIEButton(() => skipRequest?.Invoke(step, -3)) { text = "Wrong ▶" };

                bWrong.style.marginLeft = 4;

                skipRow.Add(bCorrect);
                skipRow.Add(bWrong);
            }
            else if (step is QuestionStep q)
            {
                int count = q.choices != null ? q.choices.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    int idx = i;
                    var b = new UIEButton(() => skipRequest?.Invoke(step, idx))
                    {
                        text = $"Choice {idx} ▶"
                    };
                    if (i > 0) b.style.marginLeft = 2;
                    skipRow.Add(b);
                }
            }
            else if (step is ConditionsStep cnd)
            {
                var btn = new UIEButton(() => skipRequest?.Invoke(step, -1)) { text = "Skip (Default) ▶" };
                skipRow.Add(btn);
                int count = cnd.outcomes != null ? cnd.outcomes.Count : 0;
                for (int i = 0; i < Mathf.Min(count, 4); i++)
                {
                    int idx = i;
                    var b = new UIEButton(() => skipRequest?.Invoke(step, idx)) { text = $"Branch {idx} ▶" };
                    b.style.marginLeft = 2;
                    skipRow.Add(b);
                }
            }
            else if (step is MiniQuizStep mq)
            {
                // Provide a simple Skip for playmode testing (routes via Default).
                var btn = new UIEButton(() => skipRequest?.Invoke(step, -1)) { text = "Skip ▶" };
                skipRow.Add(btn);
                int count = mq.outcomes != null ? mq.outcomes.Count : 0;
                for (int i = 0; i < Mathf.Min(count, 4); i++)
                {
                    int idx = i;
                    var b = new UIEButton(() => skipRequest?.Invoke(step, idx)) { text = $"Outcome {idx} ▶" };
                    b.style.marginLeft = 2;
                    skipRow.Add(b);
                }
            }

            if (skipRow.childCount > 0)
                mainContainer.Add(skipRow);
        }

        void RemoveSkipButtons()
        {
            if (skipRow != null && skipRow.parent == mainContainer)
                mainContainer.Remove(skipRow);
            skipRow = null;
        }

        void UpdateGroupSummaryLabel(GroupStep g)
        {
            if (_groupSummaryLabel == null) return;
            _groupSummaryLabel.text = GroupSummary(g);
        }


        Port MakePort(Direction dir, Port.Capacity cap, string label, int choiceIndex)
        {
            var p = InstantiatePort(Orientation.Horizontal, dir, cap, typeof(bool));
            p.portName = label;
            PortMeta.Set(p, step, choiceIndex);

            // Create FlowEdge when user drags connections
            var connector = new EdgeConnector<FlowEdge>(new ECListener());
            p.AddManipulator(connector);

            return p;
        }

        Port MakeProxyPort(Step owner, Direction dir, Port.Capacity cap, string label, int choiceIndex)
        {
            var p = InstantiatePort(Orientation.Horizontal, dir, cap, typeof(bool));
            p.portName = label;
            PortMeta.Set(p, owner, choiceIndex);
            var connector = new EdgeConnector<FlowEdge>(new ECListener());
            p.AddManipulator(connector);
            return p;
        }

        void RebuildGroupProxyOutputPorts()
        {
            if (step is not GroupStep g) return;

            if (outChoices != null)
            {
                foreach (var p in outChoices)
                    if (p != null && p.parent == outputContainer)
                        outputContainer.Remove(p);
            }
            outChoices = null;

            if (outNext != null)
            {
                if (outNext.parent == outputContainer)
                    outputContainer.Remove(outNext);
                outNext = null;
            }

            if (GroupUsesMultiConditionPorts(g))
            {
                outChoices = new List<Port>();
                for (int i = 0; i < g.multiConditionBranches.Count; i++)
                {
                    var branch = g.multiConditionBranches[i];
                    string lbl = branch != null && !string.IsNullOrWhiteSpace(branch.label)
                        ? branch.label
                        : $"Cond {i + 1}";
                    var p = MakeProxyPort(g, Direction.Output, Port.Capacity.Single, lbl, i);
                    outChoices.Add(p);
                    outputContainer.Add(p);
                }
                if (g.multiConditionBranches.Count == 0)
                    outputContainer.Add(new Label("No conditions (add in Group Settings)"));
            }
            else
            {
                var branchChild = TryGetGroupProxyBranchChild(g);
                if (branchChild is QuestionStep nq)
                {
                    outChoices = new List<Port>();
                    int n = nq.choices != null ? nq.choices.Count : 0;
                    if (n == 0)
                        outputContainer.Add(new Label("No choices (edit nested Question)"));
                    else
                    {
                        for (int c = 0; c < n; c++)
                        {
                            var p = MakeProxyPort(nq, Direction.Output, Port.Capacity.Single, $"Choice {c}", c);
                            outChoices.Add(p);
                            outputContainer.Add(p);
                        }
                    }
                }
                else if (branchChild is ConditionsStep ncnd)
                {
                    outChoices = new List<Port>();
                    int n = ncnd.outcomes != null ? ncnd.outcomes.Count : 0;
                    if (n == 0)
                        outputContainer.Add(new Label("No outcomes (edit nested Conditions)"));
                    else
                    {
                        for (int i = 0; i < n; i++)
                        {
                            var b = ncnd.outcomes[i];
                            string label = b != null && !string.IsNullOrWhiteSpace(b.label) ? b.label : ConditionOutcomeLabel(b, i);
                            var p = MakeProxyPort(ncnd, Direction.Output, Port.Capacity.Single, label, i);
                            outChoices.Add(p);
                            outputContainer.Add(p);
                        }
                    }
                }
                else
                {
                    outNext = MakePort(Direction.Output, Port.Capacity.Single, "Next", -1);
                    outputContainer.Add(outNext);
                }
            }

            RefreshPorts();
        }

        void RecreateChoicePorts()
        {
            if (step is not QuestionStep q) return;

            if (outChoices != null)
                foreach (var p in outChoices) outputContainer.Remove(p);

            outChoices = new List<Port>();
            int count = q.choices != null ? q.choices.Count : 0;

            if (count == 0)
            {
                outputContainer.Add(new Label("No choices (edit in “Edit…”)"));
            }
            else
            {
                for (int c = 0; c < count; c++)
                {
                    // Display-only label: Choice has no text field, so prefer the linked Button's
                    // name; fall back to the index. The routing key is PortMeta.choiceIndex (= c),
                    // NOT the label - relabelling never changes which nextGuid a drag writes.
                    var ch = q.choices[c];
                    string label = ch != null && ch.button != null ? ch.button.name : $"Choice {c}";
                    var p = MakePort(Direction.Output, Port.Capacity.Single, label, c);
                    outChoices.Add(p);
                    outputContainer.Add(p);
                }
            }

            RefreshPorts();
            rebuild?.Invoke(); // will be ignored during Load thanks to guard
        }

        void RecreateMiniQuizOutcomePorts()
        {
            if (step is not MiniQuizStep mq) return;

            // Remove previous ports
            if (outChoices != null)
                foreach (var p in outChoices) outputContainer.Remove(p);
            outChoices = new List<Port>();

            if (outNext != null)
                outputContainer.Remove(outNext);

            // Default route port
            outNext = MakePort(Direction.Output, Port.Capacity.Single, "Default", -1);
            outputContainer.Add(outNext);

            int count = mq.outcomes != null ? mq.outcomes.Count : 0;
            if (count == 0)
            {
                outputContainer.Add(new Label("No outcomes (edit in “Edit…”)"));
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var o = mq.outcomes[i];
                    string label = o != null && !string.IsNullOrWhiteSpace(o.label) ? o.label : OutcomeLabel(o, i);
                    var p = MakePort(Direction.Output, Port.Capacity.Single, label, i);
                    outChoices.Add(p);
                    outputContainer.Add(p);
                }
            }

            RefreshPorts();
            rebuild?.Invoke(); // ignored during Load thanks to guard
        }

        void UpdateMiniQuizOutcomePortLabels()
        {
            if (step is not MiniQuizStep mq) return;
            if (outNext != null)
                outNext.portName = "Default";

            if (outChoices == null || outChoices.Count == 0) return;

            int count = mq.outcomes != null ? mq.outcomes.Count : 0;
            for (int i = 0; i < outChoices.Count; i++)
            {
                if (outChoices[i] == null) continue;
                if (i >= count) break;
                var o = mq.outcomes[i];
                string label = o != null && !string.IsNullOrWhiteSpace(o.label) ? o.label : OutcomeLabel(o, i);
                outChoices[i].portName = label;
            }

            RefreshPorts();
        }

        static string OutcomeLabel(MiniQuizOutcome o, int index)
        {
            if (o == null) return $"Outcome {index}";
            int min = Mathf.Max(0, o.minCorrect);
            int max = o.maxCorrect;
            if (max < 0) return $"{min}+ Correct";
            if (max == min) return $"{min} Correct";
            return $"{min}-{max} Correct";
        }

        void RecreateConditionOutcomePorts()
        {
            if (step is not ConditionsStep cnd) return;

            if (outChoices != null)
                foreach (var p in outChoices) outputContainer.Remove(p);
            outChoices = new List<Port>();

            if (outNext != null)
                outputContainer.Remove(outNext);
            outNext = null;

            int count = cnd.outcomes != null ? cnd.outcomes.Count : 0;
            if (count == 0)
            {
                outputContainer.Add(new Label("No outcomes (add in Settings)"));
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var b = cnd.outcomes[i];
                    string label = b != null && !string.IsNullOrWhiteSpace(b.label) ? b.label : ConditionOutcomeLabel(b, i);
                    var p = MakePort(Direction.Output, Port.Capacity.Single, label, i);
                    outChoices.Add(p);
                    outputContainer.Add(p);
                }
            }

            RefreshPorts();
            rebuild?.Invoke();
        }

        void UpdateConditionOutcomePortLabels()
        {
            if (step is not ConditionsStep cnd) return;

            if (outChoices == null || outChoices.Count == 0) return;

            int count = cnd.outcomes != null ? cnd.outcomes.Count : 0;
            for (int i = 0; i < outChoices.Count; i++)
            {
                if (outChoices[i] == null) continue;
                if (i >= count) break;
                var b = cnd.outcomes[i];
                string label = b != null && !string.IsNullOrWhiteSpace(b.label) ? b.label : ConditionOutcomeLabel(b, i);
                outChoices[i].portName = label;
            }

            RefreshPorts();
        }

        static string ConditionOutcomeLabel(ConditionOutcome o, int index)
        {
            if (o == null) return $"Outcome {index}";
            if (o.compareOp == CompareOp.IsTrue) return "True";
            if (o.compareOp == CompareOp.IsFalse) return "False";
            string op = o.compareOp switch
            {
                CompareOp.Less => "<",
                CompareOp.LessOrEqual => "<=",
                CompareOp.Greater => ">",
                CompareOp.GreaterOrEqual => ">=",
                CompareOp.Equal => "==",
                CompareOp.NotEqual => "!=",
                _ => "?"
            };
            return $"{op} {o.compareValue}";
        }

        VisualElement BuildNestedTile(GroupStep group, Step sub, int ordinalIndex)
        {
            var tile = new VisualElement();
            tile.style.width = GroupTileW;
            tile.style.height = GroupTileH;
            tile.style.marginRight = 10;
            tile.style.marginBottom = 10;
            tile.style.paddingLeft = 10;
            tile.style.paddingRight = 10;
            tile.style.paddingTop = 8;
            tile.style.paddingBottom = 8;
            tile.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            tile.style.borderTopLeftRadius = 6;
            tile.style.borderTopRightRadius = 6;
            tile.style.borderBottomLeftRadius = 6;
            tile.style.borderBottomRightRadius = 6;
            tile.style.borderTopWidth = 1;
            tile.style.borderBottomWidth = 1;
            tile.style.borderLeftWidth = 1;
            tile.style.borderRightWidth = 1;
            tile.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
            tile.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);

            // color accent (matches step color in graph)
            Color accent = GetStepAccent(sub);
            tile.style.borderLeftWidth = 4;
            tile.style.borderLeftColor = accent;

            // header row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            tile.Add(row);

            var name = new Label($"{ordinalIndex + 1}. {sub.Kind}");
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = Color.white;
            name.style.fontSize = 12;
            name.style.flexGrow = 1;
            row.Add(name);

            var edit = new UIEButton(() =>
            {
                // Edit nested step in focused modal
                if (sub is TimelineStep tl) StepEditWindow.OpenTimeline(scenario, tl);
                else if (sub is CueCardsStep cc) StepEditWindow.OpenCueCards(scenario, cc);
                else if (sub is QuestionStep q) StepEditWindow.OpenQuestion(scenario, q, () =>
                {
                    if (owner != null && owner.scenario != null)
                        owner.Load(owner.scenario);
                });
                else if (sub is MiniQuizStep mq) StepEditWindow.OpenMiniQuiz(scenario, mq, () => owner?.Load(owner.scenario));
                else if (sub is SelectionStep se) StepEditWindow.OpenSelection(scenario, se);
                else if (sub is InsertStep ins) StepEditWindow.OpenInsert(scenario, ins);
                else if (sub is EventStep ev) StepEditWindow.OpenEvent(scenario, ev);
                else if (sub is GroupStep g) StepEditWindow.OpenGroup(scenario, g);
            })
            { text = "Edit…" };
            edit.style.marginLeft = 6;
            row.Add(edit);

            var remove = new UIEButton(() =>
            {
                if (group?.steps == null) return;
                int idx = group.steps.IndexOf(sub);
                if (idx < 0) return;

                Dirty(scenario, "Ungroup Step");
                group.steps.RemoveAt(idx);

                int groupTopIndex = scenario.steps.IndexOf(group);
                int insertAt = Mathf.Clamp(groupTopIndex + 1, 0, scenario.steps.Count);

                // place next to the group
                var gp = group.graphPos;
                sub.graphPos = new Vector2(gp.x + 760f, gp.y + 40f + 180f * idx);

                scenario.steps.Insert(insertAt, sub);

                // rebuild window cleanly next tick
                EditorApplication.delayCall += () =>
                {
                    if (owner != null && owner.scenario != null)
                        owner.Load(owner.scenario);
                };
            })
            { text = "↗" };
            remove.tooltip = "Remove from group";
            remove.style.marginLeft = 6;
            row.Add(remove);

            // small subtext
            var hint = new Label("Nested step (runs in group)");
            hint.style.marginTop = 6;
            hint.style.fontSize = 10;
            hint.style.color = new Color(1f, 1f, 1f, 0.55f);
            tile.Add(hint);

            return tile;
        }

        static Color GetStepAccent(Step s)
        {
            if (s is TimelineStep) return new Color(0.20f, 0.42f, 0.85f);
            if (s is CueCardsStep) return new Color(0.32f, 0.62f, 0.32f);
            if (s is QuestionStep) return new Color(0.76f, 0.45f, 0.22f);
            if (s is MiniQuizStep) return new Color(0.62f, 0.34f, 0.16f);
            if (s is QuizStep) return new Color(0.78f, 0.20f, 0.20f);
            if (s is QuizResultsStep) return new Color(0.62f, 0.16f, 0.16f);
            if (s is SelectionStep) return new Color(0.58f, 0.38f, 0.78f);
            if (s is InsertStep) return new Color(0.90f, 0.75f, 0.25f);
            if (s is EventStep) return new Color(0.25f, 0.70f, 0.70f);
            if (s is GroupStep) return new Color(0.55f, 0.55f, 0.60f);
            if (s is ConditionsStep) return new Color(0.70f, 0.38f, 0.08f);
            return new Color(0.6f, 0.6f, 0.6f);
        }

        public override void SetPosition(Rect newPos)
        {
            // Persist + relative handling is centralized in graphViewChanged to avoid double-Undo and ordering bugs.
            // When expanded, don't freeze height/width from GraphView drags; keep auto-height based on content.
            // GraphView passes a rect including the current size; applying it would make height fixed and clip later.
            if (!IsNested && step is not GroupStep && _foldout != null && _foldout.value && !UserSized)
            {
                style.left = newPos.xMin;
                style.top = newPos.yMin;
                // Width is fixed in our UX; height remains Auto.
                return;
            }

            base.SetPosition(newPos);
        }

        public void SetPositionSilent(Rect newPos)
        {
            // Same rule as SetPosition: when expanded, keep auto-height (and fixed width) but move position silently.
            if (!IsNested && step is not GroupStep && _foldout != null && _foldout.value && !UserSized)
            {
                style.left = newPos.xMin;
                style.top = newPos.yMin;
                style.width = newPos.width;
                style.minWidth = newPos.width;
                style.maxWidth = newPos.width;
                style.height = new StyleLength(StyleKeyword.Auto);
                return;
            }

            base.SetPosition(newPos);
            // Hard-enforce size so user-resizes / layout quirks can't leave a permanent right-side gap.
            style.width = newPos.width;
            style.height = newPos.height;
        }

        public float GetHeight()
        {
            if (IsNested) return 110f; // compact tile in container
            if (UserSized) return UserSize.y; // honor manual resize (editor-only side-table)
            bool expandedDetails = _foldout != null && _foldout.value;
            float collapsed = GetCollapsedHeight();
            if (!expandedDetails) return collapsed;
            float groupHeight = 0f;
            if (step is GroupStep g)
            {
                int count = g.steps?.Count ?? 0;
                int rows = Mathf.CeilToInt(count / (float)GroupTileColumns);
                float tilesH = count == 0 ? 72f : rows * GroupTileH + Mathf.Max(0, rows - 1) * 10f + 18f;
                groupHeight = Mathf.Max(260f, 54f + tilesH + 160f);
            }

            // Auto-height nodes: prefer actual laid-out height when available.
            if (layout.height > 1f)
                return Mathf.Max(collapsed, Mathf.Max(layout.height, groupHeight));
            return Mathf.Max(collapsed, groupHeight);
        }

        float GetCollapsedHeight()
        {
            // Compact collapsed sizes (match "small nodes" UX). Expanded sizes are handled by the Details sizing logic.
            const float small = 120f;     // header + ports + a little breathing room
            const float medium = 140f;    // steps with extra ports (selection) need a bit more

            if (step is TimelineStep) return small;
            if (step is CueCardsStep) return small;
            if (step is InsertStep) return small;
            if (step is EventStep) return small;

            if (step is SelectionStep) return medium;

            if (step is QuestionStep q)
            {
                int count = q.choices?.Count ?? 0;
                // Keep enough vertical space to show choice ports without becoming huge.
                return Mathf.Max(medium, 130f + 18f * Mathf.Clamp(count, 0, 8));
        }
            if (step is MiniQuizStep mq)
            {
                int count = mq.outcomes?.Count ?? 0;
                // Default port + outcome ports
                return Mathf.Max(medium, 140f + 18f * Mathf.Clamp(count + 1, 0, 8));
            }
            if (step is ConditionsStep cnd)
            {
                int count = cnd.outcomes?.Count ?? 0;
                return Mathf.Max(medium, 140f + 18f * Mathf.Clamp(count + 1, 0, 8));
            }

            return small;
        }

        void ResizeToFitDetails()
        {
            if (UserSized) return;
            if (_foldout == null || !_foldout.value) return;
            // With auto-height, we just ensure height is Auto and let UIElements measure actual content.
            if (step is GroupStep) return;
            style.minHeight = GetCollapsedHeight();
            style.height = new StyleLength(StyleKeyword.Auto);
        }

        void QueueResizeToFitDetails()
        {
            if (UserSized) return;
            if (_foldout == null || !_foldout.value) return;
            if (_resizeQueued) return;
            _resizeQueued = true;
            EditorApplication.delayCall += () =>
            {
                _resizeQueued = false;
                if (this == null) return;
                ResizeToFitDetails();
            };
        }

        // Drag the bottom-right handle to resize; the size is persisted in the scenario's
        // editor-only side-table. Double-click the handle to clear it and return to auto-sizing.
        void SetupResize()
        {
            bool resizing = false;
            Vector2 startMouse = default;
            Vector2 startSize = default;
            Scenario.StepGraphDisplay sizeEntry = null;

            _resizeHandle.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;

                // Double-click clears the manual size and rebuilds with auto-sizing.
                if (e.clickCount == 2)
                {
                    Dirty(scenario, "Auto-size Node");
                    var d = scenario.FindStepGraphDisplay(step.guid);
                    if (d != null)
                    {
                        d.size = Vector2.zero;
                        scenario.PruneStepGraphDisplay(step.guid);
                    }
                    e.StopPropagation();
                    owner?.Load(owner.scenario);
                    return;
                }

                resizing = true;
                startMouse = e.mousePosition;
                startSize = new Vector2(resolvedStyle.width, resolvedStyle.height);
                if (startSize.x < 1f || float.IsNaN(startSize.x)) startSize.x = layout.width;
                if (startSize.y < 1f || float.IsNaN(startSize.y)) startSize.y = layout.height;

                // Switch to explicit sizing so width/height take effect (override the fixed-width UX).
                style.maxWidth = StyleKeyword.None;
                style.minWidth = 120f;
                style.minHeight = 80f;

                // Record BEFORE the drag mutates the entry so undo restores the pre-resize size.
                Dirty(scenario, "Resize Node");
                sizeEntry = scenario.GetOrAddStepGraphDisplay(step.guid);

                _resizeHandle.CaptureMouse();
                e.StopPropagation();
            });

            _resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!resizing) return;
                var delta = e.mousePosition - startMouse;
                float w = Mathf.Max(140f, startSize.x + delta.x);
                float h = Mathf.Max(90f, startSize.y + delta.y);
                style.width = w;
                style.height = h;
                if (sizeEntry != null) sizeEntry.size = new Vector2(w, h);
                e.StopPropagation();
            });

            _resizeHandle.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!resizing) return;
                resizing = false;
                _resizeHandle.ReleaseMouse();
                // Click-without-drag leaves a default entry behind - drop it.
                scenario.PruneStepGraphDisplay(step.guid);
                sizeEntry = null;
                e.StopPropagation();
            });
        }

        // NOTE: We intentionally avoid manual expanded-height calculations.
        // Expanded nodes auto-size to the true IMGUI content height to prevent clipping and stale sizing.


        sealed class ECListener : IEdgeConnectorListener
        {
            public void OnDropOutsidePort(Edge edge, Vector2 pos) { }
            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge?.input == null || edge.output == null) return;
                graphView.AddElement(edge);

                // IMPORTANT: some GraphView flows do not populate graphViewChanged.edgesToCreate.
                // Ensure we persist routes by scheduling a full sync after an edge drop.
                if (graphView is ScenarioGraphView gv)
                    gv.OnEdgeDropped?.Invoke();
            }
        }
        // ---- Edge with moving "flow" marker ----

    }
}
#endif
