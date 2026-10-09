using System.Text;
using PhysicsLab.Core.Web;
using UnityEngine;
using static PhysicsLab.Core.Telemetry.CsvFormat;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Turns recorded data into CSV files the user can open in Excel.
    /// </summary>
    public class ReportExporter : MonoBehaviour
    {
        private const string MeasurementHeader =
            "t_ms,step_id,pulley1_angle_deg,pulley1_mass_kg,pulley2_angle_deg,pulley2_mass_kg," +
            "spring_reading_n,ring_offset_mm";

        [SerializeField] private MeasurementRecorder recorder;
        [SerializeField] private LabAssistant assistant;

        /// <summary>
        /// Student report: one row per recorded measurement.
        /// </summary>
        public void ExportMeasurements() =>
            BrowserBridge.SaveTextFile("lab-measurements.csv", "text/csv", BuildMeasurementsCsv());

        /// <summary>
        /// Instructor export: one row per question asked, with timings.
        /// </summary>
        public void ExportExperimentLog() =>
            BrowserBridge.SaveTextFile("experiment-log.csv", "text/csv", assistant.Log.ToCsv());

        private string BuildMeasurementsCsv()
        {
            var csv = new StringBuilder(MeasurementHeader).Append('\n');
            foreach (MeasurementRow row in recorder.Rows)
            {
                csv.Append(ToCsvLine(row)).Append('\n');
            }
            return csv.ToString();
        }

        private static string ToCsvLine(MeasurementRow row) => string.Join(",",
            Number(row.t_ms), Text(row.step_id),
            Number(row.pulley1_angle_deg), Number(row.pulley1_mass_kg),
            Number(row.pulley2_angle_deg), Number(row.pulley2_mass_kg),
            Number(row.spring_reading_n), Number(row.ring_offset_mm));
    }
}