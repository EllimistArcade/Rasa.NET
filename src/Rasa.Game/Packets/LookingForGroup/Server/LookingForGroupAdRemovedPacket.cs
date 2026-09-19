namespace Rasa.Packets.LookingForGroup.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells the client its ad is gone.
    ///
    /// Recv_LookingForGroupAdRemoved() takes no arguments (client/lookingforgroupmanager.py:81)
    /// and raises UI_STATUS_UPDATER_KILL_INDICATOR for 'LFGAdPlaced', which is the only
    /// thing that clears the permanent status indicator LookingForGroupAdPlaced added.
    /// Without it the indicator sits on screen for the rest of the session.
    /// </summary>
    public class LookingForGroupAdRemovedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.LookingForGroupAdRemoved;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
