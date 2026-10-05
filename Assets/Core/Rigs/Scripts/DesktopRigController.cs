using UnityEngine;
using UnityEngine.InputSystem;

/// I use namespace to group the class under a name, in order to keep
/// the "Core" folder code separate
namespace PhysicsLab.Core.Rigs
{
    /// This code snippet is for desktop first person view only.
    /// WASD to move around while the mouse is for tilting the
    /// camera. I used the Input System package, not the legacy one.
    
    /// I used the RequiredComponent because it makes Unity refusing
    /// to remove CharacterController, if not for this snippet, the
    /// code would crash right after calling GetComponent<CharacterController>()
    [RequireComponent(typeof(CharacterController))] 
    public class DesktopRigController : MonoBehaviour 
    {
        [Header("References")]
        [Tooltip("Child object that holds the cameras.")]
        [SerializeField] private Transform cameraGroup;

        [Header("Movement")]
        [Tooltip("Walking speed in metres per second.")]
        [SerializeField] private float moveSpeed = 2.5f;

        [Tooltip("Gravity in m/s^2 (negative sign means downward).")]
        [SerializeField] private float gravity = -9.81f;

        [Header("Look")]
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] private float lookSensitivity = 0.1f;

        [Tooltip("Maximum up/down tilt in degrees.")]
        [SerializeField] private float maxPitch = 80f;

        private CharacterController _controller;
        private float _pitch; // current up/down tilt in degrees
        private float _verticalVelocity; // current fall speed in metres per second

        /// <summary>
        /// Runs once when the object loads. Caches the controller and warns if the
        /// camera group is missing. I had this since it is better to log a message
        /// for error than a null object.
        /// </summary>
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraGroup == null)
            {
                Debug.LogError("[DesktopRigController] cameraGroup is not assigned.", this);
            }
        }

        /// <summary>
        /// Unlocks and shows the cursor. When the headset rig takes over, the mouse
        /// must not stay captured.
        /// </summary>
        private void OnDisable()
        {
            // Release the mouse when this rig is switched off (e.g. when entering VR).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            UpdateCursorLock();
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Look();
            }
            Move();
        }

        /// <summary>
        /// Captures the mouse on the first left click. Esc releases it (browser and Editor).
        /// This exists since browsers only allow mouse capture after a click, so it cannot
        /// happen on load.
        /// </summary>
        private void UpdateCursorLock()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Turns the body and tilts the camera group from mouse movement.
        /// </summary>
        private void Look()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cameraGroup == null) return;

            Vector2 delta = mouse.delta.ReadValue(); // pixels moved since last frame
            transform.Rotate(0f, delta.x * lookSensitivity, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -maxPitch, maxPitch);
            cameraGroup.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>
        /// Moves the body with WASD and applies gravity through the CharacterController.
        /// </summary>
        private void Move()
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
            }

            // Direction relative to where the body faces. Clamp so diagonals are not faster.
            Vector3 horizontal = transform.right * input.x + transform.forward * input.y;
            // Normalizing stops diagonal movement from being about 41% faster.
            if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();

            // -2f is to keep a small constant push towards the floor since isGrounded is only
            // reliable if the controller keeps touching the ground.
            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            // Multiplies by Time.deltaTime keeps speed the same at any frame rate.
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = horizontal * moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}


