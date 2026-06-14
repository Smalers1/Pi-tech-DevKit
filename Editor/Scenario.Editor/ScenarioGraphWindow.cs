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

public partial class ScenarioGraphWindow : EditorWindow
{
    [SerializeField]
    Scenario scenario;
    [SerializeField]
    string authoringScenarioGlobalId;
    [SerializeField]
    string authoringScenarioScenePath;
    [SerializeField]
    string authoringScenarioGameObjectName;
    ScenarioGraphView view;
    readonly Dictionary<string, StepNode> nodes = new();
    readonly Dictionary<string, EditableNote> notes = new();
    readonly Dictionary<string, GroupBox> groups = new();
    readonly HashSet<StepNode> movedNodesSinceMouseDown = new HashSet<StepNode>();
    // Keep node expanded/collapsed state stable across Refresh/Load (do not collapse nodes on refresh).
    // Keyed by Step.guid. Not serialized into the Scenario asset.
    readonly Dictionary<string, bool> _expandedByGuid = new Dictionary<string, bool>();
    // NOTE: We intentionally do NOT represent nested steps as GraphView nodes.
    // Nested steps are rendered as UI tiles inside the Group node to avoid z-order/picking issues in GraphView.
    Vector2 mouseWorld;
    bool _suppressNestedMoveWrites;

    // ---- Group tile layout (nested steps) ----
    // Two columns, compact tiles inside the Group node (UI tiles, not GraphView nodes).
    const float GroupTileW = 210f;
    const float GroupTileH = 96f;
    const int GroupTileColumns = 2;
    const float GroupTileGapX = 10f;
    const float GroupTileGapY = 10f;
    const float GroupTilesPadX = 14f;
    const float GroupTilesPadY = 12f;
    const float GroupHeaderH = 54f;     // title + ports row
    const float GroupSettingsApproxH = 150f; // base IMGUI group settings height (extra lines added dynamically)
    const float GroupSettingsCollapsedMinH = 70f; // keep header + some breathing room so it never becomes unclickable

    // Base node sizing (non-group)
    const float StepNodeWidth = 200f;
    const float StepNodeWidthExpanded = 280f;
    const float StepNodeWidthExpandedWide = 360f;

    static float ExpandedWidthFor(Step s)
        => (s is MiniQuizStep || s is ConditionsStep) ? StepNodeWidthExpandedWide : StepNodeWidthExpanded;

    /// <summary>
    /// Nested step when the group shows proxy branch ports (Specific Child Completes + Question or Conditions).
    /// </summary>
    public static Step TryGetGroupProxyBranchChild(GroupStep g)
    {
        if (g == null || g.completeWhen != GroupStep.CompleteWhen.SpecificChildCompletes)
            return null;
        if (string.IsNullOrEmpty(g.specificStepGuid) || g.steps == null)
            return null;
        foreach (var st in g.steps)
        {
            if (st == null || st.guid != g.specificStepGuid)
                continue;
            if (st is QuestionStep || st is ConditionsStep)
                return st;
            return null;
        }
        return null;
    }

    public static bool GroupUsesProxyBranchPorts(GroupStep g)
        => TryGetGroupProxyBranchChild(g) != null || GroupUsesMultiConditionPorts(g);

    public static bool GroupUsesMultiConditionPorts(GroupStep g)
        => g != null && g.completeWhen == GroupStep.CompleteWhen.MultiCondition
           && g.multiConditionBranches != null && g.multiConditionBranches.Count > 0;

    static void AddEdgesFromGroupForLayout(GroupStep g, System.Action<string, string> addEdge)
    {
        if (g == null || string.IsNullOrEmpty(g.guid)) return;
        var from = g.guid;

        if (GroupUsesMultiConditionPorts(g))
        {
            foreach (var branch in g.multiConditionBranches)
                if (branch != null && !string.IsNullOrEmpty(branch.nextGuid))
                    addEdge(from, branch.nextGuid);
            return;
        }

        var child = TryGetGroupProxyBranchChild(g);
        if (child is QuestionStep q && q.choices != null)
        {
            foreach (var ch in q.choices)
                if (ch != null && !string.IsNullOrEmpty(ch.nextGuid))
                    addEdge(from, ch.nextGuid);
            return;
        }
        if (child is ConditionsStep cnd && cnd.outcomes != null)
        {
            foreach (var b in cnd.outcomes)
                if (b != null && !string.IsNullOrEmpty(b.nextGuid))
                    addEdge(from, b.nextGuid);
            return;
        }
        if (!string.IsNullOrEmpty(g.nextGuid))
            addEdge(from, g.nextGuid);
    }

    string _activeGuid;
    string _prevGuid;

    Color _edgeDefaultColor = new Color(0.7f, 0.7f, 0.7f);
    int _edgeDefaultWidth = 2;

    bool _isLoading;
    // GraphView can emit movedElements *after* we set _isLoading=false (layout pass).
    // During that short window we must not write graphPos back, otherwise Refresh can overwrite positions (often to 0,0).
    int _suppressGraphPosWritesFrames;
    bool _wasPlaying;
    bool _pendingFullRouteSync;

    // Persist GraphView pan/zoom across refresh/reload. Some Unity versions reset it on ClearGraph().
    [SerializeField] Vector3 _savedViewPos = Vector3.zero;
    [SerializeField] Vector3 _savedViewScale = Vector3.one;
    bool _hasSavedView;

    struct PendingNoteEdit
    {
        public string text;
        public double dueTime;
    }

    readonly Dictionary<string, PendingNoteEdit> _pendingNoteEdits = new Dictionary<string, PendingNoteEdit>();

    [MenuItem("Pi tech/Scenario Graph", false, 43)]
    public static void OpenWindow() => GetWindow<ScenarioGraphWindow>("Scenario Graph");

    public static void Open(Scenario sc)
    {
        var w = GetWindow<ScenarioGraphWindow>("Scenario Graph");
        w.Load(sc);
    }

    static void Dirty(UnityEngine.Object o, string undo)
    {
        if (!o) return;
        Undo.RecordObject(o, undo);
        EditorUtility.SetDirty(o);
        if (o is Component c) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }

    bool GetExpanded(string guid) => !string.IsNullOrEmpty(guid) && _expandedByGuid.TryGetValue(guid, out var v) && v;
    void SetExpanded(string guid, bool v)
    {
        if (string.IsNullOrEmpty(guid)) return;
        _expandedByGuid[guid] = v;
    }

    void OnEnable()
    {
        // minimal toolbar (works across 2021/2022/2023)
        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.paddingLeft = 6; bar.style.paddingTop = 4; bar.style.paddingBottom = 4;

        bar.Add(new UIEButton(() => { if (scenario) Load(scenario); }) { text = "Refresh" });

        var frame = new UIEButton(() =>
        {
            view?.FrameAll();
        })
        { text = "Frame All" };
        frame.style.marginLeft = 6;
        bar.Add(frame);

        var rearrange = new UIEButton(() => AutoLayout())
        {
            text = "Rearrange"
        };
        rearrange.style.marginLeft = 6;
        bar.Add(rearrange);

        var expandAll = new UIEButton(() => SetAllExpanded(true)) { text = "Expand All" };
        expandAll.style.marginLeft = 6;
        bar.Add(expandAll);

        var collapseAll = new UIEButton(() => SetAllExpanded(false)) { text = "Collapse All" };
        collapseAll.style.marginLeft = 6;
        bar.Add(collapseAll);

        rootVisualElement.Add(bar);

        // If we lost the object reference across playmode/domain reload, try to resolve authoring scenario.
        if (!scenario) TryResolveAuthoringScenario();

        view = new ScenarioGraphView(this);
        view.OnContextAdd += ShowCreateMenu;
        view.OnMouseWorld += p => mouseWorld = p;
        view.OnEdgeDropped += ScheduleFullRouteSync;
        view.OnMouseUp += HandleMouseUp;
        view.OnMouseDown += () => movedNodesSinceMouseDown.Clear();
        view.OnViewTransformChanged += SaveViewTransform;
        rootVisualElement.Add(view);

        // NEW: start listening to playmode updates
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        _wasPlaying = Application.isPlaying;

        if (scenario != null) Load(scenario);
    }

