using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteWorld
{
    using Structures.World;

    /// <summary>
    /// Five corrections to the logos shrine table, checked against the client's own map files:
    /// every shrine the server places should sit two units above an ArchElohLogosDispenserBase
    /// pedestal in the map, and 164 of the 166 did. The rest, and the pedestals that had no
    /// shrine at all, are what this fixes where the evidence allows.
    ///
    /// Backward (3) and Transform (331), both on Divide, carried each other's shrine class, so
    /// each showed the other's glyph. Each row gives the stone its id names, so Force Blast,
    /// Reality Ripper and Concussive Wave - which want Backward - were never affected; only the
    /// picture was wrong. Swapped back.
    ///
    /// Knowledge (208), on Palisades, used TerraForeasQuarryRockLearge01V02 - a rock - for its
    /// class, and stood on its pedestal looking like one. It gets the Knowledge shrine class.
    ///
    /// Far (64) and Disperse (30) were not in the table. The Vogren's Tomb mission texts put
    /// "Far" in the Bane's Turpis Refinery (the Wetland Refinery map, 1429) and "Disperse" in
    /// the Forean Village Ruins, P'reo Das (1743); each of those maps has exactly one pedestal
    /// and no shrine, so the two rows go there, two units above the pedestal like every other.
    ///
    /// Three pedestals still have nothing on them - one in Marshes at (-504, -690), one in
    /// Mires at (-313, -202), one in Eloh Vale - and These (319) is still at the origin: the
    /// client's text names no stone for any of them. Transform stays where it was found, on top
    /// of Many, for the same reason.
    ///
    /// Up only changes rows still carrying the wrong class, and inserts only if the id is
    /// absent, so a database corrected by hand is left alone; Down undoes exactly that. Data
    /// only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Fix_logos_shrines : Migration
    {
        private const string Table = LogosEntry.TableName;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backward and Transform had each other's class
            migrationBuilder.Sql($"update {Table} set class_id = 7289 where id = 3 and class_id = 21403;");
            migrationBuilder.Sql($"update {Table} set class_id = 21403 where id = 331 and class_id = 7289;");

            // Knowledge was a quarry rock
            migrationBuilder.Sql($"update {Table} set class_id = 21261 where id = 208 and class_id = 21268;");

            // Far, on the Turpis Refinery's pedestal; Disperse, on P'reo Das's
            migrationBuilder.Sql($"insert or ignore into {Table} (id, class_id, map_context_id, pos_x, pos_y, pos_z, name) values (64, 21210, 1429, 40.0, 111.8014907836914, -224.0, 'Far');");
            migrationBuilder.Sql($"insert or ignore into {Table} (id, class_id, map_context_id, pos_x, pos_y, pos_z, name) values (30, 12678, 1743, -30.0, 58.25, 18.75, 'Disperse');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"delete from {Table} where id = 64 and class_id = 21210 and map_context_id = 1429;");
            migrationBuilder.Sql($"delete from {Table} where id = 30 and class_id = 12678 and map_context_id = 1743;");

            migrationBuilder.Sql($"update {Table} set class_id = 21268 where id = 208 and class_id = 21261;");

            migrationBuilder.Sql($"update {Table} set class_id = 7289 where id = 331 and class_id = 21403;");
            migrationBuilder.Sql($"update {Table} set class_id = 21403 where id = 3 and class_id = 7289;");
        }
    }
}
