using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// The action a usable item performs: a med pack is CONSUMABLE_MEDPACK at some level, a
    /// grenade is CONSUMABLE_GRENADE, a firework is CONSUMABLE_FIREWORK. Keyed by item template,
    /// so the level is the item's grade. The client's
    /// generated.client.actiondata.itemTemplateActions.
    /// </summary>
    [Table(TableName)]
    public class ItemTemplateActionEntry : IHasId
    {
        public const string TableName = "item_template_action";

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("item_template_id")]
        [Required]
        public uint ItemTemplateId { get; set; }

        [Column("action_id")]
        [Required]
        public uint ActionId { get; set; }

        [Column("level")]
        [Required]
        public uint Level { get; set; }
    }
}
