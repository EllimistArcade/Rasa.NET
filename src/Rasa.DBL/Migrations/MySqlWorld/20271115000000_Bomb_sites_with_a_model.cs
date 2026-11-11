using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Devil's Den's bomb sites are the AFS charge's acceptor, which the client draws, in place of
    /// UsableBombForeanV01, which has no model to click (BombSitesWithAModel). Data only, in plain
    /// SQL: no model of its own.
    ///
    /// Down puts them back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271115000000_Bomb_sites_with_a_model")]
    public class Bomb_sites_with_a_model : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in BombSitesWithAModel.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in BombSitesWithAModel.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
