using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Safety net for demos. If the object ever drops below the floor, it goes back to where it started.
    /// Put it on any loose lab part (scale, masses, string end).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ReturnIfFallen : MonoBehaviour
    {
        [Tooltip("World height in metres. Below this, the object is put back.")]
        [SerializeField] private float lowestAllowedY = -0.5f;

        private Rigidbody _body;
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _startPosition = _body.position;
            _startRotation = _body.rotation;
        }

        private void FixedUpdate()
        {
            if (_body.position.y >= lowestAllowedY) return;

            Debug.LogWarning($"[ReturnIfFallen] {name} fell below the floor and was put back.", this);
            _body.position = _startPosition;
            _body.rotation = _startRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }
}
