#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Small UI helpers layered on top of DevkitTheme.
    /// Keep this minimal and reusable for Dashboard/Tools.
    /// </summary>
    internal static class DevkitWidgets
    {
        // ---------- Status chip ----------
        public static VisualElement StatusChip(bool ok, string label)
        {
            var r = DevkitTheme.Row();
            var dot = new VisualElement
            {
                style =
                {
                    width = 10, height = 10,
                    borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5,
                    backgroundColor = ok ? new Color(0.30f,0.90f,0.50f,1) : new Color(0.95f,0.35f,0.35f,1),
                    marginRight = 6
                }
            };
            r.Add(dot);
            r.Add(new Label(label) { style = { color = DevkitTheme.Text } });
            return r;
        }

        // ---------- Tiles ----------
        public static VisualElement TileGrid()
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            return grid;
        }

        public static VisualElement Actions(params VisualElement[] buttons)
        {
            var r = DevkitTheme.WrapRow();

            foreach (var b in buttons)
            {
                if (b == null) continue;

                // Make actions wrap nicely instead of overflowing/clipping.
                b.style.marginRight = 0;
                b.style.marginBottom = 0;
                b.style.flexShrink = 0;

                // Unity 2022 UIElements doesn't support rowGap/columnGap on IStyle.
                // Use margins on children for consistent spacing.
                b.style.marginRight = 8;
                b.style.marginBottom = 8;

                if (b is Button btn)
                {
                    btn.style.whiteSpace = WhiteSpace.NoWrap;
                    btn.style.minWidth = 140;
                }

                r.Add(b);
            }

            return r;
        }

        // ---------- Card (rounded tile with soft border, actions row, optional body) ----------
        public static VisualElement Card(string title, string subtitle, VisualElement actions, VisualElement body = null)
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = DevkitTheme.Panel2,
                    // Slightly sharper than big "bubbly" rounding
                    borderTopLeftRadius = 12, borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12, borderBottomRightRadius = 12,
                    paddingLeft = 14, paddingRight = 14, paddingTop = 12, paddingBottom = 12,
                    marginRight = 10, marginBottom = 10,
                    // Faux "depth": thin outline darker than bg
                    borderBottomWidth = 1, borderTopWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderBottomColor = new Color(0.10f,0.12f,0.16f,1),
                    borderTopColor    = new Color(0.10f,0.12f,0.16f,1),
                    borderLeftColor   = new Color(0.10f,0.12f,0.16f,1),
                    borderRightColor  = new Color(0.10f,0.12f,0.16f,1),
                    // Responsive layout
                    flexBasis = Length.Percent(50),
                    flexGrow = 1,
                    minWidth = 360
                }
            };

            var head = DevkitTheme.Row();
            head.Add(new Label(title) { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold } });
            card.Add(head);

            if (!string.IsNullOrEmpty(subtitle))
            {
                card.Add(DevkitTheme.VSpace(4));
                card.Add(DevkitTheme.Body(subtitle, dim: true));
            }

            if (body != null)
            {
                card.Add(DevkitTheme.VSpace(8));
                card.Add(body);
            }

            if (actions != null)
            {
                card.Add(DevkitTheme.VSpace(10));
                card.Add(actions);
            }

            return card;
        }

        public static VisualElement CardGridTwoCol(out VisualElement left, out VisualElement right)
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexGrow = 1;

            left = new VisualElement();
            left.style.flexGrow = 1;

            right = new VisualElement();
            right.style.flexGrow = 1;
            right.style.marginLeft = 10; // gutter

            grid.Add(left);
            grid.Add(right);
            return grid;
        }

        // === Pills (chip-style) ======================================================
        public enum PillKind { Success, Warning, Error, Neutral }

        public static VisualElement Pill(string text, PillKind kind)
        {
            Color bg, fg;
            switch (kind)
            {
                case PillKind.Success: bg = new Color(0.12f, 0.32f, 0.22f, 1f); fg = new Color(0.76f, 0.95f, 0.85f, 1f); break;
                case PillKind.Warning: bg = new Color(0.32f, 0.28f, 0.10f, 1f); fg = new Color(0.95f, 0.90f, 0.70f, 1f); break;
                case PillKind.Error: bg = new Color(0.35f, 0.12f, 0.14f, 1f); fg = new Color(0.98f, 0.78f, 0.82f, 1f); break;
                default: bg = new Color(0.20f, 0.22f, 0.26f, 1f); fg = new Color(0.84f, 0.88f, 0.94f, 1f); break;
            }

            var pill = new VisualElement();
            pill.style.backgroundColor = bg;
            // Tags (NOT full pills): subtle radius reads more "pro" and less bubbly.
            const int r = 7;
            pill.style.borderTopLeftRadius = r; pill.style.borderTopRightRadius = r;
            pill.style.borderBottomLeftRadius = r; pill.style.borderBottomRightRadius = r;
            pill.style.paddingLeft = 8; pill.style.paddingRight = 8; pill.style.paddingTop = 3; pill.style.paddingBottom = 3;

            // Thin outline for contrast against dark cards
            pill.style.borderBottomWidth = 1; pill.style.borderTopWidth = 1; pill.style.borderLeftWidth = 1; pill.style.borderRightWidth = 1;
            var outline = new Color(1f, 1f, 1f, 0.06f);
            pill.style.borderBottomColor = outline;
            pill.style.borderTopColor = outline;
            pill.style.borderLeftColor = outline;
            pill.style.borderRightColor = outline;

            var label = new Label(text)
            {
                style =
                {
                    color = fg,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11
                }
            };
            pill.Add(label);
            return pill;
        }

        public static VisualElement PillsRow(params (PillKind kind, string text)[] items)
        {
            var row = DevkitTheme.Row();
            for (int i = 0; i < items.Length; i++)
            {
                row.Add(Pill(items[i].text, items[i].kind));
                if (i < items.Length - 1) row.Add(DevkitTheme.HSpace(8));
            }
            return row;
        }
    }
}
#endif
