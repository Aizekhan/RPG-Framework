using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RPGFramework.UI;
using RPGFramework.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RPGFramework.TurnBased.Unity
{
    /// <summary>
    /// A battle screen built from code on the framework's panels: the enemies and the party with their health and
    /// mana, the battle log, and the menu of the party member whose turn it is (<see cref="BattleMenu"/>: a command,
    /// then its target). Between battles it offers the next encounter, or a new run when the run is over.
    /// </summary>
    public class BattleScreen : MonoBehaviour
    {
        [SerializeField, Min(8)] private int _fontSize = 24;
        [SerializeField, Min(1)] private int _logLines = 6;

        private TurnBasedWorld _world;
        private Func<BattleAction, ActionResult> _act;
        private Action _startBattle;
        private Action _newRun;
        private Func<string> _saveProblem;
        private StatListPanel _enemies;
        private StatListPanel _party;
        private ActionListPanel _menu;
        private Text _log;
        private string _shownFor;
        private BattleCommand _aiming;
        private readonly StringBuilder _text = new StringBuilder();

        /// <summary>
        /// The menu's rows as they are now (for tests and a game's own checks).
        /// </summary>
        public ActionListPanel Menu => _menu;

        /// <summary>
        /// Builds the screen on its own canvas under <paramref name="parent"/>.
        /// </summary>
        public static BattleScreen Create(Transform parent, TurnBasedHost host)
        {
            var canvasObject = new GameObject("Battle", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(parent, false);
            }
            var screen = canvasObject.AddComponent<BattleScreen>();
            screen.Bind(host.World, host.Act, () => host.StartBattle(), host.NewRun, () => host.SaveProblem);
            return screen;
        }

        /// <summary>
        /// Shows a world; without the host's callbacks the screen acts on the world itself.
        /// </summary>
        public void Bind(TurnBasedWorld world, Func<BattleAction, ActionResult> act = null, Action startBattle = null,
            Action newRun = null, Func<string> saveProblem = null)
        {
            _world = world;
            _act = act ?? (action => _world.Battle?.Perform(action));
            _startBattle = startBattle ?? (() => _world.StartBattle());
            _newRun = newRun ?? (() => _world.Restart());
            _saveProblem = saveProblem ?? (() => "");
            Build();
            _shownFor = null;
            Refresh();
        }

        private void Build()
        {
            if (_menu != null)
                return;
            _enemies = Panel<StatListPanel>("Enemies");
            _enemies.FontSize = _fontSize;
            _enemies.Width = 520f;
            _enemies.Anchor = new Vector2(1f, 1f);
            _enemies.Offset = new Vector2(-40f, -40f);
            _enemies.Title = "Enemies";

            _party = Panel<StatListPanel>("Party");
            _party.FontSize = _fontSize;
            _party.Width = 520f;
            _party.Anchor = new Vector2(0f, 0f);
            _party.Offset = new Vector2(40f, 40f);
            _party.Title = "Party";

            _menu = Panel<ActionListPanel>("Menu");
            _menu.FontSize = _fontSize;
            _menu.IconSize = 0f;
            _menu.Width = 560f;
            _menu.MaxHeight = 520f;
            _menu.Anchor = new Vector2(1f, 0f);
            _menu.Offset = new Vector2(-40f, 40f);

            _log = UIBuilder.Label(transform, "Log", "", _fontSize, TextAnchor.UpperLeft);
            var rect = (RectTransform)_log.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -40f);
            rect.sizeDelta = new Vector2(1000f, _fontSize * 1.4f * _logLines);
        }

        private T Panel<T>(string name) where T : Component
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            UIBuilder.Fill((RectTransform)holder.transform);
            return holder.AddComponent<T>();
        }

        protected virtual void Update() => Refresh();

        /// <summary>
        /// Reads the world again: a new turn, battle or run rebuilds the menu.
        /// </summary>
        public void Refresh()
        {
            var world = _world;
            if (world == null || _menu == null)
                return;
            var battle = world.Battle;
            ShowLog(battle != null ? battle.Log : world.Log);

            string state = StateKey(world, battle);
            if (state == _shownFor)
                return;
            _shownFor = state;
            if (battle == null || battle.IsOver || battle.Current == null || !battle.Current.IsParty)
                _aiming = null;

            _enemies.gameObject.SetActive(battle != null && !battle.IsOver);
            if (battle != null)
                _enemies.Bind(battle.Enemies.Select(e => (e.Name, (Func<string>)(() => Status(e)))));
            var members = battle != null && !battle.IsOver ? battle.Party : world.Party;
            _party.Bind(members.Select(m => (m.Name, (Func<string>)(() => Status(m)))));
            _menu.Bind(Rows(world, battle));
        }

        // What decides the menu: whose turn, which command is being aimed, the encounter, the run's end
        private string StateKey(TurnBasedWorld world, Battle battle)
        {
            if (battle == null || battle.IsOver)
                return "between:" + world.EncounterIndex + ":" + world.IsRunOver + ":" + world.IsRunComplete + ":" + (battle?.Outcome);
            return "turn:" + battle.EncounterIndex + ":" + battle.Round + ":" + (battle.Current?.Id ?? -1) + ":" +
                   (battle.Current?.TurnsTaken ?? 0) + ":" + (_aiming?.Label ?? "");
        }

        private IEnumerable<ActionRow> Rows(TurnBasedWorld world, Battle battle)
        {
            if (battle == null || battle.IsOver)
            {
                if (world.IsRunOver || world.IsRunComplete)
                {
                    yield return new ActionRow(() => world.IsRunComplete ? "Every encounter is won!" : "The party has fallen.");
                    yield return new ActionRow(() => "", null, new ActionButton("New run", () => _newRun()));
                    yield break;
                }
                var next = world.NextEncounter;
                yield return new ActionRow(() => next != null ? "Next: " + next.DisplayName : "",
                    null, new ActionButton("Fight", () => _startBattle(), () => world.WhyCannotStartBattle() == null));
                string problem = _saveProblem();
                if (!string.IsNullOrEmpty(problem))
                    yield return new ActionRow(() => problem);
                yield break;
            }

            var current = battle.Current;
            if (current == null || !current.IsParty)
            {
                yield return new ActionRow(() => current != null ? current.Name + " acts..." : "");
                yield break;
            }

            if (_aiming != null)
            {
                var command = _aiming;
                yield return new ActionRow(() => current.Name + ": " + command.Label + " on whom?");
                foreach (var target in command.Targets)
                {
                    var aimed = target;
                    yield return new ActionRow(() => aimed.Name + "  " + Status(aimed), null,
                        new ActionButton("Choose", () => Take(command.With(aimed))));
                }
                yield return new ActionRow(() => "", null, new ActionButton("Back", () => Aim(null)));
                yield break;
            }

            yield return new ActionRow(() => current.Name + "'s turn");
            foreach (var command in BattleMenu.For(battle, world.Inventory.GetAllItems()))
            {
                var chosen = command;
                yield return new ActionRow(() => chosen.IsAvailable ? chosen.Label : chosen.Label + "  (" + chosen.Refusal + ")", null,
                    new ActionButton(chosen.NeedsTarget ? "Aim" : "Go", () =>
                    {
                        if (chosen.NeedsTarget)
                            Aim(chosen);
                        else
                            Take(chosen.Action);
                    }, () => chosen.IsAvailable));
            }
        }

        private void Aim(BattleCommand command)
        {
            _aiming = command;
            _shownFor = null;
        }

        private void Take(BattleAction action)
        {
            _aiming = null;
            _shownFor = null;
            _act(action);
        }

        private void ShowLog(IReadOnlyList<string> lines)
        {
            _text.Clear();
            if (lines != null)
            {
                for (int i = Math.Max(0, lines.Count - _logLines); i < lines.Count; i++)
                    _text.AppendLine(lines[i]);
            }
            string text = _text.ToString();
            if (_log.text != text)
                _log.text = text;
        }

        private static string Status(Combatant combatant)
        {
            if (!combatant.IsAlive)
                return "fallen";
            string text = combatant.Health + "/" + combatant.MaxHealth + " HP";
            if (combatant.MaxMana > 0)
                text += "  " + combatant.Mana + "/" + combatant.MaxMana + " MP";
            if (combatant.Controls.Count > 0)
                text += "  " + string.Join(", ", combatant.Controls);
            return text;
        }
    }
}
