#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Pitech.XR.Scenario;
using Pitech.XR.Analytics;
using UnityEditor;
using UnityEngine;

// ---------- ScenarioGraphWindow: in-graph Step Analytic authoring (WS B2.2) ----------
// The Scenario Graph is the main developer authoring surface, so a step's analytics "sidecar" (a
// StepAnalytic in the lab's LabAnalytics.rubric, keyed by the step's guid) is authored straight on the
// step node: right-click Add / Remove, and steps that own one show a white collapsible "brick" at the top
// of the node whose METRICS are edited INLINE (see ScenarioGraphWindow.StepAnalyticBrick.cs) - no inspector
// trip. This file is the right-click menu + create/remove/resolve plumbing + the per-Load brick index.
//
// This is the ONLY place Scenario.Editor reaches into Analytics, which is why
// Pitech.XR.Scenario.Editor.asmdef now references Pitech.XR.Analytics. The edge is acyclic: the
// Analytics runtime assembly depends only on Core, never on Scenario.
//
// Mutation mirrors LabAnalyticsEditor.AutoDetect exactly: RegisterCompleteObjectUndo + a direct edit of
// the live rubric list + SetDirty. The rubric.analytics list is [SerializeReference]; editing the live
// object (rather than a SerializedProperty) is the same idiom the inspector's Auto-detect already uses,
// so graph-created and inspector-created analytics are identical.
public partial class ScenarioGraphWindow
{
    // Step guids that currently own a StepAnalytic, refreshed once per Load() (before nodes are built) so
    // step nodes render their analytic brick cheaply with no per-node scene scan. From the lab's rubric.
    HashSet<string> _stepsWithAnalytics;

    /// <summary>True if <paramref name="stepGuid"/> has a StepAnalytic in the lab's rubric (cached at Load()).</summary>
    internal bool StepHasAnalytic(string stepGuid)
    {
        return !string.IsNullOrEmpty(stepGuid)
            && _stepsWithAnalytics != null
            && _stepsWithAnalytics.Contains(stepGuid);
    }

    /// <summary>Rebuilds <see cref="_stepsWithAnalytics"/> from the lab's LabAnalytics rubric. Called at the
    /// top of Load() so node badges reflect the current rubric. Null-safe (clears the set if nothing resolves).</summary>
    void RefreshAnalyticsIndex()
    {
        if (_stepsWithAnalytics == null) _stepsWithAnalytics = new HashSet<string>();
        _stepsWithAnalytics.Clear();

        LabAnalytics la = ResolveLabAnalytics(false);
        if (la == null || la.rubric == null || la.rubric.analytics == null) return;

        List<Analytic> list = la.rubric.analytics;
        for (int i = 0; i < list.Count; i++)
            if (list[i] is StepAnalytic sa && !string.IsNullOrEmpty(sa.stepGuid))
                _stepsWithAnalytics.Add(sa.stepGuid);
    }

    /// <summary>Right-click "Add Step Analytic": creates the step's sidecar StepAnalytic in the lab's rubric
    /// (resolving, or offering to add, a LabAnalytics if none exists), opens the step's analytic brick, and
    /// rebuilds the graph so the brick appears for inline metric editing. If the step already has a
    /// StepAnalytic, it opens the existing brick instead of creating a duplicate.</summary>
    internal void AddStepAnalytic(Step step)
    {
        if (step == null) return;
        if (!scenario)
        {
            EditorUtility.DisplayDialog("Add Step Analytic", "This graph is not linked to a Scenario.", "OK");
            return;
        }

        LabAnalytics la = ResolveLabAnalytics(true);
        if (la == null) return;                 // none resolvable, or the user declined to add one
        if (la.rubric == null) la.rubric = new LabRubric();

        string display = scenario.FindStepGraphDisplay(step.guid)?.displayName;
        bool hasDisplay = !string.IsNullOrWhiteSpace(display);
        string stepLabel = hasDisplay ? display : step.Kind;

        if (FindStepAnalyticIndex(la, step.guid) >= 0)
        {
            // Already authored - just open its editor window; do not duplicate.
            StepAnalyticEditWindow.Open(la, step.guid, stepLabel);
            return;
        }

        string id = UniqueAnalyticId(la, hasDisplay ? display : step.Kind);

        Undo.RegisterCompleteObjectUndo(la, "Add Step Analytic");
        la.rubric.analytics.Add(new StepAnalytic { id = id, label = hasDisplay ? display : (step.Kind + " step"), stepGuid = step.guid });
        EditorUtility.SetDirty(la);

        Load(scenario);                                        // rebuild so the brick appears on the node
        StepAnalyticEditWindow.Open(la, step.guid, stepLabel); // open the metrics window for the new analytic
    }

