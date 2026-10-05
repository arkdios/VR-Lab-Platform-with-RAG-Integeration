using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicsLab.Core.Stereo
{
    /// <summary>
    /// Switches the desktop view between the normal single camera and the
    /// side-by-side Stereo Preview (Tutorial 2 rig). Press T to toggle.
    /// The preview is always switched off when this rig is deactivated (e.g. entering VR).
    /// </summary>
    public class StereoPreviewController : MonoBehaviour
    {
        [Header("Normal view")]
        [SerializeField] private Camera monoCamera;

        [Header("Stereo Preview")]
        [SerializeField] private Camera leftEyeCamera;
        [SerializeField] private Camera rightEyeCamera;
        [SerializeField] private Camera stereoCamera;
        [Tooltip("Parent of the two eye planes. Switched on only while the preview is on.")]
        [SerializeField] private GameObject stereoGroup;

        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.T;
        [SerializeField] private bool startWithPreviewOn = false;

        /// <summary>
        /// True while the side-by-side preview is showing.
        /// </summary>
        public bool IsPreviewActive { get; private set; }

        private void Awake()
        {
            // Cameras draw in order of depth (called Priority in the URP Inspector).
            // Eye cameras must draw first into their textures. The stereo camera draws last to the screen.
            if (leftEyeCamera != null) leftEyeCamera.depth = -2f;
            if (rightEyeCamera != null) rightEyeCamera.depth = -2f;
            if (stereoCamera != null) stereoCamera.depth = 0f;

            SetPreview(startWithPreviewOn);
        }

        /// <summary>
        /// Switches the preview off since the stereo camera is outside the rig. Without this,
        /// it would keep drawing during VR.
        /// </summary>
        private void OnDisable()
        {
            SetPreview(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                SetPreview(!IsPreviewActive);
            }
        }

        /// <summary>
        /// Turns the preview on or off and flips every camera and object that belongs to it.
        /// Such that cameras and planes can never end up half on.
        /// </summary>
        public void SetPreview(bool on)
        {
            IsPreviewActive = on;

            if (monoCamera != null) monoCamera.enabled = !on;
            if (leftEyeCamera != null) leftEyeCamera.enabled = on;
            if (rightEyeCamera != null) rightEyeCamera.enabled = on;
            if (stereoCamera != null) stereoCamera.enabled = on;
            if (stereoGroup != null) stereoGroup.SetActive(on);
        }
    }
}