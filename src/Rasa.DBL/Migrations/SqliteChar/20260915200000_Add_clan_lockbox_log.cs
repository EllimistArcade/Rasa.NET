using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteChar
{
    /// <summary>
    /// The clan lockbox's transaction history: one row per deposit, withdrawal, destruction and
    /// tab purchase, which the clan lockbox window lists under its Log tab.
    ///
    /// The names are stored rather than joined from character_id, because a log says who did
    /// something at the time they did it - a member who has since left, been renamed or been
    /// deleted still has to read back as themselves.
    ///
    /// credit_type and item_template_id are both nullable in effect: a credit row carries no
    /// item and an item row carries no credit type, and the client picks how to draw the line
    /// from which of the two it was sent. Zero is the stored form of absent; the packet turns it
    /// into the real None the client tests for.
    /// </summary>
    public partial class Add_clan_lockbox_log : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clan_lockbox_log",
                columns: table => new
                {
                    id = table.Column<uint>(type: "integer", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    clan_id = table.Column<uint>(type: "integer", nullable: false),
                    transaction_type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    character_id = table.Column<uint>(type: "integer", nullable: false),
                    character_name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    user_name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    credit_type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    item_template_id = table.Column<uint>(type: "integer", nullable: false),
                    quantity = table.Column<uint>(type: "integer", nullable: false),
                    transaction_time = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_lockbox_log", x => x.id);
                });

            // Every read is one clan's most recent hundred.
            migrationBuilder.CreateIndex(
                name: "clan_lockbox_log_index_clan_id_time",
                table: "clan_lockbox_log",
                columns: new[] { "clan_id", "transaction_time" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clan_lockbox_log");
        }
    }
}
