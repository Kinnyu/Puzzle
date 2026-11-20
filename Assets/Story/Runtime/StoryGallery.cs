using System.Collections.Generic;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Puzzle.Story
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StoryGallery : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] StarryNightWalk walk;
        [SerializeField] Font font;
        [SerializeField, Min(0.1f)] float reach = 3.4f;
        [SerializeField] GalleryDrawingDesk drawingDesk;

        readonly List<(StoryDefinition.Chapter chapter, Button button)> rows = new();
        VisualElement reticle;
        Label prompt;
        Label controls;
        VisualElement overlay;
        VisualElement card;
        Label detail;
        Button resume;
        Button restart;
        Button deleteSave;
        Button confirm;
        StoryDefinition story;
        StoryDefinition.Chapter selected;
        string selectedCheckpoint;
        bool confirmingDelete;

        void Start()
        {
            var screen = StoryMenu.Screen(GetComponent<UIDocument>(), font);
            screen.Clear();
            reticle = StoryMenu.Panel(screen, "story-reticle", false);
            prompt = StoryMenu.Label(screen, "", "story-prompt");
            controls = StoryMenu.Label(screen, "WASD 移動   ·   滑鼠轉向   ·   Esc 回主選單", "story-controls");
            overlay = StoryMenu.Panel(screen, "story-overlay");
            card = StoryMenu.Panel(overlay, "story-card");
            StoryMenu.Show(overlay, false);
            StoryMenu.Show(prompt, false);
            if (drawingDesk != null) drawingDesk.Initialize(view, walk, screen);
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (story != null)
            {
                if (keys?.escapeKey.wasPressedThisFrame == true)
                {
                    if (confirmingDelete) Open(story, selected);
                    else Close();
                }
                return;
            }

            bool captured = Cursor.lockState == CursorLockMode.Locked;
            Collider target = null;
            if (captured && Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var hit, reach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = hit.collider;

            if (drawingDesk != null && drawingDesk.HandleInput(target, captured, out string deskPrompt))
            {
                bool seated = drawingDesk.IsSeated;
                StoryMenu.Show(reticle, captured && !seated);
                reticle.EnableInClassList("story-reticle-active", target != null && !seated);
                StoryMenu.Show(controls, !seated);
                StoryMenu.Show(prompt, !string.IsNullOrEmpty(deskPrompt));
                prompt.text = deskPrompt;
                return;
            }

            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                StoryProgress.OpenMenu(StoryProgress.StartScene);
                return;
            }

            var painting = target != null ? target.GetComponentInParent<StoryPainting>() : null;
            bool aimed = painting != null && painting.Story != null;
            StoryMenu.Show(controls, true);
            reticle.EnableInClassList("story-reticle-active", aimed);
            StoryMenu.Show(reticle, captured);
            StoryMenu.Show(prompt, aimed || !captured);
            if (!captured)
                prompt.text = "點擊畫面，繼續探索";
            else if (aimed)
            {
                prompt.text = $"E  或點擊   查看《{painting.Story.Title}》";
                if (keys?.eKey.wasPressedThisFrame == true || Mouse.current?.leftButton.wasPressedThisFrame == true)
                    Open(painting.Story);
            }
        }

        void Open(StoryDefinition definition, StoryDefinition.Chapter initialChapter = null)
        {
            story = definition;
            selected = null;
            confirmingDelete = false;
            rows.Clear();
            card.Clear();
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            StoryMenu.Show(prompt, false);
            StoryMenu.Show(reticle, false);
            StoryMenu.Show(controls, false);
            StoryMenu.Show(overlay, true);

            StoryMenu.Label(card, story.Title, "story-card-title");
            var chapters = new ScrollView();
            chapters.AddToClassList("story-chapters");
            card.Add(chapters);
            Button initial = null;
            for (int i = 0; i < story.Chapters.Count; i++)
            {
                var chapter = story.Chapters[i];
                if (chapter == null) continue;
                var record = StoryProgress.Read(story, chapter);
                bool available = story.IsUnlocked(chapter);
                bool playable = chapter.IsPlayable;
                string status = !available ? "未解鎖" : !playable ? "尚未開放" :
                    record.cleared ? "已通關" : chapter.FindCheckpoint(record.checkpointId) != null ? "有紀錄" : "可遊玩";
                var row = StoryMenu.Button(chapters, $"{i + 1:00}   {chapter.title}    ·    {status}",
                    () => Select(chapter), "story-chapter");
                row.SetEnabled(available && playable);
                rows.Add((chapter, row));
                if (row.enabledSelf && (initial == null || chapter == initialChapter)) initial = row;
            }

            detail = StoryMenu.Label(card, "", "story-detail");
            var actions = StoryMenu.Panel(card, "story-row");
            StoryMenu.Button(actions, "取消", Close);
            restart = StoryMenu.Button(actions, "從章首", () => ChooseCheckpoint(null));
            resume = StoryMenu.Button(actions, "讀取記錄點", () =>
                ChooseCheckpoint(StoryProgress.Read(story, selected).checkpointId));
            deleteSave = StoryMenu.Button(actions, "刪除存檔", ShowDeleteConfirmation);
            confirm = StoryMenu.Button(actions, "確認進入", Confirm, "story-primary");
            if (initial != null)
            {
                Select(rows.Find(row => row.button == initial).chapter);
                initial.Focus();
            }
            else
            {
                detail.text = "目前沒有可遊玩的章節";
                StoryMenu.Show(restart, false);
                StoryMenu.Show(resume, false);
                StoryMenu.Show(deleteSave, false);
                confirm.SetEnabled(false);
            }
        }

        void Select(StoryDefinition.Chapter chapter)
        {
            selected = chapter;
            foreach (var row in rows)
                row.button.EnableInClassList("story-selected", row.chapter == chapter);

            string saved = StoryProgress.Read(story, chapter).checkpointId;
            bool canResume = chapter.FindCheckpoint(saved) != null;
            StoryMenu.Show(restart, canResume);
            StoryMenu.Show(resume, canResume);
            StoryMenu.Show(deleteSave, StoryProgress.HasSave(story, chapter));
            ChooseCheckpoint(canResume ? saved : null);
        }

        void ShowDeleteConfirmation()
        {
            if (!StoryProgress.HasSave(story, selected)) return;

            var chapter = selected;
            confirmingDelete = true;
            card.Clear();
            StoryMenu.Label(card, $"刪除〈{chapter.title}〉的存檔？", "story-card-title");
            StoryMenu.Label(card, "記錄點與通關紀錄會永久移除，依賴此通關紀錄的章節也可能重新上鎖。", "story-detail");
            var actions = StoryMenu.Panel(card, "story-row");
            var cancel = StoryMenu.Button(actions, "取消", () => Open(story, chapter));
            StoryMenu.Button(actions, "永久刪除", () =>
            {
                StoryProgress.DeleteSave(story, chapter);
                Open(story, chapter);
            }, "story-danger");
            cancel.Focus();
        }

        void ChooseCheckpoint(string checkpoint)
        {
            selectedCheckpoint = checkpoint;
            var point = selected.FindCheckpoint(checkpoint);
            detail.text = point == null ? "進入位置　章節開頭" : $"進入位置　{point.title}";
            resume.EnableInClassList("story-selected", point != null);
            restart.EnableInClassList("story-selected", point == null);
        }

        void Confirm()
        {
            if (selected != null && StoryProgress.EnterChapter(story, selected, selectedCheckpoint))
                card.SetEnabled(false);
        }

        void Close()
        {
            story = null;
            walk.InputSuppressed = false;
            StoryMenu.Show(overlay, false);
            StoryMenu.Show(controls, true);
            StoryMenu.SetCursor(true);
        }
    }
}
