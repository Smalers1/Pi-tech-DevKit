using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Stats
{
    public class StatsRuntime
    {
        readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);
        public event Action<string, float, float> OnChanged;

        StatsConfig _cfg;

        public void Reset(StatsConfig cfg)
        {
            _cfg = cfg;
            _values.Clear();
            if (cfg == null) return;
            foreach (var kv in cfg.All())
                _values[kv.Key] = kv.Value.defaultValue;
        }

        public bool TryGetRange(string key, out float min, out float max)
        {
            if (_cfg == null) { min = 0; max = 1; return false; }
            var r = _cfg.GetRange(key);
            min = r.x; max = r.y;
            return true;
        }

        public void EnsureKey(string key, float initial = 0f)
        {
            var k = StatsConfig.NormalizeKey(key);
            if (string.IsNullOrEmpty(k)) return;
            if (!_values.ContainsKey(k)) _values[k] = initial;
        }

        public bool TryGet(string key, out float value) => _values.TryGetValue(StatsConfig.NormalizeKey(key), out value);

        public float this[string key]
        {
            get => _values.TryGetValue(StatsConfig.NormalizeKey(key), out var val) ? val : 0f; // no exception
            set
            {
                var k = StatsConfig.NormalizeKey(key);
                if (string.IsNullOrEmpty(k)) return;

                var old = _values.TryGetValue(k, out var o) ? o : 0f;
                if (Mathf.Approximately(old, value)) return;
                _values[k] = value;
                OnChanged?.Invoke(k, old, value);
            }
        }
    }
}
