using System;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// A press button on a board that stands in the room (Next and Previous on a guide board).
    /// It reuses LabObject, so it works with the desktop aim dot (click) and with the headset ray
    /// (squeeze the grip), the same way as every other lab part. It needs a collider on the same object,
    /// and an XRSimpleInteractable for the headset. The Editor builder adds all of them.
    /// </summary>
    [RequireComponent(typeof(LabObject))]
    public class BoardButton : MonoBehaviour
    {
        [Tooltip("The picture of the button. Its colour shows hover and disabled.")]
        [SerializeField] private Image face;
        [SerializeField] private Color normalColour = new Color(0.20f, 0.45f, 0.70f, 1f);
        [SerializeField] private Color hoverColour = new Color(0.28f, 0.62f, 0.95f, 1f);
        [SerializeField] private Color disabledColour = new Color(0.18f, 0.22f, 0.28f, 0.6f);
        [Tooltip("Two presses closer together than this count as one, in seconds. It stops a double press.")]
        [SerializeField] private float cooldownSeconds = 0.25f;

        private LabObject _labObject;
        private bool _hovered;
        private bool _interactable = true;
        private float _lastPressTime = -10f;

        /// <summary>Raised when the button is pressed and it is allowed to be pressed.</summary>
        public event Action Pressed;

        /// <summary>False greys the button out and ignores presses.</summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                RefreshColour();
            }
        }

        private void Awake() => _labObject = GetComponent<LabObject>();

        private void OnEnable()
        {
            _labObject.Clicked += OnClicked;
            _labObject.HoverChanged += OnHoverChanged;
            RefreshColour();
        }

        private void OnDisable()
        {
            _labObject.Clicked -= OnClicked;
            _labObject.HoverChanged -= OnHoverChanged;
        }

        private void OnClicked(LabObject clicked)
        {
            if (!_interactable) return;
            if (Time.unscaledTime - _lastPressTime < cooldownSeconds) return;

            _lastPressTime = Time.unscaledTime;
            Pressed?.Invoke();
        }

        private void OnHoverChanged(LabObject hovered, bool isHovered)
        {
            _hovered = isHovered;
            RefreshColour();
        }

        private void RefreshColour()
        {
            if (face == null) return;

            face.color = !_interactable ? disabledColour : (_hovered ? hoverColour : normalColour);
        }
    }
}
