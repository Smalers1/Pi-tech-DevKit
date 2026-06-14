using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Stats
{
    [CreateAssetMenu(menuName = "Pi tech/Stats Config")]
    public class StatsConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Stat title / identifier used in code & bindings. Must be unique. Example: \"Money\" or \"CO2\".")]
            public string key;

            [Tooltip("Initial value when a scenario starts / stats are reset.")]
            public float defaultValue;

            [Tooltip("Minimum allowed value (used for UI sliders/clamping).")]
            public float min;

            [Tooltip("Maximum allowed value (used for UI sliders/clamping).")]
            public float max;
        }
        [SerializeField] Entry[] entries;

        Dictionary<string, Entry> _table;

        void Ensure()
        {
            if (_table != null) return;
            _table = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (entries == null) return;

            foreach (var e in entries)
            {
                var k = NormalizeKey(e.key);
                if (string.IsNullOrEmpty(k)) continue;
                _table[k] = e;
            }
        }

        public static string NormalizeKey(string key) => string.IsNullOrWhiteSpace(key) ? "" : key.Trim();

        public bool TryGet(string key, out Entry entry)
        {
            Ensure();
            return _table.TryGetValue(NormalizeKey(key), out entry);
        }

        public float GetDefault(string key) => TryGet(key, out var e) ? e.defaultValue : 0f;

        public Vector2 GetRange(string key)
        {
            if (!TryGet(key, out var e)) return new Vector2(0f, 1f);
            return new Vector2(e.min, e.max);
        }

        public IEnumerable<KeyValuePair<string, Entry>> All()
        {
            Ensure();
            return _table;
        }
    }
}
