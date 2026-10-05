using System;
using UnityEngine;
using UnityEngine.XR;

namespace PhysicsLab.Core.Rigs
{
    /// Keeps exactly one player rig active. Turn off the headset rig while an XR device is
    /// active, the desktop rig otherwise. It checks every frame, so it also reacts when the
    /// user presses "Enter VR" or leaves VR in the browser.
    public class RigModeSwitcher : MonoBehaviour
    {
        /// <summary>
        /// Which rig is currently in use. I used names for these two enumerations
        /// was because named values read better than true/false and cannot be mixed up.
        /// </summary>
        public enum RigMode { Desktop, Headset }

        /// <summary>
        /// Lets you force a mode for testing in the Editor.
        /// </summary>
        public enum DebugOverride { Auto, ForceDesktop, ForceHeadset }

        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject headsetRig;

        [Tooltip("Auto follows the real XR state. The Force options are for Editor testing.")]
        [SerializeField] private DebugOverride debugOverride = DebugOverride.Auto;

        [SerializeField] private bool logChanges = true;

        /// <summary>
        /// The rig that is active right now.
        /// </summary>
        public RigMode CurrentMode { get; private set; }

        /// <summary>
        /// Raised after the active rig changes. Other scripts can listen to it.
        /// This is for decoupling the script since the switcher does not need to
        /// know who listens.
        /// </summary>
        public event Action<RigMode> ModeChanged;

        /// <summary>
        /// Makes the first Update apply the mode even if it equals the default.
        /// </summary>
        private bool _applied; // false until the first Apply

        private void Start()
        {
            Apply(DetectMode());
        }

        /// <summary>
        /// Compares the detected mode to the current one every frame.
        /// I wrote it as this way since it is cheap, and this also avoids needing an event
        /// whose name may differ between package versions.
        /// </summary>
        private void Update()
        {
            RigMode detected = DetectMode();
            if (!_applied || detected != CurrentMode)
            {
                Apply(detected);
            }
        }

        /// <summary>
        /// Works out the mode from the override setting and the XR state.
        /// </summary>
        private RigMode DetectMode()
        {
            switch (debugOverride)
            {
                case DebugOverride.ForceDesktop: return RigMode.Desktop;
                case DebugOverride.ForceHeadset: return RigMode.Headset;
                // This is a Unity's flag for "an XR device is running".
                default: return XRSettings.isDeviceActive ? RigMode.Headset : RigMode.Desktop;
            }
        }

        /// <summary>
        /// Turns one rig on, the other off, and notifies listeners.
        /// I write this snippet like this is because it means that deactivating a
        /// whole rig also deactivates its cameras and Audio Listener, so there will
        /// never be a second listener.
        /// </summary>
        private void Apply(RigMode mode)
        {
            CurrentMode = mode;
            _applied = true;

            if (desktopRig != null) desktopRig.SetActive(mode == RigMode.Desktop);
            if (headsetRig != null) headsetRig.SetActive(mode == RigMode.Headset);

            if (logChanges) Debug.Log($"[RigModeSwitcher] Mode = {mode}", this);
            ModeChanged?.Invoke(mode);
        }
    }
}