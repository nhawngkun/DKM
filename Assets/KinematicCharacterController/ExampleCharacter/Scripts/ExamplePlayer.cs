using UnityEngine;
using UnityEngine.InputSystem;

namespace KinematicCharacterController.Examples
{
    public class ExamplePlayer : MonoBehaviour
    {
        public CharController Character;
        public CameraController CharacterCamera;

        [Header("Gamepad Settings")]
        public float GamepadLookSensitivity = 150f; // độ/giây khi stick full deflection
        public float GamepadStickDeadzone = 0.15f;

        PlayerCharacterInputs characterInputs;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            CharacterCamera.SetFollowTransform(Character.CameraFollowPoint);
            characterInputs = new PlayerCharacterInputs();

            CharacterCamera.Follow.IgnoredColliders.Clear();
            CharacterCamera.Follow.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            HandleCharacterInput();
        }

        private void LateUpdate()
        {
            if (CharacterCamera.RotateWithPhysicsMover && Character.Motor.AttachedRigidbody != null)
            {
                CharacterCamera.Focus.PlanarDirection = Character.Motor.AttachedRigidbody.GetComponent<PhysicsMover>().RotationDeltaFromInterpolation * CharacterCamera.Focus.PlanarDirection;
                CharacterCamera.Focus.PlanarDirection = Vector3.ProjectOnPlane(CharacterCamera.Focus.PlanarDirection, Character.Motor.CharacterUp).normalized;
            }

            HandleCameraInput();
        }

        private void HandleCameraInput()
        {
            Vector3 lookInputVector = Vector3.zero;
            float scrollInput = 0f;

            // --- Chuột + bàn phím ---
            if (Mouse.current != null)
            {
                float mouseLookAxisUp = Mouse.current.delta.ReadValue().y;
                float mouseLookAxisRight = Mouse.current.delta.ReadValue().x;
                lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

                scrollInput = -Mouse.current.scroll.ReadValue().y;
#if UNITY_WEBGL
                scrollInput = 0f;
#endif
            }

            // Prevent moving the camera while the cursor isn't locked
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                lookInputVector = Vector3.zero;
            }

            // --- Gamepad (right stick) ---
            if (Gamepad.current != null)
            {
                Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

                if (rightStick.magnitude > GamepadStickDeadzone)
                {
                    // Nhân theo deltaTime vì stick trả giá trị liên tục (analog), không phải delta như chuột
                    Vector3 gamepadLook = new Vector3(rightStick.x, rightStick.y, 0f) * GamepadLookSensitivity * Time.deltaTime;
                    lookInputVector += gamepadLook;
                }

                // Zoom bằng D-pad lên/xuống hoặc trigger, tuỳ bạn muốn map
                float dpadZoom = Gamepad.current.dpad.up.isPressed ? -1f :
                                  Gamepad.current.dpad.down.isPressed ? 1f : 0f;
                if (dpadZoom != 0f)
                {
                    scrollInput = dpadZoom;
                }
            }

            CharacterCamera.UpdateCamera(Time.deltaTime, scrollInput, lookInputVector);
        }

        private void HandleCharacterInput()
        {
            float moveForward = 0f;
            float moveRight = 0f;
            bool jumpDown = false;
            bool crouchDown = false;
            bool crouchUp = false;
            bool orientationSwitch = false;
            bool dash = false;

            // --- Bàn phím ---
            if (Keyboard.current != null)
            {
                moveForward = Keyboard.current.wKey.isPressed ? 1f :
                              Keyboard.current.sKey.isPressed ? -1f : 0f;

                moveRight = Keyboard.current.dKey.isPressed ? 1f :
                            Keyboard.current.aKey.isPressed ? -1f : 0f;

                jumpDown |= Keyboard.current.spaceKey.wasPressedThisFrame;
                crouchDown |= Keyboard.current.cKey.wasPressedThisFrame;
                crouchUp |= Keyboard.current.cKey.wasReleasedThisFrame;
                orientationSwitch |= Keyboard.current.fKey.wasPressedThisFrame;
                dash |= Keyboard.current.eKey.wasPressedThisFrame;
            }

            // --- Tay cầm ---
            if (Gamepad.current != null)
            {
                Vector2 leftStick = Gamepad.current.leftStick.ReadValue();

                // Nếu stick có input đáng kể thì ưu tiên đè lên bàn phím
                if (leftStick.magnitude > GamepadStickDeadzone)
                {
                    moveForward = leftStick.y;
                    moveRight = leftStick.x;
                }

                jumpDown |= Gamepad.current.buttonSouth.wasPressedThisFrame;      // A / Cross
                //crouchDown |= Gamepad.current.buttonEast.wasPressedThisFrame;     // B / Circle
                //crouchUp |= Gamepad.current.buttonEast.wasReleasedThisFrame;
                //orientationSwitch |= Gamepad.current.buttonNorth.wasPressedThisFrame; // Y / Triangle
                dash |= Gamepad.current.rightTrigger.wasPressedThisFrame;         // RT / R2
                // hoặc: dash |= Gamepad.current.buttonWest.wasPressedThisFrame; // X / Square
            }

            characterInputs.MoveAxisForward = moveForward;
            characterInputs.MoveAxisRight = moveRight;
            characterInputs.CameraRotation = CharacterCamera.transform.rotation;
            characterInputs.JumpDown = jumpDown;
            characterInputs.CrouchDown = crouchDown;
            characterInputs.CrouchUp = crouchUp;
            characterInputs.OrientationSwitch = orientationSwitch;
            characterInputs.Dash = dash;

            Character.SetInputs(ref characterInputs);
        }
    }
}