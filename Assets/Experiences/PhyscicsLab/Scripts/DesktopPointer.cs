using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Desktop replacement for the headset pointer. Casts a ray from the screen center,
    /// reports hover and clicks to LabObject, and drags grabbable objects by velocity.
    /// Only acts while the mouse is captured by DesktopRigController.
    /// </summary>
    public class DesktopPointer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("How far, in metres, the pointer can reach.")]
        [SerializeField] private float reach = 4f;
        [Tooltip("How strongly a dragged object chases the aim point (1/s).")]
        [SerializeField] private float dragGain = 15f;
        [Tooltip("Speed limit for a dragged object, in metres per second.")]
        [SerializeField] private float maxDragSpeed = 3f;

        private static readonly Vector3 ScreenCenter = new Vector3(0.5f, 0.5f, 0f);

        private LabObject _hovered;
        private LabObject _dragged;
        private Rigidbody _draggedBody;
        private float _dragDistance;

        /// <summary>
        /// Ends any drag and clears hover.
        /// </summary>
        private void OnDisable()
        {
            EndDrag();
            SetHovered(null);
        }

        private void Update()
        {
            // Does nothing until the mouse is captured since the first click captures the mouse
            // (DesktopRigController). Therefore, it should not also grab.
            if (Cursor.lockState != CursorLockMode.Locked) return;

            UpdateHover();
            HandleMouseButton();
        }

        private void FixedUpdate()
        {
            // ChaseAimPoint() sets velocity toward the aim point.
            if (_draggedBody != null) ChaseAimPoint();
        }

        /// <summary>
        /// Raycasts every frame, reports changes only since SetHovered ignores repeats,
        /// the log is not flooded.
        /// </summary>
        private void UpdateHover()
        {
            if (_dragged != null) return; // keep the dragged object as the hover target
            SetHovered(RaycastLabObject(out _));
        }

        private void HandleMouseButton()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame) PressOnHovered();
            if (mouse.leftButton.wasReleasedThisFrame) EndDrag();
        }

        /// <summary>
        /// Grabbable objects start a drag. Others get a click.
        /// </summary>
        private void PressOnHovered()
        {
            if (_hovered == null) return;

            if (_hovered.IsGrabbable) BeginDrag(_hovered);
            else _hovered.ReportClick();
        }

        private void BeginDrag(LabObject target)
        {
            _draggedBody = target.GetComponent<Rigidbody>();
            if (_draggedBody == null) return;

            RaycastLabObject(out float distance);
            _dragDistance = distance;
            _dragged = target;
            _dragged.ReportHeld(true);
        }

        private void EndDrag()
        {
            if (_dragged == null) return;

            _dragged.ReportHeld(false);
            _dragged = null;
            _draggedBody = null;
        }

        /// <summary>
        /// Sets the body's speed toward the aim point instead of teleporting it, so joints stay stable.
        /// </summary>
        private void ChaseAimPoint()
        {
            Ray ray = viewCamera.ViewportPointToRay(ScreenCenter);
            Vector3 aimPoint = ray.GetPoint(_dragDistance);
            aimPoint.y = _draggedBody.position.y; // stay on the table plane

            Vector3 velocity = (aimPoint - _draggedBody.position) * dragGain;
            // ClampMagnitude caps the speed since a fast mouse flick must not throw the scale across the table.
            _draggedBody.linearVelocity = Vector3.ClampMagnitude(velocity, maxDragSpeed);
        }

        private LabObject RaycastLabObject(out float distance)
        {
            Ray ray = viewCamera.ViewportPointToRay(ScreenCenter);
            if (Physics.Raycast(ray, out RaycastHit hit, reach))
            {
                distance = hit.distance;
                return hit.collider.GetComponentInParent<LabObject>();
            }

            distance = 0f;
            return null;
        }

        private void SetHovered(LabObject next)
        {
            if (next == _hovered) return;

            if (_hovered != null) _hovered.ReportHover(false);
            _hovered = next;
            if (_hovered != null) _hovered.ReportHover(true);
        }
    }
}