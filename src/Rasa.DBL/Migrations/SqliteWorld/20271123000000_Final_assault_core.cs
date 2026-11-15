using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Final Assault's power core ready in its running state and destroyed by the detonator, not the other way round
    /// (FinalAssaultCore). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts it back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271123000000_Final_assault_core")]
    public class Final_assault_core : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in FinalAssaultCore.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in FinalAssaultCore.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
