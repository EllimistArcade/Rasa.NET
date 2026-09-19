using System.IO;

namespace Rasa.Packets.Communicator
{
    using Data;

    /// <summary>
    /// Auth -> game: an account was banned or unbanned on the Auth console. Auth refuses a locked
    /// account at login, but a player already past that point - on the server list, handed to a
    /// queue, or in the world - only finds out through this.
    /// </summary>
    public class AccountLockChangedPacket : IOpcodedPacket<CommOpcode>
    {
        public CommOpcode Opcode { get; } = CommOpcode.AccountLockChanged;
        public uint AccountId { get; set; }
        public bool Locked { get; set; }

        public void Read(BinaryReader br)
        {
            AccountId = br.ReadUInt32();
            Locked = br.ReadBoolean();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte) Opcode);
            bw.Write(AccountId);
            bw.Write(Locked);
        }
    }
}
