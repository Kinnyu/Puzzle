using Puzzle.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Puzzle.StarryNight
{
    [RequireComponent(typeof(Camera))]
    public sealed class StarryNightWalk : MonoBehaviour
    {
        [SerializeField] CharacterController player;
        [SerializeField] Transform approachGuide;
        [SerializeField, Min(.1f)] float approachSeconds = 6.5f;
        [SerializeField, Min(.1f)] float eyeHeight = 1.62f;
        [SerializeField] DialoguePlayer dialoguePlayer;

        enum View { Overlook, Approaching, Walking }
        View view;
        Vector3 overlook;
        Quaternion openingRotation;
        float elapsed, yaw, pitch, verticalSpeed;

        void Start()
        {
            overlook = transform.position;
            openingRotation = transform.rotation;
            player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
        }

        void Update()
        {
            if (dialoguePlayer != null && dialoguePlayer.BlocksGameplayInput) return;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (view == View.Overlook)
            {
                if (clicked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    view = View.Approaching;
                }
                return;
            }
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;
            if (view == View.Approaching)
            {
                FlyToEntrance();
                return;
            }
            if (clicked) Cursor.lockState = CursorLockMode.Locked;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * .11f;
                pitch = Mathf.Clamp(pitch - delta.y * .11f, -89f, 88f);
            }
            float dt = Time.deltaTime;
            Vector2 input = Vector2.zero;
            float speed = 3.6f;
            if (keys != null)
            {
                yaw += (Pressed(keys.rightArrowKey.isPressed) - Pressed(keys.leftArrowKey.isPressed)) * 85f * dt;
                input.x = Pressed(keys.dKey.isPressed) - Pressed(keys.aKey.isPressed);
                input.y = Pressed(keys.wKey.isPressed || keys.upArrowKey.isPressed)
                          - Pressed(keys.sKey.isPressed || keys.downArrowKey.isPressed);
                if (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed) speed = 6.1f;
            }
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 velocity = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y) * speed;
            if (player.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * dt;
            player.Move((velocity + Vector3.up * verticalSpeed) * dt);
            transform.SetPositionAndRotation(player.transform.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0));
        }

        static int Pressed(bool value) => value ? 1 : 0;

        void FlyToEntrance()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / approachSeconds));
            Vector3 end = player.transform.position + Vector3.up * eyeHeight;
            Vector3 a = Vector3.Lerp(overlook, approachGuide.position, t);
            Vector3 b = Vector3.Lerp(approachGuide.position, end, t);
            var arrivalRotation = Quaternion.Euler(-5f, player.transform.eulerAngles.y, 0);
            transform.SetPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.Slerp(openingRotation, arrivalRotation, t));
            if (elapsed < approachSeconds) return;
            yaw = player.transform.eulerAngles.y;
            pitch = -5f;
            player.enabled = true;
            view = View.Walking;
        }

        void OnGUI()
        {
            if (view != View.Overlook) return;
            var rect = new Rect((Screen.width-164)*.5f, Screen.height-62, 164, 34);
            GUI.Box(rect, "点击进入村落");
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Cursor.lockState = CursorLockMode.None;
        }

        void OnDisable() => Cursor.lockState = CursorLockMode.None;
    }
}

