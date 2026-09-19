namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:447 - SendCallUserMethod('ChangeFirstName', (arg,)), from
    /// /changefirstname. Renames the character the player is on.
    /// </summary>
    public class ChangeFirstNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ChangeFirstName;

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
