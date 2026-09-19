using System.Linq;

namespace Rasa.Repositories.Char.CharacterLockbox
{
    using Context.Char;
    using Structures.Char;

    public class CharacterLockboxRepository : ICharacterLockboxRepository
    {
        private readonly CharContext _charContext;

        public CharacterLockboxRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        public void Add(uint id)
        {
            _charContext.Add(new CharacterLockboxEntry(id, 0, 1));
            _charContext.SaveChanges();
        }

        public CharacterLockboxEntry Get(uint accountId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.CharacterLockboxEntries);
            var lockboxInfo = query.FirstOrDefault(e => e.AccountId == accountId);

            return lockboxInfo;
        }

        private CharacterLockboxEntry GetWritable(uint accountId)
        {
            var entry = _charContext.CreateTrackingQuery(_charContext.CharacterLockboxEntries).FirstOrDefault(e => e.AccountId == accountId);

            if (entry == null)
                Logger.WriteLog(LogType.Error, $"Account {accountId} has no lockbox row; update skipped.");

            return entry;
        }

        public void UpdateCredits(uint accountId, int credits)
        {
            var entry = GetWritable(accountId);

            if (entry == null)
                return;

            entry.Credits = credits;
            _charContext.SaveChanges();
        }

        public void UpdatePurashedTabs(uint accountId, int purashedTabs)
        {
            var entry = GetWritable(accountId);

            if (entry == null)
                return;

            entry.PurashedTabs = purashedTabs;
            _charContext.SaveChanges();
        }
    }
}
