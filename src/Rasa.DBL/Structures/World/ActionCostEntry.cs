using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// What one action at one level costs to perform: an attribute (5 chi/adrenaline, 6 power -
    /// the Attributes enum) and the amount. An action can cost more than one attribute. The
    /// client's generated.client.actiondata.actionAttributeCost; the client checks it before
    /// asking (BaseActorAbility.CheckConsumables) and the server takes it when the action is
    /// performed. Scaled by the CONSUMABLE_SCALE_TYPE property where one is set.
    /// </summary>
    [Table(TableName)]
    public class ActionCostEntry : IHasId
    {
        public const string TableName = "action_cost";

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

        /// <summary>An Attributes value: 5 is chi (adrenaline), 6 is power.</summary>
        [Column("attribute_id")]
        [Required]
        public uint AttributeId { get; set; }

        [Column("cost")]
        [Required]
        public int Cost { get; set; }
    }
}
