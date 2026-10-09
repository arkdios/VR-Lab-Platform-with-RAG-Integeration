using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Student HUD: step name, spring reading, measurement count, and three buttons.
    /// </summary>
    public class HudPresenter : MonoBehaviour
    {
        [SerializeField] private LabStepTracker steps;
        [SerializeField] private SpringScale springScale;
        [SerializeField] private MeasurementRecorder recorder;
        [SerializeField] private ReportExporter exporter;

        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text readingLabel;
        [SerializeField] private TMP_Text measurementCountLabel;
        [SerializeField] private Button nextStepButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button exportButton;

        [Tooltip("How often the reading text refreshes. Less often means fewer string allocations.")]
        [SerializeField] private float readingRefreshSeconds = 0.1f;

        private float _nextReadingRefresh;

        private void Awake()
        {
            nextStepButton.onClick.AddListener(OnNextStepPressed);
            recordButton.onClick.AddListener(recorder.Record);
            exportButton.onClick.AddListener(exporter.ExportMeasurements);
        }

        private void OnEnable()
        {
            steps.StepChanged += OnStepChanged;
            recorder.MeasurementAdded += OnMeasurementAdded;
            RefreshStep();
            RefreshMeasurementCount();
        }

        private void OnDisable()
        {
            steps.StepChanged -= OnStepChanged;
            recorder.MeasurementAdded -= OnMeasurementAdded;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextReadingRefresh) return;

            _nextReadingRefresh = Time.unscaledTime + readingRefreshSeconds;
            readingLabel.text = $"Spring scale: {springScale.ReadingNewtons:0.00} N";
        }

        private void OnNextStepPressed() => steps.TryAdvance();

        private void OnStepChanged(string previousId, string currentId) => RefreshStep();

        private void OnMeasurementAdded(MeasurementRow row) => RefreshMeasurementCount();

        private void RefreshStep()
        {
            stepLabel.text = $"Step: {steps.CurrentStepId}";
            nextStepButton.interactable = !steps.IsLastStep;
        }

        private void RefreshMeasurementCount() =>
            measurementCountLabel.text = $"Measurements: {recorder.Rows.Count}";
    }
}