#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Pitech.XR.Core.Editor; // DevkitTheme / DevkitWidgets / DevkitHubWindow

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// The Lab Console window: the rich, current-lab authoring surface (DevKit surface-separation schema,
    /// section 4). It is the Timeline-for-a-lab - a VIEW over the same data as the (now thin) LabConsole
    /// inspector (the LabConsole component + its Scenario + the config), never a parallel store. It reuses
    /// the Hub's page model and the shared DevkitTheme/DevkitWidgets vocabulary; pages grey in the nav when
    /// their feature is absent but stay open so you can add it from the page.
    /// </summary>
    public sealed class LabConsoleWindow : EditorWindow
    {
        [SerializeField] Pitech.XR.Scenario.LabConsole _console;
        [SerializeField] int _current;

        SerializedObject _so;
        List<ILabConsolePage> _pages;
        VisualElement _nav;
        VisualElement _content;

        [MenuItem("Pi tech/Lab Console", false, 1)]
        public static void Open()
        {
            var w = GetWindow<LabConsoleWindow>();
            w.titleContent = new GUIContent("Lab Console");
            w.minSize = new Vector2(820, 520);
            var c = ResolveAny();
            if (c != null) w.Retarget(c);
            w.Show();
        }

        /// <summary>Open the window focused on a specific console (used by the inspector's "Open Lab Console").</summary>
        public static void Open(Pitech.XR.Scenario.LabConsole console)
        {
            var w = GetWindow<LabConsoleWindow>();
            w.titleContent = new GUIContent("Lab Console");
            w.minSize = new Vector2(820, 520);
            if (console != null) w.Retarget(console);
            w.Show();
        }

        void OnEnable()
        {
            EnsurePages();
            if (_console == null) _console = ResolveAny();
            _so = _console != null ? new SerializedObject(_console) : null;
            BuildUI();
        }

        void OnSelectionChange()
        {
            var sel = Selection.activeGameObject;
            if (sel == null) return;
            var c = sel.GetComponentInParent<Pitech.XR.Scenario.LabConsole>();
            if (c != null && c != _console) Retarget(c);
        }

        void EnsurePages()
        {
            if (_pages != null) return;
            _pages = new List<ILabConsolePage>
            {
                new OverviewPage(),
                new FlowPage(),
                new ParametersPage(),
                new ContentPage(),
                new AnalyticsPage(),
                new RolesPage(),
                new VitalsPage(),
                new DeliveryPage(),
                new RunPage(),
            };
        }

        void Retarget(Pitech.XR.Scenario.LabConsole console)
        {
            _console = console;
            _so = console != null ? new SerializedObject(console) : null;
            BuildUI();
        }

        void Refresh()
        {
            if (_console != null && _so == null) _so = new SerializedObject(_console);
            ShowPage(_current);
        }

        // ---------- chrome ----------
        void BuildUI()
        {
            EnsurePages();
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = DevkitTheme.Bg;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(row);

            // Sidebar
            var side = new VisualElement();
            side.style.width = 220;
            side.style.backgroundColor = DevkitTheme.Panel2;
            side.style.paddingLeft = side.style.paddingRight = 12;
            side.style.paddingTop = side.style.paddingBottom = 12;
            side.style.flexShrink = 0;

            var title = new Label("Lab Console") { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13 } };
            side.Add(title);
            var labName = new Label(_console != null ? _console.gameObject.name : "(no lab selected)")
            { style = { color = DevkitTheme.SubText, fontSize = 11, marginTop = 2, whiteSpace = WhiteSpace.Normal } };
            side.Add(labName);
            side.Add(DevkitTheme.VSpace(8));
            side.Add(DevkitTheme.Divider());
            side.Add(DevkitTheme.VSpace(8));

            _nav = new VisualElement();
            side.Add(_nav);

            side.Add(DevkitTheme.Flex());
            side.Add(DevkitTheme.Divider());
            side.Add(DevkitTheme.VSpace(8));
            side.Add(DevkitTheme.Secondary("Open DevKit Hub", DevkitHubWindow.Open));

            // Right: top bar + content
            var top = DevkitTheme.Row();
            top.style.backgroundColor = DevkitTheme.Panel2;
            top.style.paddingLeft = top.style.paddingRight = 12;
            top.style.paddingTop = top.style.paddingBottom = 8;
            top.Add(new Label(_console != null ? ("Lab: " + _console.gameObject.name) : "Lab Console")
            { style = { color = DevkitTheme.Text, unityFontStyleAndWeight = FontStyle.Bold } });
            top.Add(DevkitTheme.Flex());
            top.Add(DevkitTheme.Secondary("Open Scenario Graph", ScenarioGraphWindow.OpenWindow));
            top.Add(DevkitTheme.HSpace(8));
            top.Add(DevkitTheme.Secondary("Refresh", Refresh));

            _content = new ScrollView();
            _content.style.flexGrow = 1;
            _content.style.paddingLeft = _content.style.paddingRight = 12;
            _content.style.paddingTop = _content.style.paddingBottom = 8;

            var right = new VisualElement { style = { flexGrow = 1 } };
            right.Add(top);
            right.Add(_content);

            row.Add(side);
            row.Add(right);

            ShowPage(_current);
        }

        void RebuildNav()
        {
            if (_nav == null) return;
            _nav.Clear();
            var ctx = Ctx();
            for (int i = 0; i < _pages.Count; i++)
                _nav.Add(NavButton(i, ctx));
        }

        Button NavButton(int index, LabConsoleContext ctx)
        {
            var page = _pages[index];
            bool present = page.AlwaysShown || (_console != null && page.IsRelevant(ctx));

            var b = DevkitTheme.Secondary(page.Title, () => ShowPage(index));
            b.style.width = Length.Percent(100);
            b.style.marginBottom = 6;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            if (!present) b.style.color = DevkitTheme.SubText; // dim: feature not present (still clickable to add)
            if (index == _current)
            {
                b.style.borderLeftWidth = 3;
                b.style.borderLeftColor = DevkitTheme.Brand;
            }
            return b;
        }

        void ShowPage(int index)
        {
            if (_pages == null || _pages.Count == 0) return;
            _current = Mathf.Clamp(index, 0, _pages.Count - 1);
            RebuildNav();

            if (_content == null) return;
            _content.Clear();

            if (_console == null || _so == null)
            {
                BuildEmptyState(_content);
                return;
            }

            _so.Update();
            _pages[_current].BuildUI(_content, Ctx());
            _content.Bind(_so);
        }

        void BuildEmptyState(VisualElement host)
        {
            var grid = DevkitWidgets.TileGrid();
            grid.Add(DevkitWidgets.Card(
                "No Lab Console selected",
                "Select a Lab Console in the open scene, or scaffold a lab from the DevKit Hub.",
                DevkitWidgets.Actions(DevkitTheme.Secondary("Open DevKit Hub", DevkitHubWindow.Open))));
            host.Add(grid);
        }

        LabConsoleContext Ctx()
        {
            return new LabConsoleContext
            {
                Console = _console,
                SerializedObject = _so,
                Window = this,
                Refresh = Refresh,
                Navigate = NavigateTo,
            };
        }

        void NavigateTo(string pageTitle)
        {
            for (int i = 0; i < _pages.Count; i++)
                if (_pages[i].Title == pageTitle) { ShowPage(i); return; }
        }

        static Pitech.XR.Scenario.LabConsole ResolveAny()
        {
            var sel = Selection.activeGameObject;
            if (sel != null)
            {
                var c = sel.GetComponentInParent<Pitech.XR.Scenario.LabConsole>();
                if (c != null) return c;
            }
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<Pitech.XR.Scenario.LabConsole>();
#else
            return UnityEngine.Object.FindObjectOfType<Pitech.XR.Scenario.LabConsole>();
#endif
        }
    }
}
#endif
