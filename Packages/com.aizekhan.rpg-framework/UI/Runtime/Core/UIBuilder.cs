using UnityEngine;
using UnityEngine.UI;

namespace RPGFramework.UI
{
    /// <summary>
    /// Builds plain uGUI pieces in code (boxes, labels, buttons, lists) for panels that need no prefab. Text uses
    /// Unity's built-in font, so a new project shows it without importing anything.
    /// </summary>
    public static class UIBuilder
    {
        private static Font _font;

        public static Font DefaultFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>
        /// A rectangle anchored at <paramref name="anchor"/> (also its pivot); filled when the colour is not clear.
        /// </summary>
        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color color = default)
        {
            var box = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)box.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (color.a > 0f)
                box.AddComponent<Image>().color = color;
            return rect;
        }

        /// <summary>
        /// Stretches the rectangle over its parent, inset by <paramref name="padding"/>.
        /// </summary>
        public static RectTransform Fill(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        /// <summary>
        /// Text that fills its parent.
        /// </summary>
        public static Text Label(Transform parent, string name, string text, int size, TextAnchor alignment,
            Color? color = null)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(parent, false);
            Fill(label.rectTransform);
            label.font = DefaultFont;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.text = text ?? "";
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>
        /// A box that lays its children out top to bottom (or left to right) and grows with them.
        /// </summary>
        public static RectTransform List(Transform parent, string name, bool vertical, float spacing, int padding = 0)
        {
            var list = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)list.transform;
            rect.SetParent(parent, false);
            HorizontalOrVerticalLayoutGroup layout = vertical
                ? list.AddComponent<VerticalLayoutGroup>()
                : (HorizontalOrVerticalLayoutGroup)list.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = vertical;
            layout.childForceExpandHeight = !vertical;
            var fitter = list.AddComponent<ContentSizeFitter>();
            if (vertical)
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            else
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        /// <summary>
        /// Gives a child of a list its height (or width) in the layout.
        /// </summary>
        public static T Sized<T>(T component, float width, float height) where T : Component
        {
            var element = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
                element.preferredWidth = element.minWidth = width;
            if (height > 0f)
                element.preferredHeight = element.minHeight = height;
            return component;
        }

        /// <summary>
        /// Removes a piece built here: at once in the editor, at the end of the frame while playing.
        /// </summary>
        public static void Discard(GameObject piece)
        {
            if (piece == null)
                return;
            if (Application.isPlaying)
            {
                piece.transform.SetParent(null, false);
                Object.Destroy(piece);
            }
            else
            {
                Object.DestroyImmediate(piece);
            }
        }

        /// <summary>
        /// A filled button with a caption.
        /// </summary>
        public static UnityEngine.UI.Button Button(Transform parent, string name, string caption, int size, Color color)
        {
            var rect = Box(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 48f), color);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Label(rect, "Caption", caption, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
