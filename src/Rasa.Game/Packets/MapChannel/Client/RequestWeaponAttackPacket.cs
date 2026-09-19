namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    public class RequestWeaponAttackPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestWeaponAttack;

        public ActionId ActionId { get; set; }
        public int ActionArgId { get; set; }
        public long TargetId { get; set; }
        /// <summary>
        /// The alternate attack - the melee swing every weapon has besides its own fire.
        /// baseweaponattack.py sets it when the action it is sending is not the weapon's own
        /// attack pair, and sends it as the fourth element.
        /// </summary>
        public bool IsAltAction { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ActionId = (ActionId)pr.ReadInt();
            ActionArgId = pr.ReadInt();
            if (pr.PeekType() == PythonType.Long)   // has target
                TargetId = pr.ReadLong();
            else
                pr.ReadNoneStruct();                // no target
            IsAltAction = pr.ReadBool();
        }
    }
}
