using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The client's shared/damageinfo.py rawInfo: the 12-tuple every damage announcement is
    /// built from - a weapon hit's hitdata, an ability's, a damage-over-time tick's, an effect's
    /// AnnounceDamage. One writer, so they all agree on the field order.
    /// </summary>
    public static class DamageInfoWriter
    {
        public static void WriteRawInfo(PythonWriter pw, DamageType damageType, long finalAmount, int resisted = 0, bool isCritical = false, bool deathBlow = false)
        {
            pw.WriteTuple(12);
            pw.WriteUInt((uint)damageType);     // damageType
            pw.WriteUInt(0);                    // reflected
            pw.WriteUInt(0);                    // filtered
            pw.WriteUInt(0);                    // absorbed
            pw.WriteUInt((uint)resisted);       // resisted
            pw.WriteLong(finalAmount);          // finalAmt
            pw.WriteInt(isCritical ? 1 : 0);    // isCrit
            pw.WriteInt(deathBlow ? 1 : 0);     // deathBlow
            pw.WriteUInt(0);                    // coverModifier
            pw.WriteInt(0);                     // wasImmune
            pw.WriteList(0);                    // targetEffectIds
            pw.WriteList(0);                    // sourceEffectIds
        }

        /// <summary>A tooltip or argument value of one of the types Python packets carry; a list of ints is a Python list.</summary>
        public static void WriteValue(PythonWriter pw, object value)
        {
            switch (value)
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
                case IList<int> list:
                    pw.WriteList(list.Count);
                    foreach (var item in list)
                        pw.WriteInt(item);
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"Python packet: unsupported value type {value.GetType().Name}");
                    pw.WriteNoneStruct();
                    break;
            }
        }
    }
}
