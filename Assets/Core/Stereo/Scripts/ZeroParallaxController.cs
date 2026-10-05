using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsLab.Core.Stereo
{
    /// <summary>
    /// Changes the size of the orthographic Stereo Camera while a key is held.
    /// K makes it smaller, L makes it bigger (Tutorial 2 defines this "zero parallax"
    /// control as the size of the stereo camera).
    /// </summary>
    public class ZeroParallaxController : MonoBehaviour
    {
        [SerializeField] private Camera stereoCamera;

        [Header("Size limits")]
        [Tooltip("Orthographic size = half the visible height")]
        [SerializeField] private float minSize = 1f;
        [SerializeField] private float maxSize = 10f;

        [Header("Input")]
        [Tooltip("How fast the size changes while the key is held, in units per second.")]
        [SerializeField] private float changeSpeed = 2f;
        [SerializeField] private Key decreaseKey = Key.K;
        [SerializeField] private Key increaseKey = Key.L;

        /// <summary>
        /// Current orthographic size of the stereo camera.
        /// </summary>
        public float Size => stereoCamera != null ? stereoCamera.orthographicSize : 0f;

        /// <summary>
        /// Logs an error and disables the script if the camera is missing. Warns if it is not orthographic.
        /// </summary>
        private void Awake()
        {
            if (stereoCamera == null)
            {
                Debug.LogError("[ZeroParallaxController] stereoCamera is not assigned.", this);
                enabled = false; // Stop Update from running to avoid a null error every frame.
                return;
            }
            if (!stereoCamera.orthographic)
            {
                Debug.LogWarning("[ZeroParallaxController] Stereo camera should be Orthographic.", this);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float direction = 0f;
            if (keyboard[increaseKey].isPressed) direction += 1f;
            if (keyboard[decreaseKey].isPressed) direction -= 1f;
            if (Mathf.Approximately(direction, 0f)) return;

            stereoCamera.orthographicSize = Mathf.Clamp(
                stereoCamera.orthographicSize + direction * changeSpeed * Time.deltaTime,
                minSize, maxSize);
        }
    }
}