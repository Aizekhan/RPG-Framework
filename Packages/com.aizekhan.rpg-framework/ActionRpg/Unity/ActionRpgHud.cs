using System.Linq;
using System.Text;
using RPGFramework.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Screen HUD of an action RPG: player health and level, quests with progress, wave, the event log and the
    /// controls; I / Tab / gamepad Select opens the character and backpack panel. Builds its own canvas so the scene needs no UI setup; colours and font size are tunable in the
    /// inspector. Turn it off on the host to use your own UI.
    /// </summary>
    public sealed class ActionRpgHud : MonoBehaviour
    {
        [SerializeField] private int _fontSize = 26;
        [SerializeField] private Color _textColor = new Color(1f, 0.97f, 0.9f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _healthColor = new Color(0.85f, 0.2f, 0.2f);
        [SerializeField] private bool _showLog = true;

        private readonly StringBuilder _text = new StringBuilder();
        private ActorWorld _world;
        private Font _font;
        private RectTransform _healthFill;
        private Text _healthLabel;
        private Text _status;
        private Text _log;
        private Text _quests;
        private Text _banner;
        private RectTransform _sheet;
        private Text _character;
        private Text _backpack;
        private InputAction _toggleSheet;

        /// <summary>
        /// The character and backpack panel is open.
        /// </summary>
        public bool IsSheetOpen => _sheet != null && _sheet.gameObject.activeSelf;

        public void Bind(ActorWorld world)
        {
            _world = world;
            if (_status == null)
                Build();
        }

        public void ToggleSheet()
        {
            if (_sheet != null)
                _sheet.gameObject.SetActive(!_sheet.gameObject.activeSelf);
        }

        private void Awake()
        {
            _toggleSheet = new InputAction("Character Sheet", InputActionType.Button);
            _toggleSheet.AddBinding("<Keyboard>/i");
            _toggleSheet.AddBinding("<Keyboard>/tab");
            _toggleSheet.AddBinding("<Gamepad>/select");
        }

        private void OnEnable() => _toggleSheet.Enable();

        private void OnDisable() => _toggleSheet.Disable();

        private void OnDestroy() => _toggleSheet.Dispose();

        private void Update()
        {
            if (_toggleSheet.WasPressedThisFrame())
                ToggleSheet();
        }

        private void LateUpdate()
        {
            var hero = _world?.Player;
            if (hero == null)
                return;

            _healthFill.anchorMax = new Vector2(hero.HealthFraction, 1f);
            _healthLabel.text = $"{hero.Health:F0} / {hero.MaxHealth:F0}";

            _text.Clear()
                .Append(hero.DisplayName).Append("   Level ").Append(_world.Level).Append("   ").Append(_world.TotalExperience).Append(" XP\n")
                .Append("Attack ").Append(hero.Attack.ToString("F0")).Append("   Defense ").Append(hero.Defense.ToString("F0")).Append('\n')
                .Append("Wave ").Append(_world.CurrentWave).Append("   Kills ").Append(_world.Kills).Append('\n')
                .Append("Backpack ").Append(_world.Inventory.Count).Append(" items");
            _status.text = _text.ToString();

            _text.Clear();
            foreach (var (quest, progress) in _world.ActiveQuests)
            {
                _text.Append("<b>").Append(quest.Title).Append("</b>\n");
                foreach (var objective in quest.Objectives)
                {
                    progress.ObjectiveProgress.TryGetValue(objective.Id, out int done);
                    _text.Append("  ").Append(objective.Description).Append("  ")
                        .Append(Mathf.Min(done, objective.RequiredCount)).Append('/').Append(objective.RequiredCount).Append('\n');
                }
            }
            if (_world.CompletedQuests.Count > 0)
                _text.Append("Completed: ").Append(_world.CompletedQuests.Count);
            _quests.text = _text.ToString();

            if (_showLog)
            {
                _text.Clear();
                foreach (var line in _world.Log)
                    _text.AppendLine(line);
                _log.text = _text.ToString();
            }

            _banner.enabled = !hero.IsAlive();

            if (IsSheetOpen)
                FillSheet(hero);
        }

        private void FillSheet(Actor hero)
        {
            _text.Clear()
                .Append("<b>").Append(hero.DisplayName).Append("</b>\n\n")
                .Append("Level ").Append(_world.Level).Append('\n')
                .Append("Experience ").Append(_world.TotalExperience).Append(" / ").Append(_world.ExperienceForNextLevel).Append('\n')
                .Append("Health ").Append(hero.Health.ToString("F0")).Append(" / ").Append(hero.MaxHealth.ToString("F0")).Append('\n')
                .Append("Attack ").Append(hero.Attack.ToString("F0")).Append('\n')
                .Append("Defense ").Append(hero.Defense.ToString("F0")).Append("\n\n")
                .Append("Kills ").Append(_world.Kills).Append("   Wave ").Append(_world.CurrentWave).Append('\n')
                .Append("Quests done ").Append(_world.CompletedQuests.Count);
            _character.text = _text.ToString();

            var inventory = _world.Inventory;
            _text.Clear().Append("<b>Backpack</b>  ").Append(inventory.Count).Append(" / ").Append(inventory.Capacity).Append("\n\n");
            var stacks = inventory.GetAllItems().Where(i => i != null).GroupBy(i => i.ItemId)
                .Select(g => (Item: g.First(), Count: g.Count()))
                .OrderByDescending(stack => stack.Item.Rarity).ThenBy(stack => stack.Item.Name);
            bool any = false;
            foreach (var (item, count) in stacks)
            {
                any = true;
                _text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(RarityColor(item.Rarity))).Append('>')
                    .Append(item.Name).Append("</color>");
                if (count > 1)
                    _text.Append("  x").Append(count);
                _text.Append('\n');
            }
            if (!any)
                _text.Append("Empty. Slay enemies for loot.");
            _backpack.text = _text.ToString();
        }

        private static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.4f, 0.9f, 0.4f);
                case ItemRarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.75f, 0.45f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.65f, 0.2f);
                case ItemRarity.Mythic: return new Color(1f, 0.3f, 0.3f);
                default: return new Color(0.92f, 0.9f, 0.85f);
            }
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
            var panel = Box(root, "Status", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(460f, 220f), _panelColor);
            var bar = Box(panel, "Health", new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(428f, 34f),
                new Color(0.15f, 0.15f, 0.15f, 0.9f));
            _healthFill = Box(bar, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, _healthColor);
            _healthFill.anchorMin = Vector2.zero;
            _healthFill.anchorMax = Vector2.one;
            _healthFill.offsetMin = _healthFill.offsetMax = Vector2.zero;
            _healthLabel = Label(bar, "Value", TextAnchor.MiddleCenter, _fontSize - 4);
            _status = Label(Box(panel, "Text", new Vector2(0f, 1f), new Vector2(16f, -60f), new Vector2(428f, 150f), Color.clear),
                "Stats", TextAnchor.UpperLeft, _fontSize);

            // Top right: quests
            _quests = Label(Box(root, "Quests", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(560f, 400f), Color.clear),
                "Lines", TextAnchor.UpperRight, _fontSize - 2);
            _quests.supportRichText = true;

            // Bottom left: what happened
            _log = Label(Box(root, "Log", Vector2.zero, new Vector2(24f, 24f), new Vector2(620f, 380f), Color.clear),
                "Lines", TextAnchor.LowerLeft, _fontSize - 4);
            _log.enabled = _showLog;

            // Bottom right: controls
            var help = Label(Box(root, "Controls", new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(560f, 140f), Color.clear),
                "Lines", TextAnchor.LowerRight, _fontSize - 6);
            help.text = "Move  WASD / arrows / left stick\nAttack  Space / left click / A\nCharacter  I / Tab / Select\nRespawn  R / Start";

            // Centre: character sheet and backpack (hidden until toggled)
            _sheet = Box(root, "Character Sheet", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f),
                new Color(0.05f, 0.05f, 0.07f, 0.92f));
            _character = Label(Box(_sheet, "Character", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(440f, 500f), Color.clear),
                "Text", TextAnchor.UpperLeft, _fontSize);
            _backpack = Label(Box(_sheet, "Backpack", new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(480f, 500f), Color.clear),
                "Text", TextAnchor.UpperLeft, _fontSize - 2);
            _character.supportRichText = _backpack.supportRichText = true;
            _sheet.gameObject.SetActive(false);

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
