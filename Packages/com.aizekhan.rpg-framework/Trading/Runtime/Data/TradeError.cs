namespace RPGFramework.Trading
{
    /// <summary>
    /// Why a trade failed (<see cref="TradeResultData.Error"/>), for a game to tell the player in its own words;
    /// <see cref="TradeResultData.ErrorMessage"/> stays the English sentence for logs.
    /// </summary>
    public enum TradeError
    {
        /// <summary>The trade went through.</summary>
        None,

        /// <summary>A reason the framework has no code for (<see cref="TradeResultData.ErrorMessage"/> says it).</summary>
        Other,

        /// <summary>AcceptTrade without ProposeTradeOffer first.</summary>
        NotProposed,

        /// <summary>No trader has the offer.</summary>
        OfferNotFound,

        /// <summary>The offer's trader is gone.</summary>
        TraderNotFound,

        /// <summary>The offer was used up (sold out, or a one-time offer already taken).</summary>
        OfferNotAvailable,

        /// <summary>The trader does not deal with the player at their reputation or standing.</summary>
        ReputationTooLow,

        /// <summary>The player does not have the items the trader asks for.</summary>
        MissingItems,

        /// <summary>The offered items did not fit in the player's inventory.</summary>
        CouldNotGiveItems,

        /// <summary>The player cannot pay the price.</summary>
        InsufficientGold,

        /// <summary>The player is not in the party (action RPG kit).</summary>
        NotInParty,

        /// <summary>The game has no traders (action RPG kit).</summary>
        NoTraders
    }
}
