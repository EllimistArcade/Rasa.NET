namespace Rasa.Packets.Social.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:527 - SendChatMsg('AddIgnoreByName', (arg,)), from the social
    /// window's name box and from /ignore and /addignore.
    /// </summary>
    public class AddIgnoreByNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddIgnoreByName;

        public string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = SocialArgs.ReadName(pr);
        }
    }
}
