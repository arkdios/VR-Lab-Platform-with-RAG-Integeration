using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Placeholder step rules for the prototype, until the real PHYS 124 steps exist.
    /// Grabbing the ring during step 1 moves to step 2. Grabbing the spring scale during step 2
    /// moves to step 3. Remove this component when the real rules arrive.
    /// </summary>
    public class PrototypeStepRules : MonoBehaviour
    {
        [SerializeField] private LabStepTracker steps;
        [SerializeField] private LabObject ringObject;
        [SerializeField] private LabObject springScaleObject;
        [Tooltip("Step index (0 = first step) in which grabbing the ring advances.")]
        [SerializeField] private int ringGrabFromStep = 0;
        [Tooltip("Step index in which grabbing the spring scale advances.")]
        [SerializeField] private int scaleGrabFromStep = 1;

        private void OnEnable()
        {
            ringObject.HeldChanged += OnRingHeld;
            springScaleObject.HeldChanged += OnScaleHeld;
        }

        private void OnDisable()
        {
            ringObject.HeldChanged -= OnRingHeld;
            springScaleObject.HeldChanged -= OnScaleHeld;
        }

        private void OnRingHeld(LabObject labObject, bool held) => AdvanceIf(held, ringGrabFromStep);

        private void OnScaleHeld(LabObject labObject, bool held) => AdvanceIf(held, scaleGrabFromStep);

        private void AdvanceIf(bool grabbed, int requiredStep)
        {
            if (grabbed && steps.CurrentIndex == requiredStep) steps.TryAdvance();
        }
    }
}
