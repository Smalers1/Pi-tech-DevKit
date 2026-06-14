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
    // Custom, always-editable note element. Replaces Unity's StickyNote because its Content field
    // cannot be focused/edited when created by script on Unity 6000.3.x/6000.4.x (issue UUM-133754).
    sealed class EditableNote : GraphElement
    {
        public readonly TextField Field;
        readonly VisualElement _resizeHandle;
        Rect _rect = new Rect(0, 0, 240, 160);

        public EditableNote()
        {
            capabilities |= Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable;

            style.position = Position.Absolute;
            style.backgroundColor = new Color(0.96f, 0.86f, 0.36f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            style.minWidth = 140;
            style.minHeight = 70;
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 4;
            style.paddingBottom = 4;

            // Title bar doubles as the drag handle (it doesn't capture clicks like the text area does).
            var titleBar = new Label("NOTE")
            {
                pickingMode = PickingMode.Ignore
            };
            titleBar.style.fontSize = 9;
            titleBar.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleBar.style.color = new Color(0f, 0f, 0f, 0.45f);
            titleBar.style.unityTextAlign = TextAnchor.UpperLeft;
            titleBar.style.marginBottom = 2;
            Add(titleBar);

            Field = new TextField { multiline = true };
            Field.style.flexGrow = 1;
            Field.style.fontSize = 11;
            Field.style.whiteSpace = WhiteSpace.Normal;
            Field.style.color = new Color(0f, 0f, 0f, 0.85f);
            var input = Field.Q(className: "unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
                input.style.color = new Color(0f, 0f, 0f, 0.9f);
            }
            Add(Field);

            // Bottom-right drag-to-resize handle.
            _resizeHandle = new VisualElement();
            _resizeHandle.style.position = Position.Absolute;
            _resizeHandle.style.right = 0;
            _resizeHandle.style.bottom = 0;
            _resizeHandle.style.width = 14;
            _resizeHandle.style.height = 14;
            _resizeHandle.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            _resizeHandle.style.borderTopLeftRadius = 4;
            Add(_resizeHandle);
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

        void SetupResize()
        {
            bool resizing = false;
            Vector2 startMouse = default;
            Vector2 startSize = default;

            _resizeHandle.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                resizing = true;
                startMouse = e.mousePosition;
                startSize = new Vector2(_rect.width, _rect.height);
                _resizeHandle.CaptureMouse();
                e.StopPropagation();
            });
            _resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!resizing) return;
                var delta = e.mousePosition - startMouse;
                float w = Mathf.Max(140f, startSize.x + delta.x);
                float h = Mathf.Max(70f, startSize.y + delta.y);
                SetPosition(new Rect(_rect.x, _rect.y, w, h));
                e.StopPropagation();
            });
            _resizeHandle.RegisterCallback<MouseUpEvent>(e =>
            {
                if (!resizing) return;
                resizing = false;
                _resizeHandle.ReleaseMouse();
                e.StopPropagation();
            });
        }
    }
}
#endif
