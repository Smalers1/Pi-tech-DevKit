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
        // Fixture menu paths - verbatim from ExportLabAsTestFixture (Scenario.Editor).
        const string ExportFixtureMenu = "Pi tech/Tools/Export Lab as Test Fixture";
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
                    "Capture the WHOLE open lab scene as one self-contained fixture prefab + its Proof A baseline. Refuses on graph violations, so a broken lab can never poison the corpus.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Export Open Scene", () =>
                    {
                        EditorApplication.ExecuteMenuItem(ExportFixtureMenu);
                    })),
                    DevkitTheme.Body("Open the lab scene (saved, no pending edits) first.", dim: true)));

                grid.Add(DevkitWidgets.Card(
                    "Generate Synthetic Fixture",
                    "Create the mandatory synthetic fixture covering the routing families absent from real labs (ConditionsStep, SpecificChild, allowedWrong>0, defaultNextGuid).",
                    DevkitWidgets.Actions(DevkitTheme.Secondary("Generate", () =>
                    {
                        EditorApplication.ExecuteMenuItem(SyntheticFixtureMenu);
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
    }
}
#endif
