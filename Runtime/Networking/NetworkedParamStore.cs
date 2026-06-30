#if PITECH_HAS_FUSION || FUSION_WEAVER
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// The networked (Fusion) <see cref="IParamStore"/> impl (map sec-8 / sec-10) - the B1.2 carry-over.
    /// The replicated twin of <c>LocalParamStore</c>: declared parameter VALUES live in a
    /// <c>[Networked]</c> dictionary replicated to every peer; relative ops (<see cref="ParamOp"/>) are
    /// applied AUTHORITY-ONLY and SEQUENCED (non-authority peers RPC the authority) so two near-
    /// simultaneous Add/Multiply can't race (map sec-8). Declarations are LOCAL (authored, identical on
    /// every peer); only values replicate. A Networked-scope param auto-degrades to <c>LocalParamStore</c>
    /// where Fusion is absent.
    ///
    /// Mirrors <c>NetworkedLabStateStore</c>'s proven shape: a <c>[Networked]</c> NetworkDictionary +
    /// Render-diff (raise ParamChanged once per real change) + an authority-gated RPC. Gated
    /// <c>#if PITECH_HAS_FUSION || FUSION_WEAVER</c>.
    ///
    /// SCOPE NOTE (B2.4): this is the replicated DATA PLANE. Routing LabConsole's Networked-scope params
    /// + the runner's effects through it (the "declared-param wiring") is the post-B2 on-device
    /// integration; string values clamp to 64 chars; provenance/actor tracking is a noted follow-up.
    /// </summary>
    [AddComponentMenu("Pi tech/Networking/Networked Param Store")]
    [DisallowMultipleComponent]
    public sealed class NetworkedParamStore : NetworkBehaviour, IParamStore
    {
        const int Cap = 128;

        // Authored declarations - local, identical on every peer (not replicated; only values are).
        readonly Dictionary<string, ConsoleParameter> _declarations = new Dictionary<string, ConsoleParameter>();

        // Replicated values, keyed by parameter id.
        [Networked, Capacity(Cap)]
        NetworkDictionary<NetworkString<_64>, NetworkParamValue> Values { get; }

        // Non-networked mirror so Render raises ParamChanged exactly once per real change.
        readonly Dictionary<string, ParamValue> _mirror = new Dictionary<string, ParamValue>();

        public event Action<string> ParamChanged;

        // True only once the NetworkObject is spawned + valid: BEFORE that, the [Networked] dictionary,
        // Object.HasStateAuthority and RPCs must NOT be touched (a UnityEvent / trigger that fires in
        // Awake/OnEnable, before Fusion spawns this, would otherwise throw or read invalid state). All
        // public writers/readers gate on this and fall back to the local mirror pre-spawn.
        bool Ready => Object != null && Object.IsValid;

        public override void Spawned()
        {
            // The authority seeds the replicated dict from the local mirror, which by now holds every
            // declared default (seeded in Declare) PLUS any value written before spawn (buffered by the
            // pre-spawn guard in Set/Apply). Flushing the mirror makes those early writes durable/replicated.
            if (!Object.HasStateAuthority) return;
            foreach (var kv in _mirror)
            {
                if (!Values.ContainsKey(kv.Key))
                    Values.Set(kv.Key, ToNet(kv.Value));
            }
        }

        public void Declare(ConsoleParameter declaration)
        {
            if (declaration == null || string.IsNullOrEmpty(declaration.id)) return;
            string id = Norm(declaration.id);
            _declarations[id] = declaration;
            // Seed the local mirror so reads before Spawned() still return the default.
            if (!_mirror.ContainsKey(id)) _mirror[id] = declaration.DefaultValue();
            // If already spawned with authority, seed the networked value now too.
            if (Ready && Object.HasStateAuthority && !Values.ContainsKey(id))
                Values.Set(id, ToNet(declaration.DefaultValue()));
        }

        public bool IsDeclared(string id) => !string.IsNullOrEmpty(id) && _declarations.ContainsKey(Norm(id));

        public bool TryGet(string id, out ParamValue value)
        {
            string key = Norm(id);
            if (string.IsNullOrEmpty(key)) { value = default; return false; }
            // Only read the [Networked] dictionary once spawned + valid; before that serve the local mirror
            // (which holds declared defaults + any pre-spawn writes).
            if (Ready && Values.TryGet(key, out NetworkParamValue nv))
            {
                value = FromNet(nv);
                return true;
            }
            if (_mirror.TryGetValue(key, out value)) return true;
            value = default;
            return false;
        }

        public void Set(string id, in ParamValue value)
        {
            string key = Norm(id);
            if (string.IsNullOrEmpty(key)) return;
            ParamValue clamped = Clamp(key, value);
            // Pre-spawn / invalid: buffer into the mirror so an early write isn't lost (the authority
            // flushes the mirror into the replicated dict in Spawned). Never touch Object/Values here.
            if (!Ready) { SetMirror(key, clamped); return; }
            if (Object.HasStateAuthority) Values.Set(key, ToNet(clamped));
            else RPC_RequestSet(key, (int)clamped.Type, clamped.Number, clamped.Text ?? string.Empty);
        }

        public void Apply(string id, ParamOp op, float operand)
        {
            string key = Norm(id);
            if (string.IsNullOrEmpty(key)) return;
            // Pre-spawn / invalid: resolve the relative op against the mirror (authority-sequencing resumes
            // once spawned). Sequenced on the authority so concurrent relative ops don't race.
            if (!Ready) { ApplyMirror(key, op, operand); return; }
            if (Object.HasStateAuthority) ApplyOnAuthority(key, op, operand);
            else RPC_RequestApply(key, (int)op, operand);
        }

        void ApplyOnAuthority(string key, ParamOp op, float operand)
        {
            ParamValue current = Values.TryGet(key, out NetworkParamValue nv) ? FromNet(nv)
                : (_declarations.TryGetValue(key, out ConsoleParameter d) ? d.DefaultValue() : ParamValue.Float(0f));
            if (current.Type == ParamType.String) return;   // relative ops are numeric only (StatEffect parity)
            float n = Op(current.Number, op, operand);
            ParamValue next = Clamp(key, new ParamValue(current.Type, n, current.Text));
            Values.Set(key, ToNet(next));
        }

        // Pre-spawn buffer write: update the mirror + fan out, so an early UnityEvent/trigger write is
        // visible to listeners and survives to Spawned (where the authority flushes it into the dict).
        void SetMirror(string key, in ParamValue v)
        {
            if (_mirror.TryGetValue(key, out ParamValue prev) && SameValue(prev, v)) return;
            _mirror[key] = v;
            ParamChanged?.Invoke(key);
        }

        // Pre-spawn relative op resolved locally against the mirror (numeric only, like the authority path).
        void ApplyMirror(string key, ParamOp op, float operand)
        {
            ParamValue current = _mirror.TryGetValue(key, out ParamValue cur) ? cur
                : (_declarations.TryGetValue(key, out ConsoleParameter d) ? d.DefaultValue() : ParamValue.Float(0f));
            if (current.Type == ParamType.String) return;
            float n = Op(current.Number, op, operand);
            SetMirror(key, Clamp(key, new ParamValue(current.Type, n, current.Text)));
        }

        // The relative-op math, shared by the authority path and the pre-spawn mirror path (StatEffect parity).
        static float Op(float n, ParamOp op, float operand)
        {
            switch (op)
            {
                case ParamOp.Add: return n + operand;
                case ParamOp.Subtract: return n - operand;
                case ParamOp.Multiply: return n * operand;
                case ParamOp.Divide: return operand != 0f ? n / operand : n;   // divide-by-zero = no-op
                case ParamOp.Set: return operand;
                default: return n;
            }
        }

        public override void Render()
        {
            if (!Ready) return;   // never read the [Networked] dict on an invalid/despawned object
            // Raise ParamChanged once per real change on every client (no polling).
            foreach (var kv in Values)
            {
                string id = kv.Key.ToString();
                ParamValue v = FromNet(kv.Value);
                if (!_mirror.TryGetValue(id, out ParamValue prev) || !SameValue(prev, v))
                {
                    _mirror[id] = v;
                    ParamChanged?.Invoke(id);
                }
            }
        }

        // ---- typed accessors (sugar over the union) ----
        public bool GetBool(string id, bool fallback = false) => TryGet(id, out ParamValue v) ? v.AsBool() : fallback;
        public int GetInt(string id, int fallback = 0) => TryGet(id, out ParamValue v) ? v.AsInt() : fallback;
        public float GetFloat(string id, float fallback = 0f) => TryGet(id, out ParamValue v) ? v.AsFloat() : fallback;
        public string GetString(string id, string fallback = "") => TryGet(id, out ParamValue v) ? v.AsString() : fallback;

        public T GetEnum<T>(string id, T fallback = default) where T : struct, Enum
            => TryGet(id, out ParamValue v) ? (T)Enum.ToObject(typeof(T), v.AsInt()) : fallback;

        public void SetBool(string id, bool value) => Set(id, ParamValue.Bool(value));
        public void SetInt(string id, int value) => Set(id, ParamValue.Int(value));
        public void SetFloat(string id, float value) => Set(id, ParamValue.Float(value));
        public void SetString(string id, string value) => Set(id, ParamValue.Str(value));
        public void SetEnum<T>(string id, T value) where T : struct, Enum
            => Set(id, ParamValue.Enum(Convert.ToInt32(value)));

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestSet(string id, int type, float number, string text)
        {
            ParamValue clamped = Clamp(id, new ParamValue((ParamType)type, number, text));
            Values.Set(id, ToNet(clamped));
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        void RPC_RequestApply(string id, int op, float operand) => ApplyOnAuthority(id, (ParamOp)op, operand);

        // ---- helpers ----
        ParamValue Clamp(string id, in ParamValue value)
            => _declarations.TryGetValue(id, out ConsoleParameter d) ? d.Clamp(value) : value;

        static string Norm(string id) => string.IsNullOrEmpty(id) ? id : id.Trim();

        static NetworkParamValue ToNet(in ParamValue v)
            => new NetworkParamValue { type = (int)v.Type, number = v.Number, text = v.Text ?? string.Empty };

        static ParamValue FromNet(in NetworkParamValue v)
            => new ParamValue((ParamType)v.type, v.number, v.text.ToString());

        static bool SameValue(in ParamValue a, in ParamValue b)
        {
            if (a.Type != b.Type) return false;
            if (a.Type == ParamType.String) return string.Equals(a.Text ?? string.Empty, b.Text ?? string.Empty, StringComparison.Ordinal);
            return Mathf.Approximately(a.Number, b.Number);
        }
    }

    /// <summary>The Fusion-replicable (unmanaged) twin of <see cref="ParamValue"/>: the same fields with
    /// a <c>NetworkString&lt;_64&gt;</c> for the text slot (Fusion value types must be unmanaged).</summary>
    public struct NetworkParamValue : INetworkStruct
    {
        public int type;        // (int)ParamType
        public float number;    // bool 0/1 / int / enum index / float
        public NetworkString<_64> text;   // string payload (<= 64 chars)
    }
}
#endif
