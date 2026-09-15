using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    public partial class Add_recipe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: RecipeEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    energy_cost = table.Column<uint>(type: "INTEGER", nullable: false),
                    kraftwerks_seconds = table.Column<uint>(type: "INTEGER", nullable: false),
                    result_template_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    result_amount = table.Column<uint>(type: "INTEGER", nullable: false),
                    min_level = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: RecipeInputEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    recipe_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    input_class_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    quantity = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_input", x => x.id);
                });

            new RecipePreloader().Preload(migrationBuilder);
            new RecipeInputPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: RecipeInputEntry.TableName);

            migrationBuilder.DropTable(
                name: RecipeEntry.TableName);
        }
    }
}
