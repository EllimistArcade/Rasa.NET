using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Too Quiet's Conical Repair Tool, whose class the client does not make an item of, made the
    /// Direct Repair Tool of the same band (ConicalRepairTool). Data only, in plain SQL: no model of
    /// its own.
    ///
    /// Down gives the mission the old template back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271204000000_Conical_repair_tool")]
    public class Conical_repair_tool : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ConicalRepairTool.WorldUp)
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ConicalRepairTool.WorldDown)
                migrationBuilder.Sql(sql);
        }
    }
}
