namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:439 - SendCallUserMethod('ChangeLastName', (arg,)), from
    /// /changelastname. The last name belongs to the account, so it renames every character on it.
    /// </summary>
    public class ChangeLastNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ChangeLastName;

        public string Name { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            // Typed into the chat box, so unicode; read either type, as the social by-name
            // packets do. Read used to only log the payload, which left the 0x66 terminator
            // check to fail and drop the connection.
            Name = (pr.PeekType() == PythonType.UnicodeString ? pr.ReadUnicodeString() : pr.ReadString())?.Trim() ?? string.Empty;
        }
    }
}
