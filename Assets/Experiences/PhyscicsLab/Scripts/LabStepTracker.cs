using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Tracks which lab step the student is on. The step list lives only here: the backend
    /// receives the current and previous step ids, never the upcoming ones.
    /// </summary>
    public class LabStepTracker : MonoBehaviour
    {
        [Tooltip("Step ids in lab order. They must match the ids in the backend retrieval chunks.")]
        [SerializeField] private string[] stepIds = { "step-1", "step-2", "step-3" };

        private int _index;
        private bool _completed;       // true once the rules mark the current step as done
        private float _stepStartTime;  // Time.time when the current step began

        public string CurrentStepId => stepIds[_index];
        public string PreviousStepId => _index == 0 ? string.Empty : stepIds[_index - 1];
        public bool IsLastStep => _index == stepIds.Length - 1;
        public int CurrentIndex => _index;
        public int StepCount => stepIds.Length;
        public bool IsCurrentStepComplete => _completed;
        public float StepElapsedSeconds => Time.time - _stepStartTime;

        /// <summary>
        /// The id of the step after this one. The app may show it. It must never go into the payload:
        /// PayloadGuard refuses the key next_step.
        /// </summary>
        public string NextStepId => IsLastStep ? string.Empty : stepIds[_index + 1];

        /// <summary>
        /// Raised after the step changes. Arguments: previous id, current id.
        /// </summary>
        public event Action<string, string> StepChanged;

        /// <summary>
        /// Raised once when the current step is marked as done. Argument: the step id.
        /// </summary>
        public event Action<string> StepCompleted;

        private void Awake()
        {
            if (stepIds == null || stepIds.Length == 0)
            {
                Debug.LogError("[LabStepTracker] stepIds is empty.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Jumps to a saved step when progress is loaded. Raises no StepChanged, because
        /// restoring is not something the student did. Returns false for an index out of range.
        /// </summary>
        public bool TrySetIndex(int index)
        {
            if (index < 0 || index >= stepIds.Length) return false;

            _index = index;
            _completed = false; // a restored step starts as not done
            _stepStartTime = Time.time;
            return true;
        }

        /// <summary>
        /// Marks the current step as done. Returns false if it was already done.
        /// This does not move to the next step. The rules do that when they decide.
        /// </summary>
        public bool TryMarkComplete()
        {
            if (_completed) return false;

            _completed = true;
            StepCompleted?.Invoke(CurrentStepId);
            return true;
        }

        public bool TryAdvance()
        {
            if (IsLastStep) return false;

            _index++;
            _completed = false;
            _stepStartTime = Time.time;
            StepChanged?.Invoke(PreviousStepId, CurrentStepId);
            return true;
        }
    }
}