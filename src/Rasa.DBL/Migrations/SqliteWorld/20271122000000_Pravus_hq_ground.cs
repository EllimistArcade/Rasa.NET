using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Two Thrax packs in the compound about Pravus Research's HQ on the ground, not on the plane under it (PravusHqGround). Data
    /// only, in plain SQL: no model of its own.
    ///
    /// Down puts them back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271122000000_Pravus_hq_ground")]
    public class Pravus_hq_ground : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PravusHqGround.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in PravusHqGround.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
