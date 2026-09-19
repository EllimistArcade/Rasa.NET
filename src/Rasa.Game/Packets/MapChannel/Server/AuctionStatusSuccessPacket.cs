using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Everything the requesting character has listed. The client hands the list straight to
    /// inventory.UpdateAuctionItems, which replaces its whole auction dictionary from it, so a
    /// short list is how an auction disappears from the tab - there is no per-row removal here.
    /// </summary>
    public class AuctionStatusSuccessPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AuctionStatusSuccess;

        public List<AuctionStatus> Auctions { get; }

        public AuctionStatusSuccessPacket(List<AuctionStatus> auctions)
        {
            Auctions = auctions;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(Auctions.Count);

            foreach (var auction in Auctions)
                pw.WriteStruct(auction);
        }
    }
}
