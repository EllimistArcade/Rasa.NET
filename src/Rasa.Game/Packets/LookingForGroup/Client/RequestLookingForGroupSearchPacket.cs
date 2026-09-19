namespace Rasa.Packets.LookingForGroup.Client
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// client/lookingforgroupmanager.py:47 - SendWorldMsg('RequestLookingForGroupSearch', (adInfoTuple,)).
    ///
    /// The same ad tuple as RequestCreateLookingForGroupAd, but read as a filter rather
    /// than an ad: the search tab fills in the criteria the player wants matched. Also
    /// sent by the ad status window's "see matches" button, which replays the player's
    /// own ad as the filter (ShowMatchesforCurrentAd, lookingforgroupmanager.py:59).
    /// </summary>
    public class RequestLookingForGroupSearchPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestLookingForGroupSearch;

        public LookingForGroupAdInfo Filter { get; } = new LookingForGroupAdInfo();

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();     // (adInfoTuple,)
            Filter.Read(pr);
        }
    }
}
