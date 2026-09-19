using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_TierAdvancementInfo(classIds) - the classes this character could advance into,
    /// sent once as the manifestation loads.
    ///
    /// manifestation.py stores it and posts UI_UPDATE_CHARACTER_ADVANCEMENT, which is what draws
    /// the advancement tracker. It fires no event of its own, so this is state rather than an
    /// offer: the tracker knows what is ahead, and nothing opens.
    /// <see cref="AvailableCharacterClassesPacket"/> is the same payload with the offer attached.
    /// </summary>
    public class TierAdvancementInfoPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.TierAdvancementInfo;

        public List<uint> ClassIds { get; }

        public TierAdvancementInfoPacket(List<uint> classIds)
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
