using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Dialogue
{
    public enum DialogueLanguage { TraditionalChinese, Japanese }

    [Serializable]
    public struct DialogueText
    {
        [SerializeField, TextArea(2, 5)] string traditionalChinese;
        [SerializeField, TextArea(2, 5)] string japanese;

        public string Get(DialogueLanguage language, out DialogueLanguage resolvedLanguage)
        {
            string preferred = language == DialogueLanguage.Japanese ? japanese : traditionalChinese;
            resolvedLanguage = language;
            if (!string.IsNullOrWhiteSpace(preferred)) return preferred;
            string fallback = language == DialogueLanguage.Japanese ? traditionalChinese : japanese;
            if (string.IsNullOrWhiteSpace(fallback)) return string.Empty;
            resolvedLanguage = language == DialogueLanguage.Japanese
                ? DialogueLanguage.TraditionalChinese : DialogueLanguage.Japanese;
            return fallback;
        }
    }

    [CreateAssetMenu(menuName = "Puzzle/Dialogue Conversation")]
    public sealed class DialogueConversation : ScriptableObject
    {
        [Serializable]
        public sealed class Choice
        {
            public DialogueText text;
            [Tooltip("Leave empty to finish the conversation with this choice.")]
            public string nextNodeId;
        }

        [Serializable]
        public sealed class Node
        {
            public string id;
            public DialogueText speaker;
            public DialogueText line;
            public Texture2D portrait;
            public bool portraitOnRight;
            [Tooltip("For a line without choices. Leave empty to finish the conversation.")]
            public string nextNodeId;
            public Choice[] choices;
        }

        [SerializeField] string entryNodeId = "start";
        [SerializeField] Node[] nodes = { new Node { id = "start" } };

        internal bool TryBuildIndex(out Dictionary<string, Node> index, out Node entry, out string error)
        {
            index = new Dictionary<string, Node>(StringComparer.Ordinal);
            entry = null;

            foreach (var node in nodes ?? Array.Empty<Node>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || !index.TryAdd(node.id, node))
                {
                    error = "Every node needs a unique, non-empty ID.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(entryNodeId) || !index.TryGetValue(entryNodeId, out entry))
            {
                error = $"Entry node '{entryNodeId}' does not exist.";
                return false;
            }

            foreach (var node in index.Values)
            {
                if (!string.IsNullOrEmpty(node.nextNodeId) && !index.ContainsKey(node.nextNodeId))
                {
                    error = $"Node '{node.id}' links to missing node '{node.nextNodeId}'.";
                    return false;
                }

                foreach (var choice in node.choices ?? Array.Empty<Choice>())
                {
                    if (choice == null || (!string.IsNullOrEmpty(choice.nextNodeId) && !index.ContainsKey(choice.nextNodeId)))
                    {
                        error = $"Node '{node.id}' has an invalid choice or a missing choice target ('{choice?.nextNodeId}').";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
