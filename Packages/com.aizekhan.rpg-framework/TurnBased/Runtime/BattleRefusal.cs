using System.Collections.Generic;
using RPGFramework.Localization;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// Why a battle refused an action, as a code for a game's own rules (<see cref="BattleRefusal.Reason"/>).
    /// </summary>
    public enum BattleRefusalReason
    {
        None,

        /// <summary>A reason of another module (the Abilities module refusing); the text carries it as written.</summary>
        Other,

        BattleOver,
        NotStarted,
        NotYourTurn,
        UnknownAction,

        ChooseTarget,
        TargetNotInBattle,
        CannotAttackAlly,
        CannotUseOnAlly,
        CannotUseOnEnemy,
        TargetNotDown,
        TargetAlreadyDown,

        OnlyPartyFlees,
        CannotFlee,

        ChooseAbility,
        NoSuchAbility,
        DoesNotKnowAbility,
        Silenced,
        AbilityNotForBattle,
        CastTimeNotSupported,
        NotReady,
        NoUsesLeft,
        UnknownResource,
        NotEnoughMana,
        NotEnoughHealth,
        NobodyToReach,

        OnlyPartyUsesItems,
        ChooseItem,
        NoSuchItem,
        ItemNotForBattle
    }

    /// <summary>
    /// Why a battle refused an action: its <see cref="Reason"/>, a <see cref="Text"/> to translate (a
    /// <c>battle.refusal.*</c> key from <see cref="BattleTexts"/> with the names and numbers as arguments) and the
    /// <see cref="Message"/> in English. <see cref="IsNone"/> when the action is allowed.
    /// </summary>
    public readonly struct BattleRefusal
    {
        private BattleRefusal(BattleRefusalReason reason, LocalizedText text, string message)
        {
            Reason = reason;
            Text = text;
            Message = message;
        }

        public static BattleRefusal None => default;

        public BattleRefusalReason Reason { get; }
        public LocalizedText Text { get; }

        /// <summary>
        /// The refusal in English; null when there is none.
        /// </summary>
        public string Message { get; }

        public bool IsNone => Reason == BattleRefusalReason.None;

        internal static BattleRefusal Of(BattleRefusalReason reason, params (string Name, object Value)[] args)
        {
            var text = new LocalizedText(BattleTexts.KeyOf(reason), args);
            return new BattleRefusal(reason, text, BattleTexts.English(text));
        }

        public override string ToString() => IsNone ? "None" : Reason + ": " + Message;
    }

    /// <summary>
    /// The turn-based battle's own texts (<c>battle.refusal.*</c>), in English. A game translates them in its string
    /// tables (<see cref="Table"/> is a start). Names of combatants, abilities and items are put in as the game's
    /// content gives them; <c>{count}</c> picks the plural form (keys ending in <c>.one</c> / <c>.other</c>).
    /// </summary>
    public static class BattleTexts
    {
        public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
        {
            ["battle.refusal.other"] = "{reason}",
            ["battle.refusal.battleOver"] = "The battle is over.",
            ["battle.refusal.notStarted"] = "The battle has not started.",
            ["battle.refusal.notYourTurn"] = "It is not {name}'s turn; it is {current}'s.",
            ["battle.refusal.unknownAction"] = "Unknown action {action}.",
            ["battle.refusal.chooseTarget"] = "Choose a target.",
            ["battle.refusal.targetNotInBattle"] = "That target is not in this battle.",
            ["battle.refusal.cannotAttackAlly"] = "{actor} cannot attack an ally.",
            ["battle.refusal.cannotUseOnAlly"] = "{actor} cannot use {what} on an ally.",
            ["battle.refusal.cannotUseOnEnemy"] = "{actor} cannot use {what} on an enemy.",
            ["battle.refusal.targetNotDown"] = "{target} is not down.",
            ["battle.refusal.targetAlreadyDown"] = "{target} is already down.",
            ["battle.refusal.onlyPartyFlees"] = "Only the party can flee.",
            ["battle.refusal.cannotFlee"] = "There is no running from this fight.",
            ["battle.refusal.chooseAbility"] = "Choose an ability.",
            ["battle.refusal.noSuchAbility"] = "There is no ability '{ability}'.",
            ["battle.refusal.doesNotKnowAbility"] = "{actor} does not know {ability}.",
            ["battle.refusal.silenced"] = "{actor} is silenced and cannot use abilities.",
            ["battle.refusal.abilityNotForBattle"] = "{ability} cannot be used in battle ({type}).",
            ["battle.refusal.castTimeNotSupported"] = "{ability} takes time to cast, which turn-based battles do not support.",
            ["battle.refusal.notReady.one"] = "{ability} is not ready: {count} more round.",
            ["battle.refusal.notReady.other"] = "{ability} is not ready: {count} more rounds.",
            ["battle.refusal.noUsesLeft"] = "{ability} has no uses left.",
            ["battle.refusal.unknownResource"] = "{ability} costs {resource}, which combatants do not have.",
            ["battle.refusal.notEnoughMana"] = "Not enough mana: {ability} costs {cost} MP and {actor} has {has}.",
            ["battle.refusal.notEnoughHealth"] = "{ability} costs {cost} HP, more than {actor} can give.",
            ["battle.refusal.nobodyToReach"] = "{ability} has nobody to reach.",
            ["battle.refusal.onlyPartyUsesItems"] = "Only the party uses items.",
            ["battle.refusal.chooseItem"] = "Choose an item.",
            ["battle.refusal.noSuchItem"] = "The party has no {item}.",
            ["battle.refusal.itemNotForBattle"] = "{item} cannot be used in battle."
        };

        private static readonly ILocalization EnglishTexts = Build();

        /// <summary>
        /// The battle's texts as an English table, to add to the game's or to write out for translators.
        /// </summary>
        public static StringTable Table(string language = "en", string displayName = "English")
        {
            var table = new StringTable(language, displayName);
            foreach (var text in Keys)
                table.Set(text.Key, text.Value);
            return table;
        }

        /// <summary>
        /// The key of a refusal's text ("battle.refusal.notEnoughMana"); null for <see cref="BattleRefusalReason.None"/>.
        /// </summary>
        public static string KeyOf(BattleRefusalReason reason)
        {
            if (reason == BattleRefusalReason.None)
                return null;
            string name = reason.ToString();
            return "battle.refusal." + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        internal static string English(LocalizedText text) => EnglishTexts.Text(text);

        private static ILocalization Build()
        {
            var table = Table();
            return new LocalizationService(new[] { table }, table.Language);
        }
    }
}
