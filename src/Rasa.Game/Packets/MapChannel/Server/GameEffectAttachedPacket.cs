using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_GameEffectAttached(typeId, effectId, level, sourceId, announce, tooltipDict, *args).
    /// The client creates the effect class for typeId and calls its Attach(target, sourceId,
    /// *args), so whatever follows the tooltip dictionary in the tuple is passed straight to that
    /// effect's OnAttach. The sprint effect, for one, takes a single bead-modifier float there;
    /// most effects take nothing. Args holds those trailing values, each written as its own
    /// tuple element - a list in that position would arrive as one argument, a list.
    /// </summary>
    public class GameEffectAttachedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.GameEffectAttached;

        public int EffectTypeId { get; set; }
        public int EffectId { get; set; }
        public uint EffectLevel { get; set; }
        public ulong SourceId { get; set; }
        public bool Announced { get; set; }
        // tooltip
        public int Duration { get; set; }
        public int DamageType { get; set; }
        public int AttrId { get; set; }
        public bool IsActive { get; set; }
        public bool IsBuff { get; set; }
        public bool IsDebuff { get; set; }
        public bool IsNegativeEffect { get; set; }

        /// <summary>Extra OnAttach arguments; double, int, uint, long, ulong, bool, string or null.</summary>
        public List<object> Args { get; set; } = new List<object>();

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(6 + Args.Count);
            pw.WriteInt(EffectTypeId);      //typeId
            pw.WriteInt(EffectId);          //effectId
            pw.WriteUInt(EffectLevel);      //level
            pw.WriteULong(SourceId);        //sourceId
            pw.WriteBool(Announced);        //announce
            pw.WriteDictionary(7);          //tooltipDict
            pw.WriteString("duration");
            pw.WriteInt(Duration);
            pw.WriteString("damageType");
            pw.WriteInt(DamageType);
            pw.WriteString("attrId");
            pw.WriteInt(AttrId);
            pw.WriteString("isActive");
            pw.WriteBool(IsActive);
            pw.WriteString("isBuff");
            pw.WriteBool(IsBuff);
            pw.WriteString("isDebuff");
            pw.WriteBool(IsDebuff);
            pw.WriteString("isNegativeEffect");
            pw.WriteBool(IsNegativeEffect);

            foreach (var arg in Args)
            {
                switch (arg)
                {
                    case null: pw.WriteNoneStruct(); break;
                    case double d: pw.WriteDouble(d); break;
                    case float f: pw.WriteDouble(f); break;
                    case int i: pw.WriteInt(i); break;
                    case uint u: pw.WriteUInt(u); break;
                    case long l: pw.WriteLong(l); break;
                    case ulong ul: pw.WriteULong(ul); break;
                    case bool b: pw.WriteBool(b); break;
                    case string s: pw.WriteString(s); break;
                    default:
                        Logger.WriteLog(LogType.Error, $"GameEffectAttached: unsupported attach argument type {arg.GetType().Name}");
                        pw.WriteNoneStruct();
                        break;
                }
            }
        }
    }
}
