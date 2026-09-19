namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    public class WhoPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.Who;
        
        /// <summary>
        /// Free text from /who, /lookup or /whois - matched against character and family
        /// names, not a family name specifically. Empty when the player typed the command
        /// with no argument.
        /// </summary>
        public string SearchText { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            SearchText = pr.ReadUnicodeString();
        }
    }
}
 