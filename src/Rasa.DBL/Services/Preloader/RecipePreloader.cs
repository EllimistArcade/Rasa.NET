using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// The 160 fabrication recipes of Tabula Rasa 1.16.5, from the client's
    /// generated.shared.recipe RecipeTemplate set: ammunition, med packs, grenades, the component
    /// ladder, the Laser Pistol and the armour paints. The id is the schematic's item template.
    /// </summary>
    public class RecipePreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, RecipeEntry.TableName, typeof(RecipeEntry));
        }

        protected override IEnumerable<object[]> GetRows()
        {
            // id (schematic template), energy_cost, kraftwerks_seconds, result_template_id, result_amount, min_level
            yield return new object[] { 641, 0, 5, 28, 500, 1 };
            yield return new object[] { 1929, 0, 5, 30, 50, 1 };
            yield return new object[] { 42227, 100, 8, 175, 1, 1 };
            yield return new object[] { 45072, 0, 2, 44917, 10, 1 };
            yield return new object[] { 45073, 0, 2, 44918, 10, 1 };
            yield return new object[] { 45074, 0, 2, 44919, 10, 1 };
            yield return new object[] { 45075, 0, 2, 44920, 10, 6 };
            yield return new object[] { 45076, 0, 2, 44921, 10, 6 };
            yield return new object[] { 45077, 0, 2, 45047, 10, 6 };
            yield return new object[] { 45078, 0, 2, 45048, 10, 11 };
            yield return new object[] { 45079, 0, 2, 45049, 10, 11 };
            yield return new object[] { 45080, 0, 2, 45050, 10, 11 };
            yield return new object[] { 45081, 0, 2, 45051, 10, 16 };
            yield return new object[] { 45082, 0, 2, 45052, 10, 16 };
            yield return new object[] { 45083, 0, 2, 45053, 10, 16 };
            yield return new object[] { 45084, 0, 2, 45054, 10, 21 };
            yield return new object[] { 45085, 0, 2, 45055, 10, 21 };
            yield return new object[] { 45086, 0, 2, 45056, 10, 21 };
            yield return new object[] { 45087, 0, 2, 45057, 10, 26 };
            yield return new object[] { 45088, 0, 2, 45058, 10, 26 };
            yield return new object[] { 45089, 0, 2, 45059, 10, 26 };
            yield return new object[] { 45090, 0, 2, 45060, 10, 31 };
            yield return new object[] { 45091, 0, 2, 45061, 10, 31 };
            yield return new object[] { 45092, 0, 2, 45062, 10, 31 };
            yield return new object[] { 45093, 0, 2, 45063, 10, 36 };
            yield return new object[] { 45094, 0, 2, 45064, 10, 36 };
            yield return new object[] { 45095, 0, 2, 45065, 10, 36 };
            yield return new object[] { 45096, 0, 2, 45066, 10, 41 };
            yield return new object[] { 45097, 0, 2, 45067, 10, 41 };
            yield return new object[] { 45098, 0, 2, 45068, 10, 41 };
            yield return new object[] { 45099, 0, 2, 45069, 10, 46 };
            yield return new object[] { 45100, 0, 2, 45070, 10, 46 };
            yield return new object[] { 45101, 0, 2, 45071, 10, 46 };
            yield return new object[] { 45110, 0, 5, 45102, 100, 1 };
            yield return new object[] { 45111, 0, 6, 45103, 100, 1 };
            yield return new object[] { 45112, 0, 8, 45104, 100, 1 };
            yield return new object[] { 45113, 0, 9, 45105, 100, 1 };
            yield return new object[] { 45114, 0, 5, 42272, 100, 1 };
            yield return new object[] { 45115, 0, 6, 42273, 100, 1 };
            yield return new object[] { 45116, 0, 8, 42274, 100, 1 };
            yield return new object[] { 45117, 0, 9, 42275, 100, 1 };
            yield return new object[] { 45118, 0, 5, 45106, 100, 1 };
            yield return new object[] { 45119, 0, 6, 45107, 100, 1 };
            yield return new object[] { 45120, 0, 8, 45108, 100, 1 };
            yield return new object[] { 45121, 0, 9, 45109, 100, 1 };
            yield return new object[] { 45658, 0, 6, 44859, 10, 1 };
            yield return new object[] { 110944, 0, 8, 50259, 10, 1 };
            yield return new object[] { 110945, 0, 8, 50256, 10, 1 };
            yield return new object[] { 110946, 0, 8, 50261, 10, 1 };
            yield return new object[] { 110947, 0, 8, 50257, 10, 1 };
            yield return new object[] { 110948, 0, 8, 50258, 10, 1 };
            yield return new object[] { 110949, 0, 8, 50260, 10, 1 };
            yield return new object[] { 110950, 0, 8, 50264, 10, 1 };
            yield return new object[] { 110951, 0, 8, 50262, 10, 1 };
            yield return new object[] { 110952, 0, 8, 50263, 10, 1 };
            yield return new object[] { 110953, 0, 8, 50273, 10, 1 };
            yield return new object[] { 110954, 0, 8, 50271, 10, 1 };
            yield return new object[] { 110955, 0, 8, 50272, 10, 1 };
            yield return new object[] { 110956, 0, 8, 50270, 10, 1 };
            yield return new object[] { 110957, 0, 8, 50268, 10, 1 };
            yield return new object[] { 110958, 0, 8, 50269, 10, 1 };
            yield return new object[] { 110959, 0, 8, 50267, 10, 1 };
            yield return new object[] { 110960, 0, 8, 50265, 10, 1 };
            yield return new object[] { 110961, 0, 8, 50266, 10, 1 };
            yield return new object[] { 110962, 0, 8, 50285, 10, 1 };
            yield return new object[] { 110963, 0, 8, 50283, 10, 1 };
            yield return new object[] { 110964, 0, 8, 50284, 10, 1 };
            yield return new object[] { 110965, 0, 8, 50279, 10, 1 };
            yield return new object[] { 110966, 0, 8, 50277, 10, 1 };
            yield return new object[] { 110967, 0, 8, 50278, 10, 1 };
            yield return new object[] { 110968, 0, 8, 50282, 10, 1 };
            yield return new object[] { 110969, 0, 8, 50280, 10, 1 };
            yield return new object[] { 110970, 0, 8, 50281, 10, 1 };
            yield return new object[] { 110971, 0, 8, 50276, 10, 1 };
            yield return new object[] { 110972, 0, 8, 50274, 10, 1 };
            yield return new object[] { 110973, 0, 8, 50275, 10, 1 };
            yield return new object[] { 110974, 0, 8, 50291, 10, 1 };
            yield return new object[] { 110975, 0, 8, 50289, 10, 1 };
            yield return new object[] { 110976, 0, 8, 50290, 10, 1 };
            yield return new object[] { 110977, 0, 8, 50288, 10, 1 };
            yield return new object[] { 110978, 0, 8, 50286, 10, 1 };
            yield return new object[] { 110979, 0, 8, 50287, 10, 1 };
            yield return new object[] { 110987, 0, 3, 638, 100, 1 };
            yield return new object[] { 110988, 0, 5, 56, 250, 1 };
            yield return new object[] { 110989, 0, 6, 110894, 250, 11 };
            yield return new object[] { 110990, 0, 8, 110896, 250, 21 };
            yield return new object[] { 110991, 0, 9, 110897, 250, 31 };
            yield return new object[] { 110992, 0, 10, 110902, 250, 41 };
            yield return new object[] { 110993, 0, 5, 32, 100, 1 };
            yield return new object[] { 110994, 0, 6, 110898, 100, 11 };
            yield return new object[] { 110995, 0, 8, 110899, 100, 21 };
            yield return new object[] { 110996, 0, 9, 110901, 100, 31 };
            yield return new object[] { 110997, 0, 10, 110900, 100, 41 };
            yield return new object[] { 110998, 0, 6, 110903, 500, 11 };
            yield return new object[] { 110999, 0, 8, 110904, 500, 21 };
            yield return new object[] { 111000, 0, 9, 110905, 500, 31 };
            yield return new object[] { 111001, 0, 10, 110906, 500, 41 };
            yield return new object[] { 111002, 0, 5, 636, 100, 1 };
            yield return new object[] { 111003, 0, 6, 110907, 100, 11 };
            yield return new object[] { 111004, 0, 8, 110908, 100, 21 };
            yield return new object[] { 111005, 0, 9, 110909, 100, 31 };
            yield return new object[] { 111006, 0, 10, 110910, 100, 41 };
            yield return new object[] { 111007, 0, 6, 110911, 50, 11 };
            yield return new object[] { 111008, 0, 8, 110912, 50, 21 };
            yield return new object[] { 111009, 0, 9, 110913, 50, 31 };
            yield return new object[] { 111010, 0, 10, 110914, 50, 41 };
            yield return new object[] { 111052, 0, 5, 111022, 25, 1 };
            yield return new object[] { 111053, 0, 6, 111023, 25, 6 };
            yield return new object[] { 111054, 0, 8, 111024, 25, 11 };
            yield return new object[] { 111055, 0, 9, 111025, 25, 16 };
            yield return new object[] { 111056, 0, 10, 111026, 25, 21 };
            yield return new object[] { 111057, 0, 11, 111027, 25, 26 };
            yield return new object[] { 111058, 0, 12, 111028, 25, 31 };
            yield return new object[] { 111059, 0, 13, 111029, 25, 36 };
            yield return new object[] { 111060, 0, 13, 111030, 25, 41 };
            yield return new object[] { 111061, 0, 14, 111031, 25, 46 };
            yield return new object[] { 111062, 0, 5, 111032, 25, 1 };
            yield return new object[] { 111063, 0, 6, 111033, 25, 6 };
            yield return new object[] { 111064, 0, 8, 111034, 25, 11 };
            yield return new object[] { 111065, 0, 9, 111035, 25, 16 };
            yield return new object[] { 111066, 0, 10, 111036, 25, 21 };
            yield return new object[] { 111067, 0, 11, 111037, 25, 26 };
            yield return new object[] { 111068, 0, 12, 111038, 25, 31 };
            yield return new object[] { 111069, 0, 13, 111039, 25, 36 };
            yield return new object[] { 111070, 0, 13, 111040, 25, 41 };
            yield return new object[] { 111071, 0, 14, 111041, 25, 46 };
            yield return new object[] { 111072, 0, 5, 111042, 25, 1 };
            yield return new object[] { 111073, 0, 6, 111043, 25, 6 };
            yield return new object[] { 111074, 0, 8, 111044, 25, 11 };
            yield return new object[] { 111075, 0, 9, 111045, 25, 16 };
            yield return new object[] { 111076, 0, 10, 111046, 25, 21 };
            yield return new object[] { 111077, 0, 11, 111047, 25, 26 };
            yield return new object[] { 111078, 0, 12, 111048, 25, 31 };
            yield return new object[] { 111079, 0, 13, 111049, 25, 36 };
            yield return new object[] { 111080, 0, 13, 111050, 25, 41 };
            yield return new object[] { 111081, 0, 14, 111051, 25, 46 };
            yield return new object[] { 111082, 0, 5, 45125, 25, 1 };
            yield return new object[] { 111083, 0, 6, 45441, 25, 6 };
            yield return new object[] { 111084, 0, 8, 45442, 25, 11 };
            yield return new object[] { 111085, 0, 9, 45443, 25, 16 };
            yield return new object[] { 111086, 0, 10, 45444, 25, 21 };
            yield return new object[] { 111087, 0, 11, 45445, 25, 26 };
            yield return new object[] { 111088, 0, 12, 45446, 25, 31 };
            yield return new object[] { 111089, 0, 13, 45447, 25, 36 };
            yield return new object[] { 111090, 0, 13, 45448, 25, 41 };
            yield return new object[] { 111091, 0, 14, 45449, 25, 46 };
            yield return new object[] { 111092, 0, 5, 111012, 25, 1 };
            yield return new object[] { 111093, 0, 6, 111013, 25, 6 };
            yield return new object[] { 111094, 0, 8, 111014, 25, 11 };
            yield return new object[] { 111095, 0, 9, 111015, 25, 16 };
            yield return new object[] { 111096, 0, 10, 111016, 25, 21 };
            yield return new object[] { 111097, 0, 11, 111017, 25, 26 };
            yield return new object[] { 111098, 0, 12, 111018, 25, 31 };
            yield return new object[] { 111099, 0, 13, 111019, 25, 36 };
            yield return new object[] { 111100, 0, 13, 111020, 25, 41 };
            yield return new object[] { 111101, 0, 14, 111021, 25, 46 };
            yield return new object[] { 111124, 0, 8, 111128, 1, 1 };
            yield return new object[] { 111125, 0, 8, 111129, 1, 1 };
            yield return new object[] { 111126, 0, 8, 111130, 1, 1 };
            yield return new object[] { 111127, 0, 8, 111131, 1, 1 };
        }
    }
}
