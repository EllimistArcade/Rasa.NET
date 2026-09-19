using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// One crafting station per CRAFTING_STATION marker on the client's map screen
    /// (generated.client.uimapmarker), all of class 9595 UsableCraftingHumStationV01, the only
    /// entity class with the Kraftwerks augmentation. The markers carry no facing, so every row
    /// starts at rotation 0; .kraftwerks id rotate fixes the ones that stand against a wall.
    /// Generated from Rasa\world\markers.csv.
    /// </summary>
    public class KraftwerksPreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, KraftwerksEntry.TableName, typeof(KraftwerksEntry));
        }

        protected override IEnumerable<object[]> GetRows()
        {
            // id, class_id, map_context_id, pos_x, pos_y, pos_z, rotation, comment
            yield return new object[] { 1, 9595, 1148, 111.2339, 85.0208, -62.7060, 0.0, "foreas_concordia_divide - Foxtrot Outpost" };
            yield return new object[] { 2, 9595, 1148, -314.2747, 56.8350, 87.6873, 0.0, "foreas_concordia_divide - Hydro Plant" };
            yield return new object[] { 3, 9595, 1148, 32.9999, 48.0000, 479.9300, 0.0, "foreas_concordia_divide - Foreas Base" };
            yield return new object[] { 4, 9595, 1220, 797.8405, 294.3154, 359.7303, 0.0, "foreas_concordia_wilderness - Alia Das" };
            yield return new object[] { 5, 9595, 1220, -606.1458, 276.6041, 854.6699, 0.0, "foreas_concordia_wilderness - Daghda's Urn" };
            yield return new object[] { 6, 9595, 1220, -140.2918, 220.5689, -535.7525, 0.0, "foreas_concordia_wilderness - Twin Pillars" };
            yield return new object[] { 7, 9595, 1244, -743.0000, 139.1360, 609.7500, 0.0, "foreas_concordia_palisades - Cumbria Research Facility" };
            yield return new object[] { 8, 9595, 1304, 1003.9401, 656.4999, 368.5179, 0.0, "foreas_valverde_pools - East Listening Post Crafting Station" };
            yield return new object[] { 9, 9595, 1454, -318.4348, 218.3305, 87.9550, 0.0, "foreas_valverde_marshes - Paludos" };
            yield return new object[] { 10, 9595, 1454, -269.7494, 216.8921, -832.5015, 0.0, "foreas_valverde_marshes - Retread City" };
            yield return new object[] { 11, 9595, 1497, -607.4917, 442.6828, -220.7404, 0.0, "foreas_valverde_plateau - Camp Resistance" };
            yield return new object[] { 12, 9595, 1497, -48.0040, 399.3475, 891.2266, 0.0, "foreas_valverde_plateau - Fort Defiance" };
            yield return new object[] { 13, 9595, 1497, -57.0040, 399.3475, 891.2266, 0.0, "foreas_valverde_plateau - Fort Defiance" };
            yield return new object[] { 14, 9595, 1497, 485.9565, 378.4069, 339.1709, 0.0, "foreas_valverde_plateau - Wedge Rock Outpost" };
            yield return new object[] { 15, 9595, 1734, -68.0000, 276.5000, -976.0000, 0.0, "arieki_ligo_ashendesert - Shadow's Edge Post" };
            yield return new object[] { 16, 9595, 1759, -572.3752, 254.0000, -524.9523, 0.0, "arieki_torden_mires - Baylor Base Crafting Stations" };
            yield return new object[] { 17, 9595, 1761, 316.5944, 260.2909, 14.6803, 0.0, "arieki_torden_incline - Ortho Outpost" };
            yield return new object[] { 18, 9595, 1764, -562.0000, 438.0000, 392.5000, 0.0, "arieki_torden_plains - Mt. Hellas Outpost" };
            yield return new object[] { 19, 9595, 1764, 375.0902, 433.3058, -103.8618, 0.0, "arieki_torden_plains - Irendas Penal Colony" };
            yield return new object[] { 20, 9595, 1911, 706.3472, 341.2426, -499.4954, 0.0, "arieki_ligo_thunderhead - Thunderhead Base" };
            yield return new object[] { 21, 9595, 1993, 1039.5886, 144.5000, 59.9383, 0.0, "arieki_ligo_burningsteps - Outpost Intrepid" };
            yield return new object[] { 22, 9595, 1993, -192.5071, 180.5000, 264.1511, 0.0, "arieki_ligo_burningsteps - Staal Centrum" };
            yield return new object[] { 23, 9595, 2028, -611.4385, 536.4999, -109.7810, 0.0, "arieki_torden_abyss - Penumbra" };
            yield return new object[] { 24, 9595, 2028, -599.9240, 536.4999, -100.4250, 0.0, "arieki_torden_abyss - Penumbra" };
            yield return new object[] { 25, 9595, 2028, -588.6433, 536.4999, -110.0527, 0.0, "arieki_torden_abyss - Penumbra" };
            yield return new object[] { 26, 9595, 2047, -321.5000, 713.9999, -635.0000, 0.0, "foreas_valverde_descent - Fort Virgil" };
            yield return new object[] { 27, 9595, 2047, -321.7500, 713.9999, -643.0000, 0.0, "foreas_valverde_descent - Fort Virgil" };
            yield return new object[] { 28, 9595, 2047, -321.5000, 713.9999, -651.0000, 0.0, "foreas_valverde_descent - Fort Virgil" };
            yield return new object[] { 29, 9595, 2051, -660.2500, 200.7500, -1022.7500, 0.0, "foreas_howlingmaw1 - Gangus Outpost" };
            yield return new object[] { 30, 9595, 2051, -660.7500, 200.7500, -1014.0000, 0.0, "foreas_howlingmaw1 - Gangus Outpost" };
            yield return new object[] { 31, 9595, 2265, 797.8405, 294.3154, 359.7303, 0.0, "wargame_foreas_concordia_wilderness_revisited - Alia Das" };
            yield return new object[] { 32, 9595, 2265, -606.1458, 276.6041, 854.6699, 0.0, "wargame_foreas_concordia_wilderness_revisited - Daghda's Urn" };
            yield return new object[] { 33, 9595, 2265, -140.2918, 220.5689, -535.7525, 0.0, "wargame_foreas_concordia_wilderness_revisited - Twin Pillars" };
            yield return new object[] { 34, 9595, 2373, -607.4917, 442.6828, -220.7404, 0.0, "foreas_valverde_plateau_wargame - Camp Resistance" };
            yield return new object[] { 35, 9595, 2373, -48.0040, 399.3475, 891.2266, 0.0, "foreas_valverde_plateau_wargame - Fort Defiance" };
            yield return new object[] { 36, 9595, 2373, -57.0040, 399.3475, 891.2266, 0.0, "foreas_valverde_plateau_wargame - Fort Defiance" };
            yield return new object[] { 37, 9595, 2373, 485.9565, 378.4069, 339.1709, 0.0, "foreas_valverde_plateau_wargame - Wedge Rock Outpost" };
            yield return new object[] { 38, 9595, 20000009, -30.0000, 41.9400, 184.0000, 0.0, "afs_arena - CELLAR Arena" };
            yield return new object[] { 39, 9595, 20000009, 30.0000, 41.9453, 184.0000, 0.0, "afs_arena - CELLAR Arena" };
        }
    }
}
