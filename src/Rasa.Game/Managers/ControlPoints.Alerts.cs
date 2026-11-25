using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Structures;

    /// <summary>
    /// A point's defenders alerted: an enemy of its holder who comes into the point's alert
    /// radius has the defenders set on them, and a player who begins the use that captures it
    /// raises the alarm louder still.
    ///
    /// The client says nothing of either. The weights are those of the server's design for the
    /// fight at a point: 500 hate on an intruder (<see cref="ControlPointConfig.DefenderAlertHate"/>),
    /// 2,500 on a player using the point (<see cref="ControlPointConfig.CaptureAlarmHate"/>). The rest is ours:
    ///  - The trigger area is a radius about the point's object (<see cref="ControlPointConfig.DefenderAlertRadius"/>),
    ///    looked at on the control point worker's pass, once a second. Whoever is inside it on a
    ///    pass and was not on the one before has come in; they are out again once
    ///    <see cref="AlertExitMargin"/> further off, so someone standing at its edge is not
    ///    coming in over and over. Leaving and coming back alerts them again.
    ///  - The defenders are the holder's garrison of the point: the living creatures of its
    ///    pools, the service NPCs among them left out.
    ///  - A defender is alerted to someone it would go looking for (TargetCategories.Seeks) and
    ///    can fight now (Threat.CanFight: not cloaked, dead or gone) - so the AFS's turrets are
    ///    set on Bane coming in to an AFS point and never on players, and a player the Bane
    ///    cannot see goes unnoticed. A game master who is always friendly is nobody's enemy.
    ///  - Only a defender within <see cref="BehaviorManager.MaxChaseDistance"/> of the enemy is
    ///    alerted: one further off would only walk to the end of its leash and go home again,
    ///    forgetting it all.
    ///  - The hate is added (Threat.Alerted): an idle defender turns on the enemy, and one
    ///    already fighting turns too unless whoever it is fighting is hated more.
    ///
    /// The capture alarm can only find defenders where a point is in service with some of them
    /// still alive. A Bane point is in service only once its whole garrison is dead, and an AFS
    /// or clan point's garrison does not seek players, so as the capture rules stand it rarely
    /// finds anyone.
    /// </summary>
    public partial class ControlPoints
    {
        /// <summary>How much further than the alert radius someone has to go to have left it, in metres.</summary>
        public const float AlertExitMargin = 5f;

        /// <summary>
        /// Who has come into the point's alert radius since the last pass: its defenders are
        /// set on each of them. From <see cref="Worker"/>.
        /// </summary>
        internal void Alerts(MapChannel mapChannel, Point point)
        {
            var radius = Config.DefenderAlertRadius;

            if (point.Object == null || radius <= 0 || Config.DefenderAlertHate <= 0)
            {
                point.Inside.Clear();
                return;
            }

            var centre = point.Object.Position;
            var inside = new HashSet<ulong>();
            List<Actor> entered = null;

            foreach (var actor in ActorsNear(mapChannel, centre, radius + AlertExitMargin))
            {
                var distance = Flat(actor.Position, centre);

                if (point.Inside.Contains(actor.EntityId))
                    inside.Add(actor.EntityId);
                else if (distance <= radius)
                {
                    inside.Add(actor.EntityId);
                    (entered ??= new List<Actor>()).Add(actor);
                }
            }

            point.Inside.Clear();
            point.Inside.UnionWith(inside);

            if (entered == null)
                return;

            var defenders = Defenders(mapChannel, point);

            foreach (var enemy in entered)
                Alert(defenders, enemy, Config.DefenderAlertHate);
        }

        /// <summary>
        /// A player has begun the use that captures the point: every defender in reach that would
        /// fight them hates them by <see cref="ControlPointConfig.CaptureAlarmHate"/>. From
        /// DynamicObjectManager, as the use begins.
        /// </summary>
        public void CaptureAlarm(MapChannel mapChannel, Point point, Actor claimant)
        {
            if (mapChannel == null || point == null || claimant == null || point.IsBattleground || mapChannel.IsCopy || Config.CaptureAlarmHate <= 0)
                return;

            Alert(Defenders(mapChannel, point), claimant, Config.CaptureAlarmHate);
        }

        /// <summary>The holder's garrison of the point alive on the map, its service NPCs left out.</summary>
        internal List<Creature> Defenders(MapChannel mapChannel, Point point)
        {
            var pools = PoolsOn(mapChannel, point.PoolsOf(point.Owner)).Where(pool => ServicePointOf(mapChannel, pool) == null).ToHashSet();

            if (pools.Count == 0)
                return new List<Creature>();

            return mapChannel.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Where(creature => creature.SpawnPool != null && pools.Contains(creature.SpawnPool) && CreatureManager.IsLivingOnMap(mapChannel, creature))
                .ToList();
        }

        /// <summary>Each defender that would fight <paramref name="enemy"/> and is near enough to get to them hates them by <paramref name="hate"/>.</summary>
        private static void Alert(List<Creature> defenders, Actor enemy, int hate)
        {
            foreach (var defender in defenders)
                if (Seeks(defender, enemy) && Flat(defender.Position, enemy.Position) <= BehaviorManager.MaxChaseDistance)
                    Threat.Alerted(defender, enemy, hate);
        }

        /// <summary>Whether a defender would go after this enemy now: one it seeks out, and can fight.</summary>
        internal static bool Seeks(Creature defender, Actor enemy)
        {
            if (defender == null || enemy == null || defender == enemy)
                return false;

            TargetCategory category;

            switch (enemy)
            {
                case Manifestation player:
                    if (player.GmFlagAlwaysFriendly)
                        return false;
                    category = player.CombatCategory;
                    break;
                case Creature creature:
                    category = creature.TargetCategory;
                    break;
                default:
                    return false;
            }

            return TargetCategories.Seeks(defender.TargetCategory, category) && Threat.CanFight(defender, enemy.EntityId);
        }

        /// <summary>The players in game and the creatures alive within <paramref name="range"/> of a place on the map.</summary>
        private static IEnumerable<Actor> ActorsNear(MapChannel mapChannel, Vector3 centre, float range)
        {
            if (mapChannel.ClientList != null)
                foreach (var client in mapChannel.ClientList.ToArray())
                {
                    var player = client?.Player;

                    if (client.State == ClientState.Ingame && player != null && !player.IsGone && player.MapChannel == mapChannel
                        && player.State != CharacterState.Dead && Flat(player.Position, centre) <= range)
                        yield return player;
                }

            var reach = (int)Math.Ceiling(range / CellManager.CellSize);
            var centreX = (uint)(centre.X / CellManager.CellSize + CellManager.CellBias);
            var centreZ = (uint)(centre.Z / CellManager.CellSize + CellManager.CellBias);
            var cells = mapChannel.MapCellInfo.Cells;

            for (var dx = -reach; dx <= reach; dx++)
                for (var dz = -reach; dz <= reach; dz++)
                {
                    var cellX = (uint)(centreX + dx);
                    var cellZ = (uint)(centreZ + dz);

                    if (!cells.TryGetValue((cellX & 0xFFFF) | (cellZ << 16), out var cell))
                        continue;

                    foreach (var creature in cell.CreatureList.ToArray())
                        if (creature != null && creature.State != CharacterState.Dead && creature.State != CharacterState.Dying
                            && creature.Attributes[Attributes.Health].Current > 0 && Flat(creature.Position, centre) <= range)
                            yield return creature;
                }
        }

        /// <summary>Distance across the ground, as the leash measures it.</summary>
        private static float Flat(Vector3 a, Vector3 b)
        {
            var x = a.X - b.X;
            var z = a.Z - b.Z;

            return MathF.Sqrt(x * x + z * z);
        }
    }
}
