using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// The Treeback Camp's AFS, Bane, leaders and thirteen missions, and the three who give its
    /// missions at Viands Village on the Palisades (TreebackCampContent). Data only, in plain SQL:
    /// no model of its own.
    ///
    /// Down takes the missions out, then the rows.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271126000000_Treeback_camp")]
    public class Treeback_camp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in TreebackCampContent.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in TreebackCampContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
