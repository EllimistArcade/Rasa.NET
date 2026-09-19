using System.Collections.Generic;

namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py:250 - Recv_DisplayPlayerNotification(notificationTypeId, msgId, args={}).
    ///
    /// The same player-message ids as DisplaySystemMessage, but the server says where it goes:
    /// Big across the middle of the screen, Destination on the sub-region strip. gameui.py sends
    /// every other type down the ordinary player-message path, so Info, Alert and CurrentLocation
    /// currently read as chat lines - see PlayerNotificationType.
    /// </summary>
    public class DisplayPlayerNotificationPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplayPlayerNotification;

        public PlayerNotificationType NotificationType { get; set; }
        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }

        internal DisplayPlayerNotificationPacket(PlayerNotificationType notificationType, PlayerMessage msgId,
            Dictionary<string, string> args = null)
        {
            NotificationType = notificationType;
            MsgId = msgId;
            Args = args ?? new Dictionary<string, string>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteUInt((uint)NotificationType);
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
