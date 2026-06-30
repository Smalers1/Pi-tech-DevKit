using System.Collections.Generic;
using UnityEngine;
using Pitech.XR.Core;

namespace Pitech.XR.Vitals
{
    // ---------- PatientVitals: the typed patient-state model (map sec-8, WS B2.6) ----------
    // The single typed model for a patient's vitals (pulse / breathing / BP / temp ...), backed by a
    // param store and exposing IAgentStateSource for VICKY-observe. FOUNDATION ONLY (decision 41,
    // CAN_TRAIL): a self-contained typed surface that sits ADDITIVELY alongside VR's existing scattered
    // vitals logic - NOT a cutover. The full digital twin (cascade rules, profiles, the ControlOptionManager
    // PUN->Fusion convergence, scene migration) is post-launch.
    //
    // Owns its own LocalParamStore (so it is testable + decoupled from LabConsole). SetVital writes the
    // store -> ParamChanged -> the Vital drives its 3D binding. Wire at least one real binding (e.g. the
    // breathing-blendshape Timeline-speed vital) in a scene to validate - that scene wiring is the
    // author-side/post-B2 step; the code path is delivered here.

    [AddComponentMenu("Pi tech/Vitals/Patient Vitals")]
    [DisallowMultipleComponent]
    public sealed class PatientVitals : MonoBehaviour, IAgentStateSource
    {
        [Tooltip("The typed vitals for this patient. Each has a value + an optional 3D binding.")]
        public List<Vital> vitals = new List<Vital>();

        LocalParamStore _store;

        void Awake()
        {
            _store = new LocalParamStore();
            for (int i = 0; i < vitals.Count; i++)
            {
                Vital v = vitals[i];
                if (v == null || string.IsNullOrEmpty(v.id)) continue;
                _store.Declare(new ConsoleParameter
                {
                    id = v.id,
                    type = ParamType.Float,
                    defaultNumber = v.defaultValue,
                    min = v.min,
                    max = v.max,
                    scope = ParamScope.Local
                });
            }
            _store.ParamChanged += OnParamChanged;
        }

        void Start()
        {
            // Initial bind: drive each binding from its seeded value.
            for (int i = 0; i < vitals.Count; i++)
            {
                Vital v = vitals[i];
                if (v != null && !string.IsNullOrEmpty(v.id)) v.Apply(_store.GetFloat(v.id));
            }
        }

        void OnDestroy()
        {
            if (_store != null) _store.ParamChanged -= OnParamChanged;
        }

        /// <summary>Set a vital's value (clamped to its declared range). Drives its 3D binding.</summary>
        public void SetVital(string id, float value)
        {
            if (_store == null || string.IsNullOrEmpty(id)) return;
            _store.SetFloat(id, value);
        }

        /// <summary>Read a vital's current value (or 0 if unknown).</summary>
        public float GetVital(string id) => _store != null ? _store.GetFloat(id) : 0f;

        void OnParamChanged(string id)
        {
            Vital v = Find(id);
            if (v != null) v.Apply(_store.GetFloat(id));
        }

        Vital Find(string id)
        {
            for (int i = 0; i < vitals.Count; i++)
                if (vitals[i] != null && vitals[i].id == id) return vitals[i];
            return null;
        }

        // ---- IAgentStateSource (VICKY-observe) ----
        public bool TryGetState(string key, out float value)
        {
            if (_store != null && _store.IsDeclared(key)) { value = _store.GetFloat(key); return true; }
            value = 0f;
            return false;
        }

        public IEnumerable<KeyValuePair<string, float>> ReadState()
        {
            if (_store == null) yield break;
            for (int i = 0; i < vitals.Count; i++)
            {
                Vital v = vitals[i];
                if (v != null && !string.IsNullOrEmpty(v.id))
                    yield return new KeyValuePair<string, float>(v.id, _store.GetFloat(v.id));
            }
        }
    }
}
