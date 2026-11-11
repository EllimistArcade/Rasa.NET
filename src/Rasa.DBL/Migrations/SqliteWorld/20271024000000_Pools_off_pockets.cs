using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Six lone creatures, five of the Warnet Caverns' wave spots and a pack moved off the pockets
    /// of navmesh they could not leave onto the open ground beside them, and a pack with no
    /// ground in reach made scripted (PoolsOffPockets). Data only, in plain SQL: no model of its own.
    ///
    /// It was 20271023000000, the timestamp Crater_lake_pen_generator has. A World that ran it under
    /// that id runs it again under this one, which its guards leave as it is, and its history row
    /// for the old id is removed.
    ///
    /// Down undoes only what is still as Up left it.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271024000000_Pools_off_pockets")]
    public class Pools_off_pockets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PoolsOffPockets.Up())
                migrationBuilder.Sql(sql);

            migrationBuilder.Sql(PoolsOffPockets.ForgetOldId);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PoolsOffPockets.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
