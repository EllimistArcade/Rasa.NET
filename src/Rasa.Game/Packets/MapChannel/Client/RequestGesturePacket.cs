namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/actions/gesture.py Gesture.SendServerRequest:
    /// SendCallActorMethod('RequestGesture', (self.actionArgId, self.targetId)).
    ///
    /// targetId is the entity under the mouse (gameui.PerformSlashCommandAction), so it is None
    /// whenever the player emotes at nothing. It used to be read with ReadULong, which throws on
    /// None, and the throw disconnected the player. An entity id the server sent with WriteULong
    /// comes back as a long; a small one typed into the console would be an int.
    /// </summary>
    public class RequestGesturePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestGesture;

        public uint GestureId { get; set; }

        /// <summary>0 when the gesture has no target.</summary>
        public ulong TargetEntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            GestureId = pr.ReadUInt();

            switch (pr.PeekType())
            {
                case PythonType.Structs:
                    pr.ReadUnkStruct();
                    TargetEntityId = 0;
                    break;

                case PythonType.Long:
                    TargetEntityId = pr.ReadULong();
                    break;

                default:
                    var value = pr.ReadInt();
                    TargetEntityId = value > 0 ? (ulong)value : 0;
                    break;
            }
        }
    }
}
