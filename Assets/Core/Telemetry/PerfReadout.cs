using PhysicsLab.Core.Web;
using TMPro;
using UnityEngine;

namespace PhysicsLab.Core.Telemetry
{
    /// <summary>
    /// Small text label with frame rate and frame time. Hidden unless the page URL contains
    /// ?perf=1 (or in the Editor), so participants never see it. The text changes once per
    /// sampler window, not every frame, to keep the readout itself cheap.
    /// </summary>
    public class PerfReadout : MonoBehaviour
    {
        [SerializeField] private PerfSampler sampler;
        [SerializeField] private TMP_Text label;

        [Tooltip("Frame budget in milliseconds. 72 fps = 13.9, 90 fps = 11.1. The text turns red above it.")]
        [SerializeField] private float budgetMs = 13.9f; // Colors the text red when the average frame is over budget.

        [SerializeField] private bool showInEditor = true;

        /// <summary>
        /// Shows the label only for ?perf=1 or in the Editor.
        /// </summary>
        private void Awake()
        {
            bool requestedByUrl = UrlParameters.TryGet("perf", out string value) && value == "1";
            label.gameObject.SetActive(requestedByUrl || (Application.isEditor && showInEditor));
        }

        private void OnEnable() => sampler.WindowPublished += Refresh;

        private void OnDisable() => sampler.WindowPublished -= Refresh;

        private void Refresh()
        {
            if (!label.gameObject.activeSelf) return;

            label.text = $"{sampler.Fps:0} fps\n{sampler.FrameMsAverage:0.0} ms avg\n{sampler.FrameMsMax:0.0} ms max";
            label.color = sampler.FrameMsAverage > budgetMs ? Color.red : Color.white;
        }
    }
}