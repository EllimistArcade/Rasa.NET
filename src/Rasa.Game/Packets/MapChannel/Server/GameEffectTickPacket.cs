using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_GameEffectTick(effectId, *args): the effect did something, here is what. Sent on the
    /// entity that holds the effect; the client hands the arguments to the effect class's OnTick,
    /// and each class wants its own shape:
    ///
    ///  - DamageOverTime.OnTick(target, damageData): damageData is [(targetId, rawInfo), ...]
    ///    and the client announces each as damage from the effect's source (Ruin, the
    ///    Reconstruction harm effect).
    ///  - HealOverTime.OnTick(target, healData): [(targetId, amount), ...], announced as healing.
    ///  - RageSourceEffect.OnTick(target, targetIds): the ids of the squad members the aura just
    ///    reached, announced as attaches of the RAGE effect on them.
    ///  - BaseGameEffect.OnTick(target): nothing but the tick marker on the effect's FX.
    ///
    /// Kind picks the shape. Entries is empty for the bare tick.
    /// </summary>
    public class GameEffectTickPacket : ServerPythonPacket
    {
        public enum TickKind { Bare, Damage, Heal, EntityIds }

        public override GameOpcode Opcode { get; } = GameOpcode.GameEffectTick;

        public int EffectId { get; }
        public TickKind Kind { get; }
        public List<TickEntry> Entries { get; } = new List<TickEntry>();

        public GameEffectTickPacket(int effectId, TickKind kind)
        {
            EffectId = effectId;
            Kind = kind;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(Kind == TickKind.Bare ? 1 : 2);
            pw.WriteInt(EffectId);

            if (Kind == TickKind.Bare)
                return;

            pw.WriteList(Entries.Count);

            foreach (var entry in Entries)
            {
                switch (Kind)
                {
                    case TickKind.Damage:
                        pw.WriteTuple(2);
                        pw.WriteULong(entry.EntityId);
                        DamageInfoWriter.WriteRawInfo(pw, entry.DamageType, entry.Amount, entry.Resisted, false, entry.DeathBlow);
                        break;
                    case TickKind.Heal:
                        pw.WriteTuple(2);
                        pw.WriteULong(entry.EntityId);
                        pw.WriteInt(entry.Amount);
                        break;
                    case TickKind.EntityIds:
                        pw.WriteULong(entry.EntityId);
                        break;
                }
            }
        }
    }

    public class TickEntry
    {
        public ulong EntityId { get; set; }
        public int Amount { get; set; }
        public int Resisted { get; set; }
        public DamageType DamageType { get; set; }
        public bool DeathBlow { get; set; }
    }
}
