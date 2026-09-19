using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rasa.Repositories.Char.Ignored
{
    using Context.Char;
    using Structures.Char;

    public class IgnoredRepository : IIgnoredRepository
    {
        private readonly CharContext _charContext;

        public IgnoredRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        /// <summary>
        /// Reports whether the row was written, as AddFriend does. It used to swallow the failure
        /// and return void, so the caller added the account to the in-memory list anyway: the
        /// player was ignoring someone the database had never heard of, and un-ignoring them later
        /// looked for a row that was not there.
        /// </summary>
        public bool AddIgnored(uint accountId, uint ignoredAccountId)
        {
            var entry = new IgnoredEntry(accountId, ignoredAccountId);

            try
            {
                _charContext.IgnoredEntries.Add(entry);
                _charContext.SaveChanges();
                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error adding Ignored:");
                Logger.WriteLog(LogType.Error, e);
                return false;
            }
        }

        public List<uint> GetIgnored(uint accountId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.IgnoredEntries);
            var entries = query.Where(e => e.AccountId == accountId).Select(e => e.IgnoredAccountId).ToList();

            return entries;
        }

        public void RemoveIgnored(uint accountId, uint ignoredAccountId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.IgnoredEntries);
            var entry = query.Where(e => e.AccountId == accountId && e.IgnoredAccountId == ignoredAccountId).FirstOrDefault();

            // Nothing to remove is not an error. Remove(null) throws, and this runs inside a packet
            // handler, so it would cost the player their connection over a row that is already gone.
            if (entry == null)
                return;

            _charContext.Remove(entry);
            _charContext.SaveChanges();
        }
    }
}
