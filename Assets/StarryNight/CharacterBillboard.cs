using UnityEngine;

namespace Puzzle.StarryNight
{
    [DisallowMultipleComponent]
    public sealed class CharacterBillboard : MonoBehaviour
    {
        const float HopHeight = .12f;
        const float HopsPerSecond = 3f;

        [SerializeField] Transform visual;

        Camera playerCamera;
        Vector3 previousPosition;
        float hopPhase;

        internal static SpriteRenderer AddSprite(GameObject character, Sprite sprite)
        {
            var visual = new GameObject("Character sprite");
            visual.layer = character.layer;
            visual.transform.SetParent(character.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            character.AddComponent<CharacterBillboard>().visual = visual.transform;
            return renderer;
        }

        void OnEnable()
        {
            previousPosition = transform.position;
            hopPhase = 0f;
            if (visual != null) visual.localPosition = Vector3.zero;
        }

        void LateUpdate()
        {
            UpdateWalk();
            if (playerCamera == null || !playerCamera.isActiveAndEnabled)
                playerCamera = Camera.main;
            if (playerCamera == null) return;

            Vector3 forward = transform.position - playerCamera.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward);
        }

        void UpdateWalk()
        {
            Vector3 position = transform.position;
            Vector3 movement = position - previousPosition;
            previousPosition = position;
            if (visual == null) return;

            movement.y = 0f;
            float distanceSquared = movement.sqrMagnitude;
            float dt = Time.deltaTime;
            if (distanceSquared > .00000001f && distanceSquared < 1f)
            {
                hopPhase = Mathf.Repeat(hopPhase + dt * HopsPerSecond, 1f);
                visual.localPosition = Vector3.up * (Mathf.Sin(hopPhase * Mathf.PI) * HopHeight);
            }
            else
            {
                hopPhase = 0f;
                visual.localPosition = Vector3.MoveTowards(visual.localPosition, Vector3.zero, dt * 1.8f);
            }
        }
    }
}
