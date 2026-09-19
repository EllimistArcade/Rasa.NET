namespace Rasa.Navigation
{
    /// <summary>
    /// The per-polygon flags Rasa.NavMesh writes into a <c>.nav</c> and the server reads back.
    /// The query filter includes every flag, so none of them restricts pathing; they carry
    /// information about the surface.
    /// </summary>
    public static class NavMeshFlags
    {
        /// <summary>Set on every polygon.</summary>
        public const int Walk = 0x01;

        /// <summary>
        /// The polygon lies under the terrain heightmap: a cave, tunnel or cellar floor that the
        /// surface runs over. Whether a player is "underground" for the map's regions is read
        /// from the polygon under their feet. Never set on a map with no terrain.
        /// </summary>
        public const int Underground = 0x02;

        /// <summary>How far below the terrain a polygon's centre has to be to count as underground.</summary>
        public const float UndergroundDepth = 2.0f;
    }
}
