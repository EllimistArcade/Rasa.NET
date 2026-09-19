using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_AvailableCharacterClasses(classIds) - "let the user select a new tier character
    /// class", in the client's own words.
    ///
    /// The same list as <see cref="TierAdvancementInfoPacket"/>, but this one also posts
    /// TIER_SELECTION_AVAILABLE, which is the offer: it is what tells the player a choice is
    /// waiting. The client's comment says the list should always be non-empty, so it is only
    /// sent when there is something to choose.
    /// </summary>
    public class AvailableCharacterClassesPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AvailableCharacterClasses;

        public List<uint> ClassIds { get; }

        public AvailableCharacterClassesPacket(List<uint> classIds)
        {
            ClassIds = classIds;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(ClassIds.Count);

            foreach (var classId in ClassIds)
                pw.WriteUInt(classId);
        }
    }
}
