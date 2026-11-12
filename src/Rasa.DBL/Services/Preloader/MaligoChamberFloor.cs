using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Rasa.Missions.Scenes;
using Rasa.Services.Preloader.Missions.Plateau;
using Rasa.Structures;

using T = Rasa.Services.Preloader.Missions.Plateau.PlateauOneWayTripV1;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// One Way Trip's Special Projects chamber in Maligo Base, on its floor (Maligo_chamber_floor). The chamber's
    /// places stood 4.5 m under it, on the plane the navmesh has at -104.28 below the room - an island nobody stands
    /// on, taken for the room. The real floor, at -99.1 to -99.9 and walked to from Level 2, covered the device, the
    /// Explosive Charge the bomb is set on, Homburg, the four prisoners and the pad out: the charge could not be seen
    /// or clicked, and the bomb not set. Each stands at the floor's height under it now; the second prisoner's lane is
    /// moved off the machine that stands on the floor in its way (T.PrisonerLanes); objective 12's marker on the
    /// device is on the charge instead, where the bomb is set.
    ///
    /// The scene is the stored One Way Trip scene (mission_scene_binding.bindings, camelCase as MissionContentCodec
    /// writes it): each of the chamber's positions in it, <c>{"x":..,"y":-104.28,"z":..}</c>, is replaced whole by
    /// the place it has now. Plain SQL for both providers (REPLACE is both's). Guarded: what is not as this found it
    /// is left as it is, both ways.
    /// </summary>
    public static class MaligoChamberFloor
    {
        public const uint Mission = T.MissionId, Objective = T.Destroy;

        /// <summary>Objective 12's first marker, unnamed (PlateauMissionDataV1.Indicator: 12).</summary>
        public const uint Marker = MissionIndicator.UnnamedFrom + 12;

        public const string Revision = PlateauMissionDataV1.Revision;
        public const uint Pad = PlateauMaligoBaseV1.ChamberPad;

        /// <summary>The prisoners' lanes as they were.</summary>
        public static readonly float[] WasLanes = { 112, 120, 136, 144 };

        /// <summary>Each of the chamber's positions in the scene: as it was, under the floor, and as it is now.</summary>
        public static IEnumerable<(ScenePosition Was, ScenePosition Now)> Places()
        {
            yield return (Under(T.ChamberArrival), T.ChamberArrival);
            yield return (Under(T.DeviceAt), T.DeviceAt);
            yield return (Under(T.ChargeAt), T.ChargeAt);
            yield return (Under(T.HomburgAt), T.HomburgAt);

            var last = T.MarchX.Length - 1;
            for (var index = 0; index < WasLanes.Length; index++)
            {
                var now = T.MarchOf(index);
                for (var step = 0; step <= last; step++)
                {
                    var lane = WasLanes[index];
                    yield return (new ScenePosition(T.MarchX[step], T.UnderTheFloorY, lane + (T.DeviceAt.Z - lane) * 0.5f * step / last),
                        now[step].Position);
                }
            }
        }

        /// <summary>Objective 12's marker: on the device, under the floor; on the charge, on it.</summary>
        public const double WasX = -246, WasY = T.UnderTheFloorY, WasZ = 128;
        public static double NowX => T.ChargeAt.X;
        public static double NowY => T.ChargeAt.Y;
        public static double NowZ => T.ChargeAt.Z;

        /// <summary>The pad out: its height under the floor, and its own spot's floor.</summary>
        public const double PadWasY = T.UnderTheFloorY;
        public static double PadNowY => PlateauMaligoBaseV1.ChamberPadAt.Y;

        public static IEnumerable<string> Up()
        {
            foreach (var (was, now) in Places().Distinct())
                yield return Replace(Json(was), Json(now));

            yield return $"UPDATE mission_indicator SET pos_x = {N(NowX)}, pos_y = {N(NowY)}, pos_z = {N(NowZ)} " +
                $"WHERE {MarkerKey} AND pos_x = {N(WasX)} AND pos_z = {N(WasZ)} AND pos_y < -104;";

            yield return $"UPDATE teleporter SET pos_y = {N(PadNowY)} WHERE id = {Pad} AND pos_y < -104;";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"UPDATE teleporter SET pos_y = {N(PadWasY)} WHERE id = {Pad} AND pos_y > -100 AND pos_y < -99.5;";

            yield return $"UPDATE mission_indicator SET pos_x = {N(WasX)}, pos_y = {N(WasY)}, pos_z = {N(WasZ)} " +
                $"WHERE {MarkerKey} AND pos_x = {N(NowX)} AND pos_z = {N(NowZ)} AND pos_y > -100 AND pos_y < -99;";

            foreach (var (was, now) in Places().Distinct().Reverse())
                yield return Replace(Json(now), Json(was));
        }

        /// <summary>A position as the stored scene has it: <c>{"x":-246,"y":-104.28,"z":128}</c>.</summary>
        public static string Json(ScenePosition at) => $"{{\"x\":{F(at.X)},\"y\":{F(at.Y)},\"z\":{F(at.Z)}}}";

        private static ScenePosition Under(ScenePosition at) => new(at.X, T.UnderTheFloorY, at.Z);

        private static string Replace(string from, string to) =>
            $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, '{from}', '{to}') " +
            $"WHERE mission_id = {Mission} AND content_revision = '{Revision}' AND bindings LIKE '%{from}%';";

        private static string MarkerKey =>
            $"mission_id = {Mission} AND content_revision = '{Revision}' AND objective_id = {Objective} AND indicator_id = {Marker}";

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
