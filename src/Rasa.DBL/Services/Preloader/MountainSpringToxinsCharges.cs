using System.Collections.Generic;
using System.Text.Json;

using Rasa.Missions.Content;
using Rasa.Missions.Scenes;
using Rasa.Services.Preloader.Missions.Plateau;

using M = Rasa.Services.Preloader.Missions.Plateau.PlateauMountainSpringToxinsV1;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Mountain Spring Toxins' chemical towers in Velon Hollow, each with a charge at its foot to set the bomb at
    /// (Mountain_spring_toxins_charges). A tower was the thing used, and the client measures a use to the model's DAMAGE1
    /// point; arch_bane_water_converter_tower_v01 has none, so it measures to the model's root, in the middle of the
    /// 40 by 16 m structure: highlighted, and out of the 6 m use range from anywhere a player would stand to use it.
    /// The charge (UsableBombHumV01, its DAMAGE1 point at its foot) takes the bomb and blows the tower, by the tower's
    /// own sequence (M.Charges, M.Scene).
    ///
    /// The stored scene (mission_scene_binding.bindings, as MissionContentCodec writes it) is replaced whole where it has
    /// no charge yet; Down puts back the scene without them where it has them. Not by the old text itself: what the
    /// codec writes for the same scene has grown fields since it was first stored. Plain SQL for both providers. A scene
    /// a player has already started keeps the actors it made, so it has no charges: its towers are still used
    /// themselves.
    /// </summary>
    public static class MountainSpringToxinsCharges
    {
        public const uint Mission = M.MissionId;
        public const string Revision = PlateauMissionDataV1.Revision;

        public static string Before => JsonSerializer.Serialize(M.Scene(charges: false), MissionContentCodec.Options);
        public static string After => JsonSerializer.Serialize(M.Scene(), MissionContentCodec.Options);

        /// <summary>What every scene with the charges has, and no scene without them.</summary>
        public static string Marker => $"%\"{M.ChargeRole(0)}\"%";

        public static IEnumerable<string> Up()
        {
            yield return Replace(After, "NOT LIKE");
        }

        public static IEnumerable<string> Down()
        {
            yield return Replace(Before, "LIKE");
        }

        private static string Replace(string to, string like) =>
            $"UPDATE mission_scene_binding SET bindings = {S(to)} " +
            $"WHERE mission_id = {Mission} AND content_revision = {S(Revision)} AND bindings {like} {S(Marker)};";

        private static string S(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
