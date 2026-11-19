using UnityEngine;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class StoryStartMenu : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] Texture2D cover;

        void Start()
        {
            StoryMenu.SetCursor(false);
            var screen = StoryMenu.Screen(GetComponent<UIDocument>(), font);
            screen.Clear();
            var art = StoryMenu.Panel(screen, "story-start-art", false);
            art.style.backgroundImage = new StyleBackground(cover);
            var layout = StoryMenu.Panel(screen, "story-start");
            var card = StoryMenu.Panel(layout, "story-title-card");
            StoryMenu.Label(card, "畫中故事", "story-title");
            StoryMenu.Button(card, "開始遊戲   →", () => StoryProgress.OpenMenu(StoryProgress.GalleryScene),
                "story-primary").Focus();
        }
    }
}
