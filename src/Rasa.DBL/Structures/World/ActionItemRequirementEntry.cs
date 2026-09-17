using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// An item the performer must carry to use one action at one level - grenades for the
    /// grenade abilities, mines for crab mines - by item class and quantity. The client's
    /// generated.client.actiondata.itemReqs, checked client-side in
    /// BaseActorAbility.CheckConsumables against the personal inventory.
    /// </summary>
    [Table(TableName)]
    public class ActionItemRequirementEntry : IHasId
    {
        public const string TableName = "action_item_requirement";

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("action_id")]
        [Required]
        public uint ActionId { get; set; }

        [Column("level")]
        [Required]
        public uint Level { get; set; }

        /// <summary>An entity class id; every item template of that class counts.</summary>
        [Column("item_class_id")]
        [Required]
        public uint ItemClassId { get; set; }

        [Column("quantity")]
        [Required]
        public uint Quantity { get; set; }
    }
}