    /// <summary>Right-click "Remove Step Analytic": deletes the step's StepAnalytic from the rubric (after a
    /// confirm) and rebuilds the graph so the brick disappears. No-op if none exists.</summary>
    internal void RemoveStepAnalytic(Step step)
    {
        if (step == null || !scenario) return;
        LabAnalytics la = ResolveLabAnalytics(false);
        if (la == null || la.rubric == null || la.rubric.analytics == null) return;

        int idx = FindStepAnalyticIndex(la, step.guid);
        if (idx < 0) return;

        if (!EditorUtility.DisplayDialog("Remove Step Analytic",
            "Remove this step's Step Analytic (and its metrics) from the lab rubric?", "Remove", "Cancel"))
            return;

        Undo.RegisterCompleteObjectUndo(la, "Remove Step Analytic");
        la.rubric.analytics.RemoveAt(idx);
        EditorUtility.SetDirty(la);

        Load(scenario);
    }

    // ---------- resolution + helpers ----------

    /// <summary>Resolves the lab's LabAnalytics recorder for the open scenario: any recorder anywhere under the
    /// lab ROOT - so a dedicated "Analytics" object that is a SIBLING of the LabConsole resolves (as does a legacy
    /// co-located or child recorder). When <paramref name="createIfMissing"/> is set and none is found, offers to
    /// add one NEXT TO the LabConsole.</summary>
    internal LabAnalytics ResolveLabAnalytics(bool createIfMissing)
    {
        if (!scenario) return null;

        LabConsole console = FindConsoleForScenario(scenario);
        // Anchor placement on the LabConsole (the Analytics object becomes its SIBLING); fall back to the
        // scenario's own object if there is no in-scene console. labRoot = the topmost object of that hierarchy.
        Transform anchor = console != null ? console.transform : scenario.transform;
        GameObject labRoot = anchor.root.gameObject;

        // Find anywhere under the lab root (GetComponentInChildren on the root includes every descendant), so an
        // "Analytics" SIBLING of the LabConsole resolves the same as a co-located or (legacy) child recorder.
        LabAnalytics la = labRoot.GetComponentInChildren<LabAnalytics>(true);
        if (la == null) la = scenario.GetComponentInParent<LabAnalytics>(true);
        if (la != null) return la;

        if (!createIfMissing) return null;

        bool ok = EditorUtility.DisplayDialog("Add Step Analytic",
            "This lab has no Lab Analytics recorder yet.\n\nAdd an \"Analytics\" GameObject (next to \"" + anchor.name +
            "\") with a Lab Analytics recorder so step analytics can be authored?",
            "Add Analytics", "Cancel");
        if (!ok) return null;

        // The recorder lives on a dedicated "Analytics" GameObject that is a SIBLING of the LabConsole (next to it),
        // NOT a child of it. For the shared event bus to resolve, the LabRuntimeContext must sit on the lab ROOT (the
        // topmost object) - the COMMON ANCESTOR of the console and the sibling: the runner finds it by parent-walk
        // from the console, and LabAnalytics (on the sibling) finds the SAME one by parent-walk up to that root. (A
        // context on the console GO would NOT be reachable from a sibling, splitting the bus.) ContentDelivery
        // GetOrAdds + stamps this same root component at spawn, so production is unaffected (one context, on the root).
        if (labRoot.GetComponent<Pitech.XR.Core.LabRuntimeContext>() == null)
            Undo.AddComponent<Pitech.XR.Core.LabRuntimeContext>(labRoot);

        GameObject analyticsGo = FindOrCreateAnalyticsSibling(anchor);
        return Undo.AddComponent<LabAnalytics>(analyticsGo);
    }

