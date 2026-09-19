namespace Rasa.Packets.LookingForGroup.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/lookingforgroupmanager.py:55 - SendWorldMsg('RemoveLookingForGroupAd', ()).
    ///
    /// No arguments: the server removes whatever ad the sending account has placed.
    /// Sent by the "remove ad" button on the ad status window (lfgadstatuswindow.py:72).
    /// </summary>
    public class RemoveLookingForGroupAdPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveLookingForGroupAd;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
