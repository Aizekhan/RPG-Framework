using System;
using System.Collections.Generic;
using RPGFramework.Dice;

namespace RPGFramework.Cards
{
    /// <summary>
    /// An ordered pile of cards: shuffle with the dice (a seed gives the same order), draw from the top, put on top
    /// or under, look at or take one of the top cards. Index 0 is the top. A card is anything: an id, a data class,
    /// an asset.
    /// </summary>
    public sealed class Deck<TCard>
    {
        private readonly List<TCard> _cards;

        public Deck()
        {
            _cards = new List<TCard>();
        }

        /// <param name="cards">From the top down.</param>
        public Deck(IEnumerable<TCard> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            _cards = new List<TCard>(cards);
        }

        /// <summary>
        /// A deck of each card in its number of copies, in the order given (shuffle it to play).
        /// </summary>
        public static Deck<TCard> FromCopies(IEnumerable<(TCard Card, int Copies)> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            var deck = new Deck<TCard>();
            foreach (var (card, copies) in cards)
            {
                for (int i = 0; i < copies; i++)
                    deck._cards.Add(card);
            }
            return deck;
        }

        public int Count => _cards.Count;

        public bool IsEmpty => _cards.Count == 0;

        /// <summary>
        /// The cards from the top down (for views that may see them, and saves).
        /// </summary>
        public IReadOnlyList<TCard> Cards => _cards;

        /// <summary>
        /// Shuffles with the dice (Fisher-Yates; the rolls are logged as <see cref="DicePurposes.Shuffle"/>).
        /// </summary>
        public void Shuffle(IDiceService dice)
        {
            if (dice == null)
                throw new ArgumentNullException(nameof(dice));
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = dice.Roll(i + 1, DicePurposes.Shuffle) - 1;
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>
        /// Takes the top card; false when the deck is empty.
        /// </summary>
        public bool TryDraw(out TCard card) => TryTake(0, out card);

        /// <summary>
        /// Takes the card <paramref name="index"/> places from the top (choosing one of the top cards).
        /// </summary>
        public bool TryTake(int index, out TCard card)
        {
            if (index < 0 || index >= _cards.Count)
            {
                card = default;
                return false;
            }
            card = _cards[index];
            _cards.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Takes this card wherever it is (the first from the top); false when it is not in the deck.
        /// </summary>
        public bool Remove(TCard card) => _cards.Remove(card);

        /// <summary>
        /// Up to <paramref name="count"/> cards from the top, without taking them.
        /// </summary>
        public IReadOnlyList<TCard> Peek(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            return _cards.GetRange(0, Math.Min(count, _cards.Count));
        }

        /// <summary>
        /// Puts a card at the bottom.
        /// </summary>
        public void PutUnder(TCard card) => _cards.Add(card);

        /// <summary>
        /// Puts a card on top (drawn next).
        /// </summary>
        public void PutOnTop(TCard card) => _cards.Insert(0, card);

        /// <summary>
        /// Puts a card <paramref name="index"/> places from the top (0: on top; past the bottom: at the bottom).
        /// </summary>
        public void Insert(int index, TCard card) => _cards.Insert(Math.Max(0, Math.Min(index, _cards.Count)), card);

        /// <summary>
        /// Puts every card of another pile (a discard pile) under this one, and empties it.
        /// </summary>
        public void PutUnder(Deck<TCard> pile)
        {
            if (pile == null || ReferenceEquals(pile, this))
                return;
            _cards.AddRange(pile._cards);
            pile._cards.Clear();
        }

        public void Clear() => _cards.Clear();
    }

    /// <summary>
    /// The cards a player holds, in the order they came, with an optional limit.
    /// </summary>
    public sealed class Hand<TCard>
    {
        private readonly List<TCard> _cards = new List<TCard>();

        /// <param name="limit">Cards held at most; 0: no limit.</param>
        public Hand(int limit = 0)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
        }

        public int Limit { get; }

        public int Count => _cards.Count;

        public bool IsFull => Limit > 0 && _cards.Count >= Limit;

        public IReadOnlyList<TCard> Cards => _cards;

        /// <summary>
        /// Takes a card into the hand; false when the hand is full.
        /// </summary>
        public bool TryAdd(TCard card)
        {
            if (IsFull)
                return false;
            _cards.Add(card);
            return true;
        }

        /// <summary>
        /// Draws the top card of a deck into the hand; false when the hand is full or the deck empty.
        /// </summary>
        public bool TryDrawFrom(Deck<TCard> deck)
        {
            if (deck == null)
                throw new ArgumentNullException(nameof(deck));
            if (IsFull || !deck.TryDraw(out var card))
                return false;
            _cards.Add(card);
            return true;
        }

        /// <summary>
        /// Plays or gives away the card at <paramref name="index"/>.
        /// </summary>
        public bool TryTake(int index, out TCard card)
        {
            if (index < 0 || index >= _cards.Count)
            {
                card = default;
                return false;
            }
            card = _cards[index];
            _cards.RemoveAt(index);
            return true;
        }

        public bool Remove(TCard card) => _cards.Remove(card);

        public bool Contains(TCard card) => _cards.Contains(card);

        public void Clear() => _cards.Clear();
    }
}
