#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Pitech.XR.Core.Editor; // DevkitTheme / DevkitWidgets

namespace Pitech.XR.Scenario.Editor
{
    // =====================================================================================================
    // The Lab Console pages (DevKit surface-separation schema, section 4). One per-lab authoring concern.
    // Core pages (Overview / Flow / Parameters / Run) are always shown; feature pages grey in the nav when
    // their feature is absent but stay open so you can add it. Content Delivery is a SIGNPOST for now -
    // it stays authored in the inspector (Stergios, 2026-06-30) until its relocation is scheduled.
    // =====================================================================================================

    // ---------- Overview ----------
    internal sealed class OverviewPage : LabConsolePageBase
    {
        public override string Title => "Overview";
        public override bool AlwaysShown => true;

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            var health = Section("Lab health");
            health.Add(Body("What this lab has wired. Grey pages in the sidebar are features you can still add.", dim: true));
            health.Add(VSpace(8));

            var chips = DevkitTheme.WrapRow();
            AddChip(chips, "Flow", LabConsoleAuthoring.HasScenario(c));
            AddChip(chips, "Parameters", LabConsoleAuthoring.HasParameters(c));
            AddChip(chips, "Content", LabConsoleAuthoring.HasContent(c));
            AddChip(chips, "Analytics", LabConsoleAuthoring.HasAnalytics(c));
            AddChip(chips, "Roles", LabConsoleAuthoring.HasRoles(c));
            AddChip(chips, "Vitals", LabConsoleAuthoring.HasVitals(c));
            AddChip(chips, "Delivery", LabConsoleAuthoring.HasDelivery(c));
            health.Add(chips);
            root.Add(health);

            // Setup / fix list
            var setup = Section("Setup");
            bool anyIssue = false;
            if (!LabConsoleAuthoring.HasScenario(c))
            {
                anyIssue = true;
                setup.Add(Body("- No Scenario assigned. A lab needs a Scenario to run a step flow."));
                setup.Add(VSpace(4));
                setup.Add(DevkitWidgets.Actions(DevkitTheme.Primary("Go to Flow", () => ctx.GoTo("Flow"))));
            }
            else
            {
                int steps = (c != null && c.scenario != null && c.scenario.steps != null) ? c.scenario.steps.Count : 0;
                setup.Add(Body("Scenario assigned with " + steps + " step(s).", dim: true));
                if (steps == 0)
                {
                    anyIssue = true;
                    setup.Add(VSpace(4));
                    setup.Add(Body("- The scenario has no steps yet. Open the graph to author the flow."));
                }
            }
            if (!anyIssue) { setup.Add(VSpace(4)); setup.Add(Body("No blocking setup issues found.", dim: true)); }
            root.Add(setup);

            // Entry points
            var nav = Section("Open");
            nav.Add(DevkitWidgets.Actions(
                OpenGraphButton(),
                DevkitTheme.Secondary("Open DevKit Hub", DevkitHubWindow.Open)));
            root.Add(nav);
        }

