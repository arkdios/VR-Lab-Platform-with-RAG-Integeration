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

        [Tooltip("How forgiving the aim is, in metres. A thin ray misses small parts.")]
        [SerializeField] private float pickRadius = 0.03f;
        [Tooltip("A lab part this far behind the first surface still counts. It lets you pick parts that sit on the table.")]
        [SerializeField] private float pickDepthMargin = 0.15f;

        private const float DefaultAimDistance = 1.2f; // where the aim point sits when the ray hits nothing

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private bool _castHit;
        private Vector3 _castPoint;
        private Vector3 _castNormal;

        /// <summary>True while the mouse is captured. This is when the pointer works (Tab or a click turns it on).</summary>
        public bool IsAiming => Cursor.lockState == CursorLockMode.Locked;

        public LabObject Hovered => _hovered;

        public LabObject Dragged => _dragged;

        /// <summary>The point under the aim: on the held part, or where the ray lands, or in the air in front of the camera.</summary>
        public Vector3 AimPoint
        {
            get
            {
                if (_dragged != null && _draggedBody != null) return _draggedBody.position;
                if (_castHit) return _castPoint;
                return viewCamera.ViewportPointToRay(ScreenCenter).GetPoint(DefaultAimDistance);
            }
        }

        public Vector3 AimNormal => _castHit ? _castNormal : -viewCamera.transform.forward;

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
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                EndDrag(); // pointer lock was lost (Esc) while dragging: drop the object
                return;
            }

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
            PushPullWithWheel(mouse);
        }

        /// <summary>
        /// While a Free Drag part is held, the mouse wheel moves it closer or farther: 0.1 m per notch,
        /// between 0.4 m and the reach.
        /// </summary>
        private void PushPullWithWheel(Mouse mouse)
        {
            if (_dragged == null || !_dragged.FreeDrag) return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(wheel, 0f)) return;
            _dragDistance = Mathf.Clamp(_dragDistance + Mathf.Sign(wheel) * 0.1f, 0.4f, reach);
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
            // Parts with Free Drag ticked (the masses) move in 3D. The others stay on the table plane.
            if (!_dragged.FreeDrag) aimPoint.y = _draggedBody.position.y;

            Vector3 velocity = (aimPoint - _draggedBody.position) * dragGain;
            // ClampMagnitude caps the speed since a fast mouse flick must not throw the scale across the table.
            _draggedBody.linearVelocity = Vector3.ClampMagnitude(velocity, maxDragSpeed);
        }

        /// <summary>
        /// Sweeps a small sphere along the aim ray, because a thin ray misses small parts. The nearest lab part wins,
        /// as long as it is not far behind the first surface hit. That lets you pick parts that sit on the table.
        /// </summary>
        private LabObject RaycastLabObject(out float distance)
        {
            Ray ray = viewCamera.ViewportPointToRay(ScreenCenter);
            int count = Physics.SphereCastNonAlloc(ray, pickRadius, _hits, reach);

            RaycastHit first = default(RaycastHit);
            float firstDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance <= 0f || _hits[i].distance >= firstDistance) continue;
                first = _hits[i];
                firstDistance = first.distance;
            }

            _castHit = firstDistance < float.MaxValue;
            _castPoint = first.point;
            _castNormal = first.normal;

            LabObject part = NearestLabPart(count, firstDistance + pickDepthMargin, out RaycastHit partHit);
            if (part != null)
            {
                _castPoint = partHit.point;
                _castNormal = partHit.normal;
            }

            distance = part != null ? partHit.distance : 0f;
            return part;
        }

        private LabObject NearestLabPart(int count, float farthest, out RaycastHit best)
        {
            best = default(RaycastHit);
            LabObject part = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                bool nearer = hit.distance > 0f && hit.distance <= farthest && (part == null || hit.distance < best.distance);
                if (!nearer) continue;

                LabObject candidate = hit.collider.GetComponentInParent<LabObject>();
                if (candidate == null) continue;
                part = candidate;
                best = hit;
            }
            return part;
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