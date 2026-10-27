using System;
using System.Collections.Generic;
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
        private const int BackpackRows = 7;
        private const float RowHeight = 58f;

        [SerializeField] private int _fontSize = 26;
        [SerializeField] private Color _textColor = new Color(1f, 0.97f, 0.9f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _healthColor = new Color(0.85f, 0.2f, 0.2f);
        [SerializeField] private bool _showLog = true;

        private readonly StringBuilder _text = new StringBuilder();
        private IActorWorld _world;
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
        private RectTransform _backpackBox;
        private readonly List<BackpackRow> _rows = new List<BackpackRow>();
        private Func<string, Sprite> _iconOf;
        private InputAction _toggleSheet;

        /// <summary>
        /// The character and backpack panel is open.
        /// </summary>
        public bool IsSheetOpen => _sheet != null && _sheet.gameObject.activeSelf;

        /// <param name="iconOf">The picture of an item by id (e.g. <see cref="GameSetup.IconOf"/>); null or no
        /// picture shows the item's initials.</param>
        public void Bind(IActorWorld world, Func<string, Sprite> iconOf = null)
        {
            _world = world;
            _iconOf = iconOf;
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
            var pvp = _world.PvpStatus;
            if (pvp != null)
                AppendScore(pvp);
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

            string banner = Banner(hero, pvp);
            _banner.enabled = banner != null;
            if (banner != null && _banner.text != banner)
                _banner.text = banner;

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
            var stacks = inventory.GetAllItems().Where(i => i != null).GroupBy(i => i.ItemId)
                .Select(g => (Item: g.First(), Count: g.Count()))
                .OrderByDescending(stack => stack.Item.Rarity).ThenBy(stack => stack.Item.Name)
                .ToList();

            _text.Clear().Append("<b>Backpack</b>  ").Append(inventory.Count).Append(" / ").Append(inventory.Capacity).Append("\n\n");
            if (stacks.Count == 0)
                _text.Append("Empty. Slay enemies for loot.");
            _backpack.text = _text.ToString();

            // The last row says how many kinds did not fit
            bool overflow = stacks.Count > BackpackRows;
            int shown = overflow ? BackpackRows - 1 : stacks.Count;
            for (int i = 0; i < BackpackRows; i++)
            {
                var row = Row(i);
                row.Root.gameObject.SetActive(i < shown || (overflow && i == shown));
                if (i < shown)
                    row.Show(stacks[i].Item, stacks[i].Count, RarityColor(stacks[i].Item.Rarity), _iconOf?.Invoke(stacks[i].Item.ItemId));
                else if (overflow && i == shown)
                    row.ShowMore(stacks.Count - shown);
            }
        }

        private BackpackRow Row(int index)
        {
            while (_rows.Count <= index)
            {
                int i = _rows.Count;
                var root = Box(_backpackBox, $"Slot{i}", new Vector2(0f, 1f), new Vector2(8f, -64f - i * RowHeight),
                    new Vector2(460f, RowHeight - 6f), Color.clear);
                var frame = Box(root, "Frame", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(RowHeight - 6f, RowHeight - 6f),
                    Color.white);
                var plate = Box(frame, "Plate", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RowHeight - 12f, RowHeight - 12f),
                    new Color(0.12f, 0.12f, 0.14f, 1f));
                var icon = Box(plate, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RowHeight - 16f, RowHeight - 16f),
                    Color.white).GetComponent<Image>();
                icon.preserveAspect = true;
                var initials = Label(plate, "Initials", TextAnchor.MiddleCenter, _fontSize - 6);
                var label = Label(Box(root, "Name", new Vector2(0f, 0.5f), new Vector2(RowHeight + 6f, 0f),
                    new Vector2(400f, RowHeight - 6f), Color.clear), "Text", TextAnchor.MiddleLeft, _fontSize - 2);
                label.supportRichText = true;
                _rows.Add(new BackpackRow(root, frame.GetComponent<Image>(), icon, initials, label));
            }
            return _rows[index];
        }

        private sealed class BackpackRow
        {
            public readonly RectTransform Root;
            private readonly Image _frame;
            private readonly Image _icon;
            private readonly Text _initials;
            private readonly Text _label;

            public BackpackRow(RectTransform root, Image frame, Image icon, Text initials, Text label)
            {
                Root = root;
                _frame = frame;
                _icon = icon;
                _initials = initials;
                _label = label;
            }

            public void Show(IItem item, int count, Color rarity, Sprite icon)
            {
                _frame.gameObject.SetActive(true);
                _frame.color = rarity;
                _icon.sprite = icon;
                _icon.enabled = icon != null;
                _initials.enabled = icon == null;
                _initials.text = Initials(item.Name);
                _initials.color = rarity;
                _label.text = count > 1
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(rarity)}>{item.Name}</color>  x{count}"
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(rarity)}>{item.Name}</color>";
            }

            public void ShowMore(int kinds)
            {
                _frame.gameObject.SetActive(false);
                _label.text = $"+{kinds} more kinds";
            }

            private static string Initials(string name)
            {
                var words = (name ?? "?").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return words.Length == 0 ? "?" : string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
            }
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
            var help = Label(Box(root, "Controls", new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(560f, 175f), Color.clear),
                "Lines", TextAnchor.LowerRight, _fontSize - 6);
            help.text = "Move  WASD / arrows / left stick\nAttack  Space / left click / A\nCharacter  I / Tab / Select\nRespawn  R / Start\nCo-op  F2";

            // Centre: character sheet and backpack (hidden until toggled)
            _sheet = Box(root, "Character Sheet", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f),
                new Color(0.05f, 0.05f, 0.07f, 0.92f));
            _character = Label(Box(_sheet, "Character", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(440f, 500f), Color.clear),
                "Text", TextAnchor.UpperLeft, _fontSize);
            _backpackBox = Box(_sheet, "Backpack", new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(480f, 500f), Color.clear);
            _backpack = Label(_backpackBox, "Text", TextAnchor.UpperLeft, _fontSize - 2);
            _character.supportRichText = _backpack.supportRichText = true;
            _sheet.gameObject.SetActive(false);

            _banner = Label(Box(root, "Banner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f), Color.clear),
                "Text", TextAnchor.MiddleCenter, _fontSize * 2);
            _banner.text = "You fell.  Press R to rise again";
            _banner.enabled = false;
        }

        // "Red 3 : 2 Blue   (Duel: rounds)   1:23"
        private void AppendScore(PvpStatus pvp)
        {
            _text.Append('\n');
            for (int i = 0; i < pvp.Sides.Count; i++)
            {
                var side = pvp.Sides[i];
                if (i > 0)
                    _text.Append("  :  ");
                _text.Append("<b>").Append(side.Name).Append(' ')
                    .Append(pvp.Mode == PvpMode.Duel ? side.RoundWins : side.Score).Append("</b>");
            }
            if (pvp.Mode == PvpMode.Duel && pvp.Round > 0)
                _text.Append("   Round ").Append(pvp.Round);
            if (pvp.SecondsLeft > 0)
                _text.Append("   ").Append(pvp.SecondsLeft / 60).Append(':').Append((pvp.SecondsLeft % 60).ToString("00"));
        }

        // What fills the middle of the screen: the match's state in PvP, a fall otherwise; null for nothing
        private static string Banner(Actor hero, PvpStatus pvp)
        {
            if (pvp == null)
                return hero.IsAlive() ? null : "You fell.  Press R to rise again";
            switch (pvp.Phase)
            {
                case PvpPhase.Waiting:
                    return "Waiting for an opponent...";
                case PvpPhase.Finished:
                    return pvp.Winner == null ? "Draw!" : pvp.Winner == hero.Team ? "Victory!" : $"{pvp.Winner} wins";
                case PvpPhase.RoundOver:
                    return $"Round {pvp.Round} is over";
                default:
                    return hero.IsAlive() ? null : "Defeated.  Back in a moment";
            }
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
