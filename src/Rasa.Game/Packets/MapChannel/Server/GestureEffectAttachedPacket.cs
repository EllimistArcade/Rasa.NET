namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// GameEffectAttached for a GESTURE_EFFECT.
    ///
    /// client/physicalentity.py Recv_GameEffectAttached(typeId, effectId, level, sourceId,
    /// announce, tooltipDict, *args) passes *args to the effect's Attach, and
    /// client/actions/gesture.py GestureEffect.OnAnnounceAttach reads them as
    /// (actionId, actionArgId, actionTargetId) to pick the looping animation and FX.
    /// GameEffectAttachedPacket carries them as Args now; this packet predates that and keeps
    /// its own layout.
    ///
    /// announce is false: the pose starts when the Gesture action reaches recovery and calls
    /// AnnounceGameEffectAttach, which on the gesturing player's own client may come before or
    /// after this packet (AttachGameEffect honours an announce made up to 10 s earlier).
    /// The tooltip dictionary is empty: gesture effects have no icon or tooltip, and a
    /// 'duration' key would start an expiry timer.
    /// </summary>
    public class GestureEffectAttachedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.GameEffectAttached;

        public int EffectId { get; set; }
        public uint ActionArgId { get; set; }
        public ulong SourceId { get; set; }
        public ulong TargetId { get; set; }

        public GestureEffectAttachedPacket(int effectId, uint actionArgId, ulong sourceId, ulong targetId)
        {
            EffectId = effectId;
            ActionArgId = actionArgId;
            SourceId = sourceId;
            TargetId = targetId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(9);
            pw.WriteInt(Gestures.EffectTypeId);                 // typeId
            pw.WriteInt(EffectId);                              // effectId
            pw.WriteUInt(ActionArgId);                          // level
            pw.WriteULong(SourceId);                            // sourceId
            pw.WriteBool(false);                                // announce
            pw.WriteDictionary(0);                              // tooltipDict
            pw.WriteUInt((uint)ActionId.Gesture);               // actionId
            pw.WriteUInt(ActionArgId);                          // actionArgId
            if (TargetId != 0)                                  // actionTargetId
                pw.WriteULong(TargetId);
            else
                pw.WriteNoneStruct();
        }
    }
}
