namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SelectNewCharacterClass(chosenClassId) - the Train button in the tier select window.
    ///
    /// gameui.py's OnChooseTierAdvance sends one class id and nothing else. The window only
    /// enables Train for a class the client thinks is trainable, and only when the server said
    /// CanTrain - but neither of those constrains what arrives here, so the id is read as a
    /// claim and checked against the tree and the character's level before anything is written.
    /// </summary>
    public class SelectNewCharacterClassPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SelectNewCharacterClass;

        public uint ClassId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ClassId = pr.ReadUInt();
        }
    }
}
