using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Structures.World;

    /// <summary>
    /// Makes weapon overheating reachable.
    ///
    /// Every one of the 2440 item_template_weapon rows carried cool_rate 1 and heat_per_shot 2 -
    /// one placeholder pair copied across the whole table. Against the client's HEAT_CAPACITY of
    /// 1000 that is 500 shots to jam and sixteen minutes to cool from one, so no player would ever
    /// have seen a jam.
    ///
    /// 100 and 10 give 100 shots to jam at full condition and ten seconds back to cold. A working
    /// default, not a reconstruction: the real values were the 2009 server's and are not in the
    /// client or anything shipped with it.
    ///
    /// The update is conditional on the row still holding the old pair, so a weapon someone has
    /// since tuned by hand keeps its values - which is the point, since these are meant to be
    /// corrected weapon by weapon. Down puts back exactly the rows this changed, on the same test.
    ///
    /// Data only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Retune_weapon_heat : Migration
    {
        private const string Table = ItemTemplateWeaponEntry.TableName;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"update {Table} set cool_rate = 100, heat_per_shot = 10 " +
                "where cool_rate = 1 and heat_per_shot = 2;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"update {Table} set cool_rate = 1, heat_per_shot = 2 " +
                "where cool_rate = 100 and heat_per_shot = 10;");
        }
    }
}
