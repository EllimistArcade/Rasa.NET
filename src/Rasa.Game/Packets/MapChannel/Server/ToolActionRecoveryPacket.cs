using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>What one target got out of a tool being used on it.</summary>
    public readonly struct ToolHit
    {
        public ulong EntityId { get; }

        /// <summary>The single number, for a tool whose hit data is one. What it means is the
        /// tool's business: health for the healing disc, armour for the augmentation.</summary>
        public int Amount { get; }

        public int Armor { get; }
        public int Health { get; }

        /// <summary>
        /// True when the hit data is the (armour, health) pair rather than one number. Only the
        /// field repair tool sends the pair.
        /// </summary>
        public bool IsPair { get; }

        public ToolHit(ulong entityId, int amount)
        {
            EntityId = entityId;
            Amount = amount;
            Armor = 0;
            Health = 0;
            IsPair = false;
        }

        public ToolHit(ulong entityId, int armor, int health)
        {
            EntityId = entityId;
            Amount = 0;
            Armor = armor;
            Health = health;
            IsPair = true;
        }
    }

    /// <summary>
    /// PerformRecovery carrying a tool's results. Same envelope as the missile form -
    /// <c>(actionId, actionArgId, hits, misses, missdata, hitdata)</c>, which the client hands
    /// straight to <c>TargetedAction.DoAction(actor, hits, misses, missdata, hitdata)</c> - but
    /// the hit data is not a damage record. Each tool module reads it its own way:
    ///
    ///  - healdisc.py takes one number and calls <c>target.AnnounceHealing(sourceId, data)</c>
    ///  - armoraug.py takes one number and calls <c>target.FloatAttrChange(ARMOR, amt)</c>
    ///  - repairtool.py unpacks <c>(armorAmount, healthAmount) = data</c> and announces each
    ///
    /// So a scalar for two of them and a two-tuple for the third, which is why this is its own
    /// packet rather than another PerformType on the missile one: nothing is shared past the
    /// envelope, and a damage record in this slot would raise on the client.
    ///
    /// The amounts are what actually went on, not what was asked for, so a target that was
    /// nearly full sees the smaller number - and a hit that applied nothing is left out
    /// entirely, since every announce is guarded by <c>if amount:</c> anyway.
    /// </summary>
    public class ToolActionRecoveryPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PerformRecovery;

        public ActionId ActionId { get; set; }
        public uint ActionArgId { get; set; }
        public List<ToolHit> Hits { get; set; }

        internal ToolActionRecoveryPacket(ActionId actionId, uint actionArgId, List<ToolHit> hits)
        {
            ActionId = actionId;
            ActionArgId = actionArgId;
            Hits = hits ?? new List<ToolHit>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(6);
            pw.WriteUInt((uint)ActionId);
            pw.WriteUInt(ActionArgId);

            pw.WriteList(Hits.Count);                    // hits
            foreach (var hit in Hits)
                pw.WriteULong(hit.EntityId);

            pw.WriteList(0);                             // misses
            pw.WriteList(0);                             // missdata

            pw.WriteList(Hits.Count);                    // hitdata, one entry per hit
            foreach (var hit in Hits)
            {
                if (hit.IsPair)
                {
                    pw.WriteTuple(2);
                    pw.WriteInt(hit.Armor);
                    pw.WriteInt(hit.Health);
                }
                else
                {
                    pw.WriteInt(hit.Amount);
                }
            }
        }
    }
}
