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
    // Purely visual organizing box. Movable (drags contained nodes with it) + resizable + renamable.
    // Completely separate from GroupStep; no effect on scenario flow or runtime.
    sealed class GroupBox : GraphElement
    {
        readonly VisualElement _titleBar;
        readonly TextField _titleField;
        readonly VisualElement _resizeHandle;
        readonly UIEButton _deleteButton;
        Rect _rect = new Rect(0, 0, 340, 260);

        public Func<List<StepNode>> NodesProvider;
        public Action PersistRect;
        public Action<string> PersistTitle;
        public Action PersistChildren;
        public Action OnDeleteRequested;

        public string Title
        {
            get => _titleField.value;
            set { _titleField.SetValueWithoutNotify(value ?? ""); }
        }

        public GroupBox()
        {
            capabilities |= Capabilities.Selectable | Capabilities.Deletable;
            // Pickable so the title bar (rename/delete/move) and resize handle work. The box is drawn
            // behind the nodes, so node clicks still pass to the nodes; only clicks on the box's empty
            // interior are captured by the box.
            pickingMode = PickingMode.Position;

            style.position = Position.Absolute;
            style.backgroundColor = new Color(0.16f, 0.55f, 0.55f, 0.18f);
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            var border = new Color(0.20f, 0.66f, 0.66f, 0.65f);
            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.minWidth = 120;
            style.minHeight = 80;

            // Title bar = always-editable name field + delete button.
            _titleBar = new VisualElement();
            _titleBar.style.height = 26;
            _titleBar.style.backgroundColor = new Color(0.16f, 0.55f, 0.55f, 0.55f);
            _titleBar.style.borderTopLeftRadius = 6;
            _titleBar.style.borderTopRightRadius = 6;
            _titleBar.style.paddingLeft = 6;
            _titleBar.style.paddingRight = 4;
            _titleBar.style.flexDirection = FlexDirection.Row;
            _titleBar.style.alignItems = Align.Center;
            Add(_titleBar);

            // Click straight into this to rename (same approach as the notes, which work reliably).
            _titleField = new TextField { isDelayed = false };
            _titleField.style.flexGrow = 1;
            _titleField.style.fontSize = 12;
            _titleField.style.unityFontStyleAndWeight = FontStyle.Bold;
            var ti = _titleField.Q(className: "unity-text-input");
            if (ti != null)
            {
                ti.style.backgroundColor = Color.clear;
                ti.style.color = Color.white;
                ti.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            _titleField.RegisterValueChangedCallback(e => PersistTitle?.Invoke(e.newValue ?? ""));
            _titleBar.Add(_titleField);

            _deleteButton = new UIEButton(() => OnDeleteRequested?.Invoke()) { text = "✕" };
            _deleteButton.tooltip = "Delete group box";
            _deleteButton.style.width = 18;
            _deleteButton.style.height = 18;
            _deleteButton.style.marginLeft = 4;
            _deleteButton.style.marginRight = 0;
            _deleteButton.style.paddingLeft = 0;
            _deleteButton.style.paddingRight = 0;
            _deleteButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleBar.Add(_deleteButton);

            // Resize handle.
            _resizeHandle = new VisualElement();
            _resizeHandle.style.position = Position.Absolute;
            _resizeHandle.style.right = 0;
            _resizeHandle.style.bottom = 0;
            _resizeHandle.style.width = 16;
            _resizeHandle.style.height = 16;
            _resizeHandle.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
            _resizeHandle.style.borderTopLeftRadius = 5;
            Add(_resizeHandle);

            SetupDrag();
            SetupResize();
        }

        public override Rect GetPosition() => _rect;

        public override void SetPosition(Rect r)
        {
            _rect = r;
            style.left = r.x;
            style.top = r.y;
            style.width = r.width;
            style.height = r.height;
        }

        // Context-menu convenience: focus the (already visible) name field for editing.
        public void BeginRename()
        {
            _titleField.Focus();
            _titleField.SelectAll();
        }

        Vector2 ContentMouse(Vector2 panelMouse)
            => parent != null ? (Vector2)parent.WorldToLocal(panelMouse) : panelMouse;

        bool IsInteractiveChild(IEventHandler target)
        {
            if (!(target is VisualElement ve)) return false;
            if (ve == _resizeHandle) return true;
            // Title bar (name field + delete button) handles its own input.
            return ve == _titleBar || ve.GetFirstAncestorOfType<TextField>() == _titleField || ReferenceEquals(ve, _deleteButton);
        }

        // Move the box by dragging its body/frame (not the title field, button, or resize handle).
        void SetupDrag()
        {
            bool dragging = false;
            Vector2 startMouse = default;
            Vector2 startPos = default;
            List<(StepNode node, Vector2 start)> members = null;

            RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (IsInteractiveChild(e.target)) return;

                dragging = true;
                startMouse = ContentMouse(e.mousePosition);
                startPos = _rect.position;

                // Capture step nodes currently inside the box so they move along.
                members = new List<(StepNode, Vector2)>();
                var all = NodesProvider?.Invoke();
                if (all != null)
                {
                    foreach (var n in all)
                    {
                        if (n == null) continue;
                        var nr = n.GetPosition();
                        if (_rect.Contains(nr.center))
                            members.Add((n, nr.position));
                    }
                }

                this.CaptureMouse();
                e.StopPropagation();
            });
            RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!dragging) return;
                var delta = ContentMouse(e.mousePosition) - startMouse;
                SetPosition(new Rect(startPos + delta, new Vector2(_rect.width, _rect.height)));
                if (members != null)
                {
                    foreach (var (node, start) in members)
                    {
                        if (node == null) continue;
                        var r = node.GetPosition();
                        node.SetPositionSilent(new Rect(start + delta, r.size));
                    }
                }
                e.StopPropagation();
            });
            RegisterCallback<MouseUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                this.ReleaseMouse();
                if (members != null)
                {
                    foreach (var (node, _) in members)
                        if (node != null) node.step.graphPos = node.GetPosition().position;
                }
                PersistRect?.Invoke();
                PersistChildren?.Invoke();
                e.StopPropagation();
            });
        }

        void SetupResize()
        {
            bool resizing = false;
            Vector2 startMouse = default;
            Vector2 startSize = default;

            _resizeHandle.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                resizing = true;
                startMouse = ContentMouse(e.mousePosition);
                startSize = new Vector2(_rect.width, _rect.height);
                _resizeHandle.CaptureMouse();
                e.StopPropagation();
            });
            _resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!resizing) return;
                var delta = ContentMouse(e.mousePosition) - startMouse;
                float w = Mathf.Max(120f, startSize.x + delta.x);
                float h = Mathf.Max(80f, startSize.y + delta.y);
                SetPosition(new Rect(_rect.x, _rect.y, w, h));
                e.StopPropagation();
            });
            _resizeHandle.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!resizing) return;
                resizing = false;
                _resizeHandle.ReleaseMouse();
                PersistRect?.Invoke();
                e.StopPropagation();
            });
        }
    }
}
#endif
