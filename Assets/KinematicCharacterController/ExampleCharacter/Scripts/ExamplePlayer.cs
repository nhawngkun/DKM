using UnityEngine;
using UnityEngine.InputSystem;

namespace KinematicCharacterController.Examples
{
    public class ExamplePlayer : MonoBehaviour
    {
        public CharController Character;
        public CameraController CharacterCamera;

        [Header("Mouse Settings")]
        public float MouseSensitivity = 0.12f; // độ / pixel (delta chuột thô rất lớn, 1 là quá nhanh)

        [Header("Gamepad Settings")]
        public float GamepadLookSensitivity = 150f; // độ/giây khi stick full deflection
        public float GamepadStickDeadzone = 0.15f;

        PlayerCharacterInputs characterInputs;
        private LockOnTarget _cameraLockTarget; // mục tiêu lock-on đã báo cho camera (chỉ gọi camera khi đổi)

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
            HandleCharacterInput();
        }

        private void LateUpdate()
        {
            if (CharacterCamera.RotateWithPhysicsMover && Character.Motor.AttachedRigidbody != null)
            {
                CharacterCamera.Focus.PlanarDirection = Character.Motor.AttachedRigidbody.GetComponent<PhysicsMover>().RotationDeltaFromInterpolation * CharacterCamera.Focus.PlanarDirection;
                CharacterCamera.Focus.PlanarDirection = Vector3.ProjectOnPlane(CharacterCamera.Focus.PlanarDirection, Character.Motor.CharacterUp).normalized;
            }

            // Phát rung camera/hitstop của các đòn chém đã tới frame này, trước khi camera áp hiệu ứng
            Character.FlushCombatFeedback();
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
                lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f) * MouseSensitivity;

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

            // Đồng bộ lock-on của nhân vật sang camera
            if (Character.CurrentLockTarget != _cameraLockTarget)
            {
                _cameraLockTarget = Character.CurrentLockTarget;
                if (_cameraLockTarget != null)
                {
                    CharacterCamera.SetLockOnTarget(_cameraLockTarget.LockPoint != null ? _cameraLockTarget.LockPoint : _cameraLockTarget.transform);
                }
                else
                {
                    CharacterCamera.ClearLockOnTarget();
                }
            }

            // Truyền vận tốc + trạng thái đất để camera auto-align / cúi xuống khi rơi hoạt động
            Vector3 velocity = Character.Motor.BaseVelocity;
            Vector3 up = Character.Motor.CharacterUp;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
            float verticalVelocity = Vector3.Dot(velocity, up);

            CharacterCamera.UpdateCamera(Time.deltaTime, scrollInput, lookInputVector,
                planarVelocity, verticalVelocity, Character.IsGrounded);
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
            bool attack = false;
            bool skill = false;
            int weaponSwitch = 0;
            bool lockOn = false;

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
                if (Keyboard.current.tabKey.wasPressedThisFrame) weaponSwitch = 1; // Tab = đổi vũ khí
                lockOn |= Keyboard.current.qKey.isPressed; // giữ Q để lock-on
            }

            // --- Chuột: chuột trái = đánh, chuột phải = skill ---
            if (Mouse.current != null)
            {
                // Click khi con trỏ chưa khóa chỉ để khóa con trỏ, không ra đòn
                bool leftClick = Mouse.current.leftButton.wasPressedThisFrame;
                if (leftClick && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
                else
                {
                    attack |= leftClick;
                }
                skill |= Mouse.current.rightButton.wasPressedThisFrame;
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
                attack |= Gamepad.current.buttonWest.wasPressedThisFrame;         // X / Square
                skill |= Gamepad.current.buttonNorth.wasPressedThisFrame;         // Y / Triangle
                if (Gamepad.current.dpad.right.wasPressedThisFrame) weaponSwitch = 1;  // D-pad phải/trái = đổi vũ khí
                if (Gamepad.current.dpad.left.wasPressedThisFrame) weaponSwitch = -1;
                lockOn |= Gamepad.current.rightShoulder.isPressed;                // giữ RB / R1
            }

            characterInputs.MoveAxisForward = moveForward;
            characterInputs.MoveAxisRight = moveRight;
            characterInputs.CameraRotation = CharacterCamera.transform.rotation;
            characterInputs.JumpDown = jumpDown;
            characterInputs.CrouchDown = crouchDown;
            characterInputs.CrouchUp = crouchUp;
            characterInputs.OrientationSwitch = orientationSwitch;
            characterInputs.Dash = dash;
            characterInputs.Attack = attack;
            characterInputs.Skill = skill;
            characterInputs.WeaponSwitch = weaponSwitch;
            characterInputs.LockOn = lockOn;

            Character.SetInputs(ref characterInputs);
        }
    }
}