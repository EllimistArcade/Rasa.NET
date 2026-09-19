using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// A crafting station (the client calls the object a Kraftwerks; its one entity class is
    /// UsableCraftingHumStationV01, 9595). Using one opens the crafting window on the client;
    /// the station is the entity every crafting request names. Seeded from the client's
    /// CRAFTING_STATION map markers, so the positions are where the live game drew them and the
    /// rotations are a GM's to fix with .kraftwerks.
    /// </summary>
    [Table(TableName)]
    public class KraftwerksEntry : IHasId, IHasPosition
    {
        public const string TableName = "kraftwerks";

        public const uint DefaultClassId = 9595;

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("class_id")]
        [Required]
        public uint ClassId { get; set; }

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

        [Column("rotation")]
        [Required]
        public double Rotation { get; set; }

        [Column("comment", TypeName = "varchar(64)")]
        [Required]
        public string Comment { get; set; }

        public Vector3 Position => new((float)PosX, (float)PosY, (float)PosZ);
    }
}
