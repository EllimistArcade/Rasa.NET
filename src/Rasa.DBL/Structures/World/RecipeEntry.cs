using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// A fabrication recipe: what a schematic item makes at a Kraftwerks station. The row's id is
    /// the schematic's item template id, as in the client's generated.shared.recipe
    /// (RecipeTemplate), which both sides read the same way. Inputs are in <see cref="RecipeInputEntry"/>.
    /// </summary>
    [Table(TableName)]
    public class RecipeEntry : IHasId
    {
        public const string TableName = "recipe";

        /// <summary>The schematic's item template id.</summary>
        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        /// <summary>Credits charged when the job is started (EnergyUnitCost; the window shows it as the credit requirement).</summary>
        [Column("energy_cost")]
        [Required]
        public uint EnergyCost { get; set; }

        /// <summary>How long the station works on it.</summary>
        [Column("kraftwerks_seconds")]
        [Required]
        public uint KraftwerksSeconds { get; set; }

        [Column("result_template_id")]
        [Required]
        public uint ResultTemplateId { get; set; }

        [Column("result_amount")]
        [Required]
        public uint ResultAmount { get; set; }

        [Column("min_level")]
        [Required]
        public uint MinLevel { get; set; }
    }
}
