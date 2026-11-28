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
    /// A long answer scrolls inside the answer area.
    /// </summary>
    public class ChatPanelPresenter : MonoBehaviour
    {
        [SerializeField] private LabAssistant assistant;
        [SerializeField] private QuestionButton[] questionButtons;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text answerLabel;
        [Tooltip("The scroll area that holds the answer text. Optional: without it the answer just stays where it is.")]
        [SerializeField] private ScrollRect answerScroll;

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

            // The panel may have been hidden while a request ran. Match the real state.
            bool busy = assistant.IsBusy;
            SetButtonsInteractable(!busy);
            if (busy) statusLabel.text = "Thinking...";
            else if (statusLabel.text == "Thinking...") statusLabel.text = string.Empty;
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
            ScrollAnswerToTop();
        }

        // A new answer starts at its first line, not where the last answer was scrolled to.
        // The text is anchored to the top of the scroll area, so position 0 is the top.
        private void ScrollAnswerToTop()
        {
            if (answerScroll == null) return;

            answerScroll.StopMovement();
            answerScroll.content.anchoredPosition = Vector2.zero;
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