    /// <summary>Finds (or creates) the dedicated "Analytics" GameObject the recorder lives on - a SIBLING of the
    /// <paramref name="anchor"/> (the LabConsole), i.e. sharing its parent, so it sits NEXT TO the console rather
    /// than inside it. A root-level anchor gets a root-level sibling.</summary>
    static GameObject FindOrCreateAnalyticsSibling(Transform anchor)
    {
        Transform parent = anchor.parent;   // sibling => same parent as the anchor (the LabConsole)

        // Reuse an existing "Analytics" sibling if one is already there (avoid duplicates).
        if (parent != null)
        {
            Transform existing = parent.Find("Analytics");
            if (existing != null) return existing.gameObject;
        }

        var go = new GameObject("Analytics");
        Undo.RegisterCreatedObjectUndo(go, "Create Analytics object");
        go.transform.SetParent(parent, false);   // null parent => a scene-root sibling of a root LabConsole
        return go;
    }

    /// <summary>Finds the scene LabConsole whose assigned scenario is <paramref name="sc"/> (version-gated
    /// FindObjects, mirroring TryResolveSceneScenario). Null if none.</summary>
    static LabConsole FindConsoleForScenario(Scenario sc)
    {
        if (!sc) return null;
#if UNITY_2023_1_OR_NEWER
        var consoles = UnityEngine.Object.FindObjectsByType<LabConsole>(FindObjectsSortMode.None);
#else
        var consoles = UnityEngine.Object.FindObjectsOfType<LabConsole>();
#endif
        if (consoles == null) return null;
        for (int i = 0; i < consoles.Length; i++)
            if (consoles[i] && consoles[i].scenario == sc) return consoles[i];
        return null;
    }

    /// <summary>Index of the StepAnalytic in the rubric for <paramref name="guid"/>, or -1. Tolerates the
    /// [SerializeReference] null slots and ignores SceneAnalytics.</summary>
    static int FindStepAnalyticIndex(LabAnalytics la, string guid)
    {
        if (la == null || la.rubric == null || la.rubric.analytics == null || string.IsNullOrEmpty(guid))
            return -1;
        List<Analytic> list = la.rubric.analytics;
        for (int i = 0; i < list.Count; i++)
            if (list[i] is StepAnalytic sa && sa.stepGuid == guid) return i;
        return -1;
    }

    /// <summary>A rubric-unique, sanitized analytic id derived from a base name (mirrors
    /// LabAnalyticsEditor.UniqueId/Sanitize, so graph-created ids match inspector-created ones).</summary>
    static string UniqueAnalyticId(LabAnalytics la, string baseName)
    {
        var taken = new HashSet<string>();
        if (la != null && la.rubric != null && la.rubric.analytics != null)
            for (int i = 0; i < la.rubric.analytics.Count; i++)
            {
                Analytic a = la.rubric.analytics[i];
                if (a != null && !string.IsNullOrEmpty(a.id)) taken.Add(a.id);
            }

        string id = SanitizeAnalyticId(baseName);
        if (string.IsNullOrEmpty(id)) id = "step";
        if (!taken.Contains(id)) return id;
        for (int n = 2; ; n++)
        {
            string cand = id + "_" + n;
            if (!taken.Contains(cand)) return cand;
        }
    }

    static string SanitizeAnalyticId(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
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
