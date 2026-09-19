namespace Rasa.Packets.Social.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:487 - SendChatMsg('AddFriendByName', (arg,)).
    ///
    /// Two callers send different string types for the same argument:
    ///
    ///  - the radial menu (radialwindow.py:484) passes the target's actor name, which the
    ///    server itself sent with WriteString in ActorNamePacket, so it comes back as a
    ///    0x4_ byte string;
    ///  - the social window's name box (socialwindow.py:933) passes TextEdit.GetText(),
    ///    which comes back as a 0x5_ unicode string - the same box feeds AddIgnoreByName.
    ///
    /// This used to read ReadString only, which throws "WTF? String type: 5D" on the
    /// second path; the throw escapes ReadPacket and disconnects the player. Accepts
    /// either encoding, as InviteUserToPartyByNamePacket does.
    /// </summary>
    public class AddFriendByNamePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AddFriendByName;

        public string FamilyName { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            FamilyName = SocialArgs.ReadName(pr);
        }
    }
}
