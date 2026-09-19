using System.Collections.Generic;

namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py:245 - Recv_DisplaySystemMessage(msgId, args={}, filterId=SYSTEM_GENERAL).
    ///
    /// Not a duplicate of DisplayClientMessage, which this server already sends. That one builds
    /// the text itself and posts UI_DISPLAY_CLIENT_MESSAGE - a chat line and nothing else. This
    /// one hands the id to UI_DISPLAY_PLAYER_MESSAGE, where gameui.py's DisplayPlayerMessage
    /// decides what the message is *for*:
    ///
    ///   - a message with a voice-over recorded against it is spoken and never printed;
    ///   - PM_NO_OP prints nothing at all;
    ///   - PM_CANNOT_PERFORM_ACTION_NOW plays the UI error sound instead of text;
    ///   - ids in kPlayerMessageDisplayBigText also throw the text across the middle of the screen.
    ///
    /// So the choice between the two is not stylistic: use this one when the message is a thing
    /// that happened to the player and the client should present it however that message is meant
    /// to be presented, and DisplayClientMessage when it is a line of text for the chat window.
    /// </summary>
    public class DisplaySystemMessagePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplaySystemMessage;

        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }
        public MsgFilterId FilterId { get; set; }

        internal DisplaySystemMessagePacket(PlayerMessage msgId, Dictionary<string, string> args = null,
            MsgFilterId filterId = MsgFilterId.GeneralSystemMessages)
        {
            MsgId = msgId;
            Args = args ?? new Dictionary<string, string>();
            FilterId = filterId;
        }

        public override void Write(PythonWriter pw)
        {
            // Both trailing arguments have client-side defaults and both are written anyway: a
            // message that interpolates a named value shows the raw placeholder without its args,
            // and the filter decides which chat tab the line lands in.
            pw.WriteTuple(3);
            pw.WriteUInt((uint)MsgId);

            pw.WriteDictionary(Args.Count);

            foreach (var arg in Args)
            {
                pw.WriteString(arg.Key);
                pw.WriteString(arg.Value);
            }

            pw.WriteUInt((uint)FilterId);
        }
    }
}
