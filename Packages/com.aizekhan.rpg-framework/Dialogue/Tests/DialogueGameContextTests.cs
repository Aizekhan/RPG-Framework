using NUnit.Framework;

namespace RPGFramework.Dialogue.Tests
{
    /// <summary>
    /// The game's own answers: ItemOwned counts a quantity, Custom asks the game; a context that cannot answer fails
    /// those instead of guessing.
    /// </summary>
    public sealed class DialogueGameContextTests
    {
        [Test]
        public void ItemOwned_CountsTheQuantity_AndCustomAsksTheGame()
        {
            int coins = 2;
            var context = new DialogueContext(1)
            {
                ItemCount = id => id == "coin" ? coins : 0,
                Custom = (id, value) => id == "night" && value == 1
            };

            Assert.That(new DialogueCondition(ConditionType.ItemOwned, "coin", 1).Evaluate(context), Is.True);
            Assert.That(new DialogueCondition(ConditionType.ItemOwned, "coin", 3).Evaluate(context), Is.False);
            coins = 3;
            Assert.That(new DialogueCondition(ConditionType.ItemOwned, "coin", 3).Evaluate(context), Is.True);
            Assert.That(new DialogueCondition(ConditionType.ItemOwned, "gem", 0).Evaluate(context), Is.False, "0 asks for one");

            Assert.That(new DialogueCondition(ConditionType.Custom, "night", 1).Evaluate(context), Is.True);
            Assert.That(new DialogueCondition(ConditionType.Custom, "night", 0).Evaluate(context), Is.False);
            Assert.That(new DialogueCondition(ConditionType.Custom, "night", 1).Evaluate(new DialogueContext(1)), Is.False,
                "no answer: false");
        }
    }
}
