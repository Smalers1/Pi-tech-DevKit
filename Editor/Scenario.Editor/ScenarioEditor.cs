#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    using Runtime = Pitech.XR.Scenario;

    [CustomEditor(typeof(Runtime.Scenario))]
    public class ScenarioEditor : UnityEditor.Editor
    {
        SerializedProperty stepsProp;
        SerializedProperty titleProp;
        ReorderableList list;

        // foldout prefs (persist per user)
        const string FoldStepsKey = "pitech.xr.scenario.fold.steps";
        const string FoldRoutingKey = "pitech.xr.scenario.fold.routing";
        const string FoldValidationKey = "pitech.xr.scenario.fold.validation";

        bool foldSteps;
        bool foldRouting;
        bool foldValidation;

        void OnEnable()
        {
            FindProps();
            BuildList();

            foldSteps = EditorPrefs.GetBool(FoldStepsKey, true);
            foldRouting = EditorPrefs.GetBool(FoldRoutingKey, true);
            foldValidation = EditorPrefs.GetBool(FoldValidationKey, true);
        }

        void OnDisable()
        {
            EditorPrefs.SetBool(FoldStepsKey, foldSteps);
            EditorPrefs.SetBool(FoldRoutingKey, foldRouting);
            EditorPrefs.SetBool(FoldValidationKey, foldValidation);
        }

        void FindProps()
        {
            if (target == null) return;
            stepsProp = serializedObject.FindProperty("steps");
            titleProp = serializedObject.FindProperty("title");
        }

        public override void OnInspectorGUI()
        {
            if (target == null) return;

            serializedObject.UpdateIfRequiredOrScript();
            Styles.Ensure();
            if (stepsProp == null) FindProps();
            if (list == null) BuildList();

            // TOP BAR
            DrawTopBar();

            // AUTHORING HINT
            using (new EditorGUILayout.VerticalScope(Styles.InfoBox))
            {
                EditorGUILayout.LabelField("Authoring", Styles.Bold);
                EditorGUILayout.LabelField("• Timeline: assign the scene PlayableDirector", Styles.Small);
                EditorGUILayout.LabelField("• Cue Cards: add cards and Cue Times (sec). Empty = tap-only", Styles.Small);
                EditorGUILayout.LabelField("• Question: set Panel Root, Animator and Buttons then add Effects", Styles.Small);
                EditorGUILayout.LabelField("• Selection: set SelectionLists, choose list (Key or Index), rule & completion.", Styles.Small);
                EditorGUILayout.LabelField("• Insert: set item, target trigger and optional attach behaviour.", Styles.Small);
                EditorGUILayout.LabelField("• Conditions: Stat, Component member, or List by label (list field + match); outcomes route per branch.", Styles.Small);
            }

            // STEPS SECTION
            foldSteps = Styles.Section("Steps", foldSteps, () =>
            {
                if (list != null) list.DoLayoutList();
            });

            // ROUTING SECTION
            var sc = target as Runtime.Scenario;
            foldRouting = Styles.Section("Routing (quick links)", foldRouting, () =>
            {
                DrawRouting(sc);
            });

            // VALIDATION
            foldValidation = Styles.Section("Validation", foldValidation, () =>
            {
                DrawValidation(sc);
            });

            serializedObject.ApplyModifiedProperties();
        }

        void DrawTopBar()
        {
            var sc = target as Runtime.Scenario;

            using (new EditorGUILayout.VerticalScope(Styles.InfoBox))
            {
                // Title
                EditorGUILayout.LabelField("Scenario Title", Styles.HeaderTitle);

                if (titleProp != null)
                {
                    EditorGUI.BeginChangeCheck();
                    string newTitle = EditorGUILayout.TextField(GUIContent.none, titleProp.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        titleProp.stringValue = newTitle;
                        serializedObject.ApplyModifiedProperties();

                        if (sc && !string.IsNullOrEmpty(newTitle) && sc.gameObject.name == "Scenario")
                        {
                            sc.gameObject.name = newTitle;
                            EditorUtility.SetDirty(sc);
                        }
                    }
                }

                // Big button
                EditorGUILayout.Space(4);
                var r = GUILayoutUtility.GetRect(
                    GUIContent.none, Styles.BigButton, GUILayout.Height(34), GUILayout.ExpandWidth(true)
                );

                var prevBg = GUI.backgroundColor;
                var prevCt = GUI.contentColor;
                GUI.backgroundColor = new Color(0.55f, 0.72f, 1.00f);
                GUI.contentColor = Color.white;

                if (GUI.Button(r, "★  Open Scenario Graph", Styles.BigButton))
                    ScenarioGraphWindow.Open(sc);

                GUI.contentColor = prevCt;
                GUI.backgroundColor = prevBg;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", Styles.Mid, GUILayout.Height(22)))
                        EditorGUIUtility.PingObject(sc);

                    if (GUILayout.Button("Remove null step slots", Styles.Mid, GUILayout.Height(22)))
                    {
                        if (sc?.steps != null &&
                            EditorUtility.DisplayDialog(
                                "Remove null slots",
                                "This permanently deletes list entries where the SerializeReference failed to load. " +
                                "Only use this after you are sure those slots are not going to deserialize (e.g. orphaned after a type rename).",
                                "Remove nulls",
                                "Cancel"))
                        {
                            for (int i = sc.steps.Count - 1; i >= 0; i--)
                                if (sc.steps[i] == null) sc.steps.RemoveAt(i);
                            EditorUtility.SetDirty(sc);
                        }
                    }
                }
            }
        }

        // ================== Reorderable List ==================

        void BuildList()
        {
            if (stepsProp == null) return;

            list = new ReorderableList(serializedObject, stepsProp, true, true, true, true);

            list.drawHeaderCallback = r =>
            {
                r.x += Styles.SectionBox.padding.left;
                r.width -= Styles.SectionBox.padding.horizontal;
                EditorGUI.LabelField(r, "Create Steps (Example: Timeline → Cue Cards → Question → …)", Styles.Bold);
            };

            list.onAddDropdownCallback = (rect, _) =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Timeline"), false, () => AddStep(typeof(Runtime.TimelineStep)));
                menu.AddItem(new GUIContent("Add Cue Cards"), false, () => AddStep(typeof(Runtime.CueCardsStep)));
                menu.AddItem(new GUIContent("Add Question"), false, () => AddStep(typeof(Runtime.QuestionStep)));
                menu.AddItem(new GUIContent("Add Quiz"), false, () => AddStep(typeof(Runtime.QuizStep)));
                menu.AddItem(new GUIContent("Add Quiz Results"), false, () => AddStep(typeof(Runtime.QuizResultsStep)));
                menu.AddItem(new GUIContent("Add Selection"), false, () => AddStep(typeof(Runtime.SelectionStep)));
                menu.AddItem(new GUIContent("Add Insert"), false, () => AddStep(typeof(Runtime.InsertStep)));
                menu.AddItem(new GUIContent("Add Event"), false, () => AddStep(typeof(Runtime.EventStep)));
                menu.AddItem(new GUIContent("Add Conditions"), false, () => AddStep(typeof(Runtime.ConditionsStep)));
                menu.AddItem(new GUIContent("Add Group"), false, () => AddStep(typeof(Runtime.GroupStep)));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Add Session Start"), false, () => AddStep(typeof(Runtime.SessionStartStep)));
                menu.AddItem(new GUIContent("Add Session Stop"), false, () => AddStep(typeof(Runtime.SessionStopStep)));
                menu.ShowAsContext();
            };

            list.elementHeightCallback = i =>
            {
                var p = stepsProp.GetArrayElementAtIndex(i);
                if (p == null || p.managedReferenceValue == null)
                    return EditorGUIUtility.singleLineHeight * 2 + 12;

                // Session bracket markers show only a header + one info line (no editable body).
                if (IsSessionMarker(p))
                    return EditorGUIUtility.singleLineHeight * 2 + 12;

                float inner = EditorGUI.GetPropertyHeight(p, true);
                return inner + EditorGUIUtility.singleLineHeight + 10;
            };

            list.drawElementBackgroundCallback = (rect, index, active, focused) =>
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var c = (index % 2 == 0) ? Styles.RowEven : Styles.RowOdd;
                    EditorGUI.DrawRect(rect, c);
                }
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = stepsProp.GetArrayElementAtIndex(index);

                if (el == null || el.managedReferenceValue == null)
                {
                    var header = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(header, $"{index:00}. <missing step>", Styles.Muted);
                    var fix = new Rect(rect.x + 4, header.y + header.height + 3, rect.width - 8, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(fix, "Remove null entry", Styles.Mid))
                    {
                        stepsProp.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                    return;
                }

                string full = el.managedReferenceFullTypename ?? "";
                string kind =
                    full.Contains(nameof(Runtime.TimelineStep)) ? "Timeline" :
                    full.Contains(nameof(Runtime.CueCardsStep)) ? "Cue Cards" :
                    full.Contains(nameof(Runtime.QuestionStep)) ? "Question" :
                    full.Contains(nameof(Runtime.QuizStep)) ? "Quiz" :
                    full.Contains(nameof(Runtime.SelectionStep)) ? "Selection" :
                    full.Contains(nameof(Runtime.InsertStep)) ? "Insert" :
                    full.Contains(nameof(Runtime.EventStep)) ? "Event" :
                    full.Contains(nameof(Runtime.ConditionsStep)) ? "Conditions" :
                    full.Contains(nameof(Runtime.GroupStep)) ? "Group" :
                    full.Contains(nameof(Runtime.SessionStartStep)) ? "Session Start" :
                    full.Contains(nameof(Runtime.SessionStopStep)) ? "Session Stop" :
                    "Step";


                var header2 = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, EditorGUIUtility.singleLineHeight);
                // Custom display name lives in the scenario's editor-only side-table, keyed by guid.
                string stepGuid = el.FindPropertyRelative("guid")?.stringValue;
                string displayName = (target as Runtime.Scenario)?.FindStepGraphDisplay(stepGuid)?.displayName;
                DrawStepHeader(header2, index, kind, displayName);

                if (IsSessionMarker(el))
                {
                    // Analytics bracket markers carry NO manually editable options - routing ("Next") is
                    // authored in the Scenario Graph. Show an info line instead of the default body.
                    var info = new Rect(rect.x + 4, header2.y + header2.height + 3, rect.width - 8, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(info, "No editable options - routing is set in the Scenario Graph.", Styles.Muted);
                }
                else
                {
                    var body = new Rect(
                        rect.x + 4, header2.y + header2.height + 3,
                        rect.width - 8, EditorGUI.GetPropertyHeight(el, true));

                    EditorGUI.PropertyField(body, el, GUIContent.none, true);
                }

                var xRect = new Rect(rect.xMax - 22, rect.y + 2, 18, EditorGUIUtility.singleLineHeight - 2);
                if (GUI.Button(xRect, "✕", EditorStyles.miniButton))
                    RemoveStepAt(index);
            };

            list.onCanRemoveCallback = l => l.count > 0;

            list.onRemoveCallback = l =>
            {
                RemoveStepAt(l.index);
            };
        }

        void DrawStepHeader(Rect r, int index, string kind, string displayName = null)
        {
            var left = new Rect(r.x, r.y, 50, r.height);
            EditorGUI.LabelField(left, $"{index:00}", Styles.Index);

            var badge = new Rect(left.xMax + 4, r.y + 1, 82, r.height - 2);
            Styles.DrawBadge(badge, kind);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var nameRect = new Rect(badge.xMax + 6, r.y, r.width - (badge.xMax - r.x) - 6, r.height);
                EditorGUI.LabelField(nameRect, displayName, Styles.Bold);
            }
        }

        static bool IsSessionMarker(SerializedProperty p)
        {
            string full = p?.managedReferenceFullTypename ?? "";
            return full.Contains(nameof(Runtime.SessionStartStep)) || full.Contains(nameof(Runtime.SessionStopStep));
        }

        void AddStep(Type t)
        {
            serializedObject.Update();
            int i = stepsProp.arraySize;
            stepsProp.InsertArrayElementAtIndex(i);
            var el = stepsProp.GetArrayElementAtIndex(i);
            el.managedReferenceValue = Activator.CreateInstance(t);
            serializedObject.ApplyModifiedProperties();
        }

        void RemoveStepAt(int index)
        {
            if (stepsProp == null) return;
            if (index < 0 || index >= stepsProp.arraySize) return;

            Undo.RecordObject(target, "Remove Step");

            // Capture the step before the delete so its editor-only display overrides
            // (guid-keyed side-table on the Scenario) die with it - mirrors the graph window.
            Runtime.Step removedStep = null;
            var elBefore = stepsProp.GetArrayElementAtIndex(index);
            if (elBefore != null && elBefore.propertyType == SerializedPropertyType.ManagedReference)
                removedStep = elBefore.managedReferenceValue as Runtime.Step;

            stepsProp.DeleteArrayElementAtIndex(index);

            if (index < stepsProp.arraySize)
            {
                var el = stepsProp.GetArrayElementAtIndex(index);
                bool isManaged = el != null &&
                                 el.propertyType == SerializedPropertyType.ManagedReference;
                if (isManaged && el.managedReferenceValue == null)
                    stepsProp.DeleteArrayElementAtIndex(index);
            }

            serializedObject.ApplyModifiedProperties();

            if (removedStep != null && target is Runtime.Scenario sc)
            {
                sc.RemoveStepGraphDisplayRecursive(removedStep);
                EditorUtility.SetDirty(sc);
            }

            GUI.FocusControl(null);
        }

        // ================== Routing ==================

        void DrawRouting(Runtime.Scenario sc)
        {
            if (!sc || sc.steps == null || sc.steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No steps yet.", MessageType.Info);
                return;
            }

            var names = new List<string> { "(next in list)" };
            var guids = new List<string> { "" };

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.guid)) s.guid = Guid.NewGuid().ToString();
                names.Add($"{i:00} • {s.Kind}");
                guids.Add(s.guid);
            }

            int Popup(string currentGuid)
            {
                int idx = Mathf.Max(0, guids.IndexOf(currentGuid));
                return EditorGUILayout.Popup(idx, names.ToArray());
            }

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null) continue;

                using (new EditorGUILayout.VerticalScope(Styles.OuterBox))
                {
                    EditorGUILayout.LabelField($"{i:00}  {s.Kind}", Styles.Bold);

                    if (s is Runtime.TimelineStep tl)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(tl.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != tl.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                tl.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.CueCardsStep cc)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(cc.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != cc.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                cc.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.QuestionStep q)
                    {
                        if (q.choices == null || q.choices.Count == 0)
                        {
                            EditorGUILayout.LabelField("No choices", Styles.Muted);
                        }
                        else
                        {
                            for (int c = 0; c < q.choices.Count; c++)
                            {
                                var ch = q.choices[c];
                                if (ch == null) continue;

                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label($"Choice {c}", GUILayout.Width(80));
                                    int choice = Popup(ch.nextGuid);
                                    string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                                    if (newGuid != ch.nextGuid)
                                    {
                                        Undo.RecordObject(sc, "Route Change");
                                        ch.nextGuid = newGuid;
                                        EditorUtility.SetDirty(sc);
                                    }
                                }
                            }
                        }
                    }
                    else if (s is Runtime.QuizStep qz)
                    {
                        if (qz.completion == Runtime.QuizStep.CompleteMode.BranchOnCorrectness)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label("Correct", GUILayout.Width(60));
                                int iCorr = Popup(qz.correctNextGuid);
                                string newGuid = guids[Mathf.Clamp(iCorr, 0, guids.Count - 1)];
                                if (newGuid != qz.correctNextGuid)
                                {
                                    Undo.RecordObject(sc, "Route Change");
                                    qz.correctNextGuid = newGuid;
                                    EditorUtility.SetDirty(sc);
                                }
                            }
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label("Wrong", GUILayout.Width(60));
                                int iWrong = Popup(qz.wrongNextGuid);
                                string newGuid = guids[Mathf.Clamp(iWrong, 0, guids.Count - 1)];
                                if (newGuid != qz.wrongNextGuid)
                                {
                                    Undo.RecordObject(sc, "Route Change");
                                    qz.wrongNextGuid = newGuid;
                                    EditorUtility.SetDirty(sc);
                                }
                            }
                        }
                        else
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label("Next", GUILayout.Width(60));
                                int choice = Popup(qz.nextGuid);
                                string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                                if (newGuid != qz.nextGuid)
                                {
                                    Undo.RecordObject(sc, "Route Change");
                                    qz.nextGuid = newGuid;
                                    EditorUtility.SetDirty(sc);
                                }
                            }
                        }
                    }
                    else if (s is Runtime.SelectionStep sel)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Correct", GUILayout.Width(60));
                            int iSel = Popup(sel.correctNextGuid);
                            string newGuid = guids[Mathf.Clamp(iSel, 0, guids.Count - 1)];
                            if (newGuid != sel.correctNextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                sel.correctNextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Wrong", GUILayout.Width(60));
                            int iWrong = Popup(sel.wrongNextGuid);
                            string newGuid = guids[Mathf.Clamp(iWrong, 0, guids.Count - 1)];
                            if (newGuid != sel.wrongNextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                sel.wrongNextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.InsertStep ins)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(ins.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != ins.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                ins.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.EventStep ev)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(ev.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != ev.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                ev.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.GroupStep g)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Next", GUILayout.Width(60));
                            int choice = Popup(g.nextGuid);
                            string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                            if (newGuid != g.nextGuid)
                            {
                                Undo.RecordObject(sc, "Route Change");
                                g.nextGuid = newGuid;
                                EditorUtility.SetDirty(sc);
                            }
                        }
                    }
                    else if (s is Runtime.ConditionsStep cnd)
                    {
                        if (cnd.outcomes == null || cnd.outcomes.Count == 0)
                        {
                            EditorGUILayout.LabelField("No outcomes", Styles.Muted);
                        }
                        else
                        {
                            for (int b = 0; b < cnd.outcomes.Count; b++)
                            {
                                var br = cnd.outcomes[b];
                                if (br == null) continue;
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label(string.IsNullOrEmpty(br.label) ? $"Branch {b}" : br.label, GUILayout.Width(80));
                                    int choice = Popup(br.nextGuid);
                                    string newGuid = guids[Mathf.Clamp(choice, 0, guids.Count - 1)];
                                    if (newGuid != br.nextGuid)
                                    {
                                        Undo.RecordObject(sc, "Route Change");
                                        br.nextGuid = newGuid;
                                        EditorUtility.SetDirty(sc);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // ================== Validation ==================

        void DrawValidation(Runtime.Scenario sc)
        {
            if (sc == null || sc.steps == null) return;
            int warnings = 0;

            for (int i = 0; i < sc.steps.Count; i++)
            {
                var s = sc.steps[i];
                if (s == null)
                {
                    Styles.Warn($"Step {i}: is null (remove it).");
                    warnings++;
                    continue;
                }

                if (s is Runtime.TimelineStep tl)
                {
                    if (!tl.director) { Styles.Warn($"Step {i}: Timeline has no Director."); warnings++; }
                }
                else if (s is Runtime.CueCardsStep cc)
                {
                    if (cc.cards == null || cc.cards.Length == 0)
                    { Styles.Warn($"Step {i}: Cue Cards has no cards."); warnings++; }

                    if (cc.cueTimes != null && cc.cueTimes.Length > 1 &&
                        (cc.cards == null || cc.cueTimes.Length != cc.cards.Length))
                        Styles.Info($"Step {i}: Cue Times 1 value (all cards) or match card count.");
                }
                else if (s is Runtime.QuestionStep q)
                {
                    if (!q.panelRoot)
                    { Styles.Warn($"Step {i}: Question has no Panel Root."); warnings++; }

                    if (q.choices == null || q.choices.Count == 0)
                    { Styles.Warn($"Step {i}: Question has no choices."); warnings++; }
                    else
                    {
                        for (int c = 0; c < q.choices.Count; c++)
                            if (q.choices[c] != null && !q.choices[c].button)
                            { Styles.Info($"Step {i} Choice {c}: Button not set."); }
                    }
                }
                else if (s is Runtime.QuizStep qz)
                {
                    if (qz.quiz == null)
                        Styles.Info($"Step {i}: Quiz has no QuizAsset assigned (will use LabConsole quiz if set).");
                    if (string.IsNullOrEmpty(qz.questionId) && qz.questionIndex < 0)
                        Styles.Info($"Step {i}: Quiz has no Question ID or Index set.");
                    if (qz.completion == Runtime.QuizStep.CompleteMode.BranchOnCorrectness)
                    {
                        if (string.IsNullOrEmpty(qz.correctNextGuid) && string.IsNullOrEmpty(qz.wrongNextGuid))
                            Styles.Info($"Step {i}: Quiz is BranchOnCorrectness but has no Correct/Wrong routes set.");
                    }
                }
                else if (s is Runtime.QuizResultsStep qrs)
                {
                    if (qrs.quiz == null)
                        Styles.Info($"Step {i}: Quiz Results has no QuizAsset assigned (will use LabConsole quiz if set).");

                    if (qrs.whenComplete == Runtime.QuizResultsStep.WhenComplete.AfterSeconds && qrs.completeAfterSeconds <= 0f)
                        Styles.Info($"Step {i}: Quiz Results is set to AfterSeconds but Complete After Seconds is 0 (will advance immediately).");

                    if (qrs.completion == Runtime.QuizResultsStep.CompleteMode.BranchOnPassed)
                    {
                        if (string.IsNullOrEmpty(qrs.passedNextGuid) && string.IsNullOrEmpty(qrs.failedNextGuid))
                            Styles.Info($"Step {i}: Quiz Results is BranchOnPassed but has no Passed/Failed routes set.");
                        Styles.Info($"Step {i}: BranchOnPassed requires QuizAsset.passThresholdPercent > 0 to be meaningful.");
                    }
                }
                else if (s is Runtime.SelectionStep sel)
                {
                    if (!sel.lists)
                    { Styles.Warn($"Step {i}: Selection has no SelectionLists reference."); warnings++; }

                    if (string.IsNullOrEmpty(sel.listKey) && sel.listIndex < 0)
                        Styles.Info($"Step {i}: Selection has neither List Key nor List Index set.");

                    if (sel.lists)
                    {
                        if (sel.listIndex >= sel.lists.Count)
                            Styles.Info($"Step {i}: List Index {sel.listIndex} is out of range (0..{sel.lists.Count - 1}).");

                        if (!string.IsNullOrEmpty(sel.listKey))
                        {
                            bool found = false;
                            for (int k = 0; k < sel.lists.Count; k++)
                                if (sel.lists.lists[k] != null && sel.lists.lists[k].name == sel.listKey) { found = true; break; }
                            if (!found)
                                Styles.Info($"Step {i}: List Key \"{sel.listKey}\" not found in SelectionLists.");
                        }
                    }

                    var comp = (Runtime.SelectionStep.CompleteMode)sel.completion;
                    if (comp == Runtime.SelectionStep.CompleteMode.OnSubmitButton && !sel.submitButton)
                        Styles.Info($"Step {i}: Selection is OnSubmitButton but Submit Button is not set.");

                    if (sel.requiredSelections <= 0)
                        Styles.Info($"Step {i}: Required Selections is 0 (step may pass immediately).");
                }
                else if (s is Runtime.InsertStep ins)
                {
                    if (!ins.item)
                    { Styles.Warn($"Step {i}: Insert has no Item assigned."); warnings++; }
                    if (!ins.targetTrigger)
                    { Styles.Warn($"Step {i}: Insert has no Target Trigger assigned."); warnings++; }
                    else if (!ins.targetTrigger.isTrigger)
                    {
                        Styles.Info($"Step {i}: Insert target collider is not marked as Trigger (recommended).");
                    }

                    if (ins.positionTolerance <= 0f)
                        Styles.Info($"Step {i}: Insert position tolerance is 0 or negative, step may never complete.");
                }
                else if (s is Runtime.EventStep ev)
                {
                    if (ev.onEnter == null || ev.onEnter.GetPersistentEventCount() == 0)
                        Styles.Info($"Step {i}: Event has no listeners. It will only wait {ev.waitSeconds} seconds then continue.");
                }
                else if (s is Runtime.GroupStep g)
                {
                    if (g.steps == null || g.steps.Count == 0)
                    {
                        Styles.Warn($"Step {i}: Group has no nested steps.");
                        warnings++;
                    }
                    else
                    {
                        for (int k = 0; k < g.steps.Count; k++)
                        {
                            var sub = g.steps[k];
                            if (sub == null)
                            {
                                Styles.Info($"Step {i}: Group contains a null nested step at index {k} (remove it).");
                                continue;
                            }

                            // Routing inside group is ignored at runtime; warn if authors set it.
                            if (sub is Runtime.TimelineStep subTl && !string.IsNullOrEmpty(subTl.nextGuid))
                                Styles.Info($"Step {i}: Group nested Timeline {k} has NextGuid set (ignored).");
                            else if (sub is Runtime.CueCardsStep subCc && !string.IsNullOrEmpty(subCc.nextGuid))
                                Styles.Info($"Step {i}: Group nested CueCards {k} has NextGuid set (ignored).");
                            else if (sub is Runtime.InsertStep subIns && !string.IsNullOrEmpty(subIns.nextGuid))
                                Styles.Info($"Step {i}: Group nested Insert {k} has NextGuid set (ignored).");
                            else if (sub is Runtime.EventStep ev2 && !string.IsNullOrEmpty(ev2.nextGuid))
                                Styles.Info($"Step {i}: Group nested Event {k} has NextGuid set (ignored).");
                            else if (sub is Runtime.SelectionStep sel2 &&
                                     (!string.IsNullOrEmpty(sel2.correctNextGuid) || !string.IsNullOrEmpty(sel2.wrongNextGuid)))
                                Styles.Info($"Step {i}: Group nested Selection {k} has branch guids set (ignored).");
                            else if (sub is Runtime.QuestionStep q2)
                            {
                                if (q2.choices != null)
                                {
                                    for (int c = 0; c < q2.choices.Count; c++)
                                    {
                                        if (q2.choices[c] != null && !string.IsNullOrEmpty(q2.choices[c].nextGuid))
                                        {
                                            Styles.Info($"Step {i}: Group nested Question {k} Choice {c} has NextGuid set (ignored).");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        if (g.completeWhen == Runtime.GroupStep.CompleteWhen.SpecificChildCompletes)
                        {
                            if (string.IsNullOrEmpty(g.specificStepGuid))
                                Styles.Warn($"Step {i}: Group completion is SpecificChildCompletes but SpecificStepGuid is empty.");
                            else
                            {
                                bool found = false;
                                foreach (var sub in g.steps)
                                    if (sub != null && sub.guid == g.specificStepGuid) { found = true; break; }
                                if (!found)
                                    Styles.Warn($"Step {i}: Group SpecificStepGuid does not match any nested step guid.");
                            }
                        }
                        if (g.completeWhen == Runtime.GroupStep.CompleteWhen.NOfMChildrenComplete)
                        {
                            int count = g.steps.Count;
                            if (g.requiredCount <= 0)
                                Styles.Warn($"Step {i}: Group N-of-M completion requires N >= 1.");
                            else if (g.requiredCount > count)
                                Styles.Warn($"Step {i}: Group N-of-M completion has N > child count.");
                        }
                    }
                }
            }

            // WS B1.6 Step 1: surface dangling ROUTE guids (the gate's Proof-A lint) + a safe one-click repair.
            int danglingRoutes = ScenarioGraphSnapshot.CountDanglingRoutes(sc);
            if (danglingRoutes > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"{danglingRoutes} route connection(s) point to a step that no longer exists. They fall through "
                    + "to the next step in list at runtime.", MessageType.Warning);
                if (GUILayout.Button("Clear Dangling Routes"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Clear Dangling Route GUIDs",
                        "Clears Next / branch route guids that point to non-existent steps, so they fall through to "
                        + "the next step in list. Use only after you confirm those steps are truly gone. Undo-able. "
                        + "The step list and group requirements are not touched.",
                        "Clear dangling", "Cancel"))
                    {
                        int cleared = ScenarioGraphSnapshot.RepairDanglingRoutes(sc);
                        Debug.Log($"[Scenario] Cleared {cleared} dangling route guid(s).");
                        serializedObject.Update();
                    }
                }
            }

            if (warnings == 0)
                EditorGUILayout.HelpBox("No blocking issues found.", MessageType.None);
        }

        // ================== Styles ==================

        static class Styles
        {
            static readonly Color cRowEven = new Color(0.16f, 0.18f, 0.22f);
            static readonly Color cRowOdd = new Color(0.14f, 0.16f, 0.19f);
            static readonly Color cBadgeTimeline = new Color(0.20f, 0.42f, 0.85f);
            static readonly Color cBadgeCards = new Color(0.32f, 0.62f, 0.32f);
            static readonly Color cBadgeQuestion = new Color(0.76f, 0.45f, 0.22f);
            static readonly Color cBadgeSelection = new Color(0.58f, 0.38f, 0.78f);
            static readonly Color cBadgeInsert = new Color(0.90f, 0.75f, 0.25f);
            static readonly Color cBadgeEvent = new Color(0.30f, 0.70f, 0.75f);
            static readonly Color cBadgeGroup = new Color(0.55f, 0.55f, 0.60f);
            static readonly Color cBadgeConditions = new Color(0.95f, 0.55f, 0.15f);
            // Analytics-related steps (Session Start/Stop) get a very distinguishing near-white badge.
            static readonly Color cBadgeAnalytics = new Color(0.93f, 0.93f, 0.96f);

            static bool _inited;

            public static GUIStyle SectionBox;
            public static readonly Color HeaderBg = new Color(0.11f, 0.12f, 0.15f);
            public static GUIStyle HeaderTitle;
            public static GUIStyle Bold;
            public static GUIStyle Small;
            public static GUIStyle Muted;
            public static GUIStyle Index;
            public static GUIStyle Mid;
            public static GUIStyle OuterBox;
            public static GUIStyle InfoBox;
            public static readonly Color RowEven = cRowEven;
            public static readonly Color RowOdd = cRowOdd;
            public static GUIStyle BigButton;

            public static void Ensure()
            {
                if (_inited) return;

                // During domain reload, static constructors that touch EditorStyles can NRE.
                // We initialize lazily the first time the inspector actually draws.
                if (EditorStyles.boldLabel == null) return;

                HeaderTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

                Bold = new GUIStyle(EditorStyles.boldLabel);
                Small = new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true };
                Muted = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.8f, 0.82f, 0.86f, 0.8f) } };
                Index = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };

                Mid = new GUIStyle(EditorStyles.miniButton);

                OuterBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
                InfoBox = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 8, 8) };

                BigButton = new GUIStyle(EditorStyles.miniButton)
                {
                    fixedHeight = 0,
                    stretchHeight = true,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(18, 18, 9, 9),
                    margin = new RectOffset(8, 8, 4, 8),
                    wordWrap = false,
                    clipping = TextClipping.Overflow,
                    contentOffset = new Vector2(0, 1)
                };

                SectionBox = new GUIStyle("HelpBox")
                {
                    margin = EditorStyles.helpBox.margin,
                    padding = new RectOffset(8, 8, 8, 8)
                };

                _inited = true;
            }

            public static bool Section(string title, bool open, Action drawBody)
            {
                Ensure();
                using (new EditorGUILayout.VerticalScope(SectionBox))
                {
                    var header = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(header, HeaderBg);

                    var foldRect = new Rect(header.x + 15, header.y + 3, header.width - 12, header.height - 6);
                    open = EditorGUI.Foldout(foldRect, open, title, true, HeaderTitle);

                    if (open)
                    {
                        EditorGUILayout.Space(2);
                        drawBody?.Invoke();
                    }
                }
                return open;
            }

            public static void DrawBadge(Rect r, string kind)
            {
                Ensure();
                var col = cBadgeTimeline;
                bool lightBadge = false;
                if (kind == "Cue Cards") col = cBadgeCards;
                else if (kind == "Question") col = cBadgeQuestion;
                else if (kind == "Selection") col = cBadgeSelection;
                else if (kind == "Insert") col = cBadgeInsert;
                else if (kind == "Event") col = cBadgeEvent;
                else if (kind == "Group") col = cBadgeGroup;
                else if (kind == "Conditions") col = cBadgeConditions;
                else if (kind == "Session Start" || kind == "Session Stop") { col = cBadgeAnalytics; lightBadge = true; }

                var bg = new Rect(r.x, r.y, r.width, r.height);
                EditorGUI.DrawRect(bg, col);
                var txt = new Rect(r.x + 6, r.y, r.width - 12, r.height);
                var labelStyle = new GUIStyle(lightBadge ? EditorStyles.boldLabel : EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleLeft };
                if (lightBadge) labelStyle.normal.textColor = Color.black;
                EditorGUI.LabelField(txt, kind, labelStyle);
            }

            public static void Warn(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Warning);
            public static void Info(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Info);
        }
    }
}
#endif
