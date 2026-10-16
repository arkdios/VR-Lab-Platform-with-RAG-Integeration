using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Instructor panel has two functionalities: choose the experiment condition and export the experiment log.
    /// </summary>
    public class InstructorPanelPresenter : MonoBehaviour
    {
        [SerializeField] private LabAssistant assistant;
        [SerializeField] private ReportExporter exporter;
        [SerializeField] private TMP_Dropdown conditionDropdown;
        [SerializeField] private Button exportLogButton;
        [SerializeField] private TMP_Text rowCountLabel;

        private void Awake()
        {
            // The option order must match the order of the ExperimentCondition enum.
            conditionDropdown.ClearOptions();
            conditionDropdown.AddOptions(new List<string> { "Question only", "Current state", "State and history" });
            conditionDropdown.SetValueWithoutNotify((int)assistant.Condition);

            conditionDropdown.onValueChanged.AddListener(OnConditionChosen);
            exportLogButton.onClick.AddListener(exporter.ExportExperimentLog);
        }

        private void OnEnable()
        {
            assistant.ReplyReady += OnReplyReady;
            RefreshRowCount();
        }

        private void OnDisable() => assistant.ReplyReady -= OnReplyReady;

        private void OnConditionChosen(int index) => assistant.Condition = (ExperimentCondition)index;

        private void OnReplyReady(AssistantReply reply) => RefreshRowCount();

        private void RefreshRowCount() => rowCountLabel.text = $"Logged questions: {assistant.Log.Count}";
    }
}