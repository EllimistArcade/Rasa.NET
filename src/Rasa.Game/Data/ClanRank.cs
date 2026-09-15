namespace Rasa.Data
{
    /// <summary>
    /// The four clan ranks and what each one may do, from the client's own
    /// <c>shared.gameconstants</c>: <c>CLAN_RANK_1</c> through <c>CLAN_RANK_4</c> are 0 to 3, and
    /// <c>CLAN_RANK_LEADER</c> is the last of them.
    ///
    /// These have to match, because the client decides what to *offer* from the same numbers -
    /// it greys out the invite button below <c>CLAN_MIN_RANK_TO_INVITE</c> and refuses to send on
    /// the leaders channel below <c>CLAN_MIN_RANK_TO_SPEAK_IN_LEADERS_CHANNEL</c>. A server that
    /// drew the line somewhere else would either refuse things the window had already offered, or
    /// allow things it never shows.
    ///
    /// They are the client's *own* view of the sender's rank, though, which is as current as the
    /// last roster it was sent. Every one of these is checked here as well.
    /// </summary>
    public static class ClanRank
    {
        /// <summary>The rank a member joins at, and the lowest that exists.</summary>
        public const byte Member = 0;

        /// <summary>CLAN_RANK_4, and CLAN_RANK_LEADER: one per clan.</summary>
        public const byte Leader = 3;

        /// <summary>CLAN_MIN_RANK_TO_INVITE.</summary>
        public const byte MinRankToInvite = 1;

        /// <summary>CLAN_MIN_RANK_TO_SPEAK_IN_LEADERS_CHANNEL.</summary>
        public const byte MinRankToSpeakInLeadersChannel = 2;

        /// <summary>
        /// CLAN_RANK_TO_WITHDRAW_FROM_LOCKBOX. Buying a lockbox tab is held to this as well: it
        /// spends the clan's prestige, which is a withdrawal in everything but name, and the
        /// client gates the withdraw button on it while leaving the purchase button open.
        /// </summary>
        public const byte MinRankToWithdrawFromLockbox = 2;

        /// <summary>CLAN_RANK_TO_CHALLENGE, for feuds and wargames. Unused until 8.8.</summary>
        public const byte MinRankToChallenge = 3;
    }
}