    void OnDisable()
    {
        view?.RemoveFromHierarchy();
        nodes.Clear();

        // NEW: stop listening
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    void SetAllExpanded(bool on)
    {
        foreach (var kv in nodes)
        {
            var n = kv.Value;
            if (n == null) continue;
            if (n.IsNested) continue;
            n.SetExpanded(on);
        }
    }


    // ---------- load graph from component ----------
    void Load(Scenario sc)
    {
        // Capture current view transform before ClearGraph (Unity may reset it).
        SaveViewTransform();

        scenario = sc;
        // Persist selection across playmode/domain reload.
        try
        {
            if (scenario)
            {
                authoringScenarioGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(scenario).ToString();
                authoringScenarioScenePath = scenario.gameObject.scene.path;
                authoringScenarioGameObjectName = scenario.gameObject.name;
            }
        }
        catch { /* best-effort */ }
        titleContent = new GUIContent(sc ? $"Scenario Graph • {sc.gameObject.name}" : "Scenario Graph");

        _isLoading = true; // begin guarded load
        _suppressGraphPosWritesFrames = 2; // cover current + next layout tick
        
        _activeGuid = null;
        _prevGuid = null;
        UpdateNodeHighlights(null, null);

        view.ClearGraph();
        nodes.Clear();
        notes.Clear();
        groups.Clear();
        if (!scenario || scenario.steps == null)
        {
            _isLoading = false;
            return;
        }

        // ensure guids
        bool addedGuids = false;
        foreach (var s in scenario.steps)
        {
            if (s != null && string.IsNullOrEmpty(s.guid))
            {
                s.guid = Guid.NewGuid().ToString();
                addedGuids = true;
            }

            // ensure groups always have a list so container sizing works
            if (s is GroupStep g && g.steps == null)
            {
                g.steps = new List<Step>();
                addedGuids = true;
            }
        }

        if (addedGuids)
        {
            EditorUtility.SetDirty(scenario);
            if (scenario is Component c)
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        // nodes (top-level only)
        for (int i = 0; i < scenario.steps.Count; i++)
        {
            var s = scenario.steps[i];
            if (s == null) continue;

            bool startExpanded = GetExpanded(s.guid);

            var node = new StepNode(
                this,
                scenario,
                s,
                i,
                view,
                () => { if (!_isLoading) ScheduleFullRouteSync(); },
                OnSkipRequested,
                DeleteStep,
                DuplicateStep,
                startExpanded
            );

            // IMPORTANT: do not treat (0,0) as "unset" (it's a valid graph position).
            // Only auto-place when the stored position is invalid (NaN/Infinity).
            var fallbackPos = new Vector2(80 + 340 * i, 220);
            var pos = IsValidGraphPos(s.graphPos) ? s.graphPos : fallbackPos;
            float width = StepNodeWidth;
            if (s is GroupStep gs)
            {
                int c = gs.steps != null ? gs.steps.Count : 0;
                width = GetGroupPreferredWidth(c);
            }
            else if (startExpanded)
            {
                width = ExpandedWidthFor(s);
            }
            var h = node.GetHeight();
            if (s is GroupStep) h = Mathf.Max(h, 320f); // always show as container, even when empty
            node.SetPositionSilent(new Rect(pos, new Vector2(width, h)));
            view.AddElement(node);
            nodes[s.guid] = node;
        }

        // Grow group containers to fit their nested nodes so children are visually "inside".
        foreach (var st in scenario.steps)
        {
            if (st is not GroupStep grp) continue;
            if (!nodes.TryGetValue(grp.guid, out var grpNode) || grpNode == null) continue;
            FitGroupToChildren(grp, grpNode);
        }



        // draw edges from persisted data
        foreach (var s in scenario.steps)
        {
            if (s is TimelineStep tl && !string.IsNullOrEmpty(tl.nextGuid) && nodes.TryGetValue(tl.guid, out var tlNode))
                Connect(tlNode.outNext, tl.nextGuid);

            if (s is CueCardsStep cc && !string.IsNullOrEmpty(cc.nextGuid) && nodes.TryGetValue(cc.guid, out var ccNode))
                Connect(ccNode.outNext, cc.nextGuid);

            if (s is QuestionStep q && q.choices != null && nodes.TryGetValue(q.guid, out var src))
            {
                for (int c = 0; c < q.choices.Count; c++)
                {
                    var next = q.choices[c]?.nextGuid;
                    if (!string.IsNullOrEmpty(next) && src.outChoices != null && c < src.outChoices.Count)
                        Connect(src.outChoices[c], next);
                }
            }
            if (s is MiniQuizStep mq && nodes.TryGetValue(mq.guid, out var mqNode))
            {
                if (mq.outcomes != null)
                {
                    for (int o = 0; o < mq.outcomes.Count; o++)
                    {
                        var next = mq.outcomes[o]?.nextGuid;
                        if (!string.IsNullOrEmpty(next) && mqNode.outChoices != null && o < mqNode.outChoices.Count)
                            Connect(mqNode.outChoices[o], next);
                    }
                }
                if (!string.IsNullOrEmpty(mq.defaultNextGuid) && mqNode.outNext != null)
                    Connect(mqNode.outNext, mq.defaultNextGuid);
            }
            if (s is InsertStep ins && !string.IsNullOrEmpty(ins.nextGuid) && nodes.TryGetValue(ins.guid, out var insNode))
                Connect(insNode.outNext, ins.nextGuid);
            
            if (s is EventStep ev && !string.IsNullOrEmpty(ev.nextGuid) && nodes.TryGetValue(ev.guid, out var evNode))
                Connect(evNode.outNext, ev.nextGuid);

            if (s is GroupStep grp && nodes.TryGetValue(grp.guid, out var grpNode))
            {
                if (GroupUsesMultiConditionPorts(grp))
                {
                    if (grp.multiConditionBranches != null && grpNode.outChoices != null)
                    {
                        for (int mc = 0; mc < grp.multiConditionBranches.Count; mc++)
                        {
                            var next = grp.multiConditionBranches[mc]?.nextGuid;
                            if (!string.IsNullOrEmpty(next) && mc < grpNode.outChoices.Count)
                                Connect(grpNode.outChoices[mc], next);
                        }
                    }
                }
                else if (GroupUsesProxyBranchPorts(grp))
                {
                    var ch = TryGetGroupProxyBranchChild(grp);
                    if (ch is QuestionStep nq && nq.choices != null && grpNode.outChoices != null)
                    {
                        for (int c = 0; c < nq.choices.Count; c++)
                        {
                            var next = nq.choices[c]?.nextGuid;
                            if (!string.IsNullOrEmpty(next) && c < grpNode.outChoices.Count)
                                Connect(grpNode.outChoices[c], next);
                        }
                    }
                    else if (ch is ConditionsStep ncnd && ncnd.outcomes != null && grpNode.outChoices != null)
                    {
                        for (int b = 0; b < ncnd.outcomes.Count; b++)
                        {
                            var next = ncnd.outcomes[b]?.nextGuid;
                            if (!string.IsNullOrEmpty(next) && b < grpNode.outChoices.Count)
                                Connect(grpNode.outChoices[b], next);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(grp.nextGuid) && grpNode.outNext != null)
                    Connect(grpNode.outNext, grp.nextGuid);
            }

            if (s is ConditionsStep cnd && nodes.TryGetValue(cnd.guid, out var cndNode))
            {
                if (cnd.outcomes != null)
                {
                    for (int b = 0; b < cnd.outcomes.Count; b++)
                    {
                        var next = cnd.outcomes[b]?.nextGuid;
                        if (!string.IsNullOrEmpty(next) && cndNode.outChoices != null && b < cndNode.outChoices.Count)
                            Connect(cndNode.outChoices[b], next);
                    }
                }
            }

            if (s is QuizStep qz && nodes.TryGetValue(qz.guid, out var qzNode))
            {
                if (qz.completion == QuizStep.CompleteMode.BranchOnCorrectness)
                {
                    if (!string.IsNullOrEmpty(qz.correctNextGuid)) Connect(qzNode.outCorrect, qz.correctNextGuid);
                    if (!string.IsNullOrEmpty(qz.wrongNextGuid)) Connect(qzNode.outWrong, qz.wrongNextGuid);
                }
                else if (!string.IsNullOrEmpty(qz.nextGuid))
                {
                    Connect(qzNode.outNext, qz.nextGuid);
                }
            }

            if (s is QuizResultsStep qrs && nodes.TryGetValue(qrs.guid, out var qrsNode))
            {
                if (qrs.completion == QuizResultsStep.CompleteMode.BranchOnPassed)
                {
                    if (!string.IsNullOrEmpty(qrs.passedNextGuid)) Connect(qrsNode.outCorrect, qrs.passedNextGuid);
                    if (!string.IsNullOrEmpty(qrs.failedNextGuid)) Connect(qrsNode.outWrong, qrs.failedNextGuid);
                }
                else if (!string.IsNullOrEmpty(qrs.nextGuid))
                {
                    Connect(qrsNode.outNext, qrs.nextGuid);
                }
            }
        }
        // Selection edges (Correct/Wrong)
        foreach (var s in scenario.steps)
        {
            if (s is SelectionStep sel && nodes.TryGetValue(sel.guid, out var selNode))
            {
                if (!string.IsNullOrEmpty(sel.correctNextGuid)) Connect(selNode.outCorrect, sel.correctNextGuid);
                if (!string.IsNullOrEmpty(sel.wrongNextGuid)) Connect(selNode.outWrong, sel.wrongNextGuid);
            }
        }

        // --- Z-order pass (must be AFTER edges are created) ---
        // edges behind nodes (so they don't steal pointer events)
        foreach (var e in view.graphElements.ToList().OfType<Edge>())
            e.SendToBack();


        _isLoading = false; // end guarded load

        // Notes (editor-only)
#if UNITY_EDITOR
        if (scenario != null && scenario.GraphNotes != null)
        {
            foreach (var n in scenario.GraphNotes)
                AddOrUpdateNoteElement(n);
        }

        // Visual organizing groups (editor-only) — drawn behind the nodes.
        if (scenario != null && scenario.GraphGroups != null)
        {
            foreach (var g in scenario.GraphGroups)
                AddOrUpdateGroupElement(g);
        }

        // After layout settles, place attached notes relative to their nodes and draw connectors.
        view.schedule.Execute(() => ReflowAttachedNotes()).ExecuteLater(60);
#endif

        // Restore view transform (pan/zoom) after graph rebuild.
        RestoreViewTransform();

        view.graphViewChanged = change =>
        {
            // During Load() we programmatically add/move elements; GraphView can report movedElements.
            // Never write back graphPos/Undo during loading, otherwise Refresh can overwrite saved positions (often to 0,0).
            if (_isLoading || _suppressGraphPosWritesFrames > 0) return change;

            // positions
            if (change.movedElements != null)
            {
                bool anyNodeMoved = false;
                foreach (var el in change.movedElements)
                    {
                    if (el is not StepNode sn) continue;

                        sn.step.graphPos = sn.GetPosition().position;
                        Dirty(scenario, "Move Node");

                        // Drag any notes attached to this node along with it.
                        RepositionAttachedNotes(sn);
                        anyNodeMoved = true;

                    movedNodesSinceMouseDown.Add(sn);
                }
                if (anyNodeMoved) view?.RefreshTethers();
                    }

            // edges created/removed
            if (!_isLoading && scenario != null)
            {
                bool routesChanged = false;

                if (change.elementsToRemove != null)
                    routesChanged |= ApplyRouteRemovals(change.elementsToRemove);

                if (change.edgesToCreate != null)
                    routesChanged |= ApplyRouteCreates(change.edgesToCreate);

                if (routesChanged)
                    Dirty(scenario, "Route Change");
            }

            return change;
        };
    }

    void HandleMouseUp()
    {
        if (_isLoading || scenario == null) { movedNodesSinceMouseDown.Clear(); return; }
        // Fallback: GraphView doesn't always report movedElements, so use current selection if needed.
        if (movedNodesSinceMouseDown.Count == 0)
        {
            if (view != null)
            {
                foreach (var sel in view.selection)
                    if (sel is StepNode sn) movedNodesSinceMouseDown.Add(sn);
            }
        }

        if (movedNodesSinceMouseDown.Count == 0) return;

        // If the user dragged one or more nodes over a GroupStep container node, move those steps into the group.
        var groupTargets = nodes.Values.Where(n => n != null && !n.IsNested && n.step is GroupStep).ToList();
        if (groupTargets.Count == 0) { movedNodesSinceMouseDown.Clear(); return; }

        bool changed = false;
        var removeFromView = new List<GraphElement>();

        foreach (var moved in movedNodesSinceMouseDown.ToList())
        {
            if (moved == null || moved.step == null) continue;
            if (moved.step is GroupStep) continue; // don't drag groups into groups (yet)

            var movedRect = moved.GetPosition();
            var center = movedRect.center;

            // Top-level steps can be moved into a group.
            int topIndex = scenario.steps.IndexOf(moved.step);
            if (topIndex < 0) continue;

            StepNode target = null;
            foreach (var gnode in groupTargets)
            {
                if (gnode == null) continue;
                var grect = gnode.GetPosition();
                // forgiving: any overlap counts, not just center
                if (grect.Overlaps(movedRect))
                {
                    target = gnode;
                    break;
                }
            }

            if (target?.step is not GroupStep grp) continue;

            Dirty(scenario, "Move Step Into Group");
            scenario.steps.RemoveAt(topIndex);
            grp.steps ??= new List<Step>();

            // IMPORTANT: if anything was routed into this step, re-route it into the group instead.
            RedirectIncomingRoutes(moved.step.guid, grp.guid);

            // If the group has no Next yet and this step was a linear step, adopt its next as the group's next.
            if (string.IsNullOrEmpty(grp.nextGuid))
            {
                var adopted = TryGetLinearNextGuid(moved.step);
                if (!string.IsNullOrEmpty(adopted))
                    grp.nextGuid = adopted;
            }

            // UX: always append in order. Tiles represent ordering clearly.
            grp.steps.Add(moved.step);
            changed = true;

            // IMPORTANT: visually remove the old top-level node immediately to avoid "stale" nodes
            // hanging around until the delayed Load() happens.
            removeFromView.Add(moved);
        }

        movedNodesSinceMouseDown.Clear();

        if (changed)
        {
            // Remove moved nodes (and their attached edges) immediately for clean UX.
            if (view != null && removeFromView.Count > 0)
            {
                // Remove edges connected to nodes we're removing
                var toRemove = new HashSet<GraphElement>(removeFromView);
                foreach (var e in view.graphElements.ToList().OfType<Edge>())
                {
                    if (e?.input?.node is StepNode inNode && toRemove.Contains(inNode)) { view.RemoveElement(e); continue; }
                    if (e?.output?.node is StepNode outNode && toRemove.Contains(outNode)) { view.RemoveElement(e); continue; }
                }

                foreach (var ge in removeFromView)
                    if (ge != null) view.RemoveElement(ge);
            }

            // Rebuild graph from serialized truth AFTER GraphView finishes its drag cycle.
            // (Immediate rebuild can leave SelectionDragger in a bad state and cause weird scrolling.)
            view?.ClearSelection();
            EditorApplication.delayCall += () =>
            {
                if (this != null && scenario != null)
                    Load(scenario);
            };
        }
    }

    static string TryGetLinearNextGuid(Step s)
    {
        if (s is TimelineStep tl) return tl.nextGuid;
        if (s is CueCardsStep cc) return cc.nextGuid;
        if (s is InsertStep ins) return ins.nextGuid;
        if (s is EventStep ev) return ev.nextGuid;
        return null;
    }

    void RedirectIncomingRoutes(string fromGuid, string toGuid)
    {
        if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid) || scenario == null) return;
        if (scenario.steps == null) return;

        foreach (var st in scenario.steps)
        {
            if (st == null) continue;

            if (st is TimelineStep tl && tl.nextGuid == fromGuid) tl.nextGuid = toGuid;
            else if (st is CueCardsStep cc && cc.nextGuid == fromGuid) cc.nextGuid = toGuid;
            else if (st is InsertStep ins && ins.nextGuid == fromGuid) ins.nextGuid = toGuid;
            else if (st is EventStep ev && ev.nextGuid == fromGuid) ev.nextGuid = toGuid;
            else if (st is GroupStep g && g.nextGuid == fromGuid) g.nextGuid = toGuid;
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && ch.nextGuid == fromGuid)
                        ch.nextGuid = toGuid;
            }
            else if (st is MiniQuizStep mq)
            {
                if (mq.defaultNextGuid == fromGuid) mq.defaultNextGuid = toGuid;
                if (mq.outcomes != null)
                {
                    foreach (var o in mq.outcomes)
                        if (o != null && o.nextGuid == fromGuid)
                            o.nextGuid = toGuid;
                }
            }
            else if (st is ConditionsStep cnd)
            {
                if (cnd.outcomes != null)
                {
                    foreach (var b in cnd.outcomes)
                        if (b != null && b.nextGuid == fromGuid)
                            b.nextGuid = toGuid;
                }
            }
            else if (st is SelectionStep sel)
            {
                if (sel.correctNextGuid == fromGuid) sel.correctNextGuid = toGuid;
                if (sel.wrongNextGuid == fromGuid) sel.wrongNextGuid = toGuid;
            }
        }
    }

