using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Networking;
using Pitech.XR.Scenario;

namespace Pitech.XR.Networking.Editor
{
    // ---------- Dropdown inspectors for the graduated state components (WS B2.7 S5) ----------
    // The stateID field is picked from the lab's DECLARED bool states (a popup), so authors don't hand-
    // type ids and typos can't silently break a trigger/listener. The declared states are the Bool
    // ConsoleParameters on the nearest LabConsole (the ex-switchboard states graduate to params). When no
    // LabConsole / no bool params are found, the field falls back to a free-text box.

    static class StateIdDropdown
    {
        /// <summary>Collect the bool parameter ids declared on the nearest LabConsole, if any.</summary>
        public static List<string> CollectBoolStateIds(Component from)
        {
            var ids = new List<string>();
            if (from == null) return ids;

            LabConsole console = from.GetComponentInParent<LabConsole>(true);
            if (console == null) return ids;

            var so = new SerializedObject(console);
            SerializedProperty list = so.FindProperty("parameters");
            if (list == null || !list.isArray) return ids;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                if (p == null) continue;
                SerializedProperty idProp = p.FindPropertyRelative("id");
                SerializedProperty typeProp = p.FindPropertyRelative("type");
                if (idProp == null || typeProp == null) continue;
                // ParamType.Bool is the first enum member (index 0).
                if (typeProp.enumValueIndex != 0) continue;
                if (!string.IsNullOrEmpty(idProp.stringValue)) ids.Add(idProp.stringValue);
            }
            return ids;
        }

        /// <summary>Draw a state-id field as a popup of declared bool states, or free text if none.</summary>
        public static void Draw(SerializedProperty stateIdProp, Component owner, string label = "State ID")
        {
            List<string> ids = CollectBoolStateIds(owner);
            if (ids.Count == 0)
            {
                EditorGUILayout.PropertyField(stateIdProp, new GUIContent(label));
                return;
            }

            string current = stateIdProp.stringValue;
            int index = ids.IndexOf(current);

            // Offer the declared states + a "(custom)" escape that keeps the typed value.
            var options = new List<string>(ids) { "(custom...)" };
            int shown = index >= 0 ? index : options.Count - 1;

            int picked = EditorGUILayout.Popup(label, shown, options.ToArray());
            if (picked < ids.Count)
            {
                stateIdProp.stringValue = ids[picked];
            }
            else
            {
                // custom: let the author type a value not in the declared list
                stateIdProp.stringValue = EditorGUILayout.TextField(" ", current);
            }
        }
    }

    [CustomEditor(typeof(PhysicsStateTrigger))]
    public sealed class PhysicsStateTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StateIdDropdown.Draw(serializedObject.FindProperty("stateID"), (Component)target);
            DrawPropertiesExcluding(serializedObject, "m_Script", "stateID");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(UIStateTrigger))]
    public sealed class UIStateTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StateIdDropdown.Draw(serializedObject.FindProperty("stateID"), (Component)target);
            DrawPropertiesExcluding(serializedObject, "m_Script", "stateID");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(EventStateListener))]
    public sealed class EventStateListenerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            StateIdDropdown.Draw(serializedObject.FindProperty("stateID"), (Component)target);
            DrawPropertiesExcluding(serializedObject, "m_Script", "stateID");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
