namespace Rasa.Packets.LookingForGroup.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Acknowledges that the player's ad is now live.
    ///
    /// Recv_LookingForGroupAdPlaced() takes no arguments (client/lookingforgroupmanager.py:89).
    /// It raises UI_SHOW_LFG_AD_PLACED, which adds the permanent 'LFGAdPlaced' status
    /// indicator, and UI_SHOW_LFG_AD_STATUS_WINDOW, which opens the ad status window.
    /// Until this arrives the client shows no sign the ad was accepted - the create
    /// window hides itself the moment it sends, so this is the only feedback there is.
    /// </summary>
    public class LookingForGroupAdPlacedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.LookingForGroupAdPlaced;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
