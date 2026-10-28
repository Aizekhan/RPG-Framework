using System;
using System.Collections.Generic;
using System.IO;

namespace RPGFramework.Turns
{
    /// <summary>
    /// A move a player asks for ("Roll", "PlayCard" 2, "BuyItem" "sword"): a kind with numbers and texts, small enough
    /// to send and to keep in a match record.
    /// </summary>
    public sealed class TurnIntent
    {
        public TurnIntent(string kind, int[] numbers = null, string[] texts = null)
        {
            Kind = kind ?? "";
            Numbers = numbers ?? Array.Empty<int>();
            Texts = texts ?? Array.Empty<string>();
        }

        public string Kind { get; }
        public int[] Numbers { get; }
        public string[] Texts { get; }

        /// <returns>The number at <paramref name="index"/>, or <paramref name="fallback"/>.</returns>
        public int Number(int index = 0, int fallback = 0) => index >= 0 && index < Numbers.Length ? Numbers[index] : fallback;

        /// <returns>The text at <paramref name="index"/>, or null.</returns>
        public string Text(int index = 0) => index >= 0 && index < Texts.Length ? Texts[index] : null;

        public static TurnIntent Of(string kind, params int[] numbers) => new TurnIntent(kind, numbers);

        public static TurnIntent WithText(string kind, params string[] texts) => new TurnIntent(kind, null, texts);

        public override string ToString() =>
            $"{Kind}({string.Join(", ", Numbers)}{(Numbers.Length > 0 && Texts.Length > 0 ? ", " : "")}{string.Join(", ", Texts)})";

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Kind);
            writer.Write(Numbers.Length);
            foreach (int number in Numbers)
                writer.Write(number);
            writer.Write(Texts.Length);
            foreach (string text in Texts)
                writer.Write(text ?? "");
        }

        internal static TurnIntent Read(BinaryReader reader)
        {
            string kind = reader.ReadString();
            var numbers = new int[TurnProtocol.ReadCount(reader)];
            for (int i = 0; i < numbers.Length; i++)
                numbers[i] = reader.ReadInt32();
            var texts = new string[TurnProtocol.ReadCount(reader)];
            for (int i = 0; i < texts.Length; i++)
                texts[i] = reader.ReadString();
            return new TurnIntent(kind, numbers, texts);
        }
    }

    /// <summary>
    /// What the rules said to a move: done, or refused with a reason for the player.
    /// </summary>
    public readonly struct TurnOutcome
    {
        private TurnOutcome(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason ?? "";
        }

        public bool Accepted { get; }
        public string Reason { get; }

        public static TurnOutcome Ok => new TurnOutcome(true, "");

        public static TurnOutcome Refused(string reason) => new TurnOutcome(false, string.IsNullOrEmpty(reason) ? "Refused" : reason);

        public override string ToString() => Accepted ? "Ok" : "Refused: " + Reason;
    }

    /// <summary>
    /// A place at the table: who sits there, whether they are ready (lobby) and connected (a seat stays taken while
    /// its player is away during a match). The host sits at seat 0.
    /// </summary>
    public sealed class TurnSeat
    {
        public int Seat { get; internal set; }
        public string Name { get; internal set; } = "";
        public bool Ready { get; internal set; }
        public bool Connected { get; internal set; } = true;
        public bool IsHost => Seat == 0;

        public override string ToString() => $"{Seat}: {Name}{(Ready ? " (ready)" : "")}{(Connected ? "" : " (away)")}";
    }

    /// <summary>
    /// Who may see a notice: everyone, only one seat (their own card), or only the host.
    /// </summary>
    public enum TurnVisibility
    {
        All,
        Seat,
        Host
    }

    /// <summary>
    /// Something that happened, for animations and logs ("die" 5, "card drawn"): the game's own kind and bytes.
    /// What a player may see of the match comes in their view; a notice only says what to show happening.
    /// </summary>
    public sealed class TurnNotice
    {
        public TurnNotice(string kind, byte[] data = null)
        {
            Kind = kind ?? "";
            Data = data ?? Array.Empty<byte>();
        }

        public string Kind { get; }
        public byte[] Data { get; }
    }

    /// <summary>
    /// A question to players who are not on turn (block with a card, pay to stop it, let it pass): the options they
    /// may answer and how long they have. An answer is an option index, or <see cref="Pass"/>.
    /// </summary>
    public sealed class TurnPrompt
    {
        public const int Pass = -1;

        public TurnPrompt(int windowId, string kind, IReadOnlyList<string> options, float seconds, byte[] data = null)
        {
            WindowId = windowId;
            Kind = kind ?? "";
            Options = options ?? Array.Empty<string>();
            Seconds = seconds;
            Data = data ?? Array.Empty<byte>();
        }

        public int WindowId { get; }

        /// <summary>
        /// The game's name for the question ("block", "ambush").
        /// </summary>
        public string Kind { get; }

        public IReadOnlyList<string> Options { get; }

        /// <summary>
        /// Seconds to answer; 0 or less: no limit.
        /// </summary>
        public float Seconds { get; }

        public byte[] Data { get; }
    }

    /// <summary>
    /// The game's rules as the host runs them: who may move now, what a move does, what each seat may see.
    /// Only the host has them; players get their views.
    /// </summary>
    public interface ITurnRules
    {
        /// <summary>
        /// The seat whose turn it is (the turn timer counts for it); -1: nobody (between turns, the match is over).
        /// </summary>
        int CurrentSeat { get; }

        /// <summary>
        /// Whether a seat may make this move now: the current player, or a player answering in a reaction the rules
        /// opened. Moves refused here never reach <see cref="Apply"/>.
        /// </summary>
        bool CanAct(int seat, TurnIntent intent);

        /// <summary>
        /// Does the move. The game may announce what happened with <c>TurnHost.Notify</c> while it runs.
        /// </summary>
        TurnOutcome Apply(int seat, TurnIntent intent);

        /// <summary>
        /// Everything a seat may see of the match now (its own hand, everyone's positions, a trap's place but not its
        /// kind), in the game's bytes. Sent after every change and when a player comes back.
        /// </summary>
        byte[] ViewFor(int seat);

        /// <summary>
        /// The move made for a seat whose turn ran out (<c>TurnHostOptions.TurnSeconds</c>); null: none.
        /// </summary>
        TurnIntent DefaultIntent(int seat);
    }
}
