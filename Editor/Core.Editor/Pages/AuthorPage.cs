#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: AUTHOR - scenario content authoring. Workspace launchers (Scenario Graph,
    // Dev Blocks), the verb-named "Add Scenario to Scene" command, the scene-wiring wizard moved
    // verbatim from the former GuidedSetupPage (minus the Addressables card, which re-homes to
    // Deliver), and the reserved Localization/Vitals module tiles (WS A2 Step 4/5/7).
    public sealed class AuthorPage : IDevkitPage
    {
        public string Title => "Author";

        // Runtime type names (reflection so Core.Editor stays decoupled) - verbatim from GuidedSetupPage.
        const string TSceneManager = "Pitech.XR.Scenario.SceneManager";
        const string TStatsUI = "Pitech.XR.Stats.StatsUIController";
        const string TStatsConfig = "Pitech.XR.Stats.StatsConfig";
        const string TSelectables = "Pitech.XR.Interactables.SelectablesManager";
        const string TSelectionLists = "Pitech.XR.Interactables.SelectionLists";
        const string TQuizUI = "Pitech.XR.Quiz.QuizUIController";
        const string TQuizResultsUI = "Pitech.XR.Quiz.QuizResultsUIController";

        public void BuildUI(VisualElement root)
        {
            var scen = new ScenarioService();

            // ===== Workspaces (launch tiles - the Hub launches windows, never re-implements) =====
            {
                var section = DevkitTheme.Section("Workspaces");
                section.Add(DevkitTheme.Body("Authoring workspaces. The Hub opens them; it never re-implements them.", dim: true));
                section.Add(DevkitTheme.VSpace(8));
                var grid = DevkitWidgets.TileGrid();
                grid.Add(DevkitWidgets.Card(
                    "Scenario Graph",
                    "Node-based authoring with persisted GUID routing.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Open Scenario Graph", scen.OpenGraph))));
                grid.Add(DevkitWidgets.Card(
                    "Dev Blocks",
                    "Reusable prefab library for fast scene building.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Open Dev Blocks", DevBlocksWindow.Open))));
                section.Add(grid);
                root.Add(section);
            }

            // ===== Commands =====
            {
                var section = DevkitTheme.Section("Commands");
                var grid = DevkitWidgets.TileGrid();
                grid.Add(DevkitWidgets.Card(
                    "Add Scenario to Scene",
                    "Create a Scenario GameObject under the '--- SCENE MANAGERS ---' root and select it.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Add Scenario to Scene", scen.AddScenarioToScene))));
                section.Add(grid);
                root.Add(section);
            }

            // ===== Scene wiring wizard (verbatim from GuidedSetupPage; scene-guarded) =====
            {
                var svc = new GuidedSetupService();
                var section = DevkitTheme.Section("Scene Wiring");
                section.Add(DevkitTheme.Body("A scene-agnostic setup wizard to get you productive fast. Everything is optional and safe to skip.", dim: true));
                section.Add(DevkitTheme.VSpace(10));

                if (!svc.HasActiveSceneLoaded())
                {
                    section.Add(DevkitWidgets.Card(
                        "Open a scene",
                        "Scene wiring needs an active scene. Open `Assets/Scenes/Testing` (or any scene) and come back.",
                        DevkitWidgets.Actions(
                            DevkitTheme.Primary("Open Testing scene", () =>
                            {
                                var path = "Assets/Scenes/Testing.unity";
                                if (System.IO.File.Exists(path))
                                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
                                else
                                    EditorUtility.DisplayDialog("DevKit", "Could not find Assets/Scenes/Testing.unity in this project.", "OK");
                            })
                        )
                    ));
                    root.Add(section);
                }
                else
                {
                    var grid = DevkitWidgets.TileGrid();

                    // Core wiring
                    grid.Add(CardManagersRoot(svc));
                    grid.Add(CardSceneManager(svc));

                    // Optional modules
                    grid.Add(CardStats(svc));
                    grid.Add(CardInteractables(svc));
                    grid.Add(CardQuiz(svc));

                    section.Add(grid);
                    root.Add(section);
                }
            }

            // ===== Reserved modules: Localization, Vitals (Step 7 -> Author tiles) =====
            {
                var section = DevkitTheme.Section("Reserved modules");
                var grid = DevkitWidgets.TileGrid();
                grid.Add(ReservedTile("Localization", "Keyed Greek + English (build-baked). Reserved slot - logic lands Phase B WS B7 (spec §28.3)."));
                grid.Add(ReservedTile("Vitals", "Typed vitals foundation. Reserved slot - logic lands Phase B WS B8 (spec §28.4)."));
                section.Add(grid);
                root.Add(section);
            }
        }

        static VisualElement CardManagersRoot(GuidedSetupService svc)
        {
            var parent = svc.EnsureManagersRoot();
            bool ok = parent != null;

            var pills = DevkitWidgets.PillsRow(
                (ok ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Warning, ok ? "Ready" : "Missing"),
                (DevkitWidgets.PillKind.Neutral, "--- SCENE MANAGERS ---")
            );

            return DevkitWidgets.Card(
                "Managers Root",
                "Recommended place to keep scene-level managers tidy.",
                DevkitWidgets.Actions(
                    DevkitTheme.Primary(ok ? "Ping" : "Create", () =>
                    {
                        var p = svc.EnsureManagersRoot();
                        if (p) EditorGUIUtility.PingObject(p.gameObject);
                    })
                ),
                pills
            );
        }

        static VisualElement CardSceneManager(GuidedSetupService svc)
        {
            var sm = svc.FindFirstInScene(TSceneManager) as Component;
            bool ok = sm != null;

            var pills = DevkitWidgets.PillsRow(
                (ok ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Warning, ok ? "Ready" : "Missing"),
                (DevkitWidgets.PillKind.Neutral, "SceneManager")
            );

            return DevkitWidgets.Card(
                "Scene Manager",
                "Orchestrates Scenario and optional modules (Stats / Interactables).",
                DevkitWidgets.Actions(
                    DevkitTheme.Primary(ok ? "Ping" : "Create", () =>
                    {
                        if (!ok)
                            sm = svc.CreateUnderManagersRoot(TSceneManager, "Scene Manager", "Create Scene Manager");
                        if (sm) EditorGUIUtility.PingObject(sm.gameObject);
                    })
                ),
                pills
            );
        }

        static VisualElement CardStats(GuidedSetupService svc)
        {
            var sm = svc.FindFirstInScene(TSceneManager) as Component;
            var ui = svc.FindFirstInScene(TStatsUI) as Component;

            bool hasManager = sm != null;
            bool hasUI = ui != null;

            // Asset can live anywhere, so we don't "find in scene" for config; just let user pick/create.
            var cfgType = GuidedSetupService.FindType(TStatsConfig) ?? typeof(ScriptableObject);

            var pills = DevkitWidgets.PillsRow(
                (hasUI ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral, hasUI ? "UI present" : "UI optional"),
                (DevkitWidgets.PillKind.Neutral, "Stats optional")
            );

            var body = new VisualElement();
            body.Add(pills);
            body.Add(DevkitTheme.VSpace(8));

            var cfgField = new ObjectField("Stats Config") { objectType = cfgType, allowSceneObjects = false };
            body.Add(cfgField);

            return DevkitWidgets.Card(
                "Stats (optional)",
                "Create StatsUIController and optionally assign a StatsConfig asset to the Scene Manager.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary(hasUI ? "Ping StatsUIController" : "Create StatsUIController", () =>
                    {
                        if (!ui)
                            ui = svc.CreateUnderManagersRoot(TStatsUI, "StatsUIController", "Create StatsUIController");
                        if (ui) EditorGUIUtility.PingObject(ui.gameObject);
                    }),
                    DevkitTheme.Secondary("Create StatsConfig asset", () =>
                    {
                        new StatsService().CreateConfig();
                    }),
                    DevkitTheme.Primary("Assign to Scene Manager", () =>
                    {
                        sm = svc.FindFirstInScene(TSceneManager) as Component;
                        if (!sm)
                        {
                            EditorUtility.DisplayDialog("DevKit", "Scene Manager not found in this scene.", "OK");
                            return;
                        }

                        if (ui)
                            svc.AssignObjectProperty(sm, "statsUI", ui, "Assign Stats UI");

                        if (cfgField.value)
                            svc.AssignObjectProperty(sm, "statsConfig", cfgField.value, "Assign Stats Config");

                        EditorGUIUtility.PingObject(sm);
                    })
                ),
                body
            );
        }

        static VisualElement CardInteractables(GuidedSetupService svc)
        {
            var sm = svc.FindFirstInScene(TSceneManager) as Component;
            var selMgr = svc.FindFirstInScene(TSelectables) as Component;
            var lists = svc.FindFirstInScene(TSelectionLists) as Component;

            bool hasAny = selMgr || lists;

            var pills = DevkitWidgets.PillsRow(
                (hasAny ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral, hasAny ? "Present" : "Optional"),
                (DevkitWidgets.PillKind.Neutral, "Selection Lists")
            );

            var body = new VisualElement();
            body.Add(pills);
            body.Add(DevkitTheme.VSpace(8));
            body.Add(DevkitTheme.Body("These are used by Selection steps. You can add them only if your scenario needs them.", dim: true));

            return DevkitWidgets.Card(
                "Interactables (optional)",
                "Create SelectablesManager + SelectionLists and assign them to the Scene Manager.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary(selMgr ? "Ping SelectablesManager" : "Create SelectablesManager", () =>
                    {
                        if (!selMgr)
                            selMgr = svc.CreateUnderManagersRoot(TSelectables, "Selectables Manager", "Create Selectables Manager");
                        if (selMgr) EditorGUIUtility.PingObject(selMgr.gameObject);
                    }),
                    DevkitTheme.Secondary(lists ? "Ping SelectionLists" : "Create SelectionLists", () =>
                    {
                        if (!lists)
                            lists = svc.CreateUnderManagersRoot(TSelectionLists, "Selection Lists", "Create Selection Lists");
                        if (lists) EditorGUIUtility.PingObject(lists.gameObject);
                    }),
                    DevkitTheme.Primary("Assign to Scene Manager", () =>
                    {
                        sm = svc.FindFirstInScene(TSceneManager) as Component;
                        if (!sm)
                        {
                            EditorUtility.DisplayDialog("DevKit", "Scene Manager not found in this scene.", "OK");
                            return;
                        }

                        if (selMgr)
                            svc.AssignObjectProperty(sm, "selectables", selMgr, "Assign Selectables Manager");

                        if (lists)
                            svc.AssignObjectProperty(sm, "selectionLists", lists, "Assign Selection Lists");

                        EditorGUIUtility.PingObject(sm);
                    })
                ),
                body
            );
        }

        static VisualElement CardQuiz(GuidedSetupService svc)
        {
            var sm = svc.FindFirstInScene(TSceneManager) as Component;
            var ui = svc.FindFirstInScene(TQuizUI) as Component;
            var uiRes = svc.FindFirstInScene(TQuizResultsUI) as Component;

            bool hasUI = ui != null && uiRes != null;

            var pills = DevkitWidgets.PillsRow(
                (hasUI ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral, hasUI ? "UI present" : "UI optional"),
                (DevkitWidgets.PillKind.Neutral, "Quiz optional")
            );

            var body = new VisualElement();
            body.Add(pills);
            body.Add(DevkitTheme.VSpace(8));
            body.Add(DevkitTheme.Body("Note: Quiz assets are project data. Each scene can choose its own quiz(s), so this page does not assign a QuizAsset automatically.", dim: true));

            return DevkitWidgets.Card(
                "Quiz (optional)",
                "Installs the default Quiz UI prefabs (TMP) into the current scene and wires the UI panels to the Scene Manager.",
                DevkitWidgets.Actions(
                    DevkitTheme.Secondary("Install Quiz UI + Wire Panels", () =>
                    {
                        new QuizService().AddQuizToScene();
                        DevkitHubWindow.TryRefresh();
                    }),
                    DevkitTheme.Secondary("Create QuizAsset", () =>
                    {
                        new QuizService().CreateAsset();
                        DevkitHubWindow.TryRefresh();
                    }),
                    DevkitTheme.Primary("Assign Panels to Scene Manager", () =>
                    {
                        sm = svc.FindFirstInScene(TSceneManager) as Component;
                        if (!sm)
                        {
                            EditorUtility.DisplayDialog("DevKit", "Scene Manager not found in this scene.", "OK");
                            return;
                        }

                        if (ui)
                            svc.AssignObjectProperty(sm, "quizPanel", ui, "Assign Quiz Panel");

                        if (uiRes)
                            svc.AssignObjectProperty(sm, "quizResultsPanel", uiRes, "Assign Quiz Results Panel");

                        EditorGUIUtility.PingObject(sm);
                        DevkitHubWindow.TryRefresh();
                    })
                ),
                body
            );
        }

        // Reserved-module tile: announces a future module. No action, no behaviour (Phase A
        // reserves the slot only). Body carries a Neutral "Reserved" pill.
        static VisualElement ReservedTile(string title, string subtitle) =>
            DevkitWidgets.Card(
                title,
                subtitle,
                DevkitWidgets.Actions(),
                DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Reserved")));
    }
}
#endif
