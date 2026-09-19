namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_ActionReuseTimerRestarted(actionId, actionArgId): start the action's cooldown now.
    /// The client looks the reuse time up in its own copy of the action table, so only the ids
    /// travel. Sent for actions whose startReuseTimerOnPerform flag is clear; for the others the
    /// client starts the timer itself when it performs the action.
    /// </summary>
    public class ActionReuseTimerRestartedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ActionReuseTimerRestarted;

        public ActionId ActionId { get; }
        public uint ActionArgId { get; }

        public ActionReuseTimerRestartedPacket(ActionId actionId, uint actionArgId)
        {
            ActionId = actionId;
            ActionArgId = actionArgId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt((uint)ActionId);
            pw.WriteUInt(ActionArgId);
        }
    }
}
