using System;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Puzzle.Story
{
    internal static class StoryMenu
    {
        internal static VisualElement Screen(UIDocument document, Font font)
        {
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var screen = root.Q("story-screen");
            screen.style.unityFontDefinition = FontDefinition.FromFont(font);
            return screen;
        }

        internal static VisualElement Panel(VisualElement parent, string className, bool interactive = true)
        {
            var element = new VisualElement { pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore };
            element.AddToClassList(className);
            parent.Add(element);
            return element;
        }

        internal static Label Label(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        internal static Button Button(VisualElement parent, string text, Action click, string className = null)
        {
            var button = new Button(click) { text = text, enableRichText = false };
            button.AddToClassList("story-button");
            if (className != null) button.AddToClassList(className);
            parent.Add(button);
            return button;
        }

        internal static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        internal static void SetCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
