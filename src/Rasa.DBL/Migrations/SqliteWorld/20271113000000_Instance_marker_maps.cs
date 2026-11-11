using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The instance missions' markers that were shown on the wrong map or on none each get their
    /// map (InstanceMarkerMaps): Fuel Lake Sabotage, Mining for Information, One Way Trip, Weapons
    /// Trafficking, Turpis Transport, Looking For Logos?, Deadly Silence, The Better Half, Ancient
    /// Mysteries, Stolen History and Reclaiming What's Ours. Data only, in plain SQL: no model of
    /// its own.
    ///
    /// Down takes off exactly what Up put in.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271113000000_Instance_marker_maps")]
    public class Instance_marker_maps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in InstanceMarkerMaps.Up(mySql: false))
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in InstanceMarkerMaps.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
