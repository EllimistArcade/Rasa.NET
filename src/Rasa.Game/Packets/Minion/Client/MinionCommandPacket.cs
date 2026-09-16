namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionCommand</c>, which sends <c>(commandString,)</c> - whatever the player typed after
    /// <c>/cmd</c>, unparsed. Slash command 231 in the client's own table maps straight onto this
    /// with no validation of its own, so everything the grammar allows arrives here as text.
    ///
    /// The help text's examples are <c>/cmd passive follow</c>, <c>/cmd passive assist</c> and
    /// <c>/cmd aggressive stay</c>, so a command is one or more words and a stance may be combined
    /// with an order. See MinionManager.ParseCommand.
    /// </summary>
    public class MinionCommandPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionCommand;

        public string CommandString { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            CommandString = pr.ReadUnicodeString();
        }
    }
}
