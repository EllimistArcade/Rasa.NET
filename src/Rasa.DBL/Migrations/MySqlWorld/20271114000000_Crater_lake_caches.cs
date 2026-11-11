using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Reclaiming What's Ours' detonator marker moves with the Crater Lake weapons caches, out of
    /// the pipe across the fortification's platform onto its open floor (CraterLakeCaches). Data
    /// only, in plain SQL: no model of its own.
    ///
    /// Down puts it back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271114000000_Crater_lake_caches")]
    public class Crater_lake_caches : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeCaches.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in CraterLakeCaches.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
