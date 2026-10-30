using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The pulley on the table rim with a hanging mass that pulls the ring toward itself with the
    /// mass's weight. Angle and mass are changed through SetAngle and SetMass (UI buttons).
    /// Angle convention: degrees, counter-clockwise.
    /// </summary>
    [RequireComponent(typeof(LabObject))]
    public class Pulley : MonoBehaviour
    {
        [SerializeField] private Rigidbody ring;
        [SerializeField] private Transform tableCenter;
        [SerializeField] private LabObject massObject;
        [Tooltip("Optional. The Rigidbody of the hanging mass. When set, the string pulls the ring")]
        [SerializeField] private Rigidbody massBody;
        [SerializeField] private float rimRadiusMetres = 0.4f;
        [Tooltip("How far outside the rim and how far below it.")]
        [SerializeField] private float massOutsetMetres = 0.07f;
        [SerializeField] private float massDropMetres = 0.12f;
        [SerializeField, Range(0f, 359f)] private float angleDegrees;
        [SerializeField, Range(0f, MaxMassKg)] private float massKg = 0.1f;

        private const float MaxMassKg = 0.5f;

        private LabObject _labObject;

        public string ObjectId => _labObject.ObjectId;
        public string MassObjectId => massObject.ObjectId;
        public Vector3 MassPosition => massObject.transform.position;
        public float AngleDegrees => angleDegrees;
        public float MassKg => massKg;
        // Weight of the hanging mass is the string tension
        public float PullNewtons => massKg * Physics.gravity.magnitude;

        public event Action<Pulley> AngleChanged;
        public event Action<Pulley> MassChanged;

        private void Awake()
        {
            _labObject = GetComponent<LabObject>();
        }

        private void OnEnable()
        {
            PlaceOnRim();
            ApplyMassToBody();
        }

        // PullRing on this function is for adding a force toward the pulley every physics step.
        // With a massBody the string does this job, so the fake pull is skipped.
        private void FixedUpdate()
        {
            if (massBody == null) PullRing();
        }

        public void SetAngle(float degrees)
        {
            float wrapped = Mathf.Repeat(degrees, 360f); // wraps 365 deg to 5 deg
            if (Mathf.Approximately(wrapped, angleDegrees)) return; // no change, no event
            angleDegrees = wrapped;
            PlaceOnRim();
            AngleChanged?.Invoke(this);
        }

        /// <summary>
        /// Called when the pulley was moved by hand and clamped to a table spot. It updates the angle and
        /// raises AngleChanged, but does not move anything (the clamp already put the pulley in place).
        /// </summary>
        public void SetAngleFromPosition(float degrees)
        {
            float wrapped = Mathf.Repeat(degrees, 360f);
            if (Mathf.Approximately(wrapped, angleDegrees)) return; // no change, no event
            angleDegrees = wrapped;
            AngleChanged?.Invoke(this);
        }

        public void SetMass(float kilograms)
        {
            float clamped = Mathf.Clamp(kilograms, 0f, MaxMassKg);
            if (Mathf.Approximately(clamped, massKg)) return; // no change, no event
            massKg = clamped;
            ApplyMassToBody();
            MassChanged?.Invoke(this);
        }

        // The hanging mass is a real body, so its weight comes from its Rigidbody mass.
        private void ApplyMassToBody()
        {
            if (massBody != null) massBody.mass = Mathf.Max(massKg, 0.001f);
        }

        private Vector3 OutwardDirection()
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        }

        private void PlaceOnRim()
        {
            Vector3 outward = OutwardDirection();
            // Turn the pulley so its local +Z points at the table center. The wheel then
            // lines up with the string, and the axle stays tangent to the rim.
            transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            transform.position = tableCenter.position + outward * rimRadiusMetres;
            // With a massBody the mass is a child that hangs from the string, so it moves with the pulley.
            if (massBody != null) return;
            massObject.transform.position =
                transform.position + outward * massOutsetMetres + Vector3.down * massDropMetres;
        }

        /// <summary>
        /// The string runs horizontally from the ring to the pulley, so only horizontal force acts.
        /// </summary>
        private void PullRing()
        {
            Vector3 towardPulley = transform.position - ring.position;
            towardPulley.y = 0f;
            if (towardPulley.sqrMagnitude < 1e-6f) return; // ring is directly under the pulley

            ring.AddForce(towardPulley.normalized * PullNewtons, ForceMode.Force); // ForceMode.Force is the continuous force in Newton
        }
    }
}