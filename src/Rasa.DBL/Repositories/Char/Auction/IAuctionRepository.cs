using System.Collections.Generic;

namespace Rasa.Repositories.Char.Auction
{
    using Structures.Char;

    public interface IAuctionRepository
    {
        /// <summary>Lists an item. Returns false if that item is already listed.</summary>
        bool CreateAuction(AuctionEntry auction);

        /// <summary>The auction for one item, or null when it is not listed.</summary>
        AuctionEntry GetAuctionByItemId(uint itemId);

        /// <summary>Everything one character currently has listed, oldest first.</summary>
        List<AuctionEntry> GetAuctionsBySeller(uint sellerId);

        /// <summary>How many auctions a character has running, without loading them.</summary>
        int CountAuctionsBySeller(uint sellerId);

        /// <summary>Every live auction, for the browse tab and for expiry sweeps.</summary>
        List<AuctionEntry> GetAuctions();

        void DeleteAuction(uint itemId);
    }
}
