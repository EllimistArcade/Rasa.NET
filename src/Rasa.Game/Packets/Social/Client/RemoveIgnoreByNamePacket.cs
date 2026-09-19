namespace Rasa.Packets.Social.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:535 - SendChatMsg('RemoveIgnoreByName', (arg,)), from /removeignore
    /// and /unignore. The social window's own Remove button uses RemoveIgnore with an account id.
    /// </summary>
    public class RemoveIgnoreByNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RemoveIgnoreByName;

        public string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = SocialArgs.ReadName(pr);
        }
    }
}
