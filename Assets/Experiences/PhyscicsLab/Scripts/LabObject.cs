using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Identity and interaction state of one lab object (ring, pulley, mass, spring scale).
    /// Headset input (XR Interaction Toolkit) and desktop input (DesktopPointer) both report
    /// here, so the rest of the lab never needs to know which input device is in use.
    /// </summary>
    public class LabObject : MonoBehaviour
    {
        [Tooltip("Stable id sent to the backend: ring, pulley-1, mass-2, spring-scale.")]
        [SerializeField] private string objectId;

        private bool _isGrabbable;

        public string ObjectId => objectId;
        public bool IsGrabbable => _isGrabbable;

        /// <summary>
        /// Raised when the pointer starts (true) or stops (false) pointing at this object.
        /// </summary>
        public event Action<LabObject, bool> HoverChanged;

        /// <summary>
        /// Raised when a grabbable object is grabbed (true) or released (false).
        /// </summary>
        public event Action<LabObject, bool> HeldChanged;

        /// <summary>
        /// Raised when the user selects an object that cannot be grabbed.
        /// </summary>
        public event Action<LabObject> Clicked;

        /// <summary>
        /// Logs an error for an empty id since	an empty id would silently corrupt the data sent to the backend.
        /// </summary>
        private void Awake()
        {
            if (string.IsNullOrEmpty(objectId))
            {
                Debug.LogError("[LabObject] objectId is empty.", this);
            }

            _isGrabbable = GetComponent<XRGrabInteractable>() != null;
            ConnectHeadsetInput(GetComponent<XRBaseInteractable>());
        }

        /// <summary>
        /// Called by input code (XR events here, DesktopPointer on desktop).
        /// </summary>
        public void ReportHover(bool hovered) => HoverChanged?.Invoke(this, hovered);

        /// <summary>
        /// Called by input code when a grab starts or ends.
        /// </summary>
        public void ReportHeld(bool held) => HeldChanged?.Invoke(this, held);

        /// <summary>
        /// Called by input code when a non-grabbable object is selected.
        /// </summary>
        public void ReportClick() => Clicked?.Invoke(this);

        /// <summary>
        /// Hooks XRI's hover and select events.
        /// </summary>
        /// <param name="interactable" - the object the mouse is hovering/pointing at></param>
        private void ConnectHeadsetInput(XRBaseInteractable interactable)
        {
            if (interactable == null) return; // desktop-only object

            // Listeners live as long as this object does, so they are never removed.
            // The lambda "_ => ..." means Ignore the event argument.
            interactable.hoverEntered.AddListener(_ => ReportHover(true));
            interactable.hoverExited.AddListener(_ => ReportHover(false));
            interactable.selectEntered.AddListener(_ => ReportSelect(true));
            interactable.selectExited.AddListener(_ => ReportSelect(false));
        }

        private void ReportSelect(bool selected)
        {
            if (_isGrabbable) ReportHeld(selected);
            else if (selected) ReportClick();
        }
    }
}