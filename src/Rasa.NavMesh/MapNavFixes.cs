using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.NavMesh
{
    /// <summary>
    /// Corrections the builder makes to one map's navmesh, every time it builds that map without a
    /// --profile: a finer grid, terrain left out over a tunnel mouth, off-mesh links. Each is where the
    /// ordinary build leaves parts of a map no path joins, which an escort, a guard or a creature
    /// sent after a player cannot cross. Checked against the rebuilt meshes by NavMeshJoinsTests.
    /// </summary>
    public sealed class MapNavFixes
    {
        public string Map { get; init; }
        public bool Fine { get; init; }
        public (Vector3 Min, Vector3 Max)[] TerrainCuts { get; init; } = Array.Empty<(Vector3, Vector3)>();
        public (Vector3 From, Vector3 To)[] OffMeshLinks { get; init; } = Array.Empty<(Vector3, Vector3)>();

        public static readonly MapNavFixes[] All =
        {
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

        public static MapNavFixes For(string map) =>
            All.FirstOrDefault(fix => string.Equals(fix.Map, map, StringComparison.OrdinalIgnoreCase));

        /// <summary>The settings for building this map: <paramref name="settings"/>, or the fine grid, with its cuts and links.</summary>
        public BuildSettings Apply(BuildSettings settings)
        {
            var applied = Fine ? BuildSettings.Fine(settings.Threads) : settings;
            applied.TerrainCuts = TerrainCuts.ToList();
            applied.OffMeshLinks = OffMeshLinks.ToList();
            return applied;
        }
    }
}
