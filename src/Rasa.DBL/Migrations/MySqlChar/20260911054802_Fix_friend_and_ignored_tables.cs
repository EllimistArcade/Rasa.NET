using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlChar
{
    public partial class Fix_friend_and_ignored_tables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing row was written through the swapped FriendEntry / IgnoredEntry
            // constructors, which left the properties at 0 so account_id was filled from the
            // autoincrement instead of the owner. Friend rows therefore point at account 0 and
            // ignored rows belong to whichever account happens to share the generated id.
            // Nothing is recoverable, so the tables are emptied before the key is rebuilt.
            migrationBuilder.Sql("DELETE FROM friend;");
            migrationBuilder.Sql("DELETE FROM ignored;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_friend",
                table: "friend");

            migrationBuilder.AlterColumn<uint>(
                name: "account_id",
                table: "friend",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_friend",
                table: "friend",
                columns: new[] { "account_id", "friend_account_id" });

            migrationBuilder.DropPrimaryKey(
                name: "PK_ignored",
                table: "ignored");

            migrationBuilder.AlterColumn<uint>(
                name: "account_id",
                table: "ignored",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ignored",
                table: "ignored",
                columns: new[] { "account_id", "ignored_account_id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_friend",
                table: "friend");

            migrationBuilder.AlterColumn<uint>(
                name: "account_id",
                table: "friend",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_friend",
                table: "friend",
                column: "account_id");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ignored",
                table: "ignored");

            migrationBuilder.AlterColumn<uint>(
                name: "account_id",
                table: "ignored",
                type: "int unsigned",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "int unsigned")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ignored",
                table: "ignored",
                column: "account_id");
        }
    }
}
