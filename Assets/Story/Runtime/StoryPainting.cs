using UnityEngine;

namespace Puzzle.Story
{
    [DisallowMultipleComponent]
    public sealed class StoryPainting : MonoBehaviour
    {
        [SerializeField] StoryDefinition story;
        public StoryDefinition Story => story;
    }
}
