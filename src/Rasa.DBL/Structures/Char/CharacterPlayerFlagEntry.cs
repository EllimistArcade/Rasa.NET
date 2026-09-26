using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.Char
{
    /// <summary>
    /// A player flag a character holds. The client's actions list the flags they need
    /// (actiondata.playerFlagReqs; 57 emotes, among them the account, veteran and event reward
    /// emotes), and the manifestation's PlayerFlags list is what it checks them against.
    /// </summary>
    [Table(TableName)]
    public class CharacterPlayerFlagEntry
    {
        public const string TableName = "character_player_flag";

        public CharacterPlayerFlagEntry()
        {
        }

        public CharacterPlayerFlagEntry(uint characterId, uint playerFlagId)
        {
            CharacterId = characterId;
            PlayerFlagId = playerFlagId;
        }

        [Column("character_id")]
        [Required]
        public uint CharacterId { get; set; }

        [Column("player_flag_id")]
        [Required]
        public uint PlayerFlagId { get; set; }
    }
}
