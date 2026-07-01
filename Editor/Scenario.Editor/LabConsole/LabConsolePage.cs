#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Pitech.XR.Core.Editor; // DevkitTheme / DevkitWidgets (shared DevKit visual vocabulary)

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Per-build state handed to every Lab Console page: the target console, its live <see cref="SerializedObject"/>
    /// (already bound by the window), and a <see cref="Refresh"/> callback the page calls after a STRUCTURAL change
    /// (add/create) so the nav adaptivity + the page rebuild from the new lab state.
    /// </summary>
    internal sealed class LabConsoleContext
    {
        public Pitech.XR.Scenario.LabConsole Console;
        public SerializedObject SerializedObject;
        public EditorWindow Window;
        public Action Refresh;
        public Action<string> Navigate; // jump to another page by its Title

        public void RequestRefresh() { Refresh?.Invoke(); }
        public void GoTo(string pageTitle) { Navigate?.Invoke(pageTitle); }
    }

    /// <summary>
    /// A Lab Console page. Mirrors the Hub's IDevkitPage model (a registry of pages, each building into a
    /// VisualElement root) but adds a per-build <see cref="LabConsoleContext"/> and feature adaptivity:
    /// <see cref="IsRelevant"/> drives nav greying, <see cref="AlwaysShown"/> marks the core pages.
    /// </summary>
    internal interface ILabConsolePage
    {
        string Title { get; }
        bool AlwaysShown { get; }
        bool IsRelevant(LabConsoleContext ctx);
        void BuildUI(VisualElement root, LabConsoleContext ctx);
    }

    /// <summary>Shared chrome helpers so every page speaks the same subtle DevKit visual language.</summary>
    internal abstract class LabConsolePageBase : ILabConsolePage
    {
        public abstract string Title { get; }
        public virtual bool AlwaysShown => false;
        public virtual bool IsRelevant(LabConsoleContext ctx) => true;
        public abstract void BuildUI(VisualElement root, LabConsoleContext ctx);

        // ---------- layout primitives ----------
        protected static VisualElement Section(string title) => DevkitTheme.Section(title);
        protected static Label Body(string text, bool dim = false) => DevkitTheme.Body(text, dim);
        protected static VisualElement VSpace(float h) => DevkitTheme.VSpace(h);
        protected static VisualElement Row() => DevkitTheme.Row();

        /// <summary>A bound editor field for a serialized member of the console. Returns a help line if the
        /// property is missing (e.g. scripts not recompiled), never null, so the page layout is robust.</summary>
        protected static VisualElement BoundField(LabConsoleContext ctx, string propertyPath, string label = null)
        {
            var p = ctx.SerializedObject != null ? ctx.SerializedObject.FindProperty(propertyPath) : null;
            if (p == null)
                return Body("(" + propertyPath + " not found - recompile scripts)", dim: true);
            return label != null ? new PropertyField(p, label) : new PropertyField(p);
        }

        /// <summary>A tiny dim caption above a field (matches the inspector's MiniCaption tone).</summary>
        protected static Label Caption(string text)
        {
            var l = new Label(text);
            l.style.color = DevkitTheme.SubText;
            l.style.fontSize = 11;
            l.style.marginTop = 4;
            l.style.marginBottom = 2;
            return l;
        }

        /// <summary>A soft full-width info panel (themed help box).</summary>
        protected static VisualElement Note(string text)
        {
            var box = new VisualElement();
            box.style.backgroundColor = DevkitTheme.Panel2;
            box.style.paddingLeft = box.style.paddingRight = 12;
            box.style.paddingTop = box.style.paddingBottom = 10;
            Round(box, 8);
            box.Add(Body(text, dim: true));
            return box;
        }

        /// <summary>A dim card flagged as not-yet-built work, so the page is HONEST about partial features
        /// (DevKit schema section 6.3: mark capabilities B2-hardening / post-B2, not "wire existing code").</summary>
        protected static VisualElement PostB2Card(string title, string body)
        {
            var card = new VisualElement();
            card.style.backgroundColor = DevkitTheme.Panel2;
            card.style.paddingLeft = card.style.paddingRight = 12;
            card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.marginTop = 8;
            Round(card, 8);

            var head = Row();
            head.Add(DevkitWidgets.Pill("POST-B2", DevkitWidgets.PillKind.Warning));
            head.Add(DevkitTheme.HSpace(8));
            var t = new Label(title) { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12 } };
            head.Add(t);
            card.Add(head);
            card.Add(VSpace(6));
            card.Add(Body(body, dim: true));
            return card;
        }

        /// <summary>The lead card on a page whose feature is not present: one sentence + a single Add button.</summary>
        protected static VisualElement AddFeatureCard(string title, string description, string addLabel, Action onAdd)
        {
            var actions = DevkitWidgets.Actions(DevkitTheme.Primary(addLabel, onAdd));
            return DevkitWidgets.Card(title, description, actions);
        }

        /// <summary>A read-only "resolved object" link row: a disabled object field + Select / Ping.</summary>
        protected static VisualElement ObjectLinkRow(string caption, UnityEngine.Object obj)
        {
            var wrap = new VisualElement();
            wrap.Add(Caption(caption));

            var field = new ObjectField { objectType = typeof(UnityEngine.Object), value = obj };
            field.SetEnabled(false);
            wrap.Add(field);

            GameObject go = obj is Component c ? c.gameObject : obj as GameObject;
            var actions = DevkitWidgets.Actions(
                DevkitTheme.Secondary("Select", () => { if (go != null) { Selection.activeObject = go; EditorGUIUtility.PingObject(go); } }),
                DevkitTheme.Secondary("Ping", () => { if (obj != null) EditorGUIUtility.PingObject(obj); }));
            wrap.Add(actions);
            return wrap;
        }

        /// <summary>Open the Scenario Graph (the per-step flow editor) for the current lab.</summary>
        protected static Button OpenGraphButton()
            => DevkitTheme.Secondary("Open Scenario Graph", ScenarioGraphWindow.OpenWindow);

        protected static void Round(VisualElement ve, int r)
        {
            ve.style.borderTopLeftRadius = r; ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r; ve.style.borderBottomRightRadius = r;
        }
    }
}
#endif
