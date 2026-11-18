using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Markers inside the instance for objectives marked only at its entrance (InstanceInsideMarkers):
    /// Retrieval for Recon and Destroying the Warnet Barrier in Minos Caverns, The Escapist in
    /// Pravus, Fried Atta in the Ojasa Atta Hive, Scarecrow and Trophy Case in the Warnet Caverns.
    /// Data only, in plain SQL: no model of its own.
    ///
    /// Down takes out what Up put in.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271203000000_Instance_inside_markers")]
    public class Instance_inside_markers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in InstanceInsideMarkers.Up(mySql: true))
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in InstanceInsideMarkers.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
