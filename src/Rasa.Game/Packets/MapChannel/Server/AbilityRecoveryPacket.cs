using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// PerformRecovery(actionId, actionArgId, hits, misses, missdata, hitdata) for an ability:
    /// the server has resolved it, here is who it hit and what it did to them. Sent on the
    /// performer's entity to everyone who can see them, the performer included - their client's
    /// DoAction runs the ability's own DoAbility over these lists to show the numbers.
    ///
    /// The shape of each hitdata entry is the ability class's business (client/actions/
    /// abilities): damage abilities (DamageBase) want (rawInfo, onHitData) where rawInfo is the
    /// same 12-tuple a weapon hit carries; heals (HealBase, HealAbility) want a bare amount.
    /// Kind picks which. hits and hitdata must be the same length - DamageBase.DoAbility indexes
    /// one with the other.
    /// </summary>
    public class AbilityRecoveryPacket : ServerPythonPacket
    {
        public enum HitDataKind { None, Damage, Heal }

        public override GameOpcode Opcode { get; } = GameOpcode.PerformRecovery;

        public ActionId ActionId { get; }
        public uint ActionArgId { get; }
        public HitDataKind Kind { get; }
        public List<AbilityHit> Hits { get; } = new List<AbilityHit>();
        public List<ulong> Misses { get; } = new List<ulong>();

        /// <summary>For lightning: OnHitData is (arcData,), the arcs to draw. Empty for everything else.</summary>
        public bool ArcData { get; set; }

        public AbilityRecoveryPacket(ActionId actionId, uint actionArgId, HitDataKind kind)
        {
            ActionId = actionId;
            ActionArgId = actionArgId;
            Kind = kind;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(6);
            pw.WriteUInt((uint)ActionId);
            pw.WriteUInt(ActionArgId);

            pw.WriteList(Hits.Count);
            foreach (var hit in Hits)
                pw.WriteULong(hit.EntityId);

            pw.WriteList(Misses.Count);
            foreach (var miss in Misses)
                pw.WriteULong(miss);

            pw.WriteList(0);                    // missdata

            pw.WriteList(Kind == HitDataKind.None ? 0 : Hits.Count);

            foreach (var hit in Hits)
            {
                switch (Kind)
                {
                    case HitDataKind.Heal:
                        pw.WriteInt(hit.Amount);
                        break;
                    case HitDataKind.Damage:
                        pw.WriteTuple(2);
                        pw.WriteTuple(12);                  // rawInfo
                        pw.WriteUInt((uint)hit.DamageType); // damageType
                        pw.WriteUInt(0);                    // reflected
                        pw.WriteUInt(0);                    // filtered
                        pw.WriteUInt(0);                    // absorbed
                        pw.WriteUInt(0);                    // resisted
                        pw.WriteLong(hit.Amount);           // finalAmt
                        pw.WriteInt(hit.IsCritical ? 1 : 0);// isCrit
                        pw.WriteInt(hit.DeathBlow ? 1 : 0); // deathBlow
                        pw.WriteUInt(0);                    // coverModifier
                        pw.WriteInt(0);                     // wasImmune
                        pw.WriteList(0);                    // targetEffectIds
                        pw.WriteList(0);                    // sourceEffectIds
                        if (ArcData)
                        {
                            pw.WriteTuple(1);               // onHitData = (arcData,)
                            pw.WriteList(0);
                        }
                        else
                            pw.WriteNoneStruct();           // onHitData
                        break;
                }
            }
        }
    }

    public class AbilityHit
    {
        public ulong EntityId { get; set; }
        public int Amount { get; set; }
        public DamageType DamageType { get; set; }
        public bool IsCritical { get; set; }
        public bool DeathBlow { get; set; }
    }
}
