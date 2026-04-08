using EditorAttributes;
using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController), typeof(PlayerInputWrapper))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField, Title("References")] Transform cameraPivot;

        CharacterController characterController;
        PlayerInputWrapper inputWrapper;

        [SerializeField, Title("Movement")] float walkSpeed = 5f;

        [SerializeField] float sprintSpeed = 8f;

        [SerializeField] float acceleration = 24f;

        [SerializeField] float jumpHeight = 1.3f;

        [SerializeField] float gravity = 24f;

        [SerializeField] float groundedStickForce = -2f;

        [SerializeField, Title("Look")] float mouseSensitivity = 0.08f;

        [SerializeField] float minPitch = -60f;

        [SerializeField] float maxPitch = 70f;

        [SerializeField] bool lockCursorOnStart = true;

        Vector3 _planarVelocity;
        float _verticalVelocity;
        float _pitch;

        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputWrapper = GetComponent<PlayerInputWrapper>();

            if (characterController == null)
            {
                print("[Error] PlayerController: Missing required CharacterController component.");
                enabled = false;
                return;
            }

            if (inputWrapper == null)
            {
                print("[Error] PlayerController: Missing required PlayerInputWrapper component.");
                enabled = false;
                return;
            }

            if (cameraPivot != null)
            {
                _pitch = NormalizeWrappedPitch(cameraPivot.localEulerAngles.x);
            }

            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void Update()
        {
            if (inputWrapper == null)
            {
                return;
            }

            UpdateLook();
            UpdateMovement();

            if (inputWrapper.WasCursorUnlockPressedThisFrame())
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void UpdateLook()
        {
            if (!inputWrapper.HasMouse)
            {
                return;
            }

            // Read look deltas from the shared wrapper so key/device changes are centralized.
            Vector2 mouseDelta = inputWrapper.ReadLookDelta();
            float yawDelta = mouseDelta.x * mouseSensitivity;
            float pitchDelta = mouseDelta.y * mouseSensitivity;

            transform.Rotate(Vector3.up * yawDelta, Space.World);

            if (cameraPivot == null)
            {
                return;
            }

            // Track pitch in a dedicated float to avoid 0..360 localEulerAngles wrapping issues.
            _pitch -= pitchDelta;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void UpdateMovement()
        {
            Vector2 moveInput = inputWrapper.ReadMoveInput();
            bool isSprinting = inputWrapper.IsSprintPressed();
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

            // Convert 2D input into world-space movement based on the player's facing direction.
            Vector3 desiredDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            Vector3 targetPlanarVelocity = desiredDirection * targetSpeed;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetPlanarVelocity, acceleration * Time.deltaTime);

            if (characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = groundedStickForce;
                }

                if (inputWrapper.WasJumpPressedThisFrame())
                {
                    _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
                }
            }

            _verticalVelocity -= gravity * Time.deltaTime;

            Vector3 velocityThisFrame = _planarVelocity + Vector3.up * _verticalVelocity;
            characterController.Move(velocityThisFrame * Time.deltaTime);
        }

        static float NormalizeWrappedPitch(float wrappedPitch)
        {
            return wrappedPitch > 180f ? wrappedPitch - 360f : wrappedPitch;
        }
    }
}
