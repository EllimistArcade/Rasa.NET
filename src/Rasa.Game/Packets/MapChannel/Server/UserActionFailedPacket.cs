namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/augmentations/actor.py:1356 - Recv_UserActionFailed(actionId, actionArgId, msgId).
    ///
    /// The server's refusal of an action this player asked for, addressed to the player's own
    /// actor. It does three things client-side, and the third is the one that matters most:
    ///
    ///  - shows the player message, when one is given (msgId may be None)
    ///  - cancels the action if it is still the current one, and stops auto-fire
    ///  - pops the action off <c>__unresolvedActions</c>
    ///
    /// The client pushes every request onto that list when it sends it and pops it on resolution
    /// or failure. A server that simply ignores a request leaves the entry there for the rest of
    /// the session, so refusing explicitly is not only politeness - it is what keeps the client's
    /// own bookkeeping straight.
    ///
    /// ActionFailed (13) is the quieter sibling: it cancels the action but shows nothing.
    /// </summary>
    public class UserActionFailedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.UserActionFailed;

        public ActionId ActionId { get; set; }
        public uint ActionArgId { get; set; }

        /// <summary>The message to show, or null to fail the action silently.</summary>
        public PlayerMessage? MsgId { get; set; }

        internal UserActionFailedPacket(ActionId actionId, uint actionArgId, PlayerMessage? msgId)
        {
            ActionId = actionId;
            ActionArgId = actionArgId;
            MsgId = msgId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteUInt((uint)ActionId);
            pw.WriteUInt(ActionArgId);

            if (MsgId.HasValue)
                pw.WriteUInt((uint)MsgId.Value);
            else
                pw.WriteNoneStruct();
        }
    }
}
