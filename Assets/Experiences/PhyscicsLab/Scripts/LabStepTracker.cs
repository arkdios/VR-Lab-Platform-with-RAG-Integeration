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

        public string CurrentStepId => stepIds[_index];
        public string PreviousStepId => _index == 0 ? string.Empty : stepIds[_index - 1];
        public bool IsLastStep => _index == stepIds.Length - 1;
        public int CurrentIndex => _index;

        /// <summary>
        /// Raised after the step changes. Arguments: previous id, current id.
        /// </summary>
        public event Action<string, string> StepChanged;

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
            return true;
        }

        public bool TryAdvance()
        {
            if (IsLastStep) return false;

            _index++;
            StepChanged?.Invoke(PreviousStepId, CurrentStepId);
            return true;
        }
    }
}