namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// Traitor (AA_SPY_TRAITOR 393, abilities.traitor): an enemy turns on its own for a while. The
    /// client's TraitorAction is aimed at one hostile creature, and only a BIOLOGICAL or MACHINA
    /// one; its target effect, TRAITOR_EFFECT (10000057, FX 1113), is announced on the target
    /// by the recovery. Per pump: DURATION 10-30 s, EFFECT_RADIUS 10-50 m.
    ///
    /// For the effect's duration the creature is AFS:
    /// - its hate is wiped and it stops what it was doing, so it no longer goes for the players;
    /// - AFS creatures never attack players, and a creature's scan goes for creatures of any
    ///   other faction, so it turns on the Bane around it - within EFFECT_RADIUS, which stands
    ///   in for its aggro range - and they on it;
    /// - the clients are told its target category is AFS, so it shows as friendly, and players'
    ///   abilities (AbilityManager.IsHostile) leave it alone;
    /// - its MasterEntityId is the caster, so what it kills is the caster's
    ///   (CreatureManager.HandleCreatureKill), and its stance Aggressive, since a creature with a
    ///   master only looks for enemies when it is (BehaviorManager.ScansForEnemies).
    /// When the effect ends - run out, or the creature dead - it is Bane again with its own aggro
    /// range, its hate wiped once more, and whoever it finds first is who it fights.
    /// </summary>
    public partial class AbilityManager
    {
        public const string TraitorModule = "abilities.traitor";
        public const int TraitorTypeId = 10000057;               // TRAITOR_EFFECT

        /// <summary>Whether the client's TraitorAction would allow this target: a BIOLOGICAL or MACHINA creature.</summary>
        public static bool CanTurnTraitor(Creature creature)
        {
            if (creature == null)
                return false;

            var flags = CreatureManager.CreatureFlagsOf(creature);

            return flags.Contains((int)CreatureFlag.Biological) || flags.Contains((int)CreatureFlag.Machina);
        }

        /// <summary>Turns the creature for DURATION; false when it cannot be turned.</summary>
        private bool AttachTraitor(MapChannel mapChannel, Manifestation player, Creature target, ActionLevelInfo info)
        {
            if (!CanTurnTraitor(target) || !IsHostile(player, target) || target.Faction == Factions.AFS)
                return false;

            var effect = NewEffect(mapChannel, player, info, TraitorTypeId, info.Get(AbilityProperty.Duration, 10));

            effect.IsBuff = false;
            effect.AllowDetach = false;

            var faction = target.Faction;
            var aggroRange = target.AggroRange;
            var master = target.MasterEntityId;
            var stance = target.Stance;

            effect.OnDetached = (map, actor, e) =>
            {
                if (!(actor is Creature turned))
                    return;

                turned.Faction = faction;
                turned.AggroRange = aggroRange;
                turned.MasterEntityId = master;
                turned.Stance = stance;
                turned.Hate.Clear();

                if (map != null && turned.State != CharacterState.Dead)
                {
                    CellManager.Instance.CellCallMethod(turned, new TargetCategoryPacket(faction));
                    BehaviorManager.Instance.StopFighting(turned);
                }
            };

            GameEffectManager.Instance.Attach(mapChannel, target, effect);

            target.Faction = Factions.AFS;
            target.AggroRange = info.Get(AbilityProperty.EffectRadius, 10);
            target.MasterEntityId = player.EntityId;
            target.Stance = MinionStance.Aggressive;   // a creature with a master only scans when aggressive
            target.Hate.Clear();

            CellManager.Instance.CellCallMethod(target, new TargetCategoryPacket(Factions.AFS));
            BehaviorManager.Instance.StopFighting(target);

            // The rest forget it was ever on their side.
            foreach (var cell in CellManager.CellsIn(mapChannel, target.Cells))
                foreach (var other in cell.CreatureList)
                    if (other != target)
                        other.Hate.Remove(target.EntityId);

            return true;
        }
    }
}
