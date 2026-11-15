using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// Mountain Spring Toxins: a charge at each chemical tower's foot, which the bomb is set at
    /// (MountainSpringToxinsCharges). Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the charges out of the stored scene.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271124000000_Mountain_spring_toxins_charges")]
    public class Mountain_spring_toxins_charges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MountainSpringToxinsCharges.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MountainSpringToxinsCharges.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
