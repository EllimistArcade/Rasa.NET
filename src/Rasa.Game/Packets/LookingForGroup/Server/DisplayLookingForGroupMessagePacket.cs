using System.Collections.Generic;

namespace Rasa.Packets.LookingForGroup.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// A player message about the LFG system.
    ///
    /// Recv_DisplayLookingForGroupMessage(msgId, args={}) forwards to UI_DISPLAY_PLAYER_MESSAGE
    /// with the SYSTEM_GENERAL filter (client/lookingforgroupmanager.py:98). The dispatcher
    /// applies the tuple positionally, so args could be left off, but it is always written:
    /// the language-file entries for several of these messages interpolate named values,
    /// and an absent dictionary would leave the placeholder in the player's face.
    ///
    /// Mirrors DisplayClientMessagePacket, minus the filter id - this one picks its own.
    /// </summary>
    public class DisplayLookingForGroupMessagePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplayLookingForGroupMessage;

        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }

        public DisplayLookingForGroupMessagePacket(PlayerMessage msgId, Dictionary<string, string> args = null)
        {
            MsgId = msgId;
            Args = args ?? new Dictionary<string, string>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt((uint)MsgId);
            pw.WriteDictionary(Args.Count);

            foreach (var arg in Args)
            {
                pw.WriteString(arg.Key);
                pw.WriteString(arg.Value);
            }
        }
    }
}
