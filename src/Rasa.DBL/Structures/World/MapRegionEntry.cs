using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// A volume on a map that puts whoever stands in it into one of the map's regions - the
    /// per-area ambient sound, music, sky, environment map, display name and cavern minimap the
    /// client keeps in its <c>.map</c> region table. The table names the regions; the volumes
    /// that decided which ones a player was in were server data and are gone, so these are
    /// authored: seeded from the client's region labels and cavern minimap rectangles, then
    /// adjusted in game. Several rows may point at the same region.
    /// </summary>
    [Table(TableName)]
    public class MapRegionEntry : IHasId, IHasPosition
    {
        public const string TableName = "map_region";

        /// <summary>A circle of <see cref="Radius"/> around the position, between MinY and MaxY.</summary>
        public const byte ShapeCircle = 1;

        /// <summary>A box of <see cref="HalfX"/> by <see cref="HalfZ"/> around the position, between MinY and MaxY.</summary>
        public const byte ShapeBox = 2;

        /// <summary>The volume applies wherever the player stands.</summary>
        public const byte UndergroundAny = 0;

        /// <summary>Only when the player is on navmesh flagged as under the terrain: a cavern region.</summary>
        public const byte UndergroundOnly = 1;

        /// <summary>Only when the player is not underground: a surface region a cave runs beneath.</summary>
        public const byte SurfaceOnly = 2;

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("map_context_id")]
        [Required]
        public uint MapContextId { get; set; }

        /// <summary>The region id in the map's region table (generated.client's <c>.map</c> regions; regions.csv).</summary>
        [Column("region_id")]
        [Required]
        public uint RegionId { get; set; }

        /// <summary><see cref="ShapeCircle"/> or <see cref="ShapeBox"/>.</summary>
        [Column("shape")]
        [Required]
        public byte Shape { get; set; }

        [Column("pos_x")]
        [Required]
        public double PosX { get; set; }

        /// <summary>Informational for the volume test (MinY/MaxY bound it); where a GM stood when placing it.</summary>
        [Column("pos_y")]
        [Required]
        public double PosY { get; set; }

        [Column("pos_z")]
        [Required]
        public double PosZ { get; set; }

        /// <summary>Circle: metres from the position, in the horizontal plane.</summary>
        [Column("radius")]
        [Required]
        public double Radius { get; set; }

        /// <summary>Box: half the width along x.</summary>
        [Column("half_x")]
        [Required]
        public double HalfX { get; set; }

        /// <summary>Box: half the depth along z.</summary>
        [Column("half_z")]
        [Required]
        public double HalfZ { get; set; }

        [Column("min_y")]
        [Required]
        public double MinY { get; set; }

        [Column("max_y")]
        [Required]
        public double MaxY { get; set; }

        /// <summary><see cref="UndergroundAny"/>, <see cref="UndergroundOnly"/> or <see cref="SurfaceOnly"/>.</summary>
        [Column("underground")]
        [Required]
        public byte Underground { get; set; }

        /// <summary>0 keeps the row but never applies it.</summary>
        [Column("enabled")]
        [Required]
        public byte Enabled { get; set; }

        [Column("comment", TypeName = "varchar(96)")]
        [Required]
        public string Comment { get; set; }

        public Vector3 Position => new((float)PosX, (float)PosY, (float)PosZ);

        public double Rotation => 0;
    }
}
