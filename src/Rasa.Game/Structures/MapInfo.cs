namespace Rasa.Structures
{
    using Data;
    using World;
    public class MapInfo
    {
        public uint MapContextId { get; set; }
        public string MapName { get; set; }
        public uint MapVersion { get; set; }
        public uint BaseRegionId { get; set; }

        /// <summary>
        /// The planet the map is on, read off its name: every map of the shipped world is
        /// adv_foreas_* or adv_arieki_*, which is also how the client's
        /// gamecontextlocation table has them. The arena, the boot camp, character selection and
        /// the test maps are Unknown, and Unknown is its own planet as far as the dropship
        /// network is concerned - no pad on such a map reaches another map, and none reaches it.
        /// </summary>
        public Planet Planet => PlanetOf(MapName);

        public MapInfo(uint mapContextId, string mapName, uint mapVersion, uint baseRegionId)
        {
            MapContextId = mapContextId;
            MapName = mapName;
            MapVersion = mapVersion;
            BaseRegionId = baseRegionId;
        }

        public MapInfo(MapInfoEntry map)
        {
            MapContextId = map.Id;
            MapName = map.MapName;
            MapVersion = map.MapVersion;
            BaseRegionId = map.BaseRegion;
        }

        public static Planet PlanetOf(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return Planet.Unknown;

            var name = mapName.ToLowerInvariant();

            if (name.Contains("foreas"))
                return Planet.Foreas;

            if (name.Contains("arieki"))
                return Planet.Arieki;

            return Planet.Unknown;
        }
    }
}
