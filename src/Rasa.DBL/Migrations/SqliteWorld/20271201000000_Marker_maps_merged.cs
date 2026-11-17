using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The scenes Mission_marker_maps, Devils_den_markers and Instance_marker_maps each map get
    /// every map the three name, whichever of them ran first (MarkerMapsMerged): the Den's Elohim
    /// Artifacts, The Missing Strike Team and Sever the Devil's Head, and Looking For Logos?. Data
    /// only, in plain SQL: no model of its own.
    ///
    /// Down gives the scenes the maps a world made from scratch had before it.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271201000000_Marker_maps_merged")]
    public class Marker_maps_merged : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MarkerMapsMerged.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MarkerMapsMerged.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
