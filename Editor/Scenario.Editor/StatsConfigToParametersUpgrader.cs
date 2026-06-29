#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Stats;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// WS B1.2 Step 4 (map sec-8): one-time migration of a lab's legacy <see cref="StatsConfig"/>
    /// declarations into the <see cref="LabConsole"/>'s typed <c>parameters</c> list (the Stats
    /// successor). The runtime already FALLS BACK to <see cref="StatsConfig"/> when a LabConsole has no
    /// declared parameters, so this upgrader is not required for a lab to run - it permanently moves the
    /// declarations onto the console so the StatsConfig asset can eventually be retired and so the JSON
    /// contract / multiplayer validators see the parameters.
    ///
    /// Writes through <see cref="SerializedObject"/>/<see cref="SerializedProperty"/> so it needs no
    /// reference to the (private, <c>[SerializeField]</c>) field nor to <c>Pitech.XR.Core</c> - the
    /// element sub-properties are set by name. Each entry maps to a Float, Local-scope
    /// <c>ConsoleParameter</c> carrying the StatsConfig key/default/min/max (so the now-ENFORCED clamp
    /// uses the same range the config declared). Idempotent: an id already present is skipped, so
    /// re-running never duplicates or clobbers authored parameters. Undoable.
    /// </summary>
    public static class StatsConfigToParametersUpgrader
    {
        const string Menu = "Pi tech/Stats/Upgrade StatsConfig to Parameters";

        // ParamType {Bool=0, Int=1, Float=2, Enum=3, String=4} / ParamScope {Local=0, Networked=1}.
        const int ParamTypeFloat = 2;
        const int ParamScopeLocal = 0;

        [MenuItem(Menu)]
        static void Run()
        {
            var consoles = CollectTargets();
            if (consoles.Count == 0)
            {
                EditorUtility.DisplayDialog("Upgrade StatsConfig",
                    "No LabConsole found in the selection or the open scene.", "OK");
                return;
            }

            int migratedConsoles = 0, addedParams = 0;
            foreach (var lc in consoles)
            {
                int added = UpgradeOne(lc);
                if (added > 0) { migratedConsoles++; addedParams += added; }
            }

            EditorUtility.DisplayDialog("Upgrade StatsConfig",
                migratedConsoles == 0
                    ? "Nothing to migrate: no StatsConfig entries to copy, or every key is already declared as a parameter."
                    : $"Migrated {addedParams} parameter(s) across {migratedConsoles} LabConsole(s).\n\n" +
                      "Review each LabConsole's 'parameters' list. The legacy StatsConfig can be retired once verified " +
                      "(the runtime prefers 'parameters' and only falls back to StatsConfig when empty).",
                "OK");
        }

        // Selection (incl. children, for prefab roots) first; otherwise every LabConsole in the open scene.
        static List<LabConsole> CollectTargets()
        {
            var set = new List<LabConsole>();
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                foreach (var lc in go.GetComponentsInChildren<LabConsole>(true))
                    if (lc != null && !set.Contains(lc)) set.Add(lc);
            }
            if (set.Count > 0) return set;

#if UNITY_2023_1_OR_NEWER
            var all = UnityengineFindAll();
#else
            var all = UnityEngine.Object.FindObjectsOfType<LabConsole>(true);
#endif
            foreach (var lc in all)
                if (lc != null && !set.Contains(lc)) set.Add(lc);
            return set;
        }

#if UNITY_2023_1_OR_NEWER
        static LabConsole[] UnityengineFindAll()
            => UnityEngine.Object.FindObjectsByType<LabConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif

        // Returns the number of parameters added to this console.
        static int UpgradeOne(LabConsole lc)
        {
            if (lc == null || lc.statsConfig == null) return 0;

            var so = new SerializedObject(lc);
            var paramsProp = so.FindProperty("parameters");
            if (paramsProp == null || !paramsProp.isArray) return 0;

            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < paramsProp.arraySize; i++)
            {
                var idProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (idProp != null && !string.IsNullOrEmpty(idProp.stringValue)) existing.Add(idProp.stringValue);
            }

            int added = 0;
            foreach (var kv in lc.statsConfig.All())
            {
                string id = kv.Key;   // already normalized (Trim) by StatsConfig
                if (string.IsNullOrEmpty(id) || existing.Contains(id)) continue;

                int idx = paramsProp.arraySize;
                paramsProp.InsertArrayElementAtIndex(idx);
                var el = paramsProp.GetArrayElementAtIndex(idx);
                SetString(el, "id", id);
                SetEnumIndex(el, "type", ParamTypeFloat);
                SetFloat(el, "defaultNumber", kv.Value.defaultValue);
                SetString(el, "defaultText", "");
                SetFloat(el, "min", kv.Value.min);
                SetFloat(el, "max", kv.Value.max);
                SetEnumIndex(el, "scope", ParamScopeLocal);
                existing.Add(id);
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedProperties();   // registers Undo + marks dirty (prefab-override correct)
                EditorUtility.SetDirty(lc);
            }
            return added;
        }

        static void SetString(SerializedProperty el, string rel, string v)
        {
            var p = el.FindPropertyRelative(rel);
            if (p != null) p.stringValue = v;
        }

        static void SetFloat(SerializedProperty el, string rel, float v)
        {
            var p = el.FindPropertyRelative(rel);
            if (p != null) p.floatValue = v;
        }

        static void SetEnumIndex(SerializedProperty el, string rel, int index)
        {
            var p = el.FindPropertyRelative(rel);
            if (p != null) p.enumValueIndex = index;
        }
    }
}
#endif
