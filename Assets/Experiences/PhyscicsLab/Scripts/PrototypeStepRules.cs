using System;
using PhysicsLab.Lab.Connect;
using PhysicsLab.Lab.Rope;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The PHYS 124 Lab 5 step rules. Ten times a second it reads the lab (pulleys, masses, scale, ring),
    /// asks Lab5Judge which checks of the current step fail, and tells LabStepTracker the result.
    /// A step is done when its checks pass, not when the student grabs something.
    /// It never moves a step forward by itself: the student presses Next step once the step is done.
    /// The class keeps its old name so the scene still finds it. Fields it needs and the scene does not
    /// hold yet are found when the scene starts.
    /// </summary>
    public class PrototypeStepRules : MonoBehaviour
    {
        [SerializeField] private LabStepTracker steps;
        [SerializeField] private LabObject ringObject;
        [SerializeField] private LabObject springScaleObject;
        [Tooltip("Optional. Found automatically when empty.")]
        [SerializeField] private MeasurementRecorder recorder;
        [SerializeField] private Lab5Tolerances tolerances = new Lab5Tolerances();
        [Tooltip("How often the checks run, in seconds.")]
        [SerializeField] private float evaluateEverySeconds = 0.1f;

        private readonly LabReading _reading = new LabReading();
        private readonly StepCheckSet _checks = new StepCheckSet();
        private Pulley[] _pulleys;
        private ConnectPart[] _clamps;
        private Rigidbody _ringBody;
        private TableBounds _ringBounds;
        private SpringScale _scale;
        private RopeArm _scaleArm;
        private int _stringCount;
        private Lab5Judge _judge;
        private ReadingWindow _window;
        private StepMeasurement _measurement;   // the last equilibrium of the current case
        private float _nextEvaluateTime;

        private void Awake()
        {
            if (!TryResolveReferences())
            {
                enabled = false;
                return;
            }

            steps.Configure(Lab5Procedure.CaseCountFor(_pulleys.Length));
        }

        // Pulley ids and the string arms are ready only after every Awake has run, so the rest is built here.
        private void Start()
        {
            Array.Sort(_pulleys, (a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
            var ids = new string[_pulleys.Length];
            _clamps = new ConnectPart[_pulleys.Length];
            for (int i = 0; i < _pulleys.Length; i++)
            {
                ids[i] = _pulleys[i].ObjectId;
                _clamps[i] = _pulleys[i].GetComponent<ConnectPart>();
            }

            FindStrings();
            _judge = new Lab5Judge(tolerances, ids);
            _window = new ReadingWindow(WindowSampleCount());
            Evaluate();
        }

        private void OnEnable() => steps.StepChanged += OnStepChanged;

        private void OnDisable() => steps.StepChanged -= OnStepChanged;

        private void Update()
        {
            if (_judge == null || Time.time < _nextEvaluateTime) return;

            _nextEvaluateTime = Time.time + evaluateEverySeconds;
            Evaluate();
        }

        // A new step starts with fresh results, so a question asked right after Next step is not answered from the old step.
        private void OnStepChanged(string previousId, string currentId)
        {
            if (_judge != null) Evaluate();
        }

        private bool TryResolveReferences()
        {
            if (recorder == null) recorder = FindAnyObjectByType<MeasurementRecorder>();
            _pulleys = FindObjectsByType<Pulley>();
            _ringBody = ringObject != null ? ringObject.GetComponent<Rigidbody>() : null;
            _ringBounds = ringObject != null ? ringObject.GetComponent<TableBounds>() : null;
            _scale = springScaleObject != null ? springScaleObject.GetComponent<SpringScale>() : null;

            bool ready = steps != null && recorder != null && _pulleys.Length > 0
                         && _ringBody != null && _ringBounds != null && _scale != null;
            if (!ready) Debug.LogError("[PrototypeStepRules] Steps, ring, spring scale, recorder or pulleys are missing.", this);
            return ready;
        }

        private void FindStrings()
        {
            Rigidbody scaleBody = _scale.GetComponent<Rigidbody>();
            foreach (RopeArm arm in FindObjectsByType<RopeArm>())
            {
                if (arm.Link == null) continue; // the arm turned itself off because its setup is wrong

                _stringCount++;
                if (arm.EndBody == scaleBody) _scaleArm = arm;
            }
        }

        private int WindowSampleCount() =>
            Mathf.Max(2, Mathf.CeilToInt(tolerances.stableSeconds / evaluateEverySeconds));

        // ---------- one tick ----------

        private void Evaluate()
        {
            Lab5Step step = steps.CurrentStep;
            FillReading(step);
            _judge.Evaluate(step, _reading, _checks);
            UpdateMeasurement(step);
            _reading.MeasurementReady = HasMeasurementFor(step);
            steps.Publish(IsComplete(step), _checks, _measurement);
        }

        // Equilibrium is done once a measurement was held, even if the ring moves afterwards.
        // Every other step is done while all of its checks pass.
        private bool IsComplete(Lab5Step step) =>
            step.Stage == Lab5Stage.Equilibrium ? _reading.MeasurementReady : _checks.FailedCount == 0;

        // The student cannot hold the scale and press a button at the same time on a desktop, so the numbers
        // are kept at the moment of equilibrium. Record saves these numbers, not whatever the scale shows later.
        // Changing a pulley or a mass throws them away.
        private void UpdateMeasurement(Lab5Step step)
        {
            if (step.Stage != Lab5Stage.Equilibrium && step.Stage != Lab5Stage.Record) return;

            if (!_judge.ConfigPasses(step.CaseIndex, _reading))
            {
                _measurement = StepMeasurement.None;
                return;
            }
            if (_judge.EquilibriumHolds(_reading)) _measurement = _judge.MeasureEquilibrium(_reading, step.CaseId);
        }

        private bool HasMeasurementFor(Lab5Step step) =>
            _measurement.IsValid && _measurement.CaseId == step.CaseId;

        // ---------- reading the lab ----------

        private void FillReading(Lab5Step step)
        {
            FillPulleys();
            FillScale();
            FillRing();
            _reading.ValidRecordsForCase = step.HasCase ? CountValidRecords(step.CaseId) : 0;
            _reading.MeasurementReady = HasMeasurementFor(step);
        }

        private void FillPulleys()
        {
            _reading.PulleyCount = _pulleys.Length;
            for (int i = 0; i < _pulleys.Length; i++)
            {
                _reading.Pulleys[i] = new PulleyReading
                {
                    AngleDeg = _pulleys[i].AngleDegrees,
                    MassKg = _pulleys[i].MassKg,
                    Clamped = _clamps[i] != null && _clamps[i].IsConnected
                };
            }
        }

        private void FillScale()
        {
            _reading.StringsConnected = _stringCount > _pulleys.Length && _scaleArm != null;
            _reading.ScaleAttached = _scaleArm != null;
            _reading.ScaleReadingN = _scale.ReadingNewtons;
            _reading.ScaleAngleDeg = _scale.StringAngleDegrees;
            _reading.ScaleStringLengthM = _scaleArm != null ? _scaleArm.LengthMetres : 0f;
        }

        private void FillRing()
        {
            Vector3 offset = _ringBody.position - _ringBounds.Center;
            offset.y = 0f; // only the position along the table counts
            _window.Push(offset.x, offset.z, _reading.ScaleReadingN);

            _reading.RingOffsetM = offset.magnitude;
            _reading.RingSpreadM = _window.RingSpread;
            _reading.ScaleSpreadN = _window.ScaleSpread;
            _reading.WindowFull = _window.IsFull;
        }

        // An indexed loop, because foreach over a list interface makes garbage ten times a second.
        private int CountValidRecords(string caseId)
        {
            var rows = recorder.Rows;
            int count = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].valid && rows[i].case_id == caseId) count++;
            }
            return count;
        }
    }
}