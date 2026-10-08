using System;
using PhysicsLab.Core.Chat;
using PhysicsLab.Core.Telemetry;
using PhysicsLab.Core.Web;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// What the UI needs to show after a question, aka. the answer text and how it went.
    /// </summary>
    public readonly struct AssistantReply
    {
        public AssistantReply(string text, ChatStatus status)
        {
            Text = text ?? string.Empty;
            Status = status;
        }

        public string Text { get; }
        public ChatStatus Status { get; }
    }

    /// <summary>
    /// Shape of the backend's answer.
    /// </summary>
    [Serializable]
    public class AssistantResponse
    {
        public string answer;
        public float retrieval_ms;
        public float llm_ms;
    }

    /// <summary>
    /// Asks the backend a question with the current lab state attached, then records timing.
    /// One question at a time. The lab keeps running while the request is in flight.
    /// </summary>
    public class LabAssistant : MonoBehaviour
    {
        [SerializeField] private LabStateTracker stateTracker;
        [SerializeField] private ChatClient chatClient;
        [SerializeField] private PerfSampler perfSampler;
        [SerializeField] private ExperimentCondition condition = ExperimentCondition.StateAndHistory;

        private readonly ExperimentLog _experimentLog = new ExperimentLog();
        private string _sessionId;
        private int _requestCounter;

        // IsBusy ignores a second question while one is pending, since one request at a time keeps the
        // timing clean and avoids answers arriving out of order.
        public bool IsBusy { get; private set; }
        public ExperimentLog Log => _experimentLog;

        public ExperimentCondition Condition
        {
            get => condition;
            set => condition = value;
        }

        public event Action RequestStarted;
        public event Action<AssistantReply> ReplyReady;

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
        }

        public void Ask(QuestionType question)
        {
            if (IsBusy) return;

            float buildStart = Time.realtimeSinceStartup;
            string json = BuildRequestJson(question);
            float buildMs = (Time.realtimeSinceStartup - buildStart) * 1000f;

            // PayloadGuard before sending to refuse to send a payload with a forbidden key.
            // Prevent leaking answers
            if (PayloadGuard.TryFindForbiddenKey(json, out string forbiddenKey))
            {
                Debug.LogError($"[LabAssistant] Payload contains '{forbiddenKey}'. Request not sent.", this);
                return;
            }

            Send(json, new PendingRequest(question, _requestCounter, buildMs));
        }

        private void Send(string json, PendingRequest pending)
        {
            IsBusy = true;
            RequestStarted?.Invoke();
            chatClient.Post(json, result => OnResult(pending, result));
        }

        private string BuildRequestJson(QuestionType question)
        {
            LabStatePayload payload = stateTracker.Capture();
            payload.session_id = _sessionId;
            payload.request_id = ++_requestCounter;
            payload.condition = WireNames.Of(condition);
            payload.question_type = WireNames.Of(question);
            payload.question = WireNames.QuestionText(question);
            payload.perf = new PerfState
            {
                fps = perfSampler.Fps,
                frame_ms_avg = perfSampler.FrameMsAverage,
                frame_ms_max = perfSampler.FrameMsMax
            };
            return JsonUtility.ToJson(payload);
        }

        private void OnResult(PendingRequest pending, ChatResult result)
        {
            IsBusy = false;
            ChatStatus status = ParseAnswer(result, out AssistantResponse response);
            _experimentLog.Add(CreateRecord(pending, result, response, status));
            ReplyReady?.Invoke(new AssistantReply(response.answer, status));
        }

        /// <summary>
        /// No backend yet, so the chatbot's reply is auto-validated before use.
        /// It catches only ArgumentException (aka. invalid JSON). Replaces missing fields with
        /// empty ones, then rejects an empty answer.
        /// </summary>
        private static ChatStatus ParseAnswer(ChatResult result, out AssistantResponse response)
        {
            response = new AssistantResponse { answer = string.Empty };
            if (result.Status != ChatStatus.Success) return result.Status;

            try
            {
                response = JsonUtility.FromJson<AssistantResponse>(result.Body);
            }
            catch (ArgumentException)
            {
                return ChatStatus.BadResponse; // the body was not valid JSON
            }

            response ??= new AssistantResponse();
            response.answer ??= string.Empty;
            return string.IsNullOrWhiteSpace(response.answer) ? ChatStatus.BadResponse : ChatStatus.Success;
        }

        /// <summary>
        /// Both this method and FillPerformanceAndEnvironment combine to build the CSV row.
        /// </summary>
        private ExperimentRecord CreateRecord(
            PendingRequest pending, ChatResult result, AssistantResponse response, ChatStatus status)
        {
            var record = new ExperimentRecord
            {
                session_id = _sessionId,
                request_id = pending.RequestId,
                condition = WireNames.Of(condition),
                question_type = WireNames.Of(pending.Question),
                status = status.ToString(),
                http_code = result.HttpCode,
                request_started_ms = pending.StartedMs,
                context_build_ms = pending.BuildMs, // Covers state capture and JSON creation. It is a different cost from the network wait.
                round_trip_ms = (Time.realtimeSinceStartup - pending.SentTime) * 1000f,
                server_retrieval_ms = response?.retrieval_ms ?? 0f,
                server_llm_ms = response?.llm_ms ?? 0f,
                answer_chars = response?.answer?.Length ?? 0
            };
            FillPerformanceAndEnvironment(record);
            return record;
        }

        private void FillPerformanceAndEnvironment(ExperimentRecord record)
        {
            record.fps = perfSampler.Fps;
            record.frame_ms_avg = perfSampler.FrameMsAverage;
            record.frame_ms_max = perfSampler.FrameMsMax;
            record.build_version = Application.version;
            record.platform = Application.platform.ToString();
            record.gpu = SystemInfo.graphicsDeviceName;
            record.user_agent = BrowserBridge.UserAgent;
        }

        /// <summary>
        /// Values captured when a request starts, needed again when its answer arrives.
        /// This method allows chatbot to remember question, id and start times.
        /// </summary>
        private readonly struct PendingRequest
        {
            public PendingRequest(QuestionType question, int requestId, float buildMs)
            {
                Question = question;
                RequestId = requestId;
                BuildMs = buildMs;
                StartedMs = SessionClock.NowMs;
                SentTime = Time.realtimeSinceStartup;
            }

            public QuestionType Question { get; }
            public int RequestId { get; }
            public float BuildMs { get; }
            public long StartedMs { get; }
            public float SentTime { get; }
        }
    }
}