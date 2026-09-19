using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// Zone borders and instance doors, derived from the client's own map-screen data
    /// (generated.client.uimapmarker.maplinkmarkers): the trigger is where the client draws the
    /// link icon on the source map, the arrival is the reciprocal icon on the destination map,
    /// and the arrival yaw faces the destination map's centre. Where two passes join the same
    /// pair of maps the pairing follows their order along the shared border, which reproduces
    /// the two Wilderness/Divide teleporters the C++ Game-Server had measured by hand; those
    /// rows still want a walk-through. Passes get an 8 m radius, instance doors 6 m, and the pads
    /// in the AFS Arena hub 4 m (they are only ~15 m apart). Generated from Rasa\world\map_links.csv.
    /// </summary>
    public class MapLinkPreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, MapLinkEntry.TableName, typeof(MapLinkEntry));
        }

        protected override IEnumerable<object[]> GetRows()
        {
            // id, map_context_id, pos_x, pos_y, pos_z, radius, dest_map_context_id, dest_pos_x, dest_pos_y, dest_pos_z, dest_rotation, kind, enabled, comment
            yield return new object[] { 1, 1148, -965.8018, 175.9764, 634.8506, 8.0, 1220, 297.4436, 142.3409, -580.9113, 2.6548, (byte)0, (byte)1, "divide -> wilderness" };
            yield return new object[] { 2, 1148, 498.4823, 183.8588, 1200.1517, 8.0, 1220, 888.7216, 267.0309, 31.6255, 1.5356, (byte)0, (byte)1, "divide -> wilderness" };
            yield return new object[] { 3, 1148, 834.9438, 78.9839, -714.0618, 8.0, 1244, -832.416, 178.9559, -352.4475, -1.8835, (byte)0, (byte)1, "divide -> palisades" };
            yield return new object[] { 4, 1148, 837.9222, 135.2422, -497.5993, 8.0, 1244, -675.8575, 154.8746, 835.3989, -0.6325, (byte)0, (byte)1, "divide -> palisades" };
            yield return new object[] { 5, 1148, 640.1467, 162.056, -124.0286, 6.0, 1347, -15.822, 29.5111, 122.6093, -0.7466, (byte)1, (byte)1, "divide -> divide_minoscaverns" };
            yield return new object[] { 6, 1148, -573.8266, 188.6077, -1119.4796, 6.0, 1348, -120.049, 205.6074, 546.935, -0.2957, (byte)1, (byte)1, "divide -> divide_timoramines" };
            yield return new object[] { 7, 1148, 169.3163, 205.6077, -1027.8997, 6.0, 1349, -304.0, 13.75, 286.75, -1.0855, (byte)1, (byte)1, "divide -> divide_torcastraprison" };
            yield return new object[] { 8, 1148, -863.5192, 121.6077, -883.2804, 6.0, 1806, 87.7664, 65.8084, 221.6328, 0.2648, (byte)1, (byte)1, "divide -> divide_purgasstation2" };
            yield return new object[] { 9, 1148, -132.8162, 123.9678, 542.1301, 8.0, 20000009, 27.5014, 36.5, -4.7462, 0.4618, (byte)0, (byte)1, "divide -> afs_arena" };
            yield return new object[] { 10, 1220, 297.4436, 142.3409, -580.9113, 8.0, 1148, -965.8018, 175.9764, 634.8506, -0.9681, (byte)0, (byte)1, "wilderness -> divide" };
            yield return new object[] { 11, 1220, 888.7216, 267.0309, 31.6255, 8.0, 1148, 498.4823, 183.8588, 1200.1517, 0.3886, (byte)0, (byte)1, "wilderness -> divide" };
            yield return new object[] { 12, 1220, -24.3419, 254.6502, 690.5391, 6.0, 1416, 163.4648, 314.3326, 200.2778, 0.2462, (byte)1, (byte)1, "wilderness -> wilderness_guardianprom" };
            yield return new object[] { 13, 1220, -786.2202, 233.6148, 71.5645, 6.0, 1430, -193.5236, 9.9638, -257.405, -2.4402, (byte)1, (byte)1, "wilderness -> wilderness_pravusresearch" };
            yield return new object[] { 14, 1220, -438.3519, 164.9596, 260.4733, 6.0, 1506, -275.5707, 23.9565, -374.1113, -2.7987, (byte)1, (byte)1, "wilderness -> wilderness_cavesofdonn02" };
            yield return new object[] { 15, 1220, -408.3123, 275.6469, -749.348, 6.0, 1721, 8.5096, 139.9557, 158.2927, 0.2596, (byte)1, (byte)1, "wilderness -> wilderness_clrf" };
            yield return new object[] { 16, 1220, -141.2292, 232.8332, -619.4121, 6.0, 2327, -285.7884, 208.9081, -706.0602, -2.8579, (byte)1, (byte)1, "wilderness -> manhattan_01" };
            yield return new object[] { 17, 1220, 751.5, 294.2103, 382.5, 8.0, 20000009, 13.1962, 36.5, -4.4361, 0.2332, (byte)0, (byte)1, "wilderness -> afs_arena" };
            yield return new object[] { 18, 1244, -832.416, 178.9559, -352.4475, 8.0, 1148, 834.9438, 78.9839, -714.0618, 2.257, (byte)0, (byte)1, "palisades -> divide" };
            yield return new object[] { 19, 1244, -675.8575, 154.8746, 835.3989, 8.0, 1148, 837.9222, 135.2422, -497.5993, 2.0805, (byte)0, (byte)1, "palisades -> divide" };
            yield return new object[] { 20, 1244, 159.5127, 63.3979, -581.128, 6.0, 1394, 16.7733, 122.6238, -398.1859, -2.7827, (byte)1, (byte)1, "palisades -> palisades_devilsden" };
            yield return new object[] { 21, 1244, 656.0956, 145.6077, -921.4343, 6.0, 1397, -337.4749, 103.7221, 398.6607, -0.5838, (byte)1, (byte)1, "palisades -> palisades_treebackcamp" };
            yield return new object[] { 22, 1244, -768.7433, 189.9627, 47.7394, 6.0, 1803, -1000.3902, 23.0551, 142.9685, -0.8833, (byte)1, (byte)1, "palisades -> palisades_elohtemples" };
            yield return new object[] { 23, 1244, -752.7274, 186.9622, -91.2035, 6.0, 1803, -991.1205, 22.9881, -302.2137, -2.2558, (byte)1, (byte)1, "palisades -> palisades_elohtemples" };
            yield return new object[] { 24, 1244, -675.645, 174.9564, -160.0294, 6.0, 1803, -738.7897, 22.9892, -322.2028, -3.0796, (byte)1, (byte)1, "palisades -> palisades_elohtemples" };
            yield return new object[] { 25, 1244, -439.8997, 172.9534, 130.5957, 6.0, 1803, -510.9673, 22.9785, -254.5663, 2.2459, (byte)1, (byte)1, "palisades -> palisades_elohtemples" };
            yield return new object[] { 26, 1304, 147.8214, 672.8627, -607.3924, 6.0, 1429, -211.8138, 106.7688, -219.0292, -2.6685, (byte)1, (byte)1, "pools -> marshes_wetlandrefinery" };
            yield return new object[] { 27, 1304, 708.4897, 678.9558, 733.0703, 8.0, 1454, -598.5038, 224.5517, -833.0043, -2.5186, (byte)0, (byte)1, "pools -> marshes" };
            yield return new object[] { 28, 1304, -246.6428, 685.739, -288.124, 6.0, 1465, 409.1057, 9.5831, -15.9087, 1.8722, (byte)1, (byte)1, "pools -> pools_test_weapons_center" };
            yield return new object[] { 29, 1304, -731.891, 970.9529, 776.0477, 8.0, 1497, -235.405, 418.9826, -1033.3643, -2.9144, (byte)0, (byte)1, "pools -> plateau" };
            yield return new object[] { 30, 1304, -445.8508, 886.9537, 751.579, 6.0, 1694, 19.321, 82.5442, -4.3964, -2.9672, (byte)1, (byte)1, "pools -> pools_retread_caves" };
            yield return new object[] { 31, 1347, -15.822, 29.5111, 122.6093, 6.0, 1148, 640.1467, 162.056, -124.0286, 1.7203, (byte)1, (byte)1, "divide_minoscaverns -> divide" };
            yield return new object[] { 32, 1348, -120.049, 205.6074, 546.935, 6.0, 1148, -573.8266, 188.6077, -1119.4796, -2.6608, (byte)1, (byte)1, "divide_timoramines -> divide" };
            yield return new object[] { 33, 1349, -304.0, 13.75, 286.75, 6.0, 1148, 169.3163, 205.6077, -1027.8997, 2.9701, (byte)1, (byte)1, "divide_torcastraprison -> divide" };
            yield return new object[] { 34, 1394, 16.7733, 122.6238, -398.1859, 6.0, 1244, 159.5127, 63.3979, -581.128, 2.8275, (byte)1, (byte)1, "palisades_devilsden -> palisades" };
            yield return new object[] { 35, 1397, -337.4749, 103.7221, 398.6607, 6.0, 1244, 656.0956, 145.6077, -921.4343, 2.4755, (byte)1, (byte)1, "palisades_treebackcamp -> palisades" };
            yield return new object[] { 36, 1416, 163.4648, 314.3326, 200.2778, 6.0, 1220, -24.3419, 254.6502, 690.5391, -0.0208, (byte)1, (byte)1, "wilderness_guardianprom -> wilderness" };
            yield return new object[] { 37, 1429, -211.8138, 106.7688, -219.0292, 6.0, 1304, 147.8214, 672.8627, -607.3924, 2.8971, (byte)1, (byte)1, "marshes_wetlandrefinery -> pools" };
            yield return new object[] { 38, 1430, -193.5236, 9.9638, -257.405, 6.0, 1220, -786.2202, 233.6148, 71.5645, -1.4789, (byte)1, (byte)1, "wilderness_pravusresearch -> wilderness" };
            yield return new object[] { 39, 1451, -370.5371, 149.7653, 3.1254, 6.0, 1454, 451.5638, 223.6077, -581.938, 2.4817, (byte)1, (byte)1, "marshes_banesupplydepot -> marshes" };
            yield return new object[] { 40, 1454, -598.5038, 224.5517, -833.0043, 8.0, 1304, 708.4897, 678.9558, 733.0703, 0.7582, (byte)0, (byte)1, "marshes -> pools" };
            yield return new object[] { 41, 1454, 451.5638, 223.6077, -581.938, 6.0, 1451, -370.5371, 149.7653, 3.1254, -1.4484, (byte)1, (byte)1, "marshes -> marshes_banesupplydepot" };
            yield return new object[] { 42, 1454, -918.6917, 224.3731, 74.3685, 8.0, 1497, 585.2134, 275.0308, 13.3791, 1.5223, (byte)0, (byte)1, "marshes -> plateau" };
            yield return new object[] { 43, 1454, -318.364, 285.7091, -497.7995, 6.0, 1700, 872.0, 353.75, -129.5, 2.5414, (byte)1, (byte)1, "marshes -> marshes_logosresearchfacility" };
            yield return new object[] { 44, 1454, 755.0804, 215.5619, 727.6724, 6.0, 1743, -138.5, 63.0, -360.0, -2.5735, (byte)1, (byte)1, "marshes -> marshes_villageruins" };
            yield return new object[] { 45, 1465, 409.1057, 9.5831, -15.9087, 6.0, 1304, -246.6428, 685.739, -288.124, -2.4071, (byte)1, (byte)1, "pools_test_weapons_center -> pools" };
            yield return new object[] { 46, 1497, -235.405, 418.9826, -1033.3643, 8.0, 1304, -731.891, 970.9529, 776.0477, -0.7466, (byte)0, (byte)1, "plateau -> pools" };
            yield return new object[] { 47, 1497, 585.2134, 275.0308, 13.3791, 8.0, 1454, -918.6917, 224.3731, 74.3685, -1.49, (byte)0, (byte)1, "plateau -> marshes" };
            yield return new object[] { 48, 1497, 6.9062, 423.7505, -617.006, 6.0, 1830, 186.1141, -130.2347, 127.8795, 0.779, (byte)1, (byte)1, "plateau -> plateau_maligobasev3" };
            yield return new object[] { 49, 1497, -120.1443, 394.0132, 161.1818, 6.0, 2029, -0.0025, 248.3829, 8.7204, -2.467, (byte)1, (byte)1, "plateau -> plateau_temporalchamber" };
            yield return new object[] { 50, 1497, -463.6904, 423.9398, -547.0996, 6.0, 2136, 196.1592, 95.0509, -528.7897, -3.0741, (byte)1, (byte)1, "plateau -> howlingmaw_deathburrow" };
            yield return new object[] { 51, 1497, 26.3794, 413.9873, 828.1976, 8.0, 20000009, -18.8436, 36.5, -4.7228, -0.3285, (byte)0, (byte)1, "plateau -> afs_arena" };
            yield return new object[] { 52, 1506, -275.5707, 23.9565, -374.1113, 6.0, 1220, -438.3519, 164.9596, 260.4733, -1.0245, (byte)1, (byte)1, "wilderness_cavesofdonn02 -> wilderness" };
            yield return new object[] { 53, 1694, 19.321, 82.5442, -4.3964, 6.0, 1304, -445.8508, 886.9537, 751.579, -0.5268, (byte)1, (byte)1, "pools_retread_caves -> pools" };
            yield return new object[] { 54, 1700, 872.0, 353.75, -129.5, 6.0, 1454, -318.364, 285.7091, -497.7995, -2.5726, (byte)1, (byte)1, "marshes_logosresearchfacility -> marshes" };
            yield return new object[] { 55, 1721, 8.5096, 139.9557, 158.2927, 6.0, 1220, -408.3123, 275.6469, -749.348, -2.653, (byte)1, (byte)1, "wilderness_clrf -> wilderness" };
            yield return new object[] { 56, 1734, 80.7304, 318.9353, -955.6914, 8.0, 1911, 874.6416, 426.7241, 670.0789, 0.928, (byte)0, (byte)1, "ashendesert -> thunderhead" };
            yield return new object[] { 57, 1734, 427.7449, 305.6077, 206.7952, 6.0, 1988, 7.25, 153.5, -670.25, -3.1407, (byte)1, (byte)1, "ashendesert -> ashendesert_baneconscriptfacility" };
            yield return new object[] { 58, 1734, 321.8665, 318.9557, 319.6013, 6.0, 2055, -40.689, 8.9327, -421.6495, 2.8648, (byte)1, (byte)1, "ashendesert -> ashendesert_indracaverns" };
            yield return new object[] { 59, 1734, -660.9675, 323.6152, -261.6803, 6.0, 2110, 83.9854, 528.4999, 107.6662, -2.0675, (byte)1, (byte)1, "ashendesert -> ashendesert_avernusoutpost" };
            yield return new object[] { 60, 1743, -138.5, 63.0, -360.0, 6.0, 1454, 755.0804, 215.5619, 727.6724, 0.8039, (byte)1, (byte)1, "marshes_villageruins -> marshes" };
            yield return new object[] { 61, 1759, 549.8995, 233.6077, 840.809, 8.0, 1761, -361.8102, 220.0052, -501.7468, -2.3618, (byte)0, (byte)1, "mires -> incline" };
            yield return new object[] { 62, 1759, 811.5798, 230.9652, -195.9991, 8.0, 1764, -1149.3304, 383.0512, -194.3236, -1.7383, (byte)0, (byte)1, "mires -> plains" };
            yield return new object[] { 63, 1759, -280.1593, 221.6077, 340.4434, 6.0, 2107, 346.8038, 97.781, -0.0524, 1.3344, (byte)1, (byte)1, "mires -> mires_energyweaponcenter" };
            yield return new object[] { 64, 1759, -414.943, 242.6077, 966.0897, 6.0, 2115, -38.0, 144.75, -20.0, -0.8543, (byte)1, (byte)1, "mires -> mires_banefluxitemines" };
            yield return new object[] { 65, 1759, -824.4988, 261.8089, 70.672, 6.0, 2125, 196.0, 120.5, 10.5, 1.5332, (byte)1, (byte)1, "mires -> mires_tahrendrabase" };
            yield return new object[] { 66, 1761, -361.8102, 220.0052, -501.7468, 8.0, 1759, 549.8995, 233.6077, 840.809, 0.5534, (byte)0, (byte)1, "incline -> mires" };
            yield return new object[] { 67, 1761, 148.7533, 230.3231, -392.572, 8.0, 1764, -223.0389, 421.7836, 761.8707, -0.2848, (byte)0, (byte)1, "incline -> plains" };
            yield return new object[] { 68, 1761, -548.7698, 301.0576, 344.2102, 6.0, 1865, 553.7024, 88.1357, 146.5004, 1.3747, (byte)1, (byte)1, "incline -> incline_ojasaattahive" };
            yield return new object[] { 69, 1761, 4.9918, 243.1625, -297.7522, 6.0, 2085, 98.511, 246.9496, -84.0725, 1.7543, (byte)1, (byte)1, "incline -> incline_commtower" };
            yield return new object[] { 70, 1761, 363.3616, 238.7383, -280.7301, 6.0, 2111, -280.0001, 104.2607, 99.0213, -0.7294, (byte)1, (byte)1, "incline -> incline_wardenbotfactory" };
            yield return new object[] { 71, 1763, -144.0, 185.5, -329.5, 6.0, 2051, 1148.75, 247.5, 887.25, 0.9372, (byte)1, (byte)1, "pools_livetargetpensv2 -> howlingmaw1" };
            yield return new object[] { 72, 1764, -1149.3304, 383.0512, -194.3236, 8.0, 1759, 811.5798, 230.9652, -195.9991, 1.6829, (byte)0, (byte)1, "plains -> mires" };
            yield return new object[] { 73, 1764, -223.0389, 421.7836, 761.8707, 8.0, 1761, 148.7533, 230.3231, -392.572, 2.9303, (byte)0, (byte)1, "plains -> incline" };
            yield return new object[] { 74, 1764, -510.4457, 432.2103, -60.3465, 6.0, 1773, -25.749, 312.1235, 184.0261, -0.5353, (byte)1, (byte)1, "plains -> plains_attacolony" };
            yield return new object[] { 75, 1764, 54.5, 401.5, -279.0, 6.0, 2034, 24.1922, 0.5, -0.0818, 1.5321, (byte)1, (byte)1, "plains -> plains_penalresearch" };
            yield return new object[] { 76, 1764, 768.5186, 422.5, 507.4648, 6.0, 2093, -34.0558, 423.5, -78.5776, 2.7368, (byte)1, (byte)1, "plains -> plains_brannwaterrefinery" };
            yield return new object[] { 77, 1764, -612.5, 438.9873, 417.0, 8.0, 20000009, 12.1857, 36.5, -28.7285, 0.3716, (byte)0, (byte)1, "plains -> afs_arena" };
            yield return new object[] { 78, 1773, -25.749, 312.1235, 184.0261, 6.0, 1764, -510.4457, 432.2103, -60.3465, -1.6885, (byte)1, (byte)1, "plains_attacolony -> plains" };
            yield return new object[] { 79, 1803, -1000.3902, 23.0551, 142.9685, 6.0, 1244, -768.7433, 189.9627, 47.7394, -1.4006, (byte)1, (byte)1, "palisades_elohtemples -> palisades" };
            yield return new object[] { 80, 1803, -991.1205, 22.9881, -302.2137, 6.0, 1244, -752.7274, 186.9622, -91.2035, -1.5804, (byte)1, (byte)1, "palisades_elohtemples -> palisades" };
            yield return new object[] { 81, 1803, -738.7897, 22.9892, -322.2028, 6.0, 1244, -675.645, 174.9564, -160.0294, -1.6832, (byte)1, (byte)1, "palisades_elohtemples -> palisades" };
            yield return new object[] { 82, 1803, -510.9673, 22.9785, -254.5663, 6.0, 1244, -439.8997, 172.9534, 130.5957, -1.1151, (byte)1, (byte)1, "palisades_elohtemples -> palisades" };
            yield return new object[] { 83, 1806, 87.7664, 65.8084, 221.6328, 6.0, 1148, -863.5192, 121.6077, -883.2804, -2.3543, (byte)1, (byte)1, "divide_purgasstation2 -> divide" };
            yield return new object[] { 84, 1830, 186.1141, -130.2347, 127.8795, 6.0, 1497, 6.9062, 423.7505, -617.006, 3.1301, (byte)1, (byte)1, "plateau_maligobasev3 -> plateau" };
            yield return new object[] { 85, 1865, 553.7024, 88.1357, 146.5004, 6.0, 1761, -548.7698, 301.0576, 344.2102, -0.9995, (byte)1, (byte)1, "incline_ojasaattahive -> incline" };
            yield return new object[] { 86, 1911, 874.6416, 426.7241, 670.0789, 8.0, 1734, 80.7304, 318.9353, -955.6914, 3.0586, (byte)0, (byte)1, "thunderhead -> ashendesert" };
            yield return new object[] { 87, 1911, -679.8913, 379.3535, -682.7216, 8.0, 1993, -810.8849, 292.1281, 727.7288, -0.8394, (byte)0, (byte)1, "thunderhead -> burningsteps" };
            yield return new object[] { 88, 1911, 130.205, 367.8272, -539.3315, 8.0, 1993, -95.5297, 267.7878, 732.7974, -0.1296, (byte)0, (byte)1, "thunderhead -> burningsteps" };
            yield return new object[] { 89, 1911, 0.3894, 323.6074, -14.9405, 6.0, 2103, 0.5, 325.75, 120.0, 0.0376, (byte)1, (byte)1, "thunderhead -> thunderhead_faultlever" };
            yield return new object[] { 90, 1911, -104.0988, 435.6077, 705.4872, 6.0, 2105, 9.8535, 46.2043, -197.0577, 1.5492, (byte)1, (byte)1, "thunderhead -> thunderhead_quassostation" };
            yield return new object[] { 91, 1911, -955.0184, 390.0408, 111.8128, 6.0, 2112, 36.8221, 152.1395, -453.9412, 2.8755, (byte)1, (byte)1, "thunderhead -> thunderhead_rivasaattacolony" };
            yield return new object[] { 92, 1977, -322.3497, 212.7524, -460.3171, 6.0, 2028, -533.7186, 538.9537, 120.0867, -1.3692, (byte)1, (byte)1, "burningsteps_magmacaverns -> abyss" };
            yield return new object[] { 93, 1988, 7.25, 153.5, -670.25, 6.0, 1734, 427.7449, 305.6077, 206.7952, 1.4082, (byte)1, (byte)1, "ashendesert_baneconscriptfacility -> ashendesert" };
            yield return new object[] { 94, 1993, -810.8849, 292.1281, 727.7288, 8.0, 1911, -679.8913, 379.3535, -682.7216, -2.3691, (byte)0, (byte)1, "burningsteps -> thunderhead" };
            yield return new object[] { 95, 1993, -95.5297, 267.7878, 732.7974, 8.0, 1911, 130.205, 367.8272, -539.3315, 2.9109, (byte)0, (byte)1, "burningsteps -> thunderhead" };
            yield return new object[] { 96, 1993, -492.1312, 236.9487, -149.0594, 6.0, 2138, 29.6931, 241.2155, -336.8526, 3.0537, (byte)1, (byte)1, "burningsteps -> staaljunkyard" };
            yield return new object[] { 97, 1993, -216.0389, 180.5, 419.6967, 6.0, 2141, 18.2732, 70.5, -131.3076, 0.2241, (byte)1, (byte)1, "burningsteps -> crucible_incurablesward" };
            yield return new object[] { 98, 1993, -528.5979, 249.4359, 581.6433, 6.0, 2146, -31.9056, 17.2305, -282.1061, -3.0977, (byte)1, (byte)1, "burningsteps -> crucible_wbfacility" };
            yield return new object[] { 99, 1993, 1016.0, 144.5, 78.0, 8.0, 20000009, -18.5192, 36.5, -28.9129, -0.5373, (byte)0, (byte)1, "burningsteps -> afs_arena" };
            yield return new object[] { 100, 2028, -533.7186, 538.9537, 120.0867, 6.0, 1977, -322.3497, 212.7524, -460.3171, 3.0801, (byte)1, (byte)1, "abyss -> burningsteps_magmacaverns" };
            yield return new object[] { 101, 2028, 573.5804, 569.4999, 78.5877, 6.0, 2155, 140.5483, 56.0, 143.2538, 0.5304, (byte)1, (byte)1, "abyss -> abyss_tampei" };
            yield return new object[] { 102, 2028, 630.6979, 537.6074, 283.3885, 6.0, 2190, -65.75, 321.75, -568.0, -2.9698, (byte)1, (byte)1, "abyss -> abyss_dybukkar" };
            yield return new object[] { 103, 2028, -868.0, 544.5, -81.75, 6.0, 2203, 0.0773, 268.5006, -490.2964, 3.1415, (byte)1, (byte)1, "abyss -> abyss_omegalabs" };
            yield return new object[] { 104, 2028, -397.756, 532.5215, -346.0342, 8.0, 20000009, 27.5964, 36.5, -28.6574, 0.7219, (byte)0, (byte)1, "abyss -> afs_arena" };
            yield return new object[] { 105, 2029, -0.0025, 248.3829, 8.7204, 6.0, 1497, -120.1443, 394.0132, 161.1818, -0.5985, (byte)1, (byte)1, "plateau_temporalchamber -> plateau" };
            yield return new object[] { 106, 2034, 24.1922, 0.5, -0.0818, 6.0, 1764, 54.5, 401.5, -279.0, 2.9487, (byte)1, (byte)1, "plains_penalresearch -> plains" };
            yield return new object[] { 107, 2047, 26.9251, 444.5034, -317.1158, 6.0, 2163, 188.1737, 32.5007, 33.9795, -1.989, (byte)1, (byte)1, "descent -> descent_inferno_outpost" };
            yield return new object[] { 108, 2051, 1148.75, 247.5, 887.25, 6.0, 1763, -144.0, 185.5, -329.5, -2.6957, (byte)1, (byte)1, "howlingmaw1 -> pools_livetargetpensv2" };
            yield return new object[] { 109, 2051, 1349.6243, 244.584, 492.1679, 6.0, 2162, -25.5226, 212.0938, -460.0758, -3.0862, (byte)1, (byte)1, "howlingmaw1 -> howlingmaw_cuthahbase" };
            yield return new object[] { 110, 2055, -40.689, 8.9327, -421.6495, 6.0, 1734, 321.8665, 318.9557, 319.6013, 1.0632, (byte)1, (byte)1, "ashendesert_indracaverns -> ashendesert" };
            yield return new object[] { 111, 2085, 98.511, 246.9496, -84.0725, 6.0, 1761, 4.9918, 243.1625, -297.7522, -2.8498, (byte)1, (byte)1, "incline_commtower -> incline" };
            yield return new object[] { 112, 2093, -34.0558, 423.5, -78.5776, 6.0, 1764, 768.5186, 422.5, 507.4648, 0.9872, (byte)1, (byte)1, "plains_brannwaterrefinery -> plains" };
            yield return new object[] { 113, 2093, -33.75, 423.5, -77.25, 6.0, 1764, 768.5186, 422.5, 507.4648, 0.9872, (byte)1, (byte)1, "plains_brannwaterrefinery -> plains" };
            yield return new object[] { 114, 2103, 0.5, 325.75, 120.0, 6.0, 1911, 0.3894, 323.6074, -14.9405, 3.1286, (byte)1, (byte)1, "thunderhead_faultlever -> thunderhead" };
            yield return new object[] { 115, 2103, 0.5008, 325.7656, 123.6051, 6.0, 1911, 0.3894, 323.6074, -14.9405, 3.1286, (byte)1, (byte)1, "thunderhead_faultlever -> thunderhead" };
            yield return new object[] { 116, 2105, 9.8535, 46.2043, -197.0577, 6.0, 1911, -104.0988, 435.6077, 705.4872, -0.1496, (byte)1, (byte)1, "thunderhead_quassostation -> thunderhead" };
            yield return new object[] { 117, 2107, 346.8038, 97.781, -0.0524, 6.0, 1759, -280.1593, 221.6077, 340.4434, -0.5121, (byte)1, (byte)1, "mires_energyweaponcenter -> mires" };
            yield return new object[] { 118, 2110, 83.9854, 528.4999, 107.6662, 6.0, 1734, -660.9675, 323.6152, -261.6803, -2.1181, (byte)1, (byte)1, "ashendesert_avernusoutpost -> ashendesert" };
            yield return new object[] { 119, 2111, -280.0001, 104.2607, 99.0213, 6.0, 1761, 363.3616, 238.7383, -280.7301, 2.2318, (byte)1, (byte)1, "incline_wardenbotfactory -> incline" };
            yield return new object[] { 120, 2112, 36.8221, 152.1395, -453.9412, 6.0, 1911, -955.0184, 390.0408, 111.8128, -1.4698, (byte)1, (byte)1, "thunderhead_rivasaattacolony -> thunderhead" };
            yield return new object[] { 121, 2115, -38.0, 144.75, -20.0, 6.0, 1759, -414.943, 242.6077, 966.0897, -0.3446, (byte)1, (byte)1, "mires_banefluxitemines -> mires" };
            yield return new object[] { 122, 2125, 196.0, 120.5, 10.5, 6.0, 1759, -824.4988, 261.8089, 70.672, -1.3575, (byte)1, (byte)1, "mires_tahrendrabase -> mires" };
            yield return new object[] { 123, 2136, 196.1592, 95.0509, -528.7897, 6.0, 1497, -463.6904, 423.9398, -547.0996, -2.4248, (byte)1, (byte)1, "howlingmaw_deathburrow -> plateau" };
            yield return new object[] { 124, 2138, 29.6931, 241.2155, -336.8526, 6.0, 1993, -492.1312, 236.9487, -149.0594, -1.8649, (byte)1, (byte)1, "staaljunkyard -> burningsteps" };
            yield return new object[] { 125, 2141, 18.2732, 70.5, -131.3076, 6.0, 1993, -216.0389, 180.5, 419.6967, -0.4754, (byte)1, (byte)1, "crucible_incurablesward -> burningsteps" };
            yield return new object[] { 126, 2146, -31.9056, 17.2305, -282.1061, 6.0, 1993, -528.5979, 249.4359, 581.6433, -0.7377, (byte)1, (byte)1, "crucible_wbfacility -> burningsteps" };
            yield return new object[] { 127, 2155, 140.5483, 56.0, 143.2538, 6.0, 2028, 573.5804, 569.4999, 78.5877, 1.4535, (byte)1, (byte)1, "abyss_tampei -> abyss" };
            yield return new object[] { 128, 2162, -25.5226, 212.0938, -460.0758, 6.0, 2051, 1349.6243, 244.584, 492.1679, 1.2347, (byte)1, (byte)1, "howlingmaw_cuthahbase -> howlingmaw1" };
            yield return new object[] { 129, 2163, 188.1737, 32.5007, 33.9795, 6.0, 2047, 26.9251, 444.5034, -317.1158, 3.0569, (byte)1, (byte)1, "descent_inferno_outpost -> descent" };
            yield return new object[] { 130, 2190, -65.75, 321.75, -568.0, 6.0, 2028, 630.6979, 537.6074, 283.3885, 1.1631, (byte)1, (byte)1, "abyss_dybukkar -> abyss" };
            yield return new object[] { 131, 2203, 0.0773, 268.5006, -490.2964, 6.0, 2028, -868.0, 544.5, -81.75, -1.6772, (byte)1, (byte)1, "abyss_omegalabs -> abyss" };
            yield return new object[] { 132, 2327, -285.7884, 208.9081, -706.0602, 6.0, 1220, -141.2292, 232.8332, -619.4121, -2.9328, (byte)1, (byte)1, "manhattan_01 -> wilderness" };
            yield return new object[] { 133, 2327, -1.9697, 193.9143, 486.6048, 6.0, 1220, -141.2292, 232.8332, -619.4121, -2.9328, (byte)1, (byte)1, "manhattan_01 -> wilderness" };
            yield return new object[] { 134, 2368, -275.903, 23.9539, -374.8343, 6.0, 20000009, -8.8308, 40.5004, 135.9668, -0.045, (byte)1, (byte)1, "wilderness_cavesofdonn_epic -> afs_arena" };
            yield return new object[] { 135, 20000009, 27.5014, 36.5, -4.7462, 4.0, 1148, -132.8162, 123.9678, 542.1301, -0.2226, (byte)0, (byte)1, "afs_arena -> divide" };
            yield return new object[] { 136, 20000009, 13.1962, 36.5, -4.4361, 4.0, 1220, 751.5, 294.2103, 382.5, 1.1053, (byte)0, (byte)1, "afs_arena -> wilderness" };
            yield return new object[] { 137, 20000009, -18.8436, 36.5, -4.7228, 4.0, 1497, 26.3794, 413.9873, 828.1976, 0.0313, (byte)0, (byte)1, "afs_arena -> plateau" };
            yield return new object[] { 138, 20000009, 12.1857, 36.5, -28.7285, 4.0, 1764, -612.5, 438.9873, 417.0, -0.9731, (byte)0, (byte)1, "afs_arena -> plains" };
            yield return new object[] { 139, 20000009, -18.5192, 36.5, -28.9129, 4.0, 1993, 1016.0, 144.5, 78.0, 1.4942, (byte)0, (byte)1, "afs_arena -> burningsteps" };
            yield return new object[] { 140, 20000009, 27.5964, 36.5, -28.6574, 4.0, 2028, -397.756, 532.5215, -346.0342, -2.3023, (byte)0, (byte)1, "afs_arena -> abyss" };
            yield return new object[] { 141, 20000009, -8.8308, 40.5004, 135.9668, 4.0, 2368, -275.903, 23.9539, -374.8343, -2.7986, (byte)1, (byte)1, "afs_arena -> wilderness_cavesofdonn_epic" };
        }
    }
}
