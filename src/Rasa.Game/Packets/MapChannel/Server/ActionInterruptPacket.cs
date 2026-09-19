namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/augmentations/actor.py Recv_ActionInterrupt(sourceId, actionId, actionArgId).
    /// Called on the acting entity; cancels its current action when the action and arg match.
    /// </summary>
    public class ActionInterruptPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ActionInterrupt;

        public ulong SourceId { get; set; }
        public ActionId ActionId { get; set; }
        public uint ActionArgId { get; set; }

        public ActionInterruptPacket(ulong sourceId, ActionId actionId, uint actionArgId)
        {
            SourceId = sourceId;
            ActionId = actionId;
            ActionArgId = actionArgId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteULong(SourceId);
            pw.WriteUInt((uint)ActionId);
            pw.WriteUInt(ActionArgId);
        }
    }
}