    void FitGroupToChildren(GroupStep grp, StepNode grpNode)
    {
        if (grp == null || grpNode == null) return;

        int count = grp.steps != null ? grp.steps.Count : 0;
        int rows = Mathf.CeilToInt(count / (float)GroupTileColumns);

        float tilesH = count == 0
            ? 64f
            : (rows * GroupTileH) + Mathf.Max(0, rows - 1) * GroupTileGapY + GroupTilesPadY;

        float reqW = GetGroupPreferredWidth(count);
        // Make sure the "Nested Steps" preview lines are never clipped.
        // Base + per-line (up to 8 shown) + small buffer.
        float mcBranchH = 0f;
        if (grp.completeWhen == GroupStep.CompleteWhen.MultiCondition &&
            grp.multiConditionBranches != null && grp.multiConditionBranches.Count > 0)
        {
            for (int b = 0; b < grp.multiConditionBranches.Count; b++)
            {
                float perBranch = 80f;
                var branch = grp.multiConditionBranches[b];
                if (branch != null)
                {
                    if (branch.mode == GroupStep.CompleteWhen.RequiredChildrenComplete ||
                        branch.mode == GroupStep.CompleteWhen.NOfMChildrenComplete)
                        perBranch += Mathf.Min(8, count) * 18f + 22f;
                }
                mcBranchH += perBranch;
            }
            mcBranchH += 40f;
        }
        float expandedSettingsH = GroupSettingsApproxH + Mathf.Min(8, count) * 18f + 24f + mcBranchH;
        float settingsH = grpNode.GroupSettingsExpanded ? expandedSettingsH : GroupSettingsCollapsedMinH;
        float reqH = Mathf.Max(260f, GroupHeaderH + tilesH + settingsH);

        // IMPORTANT: always anchor to serialized graphPos.
        // GraphView can temporarily report a rect at (0,0) during Refresh/layout which would "snap" the group.
        var newRect = new Rect(grp.graphPos, new Vector2(reqW, reqH));
        grpNode.SetPositionSilent(newRect);
    }

    static float GetGroupPreferredWidth(int tileCount)
    {
        // 0-1 tiles => 1 col, 2+ tiles => 2 cols
        int cols = tileCount <= 1 ? 1 : GroupTileColumns;
        float contentW = (cols * GroupTileW) + ((cols - 1) * GroupTileGapX);
        // padding on both sides + a small buffer for borders/ports
        float w = contentW + GroupTilesPadX * 2 + 24f;
        return Mathf.Max(420f, w);
    }

    static int CountRequiredChildren(GroupStep g)
    {
        if (g == null || g.steps == null) return 0;
        int count = 0;
        for (int i = 0; i < g.steps.Count; i++)
        {
            var st = g.steps[i];
            if (st != null && g.IsChildRequired(st.guid)) count++;
        }
        return count;
    }

    static string GetSpecificChildLabel(GroupStep g)
    {
        if (g == null || g.steps == null || string.IsNullOrEmpty(g.specificStepGuid)) return "None";
        for (int i = 0; i < g.steps.Count; i++)
        {
            var st = g.steps[i];
            if (st != null && st.guid == g.specificStepGuid)
                return $"{i + 1}. {st.Kind}";
        }
        return "None";
    }

    static string GroupSummary(GroupStep g)
    {
        if (g == null) return "Complete: -";
        int total = g.steps != null ? g.steps.Count : 0;

        switch (g.completeWhen)
        {
            case GroupStep.CompleteWhen.AnyChildCompletes:
                return "Complete: Any";
            case GroupStep.CompleteWhen.SpecificChildCompletes:
                return $"Complete: Target = {GetSpecificChildLabel(g)}";
            case GroupStep.CompleteWhen.RequiredChildrenComplete:
                return $"Complete: Required ({CountRequiredChildren(g)}/{total})";
            case GroupStep.CompleteWhen.NOfMChildrenComplete:
                return $"Complete: {Mathf.Clamp(g.requiredCount, 1, Mathf.Max(1, total))} of {total}";
            case GroupStep.CompleteWhen.MultiCondition:
                int mc = g.multiConditionBranches != null ? g.multiConditionBranches.Count : 0;
                return $"Complete: Multi-Condition ({mc} branch{(mc == 1 ? "" : "es")})";
            case GroupStep.CompleteWhen.AllChildrenComplete:
            default:
                return "Complete: All";
        }
    }

    static void SetGroupChildRequired(GroupStep g, Step st, bool required)
    {
        if (g == null || st == null) return;
        g.EnsureChildRequirements();
        if (g.childRequirements == null) return;

        for (int i = 0; i < g.childRequirements.Count; i++)
        {
            var req = g.childRequirements[i];
            if (req != null && req.guid == st.guid)
            {
                req.required = required;
                return;
            }
        }
        g.childRequirements.Add(new GroupStep.ChildRequirement { guid = st.guid, required = required });
    }

    static void SetBranchChildRequired(GroupStep.MultiConditionBranch branch, Step st, bool required)
    {
        if (branch == null || st == null) return;
        if (branch.childRequirements == null)
            branch.childRequirements = new List<GroupStep.ChildRequirement>();

        for (int i = 0; i < branch.childRequirements.Count; i++)
        {
            var r = branch.childRequirements[i];
            if (r != null && r.guid == st.guid)
            {
                r.required = required;
                return;
            }
        }
        branch.childRequirements.Add(new GroupStep.ChildRequirement { guid = st.guid, required = required });
    }

