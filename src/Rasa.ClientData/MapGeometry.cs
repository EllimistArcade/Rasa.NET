using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Rasa.ClientData
{
    /// <summary>
    /// Everything a creature can stand on or bump into on one map, as a triangle soup in world
    /// space: the terrain heightmap plus the collision mesh of every placed entity that has one,
    /// transformed by the entity's position, orientation and scale from the <c>.map</c>.
    /// </summary>
    public sealed class MapGeometry
    {
        public readonly List<float> Vertices = new List<float>();
        public readonly List<int> Triangles = new List<int>();

        public Vector3 BoundsMin = new Vector3(float.MaxValue);
        public Vector3 BoundsMax = new Vector3(float.MinValue);

        public int TerrainTriangles { get; private set; }
        public int EntityTriangles { get; private set; }
        public int EntitiesWithCollision { get; private set; }
        public int EntitiesWithoutMesh { get; private set; }
        public int EntitiesWithoutCollision { get; private set; }

        public int TriangleCount => Triangles.Count / 3;

        /// <summary>The map's heightmap, or null for a map without a terrain archive (indoor instances).</summary>
        public TerrainHeightmap Terrain { get; private set; }

        /// <summary>
        /// Loads a map's geometry from the client folder.
        /// </summary>
        /// <param name="mapDirectory">data/maps/&lt;name&gt;</param>
        /// <param name="meshes">the mesh library, shared across maps</param>
        /// <param name="terrainStep">heightmap samples per terrain quad edge (1 = every metre)</param>
        /// <param name="terrainMaxSlope">terrain triangles steeper than this many degrees are dropped (see <see cref="TerrainHeightmap.AppendTriangles"/>)</param>
        /// <param name="skip">classes whose geometry must not go in (null for none)</param>
        public static MapGeometry Load(string mapDirectory, MeshLibrary meshes, int terrainStep, float terrainMaxSlope = 90f, Func<int, string, bool> skip = null)
        {
            var name = Path.GetFileName(mapDirectory);
            var mapPath = Path.Combine(mapDirectory, name + ".map");
            var map = MapFile.Load(mapPath);
            var geometry = new MapGeometry();

            var terrainPath = Directory.EnumerateFiles(mapDirectory, "*_terrain.glm").FirstOrDefault();

            if (terrainPath != null)
            {
                var terrain = new TerrainHeightmap(terrainPath);
                var before = geometry.Triangles.Count;
                terrain.AppendTriangles(terrainStep, geometry.Vertices, geometry.Triangles, terrainMaxSlope);
                geometry.TerrainTriangles = (geometry.Triangles.Count - before) / 3;
                geometry.Terrain = terrain;
            }

            foreach (var entity in map.Entities)
            {
                if (!meshes.TryGetMeshName(entity.ClassId, out var meshName))
                {
                    geometry.EntitiesWithoutMesh++;
                    continue;
                }

                if (skip != null && skip(entity.ClassId, meshName))
                    continue;

                var mesh = meshes.Get(meshName);

                if (mesh == null)
                {
                    geometry.EntitiesWithoutMesh++;
                    continue;
                }

                if (!mesh.HasCollision)
                {
                    geometry.EntitiesWithoutCollision++;
                    continue;
                }

                geometry.Append(mesh, entity);
                geometry.EntitiesWithCollision++;
            }

            geometry.EntityTriangles = geometry.TriangleCount - geometry.TerrainTriangles;
            geometry.ComputeBounds();

            return geometry;
        }

        private void Append(GeoMesh mesh, MapFile.Entity entity)
        {
            var baseIndex = Vertices.Count / 3;
            var scale = entity.Scale > 0 ? entity.Scale : 1f;

            foreach (var v in mesh.Vertices)
            {
                var p = Vector3.Transform(v * scale, entity.Rotation) + entity.Position;
                Vertices.Add(p.X);
                Vertices.Add(p.Y);
                Vertices.Add(p.Z);
            }

            foreach (var i in mesh.Triangles)
                Triangles.Add(baseIndex + i);
        }

        private void ComputeBounds()
        {
            for (var i = 0; i < Vertices.Count; i += 3)
            {
                var p = new Vector3(Vertices[i], Vertices[i + 1], Vertices[i + 2]);
                BoundsMin = Vector3.Min(BoundsMin, p);
                BoundsMax = Vector3.Max(BoundsMax, p);
            }
        }

        /// <summary>Wavefront OBJ, for looking at the input in a mesh viewer.</summary>
        public void WriteObj(string path)
        {
            using var w = new StreamWriter(path);
            var ic = System.Globalization.CultureInfo.InvariantCulture;

            for (var i = 0; i < Vertices.Count; i += 3)
                w.WriteLine($"v {Vertices[i].ToString("0.###", ic)} {Vertices[i + 1].ToString("0.###", ic)} {Vertices[i + 2].ToString("0.###", ic)}");

            for (var i = 0; i < Triangles.Count; i += 3)
                w.WriteLine($"f {Triangles[i] + 1} {Triangles[i + 1] + 1} {Triangles[i + 2] + 1}");
        }
    }
}
