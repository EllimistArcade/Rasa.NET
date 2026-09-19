using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// A place on one map that leads to a place on another: the passes between adjacent zones
    /// (Wilderness/Divide), the doors into instance maps, and the exits back out. The original
    /// server watched these volumes and Wonkavated whoever walked in; the client only draws them
    /// on the map screen. A player inside <see cref="Radius"/> of the trigger position is sent to
    /// the arrival position on the destination map.
    /// </summary>
    [Table(TableName)]
    public class MapLinkEntry : IHasId, IHasPosition
    {
        public const string TableName = "map_link";

        public const byte KindBorder = 0;
        public const byte KindInstance = 1;

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        /// <summary>The map the trigger is on.</summary>
        [Column("map_context_id")]
        [Required]
        public uint MapContextId { get; set; }

        [Column("pos_x")]
        [Required]
        public double PosX { get; set; }

        [Column("pos_y")]
        [Required]
        public double PosY { get; set; }

        [Column("pos_z")]
        [Required]
        public double PosZ { get; set; }

        /// <summary>Metres from the trigger position within which the link fires.</summary>
        [Column("radius")]
        [Required]
        public double Radius { get; set; }

        [Column("dest_map_context_id")]
        [Required]
        public uint DestMapContextId { get; set; }

        [Column("dest_pos_x")]
        [Required]
        public double DestPosX { get; set; }

        [Column("dest_pos_y")]
        [Required]
        public double DestPosY { get; set; }

        [Column("dest_pos_z")]
        [Required]
        public double DestPosZ { get; set; }

        /// <summary>Yaw the player arrives with, in the client's convention (Movement.ViewDirection.X).</summary>
        [Column("dest_rotation")]
        [Required]
        public double DestRotation { get; set; }

        /// <summary><see cref="KindBorder"/> between two zones or hub maps, <see cref="KindInstance"/> when either end is an instance map.</summary>
        [Column("kind")]
        [Required]
        public byte Kind { get; set; }

        /// <summary>0 disables the link without deleting it: a pass that is not ready yet, or a door to a map with nothing in it.</summary>
        [Column("enabled")]
        [Required]
        public byte Enabled { get; set; }

        [Column("comment", TypeName = "varchar(64)")]
        [Required]
        public string Comment { get; set; }

        public Vector3 Position => new((float)PosX, (float)PosY, (float)PosZ);

        public double Rotation => 0;

        public Vector3 DestPosition => new((float)DestPosX, (float)DestPosY, (float)DestPosZ);
    }
}
