namespace Rasa.Packets.MapChannel.Server
{
    using System.Collections.Generic;

    using Data;
    using Memory;

    /// <summary>
    /// Actor.Recv_AnnounceMapDamage(rawInfo): "Map damaged this actor (falling)". The damage record
    /// is announced as a hit with no source: the number over the actor, "You are hit for N" in the
    /// combat log (PM_COMBAT_HIT_ON_YOU_NO_SOURCE), a squad mate's log line with no attacker, the
    /// get-hit animation, and the health bar - and a death, for a death blow.
    ///
    /// With nothing dealt it shows nothing of the kind, and still announces the effect types in
    /// targetEffectIds on the actor (Actor.AnnounceDamage): Managers.QuietLandings uses it so.
    /// </summary>
    public class AnnounceMapDamagePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.AnnounceMapDamage;

        public int Amount { get; }
        public DamageType DamageType { get; }
        public bool DeathBlow { get; }
        public IReadOnlyList<uint> TargetEffectIds { get; }

        public AnnounceMapDamagePacket(int amount, DamageType damageType = DamageType.Environmental, bool deathBlow = false,
            IReadOnlyList<uint> targetEffectIds = null)
        {
            Amount = amount;
            DamageType = damageType;
            DeathBlow = deathBlow;
            TargetEffectIds = targetEffectIds;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            DamageInfoWriter.WriteRawInfo(pw, DamageType, Amount, deathBlow: DeathBlow, targetEffectIds: TargetEffectIds);
        }
    }
}
