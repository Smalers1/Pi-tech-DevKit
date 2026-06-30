using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Pitech.XR.Vitals
{
    // ---------- Vital: a typed patient value + a 3D binding (map sec-8, WS B2.6) ----------
    // A Vital is a typed param-store value (pulse / breathing rate / BP / temp ...) paired with an
    // OPTIONAL 3D binding that reflects the value in the scene. The binding kinds mirror VR's
    // ControlOptionManager (the proven set): drive a Timeline's speed, set an Animator parameter, or
    // reflection-set a numeric MonoBehaviour field. PatientVitals owns the values (a LocalParamStore) and
    // calls Apply when a value changes. Foundation only - the full digital twin (cascade rules, profiles)
    // is post-launch.

    /// <summary>How a <see cref="Vital"/> reflects its value in the scene.</summary>
    public enum VitalBindingKind
    {
        None,
        TimelineSpeed,
        AnimatorParameter,
        Field
    }

    /// <summary>One typed patient vital + its optional 3D binding.</summary>
    [Serializable]
    public sealed class Vital
    {
        [Tooltip("Stable id for this vital (the param id in the vitals store).")]
        public string id;

        [Tooltip("Human-readable label (e.g. \"Heart rate\").")]
        public string label;

        [Tooltip("Initial value.")]
        public float defaultValue;

        [Tooltip("Optional clamp min (enforced when max > min).")]
        public float min;

        [Tooltip("Optional clamp max (enforced when max > min).")]
        public float max;

        [Header("3D binding (optional)")]
        public VitalBindingKind binding = VitalBindingKind.None;

        [Tooltip("TimelineSpeed: the director whose graph speed this vital drives.")]
        public PlayableDirector timeline;
        [Tooltip("TimelineSpeed: speed = baseSpeed * (value / baseRate).")]
        public float baseSpeed = 1f;
        [Tooltip("TimelineSpeed: the value that maps to baseSpeed.")]
        public float baseRate = 1f;

        [Tooltip("AnimatorParameter: the animator to drive.")]
        public Animator animator;
        [Tooltip("AnimatorParameter: the float/int parameter name to set.")]
        public string animatorParameter;

        [Tooltip("Field: the component whose numeric field this vital reflection-sets.")]
        public MonoBehaviour targetComponent;
        [Tooltip("Field: the public float/int field name to set.")]
        public string targetField;

        /// <summary>Drive the 3D binding from the current value. No-op for <see cref="VitalBindingKind.None"/>.</summary>
        public void Apply(float value)
        {
            switch (binding)
            {
                case VitalBindingKind.TimelineSpeed:
                    ApplyTimelineSpeed(value);
                    break;
                case VitalBindingKind.AnimatorParameter:
                    ApplyAnimator(value);
                    break;
                case VitalBindingKind.Field:
                    ApplyField(value);
                    break;
            }
        }

        void ApplyTimelineSpeed(float value)
        {
            if (timeline == null || !timeline.playableGraph.IsValid()) return;
            float rate = Mathf.Approximately(baseRate, 0f) ? 1f : baseRate;
            float speed = baseSpeed * (value / rate);
            timeline.playableGraph.GetRootPlayable(0).SetSpeed(speed);
        }

        void ApplyAnimator(float value)
        {
            if (animator == null || string.IsNullOrEmpty(animatorParameter)) return;
            for (int i = 0; i < animator.parameters.Length; i++)
            {
                AnimatorControllerParameter p = animator.parameters[i];
                if (p.name != animatorParameter) continue;
                if (p.type == AnimatorControllerParameterType.Int) animator.SetInteger(animatorParameter, Mathf.RoundToInt(value));
                else if (p.type == AnimatorControllerParameterType.Float) animator.SetFloat(animatorParameter, value);
                return;
            }
        }

        void ApplyField(float value)
        {
            if (targetComponent == null || string.IsNullOrEmpty(targetField)) return;
            var field = targetComponent.GetType().GetField(targetField,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null) return;
            if (field.FieldType == typeof(float)) field.SetValue(targetComponent, value);
            else if (field.FieldType == typeof(int)) field.SetValue(targetComponent, Mathf.RoundToInt(value));
        }
    }
}
