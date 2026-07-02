using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Analytics;

namespace Pitech.XR.Analytics.Editor
{
    // 'Scenario' as a bare name binds to the Pitech.XR.Scenario NAMESPACE here; alias it to the type.
    using Scenario = Pitech.XR.Scenario.Scenario;

    // ---------- LabAnalytics inspector: the v3 config builder (base - penalties + bonus) ----------
    // Second partial of LabAnalyticsEditor (the first holds Auto-detect / Auto-wire + ResolveScenario). This
    // file is the serializedObject-driven authoring UI for the v3 model:
    //   * GRADE BUDGET header - what a student can actually score (base 100, up to -X penalties, up to +Y bonus).
    //   * BASE (step analytics) - READ-ONLY here; authored on step nodes in the Scenario Graph. Shows each step's
    //     weight + live share of the base + gate badges.
    //   * PENALTIES - editable point deductions (red), the scene-wide safety net.
    //   * GOALS - editable pass/fail bonus lines (extra credit only).
    //   * TRACKED OBJECTS - the subjects registry (drops / wrong / order).
    // Role capacities live on the SessionRoleSelector (not here). The grading formula is in AnalyticsGradeEngine.

    public sealed partial class LabAnalyticsEditor : UnityEditor.Editor
    {
        const string FoldKeyBase       = "pitech.xr.analytics.fold.base";
        const string FoldKeyPenalties  = "pitech.xr.analytics.fold.penalties";
        const string FoldKeyGoals      = "pitech.xr.analytics.fold.goals";
        const string FoldKeySubjects   = "pitech.xr.analytics.fold.subjects";

        bool foldBase;
        bool foldPenalties;
        bool foldGoals;
        bool foldSubjects;
        bool _foldsLoaded;

        void EnsureFolds()
        {
            if (_foldsLoaded) return;
            foldBase      = EditorPrefs.GetBool(FoldKeyBase, true);
            foldPenalties = EditorPrefs.GetBool(FoldKeyPenalties, true);
            foldGoals     = EditorPrefs.GetBool(FoldKeyGoals, true);
            foldSubjects  = EditorPrefs.GetBool(FoldKeySubjects, false);
            _foldsLoaded = true;
        }

        // ---------- accents ----------
        static readonly Color AccentBase    = new Color(0.27f, 0.56f, 0.99f, 1f);   // blue  = the base (steps)
        static readonly Color AccentPenalty = new Color(0.86f, 0.30f, 0.28f, 1f);   // red   = deductions
        static readonly Color AccentGoal    = new Color(0.95f, 0.72f, 0.26f, 1f);   // amber = bonus

        static void AccentBar(Rect card, Color c)
        {
            if (Event.current.type == EventType.Repaint && card.height > 1f)
                EditorGUI.DrawRect(new Rect(card.x + 1f, card.y + 1f, 5f, card.height - 2f), c);
        }

