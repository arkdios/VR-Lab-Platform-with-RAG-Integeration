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
        public LabObject MassObject => massObject;
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

        /// <summary>
        /// The turn this pulley should have at a spot on the table: the same turn PlaceOnRim uses.
        /// ConnectPart calls it while the pulley is held, so the pulley keeps facing the table centre.
        /// </summary>
        public Quaternion FacingRotation(Vector3 position)
        {
            Vector3 outward = position - tableCenter.position;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-6f) return transform.rotation; // right over the centre: keep the turn
            return TurnForOutward(outward.normalized);
        }

        [Header("Wheel direction (filled in by Lab > 0)")]
        [Tooltip("The wheel axle in the pulley's own space. The pulley is turned so this axle lies along the rim of the table (the tangent), which puts the wheel plane in line with the string.")]
        [SerializeField] private Vector3 wheelAxleLocal = Vector3.right;
        [Tooltip("The direction from the wheel to the clamp, in the pulley's own space. The clamp is turned toward the table. Zero means ignore.")]
        [SerializeField] private Vector3 clampSideLocal = Vector3.zero;

        // outward is the flat direction from the table centre to the pulley, length 1.
        private Quaternion TurnForOutward(Vector3 outward)
        {
            Quaternion inward = Quaternion.LookRotation(-outward, Vector3.up);   // the old turn: +Z points at the table centre
            Vector3 axle = Flat(wheelAxleLocal);
            if (axle == Vector3.zero) return inward;                              // wheel lying flat: keep the old turn

            Vector3 tangent = Vector3.Cross(Vector3.up, outward);                 // along the rim of the table
            float yaw = Vector3.SignedAngle(axle, tangent, Vector3.up);
            Quaternion one = Quaternion.AngleAxis(yaw, Vector3.up);               // the axle on the tangent...
            Quaternion other = Quaternion.AngleAxis(yaw + 180f, Vector3.up);      // ...or the same line, turned half way round

            Vector3 clamp = Flat(clampSideLocal);
            if (clamp != Vector3.zero)
            {
                float oneScore = Vector3.Dot(one * clamp, -outward);              // 1 = the clamp faces the table
                float otherScore = Vector3.Dot(other * clamp, -outward);
                if (Mathf.Abs(oneScore - otherScore) > 0.1f) return oneScore > otherScore ? one : other;
            }
            return Quaternion.Angle(one, inward) <= Quaternion.Angle(other, inward) ? one : other;   // nearest to the old turn
        }

        // The same direction without its height, length 1. Zero when it points (almost) straight up or down.
        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.01f ? Vector3.zero : direction.normalized;
        }

        private Vector3 OutwardDirection()
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        }

        private void PlaceOnRim()
        {
            Vector3 outward = OutwardDirection();
            // Turn the pulley so its wheel axle lies along the rim (the tangent). The string then
            // runs in the plane of the wheel. FacingRotation works the turn out from the measured axle.
            transform.rotation = FacingRotation(tableCenter.position + outward * rimRadiusMetres);
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