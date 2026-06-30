using UnityEngine;

namespace Pitech.XR.Vitals
{
    // ---------- SampleBreathingVital: a per-frame breathing waveform driver (WS B2.6 sample) ----------
    // PatientVitals already maps a vital value onto a 3D binding (TimelineSpeed / AnimatorParameter /
    // Field) via Vital.Apply - but NOTHING in the Vitals package writes a value over TIME (Apply only
    // runs on SetVital or once at Start). This tiny MonoBehaviour is exactly that missing piece: each
    // frame it computes a sinusoidal breathing waveform and pushes it through the existing public
    // PatientVitals.SetVital(string,float), so whatever binding the author configured on the matching
    // vital shows a live chest rise/fall.
    //
    // PLACEMENT: add this to the patient GameObject that HAS (or sits UNDER) a PatientVitals. Leave the
    // 'vitals' field empty to auto-resolve it by parent-walk in Start, or assign it explicitly when the
    // PatientVitals is a SIBLING (parent-walk only finds it on this object or an ancestor). On that
    // PatientVitals, author a Vital whose id == vitalId ("breathing") with the binding the demo should
    // show, e.g.:
    //   - binding = Field        -> targetComponent/targetField point at a small script's public float
    //                               field (e.g. a blendshape-driver). NOTE: the Field binding reflection-
    //                               sets a public numeric FIELD only; it does NOT call
    //                               SkinnedMeshRenderer.SetBlendShapeWeight, so a tiny bridge script
    //                               (exposing a public float) is the author's job - out of scope here.
    //   - binding = TimelineSpeed -> a looping chest Timeline whose speed this value tunes.
    //   - binding = AnimatorParameter -> an Animator float the chest animation reads.
    //
    // RANGE NOTE: minValue/maxValue here set the AMPLITUDE pushed to SetVital. SetVital additionally
    // clamps to the Vital's declared min/max (the store enforces clamping only when max > min), so keep
    // the Vital's declared range consistent with these to avoid surprises.
    //
    // This is a SAMPLE / foundation driver. The full digital twin (cascade rules, profiles, multi-patient
    // scale) is post-launch; per-frame SetVital on many patients is not the intended production path.

    /// <summary>
    /// A minimal per-frame breathing driver. Pushes a sinusoidal waveform into one
    /// <see cref="PatientVitals"/> vital (by id) via <see cref="PatientVitals.SetVital"/>, so the vital's
    /// existing 3D binding animates a live chest rise/fall. Place on the patient GameObject that has (or
    /// sits under) a <see cref="PatientVitals"/>; author a <see cref="Vital"/> whose id matches
    /// <see cref="vitalId"/>. Foundation sample only - the full digital twin is post-launch.
    /// </summary>
    [AddComponentMenu("Pi tech/Vitals/Sample Breathing Vital")]
    [DisallowMultipleComponent]
    public sealed class SampleBreathingVital : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("The PatientVitals to drive. If empty, resolved by parent-walk in Start (this object or an ancestor). Assign explicitly if the PatientVitals is a sibling.")]
        PatientVitals vitals;

        [SerializeField, Tooltip("The id of the breathing Vital to drive. Must match a Vital.id authored on the PatientVitals.")]
        string vitalId = "breathing";

        [Header("Waveform")]
        [SerializeField, Tooltip("Breathing rate in breaths per minute (resting adult ~14). One full breath cycle = 1.0 phase unit.")]
        float breathsPerMinute = 14f;

        [SerializeField, Tooltip("The value pushed at the trough of the breath (chest fully exhaled). Mapped onto the vital's binding.")]
        float minValue = 0f;

        [SerializeField, Tooltip("The value pushed at the peak of the breath (chest fully inhaled). Mapped onto the vital's binding.")]
        float maxValue = 1f;

        [SerializeField, Tooltip("If true, the breathing oscillation begins automatically at Start.")]
        bool driveOnStart = true;

        bool _driving;
        float _phase;

        void Start()
        {
            // Cross-object resolution in Start (house rule), via parent-walk (no Find/Camera.main/Resources).
            if (vitals == null) vitals = GetComponentInParent<PatientVitals>(true);
            if (vitals == null)
            {
                // Loud, not silent (setup-time logging is allowed): disable self so Update never spams.
                Debug.LogWarning("[Vitals] SampleBreathingVital found no PatientVitals (set the 'vitals' field, or parent this under a PatientVitals). Disabled.", this);
                enabled = false;
                return;
            }
            _driving = driveOnStart;
        }

        void Update()
        {
            if (!_driving || vitals == null) return;
            // Advance phase by rate; one full breath cycle = 1.0 phase unit. No Debug.Log in the per-frame path.
            _phase += Time.deltaTime * (breathsPerMinute / 60f);
            // 0..1 waveform starting at the trough (-PI/2 phase offset on the sine).
            float wave01 = (Mathf.Sin(_phase * 2f * Mathf.PI - Mathf.PI * 0.5f) + 1f) * 0.5f;
            vitals.SetVital(vitalId, Mathf.Lerp(minValue, maxValue, wave01));
        }

        /// <summary>Begin (or resume) the breathing oscillation. UnityEvent-callable.</summary>
        public void StartBreathing() => _driving = true;

        /// <summary>Pause the breathing oscillation (the vital keeps its last value). UnityEvent-callable.</summary>
        public void StopBreathing() => _driving = false;

        /// <summary>Set the breathing rate in breaths per minute (clamped to >= 0). UnityEvent-callable.</summary>
        public void SetRate(float breathsPerMinuteValue) => breathsPerMinute = Mathf.Max(0f, breathsPerMinuteValue);

        /// <summary>True while the breathing oscillation is running.</summary>
        public bool IsBreathing => _driving;
    }
}
