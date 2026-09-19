using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    [Table(TableName)]
    public class CreatureClassFlagEntry
    {
        public const string TableName = "creature_class_flag";

        /// <summary>
        /// The entity class the flag belongs to, not a creature row. Flags describe a species,
        /// so every creature spawned from a class shares them and a new spawn needs no data of
        /// its own.
        /// </summary>
        [Column("class_id")]
        [Required]
        public uint ClassId { get; set; }

        /// <summary>A value from generated/client/constant/creatureflag.</summary>
        [Column("flag_id")]
        [Required]
        public uint FlagId { get; set; }
    }
}
