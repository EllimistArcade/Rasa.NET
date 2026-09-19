using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Rasa.ClientData
{
    /// <summary>
    /// The heightmap in a map's <c>t&lt;templateId hex&gt;_terrain.glm</c>.
    ///
    /// The <c>.thd</c> chunk (60 bytes) gives the world bounds (min xyz, max xyz), the sample spacing,
    /// the samples per tile edge (128 x 128) and the tile grid (columns x rows). Tile <c>c + columns * r</c>
    /// is the chunk <c>t&lt;id&gt;_&lt;index hex&gt;_0.tpc</c>: a 4-byte header then 128 * 128 samples of 12
    /// bytes, row-major from the map's min corner, x fastest. A sample is a big-endian u16 height scaled
    /// to <c>[0, maxY]</c>, four (texture layer, weight) byte pairs and two unused bytes. Tiles are 128
    /// samples * 1 m wide; the grid can extend past the stated bounds.
    /// </summary>
    public sealed class TerrainHeightmap
    {
        public const int SamplesPerTile = 128;
        public const int SampleStride = 12;
        public const int TileHeader = 4;

        public Vector3 BoundsMin { get; }
        public Vector3 BoundsMax { get; }
        public float Spacing { get; }
        public int Columns { get; }
        public int Rows { get; }

        /// <summary>Heights in metres per tile index; null where the archive has no chunk.</summary>
        private readonly float[][] _tiles;

        public float TileSize => SamplesPerTile * Spacing;

        public TerrainHeightmap(string glmPath)
        {
            using var archive = new GlmArchive(glmPath);

            var thdName = archive.Entries.Keys.FirstOrDefault(n => n.EndsWith(".thd", StringComparison.OrdinalIgnoreCase));

            if (thdName == null)
                throw new InvalidDataException($"{glmPath}: no .thd header chunk");

            var thd = archive.Read(thdName);

            BoundsMin = new Vector3(BitConverter.ToSingle(thd, 16), BitConverter.ToSingle(thd, 20), BitConverter.ToSingle(thd, 24));
            BoundsMax = new Vector3(BitConverter.ToSingle(thd, 28), BitConverter.ToSingle(thd, 32), BitConverter.ToSingle(thd, 36));
            Spacing = BitConverter.ToSingle(thd, 40);

            var samplesX = BitConverter.ToInt32(thd, 44);
            var samplesZ = BitConverter.ToInt32(thd, 48);
            Columns = BitConverter.ToInt32(thd, 52);
            Rows = BitConverter.ToInt32(thd, 56);

            if (samplesX != SamplesPerTile || samplesZ != SamplesPerTile)
                throw new InvalidDataException($"{glmPath}: {samplesX}x{samplesZ} samples per tile, expected 128x128");

            var prefix = thdName.Substring(0, thdName.Length - ".thd".Length);   // t00000562_0
            prefix = prefix.Substring(0, prefix.LastIndexOf('_') + 1);            // t00000562_

            _tiles = new float[Columns * Rows][];
            var scale = BoundsMax.Y / 65535f;

            foreach (var entry in archive.Entries.Values)
            {
                if (!entry.Name.EndsWith("_0.tpc", StringComparison.OrdinalIgnoreCase) || !entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var hex = entry.Name.Substring(prefix.Length, entry.Name.Length - prefix.Length - "_0.tpc".Length);

                if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var index) || index < 0 || index >= _tiles.Length)
                    continue;

                var data = archive.Read(entry);

                if (data.Length < TileHeader + SamplesPerTile * SamplesPerTile * SampleStride)
                    continue;

                var heights = new float[SamplesPerTile * SamplesPerTile];

                for (var i = 0; i < heights.Length; i++)
                    heights[i] = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(data, TileHeader + i * SampleStride, 2)) * scale;

                _tiles[index] = heights;
            }
        }

        public int TileCount => _tiles.Count(t => t != null);

        /// <summary>Nearest-sample height, or null outside the tiles the archive has.</summary>
        public float? Height(float x, float z)
        {
            var ox = x - BoundsMin.X;
            var oz = z - BoundsMin.Z;

            if (ox < 0 || oz < 0)
                return null;

            var tx = (int)(ox / TileSize);
            var tz = (int)(oz / TileSize);

            if (tx >= Columns || tz >= Rows)
                return null;

            var tile = _tiles[tz * Columns + tx];

            if (tile == null)
                return null;

            var c = Math.Min((int)((ox - tx * TileSize) / Spacing), SamplesPerTile - 1);
            var r = Math.Min((int)((oz - tz * TileSize) / Spacing), SamplesPerTile - 1);

            return tile[r * SamplesPerTile + c];
        }

        /// <summary>
        /// The terrain as triangles, one quad per <paramref name="step"/> samples, clipped to the
        /// stated bounds. Vertices are appended to <paramref name="vertices"/> (x, y, z triples) and
        /// triangle indices to <paramref name="triangles"/>.
        ///
        /// Triangles steeper than <paramref name="maxSlopeDegrees"/> are left out. The client treats
        /// the heightmap as a surface to stand on, not a solid: cave mouths are cut into cliffs, and
        /// the cliff face runs straight through the tunnel mesh behind it. A navmesh builder that saw
        /// that face would wall the tunnel off. Nobody can walk on a cliff face anyway, so dropping
        /// it costs nothing - the walkable terrain on either side still ends at a ledge.
        /// </summary>
        public void AppendTriangles(int step, List<float> vertices, List<int> triangles, float maxSlopeDegrees = 90f)
        {
            if (step < 1)
                step = 1;

            var minNormalY = (float)Math.Cos(maxSlopeDegrees * Math.PI / 180.0);

            // Vertex grid over the whole tile grid, then clipped by bounds.
            var gridW = Columns * SamplesPerTile;
            var gridH = Rows * SamplesPerTile;
            var nx = gridW / step + 1;
            var nz = gridH / step + 1;
            var index = new int[nx * nz];

            for (var j = 0; j < nz; j++)
            {
                var sz = Math.Min(j * step, gridH - 1);
                var z = BoundsMin.Z + sz * Spacing;

                for (var i = 0; i < nx; i++)
                {
                    var sx = Math.Min(i * step, gridW - 1);
                    var x = BoundsMin.X + sx * Spacing;

                    if (x > BoundsMax.X + Spacing || z > BoundsMax.Z + Spacing)
                    {
                        index[j * nx + i] = -1;
                        continue;
                    }

                    var tile = _tiles[(sz / SamplesPerTile) * Columns + sx / SamplesPerTile];

                    if (tile == null)
                    {
                        index[j * nx + i] = -1;
                        continue;
                    }

                    index[j * nx + i] = vertices.Count / 3;
                    vertices.Add(x);
                    vertices.Add(tile[(sz % SamplesPerTile) * SamplesPerTile + sx % SamplesPerTile]);
                    vertices.Add(z);
                }
            }

            void AddIfWalkable(int i0, int i1, int i2)
            {
                var e1x = vertices[i1 * 3] - vertices[i0 * 3];
                var e1y = vertices[i1 * 3 + 1] - vertices[i0 * 3 + 1];
                var e1z = vertices[i1 * 3 + 2] - vertices[i0 * 3 + 2];
                var e2x = vertices[i2 * 3] - vertices[i0 * 3];
                var e2y = vertices[i2 * 3 + 1] - vertices[i0 * 3 + 1];
                var e2z = vertices[i2 * 3 + 2] - vertices[i0 * 3 + 2];
                var nx = e1y * e2z - e1z * e2y;
                var ny = e1z * e2x - e1x * e2z;
                var nz2 = e1x * e2y - e1y * e2x;
                var length = (float)Math.Sqrt(nx * nx + ny * ny + nz2 * nz2);

                if (length > 0 && ny / length < minNormalY)
                    return;

                triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            }

            for (var j = 0; j + 1 < nz; j++)
            {
                for (var i = 0; i + 1 < nx; i++)
                {
                    var a = index[j * nx + i];
                    var b = index[j * nx + i + 1];
                    var c = index[(j + 1) * nx + i];
                    var d = index[(j + 1) * nx + i + 1];

                    if (a < 0 || b < 0 || c < 0 || d < 0)
                        continue;

                    // Counter-clockwise seen from above (+y), which is what Recast treats as up-facing.
                    AddIfWalkable(a, c, b);
                    AddIfWalkable(b, c, d);
                }
            }
        }
    }
}
