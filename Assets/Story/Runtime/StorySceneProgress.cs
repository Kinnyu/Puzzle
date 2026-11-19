using System;
using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StorySceneProgress : MonoBehaviour
    {
        [Serializable]
        sealed class CheckpointBinding
        {
            public string id;
            public Transform spawn;
            public DialogueConversation dialogue;
            public string dialogueNodeId;
            public UnityEvent onRestored;
        }

        [SerializeField] StoryDefinition story;
        [SerializeField] string chapterId;
        [SerializeField] CheckpointBinding[] checkpoints;
        [SerializeField] StarryNightWalk walk;
        [SerializeField] DialoguePlayer dialoguePlayer;
        [SerializeField] Font font;

        StoryDefinition.Chapter chapter;
        Label toast;
        VisualElement overlay;
        VisualElement card;
        bool capturedBeforeOverlay;
        float toastUntil;

        void Start()
        {
            chapter = story != null ? story.FindChapter(chapterId) : null;
            if (chapter == null || !StoryProgress.TryGetEntry(story, chapter, out string checkpoint))
            {
                chapter = null;
                return;
            }

            var screen = StoryMenu.Screen(GetComponent<UIDocument>(), font);
            screen.Clear();
            toast = StoryMenu.Label(screen, "", "story-toast");
            overlay = StoryMenu.Panel(screen, "story-overlay");
            card = StoryMenu.Panel(overlay, "story-card");
            StoryMenu.Show(toast, false);
            StoryMenu.Show(overlay, false);

            if (walk != null) walk.EscapeHandledExternally = true;
            if (dialoguePlayer != null) dialoguePlayer.LineChanged += OnDialogueLine;
            if (!string.IsNullOrEmpty(checkpoint)) RestoreCheckpoint(checkpoint);
        }

        void OnDestroy()
        {
            if (dialoguePlayer != null) dialoguePlayer.LineChanged -= OnDialogueLine;
        }

        void Update()
        {
            if (chapter == null) return;
            if (toastUntil > 0 && Time.unscaledTime >= toastUntil)
            {
                toastUntil = 0;
                StoryMenu.Show(toast, false);
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (dialoguePlayer != null && dialoguePlayer.BlocksGameplayInput) return;
            if (overlay.style.display == DisplayStyle.Flex) CloseOverlay();
            else OpenOverlay(chapter.title, false);
        }

        // Connect scene triggers, puzzles or dialogue completion to this method in the Inspector.
        public void SaveCheckpoint(string id)
        {
            if (chapter == null || !StoryProgress.SaveCheckpoint(story, chapter, id)) return;
            toast.text = $"已記錄　{chapter.FindCheckpoint(id).title}";
            toastUntil = Time.unscaledTime + 2.8f;
            StoryMenu.Show(toast, true);
        }

        public void CompleteChapter()
        {
            if (chapter == null) return;
            StoryProgress.MarkCleared(story, chapter);
            OpenOverlay($"{chapter.title}　已通關", true);
        }

        void OnDialogueLine(DialogueConversation asset, string nodeId)
        {
            foreach (var point in checkpoints ?? Array.Empty<CheckpointBinding>())
                if (point != null && point.dialogue == asset && point.dialogueNodeId == nodeId)
                    SaveCheckpoint(point.id);
        }

        void RestoreCheckpoint(string id)
        {
            var point = Array.Find(checkpoints ?? Array.Empty<CheckpointBinding>(), binding => binding?.id == id);
            if (point == null)
            {
                Debug.LogWarning($"Checkpoint '{id}' has no scene binding. Starting from the beginning.", this);
                return;
            }

            if (walk != null) walk.EnterWalking(point.spawn);
            point.onRestored?.Invoke();
        }

        void OpenOverlay(string heading, bool cleared)
        {
            capturedBeforeOverlay = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            if (walk != null) walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            card.Clear();
            StoryMenu.Label(card, heading, "story-card-title");
            var actions = StoryMenu.Panel(card, "story-row");
            var keepPlaying = StoryMenu.Button(actions, cleared ? "繼續探索" : "繼續遊戲", CloseOverlay);
            var gallery = StoryMenu.Button(actions, "回到故事展廳", () => StoryProgress.OpenMenu(StoryProgress.GalleryScene),
                "story-primary");
            StoryMenu.Show(overlay, true);
            (cleared ? gallery : keepPlaying).Focus();
        }

        void CloseOverlay()
        {
            StoryMenu.Show(overlay, false);
            if (walk != null) walk.InputSuppressed = false;
            StoryMenu.SetCursor(capturedBeforeOverlay);
        }
    }
}
