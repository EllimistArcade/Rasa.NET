using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IMapRegionRepository
    {
        List<MapRegionEntry> GetMapRegions();

        /// <returns>The new row's id, or 0 when the insert failed.</returns>
        uint AddMapRegion(MapRegionEntry entry);

        /// <returns>false when no row has that id or the update failed.</returns>
        bool UpdateMapRegion(MapRegionEntry entry);

        /// <returns>false when no row has that id or the delete failed.</returns>
        bool DeleteMapRegion(uint id);
    }

    /// <summary>
    /// The writes report failure instead of throwing: they run from GM chat commands, inside a
    /// packet handler, where an exception would cost the GM their connection over a bad row.
    /// </summary>
    public class MapRegionRepository : IMapRegionRepository
    {
        private readonly WorldContext _worldContext;

        public MapRegionRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<MapRegionEntry> GetMapRegions()
        {
            var query = _worldContext.CreateNoTrackingQuery(_worldContext.MapRegionEntries);

            return query.ToList();
        }

        public uint AddMapRegion(MapRegionEntry entry)
        {
            try
            {
                _worldContext.MapRegionEntries.Add(entry);
                _worldContext.SaveChanges();

                return entry.Id;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error adding map region:");
                Logger.WriteLog(LogType.Error, e);
                return 0;
            }
        }

        public bool UpdateMapRegion(MapRegionEntry entry)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.MapRegionEntries, entry.Id);

                if (row == null)
                    return false;

                row.MapContextId = entry.MapContextId;
                row.RegionId = entry.RegionId;
                row.Shape = entry.Shape;
                row.PosX = entry.PosX;
                row.PosY = entry.PosY;
                row.PosZ = entry.PosZ;
                row.Radius = entry.Radius;
                row.HalfX = entry.HalfX;
                row.HalfZ = entry.HalfZ;
                row.MinY = entry.MinY;
                row.MaxY = entry.MaxY;
                row.Underground = entry.Underground;
                row.Enabled = entry.Enabled;
                row.Comment = entry.Comment;

                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error updating map region:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }

        public bool DeleteMapRegion(uint id)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.MapRegionEntries, id);

                if (row == null)
                    return false;

                _worldContext.MapRegionEntries.Remove(row);
                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error deleting map region:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }
    }
}
