using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// One Way Trip's Special Projects chamber in Maligo Base stands on its floor, not on the plane 4.5 m under it:
    /// the device, the Explosive Charge, Homburg, the prisoners and the pad out; objective 12's marker on the charge
    /// (MaligoChamberFloor). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts them back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271117000000_Maligo_chamber_floor")]
    public class Maligo_chamber_floor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MaligoChamberFloor.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MaligoChamberFloor.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
