namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// Firing an equipped tool. <c>actions/tools/basetoolaction.py</c> sends
    /// <c>(actionId, actionArgId, target)</c> - the same shape as RequestWeaponAttack without the
    /// alt-fire flag.
    ///
    /// Which of the two a click sends is decided entirely on the client: the armed item's entity
    /// class gives a <c>weaponAttackActionId</c>, and <c>actions/__init__.py</c> looks that id up
    /// in its own table to pick the action module. Six ids map to modules under
    /// <c>client/actions/tools/</c> and every one of them comes here. The server is not consulted,
    /// so there is no way to route these down the weapon path instead - a player holding a healing
    /// disc sends this opcode or nothing.
    ///
    /// For TOOL_HARVEST the arg id is the skill being used: 168 Salvage, 169 Tissue Extraction.
    /// </summary>
    public class RequestToolActionPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestToolAction;

        public ActionId ActionId { get; set; }
        public uint ActionArgId { get; set; }
        public ActionTarget Target { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            ActionId = (ActionId)pr.ReadInt();
            ActionArgId = pr.ReadUInt();
            Target = ActionTarget.Read(pr);
        }
    }
}
