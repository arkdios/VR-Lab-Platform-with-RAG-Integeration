using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// The Finish button and the notices that go with it.
    /// Press Finish (on the student screen) and a notice asks "Finish the lab?". Press Yes and the timer stops and
    /// a second notice says the lab is complete, with the time used, the number of measurements and an Export button.
    /// When the timer reaches zero, the same second notice shows with "TIME IS UP".
    /// The notice frees the mouse on the desktop, because a locked mouse cannot press its buttons.
    /// Lives on an object that is always active (LabSession), so it works while the student screen is hidden.
    /// </summary>
    public class LabFinishController : MonoBehaviour
    {
        [SerializeField] private LabTimer timer;
        [SerializeField] private MeasurementRecorder recorder;
        [SerializeField] private ReportExporter exporter;

        [Header("Finish button on the student screen")]
        [SerializeField] private Button finishButton;
        [SerializeField] private TMP_Text finishButtonLabel;

        [Header("The notice")]
        [SerializeField] private GameObject noticeRoot;
        [SerializeField] private GameObject confirmGroup;
        [SerializeField] private GameObject doneGroup;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private TMP_Text doneTitle;
        [SerializeField] private TMP_Text doneText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button keepWorkingButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private Button closeButton;

        private bool _finished;   // the lab is over, by the Finish button or by the timer

        private void Awake()
        {
            finishButton.onClick.AddListener(AskToFinish);
            yesButton.onClick.AddListener(FinishLab);
            keepWorkingButton.onClick.AddListener(HideNotice);
            exportButton.onClick.AddListener(exporter.ExportMeasurements);
            closeButton.onClick.AddListener(HideNotice);
            noticeRoot.SetActive(false);
        }

        private void OnEnable() => timer.Expired += OnTimeUp;

        private void OnDisable() => timer.Expired -= OnTimeUp;

        // Asks first, because a headset user can press a button by accident.
        private void AskToFinish()
        {
            if (_finished) return;

            string timeLeft = LabTimer.FormatClock(Mathf.CeilToInt(timer.RemainingSeconds));
            confirmText.text = "Finish the lab?\n\n" +
                               $"Time left: {timeLeft}\n" +
                               $"Measurements recorded: {recorder.Rows.Count}";
            confirmGroup.SetActive(true);
            doneGroup.SetActive(false);
            ShowNotice();
        }

        private void FinishLab()
        {
            if (_finished) return;

            timer.Stop();
            ShowDone("LAB COMPLETE", "Well done! You finished the lab.");
        }

        private void OnTimeUp()
        {
            if (_finished) return;

            ShowDone("TIME IS UP", "The lab time is over.");
        }

        private void ShowDone(string title, string message)
        {
            _finished = true;
            finishButton.interactable = false;
            finishButtonLabel.text = "Finished";

            int usedSeconds = Mathf.RoundToInt(timer.UsedSeconds);
            doneTitle.text = title;
            doneText.text = message + "\n\n" +
                            $"Time used: {LabTimer.FormatClock(usedSeconds)}\n" +
                            $"Measurements recorded: {recorder.Rows.Count}\n\n" +
                            "Press Export to save your results as a CSV file.";
            confirmGroup.SetActive(false);
            doneGroup.SetActive(true);
            ShowNotice();
        }

        private void ShowNotice()
        {
            noticeRoot.SetActive(true);
            FreeMouse();
        }

        private void HideNotice() => noticeRoot.SetActive(false);

        // A locked mouse cannot press a button. Tab or a click in the room takes the mouse back (DesktopRigController).
        // In the headset the mouse is never locked, so this changes nothing there.
        private static void FreeMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
