namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells a player they have been idle long enough to be logged out soon.
    ///
    /// Recv_PlayerInactiveWarning(self) takes no arguments and is dispatched on the player's
    /// Manifestation (client/augmentations/manifestation.py:1374). It only acts when that
    /// entity is the client's own manifestation, printing PM_YOU_ARE_INACTIVE_WARNING:
    /// "You have been AFK for an extended period of time. You will be logged out if you
    /// stay inactive." Sent to the idle player alone, not broadcast.
    /// </summary>
    public class PlayerInactiveWarningPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PlayerInactiveWarning;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
