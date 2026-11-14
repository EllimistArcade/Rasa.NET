using System;
using System.Collections.Generic;
using System.Linq;
using DotRecast.Detour;

namespace Rasa.NavMesh
{
    /// <summary>
    /// Puts tiles built on the fine grid in place of the ordinary build's (MapNavFixes.FineTiles): every other
    /// tile, and its tile reference, is kept as the ordinary build made it.
    /// </summary>
    public static class NavMeshTiles
    {
        public static DtNavMesh Merge(DtNavMesh ordinary, DtNavMesh fine, IReadOnlyCollection<(int X, int Z)> tiles)
        {
            var original = ordinary.GetParams();
            var rebuilt = fine.GetParams();
            if (original.orig != rebuilt.orig || Math.Abs(original.tileWidth - rebuilt.tileWidth) > .001f ||
                Math.Abs(original.tileHeight - rebuilt.tileHeight) > .001f || original.maxTiles != rebuilt.maxTiles ||
                original.maxPolys != rebuilt.maxPolys)
                throw new ArgumentException("Fine tiles need the ordinary build's tile grid and origin.");

            var wanted = tiles.ToHashSet();
            var replacements = Enumerable.Range(0, fine.GetMaxTiles()).Select(fine.GetTile)
                .Where(t => t?.data?.header != null && wanted.Contains((t.data.header.x, t.data.header.y)))
                .ToDictionary(t => (t.data.header.x, t.data.header.y, t.data.header.layer), t => t.data);

            foreach (var data in replacements.Values)
                if (Math.Abs(data.header.walkableHeight - 2f) > .001f || Math.Abs(data.header.walkableRadius - .6f) > .001f ||
                    Math.Abs(data.header.walkableClimb - .9f) > .001f)
                    throw new InvalidOperationException("Fine tiles must keep the physical agent: 2 m high, 0.6 m wide, 0.9 m climb.");

            var result = new DtNavMesh();
            result.Init(original, 6);
            var placed = new HashSet<(int, int, int)>();

            for (var i = 0; i < ordinary.GetMaxTiles(); i++)
            {
                var tile = ordinary.GetTile(i);
                if (tile?.data?.header == null)
                    continue;

                var header = tile.data.header;
                var data = tile.data;

                if (wanted.Contains((header.x, header.y)))
                {
                    // A fine tile with no walkable surface left in it goes; its neighbours have nothing there to join.
                    if (!replacements.TryGetValue((header.x, header.y, header.layer), out data))
                        continue;
                    placed.Add((header.x, header.y, header.layer));
                }

                if (result.AddTile(data, 0, ordinary.GetTileRef(tile), out _).Failed())
                    throw new InvalidOperationException($"Could not keep tile {header.x},{header.y},{header.layer}.");
            }

            // A fine tile where the ordinary build had no walkable surface.
            foreach (var (key, data) in replacements.Where(r => !placed.Contains(r.Key)))
                if (result.AddTile(data, 0, 0, out _).Failed())
                    throw new InvalidOperationException($"Could not add fine tile {key.Item1},{key.Item2},{key.Item3}.");

            return result;
        }
    }
}
