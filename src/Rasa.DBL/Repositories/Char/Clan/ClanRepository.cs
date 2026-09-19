using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.Char.Clan
{
    using Context.Char;
    using Structures.Char;

    public class ClanRepository : IClanRepository
    {
        private readonly CharContext _charContext;
        private readonly string _rank0 = "Grunt";
        private readonly string _rank1 = "Soldier";
        private readonly string _rank2 = "Officier";
        private readonly string _rank3 = "Clan Leader";

        public ClanRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        public ClanEntry CreateClan(string clanName, bool isPvP)
        {
            var entry = new ClanEntry
            {
                Name = clanName,
                IsPvP = isPvP,
                RankTitle0 = _rank0,
                RankTitle1 = _rank1,
                RankTitle2 = _rank2,
                RankTitle3 = _rank3,
                CreatedAt = DateTime.UtcNow,
                Credits = 0,
                Prestige = 0,
                // One, not none: the first tab is not bought. A clan created with 0 would be told
                // every tab was locked, and the client's own purchase rule needs the tab below
                // the one being bought unlocked - so it could never have bought its way out.
                PurashedTabs = 1
            };

            _charContext.ClanEntries.Add(entry);
            _charContext.SaveChanges();

            return entry;
        }

        public bool DeleteClan(uint clanId)
        {
            try
            {
                var query = _charContext.CreateNoTrackingQuery(_charContext.ClanEntries);
                var entry = query.Where(e => e.Id == clanId).FirstOrDefault();

                _charContext.Remove(entry);
                _charContext.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteLog(LogType.Error, $"Error in delete clan: {ex}");

                return false;
            }

        }

        public ClanEntry GetClanByCharacterId(uint characterId)
        {
            var clanQuery = _charContext.CreateNoTrackingQuery(_charContext.ClanEntries);
            var clanMemberQuery = _charContext.CreateNoTrackingQuery(_charContext.ClanMemberEntries);
            var clan = clanQuery.FirstOrDefault(e => e.Id == clanMemberQuery.FirstOrDefault(e => e.CharacterId == characterId).ClanId);

            return clan;
        }

        public ClanEntry GetClanById(uint clanId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.ClanEntries);
            var entry = query.Where(e => e.Id == clanId).FirstOrDefault();

            return entry;
        }

        public ClanEntry GetClanByName(string clanName)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.ClanEntries);
            var entry = query.Where(e => e.Name == clanName).FirstOrDefault();

            return entry;
        }

        public List<ClanEntry> GetClans()
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.ClanEntries);
            var entries = query.ToList();

            return entries;
        }

        private ClanEntry GetWritable(uint clanId)
        {
            var entry = _charContext.GetWritable(_charContext.ClanEntries, clanId);

            if (entry == null)
                Logger.WriteLog(LogType.Error, $"Clan {clanId} does not exist; update skipped.");

            return entry;
        }

        /// <summary>Renames a clan. False if there is no such clan, or the write failed.</summary>
        public bool UpdateClanName(uint clanId, string clanName)
        {
            var entry = _charContext.CreateTrackingQuery(_charContext.ClanEntries).FirstOrDefault(e => e.Id == clanId);

            if (entry == null)
                return false;

            entry.Name = clanName;
            _charContext.SaveChanges();

            return true;
        }

        /// <summary>How many lockbox tabs the clan has unlocked.</summary>
        public bool UpdatePurashedTabs(uint clanId, uint purashedTabs)
        {
            var entry = _charContext.CreateTrackingQuery(_charContext.ClanEntries).FirstOrDefault(e => e.Id == clanId);

            if (entry == null)
                return false;

            entry.PurashedTabs = purashedTabs;
            _charContext.SaveChanges();

            return true;
        }

        public void UpdateCredits(uint clanId, uint credits)
        {
            var entry = GetWritable(clanId);

            if (entry == null)
                return;

            entry.Credits = credits;
            _charContext.SaveChanges();
        }

        public void UpdateLastPvPClanTimeForMembers(uint clanId, DateTime lastPvPClanTimestamp)
        {
            var members = _charContext.CreateNoTrackingQuery(_charContext.ClanMemberEntries).Where(e => e.ClanId == clanId).ToList();

            foreach (var member in members)
            {
                var character = _charContext.GetWritable(_charContext.CharacterEntries, member.CharacterId);

                if (character == null)
                    continue;

                // This used to rewrite each member's whole character row without changing
                // anything in it; the timestamp it was given was never stored.
                character.LastPvPClan = lastPvPClanTimestamp;
            }

            _charContext.SaveChanges();
        }

        /// <summary>Starts the PvP-clan cooldown for one character: the one who left or was kicked.</summary>
        public void UpdateLastPvPClanTime(uint characterId, DateTime lastPvPClanTimestamp)
        {
            var character = _charContext.GetWritable(_charContext.CharacterEntries, characterId);

            if (character == null)
                return;

            character.LastPvPClan = lastPvPClanTimestamp;
            _charContext.SaveChanges();
        }

        public void UpdatePrestige(uint clanId, uint prestige)
        {
            var entry = GetWritable(clanId);

            if (entry == null)
                return;

            entry.Prestige = prestige;
            _charContext.SaveChanges();
        }

        public bool UpdateRankTitleByClanId(uint clanId, uint rank, string title)
        {
            var entry = GetWritable(clanId);

            if (entry == null)
                return false;

            switch (rank)
            {
                case 0:
                    entry.RankTitle0 = title;
                    break;
                case 1:
                    entry.RankTitle1 = title;
                    break;
                case 2:
                    entry.RankTitle2 = title;
                    break;
                case 3:
                    entry.RankTitle3 = title;
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"Error in UpdateRankTitle: rank {rank} out of range.");
                    return false;
            }

            _charContext.SaveChanges();

            return true;
        }
    }
}
