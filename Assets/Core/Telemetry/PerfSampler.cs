using UnityEngine;

namespace PhysicsLab.Core.Telemetry
{
    /// <summary>
    /// Measures frame rate and frame time over a sliding one-second window. Allocates nothing,
    /// so it does not disturb the numbers it measures.
    /// </summary>
    public class PerfSampler : MonoBehaviour
    {
        [SerializeField] private float windowSeconds = 1f;

        private float _elapsedSeconds;
        private int _frameCount;
        private float _longestFrameSeconds;

        public float Fps { get; private set; }
        public float FrameMsAverage { get; private set; }
        public float FrameMsMax { get; private set; }

        private void Update()
        {
            float frameSeconds = Time.unscaledDeltaTime;
            _elapsedSeconds += frameSeconds;
            _frameCount++;
            _longestFrameSeconds = Mathf.Max(_longestFrameSeconds, frameSeconds);

            if (_elapsedSeconds >= windowSeconds) PublishWindow();
        }

        private void PublishWindow()
        {
            Fps = _frameCount / _elapsedSeconds;
            FrameMsAverage = _elapsedSeconds / _frameCount * 1000f;
            FrameMsMax = _longestFrameSeconds * 1000f;

            _elapsedSeconds = 0f;
            _frameCount = 0;
            _longestFrameSeconds = 0f;
        }
    }
}