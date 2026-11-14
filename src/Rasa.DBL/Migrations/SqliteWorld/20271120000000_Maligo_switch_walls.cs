using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Self Preservation's yellow and red force field switches in Maligo Base stand in front of their walls, not behind
    /// them, and their markers with them (MaligoSwitchWalls). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts them back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271120000000_Maligo_switch_walls")]
    public class Maligo_switch_walls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MaligoSwitchWalls.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MaligoSwitchWalls.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
