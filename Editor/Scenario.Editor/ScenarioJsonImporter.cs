#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Imports portable JSON (produced by <see cref="ScenarioJsonExporter"/>) onto a target
    /// <see cref="Scenario"/>, REBUILDING its step list from the DTO. The in-scene/in-prefab scenario
    /// stays canonical for object references and UnityEvents - those are OUT of the portable scope and
    /// are left at the C# defaults of each freshly created step (documented on
    /// <see cref="ScenarioDto"/>). Import replaces the entire <c>steps</c> graph: concrete step instances
    /// are re-created by their CLR short type name (mapped to a <see cref="Type"/> in the
    /// <c>Pitech.XR.Scenario</c> assembly via reflection), the nested <see cref="GroupStep"/> hierarchy is
    /// rebuilt from the DTO's flat step list + index links, then guid / graphPos / routing / scalar leaves
    /// are replayed through a <see cref="SerializedObject"/> by their step-relative paths.
    ///
    /// What import does NOT restore (by design, scene/asset-bound): <c>UnityEngine.Object</c> references
    /// (Button, PlayableDirector, panels, QuizAsset, GameObject, Transform, Collider, Animator,
    /// SelectionLists, Component) and <c>UnityEvent</c> persistent calls (onEnter / onSelected /
    /// onCorrect / onWrong). After import those slots hold each step type's constructor defaults (empty
    /// UnityEvent, null object ref) and must be re-wired in the scene. This makes import suitable for
    /// moving the AUTHORING graph (flow + data) between scenarios, not for cloning scene bindings.
    /// </summary>
    public static class ScenarioJsonImporter
    {
        /// <summary>
        /// Parse <paramref name="json"/> and rebuild <paramref name="target"/>'s step graph from it,
        /// registering an Undo step. Throws <see cref="ArgumentNullException"/> on a null target,
        /// <see cref="ArgumentException"/> on null/empty JSON, and an <see cref="InvalidOperationException"/>
        /// if a step's CLR type cannot be resolved (a faithful round-trip must not silently drop a step).
        /// </summary>
        public static void Apply(string json, Scenario target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Empty JSON.", nameof(json));

            ScenarioDto dto;
            try
            {
                dto = JsonUtility.FromJson<ScenarioDto>(json);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Could not parse scenario JSON: " + e.Message, nameof(json), e);
            }
            if (dto == null) throw new ArgumentException("Scenario JSON parsed to null.", nameof(json));

            ApplyDto(dto, target);
        }

        /// <summary>Rebuild <paramref name="target"/>'s step graph from an already-parsed DTO.</summary>
        public static void ApplyDto(ScenarioDto dto, Scenario target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Undo.RegisterCompleteObjectUndo(target, "Import Scenario JSON");

            int n = dto.steps != null ? dto.steps.Count : 0;

            // 1) Create one concrete instance per flat DTO entry (no nesting yet), so every managed
            //    reference exists before we wire structure + replay leaves.
            var instances = new Step[n];
            for (int i = 0; i < n; i++)
                instances[i] = BuildInstance(dto.steps[i]);

            // 2) Wire the hierarchy from the index links: root steps, then each group's children. The
            //    arrays are built in the DTO's recorded order, so element index k == rootIndices[k] etc.
            target.steps = ResolveList(dto.rootIndices, instances);
            for (int i = 0; i < n; i++)
            {
                if (instances[i] is GroupStep g)
                    g.steps = ResolveList(dto.steps[i].childIndices, instances);
            }

            // 3) Replay leaves (guid / graphPos / routing / scalars) through a SerializedObject, driving
            //    the walk by the SAME index structure so each element gets its own StepDto's leaves.
            var so = new SerializedObject(target);
            var titleProp = so.FindProperty("title");
            if (titleProp != null && dto.title != null) titleProp.stringValue = dto.title;

            var stepsRoot = so.FindProperty("steps");
            if (stepsRoot != null && stepsRoot.isArray && dto.rootIndices != null)
            {
                // target.steps was built strictly from rootIndices (no skips), so element r aligns with
                // rootIndices[r] one-to-one.
                for (int r = 0; r < dto.rootIndices.Count && r < stepsRoot.arraySize; r++)
                    ReplayStep(stepsRoot.GetArrayElementAtIndex(r), dto.steps[dto.rootIndices[r]], dto);
            }

            so.ApplyModifiedProperties();   // registers Undo + marks the Scenario dirty
            EditorUtility.SetDirty(target);
        }

        // ---- instance construction (concrete type only) --------------------------------------

        static Step BuildInstance(StepDto dto)
        {
            if (dto == null) throw new InvalidOperationException("Null step entry in scenario JSON.");
            Type t = ResolveStepType(dto.type);
            if (t == null)
                throw new InvalidOperationException(
                    $"Unknown step type '{dto.type}' in scenario JSON - cannot resolve a CLR type in the "
                    + "Pitech.XR.Scenario assembly. Aborting rather than dropping a step.");
            return (Step)Activator.CreateInstance(t);
        }

        // Resolve a list of flat indices to their instances, in order. A bad index means a corrupt DTO -
        // abort rather than silently drop a step (which would also misalign the leaf-replay walk).
        static List<Step> ResolveList(List<int> indices, Step[] instances)
        {
            var list = new List<Step>(indices != null ? indices.Count : 0);
            if (indices == null) return list;
            foreach (int idx in indices)
            {
                if (idx < 0 || idx >= instances.Length)
                    throw new InvalidOperationException(
                        $"Scenario JSON references an out-of-range step index {idx} (step count {instances.Length}).");
                list.Add(instances[idx]);   // BuildInstance never returns null (it throws on an unknown type)
            }
            return list;
        }

        // ---- leaf replay ---------------------------------------------------------------------

        // Replay one element's leaves, then recurse into its group children by the DTO index links. The
        // model hierarchy (built in step 2) matches the DTO order, so child element j corresponds to
        // childIndices[j].
        static void ReplayStep(SerializedProperty element, StepDto dto, ScenarioDto root)
        {
            if (element == null || dto == null) return;

            // Identity + position first (first-class DTO fields, not in the leaf lists).
            var guidProp = element.FindPropertyRelative("guid");
            if (guidProp != null) guidProp.stringValue = dto.guid ?? "";
            var graphPosProp = element.FindPropertyRelative("graphPos");
            if (graphPosProp != null) graphPosProp.vector2Value = new Vector2(dto.graphPosX, dto.graphPosY);

            // Routing + scalar leaves are replayed identically (separated only for clarity in the DTO).
            ReplayLeaves(element, dto.routes);
            ReplayLeaves(element, dto.scalars);

            // Recurse into nested GroupStep children. Step 2 built each group's steps strictly from
            // childIndices (no skips), so child element c aligns with childIndices[c] one-to-one.
            if (dto.childIndices != null && dto.childIndices.Count > 0)
            {
                var childArray = element.FindPropertyRelative("steps");
                if (childArray != null && childArray.isArray)
                    for (int c = 0; c < dto.childIndices.Count && c < childArray.arraySize; c++)
                        ReplayStep(childArray.GetArrayElementAtIndex(c), root.steps[dto.childIndices[c]], root);
            }
        }

        static void ReplayLeaves(SerializedProperty element, List<LeafDto> leaves)
        {
            if (leaves == null) return;
            foreach (var leaf in leaves)
            {
                if (leaf == null || string.IsNullOrEmpty(leaf.path)) continue;
                var prop = EnsureRelativeProperty(element, leaf.path);
                if (prop == null) continue;   // a path that no longer exists on this type is skipped (forward-compat)
                SetLeaf(prop, leaf);
            }
        }

        // Navigate a step-relative path ("outcomes.Array.data[0].nextGuid"), GROWING each array along the
        // way to fit the requested index, and return the leaf SerializedProperty (or null if a non-array
        // segment is missing on this type). Mirrors Unity's "<name>.Array.data[<n>]" path grammar.
        static SerializedProperty EnsureRelativeProperty(SerializedProperty element, string relativePath)
        {
            SerializedProperty current = element;
            string[] segments = relativePath.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];

                // "<name>.Array.data[<n>]" arrives as three tokens: "<name>", "Array", "data[<n>]".
                if (seg == "Array" && i + 1 < segments.Length && segments[i + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    if (!current.isArray) return null;
                    string idxToken = segments[i + 1];
                    int open = idxToken.IndexOf('[');
                    int close = idxToken.IndexOf(']');
                    if (open < 0 || close <= open) return null;
                    if (!int.TryParse(idxToken.Substring(open + 1, close - open - 1),
                                      NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        return null;

                    if (idx < 0) return null;
                    if (current.arraySize <= idx) current.arraySize = idx + 1;
                    current = current.GetArrayElementAtIndex(idx);
                    i++;        // consumed "data[<n>]" too
                    continue;
                }

                current = current.FindPropertyRelative(seg);
                if (current == null) return null;
            }
            return current;
        }

        static void SetLeaf(SerializedProperty prop, LeafDto leaf)
        {
            switch (leaf.kind)
            {
                case "String":
                    if (prop.propertyType == SerializedPropertyType.String)
                        prop.stringValue = leaf.value ?? "";
                    break;
                case "Integer":
                    if (prop.propertyType == SerializedPropertyType.Integer
                        && long.TryParse(leaf.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        prop.longValue = l;
                    break;
                case "Boolean":
                    if (prop.propertyType == SerializedPropertyType.Boolean)
                        prop.boolValue = string.Equals(leaf.value, "true", StringComparison.Ordinal);
                    break;
                case "Float":
                    if (prop.propertyType == SerializedPropertyType.Float
                        && double.TryParse(leaf.value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        prop.doubleValue = d;
                    break;
                case "Enum":
                    if (prop.propertyType == SerializedPropertyType.Enum
                        && int.TryParse(leaf.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int e))
                        prop.enumValueIndex = e;
                    break;
            }
        }

        // ---- CLR short-name -> Type resolution (cached) --------------------------------------

        static Dictionary<string, Type> _stepTypesByShortName;

        static Type ResolveStepType(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return null;
            if (_stepTypesByShortName == null) _stepTypesByShortName = BuildStepTypeMap();
            return _stepTypesByShortName.TryGetValue(shortName, out var t) ? t : null;
        }

        // All concrete (non-abstract) Step subclasses in the SAME assembly as Step (Pitech.XR.Scenario),
        // keyed by their CLR short name - the discriminator the exporter writes. Building from
        // typeof(Step).Assembly keeps it tied to the runtime model and picks up any future step type
        // with no edit here.
        static Dictionary<string, Type> BuildStepTypeMap()
        {
            var map = new Dictionary<string, Type>(StringComparer.Ordinal);
            Type stepBase = typeof(Step);
            foreach (var t in stepBase.Assembly.GetTypes())
            {
                if (t.IsAbstract) continue;
                if (!stepBase.IsAssignableFrom(t)) continue;
                map[t.Name] = t;   // t.Name is the bare CLR short name (e.g. "MiniQuizStep")
            }
            return map;
        }
    }
}
#endif
