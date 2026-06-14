#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    using Runtime = Pitech.XR.Scenario;

    // Per-step PropertyDrawers extracted verbatim from ScenarioEditor.cs (WS A6 Step 3). Same assembly +
    // namespace + Runtime alias, so this is a behaviour-neutral move (these drawers never referenced the
    // ScenarioEditor.Styles nested class, which stays put).

    [CustomPropertyDrawer(typeof(Runtime.TimelineStep))]
    class TimelineStepDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            h += PH(p, "director");
            h += PH(p, "rewindOnEnter");
            h += PH(p, "waitForEnd");
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            Draw(ref r, p, "director", "Director");
            Draw(ref r, p, "rewindOnEnter", "Rewind On Enter");
            Draw(ref r, p, "waitForEnd", "Wait For End");
        }
        static float PH(SerializedProperty p, string name)
        {
            var sp = p.FindPropertyRelative(name);
            float baseH = EditorGUIUtility.singleLineHeight;
            return ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : baseH)
                 + EditorGUIUtility.standardVerticalSpacing;
        }
        static void Draw(ref Rect r, SerializedProperty p, string name, string label)
        {
            var sp = p.FindPropertyRelative(name);
            if (sp == null) { r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; return; }
            var h = EditorGUI.GetPropertyHeight(sp, true);
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
            r.y += h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.CueCardsStep))]
    class CueCardsStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "director","cards","cueTimes",
            "autoShowFirst","tapHint",
            "advanceMode","nextButton",
            "extraObject","extraShowAtIndex","hideExtraWithFinalTap","useRenderersForExtra",
            "fadeDuration","popScale","popDuration","fadeCurve","scaleCurve"
        };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            var advProp = p.FindPropertyRelative("advanceMode");
            int adv = advProp != null ? advProp.enumValueIndex : 0;
            foreach (var f in fields)
            {
                if (f == "nextButton" && advProp != null && adv != (int)Runtime.CueCardsStep.AdvanceMode.OnButton)
                    continue;
                var sp = p.FindPropertyRelative(f);
                if (sp == null) { h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; continue; }
                h += EditorGUI.GetPropertyHeight(sp, true) + EditorGUIUtility.standardVerticalSpacing;

                if (f == "advanceMode" && advProp != null)
                    adv = advProp.enumValueIndex;
            }
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            var advProp = p.FindPropertyRelative("advanceMode");
            int adv = advProp != null ? advProp.enumValueIndex : 0;
            foreach (var f in fields)
            {
                if (f == "nextButton" && advProp != null && adv != (int)Runtime.CueCardsStep.AdvanceMode.OnButton)
                    continue;
                var sp = p.FindPropertyRelative(f);
                var nicified = ObjectNames.NicifyVariableName(f);
                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nicified);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nicified), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;

                if (f == "advanceMode" && advProp != null)
                    adv = advProp.enumValueIndex;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.QuestionStep))]
    class QuestionStepDrawer : PropertyDrawer
    {
        static readonly string[] fields = { "panelRoot", "panelAnimator", "showTrigger", "hideTrigger", "fallbackHideSeconds", "choices" };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                if (sp == null) { h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; continue; }
                h += EditorGUI.GetPropertyHeight(sp, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                var nicified = ObjectNames.NicifyVariableName(f);
                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nicified);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nicified), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.MiniQuizStep))]
    class MiniQuizStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "panelRoot","panelAnimator","showTrigger","hideTrigger",
            "completion","submitButton","lockQuestionAfterAnswer",
            "questions",
            "outcomes"
        };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0f;
            var completionProp = p.FindPropertyRelative("completion");
            int completionMode = completionProp != null ? completionProp.enumValueIndex : 0;

            foreach (var f in fields)
            {
                if (f == "submitButton" && completionMode != (int)Runtime.MiniQuizStep.CompleteMode.OnSubmitButton)
                    continue;

                var sp = p.FindPropertyRelative(f);
                h += (sp != null ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                     + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            var completionProp = p.FindPropertyRelative("completion");
            int completionMode = completionProp != null ? completionProp.enumValueIndex : 0;

            foreach (var f in fields)
            {
                if (f == "submitButton" && completionMode != (int)Runtime.MiniQuizStep.CompleteMode.OnSubmitButton)
                    continue;

                var sp = p.FindPropertyRelative(f);
                string label = ObjectNames.NicifyVariableName(f);
                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), label);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;

                if (f == "completion")
                    completionMode = completionProp != null ? completionProp.enumValueIndex : 0;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.Choice))]
    class ChoiceDrawer : PropertyDrawer
    {
        static string EventsFoldoutKey(SerializedProperty choiceProp)
        {
            // Persist per-choice (per object) foldout state for the onSelected UnityEvent.
            // This makes it behave like other list foldouts (Effects) instead of always taking lots of vertical space.
            var obj = choiceProp?.serializedObject?.targetObject;
            int id = obj != null ? obj.GetInstanceID() : 0;
            return $"PitechXR.ChoiceDrawer.EventsExpanded.{id}.{choiceProp.propertyPath}";
        }

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0;
            var btn = p.FindPropertyRelative("button");
            var ev = p.FindPropertyRelative("onSelected");
            var fx = p.FindPropertyRelative("effects");
            if (btn != null) h += EditorGUI.GetPropertyHeight(btn, true) + EditorGUIUtility.standardVerticalSpacing;
            if (ev != null)
            {
                // Default-collapsed, like Effects. Persist the foldout state per choice.
                ev.isExpanded = SessionState.GetBool(EventsFoldoutKey(p), false);
                h += EditorGUI.GetPropertyHeight(ev, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            if (fx != null) h += EditorGUI.GetPropertyHeight(fx, true) + EditorGUIUtility.standardVerticalSpacing;
            return h;
        }
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;
            var btn = p.FindPropertyRelative("button");
            var ev = p.FindPropertyRelative("onSelected");
            var fx = p.FindPropertyRelative("effects");

            if (btn != null)
            {
                var h0 = EditorGUI.GetPropertyHeight(btn, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h0), btn, new GUIContent("Button"), true);
                r.y += h0 + EditorGUIUtility.standardVerticalSpacing;
            }
            if (ev != null)
            {
                var key = EventsFoldoutKey(p);
                ev.isExpanded = SessionState.GetBool(key, false);
                bool before = ev.isExpanded;
                var hE = EditorGUI.GetPropertyHeight(ev, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, hE), ev, new GUIContent("Events"), true);
                r.y += hE + EditorGUIUtility.standardVerticalSpacing;
                if (before != ev.isExpanded)
                    SessionState.SetBool(key, ev.isExpanded);
            }
            if (fx != null)
            {
                var h1 = EditorGUI.GetPropertyHeight(fx, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h1), fx, new GUIContent("Effects"), true);
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.SelectionStep))]
    class SelectionStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "lists",
            "listKey","listIndex",
            "resetOnEnter",
            "completion","submitButton",
            "requiredSelections","requireExactCount","allowedWrong","timeoutSeconds",
            "panelRoot","panelAnimator","showTrigger","hideTrigger","hint",
            "onCorrectEffects","onWrongEffects",
            "onCorrect","onWrong"
        };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;

            var completionProp = p.FindPropertyRelative("completion");
            int completionMode = completionProp != null ? completionProp.enumValueIndex : 0;

            foreach (var f in fields)
            {
                if (f == "submitButton" && completionMode == 0)
                    continue;

                var sp = p.FindPropertyRelative(f);

                string label =
                    f == "listKey" ? "List Name" :
                    f == "listIndex" ? "(or) List Index" :
                    ObjectNames.NicifyVariableName(f);

                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), label);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;

                if (f == "completion")
                    completionMode = completionProp != null ? completionProp.enumValueIndex : 0;
            }
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.QuizStep))]
    class QuizStepDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0f;
            h += PH(p, "quiz");
            h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // question dropdown
            h += PH(p, "completion");
            h += PH(p, "submitMode");
            h += PH(p, "feedback");
            // feedbackSeconds is conditional; reserve a line to avoid clipping when toggled.
            h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;

            Draw(ref r, p, "quiz", "Quiz Asset");

            DrawQuestionPicker(ref r, p);

            Draw(ref r, p, "completion", "Routing");

            Draw(ref r, p, "submitMode", "Submit");
            Draw(ref r, p, "feedback", "Explanation");

            var feedbackProp = p.FindPropertyRelative("feedback");
            var secsProp = p.FindPropertyRelative("feedbackSeconds");
            if (feedbackProp != null && secsProp != null && feedbackProp.enumValueIndex == (int)Runtime.QuizStep.FeedbackMode.ForSeconds)
            {
                var h = EditorGUI.GetPropertyHeight(secsProp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), secsProp, new GUIContent("Explanation Seconds"), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        static void DrawQuestionPicker(ref Rect r, SerializedProperty p)
        {
            var quizProp = p.FindPropertyRelative("quiz");
            var idProp = p.FindPropertyRelative("questionId");
            var idxProp = p.FindPropertyRelative("questionIndex");
            if (quizProp == null || idProp == null)
            {
                r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                return;
            }

            var quizObj = quizProp.objectReferenceValue;
            if (quizObj == null)
            {
                EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), "Question", "No QuizAsset assigned");
                r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                return;
            }

            var so = new SerializedObject(quizObj);
            var questions = so.FindProperty("questions");
            if (questions == null || !questions.isArray || questions.arraySize == 0)
            {
                EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), "Question", "No questions in QuizAsset");
                r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                return;
            }

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
            int next = EditorGUI.Popup(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), "Question", cur, labels.ToArray());
            idProp.stringValue = ids[Mathf.Clamp(next, 0, ids.Count - 1)];
            if (idxProp != null)
                idxProp.intValue = next > 0 ? next - 1 : -1;
            r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        static float PH(SerializedProperty p, string name)
        {
            var sp = p.FindPropertyRelative(name);
            float baseH = EditorGUIUtility.singleLineHeight;
            return ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : baseH)
                 + EditorGUIUtility.standardVerticalSpacing;
        }

        static void Draw(ref Rect r, SerializedProperty p, string name, string label)
        {
            var sp = p.FindPropertyRelative(name);
            if (sp == null)
            {
                r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                return;
            }
            var h = EditorGUI.GetPropertyHeight(sp, true);
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
            r.y += h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.QuizResultsStep))]
    class QuizResultsStepDrawer : PropertyDrawer
    {
        static float PH(SerializedProperty p, string name)
        {
            var sp = p.FindPropertyRelative(name);
            float baseH = EditorGUIUtility.singleLineHeight;
            return ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : baseH)
                 + EditorGUIUtility.standardVerticalSpacing;
        }

        static void Draw(ref Rect r, SerializedProperty p, string name, string label)
        {
            var sp = p.FindPropertyRelative(name);
            if (sp == null)
            {
                r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                return;
            }
            var h = EditorGUI.GetPropertyHeight(sp, true);
            EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(label), true);
            r.y += h + EditorGUIUtility.standardVerticalSpacing;
        }

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            if (p == null) return 0f;
            float h = 0f;
            h += PH(p, "quiz");
            h += PH(p, "whenComplete");

            var wc = p.FindPropertyRelative("whenComplete");
            if (wc != null && wc.enumValueIndex == (int)Runtime.QuizResultsStep.WhenComplete.AfterSeconds)
                h += PH(p, "completeAfterSeconds");

            h += PH(p, "completion");
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
        {
            if (p == null) return;

            Draw(ref r, p, "quiz", "Quiz Asset");
            Draw(ref r, p, "whenComplete", "When Complete");

            var wc = p.FindPropertyRelative("whenComplete");
            if (wc != null && wc.enumValueIndex == (int)Runtime.QuizResultsStep.WhenComplete.AfterSeconds)
                Draw(ref r, p, "completeAfterSeconds", "Complete After Seconds");

            Draw(ref r, p, "completion", "Routing");
        }
    }

    [CustomPropertyDrawer(typeof(Runtime.InsertStep))]
    class InsertStepDrawer : PropertyDrawer
    {
        static readonly string[] fields =
        {
            "item",
            "targetTrigger","attachTransform",
            "smoothAttach","parentToAttach","moveSpeed","rotateSpeed",
            "positionTolerance","angleTolerance"
        };


        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            if (p == null) return;

            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                string nice =
                    f == "item" ? "Item" :
                    f == "targetTrigger" ? "Target Trigger" :
                    f == "attachTransform" ? "Attach Transform" :
                    f == "smoothAttach" ? "Smooth Attach" :
                    f == "parentToAttach" ? "Parent To Attach Point" :
                    ObjectNames.NicifyVariableName(f);


                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nice);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nice), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }
    [CustomPropertyDrawer(typeof(Runtime.EventStep))]
    class EventStepDrawer : PropertyDrawer
    {
        static readonly string[] fields = { "onEnter", "waitSeconds" };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            if (p == null) return;

            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                string nice =
                    f == "onEnter" ? "On Enter Events" :
                    f == "waitSeconds" ? "Wait Seconds Before Next" :
                    ObjectNames.NicifyVariableName(f);

                if (sp == null)
                {
                    EditorGUI.LabelField(new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight), nice);
                    r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    continue;
                }

                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nice), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    /// <summary>Hides nextGuid (routing via graph ports). Shows label, compareOp, compareValue.</summary>
    [CustomPropertyDrawer(typeof(Runtime.ConditionOutcome))]
    class ConditionOutcomeDrawer : PropertyDrawer
    {
        static readonly string[] fields = { "label", "compareOp", "compareValue" };

        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            if (p == null) return 0f;
            float h = 0f;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                h += ((sp != null) ? EditorGUI.GetPropertyHeight(sp, true) : EditorGUIUtility.singleLineHeight)
                   + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }

        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            if (p == null) return;
            foreach (var f in fields)
            {
                var sp = p.FindPropertyRelative(f);
                string nice = f == "compareOp" ? "Compare" : f == "compareValue" ? "Value" : "Label";
                if (sp == null) continue;
                var h = EditorGUI.GetPropertyHeight(sp, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), sp, new GUIContent(nice), true);
                r.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }
}
#endif
