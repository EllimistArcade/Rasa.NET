namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_LogoutTimeRemaining(timeRemaining) (client/clientmethod.py:894): milliseconds until the
    /// player may log out. The logout window counts it down and keeps its Logout button disabled
    /// until it reaches zero; Cancel sends CancelLogoutRequest.
    /// </summary>
    public class LogoutTimeRemainingPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.LogoutTimeRemaining;

        public int TimeRemainingMs { get; }

        public LogoutTimeRemainingPacket(int timeRemainingMs)
        {
            TimeRemainingMs = timeRemainingMs;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteInt(TimeRemainingMs);
        }
    }
}
