using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteChar
{
    public partial class Add_petition_table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "petition",
                columns: table => new
                {
                    id = table.Column<uint>(type: "integer", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    account_id = table.Column<uint>(type: "integer", nullable: false),
                    character_id = table.Column<uint>(type: "integer", nullable: false),
                    type = table.Column<byte>(type: "tinyint(3)", nullable: false, defaultValue: (byte)0),
                    summary = table.Column<string>(type: "varchar(255)", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    map_context_id = table.Column<uint>(type: "int(11)", nullable: false),
                    pos_x = table.Column<double>(type: "double", nullable: false),
                    pos_y = table.Column<double>(type: "double", nullable: false),
                    pos_z = table.Column<double>(type: "double", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petition", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "petition");
        }
    }
}
