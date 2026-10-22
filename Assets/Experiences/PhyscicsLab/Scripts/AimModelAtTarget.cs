using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Turns only the visual child of the spring scale so its hook faces the ring.
    /// The Rigidbody on the root has Freeze Rotation, so the root never turns; the Model child does.
    /// It measures the real hook direction every frame, so it does not depend on which axis
    /// the model's hook points along or on any sign convention.
    /// </summary>
    public class AimModelAtTarget : MonoBehaviour
    {
        private const float MinHorizontalDistanceSqr = 1e-6f;

        [Tooltip("The visual child that is allowed to turn. Its origin must sit on the scale's centre.")]
        [SerializeField] private Transform model;
        [Tooltip("A point on the hook end of the model, for example HookEye.")]
        [SerializeField] private Transform hookEye;
        [SerializeField] private Transform target;

        private void Awake()
        {
            if (model != null && hookEye != null && target != null) return;
            Debug.LogError("[AimModelAtTarget] Assign Model, Hook Eye and Target.", this);
            enabled = false;
        }

        private void LateUpdate()
        {
            Vector3 pivot = model.position;
            Vector3 hookDirection = Flatten(hookEye.position - pivot);
            Vector3 targetDirection = Flatten(target.position - pivot);
            if (hookDirection.sqrMagnitude < MinHorizontalDistanceSqr ||
                targetDirection.sqrMagnitude < MinHorizontalDistanceSqr) return;

            // SignedAngle and Rotate use the same axis convention, so this lines the hook up in one step.
            float yaw = Vector3.SignedAngle(hookDirection, targetDirection, Vector3.up);
            model.Rotate(Vector3.up, yaw, Space.World);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
