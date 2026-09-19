using System.Collections.Generic;

namespace Rasa.Repositories.Char.ClanLockboxLog
{
    using Structures.Char;

    public interface IClanLockboxLogRepository
    {
        /// <summary>The clan's most recent entries, newest first, capped at <paramref name="limit"/>.</summary>
        List<ClanLockboxLogEntry> Get(uint clanId, int limit);

        /// <summary>Records one transaction. Returns the stored row, or null if the write failed.</summary>
        ClanLockboxLogEntry Add(ClanLockboxLogEntry entry);

        /// <summary>Drops everything a disbanded clan left behind.</summary>
        void DeleteByClanId(uint clanId);
    }
}
