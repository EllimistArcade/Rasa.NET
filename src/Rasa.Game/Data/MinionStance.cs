namespace Rasa.Data
{
    /// <summary>
    /// A minion's combat stance, set with <c>/cmd passive</c>, <c>/cmd defensive</c> or
    /// <c>/cmd aggressive</c>.
    ///
    /// The client's own Command System help defines all three:
    ///
    /// <list type="bullet">
    /// <item>Passive - will not respond to attacks and will not aggro. Setting it clears the
    /// minion's hate list. A passive minion still performs helpful actions on friends.</item>
    /// <item>Defensive - fights back when attacked, but does not go looking. The stance a
    /// minion enters the world in.</item>
    /// <item>Aggressive - aggroes hostiles that come into range.</item>
    /// </list>
    ///
    /// The stance never overrides a direct order: "the player can always command a subordinate to
    /// interact with a specific target, including attacking a target, no matter what combat
    /// stance the subordinate is in."
    ///
    /// The values are ours - the wire only ever carries the PlayerMessage that acknowledges a
    /// change (PmMinionPassiveSuccess and its two siblings), never the stance itself.
    /// </summary>
    public enum MinionStance : byte
    {
        Passive = 0,
        Defensive = 1,
        Aggressive = 2
    }
}
