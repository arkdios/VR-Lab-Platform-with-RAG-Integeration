using UnityEngine;
using PhysicsLab.Lab.Rope;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Spring scale that pulls the ring with a PhysX SpringJoint. The reading is the spring
    /// constant times the stretch, so it equals the force on the ring at rest.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SpringScale : MonoBehaviour
    {
        [SerializeField] private Rigidbody ring;
        [Tooltip("Spring constant in newtons per metre. Higher = stiffer scale.")]
        [SerializeField] private float springConstant = 40f;
        [SerializeField] private float damper = 2f;
        [Tooltip("Optional. The string arm tied to this scale. When set, the reading is that string's tension " +
                 "and the spring joint below is not added.")]
        [SerializeField] private RopeArm tensionArm;

        //ReadingNewtons here is to compute from distance, not from the joint's solver force.
        public float ReadingNewtons =>
            tensionArm != null ? tensionArm.TensionNewtons : springConstant * HorizontalDistanceToRing();

        /// <summary>
        /// Angle of the string that holds the scale, in degrees, counter-clockwise, 0 to 360. It is the same
        /// convention as Pulley.AngleDegrees. With a string arm it runs from the ring anchor to the hook, so the
        /// hook counts, not the middle of the scale body.
        /// </summary>
        public float StringAngleDegrees
        {
            get
            {
                Vector3 from = tensionArm != null ? tensionArm.RingAnchor.position : ring.position;
                Vector3 to = tensionArm != null ? tensionArm.EndAnchor.position : transform.position;
                Vector3 along = to - from;
                return Mathf.Repeat(Mathf.Atan2(along.z, along.x) * Mathf.Rad2Deg, 360f);
            }
        }

        private void Awake()
        {
            if (tensionArm == null) AddJointToRing();
        }

        private float HorizontalDistanceToRing()
        {
            Vector3 offset = transform.position - ring.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>
        /// Anchors sit at both centers and the rest length is zero, so force grows with distance.
        /// </summary>
        private void AddJointToRing()
        {
            SpringJoint joint = gameObject.AddComponent<SpringJoint>();
            joint.connectedBody = ring;
            joint.autoConfigureConnectedAnchor = false; // Anchors are exactly the object centers.
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = Vector3.zero;
            joint.spring = springConstant;
            joint.damper = damper;
            joint.minDistance = 0f;
            joint.maxDistance = 0f;
        }
    }
}