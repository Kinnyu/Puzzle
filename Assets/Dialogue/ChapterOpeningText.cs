using UnityEngine;

namespace Puzzle.Dialogue
{
    [CreateAssetMenu(menuName = "Puzzle/Chapter Opening Text")]
    public sealed class ChapterOpeningText : ScriptableObject
    {
        [SerializeField] DialogueText title;
        [SerializeField] DialogueText objective;

        internal DialogueText Title => title;
        internal DialogueText Objective => objective;
    }
}
