using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Structures.World;

    /// <summary>
    /// Two corrections to the dropship pads in the teleporter table, checked against the
    /// client's map files (every pad stands on an ArchHumDropshipLandingpad) and the client's
    /// waypoint names and map markers.
    ///
    /// Ligo Crucible (359) had no position and no map, so the Crucible zone could not be flown
    /// to or from. Its pad is the landing pad at (968, 144, -76) on adv_arieki_ligo_burningsteps
    /// (1993), where the client's map puts the "Dropship Transport" marker.
    ///
    /// 536 "Shadow Edge Post" was a second row at exactly the position of 263 "Dropship
    /// Transport: Ashen Desert", with an id the client's waypoint table has no name for, so the
    /// travel window showed Ashen Desert twice, once as a missing-translation error. Removed.
    ///
    /// Up only changes rows still carrying the wrong value and deletes only the duplicate as
    /// it stands; Down undoes exactly that. Data only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Place_dropship_pads : Migration
    {
        private const string Table = TeleporterEntry.TableName;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"update {Table} set type = 4, map_context_id = 1993, pos_x = 968.0, pos_y = 144.16, pos_z = -76.0, rotation = 0.0 where id = 359 and map_context_id = 0;");
            migrationBuilder.Sql($"delete from {Table} where id = 536 and map_context_id = 1734 and type = 4;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"insert into {Table} (id, class_id, type, description, pos_x, pos_y, pos_z, rotation, map_context_id) select 536, 0, 4, 'Dropship Transport: Shadow Edge Post', -129.82812, 294.73047, -748.35256, 0.0, 1734 where not exists (select 1 from {Table} where id = 536);");
            migrationBuilder.Sql($"update {Table} set type = 0, map_context_id = 0, pos_x = 0.0, pos_y = 0.0, pos_z = 0.0 where id = 359 and map_context_id = 1993;");
        }
    }
}