    static void DrawMultiConditionBranchList(GroupStep g)
    {
        if (g == null) return;
        if (g.multiConditionBranches == null)
            g.multiConditionBranches = new List<GroupStep.MultiConditionBranch>();

        g.EnsureMultiConditionBranchRequirements();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Conditions (first match wins)", EditorStyles.boldLabel);

        int removeAt = -1;
        int moveUp = -1;
        int moveDown = -1;
        var baseModes = new GroupStep.CompleteWhen[]
        {
            GroupStep.CompleteWhen.AllChildrenComplete,
            GroupStep.CompleteWhen.AnyChildCompletes,
            GroupStep.CompleteWhen.SpecificChildCompletes,
            GroupStep.CompleteWhen.RequiredChildrenComplete,
            GroupStep.CompleteWhen.NOfMChildrenComplete,
        };
        string[] baseModeNames = new string[]
        {
            "All Children Complete",
            "Any Child Completes",
            "Specific Child Completes",
            "Required Children Complete",
            "N Of M Children Complete",
        };

        for (int b = 0; b < g.multiConditionBranches.Count; b++)
        {
            var branch = g.multiConditionBranches[b];
            if (branch == null) continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Condition {b + 1}", EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            if (b > 0 && GUILayout.Button("\u25B2", GUILayout.Width(24))) moveUp = b;
            if (b < g.multiConditionBranches.Count - 1 && GUILayout.Button("\u25BC", GUILayout.Width(24))) moveDown = b;
            if (GUILayout.Button("X", GUILayout.Width(24))) removeAt = b;
            EditorGUILayout.EndHorizontal();

            branch.label = EditorGUILayout.TextField("Label", branch.label);

            int curModeIdx = System.Array.IndexOf(baseModes, branch.mode);
            if (curModeIdx < 0) curModeIdx = 0;
            int newModeIdx = EditorGUILayout.Popup("Mode", curModeIdx, baseModeNames);
            branch.mode = baseModes[Mathf.Clamp(newModeIdx, 0, baseModes.Length - 1)];

            int childCount = g.steps != null ? g.steps.Count : 0;

            if (branch.mode == GroupStep.CompleteWhen.NOfMChildrenComplete)
            {
                branch.requiredCount = Mathf.Max(1, EditorGUILayout.IntField("Required Count", branch.requiredCount));
                if (childCount > 0 && branch.requiredCount > childCount)
                    branch.requiredCount = childCount;
            }
            else if (branch.mode == GroupStep.CompleteWhen.SpecificChildCompletes)
            {
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
                    if (!string.IsNullOrEmpty(branch.specificStepGuid))
                    {
                        int idx = guids.IndexOf(branch.specificStepGuid);
                        if (idx >= 0) cur = idx;
                    }

                    int next = EditorGUILayout.Popup("Specific Step", cur, options.ToArray());
                    branch.specificStepGuid = guids[Mathf.Clamp(next, 0, guids.Count - 1)];
                }
                else
                {
                    branch.specificStepGuid = EditorGUILayout.TextField("Specific Step Guid", branch.specificStepGuid);
                }
            }

            if (branch.mode == GroupStep.CompleteWhen.RequiredChildrenComplete ||
                branch.mode == GroupStep.CompleteWhen.NOfMChildrenComplete)
            {
                EditorGUILayout.LabelField("Required Children", EditorStyles.miniLabel);
                if (g.steps != null && g.steps.Count > 0)
                {
                    for (int i = 0; i < g.steps.Count; i++)
                    {
                        var st = g.steps[i];
                        if (st == null) continue;
                        bool req = GroupStep.IsChildRequiredInList(branch.childRequirements, st.guid);
                        bool nxt = EditorGUILayout.ToggleLeft($"{i + 1}. {st.Kind}", req);
                        if (nxt != req) SetBranchChildRequired(branch, st, nxt);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        if (removeAt >= 0) g.multiConditionBranches.RemoveAt(removeAt);
        if (moveUp >= 1)
        {
            var tmp = g.multiConditionBranches[moveUp];
            g.multiConditionBranches[moveUp] = g.multiConditionBranches[moveUp - 1];
            g.multiConditionBranches[moveUp - 1] = tmp;
        }
        if (moveDown >= 0 && moveDown < g.multiConditionBranches.Count - 1)
        {
            var tmp = g.multiConditionBranches[moveDown];
            g.multiConditionBranches[moveDown] = g.multiConditionBranches[moveDown + 1];
            g.multiConditionBranches[moveDown + 1] = tmp;
        }

        if (GUILayout.Button("+ Add Condition"))
        {
            g.multiConditionBranches.Add(new GroupStep.MultiConditionBranch());
            g.EnsureMultiConditionBranchRequirements();
        }
    }

    void ScheduleResizeGroup(string groupGuid)
    {
        if (string.IsNullOrEmpty(groupGuid)) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null || scenario == null) return;
            var grp = scenario.steps.OfType<GroupStep>().FirstOrDefault(g => g != null && g.guid == groupGuid);
            if (grp == null) return;
            if (!nodes.TryGetValue(groupGuid, out var node) || node == null) return;
            FitGroupToChildren(grp, node);
        };
    }

    void ScheduleFullRouteSync()
    {
        if (_pendingFullRouteSync) return;
        _pendingFullRouteSync = true;

        // Defer one tick so GraphView has actually updated/remapped ports/edges.
        EditorApplication.delayCall += () =>
        {
            _pendingFullRouteSync = false;
            if (this == null || view == null) return;
            SyncRoutesFromGraph();
        };
    }

    void SyncRoutesFromGraph()
    {
        if (!scenario || _isLoading) return;

        // Compute desired routing from current edges; then apply.
        // (We avoid clearing everything up-front during an in-flight GraphView change.)

        // Default everything to "linear next" (empty string)
        foreach (var st in scenario.steps)
        {
            if (st is TimelineStep tl) tl.nextGuid = "";
            else if (st is CueCardsStep cc) cc.nextGuid = "";
            else if (st is InsertStep ins) ins.nextGuid = "";
            else if (st is EventStep ev) ev.nextGuid = "";
            else if (st is GroupStep g)
            {
                g.nextGuid = "";
                if (g.steps != null)
                {
                    foreach (var nested in g.steps)
                    {
                        if (nested == null) continue;
                        if (nested is QuestionStep nq && nq.choices != null)
                        {
                            foreach (var ch in nq.choices)
                                if (ch != null) ch.nextGuid = "";
                        }
                        else if (nested is ConditionsStep ncnd && ncnd.outcomes != null)
                        {
                            foreach (var b in ncnd.outcomes)
                                if (b != null) b.nextGuid = "";
                        }
                    }
                }
            }
            else if (st is QuizStep qz)
            {
                qz.nextGuid = "";
                qz.correctNextGuid = "";
                qz.wrongNextGuid = "";
            }
            else if (st is QuizResultsStep qrs)
            {
                qrs.nextGuid = "";
                qrs.passedNextGuid = "";
                qrs.failedNextGuid = "";
            }
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null) ch.nextGuid = "";
            }
            else if (st is MiniQuizStep mq)
            {
                mq.defaultNextGuid = "";
                if (mq.outcomes != null)
                {
                    foreach (var o in mq.outcomes)
                        if (o != null) o.nextGuid = "";
                }
            }
            else if (st is ConditionsStep cnd)
            {
                if (cnd.outcomes != null)
                {
                    foreach (var b in cnd.outcomes)
                        if (b != null) b.nextGuid = "";
                }
            }

            if (st is SelectionStep sl)
            {
                sl.correctNextGuid = "";
                sl.wrongNextGuid = "";
            }
        }

        // Apply from edges that exist in the view
        foreach (var e in view.graphElements.ToList().OfType<Edge>())
            ApplyRouteCreate(e);

