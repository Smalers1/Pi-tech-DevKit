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

// =================== STEP EDIT WINDOWS (UI shown when you click “Edit…”) ===================

sealed class StepEditWindow : EditorWindow
{
    Scenario scenario;
    string scenarioGlobalId;
    string stepGuid;
    SerializedObject so;
    SerializedProperty stepProp;
    Vector2 scroll;

    Action onAfterApply; // optional (ex: rebuild choice ports)

    // --------- open helpers ----------
    public static void OpenTimeline(Scenario sc, TimelineStep tl)
        => Open(sc, tl.guid, "Timeline", w => w.DrawTimeline());

    public static void OpenCueCards(Scenario sc, CueCardsStep cc)
        => Open(sc, cc.guid, "Cue Cards", w => w.DrawCueCards());

    public static void OpenQuestion(Scenario sc, QuestionStep q, Action afterApply = null)
        => Open(sc, q.guid, "Question", w => w.DrawQuestion(), afterApply);
    public static void OpenMiniQuiz(Scenario sc, MiniQuizStep mq, Action afterApply = null)
        => Open(sc, mq.guid, "Mini Quiz", w => w.DrawMiniQuiz(), afterApply);
    public static void OpenQuiz(Scenario sc, QuizStep qz)
        => Open(sc, qz.guid, "Quiz", w => w.DrawQuiz());
    public static void OpenSelection(Scenario sc, SelectionStep sel)
    => Open(sc, sel.guid, "Selection", w => w.DrawSelection());

    public static void OpenInsert(Scenario sc, InsertStep ins)
    => Open(sc, ins.guid, "Insert", w => w.DrawInsert());

    public static void OpenEvent(Scenario sc, EventStep ev)
    => Open(sc, ev.guid, "Event", w => w.DrawEvent());

    public static void OpenGroup(Scenario sc, GroupStep g)
    => Open(sc, g.guid, "Group", w => w.DrawGroup());


    static void Open(Scenario sc, string guid, string title, Action<StepEditWindow> draw, Action afterApply = null)
    {
        var w = CreateInstance<StepEditWindow>();
        w.scenario = sc;
        try
        {
            if (sc)
                w.scenarioGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(sc).ToString();
        }
        catch { /* best-effort; GlobalObjectId can fail in some editor contexts */ }
        w.stepGuid = guid;
        w.minSize = new Vector2(460, 360);
        w.titleContent = new GUIContent($"{title} • Step");
        w.onAfterApply = afterApply;
        w.ShowUtility();
        // Event.current can be null when opened from UIElements callbacks; fall back to a sensible position.
        var mp = Event.current != null ? Event.current.mousePosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        w.position = new Rect(GUIUtility.GUIToScreenPoint(mp) + new Vector2(8, 8), w.minSize);
        w.Init(draw);
    }

    Action<StepEditWindow> drawer;

    void Init(Action<StepEditWindow> d)
    {
        drawer = d;
        TryResolveScenario();
        if (!scenario) return;
        so = new SerializedObject(scenario);
        stepProp = FindStepPropertyRecursive(so, stepGuid);
    }

    internal static SerializedProperty FindStepPropertyRecursive(SerializedObject so, string guid)
    {
        if (so == null || string.IsNullOrEmpty(guid)) return null;
        var steps = so.FindProperty("steps");
        return FindInStepsList(steps, guid);
    }

    static SerializedProperty FindInStepsList(SerializedProperty stepsList, string guid)
    {
        if (stepsList == null || !stepsList.isArray) return null;

        for (int i = 0; i < stepsList.arraySize; i++)
        {
            var el = stepsList.GetArrayElementAtIndex(i);
            if (el == null) continue;

            var g = el.FindPropertyRelative("guid");
            if (g != null && g.stringValue == guid)
                return el;

            // If this element is a GroupStep, it has a nested SerializeReference list called "steps".
            var nested = el.FindPropertyRelative("steps");
            if (nested != null && nested.isArray)
            {
                var found = FindInStepsList(nested, guid);
                if (found != null) return found;
            }
        }

        return null;
    }

