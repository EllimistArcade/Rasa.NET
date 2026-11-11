using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Six lone creatures, five of the Warnet Caverns' wave spots and a pack moved off the pockets
    /// of navmesh they could not leave onto the open ground beside them, and a pack with no
    /// ground in reach made scripted (PoolsOffPockets). Data only, in plain SQL: no model of its own.
    ///
    /// Down undoes only what is still as Up left it.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271023000000_Pools_off_pockets")]
    public class Pools_off_pockets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PoolsOffPockets.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PoolsOffPockets.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
