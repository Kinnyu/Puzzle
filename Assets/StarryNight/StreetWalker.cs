using Puzzle.Story;
using UnityEngine;

namespace Puzzle.StarryNight
{
    public sealed class StreetWalker : MonoBehaviour
    {
        [SerializeField] StarryNightChapterTwo terrain;
        [SerializeField] Sprite sprite;
        [SerializeField] Vector3 firstCorner, lastCorner;
        [SerializeField, Min(.1f)] float speed = .9f;
        [SerializeField] bool towardLast;
        SpriteRenderer figure;

        void Start()
        {
            figure = CharacterBillboard.AddSprite(gameObject, sprite);
        }

        void Update()
        {
            Vector3 position = transform.position;
            Vector3 destination = towardLast ? lastCorner : firstCorner;
            destination.y = position.y;
            Vector3 next = Vector3.MoveTowards(position, destination, speed * Time.deltaTime);
            transform.position = terrain.Ground(next);
            var camera = Camera.main;
            if (camera != null && next != position)
                figure.flipX = Vector3.Dot(next - position, camera.transform.right) < 0;
            if (next == destination) towardLast = !towardLast;
        }
    }
}
