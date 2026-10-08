using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PhysicsLab.Core.Chat
{
    public enum ChatStatus { Success, NetworkError, HttpError, Timeout, BadResponse }

    /// <summary>
    /// Outcome of one request, the body is empty unless the server answered.
    /// </summary>
    public readonly struct ChatResult
    {
        public ChatResult(ChatStatus status, string body, long httpCode)
        {
            Status = status;
            Body = body;
            HttpCode = httpCode;
        }

        public ChatStatus Status { get; }
        public string Body { get; }
        public long HttpCode { get; }
    }

    /// <summary>
    /// Sends one JSON document to the backend without blocking the frame loop. The result
    /// comes back through a callback. Failures are reported, never hidden.
    /// </summary>
    public class ChatClient : MonoBehaviour
    {
        [SerializeField] private string endpointUrl = "https://localhost:8001/ask";
        [SerializeField] private float timeoutSeconds = 20f;

        private void Awake()
        {
            WarnIfMixedContent();
        }

        public void Post(string json, Action<ChatResult> onDone)
        {
            StartCoroutine(PostRoutine(json, onDone));
        }

        private IEnumerator PostRoutine(string json, Action<ChatResult> onDone)
        {
            using UnityWebRequest request = BuildJsonRequest(json);
            float startTime = Time.realtimeSinceStartup;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    request.Abort();
                    onDone(new ChatResult(ChatStatus.Timeout, string.Empty, 0));
                    yield break;
                }
                yield return null; // wait one frame, keep rendering
            }

            onDone(ToChatResult(request));
        }

        private UnityWebRequest BuildJsonRequest(string json)
        {
            var request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        /// <summary>
        /// Maps Unity's result enum to ours, "default" covers connection errors. Since callers never touch
        /// UnityWebRequest, so tests and UI stay simple.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static ChatResult ToChatResult(UnityWebRequest request)
        {
            string body = request.downloadHandler.text;
            switch (request.result)
            {
                case UnityWebRequest.Result.Success:
                    return new ChatResult(ChatStatus.Success, body, request.responseCode);
                case UnityWebRequest.Result.ProtocolError:
                    return new ChatResult(ChatStatus.HttpError, body, request.responseCode);
                case UnityWebRequest.Result.DataProcessingError:
                    return new ChatResult(ChatStatus.BadResponse, body, request.responseCode);
                default:
                    return new ChatResult(ChatStatus.NetworkError, string.Empty, 0);
            }
        }

        /// <summary>
        /// A page served over HTTPS cannot call an http:// address. The browser blocks it silently.
        /// Because a blocked request gives a vague network error, a warning log could saves hours.
        /// </summary>
        private void WarnIfMixedContent()
        {
            bool pageIsHttps = Application.absoluteURL.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool endpointIsHttp = endpointUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            if (pageIsHttps && endpointIsHttp)
            {
                Debug.LogWarning("[ChatClient] HTTPS page with an http:// endpoint. The browser will block it.", this);
            }
        }
    }
}