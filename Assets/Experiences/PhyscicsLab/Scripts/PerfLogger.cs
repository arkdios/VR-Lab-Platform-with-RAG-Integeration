using System;
using System.Collections.Generic;
using System.Text;
using PhysicsLab.Core.Telemetry;
using UnityEngine;
using static PhysicsLab.Core.Telemetry.CsvFormat;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// One row of frame statistics. Field names become the CSV columns.
    /// Plain data class, snake_case fields.
    /// Same convention as EventRecord and ExperimentRecord.
    /// </summary>
    [Serializable]
    public class PerfRow
    {
        public long t_ms;
        public float fps;
        public float frame_ms_avg;
        public float frame_ms_max;
        public int request_in_flight; // 1 if a chat request was pending at any frame of this window
    }

    /// <summary>
    /// Keeps one row of frame statistics per second, and marks the seconds in which a chat
    /// request was pending. Comparing those rows with the others shows what the RAG round trip
    /// costs in the headset [JOHN: FPS and frame time are part of the secondary evaluation].
    /// </summary>
    public class PerfLogger : MonoBehaviour
    {
        private const string Header = "t_ms,fps,frame_ms_avg,frame_ms_max,request_in_flight,build_version";

        [SerializeField] private PerfSampler sampler;
        [SerializeField] private LabAssistant assistant;

        [Tooltip("Rows kept in memory. 3600 = one hour at one row per second.")]
        [SerializeField] private int maxRows = 3600; // Stops adding rows after 3600.

        private readonly List<PerfRow> _rows = new List<PerfRow>();
        private bool _requestSeenInWindow;
        private bool _fullWarningShown;

        public int Count => _rows.Count;

        private void OnEnable() => sampler.WindowPublished += OnWindowPublished;

        private void OnDisable() => sampler.WindowPublished -= OnWindowPublished;

        private void Update()
        {
            if (assistant.IsBusy) _requestSeenInWindow = true;
        }

        public string ToCsv()
        {
            var csv = new StringBuilder(Header).Append('\n');
            foreach (PerfRow row in _rows) csv.Append(ToCsvLine(row)).Append('\n');
            return csv.ToString();
        }

        /// <summary>
        /// Adds a row, then clears the flag.
        /// </summary>
        private void OnWindowPublished()
        {
            if (_rows.Count >= maxRows)
            {
                WarnOnceThatLogIsFull();
                return;
            }

            _rows.Add(new PerfRow
            {
                t_ms = SessionClock.NowMs,
                fps = sampler.Fps,
                frame_ms_avg = sampler.FrameMsAverage,
                frame_ms_max = sampler.FrameMsMax,
                request_in_flight = _requestSeenInWindow ? 1 : 0
            });
            _requestSeenInWindow = false;
        }

        private void WarnOnceThatLogIsFull()
        {
            if (_fullWarningShown) return;

            _fullWarningShown = true;
            Debug.LogWarning($"[PerfLogger] {maxRows} rows reached. Later windows are not logged.", this);
        }

        private static string ToCsvLine(PerfRow row) => string.Join(",",
            Number(row.t_ms), Number(row.fps), Number(row.frame_ms_avg), Number(row.frame_ms_max),
            Number(row.request_in_flight), Text(Application.version));
    }
}