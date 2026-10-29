using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RPGFramework.Interaction;
using RPGFramework.Items;
using RPGFramework.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Screen HUD of an action RPG: player health and level, quests with progress, wave, the event log and the
    /// controls; I / Tab / gamepad Select opens the character and backpack panel. Builds its own canvas so the scene needs no UI setup; colours and font size are tunable in the
    /// inspector. Turn it off on the host to use your own UI. Its texts are <c>kit.hud.*</c> keys of the host's
    /// localization, in English until a game's tables have them (<see cref="Keys"/>).
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
        private Text _prompt;
        private InteractionPoint _prompted;
        private RectTransform _sheet;
        private Text _character;
        private Text _backpack;
        private RectTransform _backpackBox;
        private readonly List<BackpackRow> _rows = new List<BackpackRow>();
        private Func<string, Sprite> _iconOf;
        private InputAction _toggleSheet;
        private ILocalization _localization;
        private Text _help;

        /// <summary>
        /// The HUD's texts by key, in English, for a game's string tables (or <see cref="LocalizationCsv"/>).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["kit.hud.status"] = "{name}   Level {level}   {xp} XP\nAttack {attack:F0}   Defense {defense:F0}\nWave {wave}   Kills {kills}\nBackpack {items} items",
            ["kit.hud.questsDone"] = "Completed: {count}",
            ["kit.hud.use"] = "E  {action}",
            ["kit.hud.sheet"] = "<b>{name}</b>\n\nLevel {level}\nExperience {xp} / {nextXp}\nHealth {health:F0} / {maxHealth:F0}\nAttack {attack:F0}\nDefense {defense:F0}\n\nKills {kills}   Wave {wave}\nGold {gold}\nQuests done {quests}",
            ["kit.hud.backpack"] = "<b>Backpack</b>  {count} / {capacity}",
            ["kit.hud.backpackEmpty"] = "Empty. Slay enemies for loot.",
            ["kit.hud.moreKinds"] = "+{count} more kinds",
            ["kit.hud.round"] = "Round {round}",
            ["kit.hud.controls"] = "Move  WASD / arrows / left stick\nAttack  Space / left click / A\nCharacter  I / Tab / Select\nUse  E / X\nRespawn  R / Start\nCo-op  F2",
            ["kit.hud.fell"] = "You fell.  Press R to rise again",
            ["kit.hud.waiting"] = "Waiting for an opponent...",
            ["kit.hud.draw"] = "Draw!",
            ["kit.hud.victory"] = "Victory!",
            ["kit.hud.wins"] = "{team} wins",
            ["kit.hud.roundOver"] = "Round {round} is over",
            ["kit.hud.defeated"] = "Defeated.  Back in a moment"
        };

        /// <summary>
        /// The character and backpack panel is open.
        /// </summary>
        public bool IsSheetOpen => _sheet != null && _sheet.gameObject.activeSelf;

        /// <param name="iconOf">The picture of an item by id (e.g. <see cref="GameSetup.IconOf"/>); null or no
        /// picture shows the item's initials.</param>
        /// <param name="localization">The texts' language; null: English.</param>
        public void Bind(IActorWorld world, Func<string, Sprite> iconOf = null, ILocalization localization = null)
        {
            _world = world;
            _iconOf = iconOf;
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
            _localization = localization;
            if (_localization != null)
                _localization.LanguageChanged += OnLanguageChanged;
            if (_status == null)
                Build();
            OnLanguageChanged(null);
        }

        // The texts set once; the others are made every frame
        private void OnLanguageChanged(string language)
        {
            if (_help == null)
                return;
            _help.text = L("kit.hud.controls");
            _prompted = null;
            _banner.text = "";
        }

        private string L(string key, params (string Name, object Value)[] args) =>
            (_localization ?? KitTexts.English).Text(new LocalizedText(key, args), Keys[key]);

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

        private void OnDestroy()
        {
            _toggleSheet.Dispose();
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
        }

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

            _text.Clear().Append(L("kit.hud.status", ("name", hero.DisplayName), ("level", _world.Level), ("xp", _world.TotalExperience),
                ("attack", hero.Attack), ("defense", hero.Defense), ("wave", _world.CurrentWave), ("kills", _world.Kills),
                ("items", _world.Inventory.Count)));
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
                _text.Append(L("kit.hud.questsDone", ("count", _world.CompletedQuests.Count)));
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

            // The place this player would use with E: "E  Open the chest"
            var place = hero.IsAlive() ? _world.NearestInteraction : null;
            _prompt.enabled = place != null;
            if (place != null && place != _prompted)
                _prompt.text = L("kit.hud.use", ("action", place.Prompt));
            _prompted = place;

            if (IsSheetOpen)
                FillSheet(hero);
        }

        private void FillSheet(Actor hero)
        {
            _character.text = L("kit.hud.sheet", ("name", hero.DisplayName), ("level", _world.Level), ("xp", _world.TotalExperience),
                ("nextXp", _world.ExperienceForNextLevel), ("health", hero.Health), ("maxHealth", hero.MaxHealth),
                ("attack", hero.Attack), ("defense", hero.Defense), ("kills", _world.Kills), ("wave", _world.CurrentWave),
                ("gold", _world.CurrencyOf()), ("quests", _world.CompletedQuests.Count));

            var inventory = _world.Inventory;
            var stacks = inventory.GetAllItems().Where(i => i != null).GroupBy(i => i.ItemId)
                .Select(g => (Item: g.First(), Count: g.Count()))
                .OrderByDescending(stack => stack.Item.Rarity).ThenBy(stack => stack.Item.Name)
                .ToList();

            _text.Clear().Append(L("kit.hud.backpack", ("count", inventory.Count), ("capacity", inventory.Capacity))).Append("\n\n");
            if (stacks.Count == 0)
                _text.Append(L("kit.hud.backpackEmpty"));
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
                    row.ShowMore(L("kit.hud.moreKinds", ("count", stacks.Count - shown)));
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

            public void ShowMore(string text)
            {
                _frame.gameObject.SetActive(false);
                _label.text = text;
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
            var help = Label(Box(root, "Controls", new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(560f, 205f), Color.clear),
                "Lines", TextAnchor.LowerRight, _fontSize - 6);
            _help = help;

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
            _banner.enabled = false;

            // Below the centre: the nearest place to use
            _prompt = Label(Box(root, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(900f, 60f), Color.clear),
                "Text", TextAnchor.MiddleCenter, _fontSize);
            _prompt.enabled = false;
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
                _text.Append("   ").Append(L("kit.hud.round", ("round", pvp.Round)));
            if (pvp.SecondsLeft > 0)
                _text.Append("   ").Append(pvp.SecondsLeft / 60).Append(':').Append((pvp.SecondsLeft % 60).ToString("00"));
        }

        // What fills the middle of the screen: the match's state in PvP, a fall otherwise; null for nothing
        private string Banner(Actor hero, PvpStatus pvp)
        {
            if (pvp == null)
                return hero.IsAlive() ? null : L("kit.hud.fell");
            switch (pvp.Phase)
            {
                case PvpPhase.Waiting:
                    return L("kit.hud.waiting");
                case PvpPhase.Finished:
                    return pvp.Winner == null ? L("kit.hud.draw") : pvp.Winner == hero.Team ? L("kit.hud.victory") : L("kit.hud.wins", ("team", pvp.Winner));
                case PvpPhase.RoundOver:
                    return L("kit.hud.roundOver", ("round", pvp.Round));
                default:
                    return hero.IsAlive() ? null : L("kit.hud.defeated");
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
