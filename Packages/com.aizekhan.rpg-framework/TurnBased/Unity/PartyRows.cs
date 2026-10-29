using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Factions;
using RPGFramework.UI.Panels;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Pages of a turn-based run for an <see cref="ActionListPanel"/> between battles: a member's skill tree, the
    /// party's quests (with "Hand in" when the game hands them in itself), its faction standing and its achievements.
    /// Rows read the world when the panel refreshes; bind a page again when what it lists changes. English texts,
    /// passed through <see cref="Text"/> when the game sets it (its localization).
    /// </summary>
    public static class PartyRows
    {
        /// <summary>
        /// The game's words for a key and its English ("party.learn", "Learn"); null shows the English.
        /// </summary>
        public static Func<string, string, string> Text { get; set; }

        /// <summary>
        /// A member's skill tree: their points, a row per skill with "Learn", and "Reset".
        /// </summary>
        public static List<ActionRow> Skills(TurnBasedWorld world, int memberId)
        {
            var rows = new List<ActionRow>();
            var skills = world?.Skills;
            var member = world?.GetMember(memberId);
            if (skills == null || !skills.HasTree || member == null)
                return rows;
            rows.Add(new ActionRow(() => $"<b>{member.Name}</b>  {Say("party.points", "Skill points")}: {skills.PointsOf(memberId)}"));
            foreach (var skill in skills.Tree.Skills.Where(s => s != null))
            {
                var shown = skill;
                rows.Add(new ActionRow(() => $"{shown.Name}  {skills.LevelOf(memberId, shown.Id)}/{shown.MaxLevel}", null,
                    new ActionButton(() => Say("party.learn", "Learn"), () => skills.Learn(memberId, shown.Id),
                        () => skills.WhyNot(memberId, shown.Id) == null)));
            }
            rows.Add(new ActionRow(() => "", null, new ActionButton(() => Say("party.reset", "Reset"), () => skills.Reset(memberId),
                () => skills.LearnedBy(memberId).Count > 0)));
            return rows;
        }

        /// <summary>
        /// The party's quests: each objective's progress, "Hand in" for a finished one the game hands in itself
        /// (<see cref="TurnBasedSettings.CompleteQuestsAutomatically"/> off), then the done ones.
        /// </summary>
        public static List<ActionRow> Quests(TurnBasedWorld world)
        {
            var rows = new List<ActionRow>();
            if (world == null || !world.HasQuests)
                return rows;
            foreach (var (quest, _) in world.ActiveQuests)
            {
                var shown = quest;
                rows.Add(new ActionRow(() =>
                {
                    var progress = world.ActiveQuests.FirstOrDefault(q => q.Quest.Id == shown.Id).Progress;
                    var parts = shown.Objectives.Select(o =>
                        $"{(string.IsNullOrEmpty(o.Description) ? o.TargetId : o.Description)} {Math.Min(o.RequiredCount, Done(progress, o.Id))}/{o.RequiredCount}");
                    return $"<b>{shown.Title}</b>  {string.Join(", ", parts)}";
                }, null, new ActionButton(() => Say("party.handIn", "Hand in"), () => world.CompleteQuest(shown.Id),
                    () => IsDone(shown, world.ActiveQuests.FirstOrDefault(q => q.Quest.Id == shown.Id).Progress))));
            }
            foreach (string done in world.CompletedQuests)
                rows.Add(new ActionRow(() => $"{done}  ✓"));
            return rows;
        }

        /// <summary>
        /// The party's standing with each faction ("The Village  Friendly (35)").
        /// </summary>
        public static List<ActionRow> Reputation(TurnBasedWorld world)
        {
            var rows = new List<ActionRow>();
            var reputation = world?.Reputation;
            if (reputation == null)
                return rows;
            foreach (var faction in reputation.Factions.GetAllFactions().Where(f => f != null && f.IsActive))
            {
                var shown = faction;
                rows.Add(new ActionRow(() =>
                {
                    FactionStanding standing = reputation.StandingOf(shown.FactionId);
                    return $"{shown.Name}  <b>{Say("party.standing." + standing, standing.ToString())}</b> ({reputation.Of(shown.FactionId)})";
                }));
            }
            return rows;
        }

        /// <summary>
        /// The party's achievements: the unlocked ones marked, the rest with their progress; hidden ones only once
        /// unlocked.
        /// </summary>
        public static List<ActionRow> Achievements(TurnBasedWorld world)
        {
            var rows = new List<ActionRow>();
            var tracker = world?.Achievements;
            if (tracker == null)
                return rows;
            int holder = world.AchievementHolderId;
            foreach (var achievement in tracker.Registry.GetAllAchievements().Where(a => a != null).OrderBy(a => a.Name, StringComparer.Ordinal))
            {
                var shown = achievement;
                rows.Add(new ActionRow(() =>
                {
                    if (tracker.IsUnlocked(holder, shown.AchievementId))
                        return $"<b>{shown.Name}</b>  ✓";
                    if (shown.IsHidden || shown.IsSecret)
                        return "???";
                    return $"{shown.Name}  {(int)Math.Round(tracker.ProgressOf(holder, shown.AchievementId) * 100f)}%";
                }));
            }
            return rows;
        }

        private static bool IsDone(RPGFramework.Quests.IQuest quest, RPGFramework.Quests.IQuestProgress progress) =>
            progress != null && quest.Objectives.Where(o => !o.IsOptional).All(o => Done(progress, o.Id) >= o.RequiredCount);

        private static int Done(RPGFramework.Quests.IQuestProgress progress, string objectiveId) =>
            progress != null && progress.ObjectiveProgress.TryGetValue(objectiveId, out int done) ? done : 0;

        private static string Say(string key, string english) => Text?.Invoke(key, english) ?? english;
    }
}
