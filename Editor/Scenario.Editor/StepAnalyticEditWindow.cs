#if UNITY_EDITOR
using System;
using Pitech.XR.Analytics;
using UnityEditor;
using UnityEngine;

// =================== STEP ANALYTIC EDIT WINDOW (opened from the white "ANALYTIC" brick on a step) ===================
// Mirrors StepEditWindow (which opens from a step's "Edit..." button), but edits a StepAnalytic's metrics on the
// lab's LabAnalytics.config rather than a Scenario step. This REPLACES the brick's old inline metrics dropdown:
// the brick is now a compact indicator; its metrics are authored here, in a window, like every other step editor.
// Edits go through a SerializedObject of the LabAnalytics (free Undo + prefab-override correctness); the
// [SerializeReference] metrics list uses the same managed-ref add/remove idiom as the inspector builder.
sealed class StepAnalyticEditWindow : EditorWindow
{
    LabAnalytics la;
    string stepGuid;
    SerializedObject so;
    Vector2 scroll;

    public static void Open(LabAnalytics analytics, string guid, string stepLabel)
    {
        var w = CreateInstance<StepAnalyticEditWindow>();
        w.la = analytics;
        w.stepGuid = guid;
        w.so = analytics != null ? new SerializedObject(analytics) : null;
        w.minSize = new Vector2(420, 320);
        w.titleContent = new GUIContent((string.IsNullOrEmpty(stepLabel) ? "Step" : stepLabel) + " - Analytic");
        w.ShowUtility();
        var mp = Event.current != null ? Event.current.mousePosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        w.position = new Rect(GUIUtility.GUIToScreenPoint(mp) + new Vector2(8, 8), w.minSize);
    }

    void OnGUI()
    {
        if (la == null)
        {
            EditorGUILayout.HelpBox("The Lab Analytics recorder for this step was removed.", MessageType.Info);
            return;
        }
        if (so == null) so = new SerializedObject(la);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawMetrics();
        EditorGUILayout.EndScrollView();
    }

