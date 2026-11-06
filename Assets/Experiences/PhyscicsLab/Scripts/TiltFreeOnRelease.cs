using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Lets a heavy prop (the chair) fall over like a real one, but keeps it level in a hand.
    /// While a hand or the mouse holds it, it is stood upright (same facing) and its tilt is locked,
    /// so it does not swing around while it is dragged.
    /// When it is let go, its tilt is free. A chair that rests on only one leg then tips and falls.
    /// Put it on the chair. It uses the LabObject grab events, so mouse and headset both work.
    /// </summary>
    [RequireComponent(typeof(LabObject), typeof(Rigidbody))]
    public class TiltFreeOnRelease : MonoBehaviour
    {
        // Tilt locked on X and Z. The chair can still turn left and right (Y).
        private const RigidbodyConstraints HeldConstraints =
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        private LabObject _labObject;
        private Rigidbody _body;

        private void Awake()
        {
            _labObject = GetComponent<LabObject>();
            _body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => _labObject.HeldChanged += OnHeldChanged;

        private void OnDisable() => _labObject.HeldChanged -= OnHeldChanged;

        private void OnHeldChanged(LabObject labObject, bool held)
        {
            if (held) StandUpright();
            else _body.constraints = RigidbodyConstraints.None;
        }

        // Keeps the way the chair faces and sets its tilt to zero. This also stands up a chair that fell over.
        private void StandUpright()
        {
            Vector3 facing = Vector3.ProjectOnPlane(_body.rotation * Vector3.forward, Vector3.up);
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward; // no flat facing to keep (chair on its face or back)

            _body.angularVelocity = Vector3.zero;
            _body.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            _body.constraints = HeldConstraints;
        }
    }
}