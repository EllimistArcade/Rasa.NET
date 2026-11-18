using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Crater Lake's mission markers (CraterLakeMarkers): Ghost in the Machine as V2, its return
    /// objective open with markers at the facility's door and at Franja until she takes it; the
    /// Biological Lab's terminal marker out of the greenhouse; a marker on Velns in the facility for
    /// The Dead Live; and Centrifuge Fuel's markers on the buildings with crates. Data only, in plain
    /// SQL: no model of its own.
    ///
    /// Down takes out what Up put in and writes V1 of Ghost in the Machine again.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271202000000_Crater_lake_markers")]
    public class Crater_lake_markers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeMarkers.Up(mySql: true))
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeMarkers.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
