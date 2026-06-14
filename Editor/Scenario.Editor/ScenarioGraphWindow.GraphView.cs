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
    // ================= GraphView =================
    class ScenarioGraphView : GraphView
    {
        readonly ScenarioGraphWindow _graphWindow;
        VisualElement _tetherLayer;
        readonly List<(Vector2 a, Vector2 b)> _tetherSegs = new();

        public Action<ContextualMenuPopulateEvent> OnContextAdd;
        public Action<Vector2> OnMouseWorld;
        public Action OnEdgeDropped;
        public Action OnMouseUp;
        public Action OnMouseDown;
        public Action OnViewTransformChanged;

        public ScenarioGraphView(ScenarioGraphWindow graphWindow)
        {
            _graphWindow = graphWindow;

            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground() { name = "grid" });
            this.Q("grid")?.StretchToParentSize();

            // Overlay (in content space, behind nodes) that draws connector lines from attached notes to their nodes.
            _tetherLayer = new VisualElement { name = "note-tethers", pickingMode = PickingMode.Ignore };
            _tetherLayer.style.position = Position.Absolute;
            _tetherLayer.style.left = 0;
            _tetherLayer.style.top = 0;
            _tetherLayer.style.right = 0;
            _tetherLayer.style.bottom = 0;
            _tetherLayer.style.overflow = Overflow.Visible;
            _tetherLayer.generateVisualContent = OnGenerateTethers;
            contentViewContainer.Add(_tetherLayer);
            _tetherLayer.SendToBack();
            RegisterCallback<MouseMoveEvent>(e =>
            {
                var p = contentViewContainer.WorldToLocal(e.mousePosition);
                OnMouseWorld?.Invoke(p);
            });

            // Important: use TrickleDown so we still get the event even if GraphView handles it.
            RegisterCallback<MouseDownEvent>(_ => OnMouseDown?.Invoke(), TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(_ => OnMouseUp?.Invoke(), TrickleDown.TrickleDown);

            // Fire when user pans/zooms so we can persist view transform.
            RegisterCallback<WheelEvent>(_ => OnViewTransformChanged?.Invoke(), TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(_ => OnViewTransformChanged?.Invoke(), TrickleDown.TrickleDown);

            // Keyboard Delete / Edit → Delete must remove scenario data + confirm — not GraphView-only removal.
            deleteSelection = HandleGraphDeleteSelection;
        }

        // Recompute the attached-note connector segments and repaint the overlay.
        public void RefreshTethers()
        {
            _tetherSegs.Clear();
            _graphWindow?.CollectTethers(_tetherSegs);
            _tetherLayer?.MarkDirtyRepaint();
        }

        void OnGenerateTethers(MeshGenerationContext ctx)
        {
            if (_tetherSegs.Count == 0) return;
            var color = new Color(0.85f, 0.70f, 0.20f, 0.70f); // amber, matches the note color
            const float half = 1.5f;
            foreach (var (a, b) in _tetherSegs)
            {
                Vector2 dir = b - a;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector2 nrm = new Vector2(-dir.y, dir.x) * half;

                var mesh = ctx.Allocate(4, 6);
                mesh.SetNextVertex(new Vertex { position = a + nrm, tint = color });
                mesh.SetNextVertex(new Vertex { position = a - nrm, tint = color });
                mesh.SetNextVertex(new Vertex { position = b - nrm, tint = color });
                mesh.SetNextVertex(new Vertex { position = b + nrm, tint = color });
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
                mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
            }
        }

        void HandleGraphDeleteSelection(string operationName, AskUser askUser)
        {
            var stepNodes = selection.OfType<StepNode>().Where(n => n != null).ToList();
            if (stepNodes.Count > 0)
            {
                _graphWindow?.RequestDeleteSelectedStepNodes(stepNodes);
                return;
            }

#if UNITY_EDITOR
            var groupEls = selection.OfType<GroupBox>().Where(g => g != null).ToList();
            if (groupEls.Count > 0)
            {
                foreach (var ge in groupEls)
                    if (ge.userData is string gg) _graphWindow?.DeleteGroupByGuid(gg);
                return;
            }
#endif

            DeleteSelectionOperation(operationName, askUser);
        }

        public void ClearGraph()
        {
            foreach (var ge in graphElements.ToList())
                RemoveElement(ge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var results = new List<Port>();
            ports.ForEach(p =>
            {
                if (p == startPort) return;
                if (p.node == startPort.node) return;
                if (p.direction == startPort.direction) return;
                if (p.portType != startPort.portType) return;
                results.Add(p);
            });
            return results;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Kill ALL default items
            evt.menu.ClearItems();

            // Right-click on a node → let StepNode decide
            if (evt.target is StepNode node)
            {
                node.BuildContextualMenu(evt);
                return;
            }

            // Right-click on a note (or inside its text area) → allow delete
#if UNITY_EDITOR
            var noteEl = (evt.target as VisualElement)?.GetFirstAncestorOfType<EditableNote>();
            if (noteEl != null && noteEl.userData is string guid)
            {
                bool attached = _graphWindow != null && _graphWindow.IsNoteAttached(guid);
                if (attached)
                    evt.menu.AppendAction("Detach Note", _ => _graphWindow?.DetachNote(guid));
                else
                    evt.menu.AppendAction("Attach Note to Nearest Node", _ => _graphWindow?.AttachNoteToNearest(guid));

                evt.menu.AppendAction("Delete Note", _ =>
                {
                    var win = EditorWindow.focusedWindow as ScenarioGraphWindow;
                    win?.DeleteNoteByGuid(guid);
                });
                return;
            }

            // Right-click on a visual group box → rename / delete
            var groupEl = evt.target as GroupBox ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<GroupBox>();
            if (groupEl != null && groupEl.userData is string gguid)
            {
                evt.menu.AppendAction("Rename Group", _ => groupEl.BeginRename());
                evt.menu.AppendAction("Delete Group (keep items)", _ => _graphWindow?.DeleteGroupByGuid(gguid));
                evt.menu.AppendAction("Delete Group + items", _ => _graphWindow?.ConfirmDeleteGroupAndItems(gguid));
                return;
            }
#endif

            // Right-click on empty space → creation menu
            if (!(evt.target is GraphElement))
            {
                OnContextAdd?.Invoke(evt);
                return;
            }

            // Ports / edges: nothing special for now
        }
    }
}
#endif
