using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// The Stalkers' ovulate rows reach as far as their egg does, the Plateau's and Devil's Den's
    /// Juggernauts get their mortars and their blackhole, and two of the Plateau's Stalker pools
    /// come down from orbit (StalkerAndJuggernautKits). Data only, in plain SQL: no model of its
    /// own.
    ///
    /// Down puts them back as they were.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271111000000_Stalker_and_juggernaut_kits")]
    public class Stalker_and_juggernaut_kits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in StalkerAndJuggernautKits.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in StalkerAndJuggernautKits.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
