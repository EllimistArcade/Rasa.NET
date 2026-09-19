using System;
using System.Numerics;

namespace Rasa.Structures
{
    using World;

    public enum MapRegionShape : byte
    {
        Circle = MapRegionEntry.ShapeCircle,
        Box = MapRegionEntry.ShapeBox
    }

    /// <summary>Where the player has to be standing for the volume to count.</summary>
    public enum MapRegionUnderground : byte
    {
        Any = MapRegionEntry.UndergroundAny,
        UndergroundOnly = MapRegionEntry.UndergroundOnly,
        SurfaceOnly = MapRegionEntry.SurfaceOnly
    }

    /// <summary>
    /// The live form of a <see cref="MapRegionEntry"/>: a volume on a map and the region id the
    /// client is told about while a player stands in it. See RegionManager.
    /// </summary>
    public class MapRegion
    {
        public uint Id { get; set; }
        public uint MapContextId { get; set; }
        public uint RegionId { get; set; }
        public MapRegionShape Shape { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; }
        public float HalfX { get; set; }
        public float HalfZ { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public MapRegionUnderground Underground { get; set; }
        public bool Enabled { get; set; }
        public string Comment { get; set; }

        /// <summary>Whether the volume covers the position, for a player who is or is not underground.</summary>
        public bool Contains(Vector3 position, bool underground)
        {
            if (Underground == MapRegionUnderground.UndergroundOnly && !underground)
                return false;

            if (Underground == MapRegionUnderground.SurfaceOnly && underground)
                return false;

            if (position.Y < MinY || position.Y > MaxY)
                return false;

            var dx = position.X - Position.X;
            var dz = position.Z - Position.Z;

            if (Shape == MapRegionShape.Box)
                return Math.Abs(dx) <= HalfX && Math.Abs(dz) <= HalfZ;

            return dx * dx + dz * dz <= Radius * Radius;
        }

        /// <summary>Horizontal distance from the position to the volume's edge; 0 inside it.</summary>
        public float Distance(Vector3 position)
        {
            var dx = Math.Abs(position.X - Position.X);
            var dz = Math.Abs(position.Z - Position.Z);

            if (Shape == MapRegionShape.Box)
            {
                var ox = Math.Max(0, dx - HalfX);
                var oz = Math.Max(0, dz - HalfZ);

                return MathF.Sqrt(ox * ox + oz * oz);
            }

            return Math.Max(0, MathF.Sqrt(dx * dx + dz * dz) - Radius);
        }

        public string Describe()
        {
            var shape = Shape == MapRegionShape.Box
                ? $"box {HalfX * 2:0}x{HalfZ * 2:0} m"
                : $"circle r {Radius:0} m";
            var height = MinY <= -1000 && MaxY >= 1000 ? "" : $", y {MinY:0}..{MaxY:0}";
            var where = Underground switch
            {
                MapRegionUnderground.UndergroundOnly => ", underground only",
                MapRegionUnderground.SurfaceOnly => ", surface only",
                _ => ""
            };
            var enabled = Enabled ? "" : ", DISABLED";

            return $"#{Id} region {RegionId}: {shape} at ({Position.X:0}, {Position.Y:0}, {Position.Z:0}){height}{where}{enabled} - {Comment}";
        }

        public static MapRegion FromEntry(MapRegionEntry entry)
        {
            return new MapRegion
            {
                Id = entry.Id,
                MapContextId = entry.MapContextId,
                RegionId = entry.RegionId,
                Shape = entry.Shape == MapRegionEntry.ShapeBox ? MapRegionShape.Box : MapRegionShape.Circle,
                Position = entry.Position,
                Radius = (float)entry.Radius,
                HalfX = (float)entry.HalfX,
                HalfZ = (float)entry.HalfZ,
                MinY = (float)entry.MinY,
                MaxY = (float)entry.MaxY,
                Underground = entry.Underground switch
                {
                    MapRegionEntry.UndergroundOnly => MapRegionUnderground.UndergroundOnly,
                    MapRegionEntry.SurfaceOnly => MapRegionUnderground.SurfaceOnly,
                    _ => MapRegionUnderground.Any
                },
                Enabled = entry.Enabled != 0,
                Comment = entry.Comment ?? string.Empty
            };
        }

        public MapRegionEntry ToEntry()
        {
            return new MapRegionEntry
            {
                Id = Id,
                MapContextId = MapContextId,
                RegionId = RegionId,
                Shape = (byte)Shape,
                PosX = Position.X,
                PosY = Position.Y,
                PosZ = Position.Z,
                Radius = Radius,
                HalfX = HalfX,
                HalfZ = HalfZ,
                MinY = MinY,
                MaxY = MaxY,
                Underground = (byte)Underground,
                Enabled = Enabled ? (byte)1 : (byte)0,
                Comment = Comment ?? string.Empty
            };
        }
    }
}
