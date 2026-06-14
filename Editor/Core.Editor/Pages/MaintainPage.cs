#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: MAINTAIN - the gate + repair/diagnose tools (WS A2 Step 4). Surfaces
    // "Evaluate Changes" (LIVE since WS A3 - opens the EditMode-net verdict window), the
    // fixture-export tools, the two in-place DevKit script-repair tools, and the
    // recommended-settings health fix.
    public sealed class MaintainPage : IDevkitPage
    {
        // Repair-tool menu paths - verbatim from DevKitFixMissingScriptRefs (Scenario.Editor).
        const string FixMissingRefsMenu = "Pi tech/Tools/Fix Missing DevKit Script References on Selection";
        const string RepairGuidsMenu = "Pi tech/Tools/Repair DevKit script GUIDs in selected prefab/scene asset (YAML only)";
        // Fixture menu paths - verbatim from ExportLabAsTestFixture / ExportAllTestScenes /
        // TestScenesListWindow (Scenario.Editor).
        const string ExportFixtureMenu = "Pi tech/Tools/Export Lab as Test Fixture";
        const string ExportAllScenesMenu = "Pi tech/Tools/Export All Test Scenes";
        const string ManageScenesMenu = "Pi tech/Tools/Manage Test Scenes List";
        const string SyntheticFixtureMenu = "Pi tech/Tools/Generate Synthetic Scenario Fixture";

        public string Title => "Maintain";

        public void BuildUI(VisualElement root)
        {
            // ===== The gate (Evaluate Changes) =====
            {
                var section = DevkitTheme.Section("Evaluate Changes (the gate)");
                section.Add(DevkitTheme.Body("Run the EditMode safety net and get a plain-language verdict before you push.", dim: true));
                section.Add(DevkitTheme.VSpace(8));
                var grid = DevkitWidgets.TileGrid();
#if PITECH_HAS_TESTFRAMEWORK
                grid.Add(DevkitWidgets.Card(
                    "Evaluate Changes",
                    "Run the DevKit equivalence proofs (graph integrity, public API, serialized GUIDs) and report pass/fail.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Run Evaluate Changes", EvaluateChanges.Open)),
                    DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Runs the EditMode net"))));
#else
                grid.Add(DevkitWidgets.Card(
                    "Evaluate Changes",
                    "Requires the Unity Test Framework package (com.unity.test-framework). Install it via the Package Manager to enable the gate.",
                    DevkitWidgets.Actions(),
                    DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Test Framework not installed"))));
#endif
                section.Add(grid);
                root.Add(section);
            }

            // ===== Fixture export (feeds the Proof A/C corpus) =====
            {
                var section = DevkitTheme.Section("Test Fixtures");
                section.Add(DevkitTheme.Body("Run the export in the LAB project (HealthOn VR, DevKit on a local file: reference) right before testing - fixtures land directly in the package for the gate project to test against.", dim: true));
                section.Add(DevkitTheme.VSpace(8));
                var grid = DevkitWidgets.TileGrid();

                grid.Add(DevkitWidgets.Card(
                    "Export Lab as Fixture",
                    "Capture the WHOLE open lab scene as one self-contained fixture prefab + its Proof A baseline and dependency declaration. Faithful capture: pre-existing lab notes are logged with an \"Export anyway\" option; only a break the export itself introduced is refused.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Export Open Scene", () =>
                    {
                        RunMenu(ExportFixtureMenu);
                    })),
                    DevkitTheme.Body("Open the lab scene (saved, no pending edits) first.", dim: true)));

                grid.Add(DevkitWidgets.Card(
                    "Export All Test Scenes",
                    "Re-export every scene in your curated test-scenes list in one pass (auto-seeded from the scenes that match your committed lab fixtures). Each scene's fixture + baseline + deps are recaptured; for an unchanged lab the graph snapshot stays the same (prefab fileIDs still churn on re-export). Use \"Manage list\" to add a new lab or drop one.",
                    DevkitWidgets.Actions(
                        DevkitTheme.Primary("Export All Test Scenes", () =>
                        {
                            RunMenu(ExportAllScenesMenu);
                        }),
                        DevkitTheme.Secondary("Manage list…", () =>
                        {
                            RunMenu(ManageScenesMenu);
                        })),
                    DevkitTheme.Body("Run in the LAB project (HealthOn VR) with every open scene saved.", dim: true)));

                grid.Add(DevkitWidgets.Card(
                    "Generate Synthetic Fixture",
                    "Regenerate the MEGA census-superset fixture (every step type, routing family, GroupStep mode, and listener shape) + its variant twin + the LegacyForms twins. Deliberate-only; spec: Documentation~/specs/2026-06-11-mega-fixture-spec.md.",
                    DevkitWidgets.Actions(DevkitTheme.Secondary("Generate", () =>
                    {
                        RunMenu(SyntheticFixtureMenu);
                    }))));

                section.Add(grid);
                root.Add(section);
            }

            // ===== Repair tools =====
            {
                var section = DevkitTheme.Section("Repair");
                var grid = DevkitWidgets.TileGrid();

                grid.Add(DevkitWidgets.Card(
                    "Fix Missing Script References",
                    "Re-link missing DevKit MonoScript references on the selected GameObject(s) in place - never drops [SerializeReference] data.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Fix on Selection", () =>
                    {
                        if (!EditorApplication.ExecuteMenuItem(FixMissingRefsMenu))
                            EditorUtility.DisplayDialog(
                                "Fix Missing Script References",
                                "Select the affected GameObject(s) in the scene first, then try again.",
                                "OK");
                    })),
                    DevkitTheme.Body("Select the affected GameObject(s) in the scene first.", dim: true)));

                grid.Add(DevkitWidgets.Card(
                    "Repair Script GUIDs (YAML)",
                    "Rewrite the m_Script GUID in the selected .prefab/.unity asset, keeping fileID - for assets with dangling DevKit script links.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Repair Selected Asset", () =>
                    {
                        if (!EditorApplication.ExecuteMenuItem(RepairGuidsMenu))
                            EditorUtility.DisplayDialog(
                                "Repair Script GUIDs",
                                "Select a .prefab or .unity asset in the Project window first, then try again.",
                                "OK");
                    })),
                    DevkitTheme.Body("Select a .prefab or .unity asset in the Project window first.", dim: true)));

                section.Add(grid);
                root.Add(section);
            }

            // ===== Project health =====
            {
                var health = new ProjectHealthService();
                var section = DevkitTheme.Section("Project Health");
                var grid = DevkitWidgets.TileGrid();
                grid.Add(DevkitWidgets.Card(
                    "Recommended Settings",
                    "Apply DevKit-recommended project settings (linear color space, Force Text serialization, visible meta files).",
                    DevkitWidgets.Actions(DevkitTheme.Secondary("Apply recommended settings", health.FixRecommended))));
                section.Add(grid);
                root.Add(section);
            }
        }

        // Invoke a Pi tech menu item; surface a dialog if the path no longer resolves, so a future
        // menu rename fails loudly instead of a silent no-op. (The repair tools keep their own
        // context-specific dialogs - their ExecuteMenuItem returns false on a wrong selection, not a
        // missing path.)
        static void RunMenu(string menuPath)
        {
            if (!EditorApplication.ExecuteMenuItem(menuPath))
                EditorUtility.DisplayDialog("DevKit",
                    "Could not run the menu command:\n" + menuPath
                    + "\n\nThe menu path may have moved - update the Maintain page.", "OK");
        }
    }
}
#endif
