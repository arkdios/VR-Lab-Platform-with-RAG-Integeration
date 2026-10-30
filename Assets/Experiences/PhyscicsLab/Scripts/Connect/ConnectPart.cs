using UnityEngine;

namespace PhysicsLab.Lab.Connect
{
    /// <summary>
    /// A part that can be clamped to a ConnectPoint (the pulley clamp). While clamped, the body is frozen.
    /// While held, it can move freely. ConnectManager decides where it ends up when it is let go.
    /// Put it on each pulley (needs LabObject and Rigidbody on the same object).
    /// </summary>
    [RequireComponent(typeof(LabObject), typeof(Rigidbody))]
    public class ConnectPart : MonoBehaviour
    {
        [SerializeField] private ConnectKind fitsKind = ConnectKind.TableSlot;
        [Tooltip("Optional. Told the new angle when the part snaps to a table slot.")]
        [SerializeField] private Pulley pulley;

        private LabObject _labObject;
        private Rigidbody _body;
        private ConnectPoint _lastPoint;

        public ConnectKind FitsKind => fitsKind;
        public ConnectPoint Point { get; private set; }
        public bool IsConnected => Point != null;
        public Vector3 Position => transform.position;

        public LabObject LabObject
        {
            get
            {
                if (_labObject == null) _labObject = GetComponent<LabObject>();
                return _labObject;
            }
        }

        private Rigidbody Body
        {
            get
            {
                if (_body == null) _body = GetComponent<Rigidbody>();
                return _body;
            }
        }

        /// <summary>Marks the part as sitting on a spot without moving it. Used at start-up.</summary>
        public void MarkConnected(ConnectPoint point)
        {
            Clear();
            Point = point;
            _lastPoint = point;
            point.Occupant = this;
        }

        /// <summary>Marks the part as not sitting on any spot, without moving it.</summary>
        public void Clear()
        {
            if (Point != null && Point.Occupant == this) Point.Occupant = null;
            Point = null;
        }

        /// <summary>Called when the part is grabbed. Frees the spot and lets the body move.</summary>
        public void PickUp()
        {
            Clear();
            Body.constraints = RigidbodyConstraints.FreezeRotation; // moves, but never tips over
            Body.useGravity = false; // held in the air or on the table plane, it must not fall
        }

        /// <summary>Moves the part onto a spot and freezes it there.</summary>
        public void SnapTo(ConnectPoint point)
        {
            Clear();
            Point = point;
            _lastPoint = point;
            point.Occupant = this;

            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = point.transform.position;
            Body.rotation = point.transform.rotation;
            transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
            Body.constraints = RigidbodyConstraints.FreezeAll;

            if (pulley != null) pulley.SetAngleFromPosition(point.AngleDegrees);
        }

        /// <summary>No spot was close enough. The part goes back to where it was clamped before.</summary>
        public bool ReturnToLastPoint()
        {
            if (_lastPoint == null || !_lastPoint.IsFree) return false;
            SnapTo(_lastPoint);
            return true;
        }
    }
}
