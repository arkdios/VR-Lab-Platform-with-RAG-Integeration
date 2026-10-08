using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Keeps a Rigidbody inside a circle around the table center. Pushing against the edge
    /// removes the outward speed, so the body slides along the edge instead of leaving the table.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TableBounds : MonoBehaviour
    {
        [SerializeField] private Transform tableCenter;
        [SerializeField] private float maxRadiusMetres = 0.38f;

        private Rigidbody _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Vector3 offset = _body.position - tableCenter.position;
            offset.y = 0f;
            if (offset.magnitude <= maxRadiusMetres) return;

            Vector3 outward = offset.normalized;
            Vector3 clamped = tableCenter.position + outward * maxRadiusMetres;
            clamped.y = _body.position.y;

            _body.position = clamped;
            _body.linearVelocity = Vector3.ProjectOnPlane(_body.linearVelocity, outward);
        }
    }
}