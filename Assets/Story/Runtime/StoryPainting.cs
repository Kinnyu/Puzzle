using UnityEngine;

namespace Puzzle.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryPainting : MonoBehaviour
    {
        [SerializeField] StoryDefinition story;
        public StoryDefinition Story => story;

        void Awake()
        {
            if (TryGetComponent<MeshRenderer>(out var surface))
                StoryPropInk.SetSurface(gameObject, surface.sharedMaterial);
        }
    }
}
