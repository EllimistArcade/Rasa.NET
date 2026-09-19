using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IKraftwerksRepository
    {
        List<KraftwerksEntry> GetKraftwerks();

        /// <returns>The new row's id, or 0 when the insert failed.</returns>
        uint AddKraftwerks(KraftwerksEntry entry);

        /// <returns>false when no row has that id or the update failed.</returns>
        bool UpdateKraftwerks(KraftwerksEntry entry);

        /// <returns>false when no row has that id or the delete failed.</returns>
        bool DeleteKraftwerks(uint id);
    }

    /// <summary>
    /// The writes report failure instead of throwing: they run from GM chat commands, inside a
    /// packet handler, where an exception would cost the GM their connection over a bad row.
    /// </summary>
    public class KraftwerksRepository : IKraftwerksRepository
    {
        private readonly WorldContext _worldContext;

        public KraftwerksRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<KraftwerksEntry> GetKraftwerks()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.KraftwerksEntries).ToList();
        }

        public uint AddKraftwerks(KraftwerksEntry entry)
        {
            try
            {
                _worldContext.KraftwerksEntries.Add(entry);
                _worldContext.SaveChanges();

                return entry.Id;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error adding kraftwerks:");
                Logger.WriteLog(LogType.Error, e);
                return 0;
            }
        }

        public bool UpdateKraftwerks(KraftwerksEntry entry)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.KraftwerksEntries, entry.Id);

                if (row == null)
                    return false;

                row.ClassId = entry.ClassId;
                row.MapContextId = entry.MapContextId;
                row.PosX = entry.PosX;
                row.PosY = entry.PosY;
                row.PosZ = entry.PosZ;
                row.Rotation = entry.Rotation;
                row.Comment = entry.Comment;

                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error updating kraftwerks:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }

        public bool DeleteKraftwerks(uint id)
        {
            try
            {
                var row = _worldContext.GetWritable(_worldContext.KraftwerksEntries, id);

                if (row == null)
                    return false;

                _worldContext.KraftwerksEntries.Remove(row);
                _worldContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error deleting kraftwerks:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }
    }
}
