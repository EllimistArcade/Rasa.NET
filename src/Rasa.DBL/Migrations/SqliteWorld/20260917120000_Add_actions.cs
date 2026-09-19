using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// The client's action tables, so the server can drive abilities from data instead of
    /// hard-coded numbers: what every action is (action), its windup, recovery, range and
    /// cooldown at every level (action_level), what it costs (action_cost), what it does and
    /// how hard (action_property), what the performer must carry (action_item_requirement),
    /// and which action a usable item performs (item_template_action). All six are
    /// generated.client.actiondata, row for row; the remarks on each entry say which table
    /// and how the client reads it.
    /// </summary>
    public partial class Add_actions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: ActionEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    module = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    is_charged = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: ActionLevelEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    action_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    windup_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    windup_anim_family_id = table.Column<uint>(type: "INTEGER", nullable: true),
                    recovery_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    recovery_anim_family_id = table.Column<uint>(type: "INTEGER", nullable: true),
                    max_range = table.Column<int>(type: "INTEGER", nullable: false),
                    reuse_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    preload = table.Column<byte>(type: "INTEGER", nullable: false),
                    start_reuse_on_perform = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_level", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: ActionCostEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    action_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    attribute_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    cost = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_cost", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: ActionPropertyEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    action_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    property_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_property", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: ActionItemRequirementEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    action_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false),
                    item_class_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    quantity = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_item_requirement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: ItemTemplateActionEntry.TableName,
                columns: table => new
                {
                    id = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    item_template_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    action_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    level = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_template_action", x => x.id);
                });

            new ActionPreloader().Preload(migrationBuilder);
            new ActionLevelPreloader().Preload(migrationBuilder);
            new ActionCostPreloader().Preload(migrationBuilder);
            new ActionPropertyPreloader().Preload(migrationBuilder);
            new ActionItemRequirementPreloader().Preload(migrationBuilder);
            new ItemTemplateActionPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: ItemTemplateActionEntry.TableName);

            migrationBuilder.DropTable(
                name: ActionItemRequirementEntry.TableName);

            migrationBuilder.DropTable(
                name: ActionPropertyEntry.TableName);

            migrationBuilder.DropTable(
                name: ActionCostEntry.TableName);

            migrationBuilder.DropTable(
                name: ActionLevelEntry.TableName);

            migrationBuilder.DropTable(
                name: ActionEntry.TableName);
        }
    }
}
