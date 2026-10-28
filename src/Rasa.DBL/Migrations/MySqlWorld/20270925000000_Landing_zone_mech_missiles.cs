using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// The Landing Zone's AFS Mech Defenders given the Mech's missiles beside its minigun
    /// (LandingZoneMechMissiles). Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the missiles off them again.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20270925000000_Landing_zone_mech_missiles")]
    public class Landing_zone_mech_missiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in LandingZoneMechMissiles.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in LandingZoneMechMissiles.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
