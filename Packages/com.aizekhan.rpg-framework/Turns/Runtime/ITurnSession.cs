using System;
using System.Collections.Generic;

namespace RPGFramework.Turns
{
    /// <summary>
    /// A turn-based match as one machine takes part in it, the host's or a player's: the seats, this machine's view
    /// of the match, its moves and answers, and what happens. Game screens are written once against it.
    /// </summary>
    public interface ITurnSession
    {
        /// <summary>
        /// This machine's seat (the host is 0); -1 before a player is let in.
        /// </summary>
        int LocalSeat { get; }

        IReadOnlyList<TurnSeat> Seats { get; }

        bool IsStarted { get; }

        /// <summary>
        /// The match options the host started with (the game's bytes).
        /// </summary>
        byte[] Options { get; }

        /// <summary>
        /// What this seat may see of the match now (the game's bytes from <see cref="ITurnRules.ViewFor"/>).
        /// </summary>
        byte[] View { get; }

        /// <summary>
        /// Questions to this seat that are still open.
        /// </summary>
        IReadOnlyList<TurnPrompt> OpenPrompts { get; }

        /// <summary>
        /// Seconds left of the current step of the turn; 0 without a limit.
        /// </summary>
        float TurnTimeLeft { get; }

        /// <summary>
        /// Seconds the current step has in all (<see cref="ITurnClock.SecondsFor"/> or <c>TurnHostOptions.TurnSeconds</c>); 0: no limit.
        /// </summary>
        float TurnLength { get; }

        /// <summary>
        /// The turn's time is running now (a limit, someone on turn, no question open, no pause).
        /// </summary>
        bool TurnClockRuns { get; }

        /// <summary>
        /// The seat whose pause stops the turn's time now; -1: none.
        /// </summary>
        int PausedBy { get; }

        /// <summary>
        /// Pause seconds a seat has left for the match (<c>TurnHostOptions.PauseBankSeconds</c>, less what its pauses used).
        /// </summary>
        float BankOf(int seat);

        /// <summary>
        /// Whether a seat may pause now: the match runs, nobody has paused, it has bank left, and the host's
        /// <c>TurnHostOptions.PauseBy</c> and <c>CanPause</c> let it (a player knows it from the host's clock).
        /// </summary>
        bool CanPause(int seat);

        /// <summary>
        /// Stops the turn's time (and the questions' time) with this seat's pause bank, until <see cref="ResumeTurn"/> or the bank runs out.
        /// </summary>
        /// <returns>False when it cannot (<see cref="CanPause"/> is false for this seat).</returns>
        bool PauseTurn();

        /// <summary>
        /// Ends this seat's pause (the host ends anyone's).
        /// </summary>
        void ResumeTurn();

        /// <summary>
        /// The clock changed: a new step, a pause or its end, a question opened or closed.
        /// </summary>
        event Action ClockChanged;

        event Action LobbyChanged;

        event Action Started;

        event Action<byte[]> ViewChanged;

        event Action<TurnNotice> Noticed;

        event Action<TurnPrompt> Prompted;

        event Action<int> PromptClosed;

        /// <summary>
        /// The host's answer to one of this machine's moves (request id, outcome); request id
        /// <see cref="TurnProtocol.PauseRequestId"/> is a pause the host refused.
        /// </summary>
        event Action<int, TurnOutcome> Replied;

        /// <summary>
        /// Asks for a move as this seat. The host decides; <see cref="Replied"/> tells.
        /// </summary>
        /// <returns>The request id, or -1 when it cannot be sent (not in a started match).</returns>
        int Submit(TurnIntent intent);

        /// <summary>
        /// Answers an open question: an option index, or <see cref="TurnPrompt.Pass"/>.
        /// </summary>
        void Answer(int windowId, int option);

        /// <summary>
        /// Ready (or not) in the lobby.
        /// </summary>
        void SetReady(bool ready);

        void Update(float deltaTime);
    }
}
