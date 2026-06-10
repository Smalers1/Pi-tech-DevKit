#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    public sealed class DevkitHubWindow : EditorWindow
    {
        static DevkitHubWindow _instance;

        enum PageKind { Setup, Author, Localization, Deliver, Maintain, Reference }

        readonly Dictionary<PageKind, IDevkitPage> _pages = new()
        {
            { PageKind.Setup,        new SetupPage() },
            { PageKind.Author,       new AuthorPage() },
            { PageKind.Localization, new LocalizationPage() },
            { PageKind.Deliver,      new DeliverPage() },
            { PageKind.Maintain,     new MaintainPage() },
            { PageKind.Reference,    new ReferencePage() },
        };

        VisualElement _content;
        PageKind _current = PageKind.Setup;

        // Pi tech menu layout (priority => sort order; a gap >= 11 draws a separator line):
        //   group 1 (top): DevKit (0)
        //   group 2:       Tools/* (20-21)              -- separator above
        //   group 3:       the rest, alphabetical (40+) -- separator above
        // WS A3's "Pi tech/Tools/Evaluate Changes" should slot at ~22 to stay in group 2.
        [MenuItem("Pi tech/DevKit", false, 0)]
        public static void Open()
        {
            var w = GetWindow<DevkitHubWindow>();
            w.titleContent = new GUIContent("DevKit Hub", DevkitContext.TitleIcon);
            w.minSize = new Vector2(860, 520);
            w.Show();
        }

        public static void TryRefresh()
        {
            if (_instance == null) return;
            _instance.RefreshCurrentPage();
        }

        void OnEnable()
        {
            _instance = this;
            BuildUI();
        }

        void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(row);

            // Sidebar
            var side = new VisualElement();
            side.style.width = 220;
            side.style.backgroundColor = DevkitTheme.Panel2;
            side.style.paddingLeft = 12;
            side.style.paddingRight = 12;
            side.style.paddingTop = 12;
            side.style.paddingBottom = 12;
            side.style.flexShrink = 0;

            // Logo + title
            var logoRow = DevkitTheme.Row();
            if (DevkitContext.SidebarLogo != null)
            {
                var logo = new Image { image = DevkitContext.SidebarLogo };
                logo.style.width = 90; logo.style.height = 60; logo.style.marginRight = 8;
                logoRow.Add(logo);
            }
            side.Add(logoRow);
            side.Add(DevkitTheme.VSpace(8));
            side.Add(DevkitTheme.Divider());
            side.Add(DevkitTheme.VSpace(8));

            // Nav buttons (task-first cockpit pages)
            side.Add(NavButton("Setup", PageKind.Setup));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Author", PageKind.Author));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Localization", PageKind.Localization));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Deliver", PageKind.Deliver));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Maintain", PageKind.Maintain));
            side.Add(DevkitTheme.VSpace(6));
            side.Add(NavButton("Reference", PageKind.Reference));

            // Top bar
            var top = DevkitTheme.Row();
            top.style.paddingLeft = 12; top.style.paddingRight = 12;
            top.style.paddingTop = 8; top.style.paddingBottom = 8;
            var hdr = new Label($"DevKit Hub {DevkitContext.Version}")
            {
                style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold }
            };
            top.Add(hdr);
            top.Add(DevkitTheme.Flex());
            top.style.backgroundColor = DevkitTheme.Panel2;

            // Content
            _content = new ScrollView();
            _content.style.flexGrow = 1;
            _content.style.paddingLeft = 12; _content.style.paddingRight = 12; _content.style.paddingTop = 8; _content.style.paddingBottom = 8;

            var right = new VisualElement { style = { flexGrow = 1 } };
            right.Add(top);
            right.Add(_content);

            row.Add(side);
            row.Add(right);

            ShowPage(_current);
        }

        Button NavButton(string text, PageKind page)
        {
            var b = DevkitTheme.Secondary(text, () => ShowPage(page));
            b.style.width = Length.Percent(100);
            return b;
        }

        void ShowPage(PageKind page)
        {
            _current = page;
            _content.Clear();
            if (_pages.TryGetValue(page, out var p))
                p.BuildUI(_content); // uses your IDevkitPage contract :contentReference[oaicite:13]{index=13}
        }

        void RefreshCurrentPage() => ShowPage(_current);
    }
}
#endif
