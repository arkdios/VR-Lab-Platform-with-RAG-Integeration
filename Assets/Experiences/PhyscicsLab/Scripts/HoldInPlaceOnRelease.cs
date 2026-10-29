using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Freezes the body when it is let go and frees it when it is grabbed again.
    /// This copies a student who holds the spring scale steady: the scale stays where you put it
    /// while the string pulls on it. Without this, the string would drag the scale to the ring.
    /// Put it on the spring scale. Uses the LabObject grab events, so mouse and headset both work.
    /// </summary>
    [RequireComponent(typeof(LabObject), typeof(Rigidbody))]
    public class HoldInPlaceOnRelease : MonoBehaviour
    {
        private LabObject _labObject;
        private Rigidbody _body;
        private RigidbodyConstraints _movingConstraints;

        private void Awake()
        {
            _labObject = GetComponent<LabObject>();
            _body = GetComponent<Rigidbody>();
            _movingConstraints = _body.constraints; // what the body uses while it is dragged
        }

        private void OnEnable()
        {
            _labObject.HeldChanged += OnHeldChanged;
            Pin(); // starts where the scene put it
        }

        private void OnDisable() => _labObject.HeldChanged -= OnHeldChanged;

        private void OnHeldChanged(LabObject labObject, bool held)
        {
            if (held) _body.constraints = _movingConstraints;
            else Pin();
        }

        private void Pin()
        {
            _body.linearVelocity = Vector3.zero;
            _body.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}