    void DrawMetrics()
    {
        so.Update();

        SerializedProperty configP = so.FindProperty("config");
        SerializedProperty analyticsP = configP != null ? configP.FindPropertyRelative("analytics") : null;
        SerializedProperty analyticP = FindStepAnalyticProp(analyticsP, stepGuid);
        if (analyticP == null) { EditorGUILayout.HelpBox("Step analytic not found (it may have been removed).", MessageType.Info); return; }

        EditorGUI.BeginChangeCheck();

        // ---- Step-level: importance + scenario gate (v3). The step's id/label are auto-managed + hidden. ----
        SerializedProperty weightP = analyticP.FindPropertyRelative("weight");
        if (weightP != null)
        {
            int imp = Mathf.Clamp(Mathf.RoundToInt(weightP.floatValue), 1, 5);
            imp = EditorGUILayout.IntSlider(new GUIContent("Step importance (1-5)",
                "How much this step counts toward the base grade, relative to the other steps. 1 = minor, 5 = critical. A 5 counts 5x a 1."), imp, 1, 5);
            if (!Mathf.Approximately(weightP.floatValue, imp)) weightP.floatValue = imp;
            EditorGUILayout.LabelField("   " + ImportanceWord(imp), EditorStyles.miniLabel);
        }
        SerializedProperty failStepP = analyticP.FindPropertyRelative("failsScenario");
        if (failStepP != null)
        {
            bool fs = EditorGUILayout.ToggleLeft(new GUIContent("Failing this step fails the whole scenario",
                "If a CRITICAL metric on this step fails, fail the entire scenario (grade 0)."), failStepP.boolValue);
            if (fs != failStepP.boolValue) failStepP.boolValue = fs;
            if (fs) EditorGUILayout.LabelField("   grade 0 if this step fails - use for must-not-fail steps.",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.86f, 0.30f, 0.28f) } });
        }

        EditorGUILayout.Space(4);
        SerializedProperty metricsP = analyticP.FindPropertyRelative("metrics");
        int scored = CountScored(metricsP);
        EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(scored > 0
            ? $"{scored} scored metric" + (scored == 1 ? "" : "s") + " - each counts 1/" + scored + " of this step's score. Gates + notify-only don't count."
            : "No scored metrics - this step scores 1.0 unless a gate fails.", EditorStyles.miniLabel);

        if (metricsP != null)
        {
            for (int j = 0; j < metricsP.arraySize; j++)
            {
                SerializedProperty m = metricsP.GetArrayElementAtIndex(j);

                if (m.propertyType == SerializedPropertyType.ManagedReference && m.managedReferenceValue == null)
                {
                    if (GUILayout.Button("Remove null metric")) { RemoveMetric(metricsP, j); break; }
                    continue;
                }

                object mv = m.managedReferenceValue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(MetricKindLabel(mv), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("X", "Remove this metric"), GUILayout.Width(24)))
                        {
                            RemoveMetric(metricsP, j);
                            break;
                        }
                    }

                    // Signal metric's id links to the emitter (AnalyticsSignalEmitter.EmitSignal must match).
                    if (mv is SignalMetric)
                        EditorGUILayout.PropertyField(m.FindPropertyRelative("id"),
                            new GUIContent("Signal id", "Must match the id your AnalyticsSignalEmitter emits."));

                    SerializedProperty criticalP = m.FindPropertyRelative("critical");
                    SerializedProperty notifyOnlyP = m.FindPropertyRelative("notifyOnly");
                    bool crit = criticalP != null && criticalP.boolValue;

                    // Critical GATE: pass/fail, no partial credit.
                    if (criticalP != null)
                    {
                        bool c = EditorGUILayout.ToggleLeft(new GUIContent("Critical (fails the step)",
                            "A gate: if this criterion fails, the whole step fails (score 0). No partial credit."), crit);
                        if (c != crit) { criticalP.boolValue = c; crit = c; }
                    }

                    if (crit)
                    {
                        EditorGUILayout.LabelField(DescribeGate(mv, m),
                            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.86f, 0.30f, 0.28f) } });
                        // A gate only needs the Error threshold (duration) - the penalty sliders don't apply.
                        SerializedProperty bandsP = m.FindPropertyRelative("bands");
                        if (bandsP != null && IsDurationKind(mv)) DrawDurationErrorOnly(bandsP);
                    }
                    else
                    {
                        if (notifyOnlyP != null)
                        {
                            bool n = EditorGUILayout.ToggleLeft(new GUIContent("Notify only (don't score)",
                                "Fire the in-scene toast but don't let this metric affect the step score."), notifyOnlyP.boolValue);
                            if (n != notifyOnlyP.boolValue) notifyOnlyP.boolValue = n;
                        }
                        SerializedProperty bandsP = m.FindPropertyRelative("bands");
                        if (bandsP != null) DrawSimplifiedBands(bandsP, IsDurationKind(mv));
                    }
                }

                if (j >= metricsP.arraySize) break;
            }
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Add Metric"))
            BuildAddMetricMenu().ShowAsContext();

        if (EditorGUI.EndChangeCheck())
            so.ApplyModifiedProperties();
    }

    // Add-metric menu, scoped to a StepAnalytic: every kind EXCEPT TotalDuration (scene-wide only).
    GenericMenu BuildAddMetricMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Step Duration"), false, () => AddMetric(typeof(StepDurationMetric)));
        menu.AddItem(new GUIContent("Drop"), false, () => AddMetric(typeof(DropMetric)));
        menu.AddItem(new GUIContent("Wrong Interaction"), false, () => AddMetric(typeof(WrongInteractionMetric)));
        menu.AddItem(new GUIContent("Order"), false, () => AddMetric(typeof(OrderMetric)));
        menu.AddItem(new GUIContent("Signal"), false, () => AddMetric(typeof(SignalMetric)));
        return menu;
    }

    void AddMetric(Type t)
    {
        if (so == null || so.targetObject == null) return;
        so.Update();
        SerializedProperty configP = so.FindProperty("config");
        SerializedProperty analyticsP = configP != null ? configP.FindPropertyRelative("analytics") : null;
        SerializedProperty analyticP = FindStepAnalyticProp(analyticsP, stepGuid);
        SerializedProperty metricsP = analyticP != null ? analyticP.FindPropertyRelative("metrics") : null;
        if (metricsP == null) return;
        SerializedProperty aidP = analyticP.FindPropertyRelative("id");
        string analyticId = aidP != null ? aidP.stringValue : null;

        // Auto-assign id + label so the dev never types them (id/label are hidden). A Signal author then
        // overrides the id to match their emitter. label is kept meaningful for the lab-end readout.
        var inst = (AnalyticsMetric)Activator.CreateInstance(t);
        inst.label = MetricKindLabel(inst);
        inst.id = UniqueMetricId(metricsP, analyticId, inst.Kind);

        int i = metricsP.arraySize;
        metricsP.InsertArrayElementAtIndex(i);
        metricsP.GetArrayElementAtIndex(i).managedReferenceValue = inst;
        so.ApplyModifiedProperties();
    }

    static void RemoveMetric(SerializedProperty metricsP, int index)
    {
        if (metricsP == null || index < 0 || index >= metricsP.arraySize) return;
        metricsP.DeleteArrayElementAtIndex(index);
        if (index < metricsP.arraySize)
        {
            SerializedProperty el = metricsP.GetArrayElementAtIndex(index);
            if (el != null && el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null)
                metricsP.DeleteArrayElementAtIndex(index);
        }
        metricsP.serializedObject.ApplyModifiedProperties();
    }

    static SerializedProperty FindStepAnalyticProp(SerializedProperty analyticsP, string guid)
    {
        if (analyticsP == null || string.IsNullOrEmpty(guid)) return null;
        for (int i = 0; i < analyticsP.arraySize; i++)
        {
            SerializedProperty el = analyticsP.GetArrayElementAtIndex(i);
            if (el.propertyType == SerializedPropertyType.ManagedReference
                && el.managedReferenceValue is StepAnalytic sa && sa.stepGuid == guid)
                return el;
        }
        return null;
    }

    // 1-5 importance -> a plain word, so the number isn't opaque.
    static string ImportanceWord(int imp)
    {
        switch (imp)
        {
            case 1: return "1/5 - minor (counts least toward the grade)";
            case 2: return "2/5 - low";
            case 3: return "3/5 - normal";
            case 4: return "4/5 - high";
            default: return "5/5 - critical (counts most toward the grade)";
        }
    }

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

    // Number of metrics that actually contribute to the step score (not gates, not notify-only).
    static int CountScored(SerializedProperty metricsP)
    {
        if (metricsP == null) return 0;
        int n = 0;
        for (int j = 0; j < metricsP.arraySize; j++)
        {
            SerializedProperty m = metricsP.GetArrayElementAtIndex(j);
            if (m.propertyType == SerializedPropertyType.ManagedReference && m.managedReferenceValue == null) continue;
            SerializedProperty crit = m.FindPropertyRelative("critical");
            SerializedProperty notify = m.FindPropertyRelative("notifyOnly");
            if ((crit != null && crit.boolValue) || (notify != null && notify.boolValue)) continue;
            n++;
        }
        return n;
    }

    // Plain-language trigger for a critical gate, so the author knows exactly when it fires.
    static string DescribeGate(object mv, SerializedProperty m)
    {
        if (mv is StepDurationMetric)
        {
            float err = AnalyticsSeverity.DurationErrorSeconds((AnalyticsMetric)mv);
            return err > 0f ? $"Fails when this step reaches {err:0.#}s (the Error threshold)."
                            : "Set an Error threshold below - this gate is inactive until you do.";
        }
        return "Zero tolerance: fails on any error-severity occurrence (drops of relevant items, wrong targets, etc.).";
    }

    // For a gate duration metric: only the Error 'fail if over' seconds matters (no penalty slider).
    static void DrawDurationErrorOnly(SerializedProperty bandsP)
    {
        int idx = FindBandIndex(bandsP, BandSeverity.Error);
        if (idx < 0) { AddBand(bandsP, BandSeverity.Error, 1.0f); return; }
        SerializedProperty band = bandsP.GetArrayElementAtIndex(idx);
        SerializedProperty thr = band.FindPropertyRelative("threshold");
        thr.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
            new GUIContent("Fail if over (seconds)", "The step fails when it reaches this many seconds."), thr.floatValue));
    }

    // ---------- Simplified, kind-aware band editor (replaces the raw ScoringBand list) ----------
    // Two fixed tiers: Warning + Error. Each is a toggle that adds/removes its band; when on it exposes a penalty
    // (0-1 slider) + notify-in-scene, plus a seconds threshold for DURATION kinds (count kinds derive severity
    // automatically, so their threshold is unused by the engine and hidden). The None band is left untouched (the
    // grade engine still uses it; it just isn't shown - "none is useless" to the author).
    static bool IsDurationKind(object metric) => metric is StepDurationMetric;

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
                    new GUIContent("Over (seconds)", "Crossed when the step takes at least this many seconds."), thr.floatValue));
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

    static string UniqueMetricId(SerializedProperty metricsP, string analyticId, string kind)
    {
        var taken = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < metricsP.arraySize; i++)
        {
            SerializedProperty el = metricsP.GetArrayElementAtIndex(i);
            if (el.propertyType == SerializedPropertyType.ManagedReference && el.managedReferenceValue == null) continue;
            SerializedProperty idP = el.FindPropertyRelative("id");
            if (idP != null && !string.IsNullOrEmpty(idP.stringValue)) taken.Add(idP.stringValue);
        }
        string baseId = Slug(analyticId);
        baseId = string.IsNullOrEmpty(baseId) ? Slug(kind) : baseId + "_" + Slug(kind);
        if (string.IsNullOrEmpty(baseId)) baseId = "metric";
        if (!taken.Contains(baseId)) return baseId;
        for (int n = 2; ; n++) { string c = baseId + "_" + n; if (!taken.Contains(c)) return c; }
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
}
#endif
