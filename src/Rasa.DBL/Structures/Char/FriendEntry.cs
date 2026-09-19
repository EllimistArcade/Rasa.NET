using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rasa.Structures.Char
{
    [Table(TableName)]
    public class FriendEntry
    {
        public const string TableName = "friend";

        public FriendEntry()
        {
        }

        public FriendEntry(uint accountId, uint friendAccountId)
        {
            AccountId = accountId;
            FriendAccountId = friendAccountId;
        }

        // Composite key (account_id, friend_account_id) is configured in CharContext.
        [Column("account_id")]
        [Required]
        public uint AccountId { get; set; }

        [Column("friend_account_id")]
        [Required]
        public uint FriendAccountId { get; set; }
    }
}
