using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Analytics;

namespace Pitech.XR.Analytics.Editor
{
    // 'Scenario' as a bare name binds to the Pitech.XR.Scenario NAMESPACE here (the enclosing Pitech.XR
    // sees the child namespace before the using-import resolves the type). Alias it to the type; this MUST
    // live inside the namespace block to win over that enclosing-namespace match. (CS0118 fix, per file.)
    using Scenario = Pitech.XR.Scenario.Scenario;

    // ---------- LabAnalytics inspector: the rubric builder, top-down OBJECTIVES -> ANALYTICS -> METRICS ----------
    // Second partial of LabAnalyticsEditor (the first, LabAnalyticsEditor.cs, holds Auto-detect / Auto-wire +
    // ResolveScenario, preserved verbatim). This file is the serializedObject-driven authoring UI.
    //
    // WS B2.2 hardening (2026-06-30) - "make Lab Analytics as easy to the dev as possible":
    //   * Section order MIRRORS the grade hierarchy (map sec-11.0 / 11.8): OBJECTIVES on top (the grade), each
    //     fed by ANALYTICS (sub-weighted), each analytic = METRICS (warning/error bands). Each objective shows
    //     its analytics' metrics inline (read-only) so the Objective -> Analytic -> Metric tree is visible.
    //   * Step analytics are authored on step nodes in the Scenario Graph (the white "brick"); they appear here
    //     READ-ONLY so objectives can still reference them. Only scene-wide analytics are authored in this inspector.
    //   * Ids + labels are auto-managed and HIDDEN (the dev never types slugs; teachers name/tune objectives in
    //     the Web Portal). The ONE exception is a Signal metric's id - it is the link the AnalyticsSignalEmitter
    //     matches by, so it stays visible.
    //   * Weights are 0-1 sliders ([Range] on the fields); scoring bands use the simplified Warning/Error editor.
    // The [SerializeReference] lists (analytics, metrics) are still authored via managedReferenceValue.

    public sealed partial class LabAnalyticsEditor : UnityEditor.Editor
    {
        // Foldout state, persisted across domain reloads via EditorPrefs. Lazily initialized on the first GUI pass.
        const string FoldKeyObjectives = "pitech.xr.analytics.fold.objectives";
        const string FoldKeyAnalytics  = "pitech.xr.analytics.fold.analytics";
        const string FoldKeySubjects   = "pitech.xr.analytics.fold.subjects";
        const string FoldKeyRoles      = "pitech.xr.analytics.fold.roles";

        bool foldObjectives;
        bool foldAnalytics;
        bool foldSubjects;
        bool foldRoles;
        bool _foldsLoaded;

        void EnsureFolds()
        {
            if (_foldsLoaded) return;
            foldObjectives = EditorPrefs.GetBool(FoldKeyObjectives, true);
            foldAnalytics  = EditorPrefs.GetBool(FoldKeyAnalytics, true);
            foldSubjects   = EditorPrefs.GetBool(FoldKeySubjects, false);
            foldRoles      = EditorPrefs.GetBool(FoldKeyRoles, false);
            _foldsLoaded = true;
        }

        /// <summary>
        /// Section orchestrator. Opens the serializedObject Update()/Apply() bracket (free Undo + prefab-override
        /// correctness), null-guards the rubric root, resolves the linked Scenario once, then draws the validation
        /// summary followed by the four sections, top-down: 1 Objectives, 2 Analytics, 3 Tracked Objects, 4 Roles.
        /// </summary>
        void DrawRubricBuilder()
        {
            EnsureFolds();
            serializedObject.Update();

            SerializedProperty rubricP = serializedObject.FindProperty("rubric");
            if (rubricP == null)
            {
                EditorGUILayout.HelpBox(
                    "Could not find the 'rubric' serialized property. Recompile scripts and reopen this inspector.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Analytics config (WS B2.2)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Grade = the Objectives below (each a share). Each Objective is fed by Analytics (sub-weighted); " +
                "each Analytic is a set of Metrics. Step analytics are authored on step nodes in the Scenario " +
                "Graph; scene-wide analytics are authored here.",
                MessageType.None);

            // Resolve the linked Scenario once (used to name read-only step analytics).
            Scenario scenario = ResolveScenario((LabAnalytics)target);

            // Top-of-builder validation summary (read-only; reflects unsaved edits).
            DrawValidationSummary(rubricP);

            SerializedProperty analyticsP = rubricP.FindPropertyRelative("analytics");
            SerializedProperty objectivesP = rubricP.FindPropertyRelative("objectives");
            SerializedProperty subjectsP = rubricP.FindPropertyRelative("subjects");
            SerializedProperty roleCapsP = rubricP.FindPropertyRelative("roleCapacities");

            // SECTION 1 - OBJECTIVES (grading), on top.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = EditorGUILayout.Foldout(foldObjectives, "1. Objectives (grading)", true);
                if (open != foldObjectives) { foldObjectives = open; EditorPrefs.SetBool(FoldKeyObjectives, open); }
                if (foldObjectives && objectivesP != null)
                    DrawObjectivesSection(objectivesP, analyticsP);
            }

            // SECTION 2 - ANALYTICS (measurement), under objectives.
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = EditorGUILayout.Foldout(foldAnalytics, "2. Analytics (measurement)", true);
                if (open != foldAnalytics) { foldAnalytics = open; EditorPrefs.SetBool(FoldKeyAnalytics, open); }
                if (foldAnalytics && analyticsP != null)
                    DrawAnalyticsSection(analyticsP, scenario);
            }

