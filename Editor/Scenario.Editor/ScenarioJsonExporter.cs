#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Exports a <see cref="Scenario"/>'s step graph to portable JSON (the portal / authoring surface).
    /// Walks the scenario through a <see cref="SerializedObject"/> - the only reliable way to read a
    /// <c>[SerializeReference]</c> polymorphic list - and emits a <see cref="ScenarioDto"/> via
    /// <c>JsonUtility</c>. The concrete step type is captured as the CLR short name discriminator
    /// (identical derivation to <c>ScenarioGraphSnapshot.ShortTypeName</c>), since JsonUtility itself
    /// erases the managed-reference type.
    ///
    /// The DTO is FLAT: every step (root + nested group children, any depth) is one entry in
    /// <see cref="ScenarioDto.steps"/>, with structure carried by <see cref="ScenarioDto.rootIndices"/>
    /// + <see cref="StepDto.childIndices"/>. This avoids a self-referential DTO field, which JsonUtility
    /// refuses to serialize past depth 10. The walk still descends the real graph recursively; only the
    /// emitted shape is flat.
    ///
    /// SCOPE: only the portable authoring surface (see <see cref="ScenarioDto"/> for the full IN/OUT
    /// list). Routing references and serializable scalars/enums/plain-data lists are emitted; object
    /// references and UnityEvent persistent calls are deliberately skipped (scene/asset-bound - cannot
    /// portably round-trip into a fresh scenario). Field classification (routing vs scalar) reuses the
    /// exact same <c>*NextGuid</c> / <c>specificStepGuid</c> / <c>childRequirements[].guid</c> convention
    /// as <c>ScenarioGraphSnapshot.IsRoutingField</c>, so the routing this exporter captures is, by
    /// construction, the same set the snapshot's routing map records.
    /// </summary>
    public static class ScenarioJsonExporter
    {
        /// <summary>Portable DTO schema version. Starts at 1.</summary>
        public const int SchemaVersion = 1;

        /// <summary>
        /// Serialize <paramref name="scenario"/>'s step graph to portable JSON. Returns pretty-printed
        /// JSON (a one-line empty DTO when <paramref name="scenario"/> is null). Does not mutate the
        /// scenario (read-only SerializedObject walk).
        /// </summary>
        public static string ToJson(Scenario scenario)
        {
            var dto = ToDto(scenario);
            return JsonUtility.ToJson(dto, true);
        }

        /// <summary>Build the portable DTO from a scenario (exposed for tests/tooling that want the
        /// object rather than its JSON form).</summary>
        public static ScenarioDto ToDto(Scenario scenario)
        {
            var dto = new ScenarioDto { schemaVersion = SchemaVersion };
            if (scenario == null) return dto;

            var so = new SerializedObject(scenario);
            var titleProp = so.FindProperty("title");
            dto.title = titleProp != null ? (titleProp.stringValue ?? "") : "";

            var stepsRoot = so.FindProperty("steps");
            if (stepsRoot != null && stepsRoot.isArray)
            {
                int count = stepsRoot.arraySize;
                for (int i = 0; i < count; i++)
                {
                    var element = stepsRoot.GetArrayElementAtIndex(i);
                    if (element.propertyType != SerializedPropertyType.ManagedReference) continue;
                    if (string.IsNullOrEmpty(element.managedReferenceFullTypename)) continue;   // null slot
                    int idx = CaptureStepFlat(element, dto);
                    dto.rootIndices.Add(idx);
                }
            }
            return dto;
        }

        // Capture one step element (and, recursively, its nested GroupStep children) into the FLAT
        // dto.steps list. Returns the flat index of THIS step. The step's slot is reserved before its
        // children are captured (pre-order), so a parent always has a lower index than its descendants -
        // but the importer relies on the explicit index links, not on ordering.
        static int CaptureStepFlat(SerializedProperty element, ScenarioDto dto)
        {
            var stepDto = new StepDto { type = ShortTypeName(element.managedReferenceFullTypename) };
            int myIndex = dto.steps.Count;
            dto.steps.Add(stepDto);          // reserve the slot before recursing children
            CaptureStepLeaves(element, stepDto, dto);
            return myIndex;
        }

        // Walk one step element's own subtree, capturing its portable leaves into stepDto. When the walk
        // reaches a nested step list (GroupStep.steps), each child is captured into the flat dto.steps via
        // CaptureStepFlat and its index recorded in stepDto.childIndices - the walk does NOT descend into
        // that array itself (its elements are managed references handled by the recursion).
        static void CaptureStepLeaves(SerializedProperty element, StepDto stepDto, ScenarioDto dto)
        {
            string elementPath = element.propertyPath;
            int elementPathLen = elementPath.Length;

            var p = element.Copy();
            var end = element.GetEndProperty();
            bool enterChildren = true;
            while (p.Next(enterChildren))
            {
                if (SerializedProperty.EqualContents(p, end)) break;

                string full = p.propertyPath;
                string rel = RelativePath(full, elementPathLen);

                // Step identity / position: lifted to first-class DTO fields, not generic leaves.
                if (rel == "guid" && p.propertyType == SerializedPropertyType.String)
                {
                    stepDto.guid = p.stringValue ?? "";
                    enterChildren = false;
                    continue;
                }
                if (rel == "graphPos" && p.propertyType == SerializedPropertyType.Vector2)
                {
                    Vector2 v = p.vector2Value;
                    stepDto.graphPosX = v.x;
                    stepDto.graphPosY = v.y;
                    enterChildren = false;   // do not descend into x/y (already captured)
                    continue;
                }

                // A nested step list (GroupStep.steps): capture each child into the flat list and record
                // its index; DO NOT walk into the array here.
                if (IsNestedStepsArray(rel) && p.isArray)
                {
                    int n = p.arraySize;
                    for (int i = 0; i < n; i++)
                    {
                        var child = p.GetArrayElementAtIndex(i);
                        if (child.propertyType != SerializedPropertyType.ManagedReference) continue;
                        if (string.IsNullOrEmpty(child.managedReferenceFullTypename)) continue;   // null slot
                        int childIndex = CaptureStepFlat(child, dto);
                        stepDto.childIndices.Add(childIndex);
                    }
                    enterChildren = false;   // skip the array's interior in this walk
                    continue;
                }

                enterChildren = true;

                // Routing references (nextGuid family, specificStepGuid, childRequirement.guid): same
                // convention as ScenarioGraphSnapshot.IsRoutingField - keyed by full path there, by
                // step-relative path here.
                if (p.propertyType == SerializedPropertyType.String && IsRoutingField(p.name, full))
                {
                    stepDto.routes.Add(new LeafDto { path = rel, kind = "String", value = p.stringValue ?? "" });
                    continue;
                }

                // Object references and UnityEvent persistent calls: OUT of scope (scene/asset-bound).
                if (p.propertyType == SerializedPropertyType.ObjectReference) continue;
                if (IsUnderPersistentCall(full)) continue;

                // Portable scalar leaves.
                if (TryLeaf(p, out string kind, out string value))
                    stepDto.scalars.Add(new LeafDto { path = rel, kind = kind, value = value });
            }
        }

        // ---- portable-leaf classification ----------------------------------------------------

        static bool TryLeaf(SerializedProperty p, out string kind, out string value)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.String:
                    kind = "String"; value = p.stringValue ?? ""; return true;
                case SerializedPropertyType.Integer:
                    kind = "Integer"; value = p.longValue.ToString(CultureInfo.InvariantCulture); return true;
                case SerializedPropertyType.Boolean:
                    kind = "Boolean"; value = p.boolValue ? "true" : "false"; return true;
                case SerializedPropertyType.Float:
                    kind = "Float"; value = p.doubleValue.ToString("R", CultureInfo.InvariantCulture); return true;
                case SerializedPropertyType.Enum:
                    // enumValueIndex is the portable serialized index (matches ScenarioGraphSnapshot's
                    // event-leaf fingerprint). Importer sets it back via enumValueIndex.
                    kind = "Enum"; value = p.enumValueIndex.ToString(CultureInfo.InvariantCulture); return true;
                default:
                    kind = null; value = null; return false;   // Vector/Color/AnimationCurve/etc. not portable here
            }
        }

        // ---- shared classifiers (mirrors ScenarioGraphSnapshot exactly) ----------------------

        static bool IsRoutingField(string name, string path)
        {
            if (name == "specificStepGuid") return true;
            if (name == "nextGuid") return true;
            if (name != null && name.EndsWith("NextGuid", System.StringComparison.Ordinal)) return true;
            if (name == "guid" && IsUnderRequirementList(path)) return true;
            return false;
        }

        static bool IsUnderRequirementList(string path)
            => path != null && path.Contains("childRequirements");

        static bool IsUnderPersistentCall(string path)
            => path != null && path.Contains("m_PersistentCalls");

        // True for a step-relative path that is exactly a nested "steps" array root ("steps" - the
        // GroupStep child list). Deeper nested step arrays are reached by recursion into children.
        static bool IsNestedStepsArray(string relativePath)
            => relativePath == "steps";

        // "Pitech.XR.Scenario Pitech.XR.Scenario.EventStep" -> "EventStep" (identical to
        // ScenarioGraphSnapshot.ShortTypeName - the ratified discriminator derivation).
        static string ShortTypeName(string managedReferenceFullTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFullTypename)) return null;
            int sp = managedReferenceFullTypename.LastIndexOf(' ');
            string t = sp >= 0 ? managedReferenceFullTypename.Substring(sp + 1) : managedReferenceFullTypename;
            int dot = t.LastIndexOf('.');
            return dot >= 0 ? t.Substring(dot + 1) : t;
        }

        // Strip the element prefix + the joining '.' to get the step-relative path. The walk only yields
        // descendants of the element, so 'full' always starts with the element path; the +1 drops the dot.
        static string RelativePath(string full, int elementPathLen)
            => full.Length > elementPathLen ? full.Substring(elementPathLen + 1) : full;
    }
}
#endif
