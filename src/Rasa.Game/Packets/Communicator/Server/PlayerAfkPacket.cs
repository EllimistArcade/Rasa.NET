namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells clients that a player's AFK state changed.
    /// Sent to the player's Manifestation entity id and broadcast to everyone in
    /// visibility range: the client shows the "you are AFK" system message only for
    /// its own manifestation, and an idle indicator over anyone else's head
    /// (Recv_PlayerAfk in client/augmentations/manifestation.py).
    /// </summary>
    public class PlayerAfkPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PlayerAfk;

        public bool IsAfk { get; set; }

        public PlayerAfkPacket(bool isAfk)
        {
            IsAfk = isAfk;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteBool(IsAfk);
        }
    }
}
