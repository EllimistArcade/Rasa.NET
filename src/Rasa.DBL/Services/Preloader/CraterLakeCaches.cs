using System.Collections.Generic;
using System.Globalization;

using Rasa.Structures;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Reclaiming What's Ours' marker for its first objective, gather the generator detonators,
    /// moves with the weapons caches (CraterLakeContent.Caches): from inside the pipe across the
    /// fortification's platform, where the caches could not be clicked, to the first of them on
    /// the platform's open floor (Crater_lake_caches). The caches themselves are put down by the
    /// server at their new place (CraterLakeFacility).
    ///
    /// Plain SQL for both providers. Guarded: a marker that is not where this found it is left
    /// as it is, both ways.
    /// </summary>
    public static class CraterLakeCaches
    {
        public const uint Mission = CraterLakeContent.ReclaimingWhatsOurs, Objective = CraterLakeContent.GatherBombs;
        public const uint Marker = MissionIndicator.UnnamedFrom + CraterLakeContent.GatherBombs;
        public const string Revision = CraterLakeContent.Revision;

        /// <summary>Where the marker was: the first cache's old place.</summary>
        public const double WasX = -283, WasZ = -52;

        public static double NowX => CraterLakeContent.Caches[0].X;
        public static double NowY => CraterLakeContent.Caches[0].Y;
        public static double NowZ => CraterLakeContent.Caches[0].Z;

        public static IEnumerable<string> Up()
        {
            yield return $"UPDATE mission_indicator SET pos_x = {N(NowX)}, pos_y = {N(NowY)}, pos_z = {N(NowZ)} " +
                $"WHERE {Key} AND pos_x = {N(WasX)} AND pos_z = {N(WasZ)};";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"UPDATE mission_indicator SET pos_x = {N(WasX)}, pos_z = {N(WasZ)} " +
                $"WHERE {Key} AND pos_x = {N(NowX)} AND pos_z = {N(NowZ)};";
        }

        private static string Key =>
            $"mission_id = {Mission} AND content_revision = '{Revision}' AND objective_id = {Objective} AND indicator_id = {Marker}";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
