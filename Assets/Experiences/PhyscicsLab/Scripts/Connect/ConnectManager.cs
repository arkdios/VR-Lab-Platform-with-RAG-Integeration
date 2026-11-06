using UnityEngine;

namespace PhysicsLab.Lab.Connect
{
    /// <summary>
    /// The rules for connecting. While a part is held, free spots glow dim and the nearest one inside the snap
    /// radius glows bright. Let go there and the part snaps on. Let go anywhere else and it goes back to where
    /// it was. No button is needed, so a mouse and a headset work the same way.
    /// Put one in the scene (for example on Lab Logic).
    /// </summary>
    public class ConnectManager : MonoBehaviour
    {
        private const float StartMatchMetres = 0.02f; // a part this close to a spot counts as sitting on it

        [SerializeField] private TableSlotRing slotRing;
        [Tooltip("Every part that can be clamped (both pulleys).")]
        [SerializeField] private ConnectPart[] parts;
        [Tooltip("Optional. Used to log connected and disconnected events for the backend.")]
        [SerializeField] private LabStateTracker stateTracker;
        [Tooltip("A held part snaps to a spot at most this far away, in metres.")]
        [SerializeField] private float snapRadiusMetres = 0.10f;

        private ConnectPart _held;

        private void OnEnable()
        {
            foreach (ConnectPart part in parts)
            {
                if (part != null) part.LabObject.HeldChanged += OnHeldChanged;
            }
        }

        private void OnDisable()
        {
            foreach (ConnectPart part in parts)
            {
                if (part != null) part.LabObject.HeldChanged -= OnHeldChanged;
            }
        }

        private void Start() => MatchPartsToSpots();

        private void Update()
        {
            if (_held != null) UpdateGlow();
            else MatchPartsToSpots(); // the UI angle buttons or a restored save can move a pulley by themselves
        }

        private void OnHeldChanged(LabObject labObject, bool held)
        {
            ConnectPart part = FindPart(labObject);
            if (part == null) return;

            if (held) Grab(part);
            else Release(part);
        }

        private void Grab(ConnectPart part)
        {
            if (part.IsConnected) Log(LabEventTypes.Disconnected, part, DescribeSpot(part.Point));
            part.PickUp();
            _held = part;
            UpdateGlow();
        }

        private void Release(ConnectPart part)
        {
            if (_held != part) return;

            ConnectPoint target = FindNearestFreeSpot(part, out float distance);
            if (target != null && distance <= snapRadiusMetres)
            {
                part.SnapTo(target);
                Log(LabEventTypes.Connected, part, DescribeSpot(target));
            }
            else if (part.ReturnToLastPoint())
            {
                Log(LabEventTypes.Connected, part, DescribeSpot(part.Point) + " (returned)");
            }
            else
            {
                part.Clear(); // nowhere to go back to: it stays where it was let go, frozen by its own rigidbody
            }

            _held = null;
            slotRing.HideAll();
        }

        // Dim for free spots, bright for the one that would be chosen, hidden for taken ones.
        private void UpdateGlow()
        {
            ConnectPoint nearest = FindNearestFreeSpot(_held, out float distance);
            bool inRange = nearest != null && distance <= snapRadiusMetres;

            foreach (ConnectPoint spot in slotRing.Slots)
            {
                if (spot.Kind != _held.FitsKind || !spot.IsFree) spot.SetGlow(GlowState.Hidden);
                else if (inRange && spot == nearest) spot.SetGlow(GlowState.Bright);
                else spot.SetGlow(GlowState.Dim);
            }
        }

        private ConnectPoint FindNearestFreeSpot(ConnectPart part, out float distance)
        {
            ConnectPoint best = null;
            float bestSqr = float.MaxValue;
            Vector3 here = part.Position;

            foreach (ConnectPoint spot in slotRing.Slots)
            {
                if (spot.Kind != part.FitsKind || !spot.IsFree) continue;
                Vector3 offset = spot.transform.position - here;
                offset.y = 0f; // only the position along the table counts
                if (offset.sqrMagnitude >= bestSqr) continue;
                bestSqr = offset.sqrMagnitude;
                best = spot;
            }

            distance = Mathf.Sqrt(bestSqr);
            return best;
        }

        // Keeps the "who sits where" list true, whoever moved the part.
        private void MatchPartsToSpots()
        {
            foreach (ConnectPart part in parts)
            {
                if (part == null) continue;
                ConnectPoint nearest = FindNearestSpotIncludingTaken(part, out float distance);
                if (nearest != null && distance <= StartMatchMetres)
                {
                    if (part.Point != nearest) part.MarkConnected(nearest);
                }
                else if (part.Point != null && !ReferenceEquals(part, _held))
                {
                    // Moved away from its spot (for example by the angle buttons): the spot is free again.
                    if ((part.Point.transform.position - part.Position).sqrMagnitude > StartMatchMetres * StartMatchMetres)
                    {
                        part.Clear();
                    }
                }
            }
        }

        private ConnectPoint FindNearestSpotIncludingTaken(ConnectPart part, out float distance)
        {
            ConnectPoint best = null;
            float bestSqr = float.MaxValue;
            foreach (ConnectPoint spot in slotRing.Slots)
            {
                if (spot.Kind != part.FitsKind) continue;
                if (!spot.IsFree && spot.Occupant != part) continue;
                Vector3 offset = spot.transform.position - part.Position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= bestSqr) continue;
                bestSqr = offset.sqrMagnitude;
                best = spot;
            }
            distance = Mathf.Sqrt(bestSqr);
            return best;
        }

        private ConnectPart FindPart(LabObject labObject)
        {
            foreach (ConnectPart part in parts)
            {
                if (part != null && part.LabObject == labObject) return part;
            }
            return null;
        }

        private static string DescribeSpot(ConnectPoint spot) => spot == null ? "none" : $"slot {spot.AngleDegrees:0} deg";

        private void Log(string type, ConnectPart part, string detail)
        {
            if (stateTracker != null) stateTracker.RecordEvent(type, part.LabObject.ObjectId, detail);
        }
    }
}
