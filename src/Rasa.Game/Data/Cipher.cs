namespace Rasa.Data
{
    /// <summary>
    /// The rules a decode is held to.
    ///
    /// The cipher is the second tool that makes something out of nothing - harvesting makes an
    /// item, this one makes a locked thing open - and unlike a harvest what it opens does not go
    /// away afterwards. A lock that can be retried without limit is not a lock, whatever its
    /// decode chance says, so the same shape applies: a budget of attempts, spent whether the
    /// roll lands or not.
    ///
    /// What the client checks - cipher.py's CheckAction, and usable.py's CanCipher() behind it -
    /// is a courtesy to the player. The entity id arrives over the wire and the skill level the
    /// check reads is the client's own copy, so every one of these is enforced here as well.
    /// </summary>
    public static class Cipher
    {
        /// <summary>
        /// The arg id the cipher carries, from the one weapon class family that has it. It is
        /// also the id of the skill those tools require, which is a coincidence of numbering
        /// rather than a rule - the harvest args are not skill ids at all.
        /// </summary>
        public const uint CipherArg = 14;

        /// <summary>
        /// How many decodes one lock will take, successes and failures alike.
        ///
        /// A level 5 cipher decodes 55% of the time. Retried freely that is a lock that opens
        /// every time and simply takes longer, which makes the chance decoration and the tool
        /// levels pointless. Counting attempts is what makes a failure cost something.
        /// </summary>
        public const int AttemptsPerLock = 3;

        /// <summary>
        /// How far a lock may be from the player, in metres, and the slack allowed on top for the
        /// server's position lagging theirs.
        ///
        /// usableMaxRange in the client's own data carries exactly one entry - class 26232, 16m -
        /// so there is no per-class range to read and this is the one number. The tool's own
        /// range column says 80 on all ten cipher templates, but that is how far the client will
        /// let a player aim, not how close ciphering is done; harvesting draws the same
        /// distinction for the same reason.
        /// </summary>
        public const float MaxRange = 16.0f;
        public const float RangeTolerance = 5.0f;

        /// <summary>
        /// Used when the tool has no itemtemplate_weapon row to give a windup. All ten that do
        /// read 800, the same as every other tool. cipher.py sets loopWindup and windupNotifyUI,
        /// so the client shows a progress bar for the whole of it.
        /// </summary>
        public const long DefaultWindupMs = 800;
    }
}