        Dirty(scenario, "Route Change");
    }

    bool ApplyRouteCreates(IEnumerable<Edge> edgesToCreate)
    {
        bool changed = false;
        foreach (var e in edgesToCreate)
            changed |= ApplyRouteCreate(e);
        return changed;
    }

    bool ApplyRouteCreate(Edge e)
    {
        if (e == null) return false;
        var outMeta = PortMeta.From(e.output);
        var inNode = e.input?.node as StepNode;
        if (outMeta == null || inNode?.step == null) return false;
        if (string.IsNullOrEmpty(inNode.step.guid)) return false;

        string dstGuid = inNode.step.guid;
        bool changed = false;

        if (outMeta.owner is TimelineStep otl)
        {
            if (otl.nextGuid != dstGuid) { otl.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is CueCardsStep occ)
        {
            if (occ.nextGuid != dstGuid) { occ.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is InsertStep oins)
        {
            if (oins.nextGuid != dstGuid) { oins.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is EventStep oev)
        {
            if (oev.nextGuid != dstGuid) { oev.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is GroupStep og)
        {
            if (outMeta.choiceIndex >= 0 &&
                og.completeWhen == GroupStep.CompleteWhen.MultiCondition &&
                og.multiConditionBranches != null &&
                outMeta.choiceIndex < og.multiConditionBranches.Count &&
                og.multiConditionBranches[outMeta.choiceIndex] != null)
            {
                var mb = og.multiConditionBranches[outMeta.choiceIndex];
                if (mb.nextGuid != dstGuid) { mb.nextGuid = dstGuid; changed = true; }
            }
            else
            {
                if (og.nextGuid != dstGuid) { og.nextGuid = dstGuid; changed = true; }
            }
        }
        else if (outMeta.owner is QuestionStep oq &&
                 outMeta.choiceIndex >= 0 &&
                 oq.choices != null &&
                 outMeta.choiceIndex < oq.choices.Count &&
                 oq.choices[outMeta.choiceIndex] != null)
        {
            var ch = oq.choices[outMeta.choiceIndex];
            if (ch.nextGuid != dstGuid) { ch.nextGuid = dstGuid; changed = true; }
        }
        else if (outMeta.owner is MiniQuizStep omq)
        {
            // -1 = default route, >=0 = outcomes index
            if (outMeta.choiceIndex == -1)
            {
                if (omq.defaultNextGuid != dstGuid) { omq.defaultNextGuid = dstGuid; changed = true; }
            }
            else if (outMeta.choiceIndex >= 0 &&
                     omq.outcomes != null &&
                     outMeta.choiceIndex < omq.outcomes.Count &&
                     omq.outcomes[outMeta.choiceIndex] != null)
            {
                var o = omq.outcomes[outMeta.choiceIndex];
                if (o.nextGuid != dstGuid) { o.nextGuid = dstGuid; changed = true; }
            }
        }
        else if (outMeta.owner is ConditionsStep ocnd)
        {
            if (outMeta.choiceIndex >= 0 &&
                     ocnd.outcomes != null &&
                     outMeta.choiceIndex < ocnd.outcomes.Count &&
                     ocnd.outcomes[outMeta.choiceIndex] != null)
            {
                var b = ocnd.outcomes[outMeta.choiceIndex];
                if (b.nextGuid != dstGuid) { b.nextGuid = dstGuid; changed = true; }
            }
        }
        else if (outMeta.owner is SelectionStep osl)
        {
            // -2 => Correct, -3 => Wrong
            if (outMeta.choiceIndex == -2)
            {
                if (osl.correctNextGuid != dstGuid) { osl.correctNextGuid = dstGuid; changed = true; }
            }
            else if (outMeta.choiceIndex == -3)
            {
                if (osl.wrongNextGuid != dstGuid) { osl.wrongNextGuid = dstGuid; changed = true; }
            }
        }
        else if (outMeta.owner is QuizStep oqz)
        {
            if (outMeta.choiceIndex == -2)
            {
                if (oqz.correctNextGuid != dstGuid) { oqz.correctNextGuid = dstGuid; changed = true; }
            }
            else if (outMeta.choiceIndex == -3)
            {
                if (oqz.wrongNextGuid != dstGuid) { oqz.wrongNextGuid = dstGuid; changed = true; }
            }
            else
            {
                if (oqz.nextGuid != dstGuid) { oqz.nextGuid = dstGuid; changed = true; }
            }
        }
        else if (outMeta.owner is QuizResultsStep oqrs)
        {
            if (outMeta.choiceIndex == -2)
            {
                if (oqrs.passedNextGuid != dstGuid) { oqrs.passedNextGuid = dstGuid; changed = true; }
            }
            else if (outMeta.choiceIndex == -3)
            {
                if (oqrs.failedNextGuid != dstGuid) { oqrs.failedNextGuid = dstGuid; changed = true; }
            }
            else
            {
                if (oqrs.nextGuid != dstGuid) { oqrs.nextGuid = dstGuid; changed = true; }
            }
        }

        return changed;
    }

    bool ApplyRouteRemovals(IEnumerable<GraphElement> removed)
    {
        bool changed = false;

        foreach (var el in removed)
        {
            if (el is not Edge e) continue;

            var outMeta = PortMeta.From(e.output);
            if (outMeta == null) continue;

            // Output ports are Capacity.Single in this graph, so removal means "clear that route".
            if (outMeta.owner is TimelineStep otl)
            {
                if (!string.IsNullOrEmpty(otl.nextGuid)) { otl.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is CueCardsStep occ)
            {
                if (!string.IsNullOrEmpty(occ.nextGuid)) { occ.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is InsertStep oins)
            {
                if (!string.IsNullOrEmpty(oins.nextGuid)) { oins.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is EventStep oev)
            {
                if (!string.IsNullOrEmpty(oev.nextGuid)) { oev.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is GroupStep og)
            {
                if (outMeta.choiceIndex >= 0 &&
                    og.completeWhen == GroupStep.CompleteWhen.MultiCondition &&
                    og.multiConditionBranches != null &&
                    outMeta.choiceIndex < og.multiConditionBranches.Count &&
                    og.multiConditionBranches[outMeta.choiceIndex] != null)
                {
                    var mb = og.multiConditionBranches[outMeta.choiceIndex];
                    if (!string.IsNullOrEmpty(mb.nextGuid)) { mb.nextGuid = ""; changed = true; }
                }
                else
                {
                    if (!string.IsNullOrEmpty(og.nextGuid)) { og.nextGuid = ""; changed = true; }
                }
            }
            else if (outMeta.owner is QuestionStep oq &&
                     outMeta.choiceIndex >= 0 &&
                     oq.choices != null &&
                     outMeta.choiceIndex < oq.choices.Count &&
                     oq.choices[outMeta.choiceIndex] != null)
            {
                var ch = oq.choices[outMeta.choiceIndex];
                if (!string.IsNullOrEmpty(ch.nextGuid)) { ch.nextGuid = ""; changed = true; }
            }
            else if (outMeta.owner is MiniQuizStep omq)
            {
                if (outMeta.choiceIndex == -1)
                {
                    if (!string.IsNullOrEmpty(omq.defaultNextGuid)) { omq.defaultNextGuid = ""; changed = true; }
                }
                else if (outMeta.choiceIndex >= 0 &&
                         omq.outcomes != null &&
                         outMeta.choiceIndex < omq.outcomes.Count &&
                         omq.outcomes[outMeta.choiceIndex] != null)
                {
                    var o = omq.outcomes[outMeta.choiceIndex];
                    if (!string.IsNullOrEmpty(o.nextGuid)) { o.nextGuid = ""; changed = true; }
                }
            }
                else if (outMeta.owner is ConditionsStep ocnd)
                {
                    if (outMeta.choiceIndex >= 0 &&
                         ocnd.outcomes != null &&
                         outMeta.choiceIndex < ocnd.outcomes.Count &&
                         ocnd.outcomes[outMeta.choiceIndex] != null)
                {
                    var b = ocnd.outcomes[outMeta.choiceIndex];
                    if (!string.IsNullOrEmpty(b.nextGuid)) { b.nextGuid = ""; changed = true; }
                }
            }
            else if (outMeta.owner is SelectionStep osl)
            {
                if (outMeta.choiceIndex == -2)
                {
                    if (!string.IsNullOrEmpty(osl.correctNextGuid)) { osl.correctNextGuid = ""; changed = true; }
                }
                else if (outMeta.choiceIndex == -3)
                {
                    if (!string.IsNullOrEmpty(osl.wrongNextGuid)) { osl.wrongNextGuid = ""; changed = true; }
                }
            }
            else if (outMeta.owner is QuizStep oqz)
            {
                if (outMeta.choiceIndex == -2)
                {
                    if (!string.IsNullOrEmpty(oqz.correctNextGuid)) { oqz.correctNextGuid = ""; changed = true; }
                }
                else if (outMeta.choiceIndex == -3)
                {
                    if (!string.IsNullOrEmpty(oqz.wrongNextGuid)) { oqz.wrongNextGuid = ""; changed = true; }
                }
                else
                {
                    if (!string.IsNullOrEmpty(oqz.nextGuid)) { oqz.nextGuid = ""; changed = true; }
                }
            }
            else if (outMeta.owner is QuizResultsStep oqrs)
            {
                if (outMeta.choiceIndex == -2)
                {
                    if (!string.IsNullOrEmpty(oqrs.passedNextGuid)) { oqrs.passedNextGuid = ""; changed = true; }
                }
                else if (outMeta.choiceIndex == -3)
                {
                    if (!string.IsNullOrEmpty(oqrs.failedNextGuid)) { oqrs.failedNextGuid = ""; changed = true; }
                }
                else
                {
                    if (!string.IsNullOrEmpty(oqrs.nextGuid)) { oqrs.nextGuid = ""; changed = true; }
                }
            }
        }

        return changed;
    }

    void AutoLayout()
    {
        if (scenario == null || scenario.steps == null || nodes.Count == 0)
            return;

        // Build incoming edge count and adjacency
        var incoming = new Dictionary<string, int>();
        var neighbors = new Dictionary<string, List<string>>();

        foreach (var kv in nodes)
        {
            incoming[kv.Key] = 0;
            neighbors[kv.Key] = new List<string>();
        }

        void AddEdge(string fromGuid, string toGuid)
        {
            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid))
                return;
            if (!neighbors.ContainsKey(fromGuid) || !incoming.ContainsKey(toGuid))
                return;

            neighbors[fromGuid].Add(toGuid);
            incoming[toGuid] += 1;
        }

        foreach (var st in scenario.steps)
        {
            if (st == null || string.IsNullOrEmpty(st.guid)) continue;

            string from = st.guid;

            if (st is TimelineStep tl && !string.IsNullOrEmpty(tl.nextGuid))
                AddEdge(from, tl.nextGuid);
            else if (st is CueCardsStep cc && !string.IsNullOrEmpty(cc.nextGuid))
                AddEdge(from, cc.nextGuid);
            else if (st is InsertStep ins && !string.IsNullOrEmpty(ins.nextGuid))
                AddEdge(from, ins.nextGuid);
            else if (st is EventStep ev && !string.IsNullOrEmpty(ev.nextGuid))
                AddEdge(from, ev.nextGuid);
            else if (st is GroupStep g)
                AddEdgesFromGroupForLayout(g, (a, b) => AddEdge(a, b));
            else if (st is QuizStep qz && !string.IsNullOrEmpty(qz.nextGuid))
                AddEdge(from, qz.nextGuid);
            else if (st is QuizResultsStep qrs && !string.IsNullOrEmpty(qrs.nextGuid))
                AddEdge(from, qrs.nextGuid);

            if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && !string.IsNullOrEmpty(ch.nextGuid))
                        AddEdge(from, ch.nextGuid);
            }
            if (st is MiniQuizStep mq)
            {
                if (!string.IsNullOrEmpty(mq.defaultNextGuid))
                    AddEdge(from, mq.defaultNextGuid);
                if (mq.outcomes != null)
                {
                    foreach (var o in mq.outcomes)
                        if (o != null && !string.IsNullOrEmpty(o.nextGuid))
                            AddEdge(from, o.nextGuid);
                }
            }
            if (st is ConditionsStep cnd)
            {
                if (cnd.outcomes != null)
                {
                    foreach (var b in cnd.outcomes)
                        if (b != null && !string.IsNullOrEmpty(b.nextGuid))
                            AddEdge(from, b.nextGuid);
                }
            }

            if (st is SelectionStep sel)
            {
                if (!string.IsNullOrEmpty(sel.correctNextGuid))
                    AddEdge(from, sel.correctNextGuid);
                if (!string.IsNullOrEmpty(sel.wrongNextGuid))
                    AddEdge(from, sel.wrongNextGuid);
            }
            if (st is QuizStep qzb && qzb.completion == QuizStep.CompleteMode.BranchOnCorrectness)
            {
                if (!string.IsNullOrEmpty(qzb.correctNextGuid))
                    AddEdge(from, qzb.correctNextGuid);
                if (!string.IsNullOrEmpty(qzb.wrongNextGuid))
                    AddEdge(from, qzb.wrongNextGuid);
            }
            if (st is QuizResultsStep qrsb && qrsb.completion == QuizResultsStep.CompleteMode.BranchOnPassed)
            {
                if (!string.IsNullOrEmpty(qrsb.passedNextGuid))
                    AddEdge(from, qrsb.passedNextGuid);
                if (!string.IsNullOrEmpty(qrsb.failedNextGuid))
                    AddEdge(from, qrsb.failedNextGuid);
            }
        }

        // Roots = no incoming. If none, start from first step
        var roots = incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
        if (roots.Count == 0 && scenario.steps.Count > 0 && !string.IsNullOrEmpty(scenario.steps[0].guid))
            roots.Add(scenario.steps[0].guid);

        var level = new Dictionary<string, int>();
        var queue = new Queue<string>();

        foreach (var r in roots)
        {
            level[r] = 0;
            queue.Enqueue(r);
        }

        // BFS: assign each node a level ONCE → no infinite loop in cycles
        while (queue.Count > 0)
        {
            var g = queue.Dequeue();
            int l = level[g];

            if (!neighbors.TryGetValue(g, out var neighList)) continue;

            foreach (var n in neighList)
            {
                if (level.ContainsKey(n))   // already visited, do not enqueue again
                    continue;

                level[n] = l + 1;
                queue.Enqueue(n);
            }
        }

        // Nodes not reached from any root (disconnected or pure cycles)
        int maxLevel = level.Count > 0 ? level.Values.Max() : 0;
        foreach (var kv in nodes)
        {
            if (!level.ContainsKey(kv.Key))
                level[kv.Key] = maxLevel + 1;
        }

        // Group by level
        var levels = level
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Key).ToList());

        // Layout constants (dynamic spacing based on node sizes)
        const float xStart = 80f;
        const float yStart = 120f;
        const float xGap = 70f;
        const float yGap = 70f;

        // Precompute desired size per node (Group nodes are wider)
        var sizeByGuid = new Dictionary<string, Vector2>();
        foreach (var kv in nodes)
        {
            var guid = kv.Key;
            var node = kv.Value;
            if (node == null || node.step == null) continue;

            float w = StepNodeWidth;
            float h = node.GetHeight();

            if (node.step is GroupStep gs)
            {
                int c = gs.steps != null ? gs.steps.Count : 0;
                w = GetGroupPreferredWidth(c);
                // ensure the rect accounts for the UI tiles/settings state
                FitGroupToChildren(gs, node);
                h = Mathf.Max(h, node.GetPosition().height);
            }
            else if (node.UserSized)
            {
                w = node.UserSize.x; // GetHeight() already returns the manual height
            }
            else if (node.IsExpanded)
            {
                w = ExpandedWidthFor(node.step);
            }

            sizeByGuid[guid] = new Vector2(w, h);
        }

        // Compute x offsets per level based on widest node in each column
        var levelKeys = levels.Keys.OrderBy(k => k).ToList();
        var xByLevel = new Dictionary<int, float>();
        float curX = xStart;
        foreach (var lvKey in levelKeys)
        {
            xByLevel[lvKey] = curX;
            float colW = 360f;
            foreach (var guid in levels[lvKey])
                if (sizeByGuid.TryGetValue(guid, out var sz))
                    colW = Mathf.Max(colW, sz.x);
            curX += colW + xGap;
        }

        // Apply positions (stack within each level using actual heights)
        _isLoading = true;
        Dirty(scenario, "Rearrange Graph");

        foreach (var lv in levelKeys)
        {
            var guidsAtLevel = levels[lv]
                .OrderBy(guid => scenario.steps.FindIndex(s => s != null && s.guid == guid))
                .ToList();

            float y = yStart;
            float x = xByLevel[lv];

            foreach (var guid in guidsAtLevel)
            {
                if (!nodes.TryGetValue(guid, out var node) || node == null) continue;
                if (!sizeByGuid.TryGetValue(guid, out var sz)) sz = new Vector2(StepNodeWidth, node.GetHeight());

                node.SetPositionSilent(new Rect(new Vector2(x, y), sz));
                node.step.graphPos = new Vector2(x, y);

                y += sz.y + yGap;
            }
        }

        _isLoading = false;
        EditorUtility.SetDirty(scenario);
        EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);

        // Nodes were repositioned by the layout — drag attached notes along and redraw connectors.
        view?.schedule.Execute(() => ReflowAttachedNotes()).ExecuteLater(60);

        view?.FrameAll();
    }


    void OnSkipRequested(Step step, int branchIndex)
    {
        if (!Application.isPlaying) return;

        Pitech.XR.Scenario.SceneManager mgr = null;
#if UNITY_2023_1_OR_NEWER
        mgr = UnityEngine.Object
            .FindObjectsByType<Pitech.XR.Scenario.SceneManager>(FindObjectsSortMode.None)
            .FirstOrDefault(m => m && m.scenario == scenario)
            ?? UnityEngine.Object.FindObjectsByType<Pitech.XR.Scenario.SceneManager>(FindObjectsSortMode.None)
                .FirstOrDefault();
#else
        mgr = UnityEngine.Object
            .FindObjectsOfType<Pitech.XR.Scenario.SceneManager>()
            .FirstOrDefault(m => m && m.scenario == scenario)
            ?? UnityEngine.Object.FindObjectsOfType<Pitech.XR.Scenario.SceneManager>()
                .FirstOrDefault();
#endif

        if (!mgr) return;

        mgr.EditorSkipFromGraph(step.guid, branchIndex);
    }

    static void ClearReferencesToRemovedStepGuid(Scenario sc, string removedGuid)
    {
        if (sc?.steps == null || string.IsNullOrEmpty(removedGuid))
            return;

        foreach (var st in sc.steps)
        {
            if (st is TimelineStep tl && tl.nextGuid == removedGuid) tl.nextGuid = "";
            else if (st is CueCardsStep cc && cc.nextGuid == removedGuid) cc.nextGuid = "";
            else if (st is InsertStep ins && ins.nextGuid == removedGuid) ins.nextGuid = "";
            else if (st is EventStep ev && ev.nextGuid == removedGuid) ev.nextGuid = "";
            else if (st is GroupStep grp && grp.nextGuid == removedGuid) grp.nextGuid = "";
            else if (st is QuizStep qz)
            {
                if (qz.nextGuid == removedGuid) qz.nextGuid = "";
                if (qz.correctNextGuid == removedGuid) qz.correctNextGuid = "";
                if (qz.wrongNextGuid == removedGuid) qz.wrongNextGuid = "";
            }
            else if (st is QuizResultsStep qrs)
            {
                if (qrs.nextGuid == removedGuid) qrs.nextGuid = "";
                if (qrs.passedNextGuid == removedGuid) qrs.passedNextGuid = "";
                if (qrs.failedNextGuid == removedGuid) qrs.failedNextGuid = "";
            }
            else if (st is QuestionStep q && q.choices != null)
            {
                foreach (var ch in q.choices)
                    if (ch != null && ch.nextGuid == removedGuid)
                        ch.nextGuid = "";
            }
            else if (st is MiniQuizStep mq)
            {
                if (mq.defaultNextGuid == removedGuid) mq.defaultNextGuid = "";
                if (mq.outcomes != null)
                {
                    foreach (var o in mq.outcomes)
                        if (o != null && o.nextGuid == removedGuid)
                            o.nextGuid = "";
                }
            }
            else if (st is ConditionsStep cnd)
            {
                if (cnd.outcomes != null)
                {
                    foreach (var b in cnd.outcomes)
                        if (b != null && b.nextGuid == removedGuid)
                            b.nextGuid = "";
                }
            }
            else if (st is SelectionStep sel)
            {
                if (sel.correctNextGuid == removedGuid) sel.correctNextGuid = "";
                if (sel.wrongNextGuid == removedGuid) sel.wrongNextGuid = "";
            }
        }
    }

    void RemoveStepAtScenarioListIndex(int index)
    {
        if (!scenario || scenario.steps == null || index < 0 || index >= scenario.steps.Count)
            return;

        var s = scenario.steps[index];
        Undo.RecordObject(scenario, "Delete Step");
        scenario.steps.RemoveAt(index);
        ClearReferencesToRemovedStepGuid(scenario, s.guid);

        // Editor-only display overrides die with the step (incl. a group's nested steps).
        scenario.RemoveStepGraphDisplayRecursive(s);
    }

    void ScheduleGraphReloadAfterScenarioMutation()
    {
        if (!scenario)
            return;

        EditorUtility.SetDirty(scenario);
        EditorSceneManager.MarkSceneDirty(scenario.gameObject.scene);

        EditorApplication.delayCall += () =>
        {
            if (this != null && scenario != null)
                Load(scenario);
        };
    }

    /// <summary>
    /// Delete key / GraphView deleteSelection: confirm then remove data + reload (no pseudo-delete).
    /// </summary>
    void RequestDeleteSelectedStepNodes(List<StepNode> stepNodes)
    {
        if (!scenario || scenario.steps == null || stepNodes == null || stepNodes.Count == 0)
            return;

        if (stepNodes.Count == 1)
        {
            DeleteStep(stepNodes[0].step);
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Delete Steps",
                $"Delete {stepNodes.Count} selected steps?",
                "Delete",
                "Cancel"))
            return;

        foreach (var sn in stepNodes)
        {
            if (sn?.step == null)
                continue;
            int idx = scenario.steps.IndexOf(sn.step);
            if (idx < 0)
                continue;
            RemoveStepAtScenarioListIndex(idx);
        }

        ScheduleGraphReloadAfterScenarioMutation();
    }

    void DeleteStep(Step step)
    {
        if (!scenario || scenario.steps == null || step == null)
            return;

        // Find the exact object in the list (same instance we built the node from)
        int index = scenario.steps.IndexOf(step);
        if (index < 0)
        {
            Debug.LogWarning($"[ScenarioGraph] DeleteStep: step not found in list (guid={step.guid})");
            return;
        }

        var s = scenario.steps[index];

        if (!EditorUtility.DisplayDialog(
                "Delete Step",
                $"Delete “{s.Kind}” step ({index:00})?",
                "Delete",
                "Cancel"))
            return;

        RemoveStepAtScenarioListIndex(index);
        ScheduleGraphReloadAfterScenarioMutation();
    }

    void DuplicateStep(Step src)
        {
            if (!scenario || scenario.steps == null || src == null)
                return;

            int index = scenario.steps.IndexOf(src);
            if (index < 0) return;

            var type = src.GetType();
            var json = JsonUtility.ToJson(src);
            var copy = Activator.CreateInstance(type) as Step;
            if (copy == null) return;

            JsonUtility.FromJsonOverwrite(json, copy);
            // The editor-only display overrides (custom name / manual size) live in the scenario's
            // side-table keyed by guid, so they no longer travel inside the step's own JSON.
            // Map src guids -> regenerated guids pairwise (CollectStepGuids and RegenerateGuids
            // walk the same recursion order) and copy the entries across.
            var srcGuids = new List<string>();
            CollectStepGuids(copy, srcGuids);   // copy still holds src's guids here
            RegenerateGuids(copy);
            var newGuids = new List<string>();
            CollectStepGuids(copy, newGuids);
            copy.graphPos = src.graphPos + new Vector2(40f, 40f);

            Undo.RecordObject(scenario, "Duplicate Step");
            scenario.steps.Insert(Mathf.Clamp(index + 1, 0, scenario.steps.Count), copy);
            for (int gi = 0; gi < srcGuids.Count && gi < newGuids.Count; gi++)
            {
                var srcDisp = scenario.FindStepGraphDisplay(srcGuids[gi]);
                if (srcDisp == null) continue;
                var d = scenario.GetOrAddStepGraphDisplay(newGuids[gi]);
                if (d == null) continue;
                d.size = srcDisp.size;
                d.displayName = srcDisp.displayName;
            }
            Dirty(scenario, "Duplicate Step");

            EditorApplication.delayCall += () =>
            {
                if (this != null && scenario != null)
        Load(scenario);
            };
        }

        static void RegenerateGuids(Step step)
        {
            if (step == null) return;
            step.guid = Guid.NewGuid().ToString();

            if (step is GroupStep g && g.steps != null)
            {
                foreach (var st in g.steps)
                    RegenerateGuids(st);
                g.EnsureChildRequirements();
            }
    }

    // Same recursion order as RegenerateGuids - the pairwise guid mapping in DuplicateStep
    // depends on that.
    static void CollectStepGuids(Step step, List<string> into)
    {
        if (step == null || into == null) return;
        into.Add(step.guid);
        if (step is GroupStep g && g.steps != null)
            foreach (var st in g.steps)
                CollectStepGuids(st, into);
    }

    void Connect(Port src, string dstGuid)
    {
        if (src == null) return;
        if (!nodes.TryGetValue(dstGuid, out var dstNode)) return;
        if (dstNode == null) return;
        if (dstNode.inPort == null) return; // nested steps do not accept routing

        var edge = new FlowEdge
        {
            output = src,
            input = dstNode.inPort
        };

        if (edge.output == null || edge.input == null) return;
        edge.output.Connect(edge);
        edge.input.Connect(edge);

        view.AddElement(edge);
    }



    void OnEditorUpdate()
    {
        if (_suppressGraphPosWritesFrames > 0) _suppressGraphPosWritesFrames--;
        CommitPendingNoteEdits();
        SyncNoteContentsFallback();
        // If we lost the object reference across reloads, try to resolve authoring scenario.
        if (!scenario) TryResolveAuthoringScenario();

        // detect transitions
        if (Application.isPlaying && !_wasPlaying)
        {
            // just entered Play
            _wasPlaying = true;
        }
        else if (!Application.isPlaying && _wasPlaying)
        {
            // just exited Play
            _wasPlaying = false;
        }

        // --- your existing code below ---
        // Only highlight during play
        if (!Application.isPlaying)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // Find any SceneManager in the scene
#if UNITY_2023_1_OR_NEWER
        var managers = UnityEngine.Object.FindObjectsByType<Pitech.XR.Scenario.SceneManager>(FindObjectsSortMode.None);
#else
        var managers = UnityEngine.Object.FindObjectsOfType<Pitech.XR.Scenario.SceneManager>();
#endif
        if (managers == null || managers.Length == 0)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // Prefer one whose scenario matches ours, otherwise just take the first
        var mgr = managers.FirstOrDefault(m => m && m.scenario == scenario)
                  ?? managers.FirstOrDefault(m => m && m.scenario != null);

        if (!mgr || mgr.scenario == null || mgr.scenario.steps == null)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        // IMPORTANT: do NOT swap the window to runtime scenario during Play.
        // We highlight by GUID, using the authoring graph as the visual source of truth.
        var sc = mgr.scenario;

        int idx = mgr.StepIndex;
        if (idx < 0 || idx >= sc.steps.Count)
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        var curStep = sc.steps[idx];
        if (curStep == null || string.IsNullOrEmpty(curStep.guid))
        {
            UpdateNodeHighlights(null, null);
            return;
        }

        string curGuid = curStep.guid;

        // Drive the active / previous flow
        if (curGuid != _activeGuid)
        {
            _prevGuid = _activeGuid;
            _activeGuid = curGuid;
            UpdateNodeHighlights(_activeGuid, _prevGuid);
        }
    }

    static bool IsValidGraphPos(Vector2 p)
    {
        return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y));
    }

    void CommitPendingNoteEdits()
    {
        if (scenario == null || scenario.GraphNotes == null || _pendingNoteEdits.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;
        List<string> ready = null;
        foreach (var kv in _pendingNoteEdits)
        {
            if (kv.Value.dueTime <= now)
            {
                ready ??= new List<string>();
                ready.Add(kv.Key);
            }
        }

        if (ready == null) return;

        foreach (var guid in ready)
        {
            if (!_pendingNoteEdits.TryGetValue(guid, out var pending)) continue;
            _pendingNoteEdits.Remove(guid);

            var data = scenario.GraphNotes.FirstOrDefault(x => x != null && x.guid == guid);
            if (data == null) continue;

            if (data.text != pending.text)
            {
                data.text = pending.text;
                Dirty(scenario, "Edit Note");
            }
        }
    }

    // Extra safety: some note edits may not fire ValueChanged reliably in GraphView.
    // Poll the note contents and persist if changed (throttled by the debounce map above).
    void SyncNoteContentsFallback()
    {
        if (_isLoading || scenario == null || scenario.GraphNotes == null) return;
        if (notes == null || notes.Count == 0) return;

        double now = EditorApplication.timeSinceStartup;
        foreach (var kv in notes)
        {
            var guid = kv.Key;
            var note = kv.Value;
            if (note == null || string.IsNullOrEmpty(guid)) continue;

            var data = scenario.GraphNotes.FirstOrDefault(x => x != null && x.guid == guid);
            if (data == null) continue;

            // If the UI shows different text and nothing is queued, queue it.
            string uiText = note.Field?.value ?? "";
            if (data.text != uiText && !_pendingNoteEdits.ContainsKey(guid))
            {
                _pendingNoteEdits[guid] = new PendingNoteEdit
                {
                    text = uiText,
                    dueTime = now + 0.25
                };
            }
        }
    }

    void SaveViewTransform()
    {
        if (view == null) return;
        try
        {
            _savedViewPos = view.contentViewContainer.transform.position;
            _savedViewScale = view.contentViewContainer.transform.scale;
            _hasSavedView = true;
        }
        catch { }
    }

    void RestoreViewTransform()
    {
        if (!_hasSavedView || view == null) return;
        try
        {
            // GraphView provides UpdateViewTransform for correct pan/zoom restoration.
            view.UpdateViewTransform(_savedViewPos, _savedViewScale);
        }
        catch
        {
            // Fallback if UpdateViewTransform is unavailable for some reason
            try
            {
                view.contentViewContainer.transform.position = _savedViewPos;
                view.contentViewContainer.transform.scale = _savedViewScale;
            }
            catch { }
        }
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            // After Play -> Stop, Unity may restore the scene and invalidate references.
            // Re-resolve authoring scenario and reload the graph.
            TryResolveAuthoringScenario();
            if (scenario != null)
                Load(scenario);
        }
    }

    void TryResolveAuthoringScenario()
    {
        if (scenario) return;

        // 1) Try GlobalObjectId (best case)
        if (!string.IsNullOrEmpty(authoringScenarioGlobalId))
        {
            try
            {
                if (GlobalObjectId.TryParse(authoringScenarioGlobalId, out var gid))
                {
                    var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                    if (obj is Scenario sc)
                    {
                        scenario = sc;
                        return;
                    }
                }
            }
            catch { }
        }

        // 2) Fallback: search loaded scenes for matching name + scene path
        try
        {
#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<Scenario>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType<Scenario>(true);
#endif
            foreach (var sc in all)
            {
                if (!sc) continue;
                if (EditorUtility.IsPersistent(sc)) continue; // skip prefabs/assets
                if (!string.IsNullOrEmpty(authoringScenarioScenePath) && sc.gameObject.scene.path != authoringScenarioScenePath) continue;
                if (!string.IsNullOrEmpty(authoringScenarioGameObjectName) && sc.gameObject.name != authoringScenarioGameObjectName) continue;
                scenario = sc;
                return;
            }
        }
        catch { }
    }


    void UpdateNodeHighlights(string activeGuid, string prevGuid)
    {
        // Nodes
        foreach (var kv in nodes)
        {
            bool isActive = kv.Key == activeGuid;
            kv.Value.SetActiveHighlight(isActive);
        }

        // Edges
        if (view == null) return;

        var edges = view.graphElements.ToList().OfType<Edge>().ToList();
        foreach (var e in edges)
        {
            var fromNode = e.output?.node as StepNode;
            var toNode = e.input?.node as StepNode;

            if (fromNode == null || toNode == null)
                continue;

            bool isTransitionEdge =
                !string.IsNullOrEmpty(activeGuid) &&
                !string.IsNullOrEmpty(prevGuid) &&
                fromNode.step != null && toNode.step != null &&
                fromNode.step.guid == prevGuid &&
                toNode.step.guid == activeGuid;

            var ec = e.edgeControl;
            if (ec == null)
                continue;

            Color activeColor = new Color(0.3f, 0.7f, 1f); // blue-cyan

            if (isTransitionEdge)
            {
                ec.inputColor = activeColor;
                ec.outputColor = activeColor;
                ec.edgeWidth = 3;

                // kick off the moving highlight
                if (e is FlowEdge fe)
                    fe.PlayFlow();
            }
            else
            {
                ec.inputColor = _edgeDefaultColor;
                ec.outputColor = _edgeDefaultColor;
                ec.edgeWidth = _edgeDefaultWidth;
            }

            e.MarkDirtyRepaint();
        }
    }


    // ---------- context menu ----------
    void ShowCreateMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Add/Timeline", _ => CreateStep(typeof(TimelineStep)));
        evt.menu.AppendAction("Add/Cue Cards", _ => CreateStep(typeof(CueCardsStep)));
        evt.menu.AppendAction("Add/Question", _ => CreateStep(typeof(QuestionStep)));
        evt.menu.AppendAction("Add/Mini Quiz", _ => CreateStep(typeof(MiniQuizStep)));
        evt.menu.AppendAction("Add/Quiz", _ => CreateStep(typeof(QuizStep)));
        evt.menu.AppendAction("Add/Quiz Results", _ => CreateStep(typeof(QuizResultsStep)));
        evt.menu.AppendAction("Add/Selection", _ => CreateStep(typeof(SelectionStep)));
        evt.menu.AppendAction("Add/Insert", _ => CreateStep(typeof(InsertStep)));
        evt.menu.AppendAction("Add/Event", _ => CreateStep(typeof(EventStep)));
        evt.menu.AppendAction("Add/Group", _ => CreateStep(typeof(GroupStep)));
        evt.menu.AppendAction("Add/Conditions", _ => CreateStep(typeof(ConditionsStep)));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Add/Note", _ => CreateNote());
        evt.menu.AppendAction("Add/Group Box", _ => CreateGroupBox());
    }

    void CreateNote()
    {
#if UNITY_EDITOR
        if (!scenario) return;

        Dirty(scenario, "Add Note");

        var n = new Scenario.GraphNote
        {
            guid = Guid.NewGuid().ToString(),
            rect = new Rect(mouseWorld, new Vector2(260, 170)),
            text = "Note…"
        };
        scenario.GraphNotes.Add(n);
        AddOrUpdateNoteElement(n);
#endif
    }

    void CreateGroupBox()
    {
#if UNITY_EDITOR
        if (!scenario) return;

        Dirty(scenario, "Add Group Box");

        // If step nodes are selected, wrap them with padding; otherwise drop a default box at the cursor.
        Rect rect;
        const float pad = 28f;
        var selected = view.selection.OfType<StepNode>().ToList();
        if (selected.Count > 0)
        {
            var b = selected[0].GetPosition();
            float xMin = b.xMin, yMin = b.yMin, xMax = b.xMax, yMax = b.yMax;
            foreach (var sn in selected)
            {
                var r = sn.GetPosition();
                xMin = Mathf.Min(xMin, r.xMin); yMin = Mathf.Min(yMin, r.yMin);
                xMax = Mathf.Max(xMax, r.xMax); yMax = Mathf.Max(yMax, r.yMax);
            }
            rect = new Rect(xMin - pad, yMin - pad - 26f, (xMax - xMin) + pad * 2f, (yMax - yMin) + pad * 2f + 26f);
        }
        else
        {
            rect = new Rect(mouseWorld, new Vector2(340, 260));
        }

        var g = new Scenario.GraphGroup
        {
            guid = Guid.NewGuid().ToString(),
            title = "Group",
            rect = rect
        };
        scenario.GraphGroups.Add(g);
        AddOrUpdateGroupElement(g);
#endif
    }

