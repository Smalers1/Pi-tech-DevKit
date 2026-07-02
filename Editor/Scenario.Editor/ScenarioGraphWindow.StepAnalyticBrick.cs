#if UNITY_EDITOR
using Pitech.XR.Scenario;
using Pitech.XR.Analytics;
using UnityEngine;
using UnityEngine.UIElements;

// ---------- StepNode: the StepAnalytic "lego brick" (WS B2.2, in-graph authoring) ----------
// When a step owns a StepAnalytic, a compact WHITE block sits at the TOP of the node body - like a brick on the
// step. It is an INDICATOR + actions only: an "Edit..." button opens the metrics in a dedicated window
// (StepAnalyticEditWindow), exactly like a step's own "Edit..." opens StepEditWindow. (Earlier the brick edited
// metrics inline via a dropdown; that is replaced by the window per Stergios.) White matches SessionStart/Stop.
// Right-click Add/Remove on the node also create/clear it (ScenarioGraphWindow.Analytics.cs).
public partial class ScenarioGraphWindow
{
    partial class StepNode
    {
        // Extra collapsed height reserved for the brick row so a collapsed step that owns a StepAnalytic does
        // not clip its brick. Read by GetCollapsedHeight (StepNode.cs).
        const float AnalyticBrickHeight = 30f;

        // True once this node has built its analytic brick (drives the collapsed-height reservation above).
        bool _hasAnalyticBrick;

        /// <summary>If this step owns a StepAnalytic, build the compact white brick and insert it at the top of
        /// the node body. No-op otherwise. Called from the constructor (non-nested nodes only).</summary>
        void BuildStepAnalyticBrick(Step s)
        {
            if (owner == null || s == null || !owner.StepHasAnalytic(s.guid)) return;

            var brick = new VisualElement();
            brick.style.backgroundColor = new Color(0.93f, 0.93f, 0.96f);   // white, like SessionStart/SessionStop
            brick.style.flexDirection = FlexDirection.Row;
            brick.style.alignItems = Align.Center;
            // Never let the brick absorb vertical pressure: it must keep a FIXED height so opening the step's
            // Settings (which grows the body) can't squeeze it - which previously shrank the brick and shoved the
            // colored header upward. The body grows downward instead (node height stays Auto; see StepNode.cs).
            brick.style.flexShrink = 0;
            brick.style.marginTop = 2;
            brick.style.marginBottom = 4;
            brick.style.paddingLeft = 6;
            brick.style.paddingRight = 4;
            brick.style.paddingTop = 3;
            brick.style.paddingBottom = 3;
            ApplyBrickBorder(brick);

            var titleLabel = new Label("ANALYTIC");
            titleLabel.style.color = Color.black;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 10;
            titleLabel.style.flexGrow = 1;
            brick.Add(titleLabel);

            // "Edit..." opens the metrics window (resolves the recorder live; self-heals a stale marker).
            var editBtn = new Button(() => owner?.EditStepAnalytic(step)) { text = "Edit..." };
            editBtn.tooltip = "Edit this step's analytic metrics in a window.";
            editBtn.style.height = 18;
            editBtn.style.marginRight = 4;
            brick.Add(editBtn);

            // Remove button (E): a little bigger and vertically centered.
            var removeBtn = new Button(() => owner?.RemoveStepAnalytic(step)) { text = "X" };
            removeBtn.tooltip = "Remove this Step Analytic";
            removeBtn.style.width = 22;
            removeBtn.style.height = 20;
            removeBtn.style.alignSelf = Align.Center;
            removeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            removeBtn.style.fontSize = 12;
            removeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            brick.Add(removeBtn);

            mainContainer.Insert(0, brick);
            _hasAnalyticBrick = true;
        }

        static void ApplyBrickBorder(VisualElement ve)
        {
            var c = new Color(0.55f, 0.55f, 0.60f);
            ve.style.borderTopWidth = 1; ve.style.borderBottomWidth = 1;
            ve.style.borderLeftWidth = 1; ve.style.borderRightWidth = 1;
            ve.style.borderTopColor = c; ve.style.borderBottomColor = c;
            ve.style.borderLeftColor = c; ve.style.borderRightColor = c;
            ve.style.borderTopLeftRadius = 5; ve.style.borderTopRightRadius = 5;
            ve.style.borderBottomLeftRadius = 5; ve.style.borderBottomRightRadius = 5;
        }
    }
}
#endif
