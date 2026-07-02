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

        // The analytic's id + label are auto-managed (id = unique slug, label = the step's name) and intentionally
        // hidden - this analytic is identified by its step (the window title). Only its metrics are authored here.
        EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Each metric scores this step; Warning/Error subtract their penalty.", EditorStyles.miniLabel);

        SerializedProperty metricsP = analyticP.FindPropertyRelative("metrics");
        if (metricsP != null)
        {
            for (int j = 0; j < metricsP.arraySize; j++)
            {
                SerializedProperty m = metricsP.GetArrayElementAtIndex(j);

                // [SerializeReference] null-slot guard - never auto-strip; let the user remove it.
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

                    // Id/label are hidden (auto) EXCEPT a Signal metric's id: it is the link to the emitter -
                    // AnalyticsSignalEmitter.EmitSignal("...") counts only when its signal id equals this id.
                    if (mv is SignalMetric)
                        EditorGUILayout.PropertyField(m.FindPropertyRelative("id"),
                            new GUIContent("Signal id", "Must match the id your AnalyticsSignalEmitter emits."));

                    EditorGUILayout.PropertyField(m.FindPropertyRelative("weight"), new GUIContent("Weight"));
                    SerializedProperty bandsP = m.FindPropertyRelative("bands");
                    if (bandsP != null) DrawSimplifiedBands(bandsP, IsDurationKind(mv));
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

    // ---------- Simplified, kind-aware band editor (replaces the raw ScoringBand list) ----------
    // Two fixed tiers: Warning + Error. Each is a toggle that adds/removes its band; when on it exposes a penalty
    // (0-1 slider) + notify-in-scene, plus a seconds threshold for DURATION kinds (count kinds derive severity
    // automatically, so their threshold is unused by the engine and hidden). The None band is left untouched (the
    // grade engine still uses it; it just isn't shown - "none is useless" to the author).
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
