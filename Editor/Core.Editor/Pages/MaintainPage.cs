#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: MAINTAIN - the gate + repair/diagnose tools (WS A2 Step 4). Surfaces
    // "Evaluate Changes" (ships with WS A3 - until then the tile informs gracefully), the two
    // in-place DevKit script-repair tools, and the recommended-settings health fix.
    public sealed class MaintainPage : IDevkitPage
    {
        // Repair-tool menu paths - verbatim from DevKitFixMissingScriptRefs (Scenario.Editor).
        const string FixMissingRefsMenu = "Pi tech/Tools/Fix Missing DevKit Script References on Selection";
        const string RepairGuidsMenu = "Pi tech/Tools/Repair DevKit script GUIDs in selected prefab/scene asset (YAML only)";
        // WS A3 ships this menu item; until then ExecuteMenuItem returns false and we inform the user.
        const string EvaluateChangesMenu = "Pi tech/Tools/Evaluate Changes";

        public string Title => "Maintain";

        public void BuildUI(VisualElement root)
        {
            // ===== The gate (Evaluate Changes) =====
            {
                var section = DevkitTheme.Section("Evaluate Changes (the gate)");
                section.Add(DevkitTheme.Body("Run the EditMode safety net and get a plain-language verdict before you push.", dim: true));
                section.Add(DevkitTheme.VSpace(8));
                var grid = DevkitWidgets.TileGrid();
                grid.Add(DevkitWidgets.Card(
                    "Evaluate Changes",
                    "Run the DevKit equivalence proofs (graph integrity, public API, serialized GUIDs) and report pass/fail.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Run Evaluate Changes", () =>
                    {
                        if (!EditorApplication.ExecuteMenuItem(EvaluateChangesMenu))
                            EditorUtility.DisplayDialog(
                                "Evaluate Changes",
                                "Evaluate Changes ships with WS A3 (the EditMode safety net). It is not available yet.",
                                "OK");
                    })),
                    DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Arrives with WS A3"))));
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
