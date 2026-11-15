using System.Collections.Generic;
using System.Globalization;

using Rasa.Structures;

using D = Rasa.Services.Preloader.CavesOfDonnContent;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The Caves of Donn's Chantry tunnel egg cluster past the cave-in, out of the narrows before Undone Fairway
    /// (Donn_egg_cluster_narrows). It stood at (-60, 14.39, -293), where the tunnel is a few metres wide. A destroyed
    /// cluster's wreck (arch_forean_fithiceggcluster_v01_destroyed_v01) keeps a collision 8.8 by 7.3 m, nearly twice
    /// the living one's 4.7 by 4.8 m, and once it was shot down it closed the tunnel for the rest of the copy: a player
    /// on the far side could not get back. It stands in the chamber east of the narrows now (D.EggClusters[3]), and
    /// Bugged to Death's marker for it (objective 1, indicator UnnamedFrom + 4) with it.
    ///
    /// Guarded: a row or a marker not where this found it is left as it is, both ways.
    /// </summary>
    public static class DonnEggClusterNarrows
    {
        public const int Index = 3;
        public const uint Mission = D.BuggedToDeath, Objective = 1;
        public const string Revision = D.Revision;

        public static readonly (double X, double Y, double Z) Was = (-60, 14.39, -293);

        public static D.EggCluster Now => D.EggClusters[Index];

        public static uint Marker => MissionIndicator.UnnamedFrom + Index + 1;

        public static IEnumerable<string> Up()
        {
            yield return Row(Now.X, Now.Y, Now.Z, Was);
            yield return Indicator(Now.X, Now.Y, Now.Z, Was);
        }

        public static IEnumerable<string> Down()
        {
            yield return Indicator(Was.X, Was.Y, Was.Z, (Now.X, Now.Y, Now.Z));
            yield return Row(Was.X, Was.Y, Was.Z, (Now.X, Now.Y, Now.Z));
        }

        private static string Row(double x, double y, double z, (double X, double Y, double Z) from) =>
            $"UPDATE world_destructible SET pos_x = {N(x)}, pos_y = {N(y)}, pos_z = {N(z)} " +
            $"WHERE id = {Now.Id} AND map_context_id = {D.Donn} AND class_id = {D.EggClusterClass} " +
            $"AND {Near("pos_x", from.X)} AND {Near("pos_z", from.Z)};";

        private static string Indicator(double x, double y, double z, (double X, double Y, double Z) from) =>
            $"UPDATE mission_indicator SET pos_x = {N(x)}, pos_y = {N(y)}, pos_z = {N(z)} " +
            $"WHERE mission_id = {Mission} AND content_revision = '{Revision}' AND objective_id = {Objective} " +
            $"AND indicator_id = {Marker} AND {Near("pos_x", from.X)} AND {Near("pos_z", from.Z)};";

        private static string Near(string column, double value) =>
            $"{column} > {N(value - 0.01)} AND {column} < {N(value + 0.01)}";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
