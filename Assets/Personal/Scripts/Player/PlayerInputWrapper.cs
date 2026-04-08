using EditorAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [DisallowMultipleComponent]
    public class PlayerInputWrapper : MonoBehaviour
    {
        [SerializeField, Title("Movement Keys")] Key moveForwardKey = Key.W;

        [SerializeField] Key moveBackwardKey = Key.S;

        [SerializeField] Key moveRightKey = Key.D;

        [SerializeField] Key moveLeftKey = Key.A;

        [SerializeField] Key sprintPrimaryKey = Key.LeftShift;

        [SerializeField] Key sprintSecondaryKey = Key.RightShift;

        [SerializeField] Key jumpKey = Key.Space;

        [SerializeField, Title("Interaction Keys")] Key interactKey = Key.E;

        [SerializeField] Key cursorUnlockKey = Key.Escape;

        public bool HasKeyboard => Keyboard.current != null;

        public bool HasMouse => Mouse.current != null;

        public Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 moveInput = Vector2.zero;

            if (keyboard[moveForwardKey].isPressed)
            {
                moveInput.y += 1f;
            }

            if (keyboard[moveBackwardKey].isPressed)
            {
                moveInput.y -= 1f;
            }

            if (keyboard[moveRightKey].isPressed)
            {
                moveInput.x += 1f;
            }

            if (keyboard[moveLeftKey].isPressed)
            {
                moveInput.x -= 1f;
            }

            return moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
        }

        public bool IsSprintPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard[sprintPrimaryKey].isPressed || keyboard[sprintSecondaryKey].isPressed;
        }

        public bool WasJumpPressedThisFrame()
        {
            return WasKeyPressedThisFrame(jumpKey);
        }

        public bool WasInteractPressedThisFrame()
        {
            return WasKeyPressedThisFrame(interactKey);
        }

        public bool WasCursorUnlockPressedThisFrame()
        {
            return WasKeyPressedThisFrame(cursorUnlockKey);
        }

        public Vector2 ReadLookDelta()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return Vector2.zero;
            }

            // We intentionally read raw mouse delta here so all look behavior stays centralized in one wrapper.
            return mouse.delta.ReadValue();
        }

        static bool WasKeyPressedThisFrame(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }
    }
}