        static void AddChip(VisualElement row, string label, bool ok)
        {
            var chip = DevkitWidgets.StatusChip(ok, label);
            chip.style.marginRight = 14;
            chip.style.marginBottom = 6;
            row.Add(chip);
        }
    }

    // ---------- Flow ----------
    internal sealed class FlowPage : LabConsolePageBase
    {
        public override string Title => "Flow";
        public override bool AlwaysShown => true;

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            var sc = Section("Scenario");
            sc.Add(Caption("Scenario asset"));
            sc.Add(BoundField(ctx, "scenario"));
            if (!LabConsoleAuthoring.HasScenario(c))
            {
                sc.Add(VSpace(4));
                sc.Add(DevkitWidgets.Actions(DevkitTheme.Primary("Create & assign Scenario",
                    () => { LabConsoleAuthoring.CreateScenario(c); ctx.RequestRefresh(); })));
            }
            sc.Add(VSpace(8));
            sc.Add(BoundField(ctx, "autoStart", "Auto start on play"));
            root.Add(sc);

            var steps = Section("Steps");
            var so = c != null ? c.scenario : null;
            if (so == null || so.steps == null || so.steps.Count == 0)
            {
                steps.Add(Body("No steps yet.", dim: true));
            }
            else
            {
                for (int i = 0; i < so.steps.Count; i++)
                    steps.Add(Body(StepLine(i, so.steps[i]), dim: true));
            }
            steps.Add(VSpace(8));
            steps.Add(DevkitWidgets.Actions(DevkitTheme.Primary("Open Scenario Graph", ScenarioGraphWindow.OpenWindow)));
            steps.Add(VSpace(6));
            steps.Add(Note("The Scenario Graph owns per-step editing: order, branches, completion conditions, the " +
                           "session start/stop bracket, and per-step analytics. This page is a read-only summary."));
            root.Add(steps);
        }

        static string StepLine(int i, Step s)
        {
            string n = i.ToString("00") + ". ";
            if (s == null) return n + "<null>";
            switch (s)
            {
                case TimelineStep tl:
                    return n + "Timeline " + (tl.director ? "[director set]" : "[no director]");
                case CueCardsStep cc:
                {
                    int times = cc.cueTimes != null ? cc.cueTimes.Length : 0;
                    return n + "Cue Cards " + (times == 0 ? "tap-only" : times + " cue time(s)");
                }
                case QuestionStep q:
                    return n + "Question " + (q.choices != null ? q.choices.Count : 0) + " choice(s)";
                case SelectionStep sel:
                    return n + "Selection " + sel.completion + " / required " + sel.requiredSelections;
                case InsertStep ins:
                {
                    string item = ins.item ? ins.item.name : "no item";
                    string target = ins.targetTrigger ? ins.targetTrigger.name : "no target";
                    return n + "Insert " + item + " -> " + target;
                }
                case EventStep ev:
                    return n + "Event " + (ev.waitSeconds > 0f ? ("wait " + ev.waitSeconds.ToString("0.##") + "s") : "immediate");
                default:
                    return n + s.GetType().Name;
            }
        }
    }

    // ---------- Parameters & State ----------
    internal sealed class ParametersPage : LabConsolePageBase
    {
        public override string Title => "Parameters & State";
        public override bool AlwaysShown => true; // LabConsole IS the lab state store - always present

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var sec = Section("Parameters");
            sec.Add(BoundField(ctx, "parameters", "Declared parameters"));
            sec.Add(VSpace(6));
            sec.Add(Note("Typed parameters seed the runtime store; bool parameters double as lab states for " +
                         "triggers/listeners and ConditionsStep. LabConsole itself is this lab's state store."));
            root.Add(sec);

            var map = Section("State map");
            map.Add(Body("Each parameter and its scope. Networked-scope parameters replicate across peers at runtime " +
                         "on a Fusion lab (RoutedParamStore -> NetworkedParamStore, B2.4); Local-scope stay client-local. " +
                         "Live values appear in Play mode.", dim: true));
            map.Add(VSpace(8));
            var host = new VisualElement();
            map.Add(host);
            root.Add(map);
            BuildStateMap(host, ctx);
            // Refresh live values a few times a second while playing (auto-stops when the page detaches).
            host.schedule.Execute(() => BuildStateMap(host, ctx)).Every(250);
        }

        void BuildStateMap(VisualElement host, LabConsoleContext ctx)
        {
            host.Clear();
            var arr = ctx.SerializedObject != null ? ctx.SerializedObject.FindProperty("parameters") : null;
            if (arr == null || arr.arraySize == 0) { host.Add(Body("(no parameters declared)", dim: true)); return; }

            bool playing = Application.isPlaying;
            Pitech.XR.Core.IParamStore store = (playing && ctx.Console != null) ? ctx.Console.Params : null;

            var head = Row();
            head.Add(Col("Parameter", 200));
            head.Add(Col("Type", 70));
            head.Add(Col("Scope", 96));
            head.Add(Col(playing ? "Live value" : "(play for live)", 130));
            host.Add(head);

            for (int i = 0; i < arr.arraySize; i++)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                var idP = elem.FindPropertyRelative("id");
                var typeP = elem.FindPropertyRelative("type");
                var scopeP = elem.FindPropertyRelative("scope");
                string id = idP != null ? idP.stringValue : null;
                if (string.IsNullOrEmpty(id)) continue;

                string type = (typeP != null && typeP.propertyType == SerializedPropertyType.Enum
                    && typeP.enumValueIndex >= 0 && typeP.enumValueIndex < typeP.enumDisplayNames.Length)
                    ? typeP.enumDisplayNames[typeP.enumValueIndex] : "";
                bool networked = scopeP != null && scopeP.propertyType == SerializedPropertyType.Enum && scopeP.enumValueIndex == 1;
                string val = (playing && store != null)
                    ? (store.TryGet(id, out Pitech.XR.Core.ParamValue v) ? Format(v) : "(unset)")
                    : "";

                var row = Row();
                row.style.marginBottom = 2;
                row.Add(Col(id, 200));
                row.Add(Col(type, 70));
                var scopeCell = new VisualElement { style = { width = 96, flexDirection = FlexDirection.Row } };
                scopeCell.Add(DevkitWidgets.Pill(networked ? "Networked" : "Local",
                    networked ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral));
                row.Add(scopeCell);
                row.Add(new Label(val) { style = { color = DevkitTheme.Text, fontSize = 11, width = 130 } });
                host.Add(row);
            }
        }

        static Label Col(string text, float w)
            => new Label(text) { style = { color = DevkitTheme.SubText, fontSize = 11, width = w } };

        static string Format(in Pitech.XR.Core.ParamValue v)
        {
            switch (v.Type)
            {
                case Pitech.XR.Core.ParamType.Bool: return v.AsBool() ? "true" : "false";
                case Pitech.XR.Core.ParamType.Int: return v.AsInt().ToString();
                case Pitech.XR.Core.ParamType.Float: return v.AsFloat().ToString("0.###");
                case Pitech.XR.Core.ParamType.Enum: return "enum " + v.AsInt();
                case Pitech.XR.Core.ParamType.String: return "\"" + v.AsString() + "\"";
                default: return v.AsString();
            }
        }
    }

    // ---------- Content ----------
    internal sealed class ContentPage : LabConsolePageBase
    {
        public override string Title => "Content";
        public override bool IsRelevant(LabConsoleContext ctx) => LabConsoleAuthoring.HasContent(ctx.Console);

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            // Quiz
            var quiz = Section("Quiz");
            quiz.Add(Caption("Quiz asset"));
            quiz.Add(BoundField(ctx, "defaultQuiz"));
            if (c != null && c.defaultQuiz == null)
                quiz.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Create & assign Quiz asset",
                    () => { LabConsoleAuthoring.CreateQuizAsset(c); ctx.RequestRefresh(); })));
            quiz.Add(Caption("Quiz panel"));
            quiz.Add(BoundField(ctx, "quizPanel"));
            quiz.Add(Caption("Quiz results panel"));
            quiz.Add(BoundField(ctx, "quizResultsPanel"));
            quiz.Add(VSpace(6));
            quiz.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Install Quiz UI + Wire",
                () => { LabConsoleAuthoring.InstallQuizUI(); ctx.RequestRefresh(); })));
            quiz.Add(VSpace(4));
            quiz.Add(Note("Use CanvasGroup-based panels so the UI can hide/show without disabling GameObjects."));
            root.Add(quiz);

            // Interactables
            var inter = Section("Interactables");
            inter.Add(Caption("Selectables manager"));
            inter.Add(BoundField(ctx, "selectables"));
            if (c != null && c.selectables == null)
                inter.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Create & assign Selectables Manager",
                    () => { LabConsoleAuthoring.CreateSelectablesManager(c); ctx.RequestRefresh(); })));
            inter.Add(Caption("Selection lists"));
            inter.Add(BoundField(ctx, "selectionLists"));
            if (c != null && c.selectionLists == null)
                inter.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Create & assign Selection Lists",
                    () => { LabConsoleAuthoring.CreateSelectionLists(c); ctx.RequestRefresh(); })));
            root.Add(inter);

            // Stats (legacy)
            var stats = Section("Stats (legacy)");
            stats.Add(Caption("Stats UI controller"));
            stats.Add(BoundField(ctx, "statsUI"));
            if (c != null && c.statsUI == null)
                stats.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Create & assign StatsUIController",
                    () => { LabConsoleAuthoring.CreateStatsUI(c); ctx.RequestRefresh(); })));
            stats.Add(Caption("Stats config"));
            stats.Add(BoundField(ctx, "statsConfig"));
            if (c != null && c.statsConfig == null)
                stats.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Create & assign StatsConfig asset",
                    () => { LabConsoleAuthoring.CreateStatsConfig(c); ctx.RequestRefresh(); })));
            stats.Add(VSpace(4));
            stats.Add(Note("Stats are legacy - prefer typed Parameters for new labs."));
            root.Add(stats);

            // Analytics subjects (scene-side wiring lives on Content per schema 6.1)
            var subj = Section("Tracked objects (scene wiring)");
            subj.Add(Body("The Tracked Objects registry (ids/labels/targets) is authored on the Analytics page. Scene-side " +
                          "AnalyticsSubject wiring / auto-wire lives on the Analytics recorder.", dim: true));
            var subjActions = DevkitWidgets.Actions(
                DevkitTheme.Secondary("Go to Analytics", () => ctx.GoTo("Analytics")));
            var la = LabConsoleAuthoring.ResolveAnalytics(c);
            if (la != null)
                subjActions.Add(DevkitTheme.Secondary("Open recorder (auto-wire)",
                    () => { Selection.activeObject = la.gameObject; EditorGUIUtility.PingObject(la.gameObject); }));
            subj.Add(subjActions);
            root.Add(subj);
        }
    }

    // ---------- Analytics ----------
    internal sealed class AnalyticsPage : LabConsolePageBase
    {
        public override string Title => "Analytics";
        public override bool IsRelevant(LabConsoleContext ctx) => LabConsoleAuthoring.HasAnalytics(ctx.Console);

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;
            var la = LabConsoleAuthoring.ResolveAnalytics(c);

            if (la == null)
            {
                root.Add(AddFeatureCard("Lab Analytics",
                    "This lab is ungraded. Add a Lab Analytics recorder (a sibling \"Analytics\" object) to author " +
                    "objectives, metrics, and per-step analytics.",
                    "Add Lab Analytics",
                    () => { LabConsoleAuthoring.AddAnalytics(c); ctx.RequestRefresh(); }));
                return;
            }

            var rec = Section("Recorder");
            rec.Add(ObjectLinkRow("Lab Analytics recorder", la));
            rec.Add(VSpace(4));
            rec.Add(Note("Authored on a sibling \"Analytics\" object. Step analytics live on the step nodes in the " +
                         "Scenario Graph; penalties + goals are on the recorder. Grade = base - penalties + bonus."));
            root.Add(rec);

            var config = Section("Config");
            var r = la.config;
            if (r == null)
            {
                config.Add(Body("No config yet.", dim: true));
            }
            else
            {
                int penalties = r.penalties != null ? r.penalties.Count : 0;
                int goals = r.goals != null ? r.goals.Count : 0;
                int steps = 0;
                if (r.analytics != null)
                    for (int i = 0; i < r.analytics.Count; i++)
                        if (r.analytics[i] is Pitech.XR.Analytics.StepAnalytic) steps++;
                int subjects = r.subjects != null ? r.subjects.Count : 0;

                config.Add(DevkitWidgets.PillsRow(
                    (DevkitWidgets.PillKind.Neutral, steps + " step" + (steps == 1 ? "" : "s")),
                    (DevkitWidgets.PillKind.Neutral, penalties + " penalt" + (penalties == 1 ? "y" : "ies")),
                    (DevkitWidgets.PillKind.Neutral, goals + " goal(s)"),
                    (DevkitWidgets.PillKind.Neutral, subjects + " tracked object(s)")));
            }
            config.Add(VSpace(8));
            config.Add(DevkitWidgets.Actions(
                DevkitTheme.Primary("Edit config", () => { Selection.activeObject = la.gameObject; EditorGUIUtility.PingObject(la.gameObject); }),
                OpenGraphButton()));
            root.Add(config);

            root.Add(PostB2Card("Report sink binding",
                "ISessionReportSink has no in-package implementation; the durable report outbox is host-owned. The " +
                "recorder warns at runtime if no sink is assigned. Binding UI is post-B2."));
        }
    }

    // ---------- Roles ----------
    internal sealed class RolesPage : LabConsolePageBase
    {
        public override string Title => "Roles";
        public override bool IsRelevant(LabConsoleContext ctx) => LabConsoleAuthoring.HasRoles(ctx.Console);

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;
            var sel = LabConsoleAuthoring.ResolveRoles(c);

            if (sel == null)
            {
                root.Add(AddFeatureCard("Session Roles",
                    "Add an in-scene role selector. Roles gate ANALYTICS only (never flow/interaction): anyone, any " +
                    "role, completes steps and it counts. The pick UI is author-built and wires to the selector.",
                    "Add Session Roles",
                    () => { LabConsoleAuthoring.AddRoles(c); ctx.RequestRefresh(); }));
                return;
            }

            var s = Section("Role selector");
            s.Add(ObjectLinkRow("Session Role Selector", sel));
            s.Add(VSpace(6));
            s.Add(Body("Current role: " + sel.CurrentRole, dim: true));
            var caps = sel.Capacities;
            if (caps != null)
                s.Add(Body("Capacities - Professors " + Cap(caps.maxProfessors) +
                           " / Participants " + Cap(caps.maxParticipants) +
                           " / Spectators " + Cap(caps.maxSpectators), dim: true));
            root.Add(s);

            root.Add(PostB2Card("Capacity enforcement",
                "Capacities are authored here on the Session Role Selector (the single source of truth; LabAnalytics " +
                "mirrors them into the report at runtime). Foundation only: currently only a max of 0 blocks a role - " +
                "minimums and cross-peer headcount are post-B2."));
        }

        static string Cap(int v) => v < 0 ? "unlimited" : (v == 0 ? "blocked" : v.ToString());
    }

    // ---------- Vitals ----------
    internal sealed class VitalsPage : LabConsolePageBase
    {
        public override string Title => "Vitals";
        public override bool IsRelevant(LabConsoleContext ctx) => LabConsoleAuthoring.HasVitals(ctx.Console);

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            if (LabConsoleAuthoring.FindType("PatientVitals", "Pitech.XR.Vitals") == null)
            {
                root.Add(Note("The Vitals module (Pitech.XR.Vitals) is not present in this project."));
                return;
            }

            var v = LabConsoleAuthoring.ResolveVitals(c);
            if (v == null)
            {
                root.Add(AddFeatureCard("Patient Vitals",
                    "Add a PatientVitals component to drive patient state and vitals channels for this lab.",
                    "Add Patient Vitals",
                    () => { LabConsoleAuthoring.AddVitals(c); ctx.RequestRefresh(); }));
                return;
            }

            var s = Section("Patient vitals");
            s.Add(ObjectLinkRow("Patient Vitals", v));
            root.Add(s);

            root.Add(PostB2Card("Monitor & channel binding",
                "Foundation only by design: real 3D / monitor-UI binding and clinical-trigger mappings are " +
                "author-side / post-B2."));
        }
    }

    // ---------- Delivery (signpost; stays in the inspector for now) ----------
    internal sealed class DeliveryPage : LabConsolePageBase
    {
        public override string Title => "Delivery";
        public override bool IsRelevant(LabConsoleContext ctx) => LabConsoleAuthoring.HasDelivery(ctx.Console);

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            var s = Section("Content Delivery");
            s.Add(Note("Content Delivery is currently authored on the LabConsole inspector (kept there for now, " +
                       "2026-06-30). Its move to this page is deferred."));
            s.Add(VSpace(8));

            if (LabConsoleAuthoring.HasDelivery(c))
                s.Add(ObjectLinkRow("Content Delivery", c.contentDelivery));
            else
                s.Add(Body("No Content Delivery on this lab yet.", dim: true));

            s.Add(VSpace(8));
            s.Add(DevkitWidgets.Actions(DevkitTheme.Secondary("Select LabConsole (edit in inspector)",
                () => { if (c != null) { Selection.activeObject = c.gameObject; EditorGUIUtility.PingObject(c.gameObject); } })));
            root.Add(s);
        }
    }

    // ---------- Run ----------
    internal sealed class RunPage : LabConsolePageBase
    {
        public override string Title => "Run";
        public override bool AlwaysShown => true;

        public override void BuildUI(VisualElement root, LabConsoleContext ctx)
        {
            var c = ctx.Console;

            var s = Section("Session");
            var status = new Label("-") { style = { color = DevkitTheme.Text } };
            s.Add(status);
            s.Add(VSpace(6));

            // Progress track + fill (themed; updated on a schedule)
            var track = new VisualElement();
            track.style.height = 8;
            track.style.backgroundColor = DevkitTheme.Panel2;
            Round(track, 4);
            var fill = new VisualElement();
            fill.style.height = 8;
            fill.style.backgroundColor = DevkitTheme.Brand;
            Round(fill, 4);
            fill.style.width = Length.Percent(0);
            track.Add(fill);
            s.Add(track);
            s.Add(VSpace(10));

            var restart = DevkitTheme.Primary("Restart Scenario", () => { if (c != null) c.Restart(); });
            s.Add(DevkitWidgets.Actions(restart));
            s.Add(VSpace(4));
            s.Add(Note("Enter Play mode to run the lab and see live progress."));
            root.Add(s);

            root.Add(PostB2Card("Live debug",
                "The analytics event stream, readout preview, state debugger, and network status panels are partly " +
                "post-B2. Today this page drives the run bracket (status, progress, restart)."));

            void Tick()
            {
                if (c == null) return;
                var sc = c.scenario;
                int total = (sc != null && sc.steps != null) ? sc.steps.Count : 0;
                int idx = c.StepIndex;

                if (!Application.isPlaying) status.text = "Editor idle (enter Play mode)";
                else if (idx < 0 || total == 0) status.text = "Idle / finished";
                else status.text = "Step " + (idx + 1) + " of " + total;

                float p = (total > 0 && idx >= 0 && idx < total) ? (idx + 1) / (float)total : 0f;
                fill.style.width = Length.Percent(p * 100f);
                restart.SetEnabled(Application.isPlaying);
            }
            Tick();
            status.schedule.Execute(Tick).Every(200);
        }
    }
}
#endif
