using System;
using PhysicsLab.Core.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Pairs a UI button with the question it asks.
    /// </summary>
    [Serializable]
    public struct QuestionButton
    {
        public Button button;
        public QuestionType question;
    }

    /// <summary>
    /// Chat panel consists of four fixed question buttons, a status line and the answer text.
    /// </summary>
    public class ChatPanelPresenter : MonoBehaviour
    {
        [SerializeField] private LabAssistant assistant;
        [SerializeField] private QuestionButton[] questionButtons;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text answerLabel;

        private void Awake()
        {
            foreach (QuestionButton entry in questionButtons)
            {
                QuestionType question = entry.question;
                entry.button.onClick.AddListener(() => assistant.Ask(question));
            }
        }

        private void OnEnable()
        {
            assistant.RequestStarted += OnRequestStarted;
            assistant.ReplyReady += OnReplyReady;
        }

        private void OnDisable()
        {
            assistant.RequestStarted -= OnRequestStarted;
            assistant.ReplyReady -= OnReplyReady;
        }

        private void OnRequestStarted()
        {
            SetButtonsInteractable(false);
            statusLabel.text = "Thinking...";
        }

        private void OnReplyReady(AssistantReply reply)
        {
            SetButtonsInteractable(true);
            statusLabel.text = StatusMessage(reply.Status);
            answerLabel.text = reply.Status == ChatStatus.Success ? reply.Text : string.Empty;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (QuestionButton entry in questionButtons) entry.button.interactable = interactable;
        }

        private static string StatusMessage(ChatStatus status)
        {
            switch (status)
            {
                case ChatStatus.Success: return string.Empty;
                case ChatStatus.NetworkError: return "Assistant unreachable. The lab still works.";
                case ChatStatus.HttpError: return "The assistant returned an error.";
                case ChatStatus.Timeout: return "The assistant took too long.";
                default: return "The assistant sent an unreadable answer.";
            }
        }
    }
}