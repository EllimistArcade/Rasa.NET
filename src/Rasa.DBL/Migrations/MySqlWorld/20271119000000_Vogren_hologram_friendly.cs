using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader.Missions.Plateau;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Vogren's hologram FRIENDLY again, not NEUTRAL, which any player could kill for experience every two seconds
    /// (PlateauVogrenHologramFriendlyV1). Data only, in plain SQL: no model of its own.
    ///
    /// Down makes it NEUTRAL again, if it is still FRIENDLY.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271119000000_Vogren_hologram_friendly")]
    public class Vogren_hologram_friendly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PlateauVogrenHologramFriendlyV1.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PlateauVogrenHologramFriendlyV1.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
