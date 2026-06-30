#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: LOCALIZATION - keyed Greek + English, baked at build time. Promoted to a
    // top-level Hub destination at the user's request. The authoring + runtime logic shipped in
    // Phase B (WS B7); this page is the top-level destination for it.
    public sealed class LocalizationPage : IDevkitPage
    {
        public string Title => "Localization";

        public void BuildUI(VisualElement root)
        {
            var section = DevkitTheme.Section("Localization");
            section.Add(DevkitTheme.Body(
                "Keyed Greek + English strings, baked at build time. The authoring and runtime " +
                "logic shipped in Phase B (WS B7).",
                dim: true));
            section.Add(DevkitTheme.VSpace(10));

            var grid = DevkitWidgets.TileGrid();
            grid.Add(DevkitWidgets.Card(
                "Localization",
                "Keyed string tables for Greek + English, resolved per-locale and baked into the build.",
                DevkitWidgets.Actions(),
                PlannedBody()));
            section.Add(grid);
            root.Add(section);
        }

        // DevkitWidgets.Card takes a single body VisualElement, so the Reserved pill and the
        // planned-capability list are wrapped together here.
        static VisualElement PlannedBody()
        {
            var body = new VisualElement();
            body.Add(DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Success, "Available")));
            body.Add(DevkitTheme.VSpace(8));

            string[] planned =
            {
                "Author keyed strings once; resolve the active locale at runtime.",
                "Greek + English at launch; the locale is baked at build time (no live download).",
                "Scenario text - cue cards, questions, prompts - resolves through the table.",
                "Shipped in Phase B WS B7; a top-level Hub destination.",
            };
            for (int i = 0; i < planned.Length; i++)
            {
                if (i > 0) body.Add(DevkitTheme.VSpace(6));
                body.Add(DevkitTheme.Body("- " + planned[i], dim: false));
            }
            return body;
        }
    }
}
#endif
