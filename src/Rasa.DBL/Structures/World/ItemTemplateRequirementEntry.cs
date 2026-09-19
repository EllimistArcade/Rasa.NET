using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.World
{
    using Interfaces;

    /// <summary>
    /// One requirement a player must meet to use an item. Despite the table name, this is the
    /// client's generated.client.itemclass.reqData, which is keyed by item CLASS and not by item
    /// template - the client reads it as reqData.get(self.classId). Id below is an EntityClasses
    /// value, and the requirement applies to every template of that class.
    /// </summary>
    [Table(TableName)]
    public class ItemTemplateRequirementEntry : IHasId
    {
        public const string TableName = "itemtemplate_requirement";

        /// <summary>The item CLASS the requirement belongs to, not a template id. See the class remarks.</summary>
        [Key]
        [Column("id")]
        [Required]
        public uint Id { get; set; }

        [Column("req_type")]
        [Required]
        public byte RequirementType { get; set; }

        [Column("req_value")]
        [Required]
        public byte RequirementValue { get; set; }
    }
}
