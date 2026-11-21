using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        const float PageScrollSeconds = 0.24f;
        const float MaxLineHeight = 152f;
        const float MaxChoiceHeight = 164f;
        const float LineBottomPadding = 8f;

        [SerializeField] Font traditionalChineseFont;
        [SerializeField] Font japaneseFont;
        [SerializeField] DialogueAppearance defaultAppearance;
        [SerializeField] Key interactKey = Key.E;
        [SerializeField, Min(1f)] float charactersPerSecond = 18f;

        readonly List<DialogueTrigger> triggers = new();
        readonly List<(string Heading, string Text, DialogueLanguage Language)> history = new();
        VisualElement overlay;
        VisualElement shade;
        VisualElement card;
        Image portrait;
        ScrollView actions;
        Label prompt;
        Label openingTitle;
        VisualElement titleScreen;
        VisualElement objectiveScreen;
        Label objectiveHeading;
        Label objectiveText;
        Coroutine openingSequence;
        bool showingChapterTitle;
        DialogueLanguage objectiveLanguage;
        bool suspended;
        bool scrolling;
        bool preparingLine;
        bool lineReady;
        VisualElement page;
        Label speaker;
        Label line;
        Coroutine lineSequence;
        string fullLine;
        DialogueLanguage lineLanguage;
        int[] textElements;
        int visibleCharacters;
        Button languageButton;
        Button closeButton;
        Button advanceButton;
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
        int renderedFrame = -1;
        int interactionFrame = -1;

        public event Action<DialogueConversation> Completed;
        public event Action<DialogueConversation, string> LineChanged;
        public bool IsPlaying => conversation != null;
        public bool BlocksGameplayInput => suspended || IsPlaying || showingChapterTitle || closedFrame == Time.frameCount;
        internal IReadOnlyList<(string Heading, string Text, DialogueLanguage Language)> History => history;
        internal string InteractionKeyLabel => interactKey == Key.None ? string.Empty
            : Keyboard.current?[interactKey].displayName ?? interactKey.ToString();
        internal bool HoldingInteractionKey => interactKey != Key.None && Keyboard.current?[interactKey].isPressed == true;
        bool HasChoices => currentNode.choices is { Length: > 0 };

        internal void SetSuspended(bool value)
        {
            suspended = value;
            overlay?.SetEnabled(!value);
            if (value) SetPromptVisible(false);
            else closedFrame = Time.frameCount;
        }

        internal void RecordHistory(string heading, string text, DialogueLanguage? textLanguage = null)
        {
            if (!string.IsNullOrWhiteSpace(text))
                history.Add((heading, text, textLanguage ?? language));
        }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            overlay = root?.Q("dialogue-overlay");
            shade = root?.Q("dialogue-shade");
            card = root?.Q("dialogue-card");
            portrait = root?.Q<Image>("dialogue-portrait");
            actions = root?.Q<ScrollView>("dialogue-actions");
            prompt = root?.Q<Label>("dialogue-prompt");
            titleScreen = root?.Q("chapter-title-screen");
            openingTitle = root?.Q<Label>("chapter-title");
            objectiveScreen = root?.Q("chapter-objective-screen");
            objectiveHeading = root?.Q<Label>("chapter-objective-heading");
            objectiveText = root?.Q<Label>("chapter-objective-text");
            languageButton = root?.Q<Button>("dialogue-language");
            closeButton = root?.Q<Button>("dialogue-close");
            advanceButton = root?.Q<Button>("dialogue-advance");
            scroll = root?.Q<ScrollView>("dialogue-scroll");
            if (overlay == null || card == null || actions == null || prompt == null
                || languageButton == null || closeButton == null || advanceButton == null || scroll == null)
            {
                Debug.LogError("Dialogue visual tree is missing required named elements. Use Dialogue.uxml as a template.", this);
                overlay = null;
                return;
            }

            if (openingTitle != null) openingTitle.enableRichText = false;
            if (objectiveText != null) objectiveText.enableRichText = false;
            advanceButton.enableRichText = false;
            if (portrait != null) portrait.scaleMode = ScaleMode.ScaleToFit;
            if (shade != null) card.RegisterCallback<GeometryChangedEvent>(ResizeShade);
            overlay.style.display = DisplayStyle.None;
            overlay.SetEnabled(!suspended);
            overlay.RegisterCallback<ClickEvent>(OnOverlayClick, TrickleDown.TrickleDown);
            overlay.RegisterCallback<NavigationSubmitEvent>(OnOverlaySubmit);
            languageButton.clicked += ToggleLanguage;
            closeButton.clicked += CloseFromButton;
            advanceButton.clicked += AdvanceLinearNode;
            SetLanguage(PlayerPrefs.GetInt(LanguagePreference) == (int)DialogueLanguage.Japanese
                ? DialogueLanguage.Japanese : DialogueLanguage.TraditionalChinese);
        }

        void OnDisable()
        {
            StopOpening();
            Cancel();
            SetPromptVisible(false);
            if (overlay == null) return;
            languageButton.clicked -= ToggleLanguage;
            closeButton.clicked -= CloseFromButton;
            advanceButton.clicked -= AdvanceLinearNode;
            overlay.UnregisterCallback<ClickEvent>(OnOverlayClick, TrickleDown.TrickleDown);
            overlay.UnregisterCallback<NavigationSubmitEvent>(OnOverlaySubmit);
            if (shade != null) card.UnregisterCallback<GeometryChangedEvent>(ResizeShade);
            overlay = null;
        }

        internal void Register(DialogueTrigger trigger) => triggers.Add(trigger);
        internal void Unregister(DialogueTrigger trigger) => triggers.Remove(trigger);
        internal Func<string> SceneInteractionPrompt { get; set; }
        internal Action<int> RuntimeChoiceSelected { get; set; }

        void LateUpdate()
        {
            if (overlay == null || suspended || showingChapterTitle || closedFrame == Time.frameCount)
            {
                SetPromptVisible(false);
                return;
            }
            var keyboard = Keyboard.current;
            if (IsPlaying)
            {
                SetPromptVisible(false);
                if (interactKey != Key.None && keyboard?[interactKey].wasPressedThisFrame == true)
                {
                    if (lineReady && HasChoices)
                    {
                        int focused = actions.focusController?.focusedElement is VisualElement element
                            ? actions.contentContainer.IndexOf(element) : -1;
                        SelectChoice(currentNode.choices[Mathf.Max(0, focused)]);
                    }
                    else AdvanceLinearNode();
                }
                return;
            }

            if (SceneInteractionPrompt != null)
            {
                string action = SceneInteractionPrompt();
                bool visible = !string.IsNullOrEmpty(action);
                if (visible)
                {
                    prompt.text = action;
                    ApplyFont(prompt, DialogueLanguage.TraditionalChinese);
                }
                SetPromptVisible(visible);
                return;
            }

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

            if (nearby != null) prompt.text = Local($"按 {InteractionKeyLabel} 與角色交談", $"{InteractionKeyLabel}　話す");
            SetPromptVisible(nearby != null && interactKey != Key.None);
            if (automatic != null)
                automatic.PlayOnEntry();
            else if (nearby != null && TryConsumeGameplayInteraction())
                nearby.Play();
        }

        internal bool TryConsumeGameplayInteraction()
        {
            if (overlay == null || BlocksGameplayInput || interactionFrame == Time.frameCount || interactKey == Key.None
                || Keyboard.current?[interactKey].wasPressedThisFrame != true)
                return false;
            interactionFrame = Time.frameCount;
            return true;
        }

        void SetPromptVisible(bool visible)
        {
            if (prompt != null)
                prompt.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void ShowOpening(ChapterOpeningText text, Action whenCovered = null)
        {
            if (!isActiveAndEnabled || IsPlaying || text == null || titleScreen == null ||
                openingTitle == null || objectiveScreen == null || objectiveHeading == null || objectiveText == null)
            {
                whenCovered?.Invoke();
                return;
            }

            StopOpening();
            SetText(openingTitle, text.Title);
            objectiveLanguage = SetText(objectiveText, text.Objective);
            if (string.IsNullOrWhiteSpace(openingTitle.text))
            {
                whenCovered?.Invoke();
                whenCovered = null;
                if (string.IsNullOrWhiteSpace(objectiveText.text)) return;
            }
            titleScreen.EnableInClassList("chapter-title-transition", whenCovered != null);
            objectiveHeading.text = Local("目前目標", "現在の目標");
            ApplyFont(objectiveHeading, language);
            openingSequence = StartCoroutine(ShowOpeningSequence(whenCovered));
        }

        IEnumerator ShowOpeningSequence(Action whenCovered)
        {
            if (!string.IsNullOrWhiteSpace(openingTitle.text))
            {
                showingChapterTitle = true;
                SetPromptVisible(false);
                yield return ShowTimedMessage(titleScreen, 0.4f, 1.6f, 1f, whenCovered);
                showingChapterTitle = false;
            }

            if (!string.IsNullOrWhiteSpace(objectiveText.text))
            {
                RecordHistory(objectiveHeading.text, objectiveText.text, objectiveLanguage);
                yield return ShowTimedMessage(objectiveScreen, 0.35f, 4f, 0.65f);
            }

            openingSequence = null;
        }

        IEnumerator ShowTimedMessage(VisualElement screen, float fadeIn, float hold, float fadeOut,
            Action whenOpaque = null)
        {
            screen.style.opacity = 0f;
            screen.AddToClassList("chapter-message-visible");
            float duration = fadeIn + hold + fadeOut;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                if (suspended) continue;
                elapsed += Time.unscaledDeltaTime;
                screen.style.opacity = Mathf.SmoothStep(0f, 1f,
                    Mathf.Min(elapsed / fadeIn, (duration - elapsed) / fadeOut));
                if (elapsed >= fadeIn && whenOpaque != null)
                {
                    whenOpaque();
                    whenOpaque = null;
                }
            }
            whenOpaque?.Invoke();
            screen.RemoveFromClassList("chapter-message-visible");
        }

        void StopOpening()
        {
            if (openingSequence != null) StopCoroutine(openingSequence);
            openingSequence = null;
            showingChapterTitle = false;
            titleScreen?.RemoveFromClassList("chapter-message-visible");
            objectiveScreen?.RemoveFromClassList("chapter-message-visible");
        }

        public bool TryPlay(DialogueConversation asset, DialogueAppearance appearance = null, Action completed = null,
            string startNodeId = null)
        {
            if (!isActiveAndEnabled || overlay == null || suspended || IsPlaying || showingChapterTitle || asset == null)
                return false;
            if (!asset.TryBuildIndex(out var index, out var entry, out string error))
            {
                Debug.LogError($"Dialogue '{asset.name}': {error}", asset);
                return false;
            }

            if (!string.IsNullOrEmpty(startNodeId))
            {
                if (index.TryGetValue(startNodeId, out var checkpointNode))
                    entry = checkpointNode;
                else
                    Debug.LogWarning($"Dialogue '{asset.name}' has no node '{startNodeId}'. Starting from the beginning.", asset);
            }

            StopOpening();
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
            scroll.scrollOffset = Vector2.zero;
            overlay.style.display = DisplayStyle.Flex;
            RenderNode();
            LineChanged?.Invoke(asset, entry.id);
            return true;
        }

        public void Cancel()
        {
            if (IsPlaying) Close();
        }

        void CloseFromButton()
        {
            if (suspended || !IsPlaying) return;
            MenuAudio.Play(MenuCue.Back);
            Cancel();
        }

        void ToggleLanguage()
        {
            if (suspended || !IsPlaying) return;
            MenuAudio.Play(MenuCue.Select);
            var next = language == DialogueLanguage.Japanese
                ? DialogueLanguage.TraditionalChinese : DialogueLanguage.Japanese;
            SetLanguage(next);
            PlayerPrefs.SetInt(LanguagePreference, (int)next);
            PlayerPrefs.Save();
            PrepareLine();
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
            prompt.text = $"{InteractionKeyLabel}　{Local("對話", "話す")}";
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
            renderedFrame = Time.frameCount;
            scrolling = page != null;
            lineReady = false;
            page = new VisualElement();
            page.AddToClassList("dialogue-page");
            speaker = new Label { enableRichText = false };
            speaker.AddToClassList("dialogue-speaker");
            line = new Label { enableRichText = false };
            line.AddToClassList("dialogue-line");
            page.Add(speaker);
            page.Add(line);
            scroll.Add(page);
            if (portrait != null)
            {
                portrait.image = currentNode.portrait;
                portrait.EnableInClassList("dialogue-portrait-visible", currentNode.portrait != null);
                portrait.EnableInClassList("dialogue-portrait-right", currentNode.portraitOnRight);
            }
            PrepareLine();
        }

        void PrepareLine()
        {
            if (lineSequence != null) StopCoroutine(lineSequence);
            preparingLine = true;
            speaker.style.visibility = Visibility.Hidden;
            line.style.visibility = Visibility.Hidden;
            SetText(speaker, currentNode.speaker);
            speaker.style.display = string.IsNullOrWhiteSpace(speaker.text) ? DisplayStyle.None : DisplayStyle.Flex;
            fullLine = currentNode.line.Get(language, out lineLanguage);
            ApplyFont(line, lineLanguage);
            textElements = StringInfo.ParseCombiningCharacters(fullLine);
            visibleCharacters = 0;
            line.text = fullLine;
            SetActionsVisible(false);
            BuildActions();
            overlay.Focus();
            lineSequence = StartCoroutine(PlayLine());
        }

        IEnumerator PlayLine()
        {
            // Measure the wrapped line and the choices while they are hidden.
            do { yield return null; }
            while (suspended);
            var (targetLineHeight, targetChoiceHeight) = MeasureDialogue();
            if (!lineReady) line.text = string.Empty;

            if (scrolling)
            {
                float from = scroll.scrollOffset.y;
                float fromLineHeight = ValidHeight(scroll.resolvedStyle.height, targetLineHeight);
                float fromChoiceHeight = ValidHeight(actions.resolvedStyle.height, targetChoiceHeight);
                for (float elapsed = 0f; elapsed < PageScrollSeconds;)
                {
                    if (!suspended)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float progress = Mathf.SmoothStep(0f, 1f, elapsed / PageScrollSeconds);
                        SetDialogueHeights(Mathf.Lerp(fromLineHeight, targetLineHeight, progress),
                            Mathf.Lerp(fromChoiceHeight, targetChoiceHeight, progress));
                        ScrollTo(Mathf.Lerp(from, page.layout.y, progress));
                    }
                    yield return null;
                }
                scrolling = false;
            }
            else
            {
                SetDialogueHeights(targetLineHeight, targetChoiceHeight);
                yield return null;
            }
            while (suspended) yield return null;

            preparingLine = false;
            speaker.style.visibility = Visibility.Visible;
            line.style.visibility = Visibility.Visible;
            FollowCurrentLine();
            if (lineReady)
            {
                lineSequence = null;
                SetActionsVisible(true);
                yield break;
            }

            float remaining = 0f;
            while (suspended || visibleCharacters < textElements.Length)
            {
                if (!suspended)
                {
                    FollowCurrentLine();
                    remaining -= Time.unscaledDeltaTime;
                    if (remaining <= 0f)
                    {
                        do
                        {
                            visibleCharacters++;
                            remaining += 1f / Mathf.Max(1f, charactersPerSecond);
                        }
                        while (remaining <= 0f && visibleCharacters < textElements.Length);
                        line.text = visibleCharacters == textElements.Length
                            ? fullLine : fullLine.Substring(0, textElements[visibleCharacters]);
                    }
                }
                yield return null;
            }
            FollowCurrentLine();
            lineSequence = null;
            FinishLine();
        }

        void SetDialogueHeights(float lineHeight, float choiceHeight)
        {
            scroll.style.height = lineHeight;
            actions.style.height = choiceHeight;
        }

        (float lineHeight, float choicesHeight) MeasureDialogue()
        {
            float lineHeight = Mathf.Clamp(ValidHeight(line.layout.yMax + LineBottomPadding, MaxLineHeight), 32f, MaxLineHeight);
            if (!HasChoices || actions.childCount == 0) return (lineHeight, 0f);
            var last = actions.ElementAt(actions.childCount - 1);
            float fallback = Mathf.Min(actions.childCount * 46f, MaxChoiceHeight);
            float measured = last.layout.height > 0f ? last.layout.yMax + last.resolvedStyle.marginBottom : fallback;
            float choiceHeight = Mathf.Clamp(ValidHeight(measured, fallback), 44f, MaxChoiceHeight);
            return (lineHeight, choiceHeight);
        }

        static float ValidHeight(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? fallback : value;

        void ScrollTo(float offset)
        {
            if (!float.IsNaN(offset) && !float.IsInfinity(offset))
                scroll.scrollOffset = new Vector2(0f, offset);
        }

        void FollowCurrentLine()
        {
            float viewportHeight = scroll.contentViewport.layout.height;
            float overflow = line.layout.yMax + LineBottomPadding - viewportHeight;
            ScrollTo(page.layout.y + Mathf.Max(0f, overflow));
        }

        void FinishLine()
        {
            if (lineSequence != null) StopCoroutine(lineSequence);
            lineSequence = null;
            if (preparingLine || scrolling)
            {
                var (lineHeight, choicesHeight) = MeasureDialogue();
                SetDialogueHeights(lineHeight, choicesHeight);
                preparingLine = false;
                scrolling = false;
            }
            lineReady = true;
            renderedFrame = Time.frameCount;
            speaker.style.visibility = Visibility.Visible;
            line.style.visibility = Visibility.Visible;
            if (line.text != fullLine)
            {
                line.text = fullLine;
                var currentPage = page;
                currentPage.schedule.Execute(() =>
                {
                    if (page == currentPage && !suspended) FollowCurrentLine();
                });
            }
            RecordHistory(speaker.text, line.text, lineLanguage);
            SetActionsVisible(true);
        }

        void BuildActions()
        {
            actions.Clear();
            actions.scrollOffset = Vector2.zero;
            if (HasChoices)
            {
                foreach (var choice in currentNode.choices)
                {
                    var button = new Button(() => SelectChoice(choice)) { enableRichText = false };
                    button.AddToClassList("dialogue-action");
                    SetText(button, choice.text);
                    actions.Add(button);
                }
            }
            else
            {
                string nextId = currentNode.nextNodeId;
                string label = string.IsNullOrEmpty(nextId)
                    ? Local("結束", "会話を終える") : Local("繼續", "続ける");
                advanceButton.text = interactKey == Key.None ? label : $"{label}　[{interactKey}]";
                ApplyFont(advanceButton, language);
            }
        }

        void SetActionsVisible(bool ready)
        {
            bool showChoices = ready && HasChoices;
            bool showAdvance = ready && !HasChoices;
            actions.EnableInClassList("dialogue-actions-hidden", !showChoices);
            actions.SetEnabled(showChoices);
            advanceButton.EnableInClassList("dialogue-actions-hidden", !showAdvance);
            advanceButton.SetEnabled(showAdvance);
            if (showChoices) actions.ElementAt(0).Focus();
        }

        void ResizeShade(GeometryChangedEvent change)
        {
            shade.style.height = change.newRect.height + 104f;
        }

        void OnOverlayClick(ClickEvent click)
        {
            if (click.button == 0 && click.target is VisualElement target
                && target is not Button && target.GetFirstAncestorOfType<Button>() == null)
                AdvanceLinearNode();
        }

        void OnOverlaySubmit(NavigationSubmitEvent submit)
        {
            if (submit.target == overlay) AdvanceLinearNode();
        }

        void AdvanceLinearNode()
        {
            if (!IsPlaying || suspended || renderedFrame == Time.frameCount || closedFrame == Time.frameCount) return;
            if (lineReady && HasChoices) return;
            MenuAudio.Play(MenuCue.Select);
            if (!lineReady) FinishLine();
            else Advance(currentNode.nextNodeId);
        }

        bool CanInteract() => IsPlaying && !suspended && !preparingLine
            && renderedFrame != Time.frameCount && closedFrame != Time.frameCount;

        void SelectChoice(DialogueConversation.Choice choice)
        {
            if (!CanInteract() || !lineReady) return;
            MenuAudio.Play(MenuCue.Confirm);
            string text = choice.text.Get(language, out var textLanguage);
            RecordHistory(Local("已選選項", "選んだ返答"), text, textLanguage);
            RuntimeChoiceSelected?.Invoke(Array.IndexOf(currentNode.choices, choice));
            Advance(choice.nextNodeId);
        }

        DialogueLanguage SetText(TextElement element, DialogueText text)
        {
            element.text = text.Get(language, out var resolvedLanguage);
            ApplyFont(element, resolvedLanguage);
            return resolvedLanguage;
        }

        internal void ApplyFont(TextElement element, DialogueLanguage value)
        {
            Font font = value == DialogueLanguage.Japanese ? japaneseFont : traditionalChineseFont;
            element.style.unityFontDefinition = font != null
                ? new StyleFontDefinition(FontDefinition.FromFont(font)) : new StyleFontDefinition(StyleKeyword.Null);
        }

        void Advance(string nextId)
        {
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
            if (lineSequence != null) StopCoroutine(lineSequence);
            lineSequence = null;
            if (!lineReady && !preparingLine) RecordHistory(speaker.text, line.text, lineLanguage);
            scroll.Clear();
            page = null;
            conversation = null;
            currentNode = null;
            nodes = null;
            onCompleted = null;
            RuntimeChoiceSelected = null;
            actions.Clear();
            if (portrait != null) portrait.image = null;
            overlay.style.display = DisplayStyle.None;
            ApplyAppearance(null);
            closedFrame = Time.frameCount;
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursorVisibility;
        }

        internal string Local(string chinese, string japanese) => language == DialogueLanguage.Japanese ? japanese : chinese;
    }
}
