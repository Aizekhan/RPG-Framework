using System.IO;
using RPGFramework.Bootstrap.Unity;
using RPGFramework.Core.Composition;
using RPGFramework.Events;
using UnityEngine;

namespace RPGFramework.TurnBased.Unity
{
    /// <summary>
    /// Runs a turn-based RPG in a scene: the game's party, enemies and encounters come from
    /// <see cref="ConfigureGame"/>; enemies take their turns one by one with a pause between them, the run is saved
    /// after every battle and continued on the next start, and a <see cref="BattleScreen"/> (built from code) lets the
    /// player fight. A game can leave the screen off and drive <see cref="World"/> from its own UI.
    /// </summary>
    public class TurnBasedHost : GameHostBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Seconds between enemy turns (0: they act at once)")]
        private float _enemyTurnSeconds = 0.6f;
        [SerializeField, Tooltip("Save after every battle and continue the saved run on start")]
        private bool _saves = true;
        [SerializeField, Tooltip("A save folder under the player's data folder")]
        private string _saveFolder = "TurnBased";
        [SerializeField, Tooltip("Build the battle screen from code")]
        private bool _battleScreen = true;
        [SerializeField] private int _seed;

        private float _enemyWait;
        private SubscriptionBag _subscriptions;

        public TurnBasedModule Module { get; private set; }
        public TurnBasedWorld World => Module?.World;
        public TurnBasedSaves Saves { get; private set; }
        public BattleScreen Screen { get; private set; }

        /// <summary>
        /// What the last save or load said went wrong; empty when it went well.
        /// </summary>
        public string SaveProblem { get; private set; } = "";

        /// <summary>
        /// The game's party, enemies, encounters and content.
        /// </summary>
        protected virtual void ConfigureGame(TurnBasedSettings settings, TurnBasedContent content)
        {
        }

        protected sealed override void ConfigureModules(IGameCompositionBuilder builder)
        {
            var settings = new TurnBasedSettings();
            var content = new TurnBasedContent();
            if (_seed != 0)
                settings.Seed = _seed;
            ConfigureGame(settings, content);
            // The host paces the enemies' turns so the player sees them
            if (_enemyTurnSeconds > 0f)
                settings.EnemiesActAutomatically = false;
            Module = TurnBasedComposition.Configure(builder, settings, content);
        }

        protected virtual void Start()
        {
            if (World == null)
                return;
            if (_saves)
            {
                Saves = new TurnBasedSaves(World, Path.Combine(Application.persistentDataPath, _saveFolder), Application.version);
                SaveProblem = Saves.Load() ?? "";
                _subscriptions = new SubscriptionBag();
                _subscriptions.Add(Host.Container.Resolve<IEventBus>().Subscribe<BattleEndedEvent>(_ => Save()));
            }
            if (_battleScreen)
                Screen = BattleScreen.Create(transform, this);
        }

        /// <summary>
        /// Starts the next encounter; null (and the reason on <see cref="TurnBasedWorld.WhyCannotStartBattle"/>)
        /// when it cannot start.
        /// </summary>
        public Battle StartBattle()
        {
            if (World == null || World.WhyCannotStartBattle() != null)
                return null;
            _enemyWait = _enemyTurnSeconds;
            return World.StartBattle();
        }

        /// <summary>
        /// The party member whose turn it is takes the action.
        /// </summary>
        public ActionResult Act(BattleAction action)
        {
            var battle = World?.Battle;
            if (battle == null || battle.IsOver)
                return null;
            _enemyWait = _enemyTurnSeconds;
            return battle.Perform(action);
        }

        /// <summary>
        /// A new run: the saved one is erased.
        /// </summary>
        public void NewRun()
        {
            Saves?.Delete();
            World?.Restart();
        }

        public void Save()
        {
            if (Saves == null)
                return;
            SaveProblem = Saves.CanSave ? Saves.Save() ?? "" : "";
        }

        /// <summary>
        /// Whether an enemy is about to act (the screen waits).
        /// </summary>
        public bool EnemyActing
        {
            get
            {
                var battle = World?.Battle;
                return battle != null && !battle.IsOver && battle.Current != null && !battle.Current.IsParty;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!EnemyActing || World.Settings.EnemiesActAutomatically)
                return;
            _enemyWait -= Time.deltaTime;
            if (_enemyWait > 0f)
                return;
            _enemyWait = _enemyTurnSeconds;
            World.Battle.PlayAiTurn();
        }

        protected override void OnDestroy()
        {
            _subscriptions?.Dispose();
            base.OnDestroy();
        }
    }
}
