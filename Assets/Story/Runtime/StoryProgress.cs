using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Puzzle.Story
{
    public static class StoryProgress
    {
        [Serializable]
        public sealed class ChapterRecord
        {
            public string checkpointId;
            public bool cleared;
        }

        internal const string StartScene = "Assets/Scenes/Start.unity";
        internal const string GalleryScene = "Assets/Scenes/StoryGallery.unity";

        static (StoryDefinition story, StoryDefinition.Chapter chapter, string checkpoint) pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRoute() => pending = default;

        static string SaveKey(StoryDefinition story, StoryDefinition.Chapter chapter) =>
            $"Puzzle.Story.v1.{story.Id.Length}.{story.Id}.{chapter.id}";

        internal static bool HasSave(StoryDefinition story, StoryDefinition.Chapter chapter) =>
            story != null && chapter != null && !string.IsNullOrWhiteSpace(story.Id)
            && !string.IsNullOrWhiteSpace(chapter.id) && PlayerPrefs.HasKey(SaveKey(story, chapter));

        internal static void DeleteSave(StoryDefinition story, StoryDefinition.Chapter chapter)
        {
            if (!HasSave(story, chapter)) return;
            PlayerPrefs.DeleteKey(SaveKey(story, chapter));
            PlayerPrefs.Save();
        }

        public static ChapterRecord Read(StoryDefinition story, StoryDefinition.Chapter chapter)
        {
            if (story == null || chapter == null || string.IsNullOrWhiteSpace(story.Id)
                || string.IsNullOrWhiteSpace(chapter.id))
                return new ChapterRecord();
            string json = PlayerPrefs.GetString(SaveKey(story, chapter));
            if (string.IsNullOrEmpty(json)) return new ChapterRecord();
            try
            {
                return JsonUtility.FromJson<ChapterRecord>(json) ?? new ChapterRecord();
            }
            catch (ArgumentException)
            {
                return new ChapterRecord();
            }
        }

        internal static bool SaveCheckpoint(StoryDefinition story, StoryDefinition.Chapter chapter, string id)
        {
            int index = chapter.CheckpointIndex(id);
            var record = Read(story, chapter);
            if (index < 0 || index <= chapter.CheckpointIndex(record.checkpointId)) return false;
            record.checkpointId = id;
            Write(story, chapter, record);
            return true;
        }

        internal static void MarkCleared(StoryDefinition story, StoryDefinition.Chapter chapter)
        {
            var record = Read(story, chapter);
            if (record.cleared) return;
            record.cleared = true;
            Write(story, chapter, record);
        }

        static void Write(StoryDefinition story, StoryDefinition.Chapter chapter, ChapterRecord record)
        {
            PlayerPrefs.SetString(SaveKey(story, chapter), JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        internal static bool EnterChapter(StoryDefinition story, StoryDefinition.Chapter chapter, string checkpoint)
        {
            if (!chapter.IsPlayable || !story.IsUnlocked(chapter)) return false;
            if (!string.IsNullOrEmpty(checkpoint) && chapter.FindCheckpoint(checkpoint) == null) return false;
            pending = (story, chapter, checkpoint);
            SceneManager.LoadScene(chapter.scenePath);
            return true;
        }

        internal static bool TryGetEntry(StoryDefinition story, StoryDefinition.Chapter chapter, out string checkpoint)
        {
            checkpoint = null;
            if (pending.story == null) return true;
            if (pending.story != story || pending.chapter != chapter) return false;
            checkpoint = pending.checkpoint;
            return true;
        }

        internal static string EntryChapterId(StoryDefinition story, string defaultId) =>
            pending.story == story && pending.chapter != null ? pending.chapter.id : defaultId;

        internal static void OpenMenu(string scenePath)
        {
            pending = default;
            SceneManager.LoadScene(scenePath);
        }
    }
}
