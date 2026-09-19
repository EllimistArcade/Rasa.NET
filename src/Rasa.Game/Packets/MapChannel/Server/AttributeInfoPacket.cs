using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;
    using Structures;

    public class AttributeInfoPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AttributeInfo;
        
        public Dictionary<Attributes, ActorAttributes> ActorAttributes { get; set; }
        
        public AttributeInfoPacket(Dictionary<Attributes, ActorAttributes> actorAttributes)
        {
            ActorAttributes = actorAttributes;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteDictionary(ActorAttributes.Count);
            foreach (var entry in ActorAttributes)
            {
                var attribute = entry.Value;
                pw.WriteInt((int)attribute.AttributeId);
                pw.WriteTuple(5);

                // normalMax, currentMax, current - in that order, and not the order the
                // docstring on Recv_AttributeInfo claims. The handler does
                // `apply(ActorAttribute, (entityId, attrType) + attrData)` and the constructor's
                // parameters are (actorId, type, normalMax, currentMax, current, ...), so the
                // first field of the tuple lands in normalMax however the comment above it reads.
                //
                // Current and normalMax were the other way round here. It was invisible for as
                // long as this only went out at moments when they were equal - login, level up,
                // a full stat reset - and stops being invisible the moment it is sent to a player
                // who is hurt, which is exactly what a combat transition is.
                pw.WriteInt(attribute.NormalMax);
                pw.WriteInt(attribute.CurrentMax);
                pw.WriteInt(attribute.Current);
                pw.WriteInt(attribute.RefreshAmount);
                pw.WriteInt(attribute.RefreshPeriod);
            }
        }
    }
}
