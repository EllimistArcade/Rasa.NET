using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    public partial class Add_kraftwerks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: KraftwerksEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    class_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    map_context_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    pos_x = table.Column<double>(type: "REAL", nullable: false),
                    pos_y = table.Column<double>(type: "REAL", nullable: false),
                    pos_z = table.Column<double>(type: "REAL", nullable: false),
                    rotation = table.Column<double>(type: "REAL", nullable: false),
                    comment = table.Column<string>(type: "varchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kraftwerks", x => x.id);
                });

            new KraftwerksPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: KraftwerksEntry.TableName);
        }
    }
}
