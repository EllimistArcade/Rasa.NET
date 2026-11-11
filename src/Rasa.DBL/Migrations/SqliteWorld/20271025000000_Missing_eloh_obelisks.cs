using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The six Eloh obelisks the client's maps leave out under their Logos signs (MissingElohObelisks):
    /// world_destructible rows, scenery, on the Concordia Divide hilltop, either side of the Eloh
    /// Communication Tower's approach, in Fault Lever's Hidden Cave, in Kardash's Eloh Cache and in
    /// Sanctus Grotto. Data only, in plain SQL: no model of its own.
    ///
    /// The Divide's first came in as 20271023000000_Divide_remember_obelisk, the timestamp
    /// Crater_lake_pen_generator has. A World that ran that keeps its row (each row goes in only where
    /// it is not) and loses the old id's history row.
    ///
    /// Down takes the six rows out.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271025000000_Missing_eloh_obelisks")]
    public class Missing_eloh_obelisks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MissingElohObelisks.Up())
                migrationBuilder.Sql(sql);

            migrationBuilder.Sql(MissingElohObelisks.ForgetOldId);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MissingElohObelisks.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
