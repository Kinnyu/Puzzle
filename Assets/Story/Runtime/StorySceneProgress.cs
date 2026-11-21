using System;
using System.Collections;
using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

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
        [SerializeField] DialogueTrigger chapterOneVincent;
        [SerializeField] StarryNightChapterTwo chapterTwo;
        [SerializeField] StarryNightChapterThree chapterThree;
        [SerializeField] StarryNightChapterFour chapterFour;

        StoryDefinition.Chapter chapter;
        Label toast;
        Label objective;
        string objectiveText;
        VisualElement overlay;
        VisualElement card;
        VisualElement historyOverlay;
        ScrollView historyScroll;
        Button historyButton;
        Button historyClose;
        Label historyTitle;
        VisualElement focusBeforeOverlay;
        VisualElement focusBeforeHistory;
        CursorLockMode cursorLockBeforeOverlay;
        bool cursorVisibleBeforeOverlay;
        bool inputSuppressedBeforeOverlay;
        float timeScaleBeforeOverlay;
        bool modalOpen;
        bool historyOpen;
        float toastUntil;
        bool transitioning;
        bool inputBeforeFade;
        VisualElement sceneFade;
        Action afterReveal;
        ExpandedStarryNight expanded;
        internal bool SceneTransitioning { get; private set; }
        internal StarryNightSound Sound { get; private set; }
        internal StarryNightWalk Walker => walk;

        void Start()
        {
            chapter = story != null ? story.FindChapter(StoryProgress.EntryChapterId(story, chapterId)) : null;
            if (chapter == null || !StoryProgress.TryGetEntry(story, chapter, out string checkpoint))
            {
                chapter = null;
                return;
            }

            var screen = StoryMenu.Screen(GetComponent<UIDocument>(), font);
            screen.Clear();
            toast = StoryMenu.Label(screen, "", "story-toast");
            objective = StoryMenu.Label(screen, "", "story-objective");
            overlay = StoryMenu.Panel(screen, "story-overlay");
            card = StoryMenu.Panel(overlay, "story-card");
            if (dialoguePlayer != null) CreateHistoryUI(screen);
            sceneFade = StoryMenu.Panel(screen, "story-scene-fade");
            StoryMenu.Show(sceneFade, false);
            StoryMenu.Show(toast, false);
            StoryMenu.Show(objective, false);
            StoryMenu.Show(overlay, false);

            if (walk != null)
            {
                walk.EscapeHandledExternally = true;
                walk.PointerOverMenu = PointerOverHistoryButton;
                Sound = walk.gameObject.AddComponent<StarryNightSound>();
                Sound.Initialize(walk.Player);
            }
            if (dialoguePlayer != null) dialoguePlayer.LineChanged += OnDialogueLine;
            BeginChapter(checkpoint);
        }

        void BeginChapter(string checkpoint, bool continuing = false)
        {
            if (story.Id == "starry-night" && walk != null && dialoguePlayer != null &&
                chapterOneVincent != null && chapterTwo != null && chapterThree != null && chapterFour != null)
            {
                if (expanded == null)
                {
                    expanded = gameObject.AddComponent<ExpandedStarryNight>();
                    expanded.Initialize(this, walk, dialoguePlayer, chapterOneVincent, chapterTwo, chapterThree, chapterFour);
                }
                expanded.Begin(chapter.id, checkpoint);
                return;
            }
            switch (chapter.id)
            {
                case "chapter-1" when chapterOneVincent != null:
                    var point = Array.Find(checkpoints ?? Array.Empty<CheckpointBinding>(), binding => binding?.id == checkpoint);
                    if (point?.spawn != null) walk.EnterWalking(point.spawn);
                    gameObject.AddComponent<StarryNightChapterOne>().Begin(this, walk, dialoguePlayer, chapterOneVincent,
                        checkpoint);
                    break;
                case StarryNightChapterTwo.ChapterId when chapterTwo != null:
                    chapterTwo.Begin(this, checkpoint, continuing);
                    break;
                case StarryNightChapterThree.ChapterId when chapterThree != null:
                    chapterThree.Begin(this, checkpoint);
                    break;
                case StarryNightChapterFour.ChapterId when chapterFour != null:
                    chapterFour.Begin(this, checkpoint);
                    break;
                default:
                    if (!string.IsNullOrEmpty(checkpoint)) RestoreCheckpoint(checkpoint);
                    break;
            }
        }

        void OnDestroy()
        {
            if (dialoguePlayer != null) dialoguePlayer.LineChanged -= OnDialogueLine;
            if (walk != null) walk.PointerOverMenu = null;
        }

        void OnDisable()
        {
            if (SceneTransitioning)
            {
                StopAllCoroutines();
                SceneTransitioning = false;
                afterReveal = null;
                if (walk != null) walk.InputSuppressed = inputBeforeFade;
                StoryMenu.Show(sceneFade, false);
            }
            if (!modalOpen) return;
            StoryMenu.Show(overlay, false);
            if (historyOverlay != null) StoryMenu.Show(historyOverlay, false);
            overlay.SetEnabled(true);
            historyOpen = false;
            ResumeGameplay();
        }

        void Update()
        {
            if (chapter == null) return;
            StoryMenu.Show(objective, !modalOpen && !SceneTransitioning && !string.IsNullOrEmpty(objective.text)
                && (dialoguePlayer == null || !dialoguePlayer.BlocksGameplayInput));
            if (toastUntil > 0 && Time.time >= toastUntil)
            {
                toastUntil = 0;
                StoryMenu.Show(toast, false);
            }

            if (SceneTransitioning || transitioning) return;
            var keys = Keyboard.current;
            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                if (historyOpen) CloseHistory();
                else if (modalOpen) CloseOverlay();
                else if (expanded != null && expanded.CloseCanvas()) return;
                else OpenOverlay(chapter.title, false);
            }
            else if (historyButton != null && keys?.hKey.wasPressedThisFrame == true && expanded?.CanvasOpen != true)
                ToggleHistory();
        }

        // Connect scene triggers, puzzles or dialogue completion to this method in the Inspector.
        public void SaveCheckpoint(string id)
        {
            if (chapter == null || !StoryProgress.SaveCheckpoint(story, chapter, id)) return;
            Notify($"已記錄　{chapter.FindCheckpoint(id).title}", 2.8f);
        }

        internal void SaveCemeteryStudy(PaintingStudy study) => StoryProgress.SaveCemeteryStudy(story, chapter, study);

        internal void SaveChoice(string id, int value) => StoryProgress.SaveChoice(story, chapter, id, value);

        internal int ReadChoice(int act, string id, int fallback = -1)
        {
            var record = StoryProgress.Read(story, story.FindChapter($"chapter-{act}"));
            return record.choices?.Find(item => item.id == id)?.value ?? fallback;
        }

        internal PaintingStudy LoadCemeteryStudy()
        {
            var cemeteryChapter = story.FindChapter(StarryNightChapterThree.ChapterId);
            int saved = StoryProgress.Read(story, cemeteryChapter).cemeteryStudy;
            return saved > 0 && saved <= (int)PaintingStudy.All ? (PaintingStudy)saved : PaintingStudy.All;
        }

        public void CompleteChapter()
        {
            if (chapter == null || transitioning) return;
            StoryProgress.MarkCleared(story, chapter);
            var (nextId, completedAct) = chapter.id switch
            {
                "chapter-1" when chapterTwo != null => (StarryNightChapterTwo.ChapterId, 1),
                StarryNightChapterTwo.ChapterId when chapterThree != null => (StarryNightChapterThree.ChapterId, 2),
                StarryNightChapterThree.ChapterId when chapterFour != null => (StarryNightChapterFour.ChapterId, 3),
                _ => (null, 0)
            };
            var following = nextId != null ? story.FindChapter(nextId) : null;
            if (following != null && following.IsPlayable)
            {
                StartCoroutine(ContinueInto(following, completedAct));
                return;
            }
            if (chapterTwo != null && chapter.id == StarryNightChapterTwo.ChapterId)
            {
                Notify("CHAPTER 2　— CLEAR —", 3f);
                return;
            }
            OpenOverlay(chapter.id == StarryNightChapterFour.ChapterId
                ? "CHAPTER 4　— CLEAR —\nTHE END" : $"{chapter.title}　已通關", true);
        }

        IEnumerator ContinueInto(StoryDefinition.Chapter following, int completedAct)
        {
            transitioning = true;
            if (walk != null) walk.InputSuppressed = true;
            float hold = completedAct == 1 ? 1.4f : 3f;
            Notify($"CHAPTER {completedAct}　— CLEAR —", hold);
            yield return new WaitForSeconds(hold);

            chapter = following;
            if (walk != null) walk.InputSuppressed = false;
            BeginChapter(null, continuing: true);
            transitioning = false;
        }

        internal void AddChapterUI(VisualElement element) => overlay.parent.Insert(overlay.parent.IndexOf(overlay), element);

        internal void AfterReveal(Action action)
        {
            if (SceneTransitioning) afterReveal += action;
            else action();
        }

        internal void TransitionScene(Action change, bool distant, bool opening = false)
        {
            if (!SceneTransitioning) StartCoroutine(FadeScene(change, distant, opening));
        }

        IEnumerator FadeScene(Action change, bool distant, bool opening)
        {
            SceneTransitioning = true;
            inputBeforeFade = walk.InputSuppressed;
            walk.InputSuppressed = true;
            sceneFade.style.opacity = opening ? 1f : 0f;
            StoryMenu.Show(sceneFade, true);
            Action revealed = null;
            try
            {
                if (!opening) yield return FadeOpacity(0f, 1f, distant ? .42f : .17f);
                change();
                // Give the renderer and the new scene's layouts a frame while the screen is covered.
                yield return null;
                if (distant) yield return new WaitForSecondsRealtime(.12f);
                yield return FadeOpacity(1f, 0f, distant ? .48f : .24f);
                revealed = afterReveal;
            }
            finally
            {
                StoryMenu.Show(sceneFade, false);
                SceneTransitioning = false;
                walk.InputSuppressed = inputBeforeFade;
                afterReveal = null;
            }
            revealed?.Invoke();
        }

        IEnumerator FadeOpacity(float from, float to, float seconds)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            for (double elapsed = 0; elapsed < seconds; elapsed = Time.realtimeSinceStartupAsDouble - start)
            {
                sceneFade.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (float)(elapsed / seconds)));
                yield return null;
            }
            sceneFade.style.opacity = to;
        }

        internal void SetObjective(string text, string detail = "")
        {
            string display = text + detail;
            if (objective.text != display) objective.text = display;
            if (objectiveText == text) return;
            objectiveText = text;
            dialoguePlayer?.RecordHistory(dialoguePlayer.Local("目前目標", "現在の目標"), text);
        }

        internal void Notify(string text, float seconds)
        {
            toast.text = text;
            toastUntil = Time.time + seconds;
            StoryMenu.Show(toast, true);
            dialoguePlayer?.RecordHistory(dialoguePlayer.Local("提示", "お知らせ"), text);
        }

        void OnDialogueLine(DialogueConversation asset, string nodeId)
        {
            if (chapter == null || chapter.id != chapterId) return;
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
            SuspendGameplay();
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
            ResumeGameplay();
        }

        void CreateHistoryUI(VisualElement screen)
        {
            historyButton = StoryMenu.Button(screen, "", ToggleHistory, "story-history-button");
            historyButton.tooltip = "對話紀錄 [H]";
            var icon = StoryMenu.Panel(historyButton, "story-history-icon", false);
            StoryMenu.Panel(icon, "story-history-icon-tail", false);

            historyOverlay = StoryMenu.Panel(screen, "story-overlay");
            var historyCard = StoryMenu.Panel(historyOverlay, "story-card");
            historyCard.AddToClassList("story-history-card");
            var header = StoryMenu.Panel(historyCard, "story-history-header");
            historyTitle = StoryMenu.Label(header, "", "story-card-title");
            historyClose = StoryMenu.Button(header, "", CloseHistory);
            historyScroll = new ScrollView { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            historyScroll.AddToClassList("story-history-scroll");
            historyCard.Add(historyScroll);
            StoryMenu.Show(historyOverlay, false);
        }

        bool PointerOverHistoryButton(Vector2 position)
        {
            if (historyButton?.panel == null || Cursor.lockState == CursorLockMode.Locked) return false;
            position.y = Screen.height - position.y;
            return historyButton.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(historyButton.panel, position));
        }

        void ToggleHistory()
        {
            if (SceneTransitioning || transitioning || expanded?.CanvasOpen == true) return;
            if (historyOpen)
            {
                CloseHistory();
                return;
            }

            focusBeforeHistory = overlay.focusController.focusedElement as VisualElement;
            SuspendGameplay();
            overlay.SetEnabled(false);
            historyOpen = true;
            historyTitle.text = dialoguePlayer.Local("對話紀錄", "会話履歴");
            historyClose.text = dialoguePlayer.Local("關閉", "閉じる");
            historyScroll.Clear();
            foreach (var entry in dialoguePlayer.History)
            {
                var item = StoryMenu.Panel(historyScroll, "story-history-entry", false);
                if (!string.IsNullOrWhiteSpace(entry.Heading))
                    StoryMenu.Label(item, entry.Heading, "story-history-heading");
                var body = StoryMenu.Label(item, entry.Text, "story-history-text");
                dialoguePlayer.ApplyFont(body, entry.Language);
            }
            if (dialoguePlayer.History.Count == 0)
                StoryMenu.Label(historyScroll, dialoguePlayer.Local("目前沒有紀錄。", "履歴はまだありません。"),
                    "story-history-text");
            historyScroll.scrollOffset = Vector2.zero;
            StoryMenu.Show(historyOverlay, true);
            historyClose.Focus();
        }

        void CloseHistory()
        {
            historyOpen = false;
            StoryMenu.Show(historyOverlay, false);
            overlay.SetEnabled(true);
            if (overlay.style.display == DisplayStyle.Flex)
                focusBeforeHistory?.Focus();
            else
                ResumeGameplay();
            focusBeforeHistory = null;
        }

        void SuspendGameplay()
        {
            if (modalOpen) return;
            modalOpen = true;
            focusBeforeOverlay = overlay.focusController.focusedElement as VisualElement;
            cursorLockBeforeOverlay = Cursor.lockState;
            cursorVisibleBeforeOverlay = Cursor.visible;
            timeScaleBeforeOverlay = Time.timeScale;
            Time.timeScale = 0f;
            if (walk != null)
            {
                inputSuppressedBeforeOverlay = walk.InputSuppressed;
                walk.InputSuppressed = true;
            }
            dialoguePlayer?.SetSuspended(true);
            StoryMenu.SetCursor(false);
        }

        void ResumeGameplay()
        {
            modalOpen = false;
            Time.timeScale = timeScaleBeforeOverlay;
            if (walk != null) walk.InputSuppressed = inputSuppressedBeforeOverlay;
            dialoguePlayer?.SetSuspended(false);
            Cursor.lockState = cursorLockBeforeOverlay;
            Cursor.visible = cursorVisibleBeforeOverlay;
            focusBeforeOverlay?.Focus();
            focusBeforeOverlay = null;
        }
    }
}
