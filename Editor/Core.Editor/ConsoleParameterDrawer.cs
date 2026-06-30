using UnityEditor;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Core.Editor
{
    // ---------- Visual params editor (WS B2.7 S1, CAN_TRAIL) ----------
    // A property drawer for ConsoleParameter so the LabConsole "parameters" list (and anywhere else a
    // ConsoleParameter is authored) renders as a clean, type-aware row instead of the raw struct: id +
    // type, then ONLY the relevant default (a Bool toggle / Int / Float / Enum index / String), min/max
    // only for numeric kinds, scope, and an inline validation hint (empty id, or max <= min on a numeric
    // range). Additive: Unity's default list UI still gives add/remove; this just improves each element.
    // (Play-mode live values - S2 - are a noted follow-up: reading the runtime store across the asmdef
    // boundary needs a small public LabConsole accessor; deferred as polish.)

    [CustomPropertyDrawer(typeof(ConsoleParameter))]
    public sealed class ConsoleParameterDrawer : PropertyDrawer
    {
        const float Pad = 2f;

        static float Line => EditorGUIUtility.singleLineHeight + Pad;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = 3;   // id, type, scope
            ParamType t = (ParamType)property.FindPropertyRelative("type").enumValueIndex;
            lines += 1;      // default (one row)
            if (t == ParamType.Int || t == ParamType.Float) lines += 2;   // min, max
            float h = lines * Line;

            if (HasValidationMessage(property, t, out _)) h += Line * 1.5f;
            return h + Pad;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty id = property.FindPropertyRelative("id");
            SerializedProperty type = property.FindPropertyRelative("type");
            SerializedProperty defNum = property.FindPropertyRelative("defaultNumber");
            SerializedProperty defText = property.FindPropertyRelative("defaultText");
            SerializedProperty min = property.FindPropertyRelative("min");
            SerializedProperty max = property.FindPropertyRelative("max");
            SerializedProperty scope = property.FindPropertyRelative("scope");

            Rect r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(r, id, new GUIContent("Id"));
            r.y += Line;
            EditorGUI.PropertyField(r, type, new GUIContent("Type"));
            r.y += Line;

            var t = (ParamType)type.enumValueIndex;
            switch (t)
            {
                case ParamType.Bool:
                    bool b = defNum.floatValue != 0f;
                    b = EditorGUI.Toggle(r, "Default", b);
                    defNum.floatValue = b ? 1f : 0f;
                    break;
                case ParamType.Int:
                    defNum.floatValue = EditorGUI.IntField(r, "Default", Mathf.RoundToInt(defNum.floatValue));
                    break;
                case ParamType.Float:
                    defNum.floatValue = EditorGUI.FloatField(r, "Default", defNum.floatValue);
                    break;
                case ParamType.Enum:
                    defNum.floatValue = EditorGUI.IntField(r, "Default (enum index)", Mathf.RoundToInt(defNum.floatValue));
                    break;
                case ParamType.String:
                    EditorGUI.PropertyField(r, defText, new GUIContent("Default"));
                    break;
            }
            r.y += Line;

            if (t == ParamType.Int || t == ParamType.Float)
            {
                EditorGUI.PropertyField(r, min, new GUIContent("Min"));
                r.y += Line;
                EditorGUI.PropertyField(r, max, new GUIContent("Max"));
                r.y += Line;
            }

            EditorGUI.PropertyField(r, scope, new GUIContent("Scope"));
            r.y += Line;

            if (HasValidationMessage(property, t, out string msg))
            {
                Rect help = new Rect(position.x, r.y, position.width, Line * 1.4f);
                EditorGUI.HelpBox(help, msg, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        static bool HasValidationMessage(SerializedProperty property, ParamType t, out string msg)
        {
            msg = null;
            string id = property.FindPropertyRelative("id").stringValue;
            if (string.IsNullOrWhiteSpace(id))
            {
                msg = "Id is required (referenced by effects / conditions / bindings).";
                return true;
            }
            if (t == ParamType.Int || t == ParamType.Float)
            {
                float min = property.FindPropertyRelative("min").floatValue;
                float max = property.FindPropertyRelative("max").floatValue;
                if (!(min == 0f && max == 0f) && max <= min)
                {
                    msg = "Max must be greater than Min for the range to be enforced (else it is unbounded).";
                    return true;
                }
            }
            return false;
        }
    }
}
