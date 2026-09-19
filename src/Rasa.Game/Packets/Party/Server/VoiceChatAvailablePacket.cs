namespace Rasa.Packets.Party.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/party.py Recv_VoiceChatAvailable(isAvail): the switch that decides whether a client
    /// ever tries to use voice chat.
    ///
    /// It sets g_voiceAvailable, and when true immediately calls _RequestJoinVoiceChannel, which
    /// sends RequestJoinVoiceChannel unless Client.Voice.Enabled is off. That is the only path to
    /// voice: g_voiceAvailable starts at 0 and nothing else assigns it, so a server that never
    /// sends this packet gets a client that never asks. Push-to-talk still calls BeginSpeaking, but
    /// it feeds a native session that was never created.
    ///
    /// false goes on the wire as None rather than a Zero struct - WriteBool's false is
    /// WriteNoneStruct, the same as the partyExclusiveMap flag in SquadMemberList. Python reads it
    /// as falsy either way, and the client only ever tests this value's truth.
    /// </summary>
    public class VoiceChatAvailablePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.VoiceChatAvailable;

        internal bool IsAvailable { get; set; }

        internal VoiceChatAvailablePacket(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteBool(IsAvailable);
        }
    }
}
