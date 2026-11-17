using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Takes the Landing Zone's turret pair off the east wall top, where no gun base is
    /// (LandingZoneFenceTurret). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts back only what is missing.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271201000000_Landing_zone_fence_turret")]
    public class Landing_zone_fence_turret : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in LandingZoneFenceTurret.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in LandingZoneFenceTurret.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
