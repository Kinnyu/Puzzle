using System;
using System.Collections.Generic;
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
        [SerializeField] ChapterOpeningText chapterOpening;

        enum View { Overlook, Approaching, Walking }
        View view;
        Vector3 overlook;
        Quaternion openingRotation;
        float elapsed, yaw, pitch, verticalSpeed;
        float sittingOffset;
        Action onApproachFinished;

        public bool InputSuppressed { get; set; }
        internal bool EscapeHandledExternally { get; set; }
        internal Func<Vector2, bool> PointerOverMenu { get; set; }
        internal CharacterController Player => player;
        internal float EyeHeight => eyeHeight;
        internal bool Walking => view == View.Walking;
        internal bool WaitingAtPainting => view == View.Overlook && onApproachFinished != null;

        void Start()
        {
            if (approachGuide == null)
            {
                EnterWalking(player.transform);
                return;
            }
            overlook = transform.position;
            openingRotation = transform.rotation;
            player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (InputSuppressed || (dialoguePlayer != null && dialoguePlayer.BlocksGameplayInput)) return;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame
                && PointerOverMenu?.Invoke(mouse.position.ReadValue()) != true;
            if (view == View.Overlook)
            {
                if (clicked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    view = View.Approaching;
                }
                return;
            }
            if (!EscapeHandledExternally && keys?.escapeKey.wasPressedThisFrame == true)
                Cursor.lockState = CursorLockMode.None;
            if (view == View.Approaching)
            {
                FlyToEntrance();
                return;
            }
            if (clicked) Cursor.lockState = CursorLockMode.Locked;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Turn(mouse.delta.ReadValue());
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
            UpdateWalkingView();
        }

        void Turn(Vector2 delta)
        {
            yaw += delta.x * .11f;
            pitch = Mathf.Clamp(pitch - delta.y * .11f, -89f, 88f);
        }

        internal void LookFromCanvas(Vector2 delta)
        {
            if (!InputSuppressed || !Walking) return;
            Turn(delta);
            UpdateWalkingView();
        }

        static int Pressed(bool value) => value ? 1 : 0;

        internal void SetSeated(bool seated)
        {
            sittingOffset = seated ? .49f : 0f;
            UpdateWalkingView();
        }

        void UpdateWalkingView() => transform.SetPositionAndRotation(
            player.transform.position + Vector3.up * (eyeHeight - sittingOffset), Quaternion.Euler(pitch, yaw, 0));

        public void EnterWalking(Transform spawn = null)
        {
            if (spawn != null && spawn != player.transform)
            {
                EnterWalkingAt(spawn.position, spawn.rotation);
                return;
            }
            EnterWalkingAt(player.transform.position, player.transform.rotation);
        }

        internal void ApproachSquare(Vector3 feet, Quaternion facing, Action onArrival)
        {
            if (approachGuide == null)
            {
                EnterWalkingAt(feet, facing);
                onArrival();
                return;
            }
            player.enabled = false;
            player.transform.SetPositionAndRotation(feet, facing);
            transform.SetPositionAndRotation(overlook, openingRotation);
            elapsed = 0;
            view = View.Overlook;
            onApproachFinished = onArrival;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        internal void EnterWalkingAt(Vector3 position, Quaternion rotation, float viewPitch = -5f)
        {
            player.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            yaw = player.transform.eulerAngles.y;
            pitch = viewPitch;
            verticalSpeed = 0;
            sittingOffset = 0;
            player.enabled = true;
            view = View.Walking;
            UpdateWalkingView();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void FlyToEntrance()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / approachSeconds));
            Vector3 end = player.transform.position + Vector3.up * eyeHeight;
            var a = Vector3.Lerp(overlook, approachGuide.position, t);
            Vector3 cameraPosition;
            float arrivalPitch = -5f;
            if (onApproachFinished != null)
            {
                // Stay above the cemetery and roofs, then descend into the open square.
                Vector3 aboveSquare = end + Vector3.up * 16f + Vector3.back * 8f;
                var b = Vector3.Lerp(approachGuide.position, aboveSquare, t);
                Vector3 c = Vector3.Lerp(aboveSquare, end, t);
                cameraPosition = Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
                arrivalPitch = 10f;
            }
            else cameraPosition = Vector3.Lerp(a, Vector3.Lerp(approachGuide.position, end, t), t);
            var arrivalRotation = Quaternion.Euler(arrivalPitch, player.transform.eulerAngles.y, 0);
            transform.SetPositionAndRotation(cameraPosition, Quaternion.Slerp(openingRotation, arrivalRotation, t));
            if (elapsed < approachSeconds) return;
            bool captured = Cursor.lockState == CursorLockMode.Locked;
            EnterWalkingAt(player.transform.position, player.transform.rotation, arrivalPitch);
            if (!captured) Cursor.lockState = CursorLockMode.None;
            var arrived = onApproachFinished;
            onApproachFinished = null;
            if (arrived != null) arrived();
            else if (dialoguePlayer != null)
                dialoguePlayer.ShowOpening(chapterOpening);
        }

        void OnGUI()
        {
            if (view != View.Overlook || WaitingAtPainting || InputSuppressed) return;
            var rect = new Rect((Screen.width-164)*.5f, Screen.height-62, 164, 34);
            GUI.Box(rect, "点击进入村落");
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Cursor.lockState = CursorLockMode.None;
        }

        void OnDisable() => Cursor.lockState = CursorLockMode.None;
    }

    internal sealed class WalkingTrail
    {
        readonly Queue<Vector3> points = new();
        Vector3 lastPoint;

        internal void StartAt(Vector3 position)
        {
            points.Clear();
            lastPoint = position;
            points.Enqueue(position);
        }

        internal void Follow(Transform actor, Vector3 player, Func<Vector3, Vector3> ground)
        {
            if (HorizontalDistance(lastPoint, player) >= .45f)
            {
                points.Enqueue(player);
                lastPoint = player;
            }
            if (!points.TryPeek(out Vector3 target)) return;
            if (points.Count < 4 && HorizontalDistance(actor.position, player) < 1.55f) return;
            target.y = actor.position.y;
            if (HorizontalDistance(actor.position, target) < .12f) points.Dequeue();
            else actor.position = ground(Vector3.MoveTowards(actor.position, target, 5.5f * Time.deltaTime));
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            offset.y = 0;
            return offset.magnitude;
        }
    }
}