    void OnGUI()
    {
        if (!scenario)
        {
            TryResolveScenario();
        }

        if (!scenario)
        {
            EditorGUILayout.HelpBox("Scenario not found.", MessageType.Warning);
            if (GUILayout.Button("Close")) Close();
            return;
        }

        if (so == null)
            so = new SerializedObject(scenario);

        // Step may be nested inside a GroupStep; always re-resolve each frame to avoid stale references after reflow/ungroup.
        if (stepProp == null)
            stepProp = FindStepPropertyRecursive(so, stepGuid);

        if (so == null || stepProp == null)
        {
            EditorGUILayout.HelpBox("Step not found (maybe removed).", MessageType.Warning);
            if (GUILayout.Button("Close")) Close();
            return;
        }

        so.Update();

        EditorGUI.BeginChangeCheck();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(2);
        drawer?.Invoke(this);
        EditorGUILayout.EndScrollView();

        // Apply immediately on change so the window doesn't feel "read-only".
        // (If we only apply on the Apply button, so.Update() will reload from the target every frame and discard edits.)
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(scenario, "Edit Step");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(scenario);
            EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);
            onAfterApply?.Invoke();
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply", GUILayout.Width(90)))
            {
                Undo.RecordObject(scenario, "Edit Step");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(scenario);
                EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);
                onAfterApply?.Invoke();
            }
            if (GUILayout.Button("Close", GUILayout.Width(90))) Close();
        }
    }

    void TryResolveScenario()
    {
        if (scenario) return;
        if (string.IsNullOrEmpty(scenarioGlobalId)) return;

        try
        {
            if (GlobalObjectId.TryParse(scenarioGlobalId, out var gid))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj is Scenario sc)
                    scenario = sc;
            }
        }
        catch { }
    }

    // ----------- specific drawers -----------
    void DrawTimeline()
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("director"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("rewindOnEnter"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("waitForEnd"));
        DrawNextGuid();
    }

    void DrawCueCards()
    {
        EditorGUILayout.LabelField("Cue Cards", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("director"), new GUIContent("Clock Director (opt)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cards in order", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("cards"), true);

        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("cueTimes"), new GUIContent("Cue Times (sec)"), true);
        EditorGUILayout.HelpBox("Cue Time = max seconds to keep the card if the player doesn’t tap.\nLeave empty for tap-only. If only one value is provided it applies to all cards.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("autoShowFirst"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("tapHint"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("advanceMode"), new GUIContent("Advance"));
        var adv = stepProp.FindPropertyRelative("advanceMode");
        if (adv != null && adv.enumValueIndex == (int)CueCardsStep.AdvanceMode.OnButton)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("nextButton"), new GUIContent("Next Button"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional extra object", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("extraObject"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("extraShowAtIndex"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideExtraWithFinalTap"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("useRenderersForExtra"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fadeDuration"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("popScale"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("popDuration"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fadeCurve"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("scaleCurve"));

        DrawNextGuid();
    }

    void DrawQuestion()
    {
        EditorGUILayout.LabelField("Question", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("fallbackHideSeconds"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);
        var choices = stepProp.FindPropertyRelative("choices");
        if (choices != null)
        {
            // Unity will render button + nested effects when we draw the element with 'true'
            EditorGUILayout.PropertyField(choices, includeChildren: true);
            EditorGUILayout.HelpBox("Each Choice → assign the Button and edit its Effects. Effects list supports Add/Remove/Reorder.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Choices list not found.", MessageType.Warning);
        }
    }

    void DrawMiniQuiz()
    {
        EditorGUILayout.LabelField("Mini Quiz", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("completion"));
        var completionProp = stepProp.FindPropertyRelative("completion");
        int completionMode = completionProp != null ? completionProp.enumValueIndex : 0;
        if (completionMode == (int)MiniQuizStep.CompleteMode.OnSubmitButton)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("submitButton"));

        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("lockQuestionAfterAnswer"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Questions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("questions"), includeChildren: true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Routing (by correct count)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("outcomes"), includeChildren: true);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("defaultNextGuid"), new GUIContent("Default Next Guid"));

        EditorGUILayout.HelpBox(
            "Mini Quiz = multiple questions shown at once. Score is the number of questions answered correctly.\n" +
            "Routing: the first Outcome whose [minCorrect..maxCorrect] contains the score will be used.\n" +
            "Use maxCorrect = -1 for no maximum. If no outcome matches, Default Next Guid is used (or linear next if empty).\n\n" +
            "Important: The chosen outcome routes to ONE step, but the scenario will keep going from there.\n" +
            "If your other outcome step is placed right after it in the list, it will still run unless you connect a Next from the chosen step to a join point (or move/reorder steps).",
            MessageType.Info);
    }
    void DrawQuiz()
    {
        EditorGUILayout.LabelField("Quiz", EditorStyles.boldLabel);
        var quizProp = stepProp.FindPropertyRelative("quiz");
        var idProp = stepProp.FindPropertyRelative("questionId");
        var completionProp = stepProp.FindPropertyRelative("completion");

        EditorGUILayout.PropertyField(quizProp, new GUIContent("Quiz Asset"));

        // Question picker
        if (quizProp != null && idProp != null)
        {
            var quizObj = quizProp.objectReferenceValue;
            if (quizObj != null)
            {
                var soQuiz = new SerializedObject(quizObj);
                var questions = soQuiz.FindProperty("questions");
                if (questions != null && questions.isArray && questions.arraySize > 0)
                {
                    var labels = new List<string> { "Pick question…" };
                    var ids = new List<string> { "" };
                    for (int i = 0; i < questions.arraySize; i++)
                    {
                        var q = questions.GetArrayElementAtIndex(i);
                        if (q == null) continue;
                        var id = q.FindPropertyRelative("id")?.stringValue ?? "";
                        var prompt = q.FindPropertyRelative("prompt")?.stringValue ?? "";
                        var ptxt = !string.IsNullOrWhiteSpace(prompt)
                            ? (prompt.Length > 28 ? prompt.Substring(0, 28) + "…" : prompt)
                            : "(No prompt)";
                        string label = $"{i + 1}. {ptxt}";
                        labels.Add(label);
                        ids.Add(id);
                    }
                    int cur = Mathf.Max(0, ids.IndexOf(idProp.stringValue));

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
                                idProp.stringValue = ids[Mathf.Clamp(ii, 0, ids.Count - 1)];
                            });
                        }
                        menu.DropDown(rr);
                    }
                }
            }
        }

        EditorGUILayout.PropertyField(completionProp, new GUIContent("Routing"));

        if (completionProp != null && completionProp.enumValueIndex == (int)QuizStep.CompleteMode.BranchOnCorrectness)
            DrawQuizCorrectWrongGuids();
        else
            DrawNextGuid();
    }

    void DrawQuizCorrectWrongGuids()
    {
        var corr = stepProp.FindPropertyRelative("correctNextGuid");
        var wrong = stepProp.FindPropertyRelative("wrongNextGuid");
        if (corr == null || wrong == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Correct →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(corr.stringValue) ? "(next in list)" : corr.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) corr.stringValue = "";
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Wrong →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(wrong.stringValue) ? "(next in list)" : wrong.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) wrong.stringValue = "";
        }
    }
    void DrawSelection()
    {
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("lists"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listKey"), new GUIContent("List Name"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("listIndex"), new GUIContent("(or) List Index"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("resetOnEnter"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("completion"));
        var comp = stepProp.FindPropertyRelative("completion").enumValueIndex;
        if (comp == (int)SelectionStep.CompleteMode.OnSubmitButton)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("submitButton"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Requirement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requiredSelections"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requireExactCount"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("allowedWrong"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("timeoutSeconds"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI (optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelRoot"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("panelAnimator"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("showTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hideTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("hint"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stat Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onCorrectEffects"), true);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("onWrongEffects"), true);

        DrawCorrectWrongGuids();
    }

    void DrawInsert()
    {
        EditorGUILayout.LabelField("Insert", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("item"), new GUIContent("Item"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("targetTrigger"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("attachTransform"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attach Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("smoothAttach"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("parentToAttach"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("moveSpeed"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("rotateSpeed"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("positionTolerance"));
        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("angleTolerance"));

        DrawNextGuid();
    }

    void DrawEvent()
    {
        EditorGUILayout.LabelField("Event", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            stepProp.FindPropertyRelative("onEnter"),
            new GUIContent("On Enter Events")
        );
        EditorGUILayout.PropertyField(
            stepProp.FindPropertyRelative("waitSeconds"),
            new GUIContent("Wait Seconds Before Next")
        );

        DrawNextGuid();
    }

    void DrawGroup()
    {
        EditorGUILayout.LabelField("Group", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Group runs all nested steps together.\n" +
            "Nested routing (nextGuid / branch guids) is ignored; only the Group's Next is used.",
            MessageType.Info);

        var modeProp = stepProp.FindPropertyRelative("completeWhen");
        EditorGUILayout.PropertyField(modeProp);
        var mode = modeProp != null ? modeProp.enumValueIndex : 0;

        var stepsProp = stepProp.FindPropertyRelative("steps");
        var reqProp = stepProp.FindPropertyRelative("childRequirements");

        if (mode == (int)GroupStep.CompleteWhen.NOfMChildrenComplete)
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requiredCount"));
        else if (mode == (int)GroupStep.CompleteWhen.SpecificChildCompletes)
            DrawSpecificChildPopup(stepsProp, stepProp.FindPropertyRelative("specificStepGuid"));

        if (mode == (int)GroupStep.CompleteWhen.RequiredChildrenComplete ||
            mode == (int)GroupStep.CompleteWhen.NOfMChildrenComplete)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Required Children", EditorStyles.boldLabel);
            EnsureGroupRequirements(stepsProp, reqProp);
            DrawRequiredChildrenList(stepsProp, reqProp);
        }

        if (mode == (int)GroupStep.CompleteWhen.MultiCondition)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Multi-Condition Branches", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("multiConditionBranches"), includeChildren: true);
        }

        EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("stopOthersOnComplete"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nested Steps", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stepsProp, includeChildren: true);

        DrawNextGuid();
    }

    void DrawSpecificChildPopup(SerializedProperty stepsProp, SerializedProperty guidProp)
    {
        if (stepsProp == null || guidProp == null) return;

        if (stepsProp.arraySize <= 0)
        {
            EditorGUILayout.HelpBox("Add nested steps to the Group to select a specific completion step.", MessageType.Info);
            EditorGUILayout.PropertyField(guidProp, new GUIContent("Specific Step Guid"));
            return;
        }

        var options = new List<string> { "None" };
        var guids = new List<string> { "" };
        for (int i = 0; i < stepsProp.arraySize; i++)
        {
            var el = stepsProp.GetArrayElementAtIndex(i);
            var g = el?.FindPropertyRelative("guid");
            if (g == null || string.IsNullOrEmpty(g.stringValue)) continue;
            options.Add($"{i + 1}. {GetStepKindLabel(el)}");
            guids.Add(g.stringValue);
        }

        int cur = 0;
        if (!string.IsNullOrEmpty(guidProp.stringValue))
        {
            int idx = guids.IndexOf(guidProp.stringValue);
            if (idx >= 0) cur = idx;
        }

        int next = EditorGUILayout.Popup("Specific Step", cur, options.ToArray());
        guidProp.stringValue = guids[Mathf.Clamp(next, 0, guids.Count - 1)];
    }

    static void EnsureGroupRequirements(SerializedProperty stepsProp, SerializedProperty reqProp)
    {
        if (stepsProp == null || reqProp == null || !stepsProp.isArray || !reqProp.isArray) return;

        var existing = new HashSet<string>();
        for (int i = 0; i < stepsProp.arraySize; i++)
        {
            var el = stepsProp.GetArrayElementAtIndex(i);
            var g = el?.FindPropertyRelative("guid");
            if (g == null || string.IsNullOrEmpty(g.stringValue)) continue;
            existing.Add(g.stringValue);

            if (FindRequirement(reqProp, g.stringValue) == null)
            {
                int idx = reqProp.arraySize;
                reqProp.InsertArrayElementAtIndex(idx);
                var item = reqProp.GetArrayElementAtIndex(idx);
                item.FindPropertyRelative("guid").stringValue = g.stringValue;
                item.FindPropertyRelative("required").boolValue = true;
            }
        }

        for (int i = reqProp.arraySize - 1; i >= 0; i--)
        {
            var item = reqProp.GetArrayElementAtIndex(i);
            var g = item.FindPropertyRelative("guid");
            if (g == null || string.IsNullOrEmpty(g.stringValue) || !existing.Contains(g.stringValue))
                reqProp.DeleteArrayElementAtIndex(i);
        }
    }

    static void DrawRequiredChildrenList(SerializedProperty stepsProp, SerializedProperty reqProp)
    {
        if (stepsProp == null || stepsProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("No nested steps yet.");
            return;
        }

        for (int i = 0; i < stepsProp.arraySize; i++)
        {
            var el = stepsProp.GetArrayElementAtIndex(i);
            var g = el?.FindPropertyRelative("guid");
            if (g == null || string.IsNullOrEmpty(g.stringValue)) continue;

            var req = FindRequirement(reqProp, g.stringValue);
            bool cur = req != null ? req.FindPropertyRelative("required").boolValue : true;
            bool next = EditorGUILayout.ToggleLeft($"{i + 1}. {GetStepKindLabel(el)}", cur);
            if (req != null && next != cur)
                req.FindPropertyRelative("required").boolValue = next;
        }
    }

    static SerializedProperty FindRequirement(SerializedProperty reqProp, string guid)
    {
        if (reqProp == null || !reqProp.isArray) return null;
        for (int i = 0; i < reqProp.arraySize; i++)
        {
            var item = reqProp.GetArrayElementAtIndex(i);
            var g = item.FindPropertyRelative("guid");
            if (g != null && g.stringValue == guid) return item;
        }
        return null;
    }

    static string GetStepKindLabel(SerializedProperty el)
    {
        if (el == null) return "Step";
        string full = el.managedReferenceFullTypename ?? "";
        if (full.Contains(nameof(TimelineStep))) return "Timeline";
        if (full.Contains(nameof(CueCardsStep))) return "Cue Cards";
        if (full.Contains(nameof(QuestionStep))) return "Question";
        if (full.Contains(nameof(SelectionStep))) return "Selection";
        if (full.Contains(nameof(InsertStep))) return "Insert";
        if (full.Contains(nameof(EventStep))) return "Event";
        if (full.Contains(nameof(GroupStep))) return "Group";
        return "Step";
    }


    void DrawCorrectWrongGuids()
    {
        var corr = stepProp.FindPropertyRelative("correctNextGuid");
        var wrong = stepProp.FindPropertyRelative("wrongNextGuid");
        if (corr == null || wrong == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Correct →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(corr.stringValue) ? "(next in list)" : corr.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) corr.stringValue = "";
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Wrong →", GUILayout.Width(70));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(wrong.stringValue) ? "(next in list)" : wrong.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) wrong.stringValue = "";
        }
    }


    void DrawNextGuid()
    {
        // Render “Next Guid” preview/readout so authors can confirm wiring without leaving the popup
        var ng = stepProp.FindPropertyRelative("nextGuid");
        if (ng == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Next", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(ng.stringValue) ? "(next in list)" : ng.stringValue, GUILayout.Height(18));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) ng.stringValue = "";
        }
    }
}
#endif
