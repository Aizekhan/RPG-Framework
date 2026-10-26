using UnityEngine;
using RPGFramework.Stats;

namespace RPGFramework.Characters
{
    /// <summary>
    /// Base implementation of ICharacter.
    /// Game layer extends this for Player, NPC, Enemy types.
    /// </summary>
    public abstract class CharacterBase : MonoBehaviour, ICharacter
    {
        [Header("Character Info")]
        [SerializeField] protected string characterName = "Character";
        [SerializeField] protected string characterClass = "Adventurer";
        [SerializeField] protected bool isPlayer = false;

        [Header("Systems")]
        [SerializeField] protected int entityId = -1;

        protected IStats stats;
        protected object inventory;
        protected object progression;
        protected bool isAlive = true;
        protected int level = 1;

        public virtual string Name => characterName;
        public virtual string CharacterClass => characterClass;
        public virtual bool IsPlayer => isPlayer;
        public virtual int EntityId => entityId;
        public virtual bool IsAlive => isAlive;

        public virtual int Level => level;
        public virtual IStats Stats => stats;
        public virtual object Inventory => inventory;
        public virtual object Progression => progression;

        protected virtual void Awake()
        {
            InitializeCharacter();
        }

        /// <summary>
        /// Initialize character systems.
        /// Override in derived classes to setup stats, inventory, etc.
        /// </summary>
        protected virtual void InitializeCharacter()
        {
            // Game layer provides concrete implementations
        }

        /// <summary>
        /// Set character's entity ID from ECS.
        /// </summary>
        public virtual void SetEntityId(int id)
        {
            entityId = id;
        }

        /// <summary>
        /// Mark character as dead.
        /// </summary>
        public virtual void Die()
        {
            isAlive = false;
            OnDeath();
        }

        /// <summary>
        /// Revive character.
        /// </summary>
        public virtual void Revive()
        {
            isAlive = true;
            OnRevive();
        }

        /// <summary>
        /// Called when character dies.
        /// Override for custom death logic.
        /// </summary>
        protected virtual void OnDeath()
        {
            // Game layer implements death behavior
        }

        /// <summary>
        /// Called when character revives.
        /// Override for custom revive logic.
        /// </summary>
        protected virtual void OnRevive()
        {
            // Game layer implements revive behavior
        }
    }
}
