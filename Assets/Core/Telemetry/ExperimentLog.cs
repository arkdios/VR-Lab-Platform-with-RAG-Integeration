using System;
using System.Collections.Generic;
using System.Text;
using static PhysicsLab.Core.Telemetry.CsvFormat;

namespace PhysicsLab.Core.Telemetry
{
    /// <summary>
    /// One row of the experiment results. One row per question asked.
    /// </summary>
    [Serializable]
    public class ExperimentRecord
    {
        public string session_id;
        public int request_id;
        public string condition;
        public string question_type;
        public string status;
        public long http_code;
        public long request_started_ms;
        public float context_build_ms; // client: build state + convert to JSON
        public float round_trip_ms; // client: request sent until answer received
        public float server_retrieval_ms; // reported by the server
        public float server_llm_ms; // reported by the server
        public float fps;
        public float frame_ms_avg;
        public float frame_ms_max;
        public int answer_chars;
        public string build_version;
        public string platform;
        public string gpu;
        public string user_agent;
    }

    /// <summary>
    /// Keeps experiment rows in memory and turns them into CSV (Excel opens it).
    /// </summary>
    public class ExperimentLog
    {
        private const string Header =
            "session_id,request_id,condition,question_type,status,http_code,request_started_ms," +
            "context_build_ms,round_trip_ms,server_retrieval_ms,server_llm_ms,fps,frame_ms_avg," +
            "frame_ms_max,answer_chars,build_version,platform,gpu,user_agent";

        private readonly List<ExperimentRecord> _records = new List<ExperimentRecord>();

        public int Count => _records.Count;

        public void Add(ExperimentRecord record) => _records.Add(record);

        public string ToCsv()
        {
            var csv = new StringBuilder(Header).Append('\n');
            foreach (ExperimentRecord record in _records) AppendRow(csv, record);
            return csv.ToString();
        }

        private static void AppendRow(StringBuilder csv, ExperimentRecord r)
        {
            string row = string.Join(",",
                Text(r.session_id), Number(r.request_id), Text(r.condition), Text(r.question_type),
                Text(r.status), Number(r.http_code), Number(r.request_started_ms),
                Number(r.context_build_ms), Number(r.round_trip_ms), Number(r.server_retrieval_ms),
                Number(r.server_llm_ms), Number(r.fps), Number(r.frame_ms_avg), Number(r.frame_ms_max),
                Number(r.answer_chars), Text(r.build_version), Text(r.platform), Text(r.gpu),
                Text(r.user_agent));
            csv.Append(row).Append('\n');
        }
    }
}