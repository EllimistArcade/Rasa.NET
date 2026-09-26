using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlChar
{
    /// <summary>
    /// The player flags a character holds: one row per flag. The client's actions list the flags
    /// they need (actiondata.playerFlagReqs: 57 emotes, among them the account, veteran and event
    /// reward emotes), and the manifestation's PlayerFlags list is what it checks them against.
    /// </summary>
    public partial class Add_character_player_flag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_player_flag",
                columns: table => new
                {
                    character_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    player_flag_id = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_player_flag", x => new { x.character_id, x.player_flag_id });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_player_flag");
        }
    }
}
