using System.Globalization;
using PhysicsLab.Core.State;
using PhysicsLab.Core.Telemetry;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Listens to everything that happens in the lab, keeps the event log, and builds a
    /// snapshot of the current state on demand (Capture). Positions are in table space,
    /// in metres, rounded to millimetres.
    /// </summary>
    public class LabStateTracker : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Rigidbody ring;
        [SerializeField] private Transform tableCenter;
        [SerializeField] private SpringScale springScale;
        [SerializeField] private LabObject springScaleObject;
        [SerializeField] private Pulley[] pulleys;
        [SerializeField] private LabObject[] labObjects;
        [SerializeField] private LabStepTracker steps;

        [Header("Event log")]
        [SerializeField] private int logCapacity = 50;
        [SerializeField] private int recentEventCount = 10;
        [Tooltip("How often the ring position is checked for movement/seconds.")]
        [SerializeField] private float ringCheckSeconds = 0.25f;
        [Tooltip("The ring must move this far in meters before a ring_moved event is logged.")]
        [SerializeField] private float ringMoveThresholdMetres = 0.02f;

        private EventLog _log;
        private string _heldObjectId = string.Empty;
        private string _selectedObjectId = string.Empty;
        private Vector3 _lastLoggedRingPosition;
        private float _nextRingCheckTime;

        private void Awake()
        {
            _log = new EventLog(logCapacity);
            _lastLoggedRingPosition = ring.position;
        }

        /// <summary>
        /// OnEnable and OnDisable call the same code with opposite flags, making sure no event
        /// is left connected after the object is disabled.
        /// </summary>
        private void OnEnable() => SetSubscriptions(true);

        private void OnDisable() => SetSubscriptions(false);

        private void Update()
        {
            if (Time.time < _nextRingCheckTime) return;

            _nextRingCheckTime = Time.time + ringCheckSeconds;
            LogRingIfMoved();
        }

        /// <summary>
        /// Adds an event from outside this class (e.g. a recorded measurement).
        /// </summary>
        public void RecordEvent(string type, string subject, string detail = "") =>
            _log.Add(type, subject, detail);

        /// <summary>
        /// Builds a snapshot of the lab right now, aka. building the whole payload from current
        /// values. And since it returns a fresh object each call. Callers can edit it freely.
        /// </summary>
        public LabStatePayload Capture()
        {
            return new LabStatePayload
            {
                t_ms = SessionClock.NowMs,
                current_step = steps.CurrentStepId,
                previous_step = steps.PreviousStepId,
                previous_action = LatestEventOrEmpty(),
                held_object = _heldObjectId,
                selected_object = _selectedObjectId,
                ring_position = ToTableSpace(ring.position),
                pulleys = CapturePulleys(),
                masses = CaptureMasses(),
                spring_scale = CaptureSpringScale(),
                recent_events = _log.SnapshotLast(recentEventCount)
            };
        }

        /// <summary>
        /// Subscribes or unsubscribes to every event through one entry point.
        /// </summary>
        private void SetSubscriptions(bool subscribe)
        {
            foreach (LabObject labObject in labObjects) SubscribeToLabObject(labObject, subscribe);
            foreach (Pulley pulley in pulleys) SubscribeToPulley(pulley, subscribe);

            if (subscribe) steps.StepChanged += OnStepChanged;
            else steps.StepChanged -= OnStepChanged;
        }

        private void SubscribeToLabObject(LabObject labObject, bool subscribe)
        {
            if (subscribe)
            {
                labObject.HoverChanged += OnHoverChanged;
                labObject.HeldChanged += OnHeldChanged;
                labObject.Clicked += OnClicked;
                return;
            }

            labObject.HoverChanged -= OnHoverChanged;
            labObject.HeldChanged -= OnHeldChanged;
            labObject.Clicked -= OnClicked;
        }

        private void SubscribeToPulley(Pulley pulley, bool subscribe)
        {
            if (subscribe)
            {
                pulley.AngleChanged += OnPulleyAngleChanged;
                pulley.MassChanged += OnMassChanged;
                return;
            }

            pulley.AngleChanged -= OnPulleyAngleChanged;
            pulley.MassChanged -= OnMassChanged;
        }

        private void OnHoverChanged(LabObject labObject, bool hovered)
        {
            if (hovered) _selectedObjectId = labObject.ObjectId;
        }

        /// <summary>
        /// Sets or clears held_object and logs grab/release.
        /// </summary>
        private void OnHeldChanged(LabObject labObject, bool held)
        {
            _heldObjectId = held ? labObject.ObjectId : string.Empty;
            if (held) _selectedObjectId = labObject.ObjectId;
            _log.Add(held ? LabEventTypes.Grab : LabEventTypes.Release, labObject.ObjectId);
        }

        private void OnClicked(LabObject labObject)
        {
            _selectedObjectId = labObject.ObjectId;
            _log.Add(LabEventTypes.Click, labObject.ObjectId);
        }

        private void OnPulleyAngleChanged(Pulley pulley) =>
            _log.Add(LabEventTypes.PulleyAngleChanged, pulley.ObjectId, Format(pulley.AngleDegrees));

        private void OnMassChanged(Pulley pulley) =>
            _log.Add(LabEventTypes.MassChanged, pulley.MassObjectId, Format(pulley.MassKg));

        private void OnStepChanged(string previousId, string currentId) =>
            _log.Add(LabEventTypes.StepChanged, "lab", $"{previousId}->{currentId}");

        /// <summary>
        /// Checks four times a second. Logs only after a 2 cm move since a moving ring would otherwise log
        /// ~50 events per second and flush the log.
        /// </summary>
        private void LogRingIfMoved()
        {
            Vector3 moved = ring.position - _lastLoggedRingPosition;
            if (moved.sqrMagnitude < ringMoveThresholdMetres * ringMoveThresholdMetres) return;

            _lastLoggedRingPosition = ring.position;
            Vector3 tablePosition = ToTableSpace(ring.position);
            _log.Add(LabEventTypes.RingMoved, "ring", $"{Format(tablePosition.x)},{Format(tablePosition.z)}");
        }

        private EventRecord LatestEventOrEmpty() =>
            _log.TryGetLatest(out EventRecord latest) ? latest : new EventRecord();

        private PulleyState[] CapturePulleys()
        {
            var states = new PulleyState[pulleys.Length];
            for (int i = 0; i < pulleys.Length; i++)
            {
                states[i] = new PulleyState
                {
                    id = pulleys[i].ObjectId,
                    object_position = ToTableSpace(pulleys[i].transform.position),
                    angle_deg = pulleys[i].AngleDegrees
                };
            }
            return states;
        }

        private MassState[] CaptureMasses()
        {
            var states = new MassState[pulleys.Length];
            for (int i = 0; i < pulleys.Length; i++)
            {
                states[i] = new MassState
                {
                    id = pulleys[i].MassObjectId,
                    object_position = ToTableSpace(pulleys[i].MassPosition),
                    mass_value = pulleys[i].MassKg
                };
            }
            return states;
        }

        private ScaleState CaptureSpringScale() => new ScaleState
        {
            id = springScaleObject.ObjectId,
            object_position = ToTableSpace(springScale.transform.position),
            reading_n = springScale.ReadingNewtons
        };

        /// <summary>
        /// World position to table-relative, rounded to 1 mm.
        /// </summary>
        private Vector3 ToTableSpace(Vector3 worldPosition) =>
            Round(tableCenter.InverseTransformPoint(worldPosition));

        private static Vector3 Round(Vector3 value) =>
            new Vector3(RoundToMillimetre(value.x), RoundToMillimetre(value.y), RoundToMillimetre(value.z));

        private static float RoundToMillimetre(float metres) => Mathf.Round(metres * 1000f) / 1000f;

        // Invariant culture keeps "0.2" from becoming "0,2" on some foreign browser.
        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}