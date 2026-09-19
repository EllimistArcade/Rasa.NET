using System.Collections.Generic;

namespace Rasa.Repositories.Char.Friend
{
    public interface IFriendRepository
    {
        /// <returns>false when the row could not be written, so the caller can tell the client.</returns>
        bool AddFriend(uint accountId, uint friendAccountId);
        List<uint> GetFriends(uint accountId);
        void RemoveFriend(uint accountId, uint friendAccountId);
    }
}
