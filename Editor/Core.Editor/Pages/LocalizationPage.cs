#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: LOCALIZATION - keyed Greek + English, baked at build time. Promoted to a
    // top-level Hub destination at the user's request. The module itself is RESERVED: the
    // authoring + runtime logic land in Phase B (WS B7, spec §28.3). Until then this page
    // documents the planned capability and performs no actions (observer-only, like the rest
    // of the cockpit).
    public sealed class LocalizationPage : IDevkitPage
    {
        public string Title => "Localization";

        public void BuildUI(VisualElement root)
        {
            var section = DevkitTheme.Section("Localization");
            section.Add(DevkitTheme.Body(
                "Keyed Greek + English strings, baked at build time. This module is reserved - " +
                "the authoring and runtime logic land in Phase B (WS B7, spec §28.3).",
                dim: true));
            section.Add(DevkitTheme.VSpace(10));

            var grid = DevkitWidgets.TileGrid();
            grid.Add(DevkitWidgets.Card(
                "Localization (coming in Phase B)",
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
            body.Add(DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Reserved")));
            body.Add(DevkitTheme.VSpace(8));

            string[] planned =
            {
                "Author keyed strings once; resolve the active locale at runtime.",
                "Greek + English at launch; the locale is baked at build time (no live download).",
                "Scenario text - cue cards, questions, prompts - resolves through the table.",
                "Arrives in Phase B WS B7 (spec §28.3); reserved here as a top-level destination.",
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
