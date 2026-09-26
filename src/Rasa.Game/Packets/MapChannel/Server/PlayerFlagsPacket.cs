using System.Collections.Generic;
using System.Linq;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// PlayerFlags (710), on a manifestation: the player flags it holds, as a list of ids.
    /// client/augmentations/manifestation.py Recv_PlayerFlags(playerFlagIds) keeps the list, and
    /// HasPlayerFlag asks <c>playerFlagId in playerFlags</c> - which is how an action's
    /// playerFlagReqs (57 emotes, the account, veteran and event reward emotes among them) are
    /// checked before the client lets it be performed. A repeat replaces the list.
    ///
    /// This used to write the int 0xFFFFFFF, and <c>in</c> on an int raises TypeError, so every
    /// flag-gated emote failed in the client's own check.
    /// </summary>
    public class PlayerFlagsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PlayerFlags;

        public List<uint> PlayerFlags { get; }

        public PlayerFlagsPacket(IEnumerable<uint> playerFlags = null)
        {
            PlayerFlags = playerFlags?.OrderBy(f => f).ToList() ?? new List<uint>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(PlayerFlags.Count);

            foreach (var flag in PlayerFlags)
                pw.WriteUInt(flag);
        }
    }
}
