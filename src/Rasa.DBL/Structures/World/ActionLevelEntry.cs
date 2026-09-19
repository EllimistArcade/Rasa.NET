using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// The timing and reach of one action at one level (pump level, for abilities): how long it
    /// winds up, how long the recovery animation runs, how far it reaches and how long until it
    /// can be used again. The client's generated.client.actiondata.actionArguments, whose eight
    /// fields client/actions/__init__.py reads into ActorActionInfo in this order.
    /// </summary>
    [Table(TableName)]
    public class ActionLevelEntry : IHasId
    {
        public const string TableName = "action_level";

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("action_id")]
        [Required]
        public uint ActionId { get; set; }

        /// <summary>The action arg id: the pump level for an ability, 1 for most other actions.</summary>
        [Column("level")]
        [Required]
        public uint Level { get; set; }

        /// <summary>windupDelayMs: the delay between the request and the action landing.</summary>
        [Column("windup_ms")]
        [Required]
        public int WindupMs { get; set; }

        /// <summary>windupAnimationFamilyId; null when the action has no windup animation.</summary>
        [Column("windup_anim_family_id")]
        public uint? WindupAnimFamilyId { get; set; }

        /// <summary>recoveryDelayMs: how long the recovery animation runs. -1 means the animation's own length.</summary>
        [Column("recovery_ms")]
        [Required]
        public int RecoveryMs { get; set; }

        /// <summary>recoveryAnimationFamilyId; null when the action has no recovery animation.</summary>
        [Column("recovery_anim_family_id")]
        public uint? RecoveryAnimFamilyId { get; set; }

        /// <summary>maxRange, in metres. 0 for self-targeted and area-around-source actions.</summary>
        [Column("max_range")]
        [Required]
        public int MaxRange { get; set; }

        /// <summary>reuseTimeMs: the cooldown. 0 for none.</summary>
        [Column("reuse_ms")]
        [Required]
        public int ReuseMs { get; set; }

        /// <summary>preload: the client loads the action's assets ahead of time.</summary>
        [Column("preload")]
        [Required]
        public byte Preload { get; set; }

        /// <summary>
        /// startReuseTimerOnPerform: the client starts the cooldown itself when the action is
        /// performed. When clear, the server says when with ActionReuseTimerRestarted.
        /// </summary>
        [Column("start_reuse_on_perform")]
        [Required]
        public byte StartReuseOnPerform { get; set; }
    }
}
