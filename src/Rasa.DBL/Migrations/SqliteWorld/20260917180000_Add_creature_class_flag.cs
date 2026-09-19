using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;

    /// <summary>
    /// What each creature class is made of.
    ///
    /// CreatureInfo carried an empty flag list for every creature, so nothing server-side knew
    /// whether a thing was biological or mechanical - which is the entire difference between
    /// Salvage and Tissue Extraction, one refusing biologicals and the other mechanicals, and
    /// also what healdisc.py checks before healing a creature at all.
    ///
    /// Keyed on the entity class, not the creature: flags describe a species, so every spawn of
    /// a class shares them and a new spawn needs no data of its own.
    ///
    /// See CreatureClassFlagPreloader for where the values come from and how they were checked.
    /// </summary>
    public partial class Add_creature_class_flag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creature_class_flag",
                columns: table => new
                {
                    class_id = table.Column<uint>(type: "INTEGER", nullable: false),
                    flag_id = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creature_class_flag", x => new { x.class_id, x.flag_id });
                });

            new CreatureClassFlagPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creature_class_flag");
        }
    }
}
