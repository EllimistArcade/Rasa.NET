namespace Rasa.Data
{
    /// <summary>
    /// The rules a harvest is held to. These exist because harvesting is the one tool that makes
    /// an item out of nothing: every other tool moves a number that already existed, so the worst
    /// a forged request could do was heal the wrong person. Here a request that gets through is
    /// wealth, and the request arrives over the wire.
    ///
    /// What the client checks - harvest.py's CheckAction - is a courtesy to the player and is not
    /// a constraint on anything. Every rule below is enforced server-side, and the ones that cost
    /// the player something are charged whether the roll succeeds or not, so that retrying is not
    /// a way around the odds.
    /// </summary>
    public static class Harvest
    {
        /// <summary>
        /// How many times one corpse may be harvested, successes and failures alike.
        ///
        /// Counting attempts rather than successes is the whole defence against the roll being
        /// cosmetic: a 55% tool retried freely is a 100% tool, and the only thing standing
        /// between a player and every item a corpse can give is how many times they may ask.
        /// PM_HARVEST_FAIL_DEPLETED exists for exactly this, so the client already has a way to
        /// say it.
        /// </summary>
        public const int AttemptsPerCorpse = 3;

        /// <summary>
        /// How far a corpse may be from the harvester, in metres.
        ///
        /// The client will not let a player aim past the tool's range, but the entity id arrives
        /// over the wire and nothing else on this server checks a distance - so without this a
        /// client could harvest every corpse on the map from where it stood. The tool's own range
        /// is 80 on every template that has a row, which is the client's number for "can be
        /// pointed at"; this is deliberately tighter, because harvesting is done standing over
        /// the thing.
        /// </summary>
        public const float MaxRange = 20.0f;

        /// <summary>
        /// Slack on the range check, in metres. The position the server holds for a player lags
        /// their real one by up to a movement update, and refusing an honest harvest because of
        /// that is worse than allowing a metre of reach.
        /// </summary>
        public const float RangeTolerance = 5.0f;

        /// <summary>
        /// The most harvests one player may have queued at once. Each request queues a windup,
        /// and nothing else caps that list - a client that sends faster than the windup resolves
        /// could grow it without limit. Two is enough that a fast player is never refused for
        /// queueing while one is still running.
        /// </summary>
        public const int MaxQueuedPerPlayer = 2;

        /// <summary>
        /// The arg ids that tell the two harvests apart, from harvest.py's own SKILL_SALVAGE and
        /// SKILL_TISSUE_EXTRACTION. Both tools share action id 172 and the arg is what decides
        /// which one this is.
        ///
        /// The client's names call them skills and they are not: skilldata has no 168 or 169,
        /// none of the 48 harvest tool templates require a skill, and harvest.py checks none.
        /// They were briefly entries in SkillId here, which made the server refuse every harvest
        /// for a skill no character could ever hold.
        /// </summary>
        public const uint SalvageArg = 168;
        public const uint TissueExtractionArg = 169;
    }
}