        static void ColoredBold(string text, Color c)
        {
            var st = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = c } };
            EditorGUILayout.LabelField(text, st);
        }

        static GUIStyle BoldFoldout => new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 };

        bool DrawSection(string title, ref bool fold, string prefKey, string help)
        {
            bool open = EditorGUILayout.Foldout(fold, title, true, BoldFoldout);
            if (open != fold) { fold = open; EditorPrefs.SetBool(prefKey, open); }
            if (open && !string.IsNullOrEmpty(help))
            {
                EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2);
            }
            return open;
        }

        static Color ColorForStep(Pitech.XR.Scenario.Step s)
        {
            switch (s)
            {
                case Pitech.XR.Scenario.TimelineStep _:    return new Color(0.20f, 0.42f, 0.85f);
                case Pitech.XR.Scenario.CueCardsStep _:    return new Color(0.32f, 0.62f, 0.32f);
                case Pitech.XR.Scenario.QuestionStep _:    return new Color(0.76f, 0.45f, 0.22f);
                case Pitech.XR.Scenario.MiniQuizStep _:    return new Color(0.62f, 0.34f, 0.16f);
                case Pitech.XR.Scenario.QuizStep _:        return new Color(0.78f, 0.20f, 0.20f);
                case Pitech.XR.Scenario.QuizResultsStep _: return new Color(0.62f, 0.16f, 0.16f);
                case Pitech.XR.Scenario.SelectionStep _:   return new Color(0.58f, 0.38f, 0.78f);
                case Pitech.XR.Scenario.InsertStep _:      return new Color(0.90f, 0.75f, 0.25f);
                case Pitech.XR.Scenario.EventStep _:       return new Color(0.25f, 0.70f, 0.70f);
                case Pitech.XR.Scenario.GroupStep _:       return new Color(0.55f, 0.55f, 0.60f);
                case Pitech.XR.Scenario.ConditionsStep _:  return new Color(0.70f, 0.38f, 0.08f);
                default:                                    return new Color(0.60f, 0.62f, 0.68f);
            }
        }

        // ======================================================================================
        void DrawConfigBuilder()
        {
            EnsureFolds();
            serializedObject.Update();

            SerializedProperty configP = serializedObject.FindProperty("config");
            if (configP == null)
            {
                EditorGUILayout.HelpBox("Could not find the 'config' property. Recompile and reopen this inspector.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Analytics configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "GRADE (0-100) = BASE - PENALTIES + BONUS.  Base = the weighted score of the step analytics " +
                "(authored in the Scenario Graph). Penalties deduct grade points scene-wide. Goals add bonus points.",
                MessageType.None);

            Scenario scenario = ResolveScenario((LabAnalytics)target);

            SerializedProperty analyticsP = configP.FindPropertyRelative("analytics");
            SerializedProperty penaltiesP = configP.FindPropertyRelative("penalties");
            SerializedProperty goalsP     = configP.FindPropertyRelative("goals");
            SerializedProperty subjectsP  = configP.FindPropertyRelative("subjects");

            DrawGradeBudget(analyticsP, penaltiesP, goalsP);
            DrawValidationSummary(analyticsP, penaltiesP, goalsP, scenario);
            DrawOrphanStepAnalytics(configP, scenario);

            // SECTION 1 - BASE (step analytics; read-only, authored in the graph).
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (DrawSection("1.  Base grade  (step analytics)", ref foldBase, FoldKeyBase,
                        "The weighted score of your step analytics IS the base grade. Authored on step nodes in the " +
                        "Scenario Graph (open a step's ANALYTIC brick to edit its metrics + weight). Read-only here.")
                    && analyticsP != null)
                    DrawBaseSection(analyticsP, scenario);
            }

            // SECTION 2 - PENALTIES.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (DrawSection("2.  Penalties  (point deductions)", ref foldPenalties, FoldKeyPenalties,
                        "Scene-wide deductions in grade points. They count occurrences ANYWHERE in the run (even on " +
                        "steps with no analytic). Use these - or a critical metric - for things that must not happen.")
                    && penaltiesP != null)
                    DrawPenaltiesSection(penaltiesP);
            }

            // SECTION 3 - GOALS.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (DrawSection("3.  Goals  (bonus points)", ref foldGoals, FoldKeyGoals,
                        "Extra credit: pass a goal -> add its bonus points. A goal can only ADD points - it can never " +
                        "require anything (use a Penalty or a critical metric for must-not-happen). Voided if a step fails.")
                    && goalsP != null)
                    DrawGoalsSection(goalsP, analyticsP, scenario);
            }

            // SECTION 4 - TRACKED OBJECTS.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (DrawSection("4.  Tracked Objects  (the props)", ref foldSubjects, FoldKeySubjects,
                        "The objects the learner handles - the registry powering Drop, Wrong-interaction and Order " +
                        "(steps AND penalties). Use Auto-detect (top) to fill from the scenario; add distractors by hand.")
                    && subjectsP != null)
                    EditorGUILayout.PropertyField(subjectsP, new GUIContent("List", "The tracked-object entries."), true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- Grade budget header ----------
        void DrawGradeBudget(SerializedProperty analyticsP, SerializedProperty penaltiesP, SerializedProperty goalsP)
        {
            int steps = CountStepAnalytics(analyticsP);
            bool uncapped; string uncappedName;
            int maxDeduct = SumPenaltyCaps(penaltiesP, out uncapped, out uncappedName);
            int maxBonus = SumGoalBonus(goalsP);
            int floor = Mathf.Max(0, 100 - maxDeduct);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Grade budget", EditorStyles.miniBoldLabel);
                string baseLine = steps > 0 ? $"Base 100  ({steps} step" + (steps == 1 ? "" : "s") + ")" : "Base 100  (pure-penalty lab - no steps)";
                string penLine = uncapped ? $"Penalties up to  -unbounded  (set a cap on: {uncappedName})" : $"Penalties up to  -{maxDeduct}";
                string bonusLine = $"Goals up to  +{maxBonus}";
                EditorGUILayout.LabelField(baseLine + "     " + penLine + "     " + bonusLine, EditorStyles.miniLabel);
                string range = uncapped ? "Student can land in:  [0 .. 100]" : $"Student can land in:  [{floor} .. 100]";
                EditorGUILayout.LabelField(range, EditorStyles.miniLabel);
            }
        }

        // ---------- SECTION 1: Base (read-only step analytics) ----------
        void DrawBaseSection(SerializedProperty analyticsP, Scenario scenario)
        {
            float sumW = SumStepWeights(analyticsP);
            bool any = false;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("<missing analytic>", EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove null entry", GUILayout.Width(140)))
                        {
                            RemoveManagedElement(analyticsP, i);
                            serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }
                    continue;
                }
                if (!(el.managedReferenceValue is StepAnalytic sa)) continue;   // v3: only step analytics
                any = true;

                Color accent = new Color(0.60f, 0.62f, 0.68f);
                var step = FindStepByGuid(scenario != null ? scenario.steps : null, sa.stepGuid);
                if (step != null) accent = ColorForStep(step);

                using (var card = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    AccentBar(card.rect, accent);
                    float share = sumW > 0f ? (sa.weight / sumW) * 100f : 0f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("[Step] " + StepAnalyticDisplay(el, scenario), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField($"importance {Mathf.Clamp(Mathf.RoundToInt(sa.weight), 1, 5)}/5  ~{Mathf.RoundToInt(share)}% of base", EditorStyles.miniLabel, GUILayout.Width(180));
                    }
                    string summary = MetricsSummary(el.FindPropertyRelative("metrics"), out bool anyCritical);
                    EditorGUILayout.LabelField("metrics: " + (string.IsNullOrEmpty(summary) ? "(none)" : summary), EditorStyles.miniLabel);
                    if (anyCritical || sa.failsScenario)
                    {
                        var st = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = AccentPenalty } };
                        string tag = anyCritical ? "critical gate" : "";
                        if (sa.failsScenario) tag += (tag.Length > 0 ? " - " : "") + "FAILS SCENARIO";
                        EditorGUILayout.LabelField("(!) " + tag, st);
                    }
                }
            }
            if (!any) EditorGUILayout.LabelField("No step analytics yet. Add them from step nodes in the Scenario Graph.", EditorStyles.miniLabel);
        }

        // ---------- SECTION 2: Penalties ----------
        void DrawPenaltiesSection(SerializedProperty penaltiesP)
        {
            for (int i = 0; i < penaltiesP.arraySize; i++)
            {
                SerializedProperty p = penaltiesP.GetArrayElementAtIndex(i);
                using (var card = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    AccentBar(card.rect, AccentPenalty);
                    SerializedProperty kindP = p.FindPropertyRelative("kind");
                    var kind = (PenaltyKind)kindP.enumValueIndex;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        ColoredBold("Penalty", AccentPenalty);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("X", "Remove this penalty"), GUILayout.Width(22)))
                        {
                            penaltiesP.DeleteArrayElementAtIndex(i);
                            serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.PropertyField(p.FindPropertyRelative("label"), new GUIContent("Name", "Shown on the readout, e.g. \"Dropped instruments\"."));
                    EditorGUILayout.PropertyField(kindP, new GUIContent("Counts"));
                    if (kind == PenaltyKind.Signal)
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("signalId"), new GUIContent("Signal id", "Must match your AnalyticsSignalEmitter's id."));

                    if (kind == PenaltyKind.TotalDuration)
                        DrawPenaltyTiers(p.FindPropertyRelative("tiers"));
                    else
                        DrawIntField(p.FindPropertyRelative("pointsPerWarning"), "Points per warning", "Deducted per warning-severity occurrence (e.g. a distractor drop).");
                    if (kind != PenaltyKind.TotalDuration)
                        DrawIntField(p.FindPropertyRelative("pointsPerError"), "Points per error", "Deducted per error-severity occurrence (e.g. a relevant-item drop).");

                    DrawIntField(p.FindPropertyRelative("maxDeduction"), "Max deduction (0 = uncapped)", "Cap the total this penalty can subtract.");

                    SerializedProperty failP = p.FindPropertyRelative("failScenario");
                    bool fail = EditorGUILayout.ToggleLeft(new GUIContent("Fail the whole scenario", "An error-severity occurrence (or a crossed tier) FAILS the scenario: grade 0."), failP.boolValue);
                    if (fail != failP.boolValue) failP.boolValue = fail;
                    if (fail) EditorGUILayout.LabelField("   grade 0 on any error occurrence - use sparingly.", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = AccentPenalty } });

                    EditorGUILayout.PropertyField(p.FindPropertyRelative("notifyInScene"), new GUIContent("Notify in scene"));
                }
                if (i >= penaltiesP.arraySize) break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Penalty")))
                    AddPenalty(penaltiesP);
            }
        }

        void DrawPenaltyTiers(SerializedProperty tiersP)
        {
            EditorGUILayout.LabelField("Time tiers (highest crossed applies)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < tiersP.arraySize; i++)
            {
                SerializedProperty t = tiersP.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Over", GUILayout.Width(34));
                    SerializedProperty over = t.FindPropertyRelative("overSeconds");
                    over.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(over.floatValue, GUILayout.Width(60)));
                    EditorGUILayout.LabelField("s  ->  -", GUILayout.Width(44));
                    SerializedProperty pts = t.FindPropertyRelative("points");
                    pts.intValue = Mathf.Max(0, EditorGUILayout.IntField(pts.intValue, GUILayout.Width(50)));
                    EditorGUILayout.LabelField("pts", GUILayout.Width(28));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("X", "Remove tier"), GUILayout.Width(22))) { tiersP.DeleteArrayElementAtIndex(i); break; }
                }
            }
            if (GUILayout.Button("+ Time tier", GUILayout.Width(90)))
            {
                int i = tiersP.arraySize;
                tiersP.InsertArrayElementAtIndex(i);
                SerializedProperty t = tiersP.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("overSeconds").floatValue = 0f;
                t.FindPropertyRelative("points").intValue = 5;
            }
        }

        // ---------- SECTION 3: Goals ----------
        void DrawGoalsSection(SerializedProperty goalsP, SerializedProperty analyticsP, Scenario scenario)
        {
            for (int i = 0; i < goalsP.arraySize; i++)
            {
                SerializedProperty g = goalsP.GetArrayElementAtIndex(i);
                using (var card = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    AccentBar(card.rect, AccentGoal);
                    SerializedProperty kindP = g.FindPropertyRelative("kind");
                    var kind = (GoalKind)kindP.enumValueIndex;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        ColoredBold("Goal", AccentGoal);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("X", "Remove this goal"), GUILayout.Width(22)))
                        {
                            goalsP.DeleteArrayElementAtIndex(i);
                            serializedObject.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.PropertyField(g.FindPropertyRelative("label"), new GUIContent("Name", "Shown on the readout, e.g. \"Finish under 2 minutes\"."));
                    DrawIntField(g.FindPropertyRelative("bonusPoints"), "Bonus points", "Added to the grade when this goal passes (voided if a step fails).");
                    EditorGUILayout.PropertyField(kindP, new GUIContent("Pass condition"));

                    SerializedProperty thr = g.FindPropertyRelative("threshold");
                    switch (kind)
                    {
                        case GoalKind.StepsScore:
                            thr.floatValue = EditorGUILayout.Slider(new GUIContent("Pass if step score >= (%)"), thr.floatValue, 0f, 100f);
                            EditorGUILayout.PropertyField(g.FindPropertyRelative("stepAnalyticIds"), new GUIContent("Steps (empty = all)"), true);
                            break;
                        case GoalKind.TotalTimeUnder:
                            thr.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Pass if total time <= (s)"), thr.floatValue));
                            EditorGUILayout.LabelField("   = " + FormatMinSec(thr.floatValue), EditorStyles.miniLabel);
                            break;
                        case GoalKind.MaxOccurrences:
                            thr.floatValue = Mathf.Max(0f, EditorGUILayout.IntField(new GUIContent("Pass if count <="), Mathf.RoundToInt(thr.floatValue)));
                            EditorGUILayout.PropertyField(g.FindPropertyRelative("countKind"), new GUIContent("Of"));
                            if ((CountKind)g.FindPropertyRelative("countKind").enumValueIndex == CountKind.Signal)
                                EditorGUILayout.PropertyField(g.FindPropertyRelative("signalId"), new GUIContent("Signal id"));
                            break;
                    }
                }
                if (i >= goalsP.arraySize) break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Goal")))
                    AddGoal(goalsP);
            }
        }

        // ---------- add helpers ----------
        void AddPenalty(SerializedProperty penaltiesP)
        {
            int i = penaltiesP.arraySize;
            penaltiesP.InsertArrayElementAtIndex(i);
            SerializedProperty p = penaltiesP.GetArrayElementAtIndex(i);
            SetStr(p, "id", UniqueId("penalty", CollectIds(penaltiesP, "id")));
            SetStr(p, "label", "Penalty");
            p.FindPropertyRelative("kind").enumValueIndex = (int)PenaltyKind.Drop;
            p.FindPropertyRelative("pointsPerWarning").intValue = 2;
            p.FindPropertyRelative("pointsPerError").intValue = 5;
            p.FindPropertyRelative("maxDeduction").intValue = 20;
            p.FindPropertyRelative("failScenario").boolValue = false;
            p.FindPropertyRelative("notifyInScene").boolValue = true;
            SerializedProperty tiers = p.FindPropertyRelative("tiers"); if (tiers != null) tiers.ClearArray();
            SetStr(p, "signalId", string.Empty);
        }

        void AddGoal(SerializedProperty goalsP)
        {
            int i = goalsP.arraySize;
            goalsP.InsertArrayElementAtIndex(i);
            SerializedProperty g = goalsP.GetArrayElementAtIndex(i);
            SetStr(g, "id", UniqueId("goal", CollectIds(goalsP, "id")));
            SetStr(g, "label", "Goal");
            g.FindPropertyRelative("bonusPoints").intValue = 10;
            g.FindPropertyRelative("kind").enumValueIndex = (int)GoalKind.StepsScore;
            g.FindPropertyRelative("threshold").floatValue = 70f;
            g.FindPropertyRelative("countKind").enumValueIndex = (int)CountKind.Drop;
            SetStr(g, "signalId", string.Empty);
            SerializedProperty ids = g.FindPropertyRelative("stepAnalyticIds"); if (ids != null) ids.ClearArray();
        }

        static void DrawIntField(SerializedProperty p, string label, string tip)
        {
            if (p == null) return;
            p.intValue = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent(label, tip), p.intValue));
        }

        static void SetStr(SerializedProperty parent, string rel, string value)
        {
            SerializedProperty p = parent.FindPropertyRelative(rel);
            if (p != null) p.stringValue = value;
        }

        static string FormatMinSec(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ---------- [SerializeReference] element delete (step analytics; mirror ScenarioEditor) ----------
        void RemoveManagedElement(SerializedProperty listProp, int index)
        {
            if (listProp == null || index < 0 || index >= listProp.arraySize) return;
            listProp.DeleteArrayElementAtIndex(index);
            if (index < listProp.arraySize)
            {
                SerializedProperty el = listProp.GetArrayElementAtIndex(index);
                if (el != null && el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                    listProp.DeleteArrayElementAtIndex(index);
            }
        }

        // ---------- displays / counts / ids ----------
        static string MetricKindLabel(object metric)
        {
            return metric switch
            {
                StepDurationMetric _ => "Step duration",
                DropMetric _ => "Drop",
                WrongInteractionMetric _ => "Wrong interaction",
                OrderMetric _ => "Order",
                SignalMetric _ => "Signal",
                _ => "Metric"
            };
        }

        static string MetricsSummary(SerializedProperty metricsP, out bool anyCritical)
        {
            anyCritical = false;
            if (metricsP == null || metricsP.arraySize == 0) return null;
            var parts = new List<string>();
            for (int i = 0; i < metricsP.arraySize; i++)
            {
                SerializedProperty el = metricsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
                object mv = el.managedReferenceValue;
                string lbl = MetricKindLabel(mv);
                SerializedProperty critP = el.FindPropertyRelative("critical");
                if (critP != null && critP.boolValue) { anyCritical = true; lbl += "*"; }
                parts.Add(lbl);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        static string StepAnalyticDisplay(SerializedProperty el, Scenario scenario)
        {
            SerializedProperty guidP = el.FindPropertyRelative("stepGuid");
            string guid = guidP != null ? guidP.stringValue : null;
            string name = null, type = null;
            if (scenario != null && !string.IsNullOrEmpty(guid))
            {
                var disp = scenario.FindStepGraphDisplay(guid);
                if (disp != null && !string.IsNullOrEmpty(disp.displayName)) name = disp.displayName;
                Pitech.XR.Scenario.Step step = FindStepByGuid(scenario.steps, guid);
                if (step != null) type = step.Kind;
            }
            if (!string.IsNullOrEmpty(type))
                return string.IsNullOrEmpty(name) ? ("(" + type + ")") : (name + " (" + type + ")");
            if (!string.IsNullOrEmpty(name)) return name;
            SerializedProperty labelP = el.FindPropertyRelative("label");
            string label = labelP != null ? labelP.stringValue : null;
            return !string.IsNullOrEmpty(label) ? label : "step analytic";
        }

        static Pitech.XR.Scenario.Step FindStepByGuid(List<Pitech.XR.Scenario.Step> steps, string guid)
        {
            if (steps == null || string.IsNullOrEmpty(guid)) return null;
            for (int i = 0; i < steps.Count; i++)
            {
                Pitech.XR.Scenario.Step s = steps[i];
                if (s == null) continue;
                if (s.guid == guid) return s;
                if (s is Pitech.XR.Scenario.GroupStep g)
                {
                    Pitech.XR.Scenario.Step nested = FindStepByGuid(g.steps, guid);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        static int CountStepAnalytics(SerializedProperty analyticsP)
        {
            int n = 0;
            if (analyticsP == null) return 0;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.managedReferenceValue is StepAnalytic) n++;
            }
            return n;
        }

        static float SumStepWeights(SerializedProperty analyticsP)
        {
            float sum = 0f;
            if (analyticsP == null) return 0f;
            for (int i = 0; i < analyticsP.arraySize; i++)
                if (analyticsP.GetArrayElementAtIndex(i).managedReferenceValue is StepAnalytic sa) sum += sa.weight;
            return sum;
        }

        static int SumPenaltyCaps(SerializedProperty penaltiesP, out bool uncapped, out string uncappedName)
        {
            uncapped = false; uncappedName = null; int sum = 0;
            if (penaltiesP == null) return 0;
            for (int i = 0; i < penaltiesP.arraySize; i++)
            {
                SerializedProperty p = penaltiesP.GetArrayElementAtIndex(i);
                int cap = p.FindPropertyRelative("maxDeduction").intValue;
                if (cap <= 0) { uncapped = true; if (uncappedName == null) uncappedName = p.FindPropertyRelative("label").stringValue; }
                else sum += cap;
            }
            return sum;
        }

        static int SumGoalBonus(SerializedProperty goalsP)
        {
            int sum = 0;
            if (goalsP == null) return 0;
            for (int i = 0; i < goalsP.arraySize; i++)
                sum += goalsP.GetArrayElementAtIndex(i).FindPropertyRelative("bonusPoints").intValue;
            return sum;
        }

        // Returns a HashSet so it binds to the sibling partial's UniqueId(string, HashSet<string>) overload
        // (defined in LabAnalyticsEditor.cs) - no duplicate UniqueId here.
        static HashSet<string> CollectIds(SerializedProperty listP, string idField)
        {
            var ids = new HashSet<string>();
            if (listP == null) return ids;
            for (int i = 0; i < listP.arraySize; i++)
            {
                SerializedProperty idP = listP.GetArrayElementAtIndex(i).FindPropertyRelative(idField);
                if (idP != null && !string.IsNullOrEmpty(idP.stringValue)) ids.Add(idP.stringValue);
            }
            return ids;
        }

        // ---------- Orphan cleanup ----------
        void DrawOrphanStepAnalytics(SerializedProperty configP, Scenario scenario)
        {
            if (scenario == null || configP == null) return;
            SerializedProperty analyticsP = configP.FindPropertyRelative("analytics");
            if (analyticsP == null || analyticsP.arraySize == 0) return;

            var valid = new HashSet<string>();
            CollectScenarioStepGuids(scenario.steps, valid);

            var orphanIdx = new List<int>();
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (!(el.managedReferenceValue is StepAnalytic)) continue;
                SerializedProperty gP = el.FindPropertyRelative("stepGuid");
                string g = gP != null ? gP.stringValue : null;
                if (string.IsNullOrEmpty(g) || valid.Contains(g)) continue;
                orphanIdx.Add(i);
            }
            if (orphanIdx.Count == 0) return;

            EditorGUILayout.HelpBox(orphanIdx.Count + " step analytic(s) point to a step that no longer exists. They score nothing - remove them.", MessageType.Warning);
            if (GUILayout.Button("Remove " + orphanIdx.Count + " orphaned step analytic(s)"))
            {
                for (int k = orphanIdx.Count - 1; k >= 0; k--) RemoveManagedElement(analyticsP, orphanIdx[k]);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        static void CollectScenarioStepGuids(List<Pitech.XR.Scenario.Step> steps, HashSet<string> into)
        {
            if (steps == null) return;
            for (int i = 0; i < steps.Count; i++)
            {
                Pitech.XR.Scenario.Step s = steps[i];
                if (s == null) continue;
                if (!string.IsNullOrEmpty(s.guid)) into.Add(s.guid);
                if (s is Pitech.XR.Scenario.GroupStep g) CollectScenarioStepGuids(g.steps, into);
            }
        }

        // ---------- Validation (v3 lints) ----------
        void DrawValidationSummary(SerializedProperty analyticsP, SerializedProperty penaltiesP, SerializedProperty goalsP, Scenario scenario)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Base
            int stepCount = 0; float sumW = 0f;
            if (analyticsP != null)
                for (int i = 0; i < analyticsP.arraySize; i++)
                {
                    if (!(analyticsP.GetArrayElementAtIndex(i).managedReferenceValue is StepAnalytic sa)) continue;
                    stepCount++; sumW += sa.weight;
                    if (sa.metrics == null || sa.metrics.Count == 0)
                        warnings.Add($"Step analytic '{(string.IsNullOrEmpty(sa.label) ? sa.id : sa.label)}' has no metrics - it won't affect the grade.");
                }
            if (stepCount > 0 && Mathf.Approximately(sumW, 0f))
                errors.Add("All step weights are 0 - the base grade can't be computed.");

            // Penalties
            int totalCaps = 0; bool anyUncapped = false;
            if (penaltiesP != null)
                for (int i = 0; i < penaltiesP.arraySize; i++)
                {
                    SerializedProperty p = penaltiesP.GetArrayElementAtIndex(i);
                    var kind = (PenaltyKind)p.FindPropertyRelative("kind").enumValueIndex;
                    string nm = p.FindPropertyRelative("label").stringValue;
                    int cap = p.FindPropertyRelative("maxDeduction").intValue;
                    if (cap <= 0) anyUncapped = true; else totalCaps += cap;
                    if (kind == PenaltyKind.TotalDuration)
                    {
                        SerializedProperty tiers = p.FindPropertyRelative("tiers");
                        float prevOver = -1f; int prevPts = -1; bool nonMono = false; bool anyPts = false;
                        for (int t = 0; t < tiers.arraySize; t++)
                        {
                            float over = tiers.GetArrayElementAtIndex(t).FindPropertyRelative("overSeconds").floatValue;
                            int pts = tiers.GetArrayElementAtIndex(t).FindPropertyRelative("points").intValue;
                            if (pts > 0) anyPts = true;
                            if (over > prevOver && prevPts >= 0 && pts < prevPts) nonMono = true;
                            prevOver = over; prevPts = pts;
                        }
                        if (nonMono) errors.Add($"Penalty '{nm}': a longer time tier deducts FEWER points (non-monotonic) - the slower student pays less.");
                        if (!anyPts) warnings.Add($"Penalty '{nm}': all tiers are 0 points - it deducts nothing.");
                    }
                    else
                    {
                        int w = p.FindPropertyRelative("pointsPerWarning").intValue;
                        int e = p.FindPropertyRelative("pointsPerError").intValue;
                        if (w == 0 && e == 0) warnings.Add($"Penalty '{nm}': 0 points - it deducts nothing.");
                    }
                }
            if (!anyUncapped && totalCaps >= 100)
                warnings.Add($"Possible deductions total {totalCaps} points - a run can floor at 0.");
            if (anyUncapped)
                warnings.Add("An uncapped penalty exists - worst-case deduction is unbounded. Set a Max deduction.");

            // Goals
            int totalBonus = 0;
            if (goalsP != null)
                for (int i = 0; i < goalsP.arraySize; i++)
                {
                    SerializedProperty g = goalsP.GetArrayElementAtIndex(i);
                    var kind = (GoalKind)g.FindPropertyRelative("kind").enumValueIndex;
                    string nm = g.FindPropertyRelative("label").stringValue;
                    int bonus = g.FindPropertyRelative("bonusPoints").intValue;
                    float thr = g.FindPropertyRelative("threshold").floatValue;
                    totalBonus += bonus;
                    if (bonus == 0) warnings.Add($"Goal '{nm}': 0 bonus points - passing it does nothing.");
                    if (kind == GoalKind.StepsScore && (thr <= 0f || thr > 100f))
                        errors.Add($"Goal '{nm}': step-score threshold must be 1-100%.");
                    if (kind == GoalKind.TotalTimeUnder && thr <= 0f)
                        errors.Add($"Goal '{nm}': time threshold must be > 0 seconds (it is ignored until set).");
                    if (kind == GoalKind.MaxOccurrences && thr <= 0f)
                        warnings.Add($"Goal '{nm}': 'at most 0' is a requirement, not extra credit - failing it only forfeits +{bonus}. Use a Penalty or a critical metric to enforce it.");
                }
            if (goalsP != null && goalsP.arraySize > 0 && totalBonus == 0)
                warnings.Add("All goals award 0 bonus.");
            if (totalBonus > 20)
                warnings.Add($"Goals add up to +{totalBonus} (above the recommended +20) - high bonus can hide poor execution.");

            for (int i = 0; i < errors.Count; i++) EditorGUILayout.HelpBox(errors[i], MessageType.Error);
            for (int i = 0; i < warnings.Count; i++) EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }
}
