using UnityEngine;
using UnityEngine.UIElements;

namespace Puzzle.Dialogue
{
    [CreateAssetMenu(menuName = "Puzzle/Dialogue Appearance")]
    public sealed class DialogueAppearance : ScriptableObject
    {
        [SerializeField, Min(1)] float maxWidth = 800;
        [SerializeField, Range(10, 100)] float maxHeightPercent = 85;
        [SerializeField] StyleSheet styles;

        internal float MaxWidth => Mathf.Max(1, maxWidth);
        internal float MaxHeightPercent => Mathf.Clamp(maxHeightPercent, 10, 100);
        internal StyleSheet Styles => styles;
    }
}
