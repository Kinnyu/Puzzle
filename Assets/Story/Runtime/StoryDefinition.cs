using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Story
{
    [CreateAssetMenu(menuName = "Puzzle/Story")]
    public sealed class StoryDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Chapter
        {
            public string id;
            public string title;
            [Tooltip("Use the full scene asset path from the build scene list. Leave empty for upcoming content.")]
            public string scenePath;
            public Checkpoint[] checkpoints;

            public bool IsPlayable => !string.IsNullOrEmpty(scenePath)
                && Application.CanStreamedLevelBeLoaded(scenePath);

            internal int CheckpointIndex(string checkpointId)
            {
                if (string.IsNullOrEmpty(checkpointId) || checkpoints == null) return -1;
                return Array.FindIndex(checkpoints, point => point != null && point.id == checkpointId);
            }

            internal Checkpoint FindCheckpoint(string checkpointId)
            {
                int index = CheckpointIndex(checkpointId);
                return index < 0 ? null : checkpoints[index];
            }
        }

        [Serializable]
        public sealed class Checkpoint
        {
            public string id;
            public string title;
        }

        [SerializeField, Tooltip("Keep this ID and chapter/checkpoint IDs stable after publishing.")]
        string storyId;
        [SerializeField] string title;
        [SerializeField] Chapter[] chapters;

        public string Id => storyId;
        public string Title => title;
        public IReadOnlyList<Chapter> Chapters => chapters ?? Array.Empty<Chapter>();

        internal Chapter FindChapter(string chapterId) =>
            Array.Find(chapters ?? Array.Empty<Chapter>(), chapter => chapter != null && chapter.id == chapterId);

        public bool IsUnlocked(Chapter chapter)
        {
            for (int i = 0; i < Chapters.Count; i++)
            {
                if (Chapters[i] != chapter) continue;
                return i == 0 || StoryProgress.Read(this, Chapters[i - 1]).cleared
                    || StoryProgress.Read(this, chapter).cleared;
            }

            return false;
        }
    }
}