            // SECTION 3 - TRACKED OBJECTS (the registry powering drops / wrong-interaction / order).
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = EditorGUILayout.Foldout(foldSubjects, "3. Tracked Objects", true);
                if (open != foldSubjects) { foldSubjects = open; EditorPrefs.SetBool(FoldKeySubjects, open); }
                if (foldSubjects && subjectsP != null)
                {
                    EditorGUILayout.HelpBox(
                        "The objects the learner handles - powers Drop, Wrong-interaction and Order. Use the " +
                        "Auto-detect button above to fill these from the scenario; add distractors by hand.",
                        MessageType.None);
                    EditorGUILayout.PropertyField(subjectsP, new GUIContent("Tracked Objects"), true);
                }
            }

            // SECTION 4 - role capacities (single nested object).
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = EditorGUILayout.Foldout(foldRoles, "4. Session role capacities", true);
                if (open != foldRoles) { foldRoles = open; EditorPrefs.SetBool(FoldKeyRoles, open); }
                if (foldRoles && roleCapsP != null)
                {
                    EditorGUILayout.PropertyField(roleCapsP, true);
                    EditorGUILayout.HelpBox("-1 = unlimited (on any max).", MessageType.None);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ---------- SECTION 1: Objectives (grading) ----------

        /// <summary>Draws the objectives, each as a help-box: share-of-grade + pass-bar sliders and its analytic
        /// feeds (analytic dropdown by human name + sub-weight). Under each feed, the analytic's metrics are shown
        /// read-only so the Objective -> Analytic -> Metric hierarchy is visible. Ids/labels are hidden (auto).</summary>
        void DrawObjectivesSection(SerializedProperty objectivesP, SerializedProperty analyticsP)
        {
            EditorGUILayout.LabelField("Each objective is a share of the grade, fed by analytics.", EditorStyles.miniLabel);

            for (int i = 0; i < objectivesP.arraySize; i++)
            {
                SerializedProperty obj = objectivesP.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Objective " + (i + 1), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("X", "Remove this objective"), GUILayout.Width(24)))
                        {
                            objectivesP.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    // id + label hidden: teachers name + tune objectives in the Web Portal; the dev wires structure.
                    EditorGUILayout.PropertyField(obj.FindPropertyRelative("weight"), new GUIContent("Share of grade"));
                    EditorGUILayout.PropertyField(obj.FindPropertyRelative("target"), new GUIContent("Pass bar (score >= target)"));
                    EditorGUILayout.LabelField(
                        "Time limits live on the Total Duration metric's bands (seconds); this pass bar stays 0-1.",
                        EditorStyles.miniLabel);

                    SerializedProperty inputsP = obj.FindPropertyRelative("inputs");
                    if (inputsP != null)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            EditorGUILayout.LabelField("Fed by these analytics", EditorStyles.boldLabel);

                            for (int j = 0; j < inputsP.arraySize; j++)
                            {
                                SerializedProperty input = inputsP.GetArrayElementAtIndex(j);
                                SerializedProperty aidP = input.FindPropertyRelative("analyticId");
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    DrawAnalyticDropdown(aidP, analyticsP);
                                    EditorGUILayout.PropertyField(input.FindPropertyRelative("subWeight"), new GUIContent("Sub-weight"));
                                    if (GUILayout.Button(new GUIContent("X", "Remove this feed"), GUILayout.Width(24)))
                                    {
                                        inputsP.DeleteArrayElementAtIndex(j);
                                        break;
                                    }
                                }
                                // The analytic's metrics, read-only - makes the Objective -> Analytic -> Metric tree visible.
                                string summary = AnalyticMetricsSummary(analyticsP, aidP != null ? aidP.stringValue : null);
                                if (!string.IsNullOrEmpty(summary))
                                    EditorGUILayout.LabelField("      metrics: " + summary, EditorStyles.miniLabel);
                            }

                            if (GUILayout.Button("+ Analytic feed"))
                                AddObjectiveInput(inputsP);
                        }
                    }
                }
                if (i >= objectivesP.arraySize) break;
            }

            if (GUILayout.Button("Add Objective"))
                AddObjective(objectivesP);
        }

        // ---------- SECTION 2: Analytics (measurement) ----------

        /// <summary>Draws the analytics list: StepAnalytics read-only (authored in the graph), SceneAnalytics
        /// editable, plus the "Add Scene Analytic" button. Guards [SerializeReference] null slots.</summary>
        void DrawAnalyticsSection(SerializedProperty analyticsP, Scenario scenario)
        {
            EditorGUILayout.LabelField(
                "Step analytics come from step nodes in the Scenario Graph. Add scene-wide analytics here.",
                EditorStyles.miniLabel);

            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);

                // [SerializeReference] null-slot guard - never strip nulls automatically (mirror ScenarioEditor).
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("<missing analytic>", EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove null entry", GUILayout.Width(140)))
                            RemoveManagedElement(analyticsP, i);
                    }
                    if (i >= analyticsP.arraySize) break;
                    continue;
                }

                object av = el.managedReferenceValue;
                if (av is StepAnalytic)
                    DrawStepAnalyticReadOnly(el, scenario);
                else
                    DrawSceneAnalyticCard(el, analyticsP, i);

                if (i >= analyticsP.arraySize) break;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Scene Analytic", "Add a scene-wide analytic (time, safety, ...).")))
                    AddAnalyticElement(analyticsP, typeof(SceneAnalytic));
            }
        }

        /// <summary>A StepAnalytic is authored in the graph; show it read-only here (named by its step) so objectives
        /// can still reference it, with its metrics summarised.</summary>
        void DrawStepAnalyticReadOnly(SerializedProperty el, Scenario scenario)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("[Step] " + StepAnalyticDisplay(el, scenario), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("authored in Graph", EditorStyles.miniLabel, GUILayout.Width(110));
                }
                string summary = MetricsSummary(el.FindPropertyRelative("metrics"));
                EditorGUILayout.LabelField("metrics: " + (string.IsNullOrEmpty(summary) ? "(none)" : summary), EditorStyles.miniLabel);
            }
        }

        /// <summary>Draws one SceneAnalytic as an editable card: category (the human name; id/label hidden), the
        /// metrics sub-list, and a remove button.</summary>
        void DrawSceneAnalyticCard(SerializedProperty el, SerializedProperty analyticsP, int i)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("[Scene] Analytic", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("X", "Remove this analytic"), GUILayout.Width(24)))
                    {
                        RemoveManagedElement(analyticsP, i);
                        return;
                    }
                }

                // Category is the human name (id + label hidden, auto-managed).
                EditorGUILayout.PropertyField(el.FindPropertyRelative("category"), new GUIContent("Category"));

                SerializedProperty metricsP = el.FindPropertyRelative("metrics");
                if (metricsP != null)
                    DrawMetricsList(metricsP, el.managedReferenceValue);
            }
        }

        /// <summary>Draws the metrics sub-list of one analytic (indented) plus the kind-filtered "Add Metric" menu.</summary>
        void DrawMetricsList(SerializedProperty metricsP, object owningAnalytic)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);

                for (int j = 0; j < metricsP.arraySize; j++)
                {
                    SerializedProperty el2 = metricsP.GetArrayElementAtIndex(j);
                    DrawMetricCard(el2, metricsP, j, owningAnalytic);
                    if (j >= metricsP.arraySize) break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Add Metric", "Add a metric legal under this analytic's kind.")))
                        BuildMetricsAddMenu(metricsP, owningAnalytic).ShowAsContext();
                }
            }
        }

        /// <summary>Draws one metric card: kind badge, (Signal id only), weight slider, the simplified Warning/Error
        /// band editor, an inline scope-validation HelpBox, and a remove button. Ids/labels are hidden (auto).</summary>
        void DrawMetricCard(SerializedProperty el2, SerializedProperty metricsP, int j, object owningAnalytic)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (el2.propertyType == SerializedPropertyType.ManagedReference && el2.managedReferenceValue == null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("<missing metric>", EditorStyles.miniLabel);
                        if (GUILayout.Button("Remove null entry", GUILayout.Width(140)))
                            RemoveManagedElement(metricsP, j);
                    }
                    return;
                }

                object mv = el2.managedReferenceValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(MetricKindLabel(mv), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("X", "Remove this metric"), GUILayout.Width(24)))
                    {
                        RemoveManagedElement(metricsP, j);
                        return;
                    }
                }

                // Inline scope validation: this kind may be illegal under the owning analytic's kind.
                if (!IsMetricLegalUnder(mv, owningAnalytic))
                {
                    EditorGUILayout.HelpBox(
                        mv is StepDurationMetric
                            ? "StepDuration must live under a Step Analytic."
                            : (mv is TotalDurationMetric
                                ? "TotalDuration must live under a Scene Analytic."
                                : "This metric is not legal under this analytic's kind."),
                        MessageType.Error);
                }

                // id/label hidden (auto) EXCEPT a Signal metric's id: it is the link to the emitter -
                // AnalyticsSignalEmitter.EmitSignal("...") counts only when its signal id equals this id.
                if (mv is SignalMetric)
                    EditorGUILayout.PropertyField(el2.FindPropertyRelative("id"),
                        new GUIContent("Signal id", "Must match the id your AnalyticsSignalEmitter emits."));

                EditorGUILayout.PropertyField(el2.FindPropertyRelative("weight"), new GUIContent("Weight"));

                SerializedProperty bandsP = el2.FindPropertyRelative("bands");
                if (bandsP != null)
                    DrawSimplifiedBands(bandsP, IsDurationKind(mv));
            }
        }

        // ---------- analytic dropdown (shows human display, stores id) ----------

        /// <summary>Draws the analytic dropdown for one ObjectiveInput, listing analytics by their human display
        /// ("[Step] name" / "[Scene] category"); "(none)" at 0; a stale id surfaces as "&lt;missing: id&gt;".
        /// The stored value is always the analytic id (never silently blanked).</summary>
        void DrawAnalyticDropdown(SerializedProperty analyticIdProp, SerializedProperty analyticsP)
        {
            if (analyticIdProp == null) return;

            var ids = new List<string>();
            var displays = new List<string>();
            CollectAnalyticOptions(analyticsP, ids, displays);

            var labels = new List<string> { "(none)" };
            var values = new List<string> { string.Empty };
            for (int i = 0; i < ids.Count; i++) { labels.Add(displays[i]); values.Add(ids[i]); }

            string cur = analyticIdProp.stringValue;
            int curIdx = 0;
            if (!string.IsNullOrEmpty(cur))
            {
                int found = values.IndexOf(cur);
                if (found >= 0) curIdx = found;
                else { labels.Add("<missing: " + cur + ">"); values.Add(cur); curIdx = values.Count - 1; }
            }

            int sel = EditorGUILayout.Popup(new GUIContent("Analytic"), curIdx, labels.ToArray());
            if (sel != curIdx && sel >= 0 && sel < values.Count)
                analyticIdProp.stringValue = values[sel];
        }

        // ---------- [SerializeReference] element create/delete (with auto-assigned id + label) ----------

        /// <summary>Creates and appends a typed [SerializeReference] analytic, then auto-assigns a unique id + label
        /// (both hidden in the UI). Mirrors ScenarioEditor.AddStep.</summary>
        void AddAnalyticElement(SerializedProperty analyticsP, Type t)
        {
            serializedObject.Update();
            List<string> taken = CollectAnalyticIds(analyticsP);
            int i = analyticsP.arraySize;
            analyticsP.InsertArrayElementAtIndex(i);
            SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
            el.managedReferenceValue = Activator.CreateInstance(t);
            string kindLabel = t == typeof(SceneAnalytic) ? "Scene" : "Step";
            SetIfPresent(el, "id", UniqueId(Slug(kindLabel), taken));
            SetIfPresent(el, "label", kindLabel + " analytic");
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Creates and appends a typed [SerializeReference] metric, then auto-assigns a globally-unique id
        /// (prefixed by the owning analytic) + a kind label (both hidden; a Signal author overrides the id).</summary>
        void AddMetricElement(SerializedProperty metricsP, object owningAnalytic, Type t)
        {
            serializedObject.Update();
            List<string> taken = CollectAllMetricIds();
            string analyticId = owningAnalytic is Analytic a ? a.id : null;
            var inst = (AnalyticsMetric)Activator.CreateInstance(t);
            string baseId = string.IsNullOrEmpty(analyticId) ? Slug(inst.Kind) : Slug(analyticId) + "_" + Slug(inst.Kind);
            int i = metricsP.arraySize;
            metricsP.InsertArrayElementAtIndex(i);
            SerializedProperty el = metricsP.GetArrayElementAtIndex(i);
            el.managedReferenceValue = inst;
            SetIfPresent(el, "id", UniqueId(baseId, taken));
            SetIfPresent(el, "label", MetricKindLabel(inst));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Adds a fresh objective (InsertArrayElementAtIndex copies the previous element, so reset to clean
        /// defaults: unique hidden id, default weight/target, empty inputs).</summary>
        void AddObjective(SerializedProperty objectivesP)
        {
            serializedObject.Update();
            List<string> taken = CollectObjectiveIds(objectivesP);
            int i = objectivesP.arraySize;
            objectivesP.InsertArrayElementAtIndex(i);
            SerializedProperty obj = objectivesP.GetArrayElementAtIndex(i);
            SetIfPresent(obj, "id", UniqueId("objective", taken));
            SetIfPresent(obj, "label", "Objective");
            SerializedProperty wP = obj.FindPropertyRelative("weight"); if (wP != null) wP.floatValue = 1f;
            SerializedProperty tP = obj.FindPropertyRelative("target"); if (tP != null) tP.floatValue = 0.9f;
            SerializedProperty inP = obj.FindPropertyRelative("inputs"); if (inP != null) inP.ClearArray();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Adds a fresh analytic feed to an objective (reset the copied analyticId/sub-weight to defaults).</summary>
        void AddObjectiveInput(SerializedProperty inputsP)
        {
            int i = inputsP.arraySize;
            inputsP.InsertArrayElementAtIndex(i);
            SerializedProperty input = inputsP.GetArrayElementAtIndex(i);
            SetIfPresent(input, "analyticId", string.Empty);
            SerializedProperty swP = input.FindPropertyRelative("subWeight"); if (swP != null) swP.floatValue = 1f;
        }

        /// <summary>Deletes a [SerializeReference] element, then deletes a second time if the slot is left as a null
        /// managed reference (Unity's two-step delete). Mirrors ScenarioEditor.RemoveStepAt.</summary>
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

        static void SetIfPresent(SerializedProperty parent, string rel, string value)
        {
            SerializedProperty p = parent.FindPropertyRelative(rel);
            if (p != null) p.stringValue = value;
        }

        // ---------- Add-Metric menu (kind-filtered) ----------

        /// <summary>Builds the "Add Metric" menu filtered by the owning analytic's kind (Step -> StepDuration;
        /// Scene -> TotalDuration; Drop/WrongInteraction/Order/Signal under either).</summary>
        GenericMenu BuildMetricsAddMenu(SerializedProperty metricsP, object analytic)
        {
            var menu = new GenericMenu();

            switch (analytic)
            {
                case StepAnalytic _:
                    menu.AddItem(new GUIContent("Step Duration"), false,
                        () => AddMetricElement(metricsP, analytic, typeof(StepDurationMetric)));
                    break;
                case SceneAnalytic _:
                    menu.AddItem(new GUIContent("Total Duration"), false,
                        () => AddMetricElement(metricsP, analytic, typeof(TotalDurationMetric)));
                    break;
                default:
                    throw new InvalidOperationException("Unknown analytic kind in Add-Metric menu dispatch.");
            }

            menu.AddItem(new GUIContent("Drop"), false,
                () => AddMetricElement(metricsP, analytic, typeof(DropMetric)));
            menu.AddItem(new GUIContent("Wrong Interaction"), false,
                () => AddMetricElement(metricsP, analytic, typeof(WrongInteractionMetric)));
            menu.AddItem(new GUIContent("Order"), false,
                () => AddMetricElement(metricsP, analytic, typeof(OrderMetric)));
            menu.AddItem(new GUIContent("Signal"), false,
                () => AddMetricElement(metricsP, analytic, typeof(SignalMetric)));
            return menu;
        }

        /// <summary>The hard-coded metric-scope rule (no enum encodes it): StepDuration only under a StepAnalytic;
        /// TotalDuration only under a SceneAnalytic; every other kind is legal under either.</summary>
        static bool IsMetricLegalUnder(object metric, object analytic)
        {
            return (metric, analytic) switch
            {
                (StepDurationMetric _, SceneAnalytic _) => false,
                (TotalDurationMetric _, StepAnalytic _) => false,
                _ => true
            };
        }

        // ---------- Simplified, kind-aware band editor (mirrors StepAnalyticEditWindow) ----------
        // Two fixed tiers: Warning + Error. Each is a toggle that adds/removes its band; when on it exposes a penalty
        // (0-1 slider) + notify-in-scene, plus a seconds threshold for DURATION kinds (count kinds derive severity
        // automatically, so their threshold is unused by the grade engine and hidden). The None band is left
        // untouched (the engine still uses it; it just isn't shown - "none is useless" to the author).

        static bool IsDurationKind(object metric) => metric is StepDurationMetric || metric is TotalDurationMetric;

        static void DrawSimplifiedBands(SerializedProperty bandsP, bool isDurationKind)
        {
            if (bandsP == null) return;
            DrawBandTier(bandsP, BandSeverity.Warning, "Warning", 0.5f, isDurationKind);
            DrawBandTier(bandsP, BandSeverity.Error, "Error", 1.0f, isDurationKind);
        }

        static void DrawBandTier(SerializedProperty bandsP, BandSeverity tier, string label, float defaultPenalty, bool isDurationKind)
        {
            int idx = FindBandIndex(bandsP, tier);
            bool enabled = idx >= 0;
            bool now = EditorGUILayout.ToggleLeft(label, enabled, EditorStyles.boldLabel);
            if (now != enabled)
            {
                if (now) AddBand(bandsP, tier, defaultPenalty);
                else if (idx >= 0) bandsP.DeleteArrayElementAtIndex(idx);
                return;   // re-layout next pass with the updated set
            }
            if (!now) return;

            SerializedProperty band = bandsP.GetArrayElementAtIndex(idx);
            using (new EditorGUI.IndentLevelScope())
            {
                if (isDurationKind)
                {
                    SerializedProperty thr = band.FindPropertyRelative("threshold");
                    thr.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
                        new GUIContent("Over (seconds)", "Crossed when the time is at least this many seconds."), thr.floatValue));
                }
                SerializedProperty pen = band.FindPropertyRelative("penaltyWeight");
                pen.floatValue = EditorGUILayout.Slider(
                    new GUIContent("Penalty", "How much this band subtracts from the metric score (0-1)."), pen.floatValue, 0f, 1f);
                SerializedProperty notify = band.FindPropertyRelative("notifyInScene");
                notify.boolValue = EditorGUILayout.Toggle(new GUIContent("Notify in scene"), notify.boolValue);
            }
        }

        static int FindBandIndex(SerializedProperty bandsP, BandSeverity tier)
        {
            for (int i = 0; i < bandsP.arraySize; i++)
            {
                SerializedProperty n = bandsP.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (n != null && n.enumValueIndex == (int)tier) return i;
            }
            return -1;
        }

        static void AddBand(SerializedProperty bandsP, BandSeverity tier, float defaultPenalty)
        {
            int i = bandsP.arraySize;
            bandsP.InsertArrayElementAtIndex(i);
            SerializedProperty b = bandsP.GetArrayElementAtIndex(i);
            b.FindPropertyRelative("name").enumValueIndex = (int)tier;
            b.FindPropertyRelative("threshold").floatValue = 0f;
            b.FindPropertyRelative("penaltyWeight").floatValue = defaultPenalty;
            b.FindPropertyRelative("notifyInScene").boolValue = true;
        }

        // ---------- displays / summaries / ids ----------

        static string MetricKindLabel(object metric)
        {
            return metric switch
            {
                StepDurationMetric _ => "Step duration",
                TotalDurationMetric _ => "Total duration",
                DropMetric _ => "Drop",
                WrongInteractionMetric _ => "Wrong interaction",
                OrderMetric _ => "Order",
                SignalMetric _ => "Signal",
                _ => "Metric"
            };
        }

        /// <summary>Comma-joined kind labels of the metrics in <paramref name="metricsP"/> (null/empty -> null).</summary>
        static string MetricsSummary(SerializedProperty metricsP)
        {
            if (metricsP == null || metricsP.arraySize == 0) return null;
            var parts = new List<string>();
            for (int i = 0; i < metricsP.arraySize; i++)
            {
                SerializedProperty el = metricsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
                parts.Add(MetricKindLabel(el.managedReferenceValue));
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>Metrics summary of the analytic with id <paramref name="analyticId"/> (for the objective tree view).</summary>
        static string AnalyticMetricsSummary(SerializedProperty analyticsP, string analyticId)
        {
            if (analyticsP == null || string.IsNullOrEmpty(analyticId)) return null;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
                SerializedProperty idP = el.FindPropertyRelative("id");
                if (idP != null && idP.stringValue == analyticId)
                    return MetricsSummary(el.FindPropertyRelative("metrics"));
            }
            return null;
        }

        /// <summary>Human display for a StepAnalytic: its auto label (= the step name), else the step's graph
        /// display name, else a generic fallback. Never shows the raw id/guid.</summary>
        static string StepAnalyticDisplay(SerializedProperty el, Scenario scenario)
        {
            SerializedProperty labelP = el.FindPropertyRelative("label");
            string label = labelP != null ? labelP.stringValue : null;
            if (!string.IsNullOrEmpty(label)) return label;

            SerializedProperty guidP = el.FindPropertyRelative("stepGuid");
            string guid = guidP != null ? guidP.stringValue : null;
            if (scenario != null && !string.IsNullOrEmpty(guid))
            {
                var disp = scenario.FindStepGraphDisplay(guid);
                if (disp != null && !string.IsNullOrEmpty(disp.displayName)) return disp.displayName;
            }
            return "Step analytic";
        }

        /// <summary>Collects parallel (id, display) lists for the analytic dropdown (skips null/idless analytics).</summary>
        static void CollectAnalyticOptions(SerializedProperty analyticsP, List<string> ids, List<string> displays)
        {
            if (analyticsP == null) return;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
                SerializedProperty idP = el.FindPropertyRelative("id");
                string id = idP != null ? idP.stringValue : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                ids.Add(id);
                displays.Add(AnalyticDisplayShort(el));
            }
        }

        static string AnalyticDisplayShort(SerializedProperty el)
        {
            object av = el.managedReferenceValue;
            SerializedProperty labelP = el.FindPropertyRelative("label");
            string label = labelP != null ? labelP.stringValue : null;
            if (av is SceneAnalytic)
            {
                SerializedProperty catP = el.FindPropertyRelative("category");
                string cat = catP != null ? catP.stringValue : null;
                string nm = !string.IsNullOrEmpty(cat) ? cat : (!string.IsNullOrEmpty(label) ? label : "scene");
                return "[Scene] " + nm;
            }
            return "[Step] " + (!string.IsNullOrEmpty(label) ? label : "step");
        }

        /// <summary>Collects the current (non-empty) analytic ids in author order.</summary>
        static List<string> CollectAnalyticIds(SerializedProperty analyticsP)
        {
            var ids = new List<string>();
            if (analyticsP == null) return ids;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                    continue;
                SerializedProperty idP = el.FindPropertyRelative("id");
                string id = idP != null ? idP.stringValue : null;
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                    ids.Add(id);
            }
            return ids;
        }

        static List<string> CollectObjectiveIds(SerializedProperty objectivesP)
        {
            var ids = new List<string>();
            if (objectivesP == null) return ids;
            for (int i = 0; i < objectivesP.arraySize; i++)
            {
                SerializedProperty idP = objectivesP.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (idP != null && !string.IsNullOrEmpty(idP.stringValue)) ids.Add(idP.stringValue);
            }
            return ids;
        }

        /// <summary>Every metric id across every analytic (for globally-unique auto-ids).</summary>
        List<string> CollectAllMetricIds()
        {
            var ids = new List<string>();
            SerializedProperty rubricP = serializedObject.FindProperty("rubric");
            SerializedProperty analyticsP = rubricP != null ? rubricP.FindPropertyRelative("analytics") : null;
            if (analyticsP == null) return ids;
            for (int i = 0; i < analyticsP.arraySize; i++)
            {
                SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
                SerializedProperty metricsP = el.FindPropertyRelative("metrics");
                if (metricsP == null) continue;
                for (int j = 0; j < metricsP.arraySize; j++)
                {
                    SerializedProperty m = metricsP.GetArrayElementAtIndex(j);
                    if (m.propertyType == SerializedPropertyType.ManagedReference && m.managedReferenceValue == null) continue;
                    SerializedProperty idP = m.FindPropertyRelative("id");
                    if (idP != null && !string.IsNullOrEmpty(idP.stringValue)) ids.Add(idP.stringValue);
                }
            }
            return ids;
        }

        static string UniqueId(string baseId, ICollection<string> taken)
        {
            string id = string.IsNullOrEmpty(baseId) ? "id" : baseId;
            if (taken == null || !taken.Contains(id)) return id;
            for (int n = 2; ; n++)
            {
                string cand = id + "_" + n;
                if (!taken.Contains(cand)) return cand;
            }
        }

        static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
            }
            return sb.ToString();
        }

        // ---------- Validation (read-only; renders HelpBoxes from serialized values each GUI pass) ----------

        /// <summary>Computes and renders the rubric validation summary at the top of the builder: empty ids,
        /// duplicate ids (per scope), zero weight sums, metric-scope mismatches, and inputless objectives.
        /// Never mutates - read-only over the serialized props (so it reflects unsaved edits).</summary>
        void DrawValidationSummary(SerializedProperty rubricP)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            SerializedProperty analyticsP = rubricP.FindPropertyRelative("analytics");
            SerializedProperty objectivesP = rubricP.FindPropertyRelative("objectives");

            // --- Analytics: empty/duplicate ids; per-analytic metric weight sum; metric scope; metric ids global.
            var analyticIdSeen = new HashSet<string>();
            var metricIdSeen = new HashSet<string>();

            if (analyticsP != null)
            {
                for (int i = 0; i < analyticsP.arraySize; i++)
                {
                    SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
                    if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                        continue;

                    object av = el.managedReferenceValue;
                    SerializedProperty idP = el.FindPropertyRelative("id");
                    string id = idP != null ? idP.stringValue : null;

                    if (string.IsNullOrWhiteSpace(id))
                        errors.Add($"Analytic #{i}: id is required.");
                    else if (!analyticIdSeen.Add(id))
                        errors.Add($"Duplicate analytic id '{id}'.");

                    SerializedProperty metricsP = el.FindPropertyRelative("metrics");
                    if (metricsP != null)
                    {
                        float weightSum = 0f;
                        int metricCount = 0;
                        for (int j = 0; j < metricsP.arraySize; j++)
                        {
                            SerializedProperty el2 = metricsP.GetArrayElementAtIndex(j);
                            if (el2.propertyType == SerializedPropertyType.ManagedReference && el2.managedReferenceValue == null)
                                continue;

                            object mv = el2.managedReferenceValue;
                            metricCount++;

                            SerializedProperty midP = el2.FindPropertyRelative("id");
                            string mid = midP != null ? midP.stringValue : null;
                            if (string.IsNullOrWhiteSpace(mid))
                                errors.Add($"Metric in analytic '{(string.IsNullOrEmpty(id) ? "#" + i : id)}': id is required.");
                            else if (!metricIdSeen.Add(mid))
                                errors.Add($"Duplicate metric id '{mid}'.");

                            SerializedProperty wP = el2.FindPropertyRelative("weight");
                            if (wP != null) weightSum += wP.floatValue;

                            if (!IsMetricLegalUnder(mv, av))
                                errors.Add($"Metric '{(string.IsNullOrEmpty(mid) ? "#" + j : mid)}' is illegal under a {(av is StepAnalytic ? "Step" : "Scene")} analytic.");
                        }

                        if (metricCount >= 1 && Mathf.Approximately(weightSum, 0f))
                            warnings.Add($"Analytic '{(string.IsNullOrEmpty(id) ? "#" + i : id)}': all metric weights are 0 (this analytic cannot score).");
                    }
                }
            }

            // --- Objectives: empty/duplicate ids; weight sum; per-objective input sub-weight sum; no-inputs; missing analyticId.
            var objectiveIdSeen = new HashSet<string>();
            if (objectivesP != null)
            {
                float objWeightSum = 0f;
                for (int i = 0; i < objectivesP.arraySize; i++)
                {
                    SerializedProperty obj = objectivesP.GetArrayElementAtIndex(i);

                    SerializedProperty idP = obj.FindPropertyRelative("id");
                    string id = idP != null ? idP.stringValue : null;
                    if (string.IsNullOrWhiteSpace(id))
                        errors.Add($"Objective #{i}: id is required.");
                    else if (!objectiveIdSeen.Add(id))
                        errors.Add($"Duplicate objective id '{id}'.");

                    SerializedProperty wP = obj.FindPropertyRelative("weight");
                    if (wP != null) objWeightSum += wP.floatValue;

                    SerializedProperty inputsP = obj.FindPropertyRelative("inputs");
                    if (inputsP == null || inputsP.arraySize == 0)
                    {
                        warnings.Add($"Objective '{(string.IsNullOrEmpty(id) ? "#" + i : id)}' has no analytic inputs (contributes nothing).");
                    }
                    else
                    {
                        float subSum = 0f;
                        for (int j = 0; j < inputsP.arraySize; j++)
                        {
                            SerializedProperty input = inputsP.GetArrayElementAtIndex(j);
                            SerializedProperty swP = input.FindPropertyRelative("subWeight");
                            if (swP != null) subSum += swP.floatValue;

                            SerializedProperty aidP = input.FindPropertyRelative("analyticId");
                            string aid = aidP != null ? aidP.stringValue : null;
                            if (!string.IsNullOrEmpty(aid) && !analyticIdSeen.Contains(aid))
                                warnings.Add($"Objective '{(string.IsNullOrEmpty(id) ? "#" + i : id)}': input references unknown analytic '{aid}'.");
                        }
                        if (Mathf.Approximately(subSum, 0f))
                            warnings.Add($"Objective '{(string.IsNullOrEmpty(id) ? "#" + i : id)}': all input sub-weights are 0.");
                    }
                }

                if (objectivesP.arraySize >= 1 && Mathf.Approximately(objWeightSum, 0f))
                    warnings.Add("All objective weights are 0 (grade is incomplete).");
            }

            for (int i = 0; i < errors.Count; i++)
                EditorGUILayout.HelpBox(errors[i], MessageType.Error);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }
}
