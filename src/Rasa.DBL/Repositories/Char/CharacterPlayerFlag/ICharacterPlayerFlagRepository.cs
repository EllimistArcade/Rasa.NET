using System.Collections.Generic;

namespace Rasa.Repositories.Char.CharacterPlayerFlag
{
    public interface ICharacterPlayerFlagRepository
    {
        /// <summary>The player flags a character holds.</summary>
        List<uint> Get(uint characterId);

        /// <summary>Gives a character a flag; false if it could not be saved.</summary>
        bool Add(uint characterId, uint playerFlagId);

        /// <summary>Takes a flag from a character; false if the row could not be removed.</summary>
        bool Remove(uint characterId, uint playerFlagId);

        /// <summary>Takes every flag from a character; false if the rows could not be removed.</summary>
        bool RemoveAll(uint characterId);

        /// <summary>Marks a deleted character's rows for removal; saved with the unit of work.</summary>
        void DeleteForCharacter(uint characterId);
    }
}
