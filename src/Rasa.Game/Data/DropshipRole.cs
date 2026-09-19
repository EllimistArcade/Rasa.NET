namespace Rasa.Data
{
    /// <summary>
    /// Which end of a flight a teleporter dropship is. A departure lands at the player, takes
    /// them aboard and leaves for the destination; an arrival lands at the destination and sets
    /// them down. The worker used to tell them apart by the client's connection state, which
    /// broke down as soon as a flight stayed on one map.
    /// </summary>
    public enum DropshipRole
    {
        Departure,
        Arrival
    }
}
