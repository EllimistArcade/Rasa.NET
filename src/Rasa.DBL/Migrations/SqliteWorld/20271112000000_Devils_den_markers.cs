using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The Devil's Den missions' markers each name their map, the Den or the Palisades, so the
    /// ones at Den places show in the Den; Sever the Devil's Head's power node objective gets a
    /// second marker, on the node west of the Shard Impact Site (DevilsDenMarkers). Data only, in
    /// plain SQL: no model of its own.
    ///
    /// Down takes off exactly what Up added.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271112000000_Devils_den_markers")]
    public class Devils_den_markers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DevilsDenMarkers.Up(mySql: false))
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DevilsDenMarkers.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
