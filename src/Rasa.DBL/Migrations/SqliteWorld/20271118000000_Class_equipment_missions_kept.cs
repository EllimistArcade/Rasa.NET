using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.SqliteWorld
{
    /// <summary>
    /// "Getting It In Gear: Soldier Class" (2010) and "Specialist Class" (2011) cannot be abandoned, as the client's
    /// NON_ABANDONABLE_MISSIONS has it (ClassEquipmentMissionsKept). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts them back to Allowed.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(SqliteWorldContext))]
    [Migration("20271118000000_Class_equipment_missions_kept")]
    public class Class_equipment_missions_kept : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ClassEquipmentMissionsKept.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ClassEquipmentMissionsKept.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
