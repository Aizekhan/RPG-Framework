using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ReferenceGame
{
    /// <summary>
    /// Screen HUD of Goblin Valley: hero health and level, quest and wave, the event log and the controls.
    /// Builds its own canvas so the scene needs no UI setup; colours and font size are tunable in the inspector.
    /// </summary>
    public sealed class GoblinValleyHud : MonoBehaviour
    {
        [SerializeField] private int _fontSize = 26;
        [SerializeField] private Color _textColor = new Color(1f, 0.97f, 0.9f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _healthColor = new Color(0.85f, 0.2f, 0.2f);
        [SerializeField] private bool _showLog = true;

        private readonly StringBuilder _text = new StringBuilder();
        private GoblinValleyGame _game;
        private Font _font;
        private RectTransform _healthFill;
        private Text _healthLabel;
        private Text _status;
        private Text _log;
        private Text _banner;

        public void Bind(GoblinValleyGame game)
        {
            _game = game;
            if (_status == null)
                Build();
        }

        private void LateUpdate()
        {
            var hero = _game?.Player;
            if (hero == null)
                return;

            float fraction = hero.MaxHealth > 0f ? Mathf.Clamp01(hero.Health / hero.MaxHealth) : 0f;
            _healthFill.anchorMax = new Vector2(fraction, 1f);
            _healthLabel.text = $"{hero.Health:F0} / {hero.MaxHealth:F0}";

            _text.Clear()
                .Append("Level ").Append(_game.Level).Append("   ").Append(_game.TotalXp).Append(" XP\n")
                .Append("Attack ").Append(hero.Attack.ToString("F0")).Append("   Defense ").Append(hero.Defense.ToString("F0")).Append('\n')
                .Append("Wave ").Append(_game.CurrentWave).Append("   Goblins slain ").Append(_game.GoblinsKilled).Append('\n')
                .Append("Backpack ").Append(_game.Inventory.Count).Append(" items\n")
                .Append(_game.QuestCompleted ? "Clear the Valley: complete" : "Quest: Clear the Valley");
            _status.text = _text.ToString();

            if (_showLog)
            {
                _text.Clear();
                foreach (var line in _game.Log)
                    _text.AppendLine(line);
                _log.text = _text.ToString();
            }

            _banner.enabled = !hero.IsAlive();
        }

        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasObject.transform;

            // Top left: health bar and stats
            var panel = Box(root, "Status", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(460f, 250f), _panelColor);
            var bar = Box(panel, "Health", new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(428f, 34f),
                new Color(0.15f, 0.15f, 0.15f, 0.9f));
            _healthFill = Box(bar, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, _healthColor);
            _healthFill.anchorMin = Vector2.zero;
            _healthFill.anchorMax = Vector2.one;
            _healthFill.offsetMin = _healthFill.offsetMax = Vector2.zero;
            _healthLabel = Label(bar, "Value", TextAnchor.MiddleCenter, _fontSize - 4);
            _status = Label(Box(panel, "Text", new Vector2(0f, 1f), new Vector2(16f, -60f), new Vector2(428f, 180f), Color.clear),
                "Stats", TextAnchor.UpperLeft, _fontSize);

            // Bottom left: what happened
            _log = Label(Box(root, "Log", Vector2.zero, new Vector2(24f, 24f), new Vector2(620f, 380f), Color.clear),
                "Lines", TextAnchor.LowerLeft, _fontSize - 4);
            _log.enabled = _showLog;

            // Bottom right: controls
            var help = Label(Box(root, "Controls", new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(560f, 110f), Color.clear),
                "Lines", TextAnchor.LowerRight, _fontSize - 6);
            help.text = "Move  WASD / arrows / left stick\nAttack  Space / left click / A\nRespawn  R / Start";

            _banner = Label(Box(root, "Banner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f), Color.clear),
                "Text", TextAnchor.MiddleCenter, _fontSize * 2);
            _banner.text = "You fell.  Press R to rise again";
            _banner.enabled = false;
        }

        private static RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color color)
        {
            var box = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)box.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (color.a > 0f)
            {
                var image = box.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
            return rect;
        }

        private Text Label(RectTransform parent, string name, TextAnchor alignment, int size)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            var rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
            label.font = _font;
            label.fontSize = size;
            label.color = _textColor;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return label;
        }
    }
}
