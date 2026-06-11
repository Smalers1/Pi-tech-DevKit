# Subsystem notes — intentional serialization & reflection exceptions

These are deliberate, load-bearing design choices in the DevKit. They can read like smells but are
correct; do **not** "clean them up" without reading this. (WS A7 Step 5.)

## 1. The public serialized-field surface (Scenario data model)
`Scenario.steps` and the `Step` subclasses use `[SerializeReference]` + `[Serializable]` with **public
fields**. Lab scenes and prefabs bind to these by field name and by MonoScript GUID. Renaming or retyping a
serialized public field, or moving a `[SerializeReference]` type's namespace/assembly, **silently breaks
shipped labs** — which is why Proof B (public API additions-only) and Proof C (GUID + serialized-diff)
exist. `[FormerlySerializedAs]` on `SceneManager.defaultQuiz` / `quizPanel` / `quizResultsPanel` preserves
older lab bindings; keep them.

## 2. `Scenario.OnValidate` — no-null-strip + `isCompiling` guard
`OnValidate` deliberately does **not** remove null entries from the `[SerializeReference] steps` lists, and
returns early while `EditorApplication.isCompiling`. Unity reports transient null slots during prefab
import, Apply, and domain reload before managed references deserialize; stripping them would mark the asset
dirty and **permanently delete the step graph**. This guard is load-bearing — never remove it. (The
inspector's explicit "Clear Nulls" is the only sanctioned null removal.)

## 3. Editor-only `FindObjectsOfType` legitimacy
The editor services and tools use `FindObjectsOfType` / `Resources.FindObjectsOfTypeAll` to discover scene
components (cockpit observation, repair tools). This is editor-only convenience and is acceptable. The
**runtime** reflection / `Find*` in ContentDelivery is a separate concern, slated for the
`ISceneRunnerControl` swap in Phase D — do not conflate the two.

## 4. Core.Editor resolves types by string (`FullName` / `Name`)
`Pitech.XR.Core.Editor` resolves Quiz/Stats/Scenario types by hard-coded string (e.g.
`"Pitech.XR.Scenario.SceneManager"`), and resolves `ScenarioGraphWindow` by **simple type name**. This keeps
the editor assembly free of hard references to those modules, but it is an **invisible contract the compiler
cannot see** — a namespace/assembly move silently breaks it. Proof B's literal-resolution test
(`CoreEditorTypeLiteralTests`) pins every one of these literals, with `ScenarioGraphWindow` checked by name
so the WS A6 namespace wrap stays green.

## 5. `ISceneRunnerControl` (WS A8) is a forwarding seam only
`SceneManager` implements `Pitech.XR.Core.ISceneRunnerControl` via members that forward to existing
state (`CurrentStepIndex`→`StepIndex`, `AutoStart`→`autoStart`, `Restart`→`Restart`). No new behaviour, no
field renamed. Do not widen the interface (Phase D extracts the runner behind it; Phase E adds
`IScenarioFlowStore` beneath it).

## 6. `link.xml` `preserve="all"` on six assemblies
IL2CPP stripping is disabled (`preserve="all"`) on the serialized / reflection-heavy assemblies. It is safe
today. **Do not narrow it without first enumerating every reflection-instantiated and `[SerializeReference]`
type** — the runner instantiates `Step` subclasses by managed-reference type string, which stripping would
break.
