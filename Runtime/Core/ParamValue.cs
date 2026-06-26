using UnityEngine;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The typed value union the param store holds (map sec-8): a small struct where bool/int/enum/
    /// float pack into <see cref="Number"/>, string into <see cref="Text"/>, and <see cref="Type"/>
    /// says how to read it. A single union is why one store (and one Fusion NetworkDictionary, on the
    /// networked side) can hold every type. Immutable, allocation-free, passed by <c>in</c>.
    ///
    /// NOTE on the networked side: the Fusion-replicated variant is a SEPARATE unmanaged
    /// <c>INetworkStruct</c> (with <c>NetworkString&lt;_64&gt;</c>) authored in Pitech.XR.Networking
    /// under <c>#if PITECH_HAS_FUSION</c> - this managed struct backs the Local store. The two carry
    /// the same fields; the split is because Fusion value types must be unmanaged.
    /// </summary>
    public readonly struct ParamValue
    {
        /// <summary>How to read this value.</summary>
        public readonly ParamType Type;

        /// <summary>Numeric slot: bool (0/1), int, enum index, or float. 0 for a string value.</summary>
        public readonly float Number;

        /// <summary>Text slot: the string payload, or null for non-string values.</summary>
        public readonly string Text;

        public ParamValue(ParamType type, float number, string text)
        {
            Type = type;
            Number = number;
            Text = text;
        }

        public static ParamValue Bool(bool value) => new ParamValue(ParamType.Bool, value ? 1f : 0f, null);
        public static ParamValue Int(int value) => new ParamValue(ParamType.Int, value, null);
        public static ParamValue Float(float value) => new ParamValue(ParamType.Float, value, null);
        public static ParamValue Enum(int index) => new ParamValue(ParamType.Enum, index, null);
        public static ParamValue Str(string value) => new ParamValue(ParamType.String, 0f, value ?? string.Empty);

        public bool AsBool() => Number != 0f;
        public int AsInt() => Mathf.RoundToInt(Number);
        public float AsFloat() => Number;
        public string AsString() => Text ?? string.Empty;
    }
}
