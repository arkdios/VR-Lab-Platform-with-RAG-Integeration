using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// One recorded measurement. Field names become the CSV columns.
    /// </summary>
    [Serializable]
    public class MeasurementRow
    {
        public long t_ms;
        public string step_id;
        public float pulley1_angle_deg;
        public float pulley1_mass_kg;
        public float pulley2_angle_deg;
        public float pulley2_mass_kg;
        public float spring_reading_n;
        public float ring_offset_mm;
        public string case_id;           // A to D
        public float pulley3_angle_deg;  // 0 when the lab has two pulleys
        public float pulley3_mass_kg;
        public float scale_angle_deg;    // angle of the scale string, counter-clockwise like the pulleys
        public float force_error_n;      // uncertainty of the scale reading
        public float angle_error_deg;    // uncertainty of the scale angle
        public bool valid;               // taken at equilibrium with the right pulleys and masses
    }

    /// <summary>
    /// Records a measurement when the student presses the button. The values are read by the
    /// program, not typed in, so nobody has to enter numbers inside the headset.
    /// </summary>
    public class MeasurementRecorder : MonoBehaviour
    {
        [SerializeField] private LabStateTracker stateTracker;

        private readonly List<MeasurementRow> _rows = new List<MeasurementRow>();

        public IReadOnlyList<MeasurementRow> Rows => _rows;

        public event Action<MeasurementRow> MeasurementAdded;

        public void Record()
        {
            LabStatePayload snapshot = stateTracker.Capture();
            MeasurementRow row = ToRow(snapshot, stateTracker.CurrentMeasurement);
            _rows.Add(row);

            string reading = row.spring_reading_n.ToString("0.###", CultureInfo.InvariantCulture);
            stateTracker.RecordEvent(LabEventTypes.MeasurementRecorded, snapshot.spring_scale.id, reading);
            MeasurementAdded?.Invoke(row);
        }

        // Uses the numbers held at equilibrium when there are some for this case. Without them the row shows
        // what the lab shows now, with no uncertainty, and is marked not valid.
        private static MeasurementRow ToRow(LabStatePayload snapshot, StepMeasurement held)
        {
            if (snapshot.pulleys.Length < 2)
            {
                throw new InvalidOperationException("The report columns need two pulleys.");
            }

            bool valid = held.IsValid && held.CaseId == snapshot.case_id;
            var row = new MeasurementRow
            {
                t_ms = snapshot.t_ms,
                step_id = snapshot.current_step,
                case_id = snapshot.case_id,
                pulley1_angle_deg = snapshot.pulleys[0].angle_deg,
                pulley1_mass_kg = snapshot.masses[0].mass_value,
                pulley2_angle_deg = snapshot.pulleys[1].angle_deg,
                pulley2_mass_kg = snapshot.masses[1].mass_value,
                valid = valid
            };
            AddThirdPulley(row, snapshot);
            AddScaleValues(row, snapshot, valid ? held : StepMeasurement.None);
            return row;
        }

        private static void AddThirdPulley(MeasurementRow row, LabStatePayload snapshot)
        {
            if (snapshot.pulleys.Length < 3) return;

            row.pulley3_angle_deg = snapshot.pulleys[2].angle_deg;
            row.pulley3_mass_kg = snapshot.masses[2].mass_value;
        }

        // The held numbers win. With none, the scale values are read live and the errors stay 0.
        private static void AddScaleValues(MeasurementRow row, LabStatePayload snapshot, StepMeasurement held)
        {
            if (held.IsValid)
            {
                row.spring_reading_n = held.ForceN;
                row.scale_angle_deg = held.AngleDeg;
                row.force_error_n = held.ForceErrorN;
                row.angle_error_deg = held.AngleErrorDeg;
                row.ring_offset_mm = held.RingOffsetMm;
                return;
            }

            row.spring_reading_n = snapshot.spring_scale.reading_n;
            row.scale_angle_deg = snapshot.spring_scale.angle_deg;
            row.ring_offset_mm = RingOffsetMillimetres(snapshot.ring_position);
        }

        private static float RingOffsetMillimetres(Vector3 ringPosition) =>
            new Vector2(ringPosition.x, ringPosition.z).magnitude * 1000f;
    }
}