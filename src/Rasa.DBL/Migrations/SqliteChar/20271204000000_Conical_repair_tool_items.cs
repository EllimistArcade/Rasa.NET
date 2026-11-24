using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteChar
{
    using Context.Char;
    using Services.Preloader;

    /// <summary>
    /// The Conical Repair Tools already given (item template 121310, whose class the client does
    /// not make an item of) made the Direct Repair Tool of the same band (ConicalRepairTool). The
    /// loader moves one in the Miscellaneous tab to the Equipment tab. No table changes.
    ///
    /// Down leaves them: nothing tells one made from a 121310 from any other.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteCharContext))]
    [Migration("20271204000000_Conical_repair_tool_items")]
    public partial class Conical_repair_tool_items : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in ConicalRepairTool.CharUp)
                migrationBuilder.Sql(statement);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
