using System.Collections.Generic;
using System.Globalization;

using Rasa.Missions.Scenes;
using Rasa.Services.Preloader.Missions.Plateau;
using Rasa.Structures;

using S = Rasa.Services.Preloader.Missions.Plateau.PlateauSelfPreservationV1;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Self Preservation's yellow and red force field switches in Maligo Base, out from behind their walls
    /// (Maligo_switch_walls). Each stood beyond the outer wall of a service turn - the yellow 1.3 m behind the curved one
    /// at (-22, -132, 328), the red 0.9 m behind the one at (-46, -132, 296) - where it could not be seen or used, so its
    /// field and its CPU stayed: two CPUs of three, at most. They stand in front of those walls now, turned to them
    /// (S.Switches); their markers (objective 1's 5 and 6) with them.
    ///
    /// The scene is the stored Self Preservation scene (mission_scene_binding.bindings, camelCase as MissionContentCodec
    /// writes it): each switch's place and turn, <c>{"x":..,"y":..,"z":..},"orientation":..</c>, is replaced whole. Plain
    /// SQL for both providers (REPLACE is both's). Guarded: what is not as this found it is left as it is, both ways.
    ///
    /// Only the red one now: Plateau_self_preservation_charges (PlateauSelfPreservationV2), which comes before this, puts
    /// Self Preservation in again with the yellow one on the floor of its turn, 2.8 m south of where it stood, and in sight
    /// from the corridor (PlateauMaligoBaseTests).
    /// </summary>
    public static class MaligoSwitchWalls
    {
        public const uint Mission = S.MissionId, Objective = S.Destroy;
        public const string Revision = PlateauMissionDataV1.Revision;

        /// <summary>The switch moved: index in S.Switches, where it was and which way it faced, and its marker (PlateauMissionDataV1.Indicator 4 + index).</summary>
        public static readonly (int Index, ScenePosition Was, double WasFacing)[] Moved =
        {
            (2, new ScenePosition(-49.3f, -130.88f, 292), 1.5708),
        };

        public static uint Marker(int index) => MissionIndicator.UnnamedFrom + 4 + (uint)index;

        public static ScenePosition Now(int index) => S.Switches[index].At;
        public static double NowFacing(int index) => S.Switches[index].Facing;

        public static IEnumerable<string> Up()
        {
            foreach (var (index, was, wasFacing) in Moved)
            {
                yield return Replace(Json(was, wasFacing), Json(Now(index), NowFacing(index)));
                yield return Indicator(index, Now(index), was);
            }
        }

        public static IEnumerable<string> Down()
        {
            foreach (var (index, was, wasFacing) in Moved)
            {
                yield return Indicator(index, was, Now(index));
                yield return Replace(Json(Now(index), NowFacing(index)), Json(was, wasFacing));
            }
        }

        /// <summary>A switch's place and turn as the stored scene has them: <c>{"x":-49.3,"y":-130.88,"z":292},"orientation":1.5708</c>.</summary>
        public static string Json(ScenePosition at, double facing) =>
            $"{{\"x\":{F(at.X)},\"y\":{F(at.Y)},\"z\":{F(at.Z)}}},\"orientation\":{N(facing)}";

        private static string Replace(string from, string to) =>
            $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, '{from}', '{to}') " +
            $"WHERE mission_id = {Mission} AND content_revision = '{Revision}' AND bindings LIKE '%{from}%';";

        /// <summary>The marker to <paramref name="to"/>, where it is still at <paramref name="from"/> (the float it was written from, to 1 cm).</summary>
        private static string Indicator(int index, ScenePosition to, ScenePosition from) =>
            $"UPDATE mission_indicator SET pos_x = {N(to.X)}, pos_y = {N(to.Y)}, pos_z = {N(to.Z)} " +
            $"WHERE mission_id = {Mission} AND content_revision = '{Revision}' AND objective_id = {Objective} " +
            $"AND indicator_id = {Marker(index)} AND {Near("pos_x", from.X)} AND {Near("pos_z", from.Z)};";

        private static string Near(string column, float value) =>
            $"{column} > {N(value - 0.01)} AND {column} < {N(value + 0.01)}";

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
