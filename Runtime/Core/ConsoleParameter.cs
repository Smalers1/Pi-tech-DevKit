using System;
using UnityEngine;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The edit-time declaration of a parameter (map sec-8): id, type, default, range, scope. The
    /// typed superset of a legacy <c>StatsConfig.Entry</c> (key + default + min/max), adding a
    /// <see cref="type"/>, a string default, and a replication <see cref="scope"/>. Declared/listed/
    /// edited in the LabConsole window. Authored serialized data; LIVE as of WS B1.2 Step 4 (seeds the
    /// runtime param store on LabConsole, which now drives effects/conditions).
    ///
    /// <see cref="min"/>/<see cref="max"/> are ENFORCED (clamp on write) whenever a real range is
    /// declared (max &gt; min) - an upgrade over Stats' display-only range, so migration must verify no
    /// existing lab relied on an out-of-range value (map sec-8). A param with no range is unbounded.
    /// </summary>
    [Serializable]
    public sealed class ConsoleParameter
    {
        [Tooltip("Stable id for this parameter (referenced by effects, conditions, bindings, and the JSON contract). Must be unique.")]
        public string id;

        [Tooltip("The value kind this parameter holds.")]
        public ParamType type = ParamType.Float;

        [Tooltip("Initial numeric value (bool 0/1, int, enum index, or float). Ignored for String.")]
        public float defaultNumber;

        [Tooltip("Initial string value. Used only when Type is String.")]
        public string defaultText = "";

        [Tooltip("Minimum allowed value (Float/Int). Enforced on write when a real range is declared (Max > Min).")]
        public float min;

        [Tooltip("Maximum allowed value (Float/Int). Enforced on write when a real range is declared (Max > Min).")]
        public float max;

        [Tooltip("Local = per-client. Networked = replicated across peers (auto-degrades to Local with no Fusion / single-player).")]
        public ParamScope scope = ParamScope.Local;

        /// <summary>The declared default as a <see cref="ParamValue"/> (clamped for number kinds).</summary>
        public ParamValue DefaultValue()
        {
            if (type == ParamType.String) return ParamValue.Str(defaultText);
            return Clamp(new ParamValue(type, defaultNumber, null));
        }

        /// <summary>Clamp a value into this declaration's range. No-op unless a real range is declared
        /// (max &gt; min) and the value is a Float or Int.</summary>
        public ParamValue Clamp(in ParamValue value)
        {
            if (max <= min) return value;   // no range declared -> unbounded
            if (value.Type != ParamType.Float && value.Type != ParamType.Int) return value;
            float n = Mathf.Clamp(value.Number, min, max);
            return new ParamValue(value.Type, n, value.Text);
        }
    }
}
