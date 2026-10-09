using System.Collections;
using UnityEngine;

namespace PhysicsLab.Core.Rigs
{
    /// <summary>
    /// Shows the UI as a flat overlay on the desktop, and as a panel floating in front of the
    /// user in the headset. The panel is placed once when VR starts and then stays put,
    /// because UI that is glued to the head is uncomfortable in VR.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HudCanvasAdapter : MonoBehaviour
    {
        [SerializeField] private RigModeSwitcher rigSwitcher;
        [SerializeField] private float distanceMetres = 1.1f;
        [SerializeField] private float dropMetres = 0.15f;
        [Tooltip("Canvas size in UI pixels. With scale 0.001 this is 1 m x 0.6 m in the room.")]
        [SerializeField] private Vector2 worldSizePixels = new Vector2(1000f, 600f);
        [SerializeField] private float worldScale = 0.001f;

        private Canvas _canvas;
        private RectTransform _rect;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            rigSwitcher.ModeChanged += OnModeChanged;
            OnModeChanged(rigSwitcher.CurrentMode);
        }

        private void OnDisable() => rigSwitcher.ModeChanged -= OnModeChanged;

        private void OnModeChanged(RigModeSwitcher.RigMode mode)
        {
            StopAllCoroutines(); // Cancels a pending placement.
            StartCoroutine(ApplyNextFrame(mode));
        }

        /// <summary>
        /// Waits one frame so the newly activated rig's camera exists before it is used.
        /// SInce Camera.main must be the new rig's camera, and it only exists after the switch finishes.
        /// </summary>
        private IEnumerator ApplyNextFrame(RigModeSwitcher.RigMode mode)
        {
            yield return null;
            if (mode == RigModeSwitcher.RigMode.Headset) ShowInRoom();
            else _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        private void ShowInRoom()
        {
            Camera head = Camera.main;
            if (head == null)
            {
                Debug.LogWarning("[HudCanvasAdapter] No main camera yet. UI stays as overlay.", this);
                return;
            }

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = head;
            _rect.sizeDelta = worldSizePixels;
            _rect.localScale = Vector3.one * worldScale;
            PlaceInFrontOf(head.transform);
        }

        /// <summary>
        /// 1.1 m ahead, 15 cm below eye height, facing the user.
        /// </summary>
        private void PlaceInFrontOf(Transform head)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 position = head.position + flatForward * distanceMetres + Vector3.down * dropMetres;
            _rect.SetPositionAndRotation(position, Quaternion.LookRotation(flatForward));
        }
    }
}