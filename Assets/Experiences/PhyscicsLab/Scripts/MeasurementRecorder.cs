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
            MeasurementRow row = ToRow(snapshot);
            _rows.Add(row);

            string reading = row.spring_reading_n.ToString("0.###", CultureInfo.InvariantCulture);
            stateTracker.RecordEvent(LabEventTypes.MeasurementRecorded, snapshot.spring_scale.id, reading);
            MeasurementAdded?.Invoke(row);
        }

        private static MeasurementRow ToRow(LabStatePayload snapshot)
        {
            if (snapshot.pulleys.Length < 2)
            {
                throw new InvalidOperationException("The report columns need two pulleys.");
            }

            PulleyState first = snapshot.pulleys[0];
            PulleyState second = snapshot.pulleys[1];
            return new MeasurementRow
            {
                t_ms = snapshot.t_ms,
                step_id = snapshot.current_step,
                pulley1_angle_deg = first.angle_deg,
                pulley1_mass_kg = snapshot.masses[0].mass_value,
                pulley2_angle_deg = second.angle_deg,
                pulley2_mass_kg = snapshot.masses[1].mass_value,
                spring_reading_n = snapshot.spring_scale.reading_n,
                ring_offset_mm = RingOffsetMillimetres(snapshot.ring_position)
            };
        }

        private static float RingOffsetMillimetres(Vector3 ringPosition) =>
            new Vector2(ringPosition.x, ringPosition.z).magnitude * 1000f;
    }
}