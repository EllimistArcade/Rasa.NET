namespace Rasa.Packets.LookingForGroup.Client
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// client/lookingforgroupmanager.py:36 - SendWorldMsg('RequestCreateLookingForGroupAd', (adInfoTuple,)).
    ///
    /// Sent when the player submits the create-ad tab. The window hides itself the moment
    /// it sends (lookingforgroupwindow.py:523), so LookingForGroupAdPlaced is the only
    /// confirmation the player ever sees.
    /// </summary>
    public class RequestCreateLookingForGroupAdPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestCreateLookingForGroupAd;

        public LookingForGroupAdInfo AdInfo { get; } = new LookingForGroupAdInfo();

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();     // (adInfoTuple,)
            AdInfo.Read(pr);
        }
    }
}
