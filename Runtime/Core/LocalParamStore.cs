using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The Local (single-client) <see cref="IParamStore"/> impl (map sec-8): plain dictionaries, no
    /// replication. A Networked declaration auto-degrades to this in a no-Fusion / single-player
    /// session, so a co-op lab and its AR/single-player build are the same asset. The Networked impl
    /// (one <c>[Networked] NetworkDictionary</c>) lives in Pitech.XR.Networking under
    /// <c>#if PITECH_HAS_FUSION</c>. Main-thread only. LIVE as of WS B1.2 Step 4 (LabConsole owns one and
    /// the runner writes effects/quiz/conditions to it; the Networked impl is the deferred Fusion pass).
    /// </summary>
    public sealed class LocalParamStore : IParamStore
    {
        readonly Dictionary<string, ConsoleParameter> _declarations = new Dictionary<string, ConsoleParameter>(StringComparer.Ordinal);
        readonly Dictionary<string, ParamValue> _values = new Dictionary<string, ParamValue>(StringComparer.Ordinal);

        public event Action<string> ParamChanged;

        public void Declare(ConsoleParameter declaration)
        {
            if (declaration == null) return;
            string id = NormKey(declaration.id);
            if (id.Length == 0) return;
            _declarations[id] = declaration;
            // Seed the default only if the parameter has no value yet (re-declaring never clobbers
            // live state).
            if (!_values.ContainsKey(id))
                _values[id] = declaration.DefaultValue();
        }

        public bool IsDeclared(string id) => _declarations.ContainsKey(NormKey(id));

        public bool TryGet(string id, out ParamValue value)
        {
            if (_values.TryGetValue(NormKey(id), out value)) return true;
            value = default;
            return false;
        }

        public void Set(string id, in ParamValue value)
        {
            id = NormKey(id);
            if (id.Length == 0) return;
            ParamValue toStore = _declarations.TryGetValue(id, out var decl) ? decl.Clamp(value) : value;
            // Change-only semantics: an unchanged write to an EXISTING key neither stores nor fans out, so
            // analytics/bus listeners only see real changes. This mirrors the legacy StatsRuntime no-op
            // suppression for keys that already hold a value. NOTE: unlike legacy, a FIRST write to an
            // absent key stores+fires even when the value is the type's zero (legacy suppressed that) -
            // trace-neutral today; revisit for exact parity if the B.2 lossless ParamChanged bus needs it.
            if (_values.TryGetValue(id, out var prev) && SameValue(prev, toStore)) return;
            _values[id] = toStore;
            ParamChanged?.Invoke(id);
        }

        public void Apply(string id, ParamOp op, float operand)
        {
            id = NormKey(id);
            if (id.Length == 0) return;
            ParamType type = ParamType.Float;
            float current = 0f;
            if (_values.TryGetValue(id, out var existing))
            {
                current = existing.Number;
                if (existing.Type != ParamType.String) type = existing.Type;
            }
            Set(id, new ParamValue(type, ApplyOp(current, op, operand), null));
        }

        public bool GetBool(string id, bool fallback = false) => TryGet(id, out var v) ? v.AsBool() : fallback;
        public int GetInt(string id, int fallback = 0) => TryGet(id, out var v) ? v.AsInt() : fallback;
        public float GetFloat(string id, float fallback = 0f) => TryGet(id, out var v) ? v.AsFloat() : fallback;
        public string GetString(string id, string fallback = "") => TryGet(id, out var v) ? v.AsString() : fallback;

        public T GetEnum<T>(string id, T fallback = default) where T : struct, Enum
            => TryGet(id, out var v) ? (T)Enum.ToObject(typeof(T), v.AsInt()) : fallback;

        public void SetBool(string id, bool value) => Set(id, ParamValue.Bool(value));
        public void SetInt(string id, int value) => Set(id, ParamValue.Int(value));
        public void SetFloat(string id, float value) => Set(id, ParamValue.Float(value));
        public void SetString(string id, string value) => Set(id, ParamValue.Str(value));
        public void SetEnum<T>(string id, T value) where T : struct, Enum => Set(id, ParamValue.Enum(Convert.ToInt32(value)));

        // Key normalization: trim so a declared id and every runtime access resolve to the SAME slot.
        // The runner already trims each effect/quiz/condition key via StatsConfig.NormalizeKey; Core
        // cannot reference Pitech.XR.Stats, so the identical trim is inlined here - this store is the
        // single source of truth for its own keying. Behaviour-neutral for existing labs (their keys are
        // already trimmed -> Trim is a no-op identity); closes the whitespace-id footgun for a
        // hand-authored ConsoleParameter id with stray leading/trailing whitespace.
        static string NormKey(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        // Mirrors the legacy StatEffect.Apply 1:1 (Divide-by-zero is a no-op, as in Stats).
        static float ApplyOp(float current, ParamOp op, float operand)
        {
            switch (op)
            {
                case ParamOp.Add: return current + operand;
                case ParamOp.Subtract: return current - operand;
                case ParamOp.Multiply: return current * operand;
                case ParamOp.Divide: return Mathf.Approximately(operand, 0f) ? current : current / operand;
                case ParamOp.Set: return operand;
                default: return current;
            }
        }

        // No-op detector for change-only semantics: same type AND same payload (Approximately for the
        // numeric slot, ordinal compare for strings).
        static bool SameValue(in ParamValue a, in ParamValue b)
        {
            if (a.Type != b.Type) return false;
            return a.Type == ParamType.String
                ? string.Equals(a.AsString(), b.AsString(), StringComparison.Ordinal)
                : Mathf.Approximately(a.Number, b.Number);
        }
    }
}
