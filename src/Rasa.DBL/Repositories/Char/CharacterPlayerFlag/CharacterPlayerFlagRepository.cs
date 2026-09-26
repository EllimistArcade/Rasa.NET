using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.Char.CharacterPlayerFlag
{
    using Context.Char;
    using Structures.Char;

    public class CharacterPlayerFlagRepository : ICharacterPlayerFlagRepository
    {
        private readonly CharContext _charContext;

        public CharacterPlayerFlagRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        public List<uint> Get(uint characterId)
        {
            try
            {
                return _charContext.CreateNoTrackingQuery(_charContext.CharacterPlayerFlagEntries)
                    .Where(e => e.CharacterId == characterId)
                    .Select(e => e.PlayerFlagId)
                    .ToList();
            }
            catch (Exception e)
            {
                // A flag that cannot be read is an emote the player cannot use this session; the
                // login goes on.
                Logger.WriteLog(LogType.Error, $"Could not read the player flags of character {characterId}: {e}");
                return new List<uint>();
            }
        }

        public bool Add(uint characterId, uint playerFlagId)
        {
            try
            {
                var exists = _charContext.CreateNoTrackingQuery(_charContext.CharacterPlayerFlagEntries)
                    .Any(e => e.CharacterId == characterId && e.PlayerFlagId == playerFlagId);

                if (!exists)
                {
                    _charContext.CharacterPlayerFlagEntries.Add(new CharacterPlayerFlagEntry(characterId, playerFlagId));
                    _charContext.SaveChanges();
                }

                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not save player flag {playerFlagId} for character {characterId}: {e}");
                return false;
            }
        }

        public bool Remove(uint characterId, uint playerFlagId)
        {
            try
            {
                var rows = _charContext.CreateTrackingQuery(_charContext.CharacterPlayerFlagEntries)
                    .Where(e => e.CharacterId == characterId && e.PlayerFlagId == playerFlagId)
                    .ToList();

                _charContext.CharacterPlayerFlagEntries.RemoveRange(rows);
                _charContext.SaveChanges();

                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not remove player flag {playerFlagId} from character {characterId}: {e}");
                return false;
            }
        }

        public bool RemoveAll(uint characterId)
        {
            try
            {
                var rows = _charContext.CreateTrackingQuery(_charContext.CharacterPlayerFlagEntries)
                    .Where(e => e.CharacterId == characterId)
                    .ToList();

                _charContext.CharacterPlayerFlagEntries.RemoveRange(rows);
                _charContext.SaveChanges();

                return true;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not remove the player flags of character {characterId}: {e}");
                return false;
            }
        }

        public void DeleteForCharacter(uint characterId)
        {
            var rows = _charContext.CreateTrackingQuery(_charContext.CharacterPlayerFlagEntries)
                .Where(e => e.CharacterId == characterId);

            _charContext.CharacterPlayerFlagEntries.RemoveRange(rows);
        }
    }
}
