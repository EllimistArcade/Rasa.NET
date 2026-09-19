using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IMapLinkRepository
    {
        List<MapLinkEntry> GetMapLinks();

        /// <returns>The new row's id, or 0 when the insert failed.</returns>
        uint AddMapLink(MapLinkEntry entry);

        /// <returns>false when no row has that id or the update failed.</returns>
        bool UpdateMapLink(MapLinkEntry entry);

        /// <returns>false when no row has that id or the delete failed.</returns>
        bool DeleteMapLink(uint id);
    }

    /// <summary>
    /// The writes report failure instead of throwing: they run from GM chat commands, inside a
    /// packet handler, where an exception would cost the GM their connection over a bad row.
    /// </summary>
    public class MapLinkRepository : IMapLinkRepository
    {
        private readonly WorldContext _worldContext;

        public MapLinkRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<MapLinkEntry> GetMapLinks()
        {
            var query = _worldContext.CreateNoTrackingQuery(_worldContext.MapLinkEntries);

            return query.ToList();
        }

        public uint AddMapLink(MapLinkEntry entry)
        {
            try
            {
                _worldContext.MapLinkEntries.Add(entry);
                _worldContext.SaveChanges();

                return entry.Id;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error adding map link:");
                Logger.WriteLog(LogType.Error, e);
                return 0;
            }
        }

        public bool UpdateMapLink(MapLinkEntry entry)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.MapLinkEntries, entry.Id);

                if (row == null)
                    return false;

                row.MapContextId = entry.MapContextId;
                row.PosX = entry.PosX;
                row.PosY = entry.PosY;
                row.PosZ = entry.PosZ;
                row.Radius = entry.Radius;
                row.DestMapContextId = entry.DestMapContextId;
                row.DestPosX = entry.DestPosX;
                row.DestPosY = entry.DestPosY;
                row.DestPosZ = entry.DestPosZ;
                row.DestRotation = entry.DestRotation;
                row.Kind = entry.Kind;
                row.Enabled = entry.Enabled;
                row.Comment = entry.Comment;

                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error updating map link:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }

        public bool DeleteMapLink(uint id)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.MapLinkEntries, id);

                if (row == null)
                    return false;

                _worldContext.MapLinkEntries.Remove(row);
                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error deleting map link:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }
    }
}
