using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// One ingredient of a recipe: any items of the class, to the quantity, taken from the
    /// player's personal inventory when the job starts (RecipeTemplateInputClass in the client).
    /// </summary>
    [Table(TableName)]
    public class RecipeInputEntry : IHasId
    {
        public const string TableName = "recipe_input";

        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("recipe_id")]
        [Required]
        public uint RecipeId { get; set; }

        /// <summary>An entity class id; every item template of that class counts.</summary>
        [Column("input_class_id")]
        [Required]
        public uint InputClassId { get; set; }

        [Column("quantity")]
        [Required]
        public uint Quantity { get; set; }
    }
}
