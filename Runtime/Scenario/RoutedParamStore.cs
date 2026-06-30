using System;
using System.Collections.Generic;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario
{
    /// <summary>
    /// WS B2.4 Step 4 (map sec-8): the declared-parameter router that <see cref="LabConsole"/> builds ONLY
    /// when a networked <see cref="IParamStore"/> (a <c>NetworkedParamStore</c>) is present on the lab. It
    /// dispatches each parameter to the store that matches its declared <see cref="ParamScope"/>:
    /// <see cref="ParamScope.Networked"/> ids go to the replicated, authority-sequenced networked store;
    /// every other id (Local-declared, or an undeclared runtime access) stays in the per-client
    /// <see cref="LocalParamStore"/>. <see cref="ParamChanged"/> aggregates BOTH stores so the StatsRuntime
    /// display mirror keeps animating regardless of a parameter's scope.
    ///
    /// Single-player / no-Fusion labs NEVER construct this - <see cref="LabConsole"/> serves the
    /// LocalParamStore directly there, so the single-player trace is byte-identical to B.1. Referencing
    /// only Core's <see cref="IParamStore"/>, this needs no Pitech.XR.Networking asmdef dependency and
    /// compiles with Fusion absent (the only component IParamStore - NetworkedParamStore - is itself
    /// Fusion-gated, so it can only exist where Fusion compiled it).
    /// </summary>
    internal sealed class RoutedParamStore : IParamStore, IDisposable
    {
        readonly IParamStore _local;
        readonly IParamStore _networked;
        // Ids declared with Networked scope - the only ids routed to _networked. Populated by Declare.
        readonly HashSet<string> _networkedIds = new HashSet<string>(StringComparer.Ordinal);

        public event Action<string> ParamChanged;

        public RoutedParamStore(IParamStore local, IParamStore networked)
        {
            _local = local;
            _networked = networked;
            _local.ParamChanged += Forward;
            _networked.ParamChanged += Forward;
        }

        void Forward(string id) => ParamChanged?.Invoke(id);

        // Route by declared scope: a Networked-declared id -> the networked store; everything else
        // (Local-declared, or an undeclared runtime access) -> the local store. Routing is exclusive, so
        // an id lives in exactly one store and ParamChanged never double-fires.
        IParamStore Route(string id)
            => (!string.IsNullOrEmpty(id) && _networkedIds.Contains(Norm(id))) ? _networked : _local;

        // Same trim LocalParamStore/NetworkedParamStore key by, so the scope set and the store agree.
        static string Norm(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        public void Declare(ConsoleParameter declaration)
        {
            if (declaration == null) return;
            if (declaration.scope == ParamScope.Networked)
            {
                _networkedIds.Add(Norm(declaration.id));
                _networked.Declare(declaration);
            }
            else
            {
                _local.Declare(declaration);
            }
        }

        public bool IsDeclared(string id) => Route(id).IsDeclared(id);
        public bool TryGet(string id, out ParamValue value) => Route(id).TryGet(id, out value);
        public void Set(string id, in ParamValue value) => Route(id).Set(id, value);
        public void Apply(string id, ParamOp op, float operand) => Route(id).Apply(id, op, operand);

        public bool GetBool(string id, bool fallback = false) => Route(id).GetBool(id, fallback);
        public int GetInt(string id, int fallback = 0) => Route(id).GetInt(id, fallback);
        public float GetFloat(string id, float fallback = 0f) => Route(id).GetFloat(id, fallback);
        public string GetString(string id, string fallback = "") => Route(id).GetString(id, fallback);
        public T GetEnum<T>(string id, T fallback = default) where T : struct, Enum => Route(id).GetEnum(id, fallback);

        public void SetBool(string id, bool value) => Route(id).SetBool(id, value);
        public void SetInt(string id, int value) => Route(id).SetInt(id, value);
        public void SetFloat(string id, float value) => Route(id).SetFloat(id, value);
        public void SetString(string id, string value) => Route(id).SetString(id, value);
        public void SetEnum<T>(string id, T value) where T : struct, Enum => Route(id).SetEnum(id, value);

        public void Dispose()
        {
            _local.ParamChanged -= Forward;
            _networked.ParamChanged -= Forward;
        }
    }
}
