using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteChar
{
    public partial class Add_character_active_ability_slot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The armed ability drawer slot, 0 to 24, restored on every map entry. Nothing saved
            // it before, so every character starts on the first slot, where they always were.
            migrationBuilder.AddColumn<byte>(
                name: "active_ability_slot",
                table: "character",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "active_ability_slot", table: "character");
        }
    }
}
