using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// The world wormholes as one network (the game server's Wormholes).
    ///
    /// wormhole_lock: a condition on a trip from one wormhole to another (from_teleporter_id,
    /// to_teleporter_id, 0 for any), kind 1 a mission taken at least once, kind 2 a level (not
    /// enforced yet), with its value. A trip no row matches is open.
    ///
    /// And the rows (WorldWormholesSeed): the Ligo Crucible wormhole, 358, which the client has
    /// and the world had as a placeholder; the Guardian Prominence Personal Wormhole's class
    /// (425, 28478 to 28474); and the locks from the Concordia Divide wormhole to the three on
    /// Arieki, which open with mission 1038.
    ///
    /// Down puts 358 and the class back and drops the table.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_world_wormholes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: WormholeLockEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    from_teleporter_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    to_teleporter_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    kind = table.Column<uint>(type: "INTEGER", nullable: false),
                    value = table.Column<uint>(type: "INTEGER", nullable: false),
                    comment = table.Column<string>(type: "varchar(128)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wormhole_lock", x => x.id);
                });

            foreach (var statement in WorldWormholesSeed.InsertStatements)
                migrationBuilder.Sql(statement);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in WorldWormholesSeed.DeleteStatements)
                migrationBuilder.Sql(statement);

            migrationBuilder.DropTable(name: WormholeLockEntry.TableName);
        }
    }
}
