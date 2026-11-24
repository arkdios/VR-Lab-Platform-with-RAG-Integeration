using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// Keeps the knot of a pulley string on the edge of the ring, on the side that faces the pulley.
    /// The builder puts the knot on one side when it builds the string. If the student then clamps the
    /// pulley at another spot, the knot would stay on the old side. The string would start on the far side
    /// of the ring, cross the ring, and be longer than it was cut for.
    /// This script turns the knot toward the groove point of the pulley in every physics step, so the string
    /// has the same length for every clamp spot.
    /// Put it on the ring hook object (a child of the Ring). The Editor builder does that.
    /// </summary>
    [DefaultExecutionOrder(-60)]   // before RimGuide (-50), RopeNetwork (200) and RopeStrand, which read the knot
    public class RingHookAimer : MonoBehaviour
    {
        [Tooltip("The ring this knot sits on.")]
        [SerializeField] private Transform ring;
        [Tooltip("The groove point of the pulley the string runs to.")]
        [SerializeField] private Transform groovePoint;
        [Tooltip("How far the knot sits from the middle of the ring, in metres.")]
        [SerializeField] private float radiusMetres = 0.03f;

        private void FixedUpdate() => Aim();

        /// <summary>Moves the knot now. The builder calls it in Edit Mode, before the string is baked.</summary>
        public void Aim()
        {
            if (ring == null || groovePoint == null) return;

            transform.position = KnotPosition(ring.position, groovePoint.position, radiusMetres);
        }

        /// <summary>
        /// The point on the edge of the ring that faces the target, seen from above.
        /// It has the height of the ring. With the target right above the ring, the knot stays at the middle.
        /// </summary>
        public static Vector3 KnotPosition(Vector3 ringCentre, Vector3 target, float radius)
        {
            Vector3 flat = target - ringCentre;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f) return ringCentre;

            return ringCentre + flat.normalized * radius;
        }
    }
}