namespace Rasa.Packets.Social.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:503 - SendChatMsg('RemoveFriendByName', (arg,)), from /removefriend
    /// and /rfriend. The social window's Remove button uses RemoveFriend with the row's account id;
    /// only the slash commands come through here.
    /// </summary>
    public class RemoveFriendByNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveFriendByName;

        public string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = SocialArgs.ReadName(pr);
        }
    }
}
