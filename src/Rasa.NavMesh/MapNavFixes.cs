using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.NavMesh
{
    /// <summary>
    /// Corrections the builder makes to one map's navmesh, every time it builds that map without a
    /// --profile: a finer grid over the whole map or over some of its tiles, terrain left out over a
    /// tunnel mouth, off-mesh links. Each is where the
    /// ordinary build leaves parts of a map no path joins, which an escort, a guard or a creature
    /// sent after a player cannot cross. Checked against the rebuilt meshes by NavMeshJoinsTests.
    /// </summary>
    public sealed class MapNavFixes
    {
        public string Map { get; init; }
        public bool Fine { get; init; }
        public (Vector3 Min, Vector3 Max)[] TerrainCuts { get; init; } = Array.Empty<(Vector3, Vector3)>();
        public (Vector3 From, Vector3 To)[] OffMeshLinks { get; init; } = Array.Empty<(Vector3, Vector3)>();

        /// <summary>
        /// Tiles (x, z on the 51.2 m grid) built on the fine grid in place of the ordinary ones; the rest of the map
        /// is the ordinary build, tile for tile. For seams the 0.4 m cells leave between floors that are one.
        /// </summary>
        public (int X, int Z)[] FineTiles { get; init; } = Array.Empty<(int, int)>();

        public static readonly MapNavFixes[] All =
        {
            // Brann Water Refinery (2093). The 0.4 m grid broke the refinery's corridors into some forty parts at
            // their ramps, stairs and jambs: the arrival, Melokan and Commander Killroy's operations room were on
            // three, and 10 of the 16 legs of the missions' route (arrival, operations room, Captain Jenks, the
            // hacking modules, Final Processing, the reservoir, maintenance, Scout Coen, the skybridge, the living
            // quarters, the Greenhouse, the north way out) had no path. The fine grid over the building's tiles
            // joins them but two: the first hacking module's walk, through a doorway at x = -40.7 that lies on the
            // tile edge z = -278.4, with the client's floor at 399.52 and full headroom; and the skybridge's west
            // end at x = -185, where the heightmap's cliff rises through the walkway and debris narrows it, the
            // floor flat at 399.5 along z = -241. A link joins each. The tiles round the building are the
            // ordinary build, tile for tile.
            new MapNavFixes
            {
                Map = "adv_arieki_torden_plains_brannwaterrefinery",
                FineTiles = Block(16, 24, 14, 22),
                OffMeshLinks = new[]
                {
                    (new Vector3(-41.8f, 399.6f, -278.6f), new Vector3(-40.0f, 399.6f, -278.2f)),
                    (new Vector3(-184.0f, 399.6f, -241.4f), new Vector3(-185.6f, 399.6f, -241.6f))
                }
            },
            // Caves of Donn (1506). The 0.4 m grid cut the caves into four parts at their narrows: the map link's
            // arrival and 5 pools on one, 17, 11 and 7 pools on the others. The fine grid over the caves joins all 40.
            new MapNavFixes
            {
                Map = "adv_foreas_concordia_wilderness_cavesofdonn02",
                FineTiles = Block(1, 16, 1, 9)
            },
            // Devil's Den (1394). The arrival stood on a 79 m² part of its own and the den's 52 pools on pieces of
            // 34 000, 33 000, 18 000 and 3 000 m²: 24 on the largest. The fine grid over the den puts the arrival
            // and 45 of them on one.
            new MapNavFixes
            {
                Map = "adv_foreas_concordia_palisades_devilsden",
                FineTiles = Block(10, 22, 5, 29)
            },
            // Crater Lake Research Facility (1721). Three pools on a 5 400 m² part the 0.4 m grid split off; on the
            // fine grid they are on the facility's own, with 21 others.
            new MapNavFixes
            {
                Map = "adv_foreas_concordia_wilderness_clrf",
                FineTiles = Block(3, 13, 5, 15)
            },
            // Purgas Station (1806): 5 of its 16 pools on the arrival's part, the others on parts of 20 000 and
            // 11 000 m² and smaller; 13 on one on the fine grid. The station covers the whole map.
            new MapNavFixes
            {
                Map = "adv_foreas_concordia_divide_purgasstation2",
                Fine = true
            },
            // Rivasa Atta Colony (2112): 6 of its pools on a 63 000 m² part of their own; one with the arrival's on
            // the fine grid (13 of 16). The colony's tunnels cover most of the map.
            new MapNavFixes
            {
                Map = "adv_arieki_ligo_thunderhead_rivasaattacolony",
                Fine = true
            },
            // Indra Caverns (2055): the arrival on a 320 m² part of its own and the caverns' pools on three; on the
            // fine grid the arrival and 6 of the 7 on-mesh pools are on one. The caverns cover most of the map.
            new MapNavFixes
            {
                Map = "adv_arieki_ligo_ashendesert_indracaverns",
                Fine = true
            },
            // Velon Hollow (2136). Escort seams the 0.4 m cells leave open: across x = 102.4 and x = 0 and on the
            // hospital's ramp (the first seven, which the velon-hollow-seams-v2 profile rebuilt over the shipped
            // file), and seam G5 at (361.8, 34.6, 52.6) on the edge z = 51.2, which cut the hospital off from the
            // control room: Holy Vengeance's followers (1977) stopped 100 m short at (360.8, 34.7, 51.2).
            new MapNavFixes
            {
                Map = "adv_foreas_howlingmaw_deathburrow",
                FineTiles = new[] { (23, 9), (23, 10), (21, 11), (22, 11), (19, 10), (20, 10), (20, 12), (27, 21) }
            },
            // Valverde Marshes (1454). Two narrow passages with the client's floor and full headroom the 0.4 m
            // cells close: from the zone into the crate area at (-346, 217.5, 28.6), 1.2 m wide, on the edge
            // z = 28.8; and from the crate area to the temple's upper floor at (-246, 228.1, 119.8).
            new MapNavFixes
            {
                Map = "adv_foreas_valverde_marshes",
                FineTiles = new[] { (16, 23), (18, 25) }
            },
            // Village Ruins (1743). The map link's arrival (-138.5, 63, -360) was on an 85 m² part, split at the
            // tile edge x = -128 though the floor runs on at 62.9 with no step; the Healing Shaman's hospital
            // (-117.8, 60.16, -273.4) on a 30 m² part in its hut. A player arrived and revived where no NPC could
            // path; arrival to hospital is now 113 m.
            new MapNavFixes
            {
                Map = "adv_foreas_valverde_marshes_villageruins",
                FineTiles = new[] { (5, 0), (5, 2) }
            },
            // Turpis Refinery (1429). The heightmap runs over the mouth of the cavern entrance
            // (terra_foreas_cavern_entrance_24m_v04 at (-256, 130, -198)): faces of 53-59 degrees, 0.3-1.4 m
            // over the tunnel floor at 124.8, which the 60 degree rule (TerrainMaxSlope) keeps. They cut the
            // entry pocket off from the west passage at (-274.8, 125.1, -200.4) / (-278.8, 125.2, -200.1).
            // Bravo's hard drive (64, 107.86, -96) is on a part the 0.4 m grid splits off at the tile edge
            // x = 51.2 (1.6 m of height between the two sides); the fine grid joins it.
            new MapNavFixes
            {
                Map = "adv_foreas_valverde_marshes_wetlandrefinery",
                Fine = true,
                TerrainCuts = new[] { (new Vector3(-280f, 124f, -206f), new Vector3(-276f, 129f, -198f)) }
            },
            // Bane Supply Depot (1451). The heightmap runs over the mouth of the entry chunnel
            // (arch_bane_industrial_chunnel_entrance_v01 at (-284, 148, 36)), 0.1-4 m over its floor at
            // 149.5 and only 16-53 degrees steep, so the slope rule keeps it. The map link's arrival and
            // the front guards were on a part of their own; nothing walked from the entry hall into the depot.
            new MapNavFixes
            {
                Map = "adv_foreas_valverde_marshes_banesupplydepot",
                TerrainCuts = new[] { (new Vector3(-276.5f, 149.55f, 29f), new Vector3(-272f, 154f, 43f)) }
            },
            // Retread Caves (1694). The 0.4 m grid leaves four parts: the first room and main passage; the
            // Retread Camp; the Western Grotto; the Mox Lair. The fine grid joins the first two through
            // (-38.6, 74.1, -104.4); two links join the others where the client's floor runs on with full
            // headroom: the ledge from the camp down to the grotto, and a 0.4 m step the mesh leaves open
            // between the camp's branch and the lair's side.
            new MapNavFixes
            {
                Map = "adv_foreas_valverde_pools_retread_caves",
                Fine = true,
                OffMeshLinks = new[]
                {
                    (new Vector3(7.4f, 51.65f, -42.6f), new Vector3(10.6f, 50.25f, -39.6f)),
                    (new Vector3(38.6f, 61.25f, -194.2f), new Vector3(39.0f, 60.85f, -194.2f))
                }
            }
        };

        /// <summary>Every tile from (x0, z0) to (x1, z1), both included: a building's whole footprint.</summary>
        private static (int X, int Z)[] Block(int x0, int x1, int z0, int z1) =>
            Enumerable.Range(x0, x1 - x0 + 1).SelectMany(x => Enumerable.Range(z0, z1 - z0 + 1).Select(z => (x, z))).ToArray();

        public static MapNavFixes For(string map) =>
            All.FirstOrDefault(fix => string.Equals(fix.Map, map, StringComparison.OrdinalIgnoreCase));

        /// <summary>The settings for building this map: <paramref name="settings"/>, or the fine grid, with its cuts and links.</summary>
        public BuildSettings Apply(BuildSettings settings)
        {
            var applied = Fine ? BuildSettings.Fine(settings.Threads) : settings;
            applied.TerrainCuts = TerrainCuts.ToList();
            applied.OffMeshLinks = OffMeshLinks.ToList();
            applied.FineTiles = FineTiles.ToList();
            return applied;
        }
    }
}
