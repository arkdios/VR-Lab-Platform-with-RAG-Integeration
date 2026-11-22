using UnityEngine;

namespace PhysicsLab.Lab.Rope
{
    /// <summary>
    /// The point where a taut string touches the edge of the force table top.
    /// Put it on a string arm that ends below the table top and outside its edge, like the spring scale
    /// resting on the bench. RopeArm uses it as the guide, the same way it uses the top of a pulley wheel.
    /// So the string bends over the edge, the length is counted along the bend, and the pull on the ring
    /// stays flat on the table, where a real string over a table edge pulls it.
    /// When the string does not touch the edge, the point lies on the straight line, so nothing changes.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]   // before RopeNetwork (200), which reads this point every physics step
    public class RimGuide : MonoBehaviour
    {
        [Tooltip("The ring end of the arm (the same Transform as the arm's Ring Anchor).")]
        [SerializeField] private Transform ringAnchor;
        [Tooltip("The loop end of the arm (the same Transform as the arm's End Anchor).")]
        [SerializeField] private Transform endAnchor;
        [Tooltip("The drawn top of the force table (Table_Disk). It gives the radius and the height of the edge.")]
        [SerializeField] private Renderer tableDisc;
        [Tooltip("How far the string stays off the edge, in metres. The same gap RopeStrand keeps.")]
        [SerializeField] private float clearanceMetres = 0.0025f;

        private void FixedUpdate()
        {
            if (Application.isPlaying) Place();
        }

        /// <summary>Moves this object to the touch point now. The builder calls it in Edit Mode, before the string is baked.</summary>
        public void Place()
        {
            if (ringAnchor == null || endAnchor == null || tableDisc == null) return;

            Bounds top = tableDisc.bounds;
            float reach = Mathf.Min(top.extents.x, top.extents.z) + clearanceMetres;
            transform.position = GuidePoint(ringAnchor.position, endAnchor.position, top.center, reach, top.max.y + clearanceMetres);
        }

        /// <summary>
        /// The point where the straight line from the ring to the end crosses the edge circle (seen from above).
        /// It is lifted to the edge height when the line would pass below it. With the end inside the circle,
        /// or no crossing, it is the middle of the line.
        /// </summary>
        public static Vector3 GuidePoint(Vector3 ring, Vector3 end, Vector3 tableCentre, float edgeRadius, float edgeHeight)
        {
            Vector3 middle = (ring + end) * 0.5f;
            float startX = ring.x - tableCentre.x;
            float startZ = ring.z - tableCentre.z;
            float alongX = end.x - ring.x;
            float alongZ = end.z - ring.z;
            // Where startX + t * alongX, startZ + t * alongZ is edgeRadius from the centre: a * t^2 + b * t + c = 0.
            float a = alongX * alongX + alongZ * alongZ;
            if (a < 1e-8f) return middle;
            float b = 2f * (startX * alongX + startZ * alongZ);
            float c = startX * startX + startZ * startZ - edgeRadius * edgeRadius;
            float root = b * b - 4f * a * c;
            if (root <= 0f) return middle;
            float t = (-b + Mathf.Sqrt(root)) / (2f * a);   // the crossing where the line leaves the circle
            if (t <= 0f || t >= 1f) return middle;
            Vector3 onLine = ring + (end - ring) * t;
            onLine.y = Mathf.Max(onLine.y, edgeHeight);
            return onLine;
        }
    }
}