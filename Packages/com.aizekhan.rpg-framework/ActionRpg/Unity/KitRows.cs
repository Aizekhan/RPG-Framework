using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Factions;
using RPGFramework.Items;
using RPGFramework.Localization;
using RPGFramework.UI.Panels;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// Pages of a player's kit systems for an <see cref="ActionListPanel"/>: their skill tree, recipes, consumables,
    /// faction standing, achievements and the conversation they are in. Each row reads the world when the panel
    /// refreshes; its buttons act for the player. Texts read <see cref="Keys"/> from the localization (English
    /// without one). Bind a page again when what it lists changes (a new item, a learned skill shows itself).
    /// </summary>
    public static class KitRows
    {
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["kit.menu.points"] = "Skill points: {points}",
            ["kit.menu.skill"] = "{name}  {level}/{max}",
            ["kit.menu.learn"] = "Learn",
            ["kit.menu.reset"] = "Reset",
            ["kit.menu.recipe"] = "{name}  ({ingredients})",
            ["kit.menu.craft"] = "Craft",
            ["kit.menu.crafting"] = "{name}  {percent}%",
            ["kit.menu.consumable"] = "{name}  x{count}",
            ["kit.menu.cooldown"] = "{name}  x{count}  ({seconds}s)",
            ["kit.menu.use"] = "Use",
            ["kit.menu.standing"] = "{name}  <b>{standing}</b> ({value})",
            ["kit.menu.achievement"] = "{name}  {percent}%",
            ["kit.menu.unlocked"] = "<b>{name}</b>  ✓",
            ["kit.menu.continue"] = "Continue",
            ["kit.menu.leave"] = "Leave"
        };

        /// <summary>
        /// The skill tree: the points left, a row per skill with "Learn" (when it can be learned) and "Reset".
        /// </summary>
        public static List<ActionRow> Skills(ActorWorld world, int playerId, ILocalization localization = null)
        {
            var rows = new List<ActionRow>();
            var skills = world?.Skills;
            if (skills == null || !skills.HasTree)
                return rows;
            rows.Add(new ActionRow(() => Text(localization, "kit.menu.points", ("points", skills.PointsOf(playerId)))));
            foreach (var skill in skills.Tree.Skills.Where(s => s != null))
            {
                var shown = skill;
                rows.Add(new ActionRow(
                    () => Text(localization, "kit.menu.skill", ("name", shown.Name), ("level", skills.LevelOf(playerId, shown.Id)), ("max", shown.MaxLevel)),
                    null,
                    new ActionButton(() => Text(localization, "kit.menu.learn"), () => skills.Learn(playerId, shown.Id),
                        () => skills.WhyNot(playerId, shown.Id) == null)));
            }
            rows.Add(new ActionRow(() => "", null, new ActionButton(() => Text(localization, "kit.menu.reset"), () => skills.Reset(playerId),
                () => skills.LearnedBy(playerId).Count > 0)));
            return rows;
        }

        /// <summary>
        /// The recipes: what each needs, "Craft" when the player can start it (by hand, or at the station given), and
        /// the crafts under way with their progress.
        /// </summary>
        public static List<ActionRow> Recipes(ActorWorld world, int playerId, RPGFramework.Crafting.ICraftingStation station = null,
            ILocalization localization = null)
        {
            var rows = new List<ActionRow>();
            var crafting = world?.Crafting;
            if (crafting == null)
                return rows;
            foreach (var active in crafting.ActiveOf(playerId))
            {
                var craft = active;
                string name = crafting.Find(craft.RecipeId)?.Name ?? craft.RecipeId;
                rows.Add(new ActionRow(() => Text(localization, "kit.menu.crafting", ("name", name),
                    ("percent", (int)Math.Round(crafting.ProgressOf(playerId, craft.OperationId) * 100f)))));
            }
            foreach (var recipe in crafting.Recipes.GetAllRecipes().Where(r => r != null))
            {
                var shown = recipe;
                string ingredients = string.Join(", ", shown.Ingredients.Select(i => $"{NameOf(world, i.ItemId)} x{i.Quantity}"));
                rows.Add(new ActionRow(() => Text(localization, "kit.menu.recipe", ("name", shown.Name), ("ingredients", ingredients)), null,
                    new ActionButton(() => Text(localization, "kit.menu.craft"), () => crafting.Start(playerId, shown.RecipeId, station),
                        () => crafting.WhyNot(playerId, shown.RecipeId, station) == null)));
            }
            return rows;
        }

        /// <summary>
        /// The consumables in the player's backpack, how many, their cooldown, and "Use".
        /// </summary>
        public static List<ActionRow> Consumables(ActorWorld world, int playerId, ILocalization localization = null)
        {
            var rows = new List<ActionRow>();
            var backpack = world?.InventoryOf(playerId);
            if (backpack == null)
                return rows;
            var consumables = world.Consumables;
            foreach (string itemId in backpack.GetAllItems().OfType<IConsumable>().Select(c => c.ItemId).Distinct())
            {
                string id = itemId;
                string name = NameOf(world, id);
                rows.Add(new ActionRow(() =>
                {
                    int count = backpack.GetAllItems().Count(i => i != null && i.ItemId == id);
                    float left = consumables.CooldownLeft(playerId, id);
                    return left > 0f
                        ? Text(localization, "kit.menu.cooldown", ("name", name), ("count", count), ("seconds", (int)Math.Ceiling(left)))
                        : Text(localization, "kit.menu.consumable", ("name", name), ("count", count));
                }, null, new ActionButton(() => Text(localization, "kit.menu.use"), () => consumables.Use(playerId, id),
                    () => consumables.WhyNot(playerId, id) == null)));
            }
            return rows;
        }

        /// <summary>
        /// The player's standing with each faction ("City Guard  Friendly (40)"); standings read
        /// <c>kit.menu.standing.&lt;Standing&gt;</c> from the localization.
        /// </summary>
        public static List<ActionRow> Reputation(ActorWorld world, int playerId, ILocalization localization = null)
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
                    FactionStanding standing = reputation.StandingOf(playerId, shown.FactionId);
                    string standingText = localization != null ? localization.Text("kit.menu.standing." + standing, standing.ToString()) : standing.ToString();
                    return Text(localization, "kit.menu.standing", ("name", shown.Name), ("standing", standingText),
                        ("value", reputation.Of(playerId, shown.FactionId)));
                }));
            }
            return rows;
        }

        /// <summary>
        /// The achievements: the unlocked ones marked, the rest with their progress; hidden ones only once unlocked.
        /// </summary>
        public static List<ActionRow> Achievements(ActorWorld world, int playerId, ILocalization localization = null)
        {
            var rows = new List<ActionRow>();
            var tracker = world?.Achievements;
            if (tracker == null)
                return rows;
            foreach (var achievement in tracker.Registry.GetAllAchievements().Where(a => a != null).OrderBy(a => a.Name, StringComparer.Ordinal))
            {
                var shown = achievement;
                rows.Add(new ActionRow(() =>
                {
                    if (tracker.IsUnlocked(playerId, shown.AchievementId))
                        return Text(localization, "kit.menu.unlocked", ("name", shown.Name));
                    if (shown.IsHidden || shown.IsSecret)
                        return "???";
                    return Text(localization, "kit.menu.achievement", ("name", shown.Name),
                        ("percent", (int)Math.Round(tracker.ProgressOf(playerId, shown.AchievementId) * 100f)));
                }));
            }
            return rows;
        }

        /// <summary>
        /// The conversation the player is in: the line ("Elder: Welcome."), then a button per choice they can take, or
        /// "Continue" (and "Leave" at the end). Empty when they are not talking; bind it again after each step.
        /// </summary>
        public static List<ActionRow> Dialogue(ActorWorld world, int playerId, ILocalization localization = null)
        {
            var rows = new List<ActionRow>();
            var dialogue = world?.Dialogue;
            var node = dialogue?.Current(playerId);
            if (node == null)
                return rows;
            string line = string.IsNullOrEmpty(node.SpeakerId) ? node.Text : node.SpeakerId + ": " + node.Text;
            rows.Add(new ActionRow(() => line));
            var choices = dialogue.Choices(playerId);
            if (node.NodeType == RPGFramework.Dialogue.DialogueNodeType.Choice && choices.Count > 0)
            {
                foreach (var choice in choices)
                {
                    var shown = choice;
                    rows.Add(new ActionRow(() => "", null, new ActionButton(shown.Text, () => dialogue.Advance(playerId, shown.Id))));
                }
            }
            else
                rows.Add(new ActionRow(() => "", null, new ActionButton(() => Text(localization, "kit.menu.continue"), () => dialogue.Advance(playerId))));
            rows.Add(new ActionRow(() => "", null, new ActionButton(() => Text(localization, "kit.menu.leave"), () => dialogue.End(playerId))));
            return rows;
        }

        private static string NameOf(ActorWorld world, string itemId) => world.ItemOf(itemId)?.Name ?? itemId;

        private static string Text(ILocalization localization, string key, params (string Name, object Value)[] args) =>
            (localization ?? KitTexts.English).Text(LocalizedText.Of(key, args), Keys[key]);
    }
}