#if UNITY_EDITOR
    void AddOrUpdateNoteElement(Scenario.GraphNote n)
    {
        if (n == null || string.IsNullOrEmpty(n.guid) || view == null) return;

        if (!notes.TryGetValue(n.guid, out var note) || note == null)
        {
            // Custom note (replaces Unity's StickyNote, whose Content field cannot be focused/edited
            // when created by script on Unity 6000.3.x/6000.4.x — see issue UUM-133754).
            note = new EditableNote();
            note.userData = n.guid;
            note.SetPosition(n.rect);
            note.Field.SetValueWithoutNotify(n.text);
            view.AddElement(note);
            notes[n.guid] = note;

            // Save note text reliably (debounced).
            note.Field.RegisterValueChangedCallback(e =>
            {
                if (_isLoading || scenario == null) return;
                if (note.userData is not string ng || string.IsNullOrEmpty(ng)) return;
                _pendingNoteEdits[ng] = new PendingNoteEdit
                {
                    text = e.newValue ?? "",
                    dueTime = EditorApplication.timeSinceStartup + 0.25
                };
            });

            // Keep rect (position + size) in sync when moved or resized.
            note.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_isLoading) return;
                var guid = note.userData as string;
                var data = scenario.GraphNotes.FirstOrDefault(x => x.guid == guid);
                if (data == null) return;
                var r = note.GetPosition();
                if (data.rect != r)
                {
                    data.rect = r;
                    // If this note is attached, keep its offset in sync when the user drags the note itself.
                    if (!string.IsNullOrEmpty(data.attachedStepGuid)
                        && nodes.TryGetValue(data.attachedStepGuid, out var anchorNode) && anchorNode != null)
                        data.attachOffset = r.position - anchorNode.GetPosition().position;
                    Dirty(scenario, "Move Note");
                }
                view?.RefreshTethers();
            });
        }
        else
        {
            note.Field.SetValueWithoutNotify(n.text);
            note.SetPosition(n.rect);
        }
    }

    void DeleteNoteByGuid(string guid)
    {
        if (!scenario || scenario.GraphNotes == null || string.IsNullOrEmpty(guid)) return;

        int idx = scenario.GraphNotes.FindIndex(x => x != null && x.guid == guid);
        if (idx < 0) return;

        Dirty(scenario, "Delete Note");
        scenario.GraphNotes.RemoveAt(idx);

        if (notes.TryGetValue(guid, out var note) && note != null)
            view.RemoveElement(note);
        notes.Remove(guid);
        view?.RefreshTethers();
    }

    // ---- Attached (anchored) notes -------------------------------------------------

    // Create a note already tethered to the given step, placed up-and-right of its node.
    public void AddAttachedNote(Step target)
    {
#if UNITY_EDITOR
        if (!scenario || target == null) return;
        Dirty(scenario, "Add Note");

        Vector2 nodePos = target.graphPos;
        if (nodes.TryGetValue(target.guid, out var nEl) && nEl != null)
            nodePos = nEl.GetPosition().position;

        var offset = new Vector2(240f, -40f);
        var n = new Scenario.GraphNote
        {
            guid = Guid.NewGuid().ToString(),
            rect = new Rect(nodePos + offset, new Vector2(220, 130)),
            text = "Note…",
            attachedStepGuid = target.guid,
            attachOffset = offset
        };
        scenario.GraphNotes.Add(n);
        AddOrUpdateNoteElement(n);
        view?.RefreshTethers();
#endif
    }

    public bool IsNoteAttached(string noteGuid)
    {
#if UNITY_EDITOR
        var n = scenario?.GraphNotes?.FirstOrDefault(x => x != null && x.guid == noteGuid);
        return n != null && !string.IsNullOrEmpty(n.attachedStepGuid);
#else
        return false;
#endif
    }

    public void DetachNote(string noteGuid)
    {
#if UNITY_EDITOR
        var n = scenario?.GraphNotes?.FirstOrDefault(x => x != null && x.guid == noteGuid);
        if (n == null || string.IsNullOrEmpty(n.attachedStepGuid)) return;
        Dirty(scenario, "Detach Note");
        n.attachedStepGuid = "";
        view?.RefreshTethers();
#endif
    }

    // Attach the note to the closest (non-nested) step node by center distance.
    public void AttachNoteToNearest(string noteGuid)
    {
#if UNITY_EDITOR
        if (scenario?.GraphNotes == null) return;
        var n = scenario.GraphNotes.FirstOrDefault(x => x != null && x.guid == noteGuid);
        if (n == null || !notes.TryGetValue(noteGuid, out var noteEl) || noteEl == null) return;

        Vector2 nc = noteEl.GetPosition().center;
        StepNode best = null; float bestD = float.MaxValue;
        foreach (var kv in nodes)
        {
            var node = kv.Value;
            if (node == null || node.IsNested || node.step == null) continue;
            float d = (node.GetPosition().center - nc).sqrMagnitude;
            if (d < bestD) { bestD = d; best = node; }
        }
        if (best == null) return;

        Dirty(scenario, "Attach Note");
        n.attachedStepGuid = best.step.guid;
        n.attachOffset = noteEl.GetPosition().position - best.GetPosition().position;
        view?.RefreshTethers();
#endif
    }

    // Move all notes attached to this node so they keep their offset from it.
    void RepositionAttachedNotes(StepNode node)
    {
#if UNITY_EDITOR
        if (node == null || node.step == null || scenario?.GraphNotes == null) return;
        Vector2 nodePos = node.GetPosition().position;
        foreach (var n in scenario.GraphNotes)
        {
            if (n == null || n.attachedStepGuid != node.step.guid) continue;
            if (!notes.TryGetValue(n.guid, out var noteEl) || noteEl == null) continue;
            // If the user is dragging the note itself, don't fight them.
            if (view != null && view.selection.Contains(noteEl)) continue;

            var r = noteEl.GetPosition();
            Vector2 newPos = nodePos + n.attachOffset;
            if ((r.position - newPos).sqrMagnitude > 0.01f)
            {
                noteEl.SetPosition(new Rect(newPos, r.size));
                n.rect = noteEl.GetPosition();
            }
        }
#endif
    }

    // Re-place every attached note relative to its node (after Load / Rearrange) and redraw tethers.
    void ReflowAttachedNotes()
    {
#if UNITY_EDITOR
        foreach (var kv in nodes)
            if (kv.Value != null && !kv.Value.IsNested)
                RepositionAttachedNotes(kv.Value);
        view?.RefreshTethers();
#endif
    }

    // Provide note→node line segments (in content coordinates) for the view's tether layer.
    public void CollectTethers(List<(Vector2 a, Vector2 b)> outSegs)
    {
#if UNITY_EDITOR
        if (outSegs == null || scenario?.GraphNotes == null) return;
        foreach (var n in scenario.GraphNotes)
        {
            if (n == null || string.IsNullOrEmpty(n.attachedStepGuid)) continue;
            if (!notes.TryGetValue(n.guid, out var noteEl) || noteEl == null) continue;
            if (!nodes.TryGetValue(n.attachedStepGuid, out var nodeEl) || nodeEl == null) continue;
            outSegs.Add((noteEl.layout.center, nodeEl.layout.center));
        }
#endif
    }


    void AddOrUpdateGroupElement(Scenario.GraphGroup g)
    {
        if (g == null || string.IsNullOrEmpty(g.guid) || view == null) return;

        if (!groups.TryGetValue(g.guid, out var box) || box == null)
        {
            box = new GroupBox();
            box.userData = g.guid;
            box.SetPosition(g.rect);
            box.Title = g.title;

            // Provide current step nodes so the box can move the ones inside it.
            box.NodesProvider = () => nodes.Values.ToList();

            box.PersistRect = () =>
            {
                if (_isLoading || scenario == null) return;
                var data = scenario.GraphGroups.FirstOrDefault(x => x != null && x.guid == g.guid);
                if (data == null) return;
                data.rect = box.GetPosition();
                Dirty(scenario, "Edit Group Box");
            };
            box.PersistTitle = t =>
            {
                if (_isLoading || scenario == null) return;
                var data = scenario.GraphGroups.FirstOrDefault(x => x != null && x.guid == g.guid);
                if (data == null) return;
                data.title = t ?? "";
                Dirty(scenario, "Rename Group Box");
            };
            box.PersistChildren = () =>
            {
                if (_isLoading || scenario == null) return;
                Dirty(scenario, "Move Group Box");
            };
            box.OnDeleteRequested = () => RequestDeleteGroupBox(g.guid);

            view.AddElement(box);
            box.SendToBack(); // keep behind nodes/edges
            groups[g.guid] = box;
        }
        else
        {
            box.Title = g.title;
            box.SetPosition(g.rect);
        }
    }

    void DeleteGroupByGuid(string guid)
    {
        if (!scenario || scenario.GraphGroups == null || string.IsNullOrEmpty(guid)) return;

        int idx = scenario.GraphGroups.FindIndex(x => x != null && x.guid == guid);
        if (idx < 0) return;

        Dirty(scenario, "Delete Group Box");
        scenario.GraphGroups.RemoveAt(idx);

        if (groups.TryGetValue(guid, out var box) && box != null)
            view.RemoveElement(box);
        groups.Remove(guid);
    }

    // Step nodes whose center currently sits inside the given rect.
    List<StepNode> StepNodesInsideRect(Rect r)
    {
        var list = new List<StepNode>();
        foreach (var n in nodes.Values)
            if (n != null && r.Contains(n.GetPosition().center))
                list.Add(n);
        return list;
    }

    // ✕ button / menu entry: ask whether to delete the box only or the box + contained steps.
    void RequestDeleteGroupBox(string guid)
    {
        if (!scenario || string.IsNullOrEmpty(guid)) return;

        List<StepNode> members = groups.TryGetValue(guid, out var box) && box != null
            ? StepNodesInsideRect(box.GetPosition())
            : new List<StepNode>();

        if (members.Count == 0)
        {
            // Nothing inside → just remove the box (no need to ask).
            DeleteGroupByGuid(guid);
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex(
            "Delete Group Box",
            $"This group contains {members.Count} step(s).\n\nDelete the group box only, or also delete the steps inside it?",
            "Group only",     // 0
            "Cancel",         // 1
            "Group + items"); // 2

        if (choice == 1) return;                       // Cancel
        if (choice == 0) { DeleteGroupByGuid(guid); return; } // Group only

        DeleteGroupAndItems(guid);                     // Group + items
    }

    // Menu entry "Delete Group + items": confirm first (this one isn't preceded by the ✕ dialog).
    void ConfirmDeleteGroupAndItems(string guid)
    {
        var members = groups.TryGetValue(guid, out var box) && box != null
            ? StepNodesInsideRect(box.GetPosition())
            : new List<StepNode>();

        if (members.Count > 0 && !EditorUtility.DisplayDialog(
                "Delete Group + Items",
                $"Delete the group box and the {members.Count} step(s) inside it?\n\nThis changes your scenario.",
                "Delete",
                "Cancel"))
            return;

        DeleteGroupAndItems(guid);
    }

    void DeleteGroupAndItems(string guid)
    {
        if (!scenario || scenario.steps == null || string.IsNullOrEmpty(guid)) return;

        var members = groups.TryGetValue(guid, out var box) && box != null
            ? StepNodesInsideRect(box.GetPosition())
            : new List<StepNode>();

        DeleteGroupByGuid(guid);

        // Remove from the end so earlier indices stay valid.
        var indices = members
            .Where(sn => sn?.step != null)
            .Select(sn => scenario.steps.IndexOf(sn.step))
            .Where(i => i >= 0)
            .Distinct()
            .OrderByDescending(i => i)
            .ToList();

        foreach (var idx in indices)
            RemoveStepAtScenarioListIndex(idx);

        ScheduleGraphReloadAfterScenarioMutation();
    }

#endif

    void CreateStep(Type t)
    {
        var inst = (Step)Activator.CreateInstance(t);
        inst.guid = Guid.NewGuid().ToString();
        inst.graphPos = mouseWorld;

        Dirty(scenario, "Add Step");
        scenario.steps.Add(inst);
        Load(scenario);
    }

}

#endif
