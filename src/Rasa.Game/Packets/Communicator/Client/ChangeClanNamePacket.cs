namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// /changeclanname, which sends <c>(arg,)</c> - the new name as a unicode string.
    ///
    /// Read only logged the reader and consumed nothing, so the payload was still sitting in the
    /// stream when the terminator check ran, the check failed, and the connection was closed.
    /// Typing the command disconnected you: the same shape as /gotomob and the Help window's
    /// Send buttons before them.
    /// </summary>
    public class ChangeClanNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ChangeClanName;

        public string ClanName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ClanName = pr.ReadUnicodeString();
        }
    }
}
