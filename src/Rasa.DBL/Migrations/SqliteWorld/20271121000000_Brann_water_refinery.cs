using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The Brann Water Refinery's AFS, Bane, bosses and five missions, and the living quarters'
    /// Plains Thrax made the refinery's own (BrannWaterRefineryContent). Data only, in plain SQL: no
    /// model of its own.
    ///
    /// Down takes the missions out, then the rows, and puts back the pools it changed.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271121000000_Brann_water_refinery")]
    public class Brann_water_refinery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in BrannWaterRefineryContent.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in BrannWaterRefineryContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
