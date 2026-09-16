using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// Replaces the whole itemtemplate table with one row for each of the 30225 item templates.
    ///
    /// The table held 4985 rows, 16% of the templates, and those rows were a stub: buy_price 1 in
    /// 4984 of them, sell_price 1 in all 4985, the seven flags identical everywhere. Only
    /// quality_id and inventory_category carried anything. So vendors sold every item for a
    /// credit, bought every item back for a credit and repaired for nothing, while the other
    /// 25240 templates had no row at all.
    ///
    /// ItemTemplatePreloader now carries all 30225 rows, priced from the item class's loot_value.
    /// Its remarks give the evidence for each derived column.
    ///
    /// The old rows are deleted rather than updated, because every column of every one of them is
    /// either reproduced by the new row or was wrong. Down empties the table; the 4985 stub rows
    /// are not worth restoring, and a rebuild from the initial migration would replay this one
    /// anyway.
    ///
    /// Data only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Regenerate_item_template : Migration
    {
        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new ItemTemplatePreloader()
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Clear(migrationBuilder);

            foreach (var preloader in _preloaders)
            {
                preloader.Preload(migrationBuilder);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Clear(migrationBuilder);
        }

        private static void Clear(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(string.Format("delete from `{0}`;", ItemTemplateEntry.TableName));
        }
    }
}
