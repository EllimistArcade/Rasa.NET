using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Two of Bug City's Hanging Hives move out of the rock (WarnetCavernsContent.Hives): the north-west
    /// chamber's from (263, 52, 69) and the west one's from (262.94, 51.5, 91.22), each hung over a ledge
    /// of the cavern wall, where they could not be seen and could be hit only by an area attack through
    /// the wall. Both now hang under open roof over the floor (Warnet_caverns_hives).
    ///
    /// Plain SQL for both providers. Guarded: a hive that is not where this found it is left as it is,
    /// both ways.
    /// </summary>
    public static class WarnetCavernsHives
    {
        public const uint Map = WarnetCavernsContent.Map;

        public sealed record Move(uint Id, double WasX, double WasY, double WasZ, string WasWhere)
        {
            public WarnetCavernsContent.Hive Now => WarnetCavernsContent.Hives.Single(hive => hive.Id == Id);
        }

        public static readonly Move[] Moves =
        {
            new(WarnetCavernsContent.FirstHive, 263.0, 52.0, 69.0, "the north-west chamber"),
            new(WarnetCavernsContent.FirstHive + 4, 262.94, 51.5, 91.22, "the west terrace")
        };

        public static IEnumerable<string> Up()
        {
            foreach (var m in Moves)
                yield return $"UPDATE world_destructible SET pos_x = {N(m.Now.X)}, pos_y = {N(m.Now.Y)}, pos_z = {N(m.Now.Z)}, " +
                    $"comment = {S(Comment(m.Now.Where))} WHERE {Key(m.Id)} AND {At(m.WasX, m.WasZ)};";
        }

        public static IEnumerable<string> Down()
        {
            foreach (var m in Moves)
                yield return $"UPDATE world_destructible SET pos_x = {N(m.WasX)}, pos_y = {N(m.WasY)}, pos_z = {N(m.WasZ)}, " +
                    $"comment = {S(Comment(m.WasWhere))} WHERE {Key(m.Id)} AND {At(m.Now.X, m.Now.Z)};";
        }

        /// <summary>As WarnetCavernsContent writes it.</summary>
        public static string Comment(string where) => $"Warnet Caverns Bug City: Hanging Hive in {where}";

        private static string Key(uint id) => $"id = {id} AND map_context_id = {Map}";

        /// <summary>Within a centimetre, so a value that has been through a float on its way is still found.</summary>
        private static string At(double x, double z) => $"ABS(pos_x - {N(x)}) < 0.01 AND ABS(pos_z - {N(z)}) < 0.01";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string S(string value) => "'" + value.Replace("'", "''") + "'";
    }
}
