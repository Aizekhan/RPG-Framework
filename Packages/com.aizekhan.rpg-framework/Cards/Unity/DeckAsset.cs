using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGFramework.Cards.Unity
{
    /// <summary>
    /// A card of a <see cref="DeckAsset{TCard}"/> in its number of copies.
    /// </summary>
    [Serializable]
    public struct DeckEntry<TCard>
    {
        public TCard Card;
        [Min(0)] public int Copies;

        public DeckEntry(TCard card, int copies)
        {
            Card = card;
            Copies = copies;
        }
    }

    /// <summary>
    /// A deck a designer fills in the inspector: each card (an asset, an id) and how many copies. The game makes one
    /// per card type: <c>[CreateAssetMenu] class MonsterDeck : DeckAsset&lt;MonsterCard&gt; { }</c>, then
    /// <c>var deck = monsters.Build(); deck.Shuffle(dice);</c>.
    /// </summary>
    public abstract class DeckAsset<TCard> : ScriptableObject
    {
        [SerializeField] private List<DeckEntry<TCard>> _cards = new List<DeckEntry<TCard>>();

        public IReadOnlyList<DeckEntry<TCard>> Entries => _cards;

        /// <summary>
        /// Every copy counted.
        /// </summary>
        public int Count
        {
            get
            {
                int count = 0;
                foreach (var entry in _cards)
                    count += Math.Max(0, entry.Copies);
                return count;
            }
        }

        /// <summary>
        /// A new deck with every copy, in the order of the asset (shuffle it to play).
        /// </summary>
        public Deck<TCard> Build()
        {
            var cards = new List<(TCard, int)>();
            foreach (var entry in _cards)
            {
                if (entry.Copies > 0 && entry.Card != null)
                    cards.Add((entry.Card, entry.Copies));
            }
            return Deck<TCard>.FromCopies(cards);
        }

        /// <summary>
        /// For code and tests: adds a card in its number of copies.
        /// </summary>
        public void Add(TCard card, int copies = 1) => _cards.Add(new DeckEntry<TCard>(card, copies));
    }
}
