using UnityEngine;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Put this on the StudentScreen object. While the mouse is locked (the player is looking
    /// around and dragging), the panels stop catching clicks and fade. Press Tab to free the
    /// mouse and the panels come back. In VR the cursor is never locked, so nothing changes.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class StudentScreenGate : MonoBehaviour
    {
        [Tooltip("How visible the panels are while the player is looking around.")]
        [SerializeField, Range(0f, 1f)] private float lookingAlpha = 0.35f;

        private CanvasGroup _group;

        private void Awake() => _group = GetComponent<CanvasGroup>();

        private void Update()
        {
            bool looking = Cursor.lockState == CursorLockMode.Locked;
            _group.blocksRaycasts = !looking; // false: clicks pass through to the 3D scene
            _group.alpha = looking ? lookingAlpha : 1f;
        }
    }
}
