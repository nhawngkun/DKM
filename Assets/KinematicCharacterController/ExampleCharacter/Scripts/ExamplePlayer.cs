using KinematicCharacterController;
using KinematicCharacterController.Examples;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KinematicCharacterController.Examples
{
    public class ExamplePlayer : MonoBehaviour
    {
        public CharController Character;
        public CameraController CharacterCamera;

        private const string MouseXInput = "Mouse X";
        private const string MouseYInput = "Mouse Y";
        private const string MouseScrollInput = "Mouse ScrollWheel";
        private const string HorizontalInput = "Horizontal";
        private const string VerticalInput = "Vertical";

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            // Tell camera to follow transform
            CharacterCamera.SetFollowTransform(Character.CameraFollowPoint);

            // Ignore the character's collider(s) for camera obstruction checks
            CharacterCamera.IgnoredColliders.Clear();
            CharacterCamera.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());
        }

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            HandleCharacterInput();
        }

        private void LateUpdate()
        {
            // Handle rotating the camera along with physics movers
            if (CharacterCamera.RotateWithPhysicsMover && Character.Motor.AttachedRigidbody != null)
            {
                CharacterCamera.PlanarDirection = Character.Motor.AttachedRigidbody.GetComponent<PhysicsMover>().RotationDeltaFromInterpolation * CharacterCamera.PlanarDirection;
                CharacterCamera.PlanarDirection = Vector3.ProjectOnPlane(CharacterCamera.PlanarDirection, Character.Motor.CharacterUp).normalized;
            }

            HandleCameraInput();
        }

        private void HandleCameraInput()
        {
            // Create the look input vector for the camera
            float mouseLookAxisUp = Mouse.current.delta.ReadValue().y;
            float mouseLookAxisRight = Mouse.current.delta.ReadValue().x;
            Vector3 lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

            // Prevent moving the camera while the cursor isn't locked
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                lookInputVector = Vector3.zero;
            }

            // Input for zooming the camera (disabled in WebGL because it can cause problems)
            float scrollInput = -Mouse.current.scroll.ReadValue().y;
#if UNITY_WEBGL
        scrollInput = 0f;
#endif

            // Apply inputs to the camera
            CharacterCamera.UpdateWithInput(Time.deltaTime, scrollInput, lookInputVector);

            // Handle toggling zoom level
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                CharacterCamera.TargetDistance = (CharacterCamera.TargetDistance == 0f) ? CharacterCamera.DefaultDistance : 0f;
            }
        }

        private void HandleCharacterInput()
        {
            PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

   
            // Build the CharacterInputs struct
            characterInputs.MoveAxisForward = Keyboard.current.wKey.isPressed ? 1f :
                                              Keyboard.current.sKey.isPressed ? -1f : 0f;

            characterInputs.MoveAxisRight = Keyboard.current.dKey.isPressed ? 1f :
                                            Keyboard.current.aKey.isPressed ? -1f : 0f;

            characterInputs.CameraRotation = CharacterCamera.Transform.rotation;

            characterInputs.JumpDown = Keyboard.current.spaceKey.wasPressedThisFrame;

            characterInputs.CrouchDown = Keyboard.current.cKey.wasPressedThisFrame;
            characterInputs.CrouchUp = Keyboard.current.cKey.wasReleasedThisFrame;

            // Apply inputs to character
            Character.SetInputs(ref characterInputs);
        }
    }
}