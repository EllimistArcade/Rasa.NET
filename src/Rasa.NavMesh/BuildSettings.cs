namespace Rasa.NavMesh
{
    /// <summary>
    /// Recast parameters, in metres and degrees unless noted. Defaults are for a human-sized agent on
    /// Tabula Rasa terrain: the creature capsules in the client's collision table are 0.25 - 0.8 m in
    /// radius, passes and doorways are 4 m and wider, and the hills are steep.
    /// </summary>
    public sealed class BuildSettings
    {
        public float CellSize = 0.4f;
        public float CellHeight = 0.2f;
        public float AgentHeight = 2.0f;
        public float AgentRadius = 0.6f;
        public float AgentMaxClimb = 0.9f;
        public float AgentMaxSlope = 50f;

        /// <summary>Tile edge in cells; 128 * 0.4 m = 51.2 m tiles, about 40 x 40 of them on a 2 km map.</summary>
        public int TileSize = 128;

        public int RegionMinSize = 8;
        public int RegionMergeSize = 20;
        public float EdgeMaxLength = 12f;
        public float EdgeMaxError = 1.3f;
        public int VertsPerPoly = 6;
        public float DetailSampleDistance = 6f;
        public float DetailSampleMaxError = 1f;

        /// <summary>Heightmap samples per terrain quad edge: 2 = a 2 m grid, four times fewer triangles than the raw 1 m data.</summary>
        public int TerrainStep = 2;

        /// <summary>Terrain steeper than this is not fed to Recast at all; see TerrainHeightmap.AppendTriangles. Above the agent slope so it never removes walkable ground.</summary>
        public float TerrainMaxSlope = 60f;

        public int Threads = System.Environment.ProcessorCount;
    }
}
