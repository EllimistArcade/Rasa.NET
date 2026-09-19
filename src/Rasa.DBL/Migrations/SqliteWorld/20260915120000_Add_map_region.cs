using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    public partial class Add_map_region : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: MapRegionEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    map_context_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    region_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    shape = table.Column<byte>(type: "INTEGER", nullable: false),
                    pos_x = table.Column<double>(type: "REAL", nullable: false),
                    pos_y = table.Column<double>(type: "REAL", nullable: false),
                    pos_z = table.Column<double>(type: "REAL", nullable: false),
                    radius = table.Column<double>(type: "REAL", nullable: false),
                    half_x = table.Column<double>(type: "REAL", nullable: false),
                    half_z = table.Column<double>(type: "REAL", nullable: false),
                    min_y = table.Column<double>(type: "REAL", nullable: false),
                    max_y = table.Column<double>(type: "REAL", nullable: false),
                    underground = table.Column<byte>(type: "INTEGER", nullable: false),
                    enabled = table.Column<byte>(type: "INTEGER", nullable: false),
                    comment = table.Column<string>(type: "varchar(96)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_region", x => x.id);
                });

            new MapRegionPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: MapRegionEntry.TableName);
        }
    }
}
