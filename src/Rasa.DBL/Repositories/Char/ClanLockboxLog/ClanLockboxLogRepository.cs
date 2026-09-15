using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.Char.ClanLockboxLog
{
    using Context.Char;
    using Structures.Char;

    public class ClanLockboxLogRepository : IClanLockboxLogRepository
    {
        private readonly CharContext _charContext;

        public ClanLockboxLogRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        public List<ClanLockboxLogEntry> Get(uint clanId, int limit)
        {
            if (limit <= 0)
                return new List<ClanLockboxLogEntry>();

            return _charContext.CreateNoTrackingQuery(_charContext.ClanLockboxLogEntries)
                .Where(e => e.ClanId == clanId)
                .OrderByDescending(e => e.TransactionTime)
                .ThenByDescending(e => e.Id)
                .Take(limit)
                .ToList();
        }

        public ClanLockboxLogEntry Add(ClanLockboxLogEntry entry)
        {
            // A log line that cannot be written must not take the transaction it describes with
            // it: the credits have already moved by the time this runs.
            try
            {
                _charContext.Add(entry);
                _charContext.SaveChanges();

                return entry;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not write a clan lockbox log line for clan {entry.ClanId}: {e}");
                return null;
            }
        }

        public void DeleteByClanId(uint clanId)
        {
            var rows = _charContext.CreateTrackingQuery(_charContext.ClanLockboxLogEntries)
                .Where(e => e.ClanId == clanId)
                .ToList();

            if (rows.Count == 0)
                return;

            _charContext.RemoveRange(rows);
            _charContext.SaveChanges();
        }
    }
}
