namespace Rasa.Packets.Clan.Client
{
    using Data;
    using Memory;

    /// <summary>The clan lockbox window's tab purchase button: <c>(tabId,)</c>.</summary>
    public class PurchaseClanLockboxTabPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PurchaseClanLockboxTab;

        public int TabId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            TabId = pr.ReadInt();
        }
    }
}
