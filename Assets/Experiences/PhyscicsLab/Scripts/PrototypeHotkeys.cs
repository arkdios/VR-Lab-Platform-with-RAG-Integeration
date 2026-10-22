using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Keyboard shortcuts for testing without a UI:
    /// F5 = send a question to the backend with the current lab state,
    /// F6 = next step, F7 = save progress now, F9 = delete the saved progress file.
    /// Function keys are used because the XR simulator already uses letters and arrows.
    /// </summary>
    public class PrototypeHotkeys : MonoBehaviour
    {
        [SerializeField] private LabAssistant assistant;
        [SerializeField] private LabStepTracker steps;
        [SerializeField] private LabProgressStore progress;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return; // no keyboard, for example on a headset

            if (keyboard.f5Key.wasPressedThisFrame) assistant.Ask(QuestionType.WhatNext);
            if (keyboard.f6Key.wasPressedThisFrame) steps.TryAdvance();
            if (keyboard.f7Key.wasPressedThisFrame) SaveAndShowPath();
            if (keyboard.f9Key.wasPressedThisFrame) progress.ClearSavedFile();
        }

        private void SaveAndShowPath()
        {
            progress.SaveNow();
            Debug.Log($"[PrototypeHotkeys] Progress saved to {progress.FilePath}", this);
        }
    }
}
