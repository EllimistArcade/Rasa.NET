using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteWorld
{
    using Structures.World;

    /// <summary>
    /// The rest of the logos shrine corrections, following Fix_logos_shrines. That migration
    /// settled what the client's map files could settle on their own - a shrine belongs two
    /// units above an ArchElohLogosDispenserBase pedestal - and left three pedestals with no
    /// stone, a stone at the origin, and one row doubled up on another's pedestal, because the
    /// client's text names no stone for any of them.
    ///
    /// The Ellatha fan site's per-zone logos maps from the live game do. Their names and
    /// coordinates agree with every one of the 164 rows already on a pedestal, so they can be
    /// trusted for the rest:
    ///
    /// Many (20) was a second row on Transform's pedestal on Divide. The live game had no Many
    /// on Divide; it stood on the empty Mires pedestal at (-313, -202). Moved there.
    ///
    /// These (319) was at the origin, filed under Palisades. It is the stone on the single
    /// pedestal in Eloh Vale (2084). Moved there.
    ///
    /// Good (180) was not in the table; it is the stone on the empty Marshes pedestal at
    /// (-504, -690). Inserted.
    ///
    /// Fix_logos_shrines put Disperse (30) on the single pedestal in P'reo Das (1743). That
    /// pedestal is Civilization's (106), which is inserted there. Disperse was a mission
    /// pickup with no pedestal: the live game had it in the Bane command room at the back of
    /// the same map, and it moves there, onto the floor at the fan site's coordinates matched
    /// to the room geometry - within a few units, which is as close as the sources allow.
    ///
    /// Logos (65), the other Vogren's Tomb "retrieve" stone, sat the same way on the console
    /// overlook in the Logos Research Facility (1700). Inserted there, on the floor.
    ///
    /// Every pedestal in every map now has a stone: 171 rows.
    ///
    /// Up only moves rows still where the previous data put them, and inserts only if the id
    /// is absent, so a database corrected by hand is left alone; Down undoes exactly that.
    /// Data only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Place_remaining_logos : Migration
    {
        private const string Table = LogosEntry.TableName;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Many: off Transform's pedestal on Divide, onto its own in Mires
            migrationBuilder.Sql($"update {Table} set map_context_id = 1759, pos_x = -313.37518310546875, pos_y = 241.7198944091797, pos_z = -201.93954467773438 where id = 20 and map_context_id = 1148;");

            // These: from the origin to the Eloh Vale pedestal
            migrationBuilder.Sql($"update {Table} set map_context_id = 2084, pos_x = -200.5, pos_y = 182.423828125, pos_z = -180.2528533935547 where id = 319 and map_context_id = 1244;");

            // Disperse: off Civilization's pedestal, into the Bane command room (approximate)
            migrationBuilder.Sql($"update {Table} set pos_x = -33.0, pos_y = 65.5, pos_z = 390.0 where id = 30 and map_context_id = 1743 and pos_z < 100;");

            // The last two empty pedestals
            migrationBuilder.Sql($"insert or ignore into {Table} (id, class_id, map_context_id, pos_x, pos_y, pos_z, name) values (180, 21228, 1454, -503.9174499511719, 233.5238037109375, -689.8259887695312, 'Good');");
            migrationBuilder.Sql($"insert or ignore into {Table} (id, class_id, map_context_id, pos_x, pos_y, pos_z, name) values (106, 21146, 1743, -30.0, 58.25, 18.75, 'Civilization');");

            // The other mission stone, on the floor of the console overlook (approximate)
            migrationBuilder.Sql($"insert or ignore into {Table} (id, class_id, map_context_id, pos_x, pos_y, pos_z, name) values (65, 21269, 1700, 608.0, 258.0, -30.0, 'Logos');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"delete from {Table} where id = 65 and map_context_id = 1700;");
            migrationBuilder.Sql($"delete from {Table} where id = 106 and map_context_id = 1743;");
            migrationBuilder.Sql($"delete from {Table} where id = 180 and map_context_id = 1454;");

            migrationBuilder.Sql($"update {Table} set pos_x = -30.0, pos_y = 58.25, pos_z = 18.75 where id = 30 and map_context_id = 1743 and pos_z > 100;");

            migrationBuilder.Sql($"update {Table} set map_context_id = 1244, pos_x = 0.0, pos_y = 0.0, pos_z = 0.0 where id = 319 and map_context_id = 2084;");

            migrationBuilder.Sql($"update {Table} set map_context_id = 1148, pos_x = -474.5039, pos_y = 143.21094, pos_z = 582.72656 where id = 20 and map_context_id = 1759;");
        }
    }
}
