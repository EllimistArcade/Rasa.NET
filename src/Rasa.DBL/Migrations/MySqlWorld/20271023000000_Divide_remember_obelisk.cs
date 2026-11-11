using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// The Eloh obelisk the client's map leaves out under the We/Us and Remember Logos signs on the
    /// Concordia Divide hilltop at (667, -419) (DivideRememberObelisk): one world_destructible row,
    /// scenery. Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the row out.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271023000000_Divide_remember_obelisk")]
    public class Divide_remember_obelisk : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DivideRememberObelisk.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DivideRememberObelisk.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
