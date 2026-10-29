using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Skills;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// A skill tree for the party (<see cref="TurnBasedContent.SkillTree"/>): every member's levels give skill
    /// points (<see cref="TurnBasedSettings.SkillPointsPerLevel"/>), each point learns a skill or raises it a level
    /// (within its prerequisites and the member's level), and a skill's bonuses stay on the member between battles:
    /// <see cref="CombatStat"/> names, or resistances ("resist:fire"). A new run starts the tree over.
    /// </summary>
    public sealed class PartySkills
    {
        private readonly TurnBasedWorld _world;
        private readonly ISkillTree _tree;
        private readonly SkillManager _manager;
        private readonly Dictionary<int, int> _bonusPoints = new Dictionary<int, int>();
        private readonly Dictionary<int, List<string>> _applied = new Dictionary<int, List<string>>();

        internal PartySkills(TurnBasedWorld world, ISkillTree tree, IEntityManager entities, IEventPublisher events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _tree = tree;
            if (tree != null)
                _manager = new SkillManager(entities, tree, events);
        }

        public ISkillTree Tree => _tree;

        public bool HasTree => _tree != null;

        public int LevelOf(int memberId, string skillId) => _manager?.GetSkillLevel(memberId, skillId) ?? 0;

        public IReadOnlyList<(ISkill Skill, int Level)> LearnedBy(int memberId)
        {
            if (_manager == null)
                return Array.Empty<(ISkill, int)>();
            return _manager.GetLearnedSkills(memberId).Select(id => (_tree.GetSkill(id), _manager.GetSkillLevel(memberId, id)))
                .Where(s => s.Item1 != null && s.Item2 > 0).ToList();
        }

        /// <summary>
        /// Points a member can still spend: the starting ones, those of every level above 1 and the ones granted,
        /// less one for every skill level learned.
        /// </summary>
        public int PointsOf(int memberId)
        {
            var member = _world.GetMember(memberId);
            if (_manager == null || member == null)
                return 0;
            var settings = _world.Settings;
            int earned = Math.Max(0, settings.StartingSkillPoints) + Math.Max(0, member.Level - 1) * Math.Max(0, settings.SkillPointsPerLevel)
                         + BonusPointsOf(memberId);
            return Math.Max(0, earned - LearnedBy(memberId).Sum(s => s.Level));
        }

        public int BonusPointsOf(int memberId) => _bonusPoints.TryGetValue(memberId, out int points) ? points : 0;

        /// <summary>
        /// Gives a member points beyond their levels (a quest reward, a tome).
        /// </summary>
        public void GrantPoints(int memberId, int points)
        {
            if (points > 0 && _world.GetMember(memberId) != null)
                _bonusPoints[memberId] = BonusPointsOf(memberId) + points;
        }

        /// <summary>
        /// Why the member cannot spend a point on this skill now, or null when they can.
        /// </summary>
        public string WhyNot(int memberId, string skillId)
        {
            if (_manager == null)
                return "There is no skill tree";
            var member = _world.GetMember(memberId);
            if (member == null)
                return "Not in the party";
            var skill = _tree.GetSkill(skillId);
            if (skill == null)
                return "Unknown skill";
            int level = _manager.GetSkillLevel(memberId, skillId);
            if (level >= skill.MaxLevel)
                return "Already at its highest level";
            if (PointsOf(memberId) <= 0)
                return "No skill points left";
            if (level == 0 && !_manager.CanLearnSkill(memberId, skillId, member.Level))
                return "Its requirements are not met";
            return null;
        }

        /// <summary>
        /// Spends a point: learns the skill or raises it a level; false (nothing changes) when <see cref="WhyNot"/>
        /// says why.
        /// </summary>
        public bool Learn(int memberId, string skillId)
        {
            if (WhyNot(memberId, skillId) != null)
                return false;
            var member = _world.GetMember(memberId);
            bool done = _manager.GetSkillLevel(memberId, skillId) == 0
                ? _manager.LearnSkill(memberId, skillId, member.Level)
                : _manager.IncreaseSkillLevel(memberId, skillId);
            if (done)
                Refresh(memberId);
            return done;
        }

        /// <summary>
        /// Forgets every skill of the member and gives the points back (a respec).
        /// </summary>
        public void Reset(int memberId)
        {
            _manager?.ResetSkills(memberId);
            Refresh(memberId);
        }

        public List<MemberSkillRecord> RecordsOf(int memberId) =>
            LearnedBy(memberId).OrderBy(s => s.Skill.Id, StringComparer.Ordinal)
                .Select(s => new MemberSkillRecord { SkillId = s.Skill.Id, Level = s.Level }).ToList();

        /// <summary>
        /// Puts a member's saved skills back (unknown skills are skipped; levels are held to the skill's highest).
        /// Null records: none.
        /// </summary>
        public void Restore(int memberId, IEnumerable<MemberSkillRecord> records, int bonusPoints)
        {
            _bonusPoints.Remove(memberId);
            if (bonusPoints > 0)
                _bonusPoints[memberId] = bonusPoints;
            if (_manager == null)
                return;
            _manager.ResetSkills(memberId);
            foreach (var record in records ?? Enumerable.Empty<MemberSkillRecord>())
            {
                var skill = record?.SkillId != null ? _tree.GetSkill(record.SkillId) : null;
                if (skill != null && record.Level > 0)
                    _manager.SetSkillLevel(memberId, skill.Id, Math.Min(record.Level, skill.MaxLevel));
            }
            Refresh(memberId);
        }

        private const string ResistancePrefix = "resist:";

        private void Refresh(int memberId)
        {
            var member = _world.GetMember(memberId);
            if (_applied.TryGetValue(memberId, out var ids))
            {
                foreach (var id in ids)
                {
                    member?.RemoveLastingModifier(id);
                    _world.Resistances.RemoveSource(memberId, id);
                }
                _applied.Remove(memberId);
            }
            if (member == null)
                return;

            var now = new List<string>();
            foreach (var (skill, level) in LearnedBy(memberId))
            {
                for (int i = 0; i < skill.Bonuses.Count; i++)
                {
                    var bonus = skill.Bonuses[i];
                    if (bonus == null || string.IsNullOrEmpty(bonus.StatId))
                        continue;
                    string id = "skill:" + skill.Id + ":" + i;
                    float value = bonus.CalculateBonus(level);
                    if (bonus.StatId.StartsWith(ResistancePrefix, StringComparison.Ordinal) && bonus.StatId.Length > ResistancePrefix.Length)
                        _world.Resistances.Set(memberId, bonus.StatId.Substring(ResistancePrefix.Length), value, id);
                    else if (CombatStats.TryParse(bonus.StatId, out var stat))
                        Apply(member, id, stat, bonus.Type, value);
                    else
                        continue;
                    now.Add(id);
                }
            }
            if (now.Count > 0)
                _applied[memberId] = now;
        }

        private static void Apply(Combatant member, string id, CombatStat stat, BonusType type, float value)
        {
            switch (type)
            {
                case BonusType.Multiplicative:
                    // +10% is times 1.1
                    member.SetLastingModifier(id, stat, ModifierOperation.Multiply, 1f + value);
                    break;
                case BonusType.Override:
                    member.SetLastingModifier(id, stat, ModifierOperation.Override, value);
                    break;
                default:
                    member.SetLastingModifier(id, stat, ModifierOperation.Add, value);
                    break;
            }
        }
    }
}
