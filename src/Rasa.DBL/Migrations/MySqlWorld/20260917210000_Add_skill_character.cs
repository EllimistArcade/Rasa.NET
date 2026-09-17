using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;

    /// <summary>
    /// Which class grants each skill, and the level it takes to train it.
    ///
    /// The client refuses to draw the spend buttons for a skill outside the player's own line of
    /// classes, and for one above their level. The server had neither rule, so LevelSkills
    /// accepted any skill at any level as long as the points were paid - and since abilities are
    /// granted by the skills a player holds, a level 1 Recruit could train a Tier 4 skill and
    /// perform that class's abilities.
    ///
    /// A verbatim copy of generated.client.skilldata.skillCharacter: 73 rows, one per skill.
    /// </summary>
    public partial class Add_skill_character : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skill_character",
                columns: table => new
                {
                    id = table.Column<uint>(type: "int unsigned", nullable: false),
                    class_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    required_level = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_character", x => x.id);
                });

            new SkillCharacterPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skill_character");
        }
    }
}
