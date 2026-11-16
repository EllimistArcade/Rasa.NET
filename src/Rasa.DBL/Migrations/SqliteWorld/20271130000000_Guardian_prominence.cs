using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Guardian Prominence's people, Bane, commanders and four missions, and the three level 38 Bane
    /// guards at its entrance in the Wilderness (GuardianProminenceContent). Data only, in plain SQL:
    /// no model of its own.
    ///
    /// Down takes the missions out, then the rows.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271130000000_Guardian_prominence")]
    public class Guardian_prominence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in GuardianProminenceContent.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in GuardianProminenceContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
