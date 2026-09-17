using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// One property of one action at one level: what the action does and how hard. The property
    /// id is an AbilityProperty (the client's generated.client.abilityproperty - DAMAGE_AMOUNT_MIN,
    /// HEAL_AMOUNT_MAX, DURATION, RADIUS_AROUND_TARGET, STUN_CHANCE, DRAIN_PER_TICK_ADRENALINE,
    /// ...) and the value is whatever unit that property uses. The client's
    /// generated.client.actiondata.abilityData, one row per (action, level, property).
    ///
    /// Damage, heal and power amounts are base values at level 1 and scale with the actor's level
    /// by the DAMAGE_SCALE_TYPE / CONSUMABLE_SCALE_TYPE property: 1 is linear, +33% per level; 2
    /// is exponential, doubling every 8 levels (shared/scaling.py).
    /// </summary>
    [Table(TableName)]
    public class ActionPropertyEntry : IHasId
    {
        public const string TableName = "action_property";

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

        /// <summary>An AbilityProperty value.</summary>
        [Column("property_id")]
        [Required]
        public uint PropertyId { get; set; }

        /// <summary>Signed: level differences and mortar angles go below zero.</summary>
        [Column("value")]
        [Required]
        public int Value { get; set; }
    }
}
