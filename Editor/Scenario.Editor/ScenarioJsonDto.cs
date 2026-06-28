#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Portable, polymorphism-safe DTO for a <see cref="Scenario"/>'s step graph - the wire shape used
    /// by <see cref="ScenarioJsonExporter"/> / <see cref="ScenarioJsonImporter"/> for the portal /
    /// authoring surface. Unity <c>JsonUtility</c> cannot serialize the runtime model directly:
    /// <c>Scenario.steps</c> is a <c>[SerializeReference] List&lt;Step&gt;</c> with 13 concrete
    /// subclasses (and <c>GroupStep.steps</c> nests more polymorphic steps), and JsonUtility drops the
    /// concrete type of a managed reference. This DTO sidesteps that by carrying an explicit type
    /// discriminator per step plus a flat, path-keyed list of portable leaf values - so it round-trips
    /// every step type, present and future, with no per-type code.
    ///
    /// FLAT BY DESIGN (no recursive nesting): every step in the graph - root steps AND every nested
    /// <see cref="GroupStep"/> child, at any depth - is a single entry in the one flat <see cref="steps"/>
    /// list. Structure is carried by INDEX references: <see cref="rootIndices"/> lists the top-level
    /// steps (indices into <see cref="steps"/>, in authored order) and each group carries
    /// <see cref="StepDto.childIndices"/> for its children. This deliberately avoids a self-referential
    /// <c>StepDto.children</c> field, which JsonUtility refuses to serialize past its hard depth-10
    /// nesting cap ("Serialization depth limit 10 exceeded ... use [SerializeReference]"). A flat list
    /// has constant nesting depth, so it serializes regardless of how deep the group hierarchy goes.
    ///
    /// SCOPE - the PORTABLE AUTHORING SURFACE only (mirrors the type-agnostic walk in
    /// <see cref="ScenarioGraphSnapshot"/>):
    ///   IN  - the CLR short type name (the RATIFIED discriminator, e.g. <c>MiniQuizStep</c>, derived
    ///         exactly like <c>ScenarioGraphSnapshot.ShortTypeName</c>); <c>guid</c>; <c>graphPos</c>;
    ///         ALL routing/flow references (the <c>specificStepGuid</c> / <c>nextGuid</c> /
    ///         <c>*NextGuid</c> / <c>childRequirements[].guid</c> convention - identical to
    ///         <c>ScenarioGraphSnapshot.IsRoutingField</c>); and every serializable scalar / string /
    ///         enum / primitive leaf plus plain-data lists (e.g. <c>StatEffect</c> {key, op, value}).
    ///   OUT - <c>UnityEngine.Object</c> references (Button, PlayableDirector, panels, QuizAsset,
    ///         GameObject, Transform, Collider, Animator, SelectionLists, Component) and
    ///         <c>UnityEvent</c> persistent-call lists (onEnter / onSelected / onCorrect / onWrong).
    ///         These are scene/asset-bound: they cannot portably round-trip into a FRESH scenario, so
    ///         they are deliberately not emitted and are left at their C# defaults on import. The
    ///         exporter walk skips <c>ObjectReference</c> leaves and anything under
    ///         <c>m_PersistentCalls</c> for exactly this reason.
    ///
    /// KNOWN LIMITATION: null managed-reference slots in a steps array are skipped on export, so the
    /// rebuilt list is COMPACTED. A scenario that intentionally holds null step slots would have its
    /// later-sibling routing paths shift on import (data[2] -> data[1]). Authored launch labs do not
    /// carry null step slots; if the portal ever needs to preserve them, the wire shape must switch to
    /// preserving original indices (or emitting null placeholders) with matching importer handling.
    ///
    /// Leaves are keyed by the <see cref="SerializedProperty"/> path RELATIVE to the step element (e.g.
    /// <c>nextGuid</c>, <c>outcomes.Array.data[0].minCorrect</c>, <c>childRequirements.Array.data[1].guid</c>).
    /// Relative paths make a step self-describing regardless of its index in the list, and the importer
    /// replays them onto a freshly created instance of the same concrete type. Routing leaves are kept in
    /// a separate list from plain scalars only for clarity and auditability; both are replayed the same
    /// way.
    /// </summary>
    [Serializable]
    public sealed class ScenarioDto
    {
        /// <summary>DTO schema version. Bump when the wire shape changes incompatibly. Starts at 1.</summary>
        public int schemaVersion = ScenarioJsonExporter.SchemaVersion;

        /// <summary>Optional human-friendly scenario title (the Scenario.title field), portable text.</summary>
        public string title = "";

        /// <summary>EVERY step in the graph, flat - root steps and all nested <see cref="GroupStep"/>
        /// children at any depth. Order is the export walk order (pre-order); structure is carried by
        /// <see cref="rootIndices"/> + <see cref="StepDto.childIndices"/>, never by nesting these objects.</summary>
        public List<StepDto> steps = new List<StepDto>();

        /// <summary>Indices into <see cref="steps"/> of the TOP-LEVEL steps (the scenario's own
        /// <c>steps</c> list), in authored order.</summary>
        public List<int> rootIndices = new List<int>();
    }

    /// <summary>One step in the portable graph: a CLR short type discriminator plus its portable leaves.</summary>
    [Serializable]
    public sealed class StepDto
    {
        /// <summary>CLR short type name discriminator (e.g. <c>EventStep</c>, <c>MiniQuizStep</c>) - the
        /// bare class name, NOT <c>Step.Kind</c>. Matches <c>ScenarioGraphSnapshot.ShortTypeName</c>.</summary>
        public string type = "";

        /// <summary>The step's own identity guid (<c>Step.guid</c>).</summary>
        public string guid = "";

        /// <summary>Graph node X position (<c>Step.graphPos.x</c>).</summary>
        public float graphPosX;

        /// <summary>Graph node Y position (<c>Step.graphPos.y</c>).</summary>
        public float graphPosY;

        /// <summary>All routing/flow references on this step (and on its plain-data sub-lists like
        /// choices / outcomes / childRequirements / multiConditionBranches), keyed by step-relative
        /// SerializedProperty path. Determined by the same convention as
        /// <c>ScenarioGraphSnapshot.IsRoutingField</c>.</summary>
        public List<LeafDto> routes = new List<LeafDto>();

        /// <summary>Portable scalar / string / enum / primitive leaves (and plain-data list elements),
        /// keyed by step-relative SerializedProperty path. Excludes routing leaves (kept in
        /// <see cref="routes"/>), object references and UnityEvent persistent calls (out of scope).</summary>
        public List<LeafDto> scalars = new List<LeafDto>();

        /// <summary>When this step is a <see cref="GroupStep"/>: the indices into
        /// <see cref="ScenarioDto.steps"/> of its children (<c>GroupStep.steps</c>), in authored order.
        /// Empty for non-group steps. Replaces a recursive child list so the DTO stays flat.</summary>
        public List<int> childIndices = new List<int>();
    }

    /// <summary>A single portable leaf: its step-relative SerializedProperty path, the value type tag,
    /// and the value rendered as an invariant-culture string. Strings/enums/ints/bools/floats only - the
    /// portable scalar set. Object references and UnityEvent calls are never emitted as leaves.</summary>
    [Serializable]
    public sealed class LeafDto
    {
        /// <summary>SerializedProperty path RELATIVE to the owning step element (e.g. <c>nextGuid</c>,
        /// <c>outcomes.Array.data[0].nextGuid</c>).</summary>
        public string path = "";

        /// <summary>Value-type tag: one of <c>String</c>, <c>Integer</c>, <c>Boolean</c>, <c>Float</c>,
        /// <c>Enum</c> (the names of the corresponding <see cref="SerializedPropertyType"/> members the
        /// exporter recognizes). Drives how <see cref="value"/> is parsed back on import.</summary>
        public string kind = "";

        /// <summary>The leaf value as an invariant-culture string: raw text for String; decimal integer
        /// for Integer / Enum (the enum's serialized index); <c>true</c>/<c>false</c> for Boolean;
        /// round-trip ("R") form for Float.</summary>
        public string value = "";
    }
}
#endif
