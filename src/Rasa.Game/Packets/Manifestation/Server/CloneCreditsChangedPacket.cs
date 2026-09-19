namespace Rasa.Packets.Manifestation.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// A character-selection pod's clone credit count, after that character has been cloned from.
    ///
    /// Addressed to the pod, not the player: the handler is
    /// <c>CharacterSelectionPod.Recv_CloneCreditsChanged</c>, and pods live at
    /// <c>CharacterManager.SelectionPodStartEntityId + slot</c>. Its own docstring is "this player
    /// has been cloned from and its clone credits have changed".
    ///
    /// CharacterInfo carries the same number in its character record, and re-sending that for the
    /// source slot is what kept the count right before this existed. The difference is the
    /// redraw: this posts UI_UPDATE_CHARACTER_SELECTION_SLOT_CLONE_CREDITS, which repaints the
    /// stats panel immediately if that slot happens to be the selected one, while CharacterInfo
    /// reaches <c>_UpdatePod</c> and <c>_AutoSelectCharacter</c>, neither of which repaints. Both
    /// are sent; this is the cheaper and more direct of the two, and CharacterInfo still carries
    /// everything else about the slot that changed.
    /// </summary>
    public class CloneCreditsChangedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CloneCreditsChanged;

        public uint CloneCredits { get; }

        public CloneCreditsChangedPacket(uint cloneCredits)
        {
            CloneCredits = cloneCredits;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt(CloneCredits);
        }
    }
}
