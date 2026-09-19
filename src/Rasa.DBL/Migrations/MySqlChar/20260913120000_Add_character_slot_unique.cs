using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlChar
{
    public partial class Add_character_slot_unique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One character per pod. Character selection keys an account's characters by slot,
            // so a second row in the same slot used to throw at every login. The game server now
            // refuses such a create; this makes the table refuse it too. Creating the index fails
            // with a duplicate-key error if the table already holds one - find them first with
            //   SELECT account_id, slot, GROUP_CONCAT(id) FROM `character`
            //   GROUP BY account_id, slot HAVING COUNT(*) > 1;
            // and move the extra rows to free slots.
            migrationBuilder.CreateIndex(
                name: "character_index_account_slot",
                table: "character",
                columns: new[] { "account_id", "slot" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "character_index_account_slot",
                table: "character");
        }
    }
}
