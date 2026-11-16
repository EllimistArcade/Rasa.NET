using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;
using Rasa.Services.Preloader.Missions.Purgas;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Purgas Station below the top floor: the seven fuel basins, the reactor's fuel pumps and the
    /// stabilization module, the Level 3 keypass Thrax, the pedestal room's puzzle and field and the
    /// sleeping pods, the three trip wires and their alarms, Juggernaut Bay's Juggernauts, and A Means
    /// to an End and Reactor Overload (PurgasStationDepthsContent, PurgasStationDepthsMissionsV1).
    /// Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the two missions out, then the rows, and puts back the two pools it changed.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271129000000_Purgas_station_lower_levels")]
    public class Purgas_station_lower_levels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PurgasStationDepthsContent.Up())
                migrationBuilder.Sql(sql);

            foreach (var sql in PurgasStationDepthsMissionsV1.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PurgasStationDepthsMissionsV1.Down())
                migrationBuilder.Sql(sql);

            foreach (var sql in PurgasStationDepthsContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
