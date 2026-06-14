using System;
using UnityEngine;

namespace Pitech.XR.Scenario
{
    // -------- InsertStep --------
    [Serializable]
    public sealed class InsertStep : Step
    {
        [Header("Object to insert")]
        [Tooltip("Root object of the tool / item the learner moves. Rigidbody can be on this or a child.")]
        public Transform item;

        [Header("Target slot")]
        [Tooltip("Trigger collider that represents the slot. Used for proximity/containment checks.")]
        public Collider targetTrigger;

        [Tooltip("Final pose for the inserted object. If empty we use targetTrigger.transform.")]
        public Transform attachTransform;

        [Header("Attach behaviour")]
        [Tooltip("If true, once 'inserted' the object is smoothly moved & rotated into the final pose.")]
        public bool smoothAttach = true;

        [Tooltip("Parent the item to the attachTransform on completion.")]
        public bool parentToAttach = true;

        [Tooltip("Movement speed when auto-attaching (m/s).")]
        public float moveSpeed = 5f;

        [Tooltip("Rotation speed when auto-attaching (lerp factor per second).")]
        public float rotateSpeed = 5f;

        [Header("Detection")]
        [Tooltip("How close (meters) to the attachTransform before we consider the object 'inserted'.")]
        public float positionTolerance = 0.02f;

        [Tooltip("How close (degrees) in rotation. Set 0 to ignore rotation for completion.")]
        public float angleTolerance = 10f;

        [Header("Routing")]
        [Tooltip("Next step (GUID). Empty = next item in list")]
        public string nextGuid = "";

        public override string Kind => "Insert";
    }
}
