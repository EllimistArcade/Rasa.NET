namespace Rasa.Structures
{
    using Memory;

    /// <summary>
    /// One row of the "My Auctions" tab. The client unpacks these five fields positionally in
    /// inventory.UpdateAuctionItems:
    ///
    ///     for (entityId, curBid, curBidder, buyoutPrice, remainingDur) in entityList
    ///
    /// and the tab renders remainingDur and buyoutPrice, each only when it is not None. There is
    /// no bidding yet, so the two bid fields are always None - which is exactly what the client
    /// shows for an auction nobody has bid on.
    ///
    /// Note this is a different shape from <see cref="AuctionItem"/>, the thirteen-field struct
    /// the browse tab's query results use. The two are not interchangeable.
    /// </summary>
    public class AuctionStatus : IPythonDataStruct
    {
        public ulong EntityId { get; set; }
        public uint BuyoutPrice { get; set; }
        public uint RemainingHours { get; set; }

        public AuctionStatus()
        {
        }

        public AuctionStatus(ulong entityId, uint buyoutPrice, uint remainingHours)
        {
            EntityId = entityId;
            BuyoutPrice = buyoutPrice;
            RemainingHours = remainingHours;
        }

        public void Read(PythonReader pr)
        {
        }

        public void Write(PythonWriter pw)
        {
            pw.WriteTuple(5);
            pw.WriteULong(EntityId);        // entityId
            pw.WriteNoneStruct();           // curBid - no bidding yet
            pw.WriteNoneStruct();           // curBidder
            pw.WriteUInt(BuyoutPrice);      // buyoutPrice
            pw.WriteUInt(RemainingHours);   // remainingDur, in hours
        }
    }
}
