using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlWorld
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
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    map_context_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    region_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    shape = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    pos_x = table.Column<double>(type: "double", nullable: false),
                    pos_y = table.Column<double>(type: "double", nullable: false),
                    pos_z = table.Column<double>(type: "double", nullable: false),
                    radius = table.Column<double>(type: "double", nullable: false),
                    half_x = table.Column<double>(type: "double", nullable: false),
                    half_z = table.Column<double>(type: "double", nullable: false),
                    min_y = table.Column<double>(type: "double", nullable: false),
                    max_y = table.Column<double>(type: "double", nullable: false),
                    underground = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    enabled = table.Column<byte>(type: "tinyint unsigned", nullable: false),
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
