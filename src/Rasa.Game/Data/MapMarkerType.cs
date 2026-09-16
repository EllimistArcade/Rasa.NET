namespace Rasa.Data
{
    /// <summary>
    /// The map screen's marker kinds, from the client's own <c>generated.client.uimapmarker</c>.
    ///
    /// Only the first four appear in <c>factionedmarkers</c> and so only those have a state the
    /// server sends; the rest are drawn from the static tables or from live entities - region and
    /// point-of-interest labels, the other teleporter kinds, party and team members, vendors and
    /// footlockers - and the client colours them itself.
    /// </summary>
    public static class MapMarkerType
    {
        public const uint ControlPoint = 1;
        public const uint WaypointTeleporter = 2;
        public const uint Hospital = 3;
        public const uint CraftingStation = 4;
        public const uint SafeZone = 19;
    }

    /// <summary>
    /// <c>generated.client.controlpointownershiptype</c>, for when 7.4 fills control points in.
    ///
    /// Worth reading before that happens: under FACTION_OWNED the client tests the second field
    /// with <c>ownerId is True</c> and <c>ownerId is False</c> - identity, not truth - so it has
    /// to arrive as a real bool. A Zero struct unmarshals to something that is falsy but is not
    /// False, and the tooltip would silently come out blank.
    /// </summary>
    public static class ControlPointOwnershipType
    {
        public const uint ClanOwned = 1;
        public const uint TeamOwned = 3;
        public const uint FactionOwned = 6;
    }
}
