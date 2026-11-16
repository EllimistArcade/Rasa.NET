using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;
using Rasa.Services.Preloader.Missions.Purgas;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Purgas Station's top floor: Keen's squad behind its force field, Taylor's crew, the Force
    /// Field Controller and the Waypoint Activation Terminal, the Level 2 waypoint, the five Thrax
    /// Deathtribe Soldiers and the keypass Thrax, the station's Bane at level 40 and over, and A New
    /// Plan of Attack, Chain Reaction and Off Grid (PurgasStationContent, PurgasStationMissionsV1).
    /// Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the missions out, then the rows, and puts back the pools and the waypoint it changed.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271128000000_Purgas_station_level_one")]
    public class Purgas_station_level_one : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PurgasStationContent.Up())
                migrationBuilder.Sql(sql);

            foreach (var sql in PurgasStationMissionsV1.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PurgasStationMissionsV1.Down())
                migrationBuilder.Sql(sql);

            foreach (var sql in PurgasStationContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
