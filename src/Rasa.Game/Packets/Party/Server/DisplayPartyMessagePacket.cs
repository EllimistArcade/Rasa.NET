using System.Collections.Generic;

namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_DisplayPartyMessage(msgId, args = {}): a player message from the squad,
    /// on the party manager's own channel rather than the communicator's.
    ///
    /// It posts UI_DISPLAY_PLAYER_MESSAGE, where DisplayClientMessage posts UI_DISPLAY_CLIENT_MESSAGE
    /// with a string the server already built. That is a real difference in general - the player
    /// message path is id-aware, so gameui.DisplayPlayerMessage can turn an id into a voice-over, an
    /// error beep, big on-screen text or nothing at all, and the status updater can raise an icon
    /// for it - but not for anything sent here: no party message appears in kPlayerMessageVOs,
    /// kPlayerMessageDisplayBigText, or the status updater's own list, and the SYSTEM_GENERAL filter
    /// it hardcodes is the GeneralSystemMessages the communicator path was already being given. Both
    /// therefore produce one identical chat line today.
    ///
    /// What it is for is the shape of the call: one message to a whole squad, from the manager that
    /// owns the squad, instead of a loop over members through a second manager's channel.
    /// </summary>
    public class DisplayPartyMessagePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplayPartyMessage;

        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }

        internal DisplayPartyMessagePacket(PlayerMessage msgId, Dictionary<string, string> args = null)
        {
            MsgId = msgId;
            Args = args ?? new Dictionary<string, string>();
        }

        public override void Write(PythonWriter pw)
        {
            // args has a default, so the tuple could be written with one element. It is always
            // written with both: every squad message that carries a name interpolates it, and a
            // missing dictionary would leave the %(player)s in the player's face.
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
