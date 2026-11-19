using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Puzzle.Dialogue
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DialoguePlayer : MonoBehaviour
    {
        const string LanguagePreference = "Puzzle.Dialogue.Language";

        [SerializeField] Font traditionalChineseFont;
        [SerializeField] Font japaneseFont;
        [SerializeField] DialogueAppearance defaultAppearance;
        [SerializeField] Key interactKey = Key.E;

        readonly List<DialogueTrigger> triggers = new();
        VisualElement overlay;
        VisualElement card;
        VisualElement actions;
        Label prompt;
        Label speaker;
        Label line;
        Button languageButton;
        Button closeButton;
        ScrollView scroll;
        DialogueConversation conversation;
        Dictionary<string, DialogueConversation.Node> nodes;
        DialogueConversation.Node currentNode;
        DialogueAppearance activeAppearance;
        Action onCompleted;
        DialogueLanguage language;
        CursorLockMode previousLock;
        bool previousCursorVisibility;
        int closedFrame = -1;

        public event Action<DialogueConversation> Completed;
        public event Action<DialogueConversation, string> LineChanged;
        public bool IsPlaying => conversation != null;
        public bool BlocksGameplayInput => IsPlaying || closedFrame == Time.frameCount;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            overlay = root?.Q("dialogue-overlay");
            card = root?.Q("dialogue-card");
            actions = root?.Q("dialogue-actions");
            prompt = root?.Q<Label>("dialogue-prompt");
            speaker = root?.Q<Label>("dialogue-speaker");
            line = root?.Q<Label>("dialogue-line");
            languageButton = root?.Q<Button>("dialogue-language");
            closeButton = root?.Q<Button>("dialogue-close");
            scroll = root?.Q<ScrollView>("dialogue-scroll");
            if (overlay == null || card == null || actions == null || prompt == null || speaker == null
                || line == null || languageButton == null || closeButton == null || scroll == null)
            {
                Debug.LogError("Dialogue visual tree is missing required named elements. Use Dialogue.uxml as a template.", this);
                overlay = null;
                return;
            }

            speaker.enableRichText = false;
            line.enableRichText = false;
            overlay.style.display = DisplayStyle.None;
            languageButton.clicked += ToggleLanguage;
            closeButton.clicked += Cancel;
            SetLanguage(PlayerPrefs.GetInt(LanguagePreference) == (int)DialogueLanguage.Japanese
                ? DialogueLanguage.Japanese : DialogueLanguage.TraditionalChinese);
        }

        void OnDisable()
        {
            Cancel();
            SetPromptVisible(false);
            if (overlay == null) return;
            languageButton.clicked -= ToggleLanguage;
            closeButton.clicked -= Cancel;
            overlay = null;
        }

        internal void Register(DialogueTrigger trigger) => triggers.Add(trigger);
        internal void Unregister(DialogueTrigger trigger) => triggers.Remove(trigger);

        void LateUpdate()
        {
            if (overlay == null) return;
            var keyboard = Keyboard.current;
            if (IsPlaying && keyboard?.escapeKey.wasPressedThisFrame == true) Cancel();

            DialogueTrigger nearby = null;
            DialogueTrigger automatic = null;
            float nearbyDistance = float.PositiveInfinity;
            float automaticDistance = float.PositiveInfinity;
            foreach (var trigger in triggers)
            {
                if (!trigger.TryGetDistance(out float distance, out bool auto)) continue;
                if (auto && distance < automaticDistance)
                {
                    automatic = trigger;
                    automaticDistance = distance;
                }
                if (!auto && distance < nearbyDistance)
                {
                    nearby = trigger;
                    nearbyDistance = distance;
                }
            }

            SetPromptVisible(!IsPlaying && nearby != null && interactKey != Key.None);
            if (BlocksGameplayInput) return;
            if (automatic != null)
                automatic.PlayOnEntry();
            else if (nearby != null && interactKey != Key.None && keyboard?[interactKey].wasPressedThisFrame == true)
                nearby.Play();
        }

        void SetPromptVisible(bool visible)
        {
            if (prompt != null)
                prompt.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public bool TryPlay(DialogueConversation asset, DialogueAppearance appearance = null, Action completed = null)
        {
            if (!isActiveAndEnabled || overlay == null || IsPlaying || asset == null) return false;
            if (!asset.TryBuildIndex(out var index, out var entry, out string error))
            {
                Debug.LogError($"Dialogue '{asset.name}': {error}", asset);
                return false;
            }

            conversation = asset;
            nodes = index;
            currentNode = entry;
            onCompleted = completed;
            previousLock = Cursor.lockState;
            previousCursorVisibility = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetPromptVisible(false);
            ApplyAppearance(appearance != null ? appearance : defaultAppearance);
            overlay.style.display = DisplayStyle.Flex;
            RenderNode();
            LineChanged?.Invoke(asset, entry.id);
            return true;
        }

        public void Cancel()
        {
            if (IsPlaying) Close();
        }

        void ToggleLanguage()
        {
            var next = language == DialogueLanguage.Japanese
                ? DialogueLanguage.TraditionalChinese : DialogueLanguage.Japanese;
            SetLanguage(next);
            PlayerPrefs.SetInt(LanguagePreference, (int)next);
            PlayerPrefs.Save();
            RenderNode();
        }

        void SetLanguage(DialogueLanguage value)
        {
            language = value;
            ApplyFont(prompt, language);
            ApplyFont(languageButton, language == DialogueLanguage.Japanese
                ? DialogueLanguage.TraditionalChinese : DialogueLanguage.Japanese);
            ApplyFont(closeButton, language);

            languageButton.text = Local("日本語", "繁體中文");
            closeButton.text = Local("關閉", "閉じる");
            string key = Keyboard.current != null && interactKey != Key.None
                ? Keyboard.current[interactKey].displayName : interactKey.ToString();
            prompt.text = $"{key}　{Local("對話", "話す")}";
        }

        void ApplyAppearance(DialogueAppearance appearance)
        {
            if (activeAppearance != null && activeAppearance.Styles != null)
                overlay.styleSheets.Remove(activeAppearance.Styles);

            activeAppearance = appearance;
            card.style.maxWidth = StyleKeyword.Null;
            card.style.maxHeight = StyleKeyword.Null;
            if (appearance == null) return;

            card.style.maxWidth = appearance.MaxWidth;
            card.style.maxHeight = Length.Percent(appearance.MaxHeightPercent);
            if (appearance.Styles != null) overlay.styleSheets.Add(appearance.Styles);
        }

        void RenderNode()
        {
            SetText(speaker, currentNode.speaker);
            speaker.style.display = string.IsNullOrWhiteSpace(speaker.text) ? DisplayStyle.None : DisplayStyle.Flex;
            SetText(line, currentNode.line);
            actions.Clear();

            if (currentNode.choices is { Length: > 0 })
            {
                foreach (var choice in currentNode.choices)
                    SetText(AddAction(choice.nextNodeId), choice.text);
            }
            else
            {
                string nextId = currentNode.nextNodeId;
                string label = string.IsNullOrEmpty(nextId) ? Local("結束", "会話を終える") : Local("繼續", "続ける");
                var button = AddAction(nextId);
                button.text = label;
                ApplyFont(button, language);
            }

            scroll.scrollOffset = Vector2.zero;
            actions.ElementAt(0).Focus();
        }

        Button AddAction(string nextId)
        {
            var button = new Button(() => Advance(nextId)) { enableRichText = false };
            button.AddToClassList("dialogue-action");
            actions.Add(button);
            return button;
        }

        void SetText(TextElement element, DialogueText text)
        {
            element.text = text.Get(language, out var resolvedLanguage);
            ApplyFont(element, resolvedLanguage);
        }

        void ApplyFont(TextElement element, DialogueLanguage value)
        {
            Font font = value == DialogueLanguage.Japanese ? japaneseFont : traditionalChineseFont;
            element.style.unityFontDefinition = font != null
                ? new StyleFontDefinition(FontDefinition.FromFont(font)) : new StyleFontDefinition(StyleKeyword.Null);
        }

        void Advance(string nextId)
        {
            if (!IsPlaying) return;
            if (!string.IsNullOrEmpty(nextId))
            {
                currentNode = nodes[nextId];
                RenderNode();
                LineChanged?.Invoke(conversation, currentNode.id);
                return;
            }

            var finished = conversation;
            var callback = onCompleted;
            Close();
            callback?.Invoke();
            Completed?.Invoke(finished);
        }

        void Close()
        {
            conversation = null;
            currentNode = null;
            nodes = null;
            onCompleted = null;
            actions.Clear();
            overlay.style.display = DisplayStyle.None;
            ApplyAppearance(null);
            closedFrame = Time.frameCount;
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursorVisibility;
        }

        string Local(string chinese, string japanese) => language == DialogueLanguage.Japanese ? japanese : chinese;
    }
}
