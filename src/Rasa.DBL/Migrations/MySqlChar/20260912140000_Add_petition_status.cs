using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlChar
{
    public partial class Add_petition_status : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows were filed before there was anywhere for them to go, so they are all
            // still open - which is what the default gives them.
            migrationBuilder.AddColumn<byte>(
                name: "status",
                table: "petition",
                type: "tinyint(3) unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "resolution",
                table: "petition",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "status", table: "petition");
            migrationBuilder.DropColumn(name: "resolution", table: "petition");
        }
    }
}
