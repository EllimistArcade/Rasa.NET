using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Reclaiming What's Ours: a "Bane Generator" marker at the courtyard's generator on the
    /// panel's objective, the generator the panel waits for (CraterLakeContent.PenGeneratorMarkerUp).
    /// Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the marker out.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271023000000_Crater_lake_pen_generator")]
    public class Crater_lake_pen_generator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeContent.PenGeneratorMarkerUp())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeContent.PenGeneratorMarkerDown())
                migrationBuilder.Sql(sql);
        }
    }
}
