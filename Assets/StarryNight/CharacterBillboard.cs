using UnityEngine;

namespace Puzzle.StarryNight
{
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class CharacterBillboard : MonoBehaviour
    {
        Camera playerCamera;

        void LateUpdate()
        {
            if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                playerCamera = Camera.main;
            if (playerCamera == null) return;

            Vector3 forward = transform.position - playerCamera.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
