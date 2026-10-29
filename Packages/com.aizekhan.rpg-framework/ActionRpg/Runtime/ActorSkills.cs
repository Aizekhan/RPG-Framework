using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Skills;
using RPGFramework.Stats;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A skill tree for the players of an <see cref="ActorWorld"/> (<see cref="ActionRpgContent.SkillTree"/>): every
    /// level gives skill points (<see cref="ActionRpgSettings.SkillPointsPerLevel"/>), each point learns a skill or
    /// raises it a level (its prerequisites and the player's level allow it), and a skill's bonuses go on the
    /// player as stat modifiers by <see cref="ActorStat"/> name, or as resistances ("resist:fire"). Each hero has a
    /// tree of their own, also when the party shares its level. <see cref="Reset"/> gives every point back.
    /// </summary>
    public sealed class ActorSkills
    {
        private readonly ActorWorld _world;
        private readonly RPGFramework.Events.IEventPublisher _events;
        private readonly Dictionary<int, int> _bonusPoints = new Dictionary<int, int>();
        private readonly Dictionary<int, List<string>> _applied = new Dictionary<int, List<string>>();
        private SkillManager _manager;
        private ISkillTree _managedTree;

        internal ActorSkills(ActorWorld world, RPGFramework.Events.IEventPublisher events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
        }

        public ISkillTree Tree => _world.Content?.SkillTree;

        public bool HasTree => Tree != null;

        // The tree comes with the content, which the module sets after the world is made
        private SkillManager Manager
        {
            get
            {
                var tree = Tree;
                if (tree == null)
                    return null;
                if (_manager == null || !ReferenceEquals(tree, _managedTree))
                {
                    _manager = new SkillManager(_world.Entities, tree, _events);
                    _managedTree = tree;
                }
                return _manager;
            }
        }

        public int LevelOf(int playerId, string skillId) => Manager?.GetSkillLevel(playerId, skillId) ?? 0;

        /// <summary>
        /// The skills a player has learned, with their levels.
        /// </summary>
        public IReadOnlyList<(ISkill Skill, int Level)> LearnedBy(int playerId)
        {
            var manager = Manager;
            if (manager == null)
                return Array.Empty<(ISkill, int)>();
            return manager.GetLearnedSkills(playerId).Select(id => (Tree.GetSkill(id), manager.GetSkillLevel(playerId, id)))
                .Where(s => s.Item1 != null && s.Item2 > 0).ToList();
        }

        /// <summary>
        /// Points a player can still spend: the starting ones, those of every level above 1 and the ones granted,
        /// less one for every skill level learned.
        /// </summary>
        public int PointsOf(int playerId)
        {
            if (Manager == null || _world.ProgressOwnerOf(playerId) < 0)
                return 0;
            var settings = _world.Settings;
            int earned = Math.Max(0, settings.StartingSkillPoints)
                         + Math.Max(0, _world.LevelOf(playerId) - 1) * Math.Max(0, settings.SkillPointsPerLevel)
                         + BonusPointsOf(playerId);
            return Math.Max(0, earned - Spent(playerId));
        }

        public int BonusPointsOf(int playerId) => _bonusPoints.TryGetValue(playerId, out int points) ? points : 0;

        /// <summary>
        /// Gives a player points beyond their levels (a quest reward, a tome).
        /// </summary>
        public void GrantPoints(int playerId, int points)
        {
            if (points <= 0 || _world.ProgressOwnerOf(playerId) < 0)
                return;
            _bonusPoints[playerId] = BonusPointsOf(playerId) + points;
        }

        /// <summary>
        /// Why the player cannot spend a point on this skill now, or null when they can.
        /// </summary>
        public string WhyNot(int playerId, string skillId)
        {
            var manager = Manager;
            if (manager == null)
                return "There is no skill tree";
            if (_world.ProgressOwnerOf(playerId) < 0)
                return "Not a player in the party";
            var skill = Tree.GetSkill(skillId);
            if (skill == null)
                return "Unknown skill";
            int level = manager.GetSkillLevel(playerId, skillId);
            if (level >= skill.MaxLevel)
                return "Already at its highest level";
            if (PointsOf(playerId) <= 0)
                return "No skill points left";
            if (level == 0 && !manager.CanLearnSkill(playerId, skillId, _world.LevelOf(playerId)))
                return "Its requirements are not met";
            return null;
        }

        /// <summary>
        /// Spends a point: learns the skill, or raises it a level. False (and nothing changes) when
        /// <see cref="WhyNot"/> says why.
        /// </summary>
        public bool Learn(int playerId, string skillId)
        {
            if (WhyNot(playerId, skillId) != null)
                return false;
            var manager = Manager;
            bool done = manager.GetSkillLevel(playerId, skillId) == 0
                ? manager.LearnSkill(playerId, skillId, _world.LevelOf(playerId))
                : manager.IncreaseSkillLevel(playerId, skillId);
            if (done)
                Refresh(playerId);
            return done;
        }

        /// <summary>
        /// Forgets every skill of the player and gives the points back (a respec).
        /// </summary>
        public void Reset(int playerId)
        {
            Manager?.ResetSkills(playerId);
            Refresh(playerId);
        }

        /// <summary>
        /// The player's skills for a save.
        /// </summary>
        public List<SkillRecord> RecordsOf(int playerId) =>
            LearnedBy(playerId).OrderBy(s => s.Skill.Id, StringComparer.Ordinal)
                .Select(s => new SkillRecord { SkillId = s.Skill.Id, Level = s.Level }).ToList();

        /// <summary>
        /// Puts saved skills back (skills no longer in the tree are skipped; levels are held to the skill's highest).
        /// </summary>
        public void Restore(int playerId, IEnumerable<SkillRecord> records, int bonusPoints)
        {
            var manager = Manager;
            _bonusPoints.Remove(playerId);
            if (bonusPoints > 0)
                _bonusPoints[playerId] = bonusPoints;
            if (manager == null)
                return;
            manager.ResetSkills(playerId);
            foreach (var record in records ?? Enumerable.Empty<SkillRecord>())
            {
                var skill = record?.SkillId != null ? Tree.GetSkill(record.SkillId) : null;
                if (skill != null && record.Level > 0)
                    manager.SetSkillLevel(playerId, skill.Id, Math.Min(record.Level, skill.MaxLevel));
            }
            Refresh(playerId);
        }

        internal void Forget(int playerId)
        {
            Manager?.ResetSkills(playerId);
            Refresh(playerId);
            _bonusPoints.Remove(playerId);
        }

        private int Spent(int playerId) => LearnedBy(playerId).Sum(s => s.Level);

        // The bonuses of the skills as learned now go on, the old ones come off
        private void Refresh(int playerId)
        {
            var actor = _world.Get(playerId);
            if (_applied.TryGetValue(playerId, out var ids))
            {
                foreach (var id in ids)
                {
                    actor?.RemoveModifier(id);
                    _world.Resistances.RemoveSource(playerId, id);
                }
                _applied.Remove(playerId);
            }
            if (actor == null)
                return;

            var now = new List<string>();
            foreach (var (skill, level) in LearnedBy(playerId))
            {
                for (int i = 0; i < skill.Bonuses.Count; i++)
                {
                    var bonus = skill.Bonuses[i];
                    if (bonus == null || string.IsNullOrEmpty(bonus.StatId))
                        continue;
                    string id = "skill:" + skill.Id + ":" + i;
                    float value = bonus.CalculateBonus(level);
                    if (bonus.StatId.StartsWith(ActorGear.ResistancePrefix, StringComparison.Ordinal) &&
                        bonus.StatId.Length > ActorGear.ResistancePrefix.Length)
                    {
                        _world.Resistances.Set(playerId, bonus.StatId.Substring(ActorGear.ResistancePrefix.Length), value, id);
                    }
                    else if (Enum.TryParse(bonus.StatId, false, out ActorStat stat) && Enum.IsDefined(typeof(ActorStat), stat))
                    {
                        actor.AddModifier(new StatModifier<ActorStat, float>(id, stat, value, TypeOf(bonus.Type)));
                    }
                    else
                    {
                        continue;
                    }
                    now.Add(id);
                }
            }
            if (now.Count > 0)
                _applied[playerId] = now;
        }

        private static StatModifierType TypeOf(BonusType type)
        {
            switch (type)
            {
                case BonusType.Multiplicative: return StatModifierType.Multiplicative;
                case BonusType.Override: return StatModifierType.Override;
                default: return StatModifierType.Additive;
            }
        }
    }

    /// <summary>
    /// A learned skill in a save.
    /// </summary>
    public sealed class SkillRecord
    {
        public string SkillId { get; set; }
        public int Level { get; set; }
    }
}
