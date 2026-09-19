using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    public partial class Add_map_link : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_link",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    map_context_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    pos_x = table.Column<double>(type: "double", nullable: false),
                    pos_y = table.Column<double>(type: "double", nullable: false),
                    pos_z = table.Column<double>(type: "double", nullable: false),
                    radius = table.Column<double>(type: "double", nullable: false),
                    dest_map_context_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    dest_pos_x = table.Column<double>(type: "double", nullable: false),
                    dest_pos_y = table.Column<double>(type: "double", nullable: false),
                    dest_pos_z = table.Column<double>(type: "double", nullable: false),
                    dest_rotation = table.Column<double>(type: "double", nullable: false),
                    kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    enabled = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    comment = table.Column<string>(type: "varchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_link", x => x.id);
                });

            new MapLinkPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: MapLinkEntry.TableName);
        }
    }
}
