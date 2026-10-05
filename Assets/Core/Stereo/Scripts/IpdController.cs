using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsLab.Core.Stereo
{
    /// <summary>
    /// Changes the distance between the left and right eye cameras (the IPD)
    /// while a key is held. O makes it smaller, P makes it bigger.
    /// </summary>
    public class IpdController : MonoBehaviour
    {
        [Header("Cameras")]
        [SerializeField] private Camera leftEyeCamera;
        [SerializeField] private Camera rightEyeCamera;

        [Header("IPD")]
        [Tooltip("In metres, total distance between the cameras")]
        [SerializeField] private float ipdMeters = 0.10f;
        [SerializeField] private float minIpdMeters = 0.03f;
        [SerializeField] private float maxIpdMeters = 0.12f;

        [Header("Input")]
        [Tooltip("How fast the IPD changes while the key is held, in metres per second.")]
        [SerializeField] private float changeSpeed = 0.02f;

        // The keys are Inspector settings.
        [SerializeField] private Key decreaseKey = Key.O;
        [SerializeField] private Key increaseKey = Key.P;

        /// <summary>
        /// Current distance between the eye cameras in metres.
        /// </summary>
        public float IpdMeters => ipdMeters;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float direction = 0f;
            // True every frame the key is held.
            if (keyboard[increaseKey].isPressed) direction += 1f;
            if (keyboard[decreaseKey].isPressed) direction -= 1f;
            if (Mathf.Approximately(direction, 0f)) return;

            // Change per frame = speed x frame time to maintain same speed at 30 or 120 fps.
            ipdMeters = Mathf.Clamp(
                ipdMeters + direction * changeSpeed * Time.deltaTime,
                minIpdMeters, maxIpdMeters);
            Apply();
        }

        /// <summary>
        /// Moves each camera half the IPD away from the center, along local X.
        /// Sets left X to minus half, right X to plus half, in local space.
        /// </summary>
        private void Apply()
        {
            if (leftEyeCamera == null || rightEyeCamera == null) return;
            SetLocalX(leftEyeCamera.transform, -ipdMeters * 0.5f);
            SetLocalX(rightEyeCamera.transform, ipdMeters * 0.5f);
        }

        private static void SetLocalX(Transform target, float x)
        {
            Vector3 position = target.localPosition;
            position.x = x;
            target.localPosition = position;
        }

        /// <summary>
        /// Runs when you edit values in the Inspector.
        /// </summary>
        private void OnValidate()
        {
            // Keeps Inspector edits inside the allowed range and updates the cameras live.
            // Mathf.Clamp() keeps IPD inside min and max.
            ipdMeters = Mathf.Clamp(ipdMeters, minIpdMeters, maxIpdMeters);
            if (Application.isPlaying) Apply();
        }
    }
}