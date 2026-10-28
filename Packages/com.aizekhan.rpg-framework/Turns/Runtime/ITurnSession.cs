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

        event Action LobbyChanged;

        event Action Started;

        event Action<byte[]> ViewChanged;

        event Action<TurnNotice> Noticed;

        event Action<TurnPrompt> Prompted;

        event Action<int> PromptClosed;

        /// <summary>
        /// The host's answer to one of this machine's moves (request id, outcome).
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
