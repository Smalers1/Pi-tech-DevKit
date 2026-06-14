using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // -------- ConditionsStep --------
    public enum CompareOp { Less, LessOrEqual, Greater, GreaterOrEqual, Equal, NotEqual, IsTrue, IsFalse }

    public enum ConditionValueSource { Stat, Component, ListByLabel }

    /// <summary>One outcome route: when value matches this comparison, go to nextGuid.</summary>
    [Serializable]
    public class ConditionOutcome
    {
        [Tooltip("Optional label for graph port (e.g. \"Low\", \"OK\")")]
        public string label;

        public CompareOp compareOp = CompareOp.Less;
        public float compareValue;

        [Tooltip("Next step (GUID) when this outcome matches. Empty = next item in list")]
        public string nextGuid = "";
    }

    [Serializable]
    public sealed class ConditionsStep : Step
    {
        [Header("What to check (one value)")]
        [Tooltip("Where we read one number to compare.\n\n" +
            "• Stat — use the scenario stats system; set Stat Key below.\n" +
            "• Component — read one public field or property on a script (e.g. a score).\n" +
            "• List by label — your data is in a list of rows: we find the row by name, then read a number from that row.")]
        public ConditionValueSource valueSource = ConditionValueSource.Component;

        [Tooltip("Only when Value Source is Stat. The name of the stat to read (same spelling as elsewhere in your project), e.g. Health or Money.")]
        public string statKey = "Health";

        [Tooltip("When Value Source is Component or List by label: drag the GameObject, then pick the script component that holds the value or the list.")]
        public Component source;

        [Tooltip("Only when Value Source is Component. Type the exact name of one public field or property on that script (e.g. score). It must be a single number (float, int, or bool), not a list.")]
        public string memberName;

        [Tooltip("Only when Value Source is List by label — fill this in. Type the exact name of the public field or property on the script that holds the list or array (e.g. counters).")]
        public string listFieldName;

        [Tooltip("Only when Value Source is List by label — fill this in. The text we look for: we use the row whose label matches this string exactly (e.g. EmergencyCount).")]
        public string listEntryLabel;

        [Tooltip("Only when Value Source is List by label. On each row object, the name of the text field we match against Match Label. Leave as label if your row class uses a field called label; change only if it uses another name.")]
        public string listLabelFieldName = "label";

        [Tooltip("Only when Value Source is List by label. On each row object, the name of the number field we read for the condition. Leave as count if your row class uses a field called count; change only if it uses another name.")]
        public string listValueFieldName = "count";

        [Header("Outcomes (checked in order, first match wins)")]
        [Tooltip("Add multiple outcomes: if value &lt; 50 → A, if value &lt; 80 → B. Connect ports in graph for routing.")]
        public List<ConditionOutcome> outcomes = new();

        public override string Kind => "Conditions";
    }

    /// <summary>Testable evaluation logic for Conditions steps.</summary>
    public static class ConditionsEvaluator
    {
        public static bool EvalCompare(float value, CompareOp op, float compareValue)
        {
            switch (op)
            {
                case CompareOp.Less: return value < compareValue;
                case CompareOp.LessOrEqual: return value <= compareValue;
                case CompareOp.Greater: return value > compareValue;
                case CompareOp.GreaterOrEqual: return value >= compareValue;
                case CompareOp.Equal: return UnityEngine.Mathf.Approximately(value, compareValue);
                case CompareOp.NotEqual: return !UnityEngine.Mathf.Approximately(value, compareValue);
                case CompareOp.IsTrue: return value > 0.5f;
                case CompareOp.IsFalse: return value < 0.5f;
                default: return false;
            }
        }
    }
}
