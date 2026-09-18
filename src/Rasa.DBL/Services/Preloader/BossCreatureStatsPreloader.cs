using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// Health and armour for the seeded bosses: health = 500 + 100 x level and armour =
    /// 200 + 5 x level, which puts a level 15 boss on 2000 health, the value the fork's existing named
    /// bosses already carry. Body, mind and spirit are 15, as they are on every creature row in the
    /// database - nothing reads them for a creature yet.
    ///
    /// These are the first numbers to retune once bosses are fought in game.
    /// </summary>
    public class BossCreatureStatsPreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, CreatureStatEntry.TableName, typeof(CreatureStatEntry));
        }

        protected override IEnumerable<object[]> GetRows()
        {
            yield return new object[] { 520001, 15, 15, 15, 5100, 430 };
            yield return new object[] { 520002, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520003, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520004, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520005, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520006, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520007, 15, 15, 15, 4900, 420 };
            yield return new object[] { 520008, 15, 15, 15, 800, 215 };
            yield return new object[] { 520009, 15, 15, 15, 800, 215 };
            yield return new object[] { 520010, 15, 15, 15, 800, 215 };
            yield return new object[] { 520011, 15, 15, 15, 1600, 255 };
            yield return new object[] { 520012, 15, 15, 15, 1400, 245 };
            yield return new object[] { 520013, 15, 15, 15, 5200, 435 };
            yield return new object[] { 520014, 15, 15, 15, 5200, 435 };
            yield return new object[] { 520015, 15, 15, 15, 2500, 300 };
            yield return new object[] { 520016, 15, 15, 15, 2500, 300 };
            yield return new object[] { 520017, 15, 15, 15, 2500, 300 };
            yield return new object[] { 520018, 15, 15, 15, 2500, 300 };
            yield return new object[] { 520019, 15, 15, 15, 2500, 300 };
            yield return new object[] { 520020, 15, 15, 15, 5300, 440 };
            yield return new object[] { 520021, 15, 15, 15, 5300, 440 };
            yield return new object[] { 520022, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520023, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520024, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520025, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520026, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520027, 15, 15, 15, 4700, 410 };
            yield return new object[] { 520028, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520029, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520030, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520031, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520032, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520033, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520034, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520035, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520036, 15, 15, 15, 4300, 390 };
            yield return new object[] { 520037, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520038, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520039, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520040, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520041, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520042, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520043, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520044, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520045, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520046, 15, 15, 15, 3300, 340 };
            yield return new object[] { 520047, 15, 15, 15, 4500, 400 };
            yield return new object[] { 520048, 15, 15, 15, 4500, 400 };
            yield return new object[] { 520049, 15, 15, 15, 4500, 400 };
            yield return new object[] { 520050, 15, 15, 15, 4500, 400 };
            yield return new object[] { 520051, 15, 15, 15, 3700, 360 };
            yield return new object[] { 520052, 15, 15, 15, 3700, 360 };
            yield return new object[] { 520053, 15, 15, 15, 3700, 360 };
            yield return new object[] { 520054, 15, 15, 15, 3700, 360 };
            yield return new object[] { 520055, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520056, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520057, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520058, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520059, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520060, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520061, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520062, 15, 15, 15, 4000, 375 };
            yield return new object[] { 520063, 15, 15, 15, 5000, 425 };
            yield return new object[] { 520064, 15, 15, 15, 5000, 425 };
        }
    }
}
