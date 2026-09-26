using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Extensions;
    using Game;
    using Packets;
    using Packets.ClientMethod.Server;
    using Packets.Game.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Packets.Protocol;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;
    using System;

    public class DynamicObjectManager
    {
        private static DynamicObjectManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public readonly Dictionary<ulong, Dropship> Dropships = new Dictionary<ulong, Dropship>();
        public readonly Dictionary<ulong, DynamicObject> Teleporters = new Dictionary<ulong, DynamicObject>();

        /// <summary>
        /// The UseObject arg id each kind of object is used with, which is what picks the recovery
        /// in ActorActionManager. The client reads it off the object's own usabledata row
        /// (client/augmentations/usable.py, defaulting to 1 for a class with no row), and it
        /// matches the object here: all 35 footlocker classes carry 1, all 39 station classes 5,
        /// 163 of the 166 logos classes 6, and the control point 7.
        /// </summary>
        public const uint FootlockerUseArgId = 1;

        /// <inheritdoc cref="FootlockerUseArgId"/>
        public const uint LogosUseArgId = 6;

        /// <inheritdoc cref="FootlockerUseArgId"/>
        public const uint ControlPointUseArgId = 7;

        /// <summary>
        /// How far from an object a player may be and still use it.
        ///
        /// The client's own radius is the larger of the player's use range and the object class's
        /// own: an actor's use range is 3 and a manifestation doubles it
        /// (client/augmentations/manifestation.py GetUseRange), and of every usable class only one
        /// - 26232, which is none of the objects placed here - sets a range of its own. So an
        /// honest client asks from within 6 units, and measures them from itself to the object's
        /// DAMAGE1 connection point rather than to where the object stands, which is what the rest
        /// of this allowance is for: the server knows only where it stands, and the position it
        /// has for the player is a tick behind the one the client checked. Twenty units is
        /// generous about all of that and still refuses what is worth refusing, which is a request
        /// from across the map.
        /// </summary>
        public const float MaxUseDistance = 20f;

        /// <summary>Whether an actor is on the object's map and near enough to use it.</summary>
        private static bool IsInUseRange(Actor actor, DynamicObject obj)
        {
            return obj.MapContextId == actor.MapContextId
                   && Vector3.Distance(actor.Position, obj.Position) <= MaxUseDistance;
        }

        public static DynamicObjectManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new DynamicObjectManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private DynamicObjectManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        internal void InitDynamicObjects()
        {
            InitControlPoints();
            InitFootlockers();
            InitTeleporters();
            LogosManager.Instance.LogosInit();
            KraftwerksManager.Instance.KraftwerksInit();
        }

        internal void ForceState(DynamicObject obj, UseObjectState state, int delta)
        {
            CellManager.Instance.CellCallMethod(obj, new ForceStatePacket(state, delta));
        }

        /// <summary>
        /// Puts an object in or out of service and tells everyone who can see it (SetUsable). Out
        /// of service it offers no Use and cannot be moused over on the client, and a use request
        /// for it is refused here. A client meeting it later has the flag in UsableInfo. Returns
        /// whether it changed; setting what it already is sends nothing.
        /// </summary>
        internal bool SetEnabled(DynamicObject obj, bool enabled)
        {
            if (obj == null || obj.IsEnabled == enabled)
                return false;

            obj.IsEnabled = enabled;

            if (MapChannelManager.Instance.FindByContextId(obj.MapContextId) != null)
                CellManager.Instance.CellCallMethod(obj, new SetUsablePacket(enabled));

            return true;
        }

        internal void RequestUseObjectPacket(Client client, RequestUseObjectPacket packet)
        {
            // Teleporting counts as being in the world for the packet gate - a dropship ride keeps
            // the manifestation and everything registered with it - but the rider is between maps,
            // standing where they left, with no cells. There is nothing there for them to use.
            if (client.State != ClientState.Ingame)
                return;

            // The id names an object, or it names nothing this can answer. GetObject is the
            // throwing indexer, so any item, creature or player id - or an object id that is no
            // longer registered - closed the connection of whoever sent it.
            if (!EntityManager.Instance.TryGetObject(packet.EntityId, out var obj))
            {
                Logger.WriteLog(LogType.Debug, $"{client.Player.FamilyName} asked to use {packet.EntityId}, which is not an object.");
                return;
            }

            // Where the player is standing decides what they can reach. Object ids are handed out
            // in order and are the same for every client, so a client could walk the id space and
            // use every footlocker, station and control point on the map without leaving the spot
            // it was standing on - and collect every logos tablet on it, since the recovery asks
            // only that the player be in the object's TriggeredByPlayers list. An object on
            // another map left them in that list for good, which holds their connection open in
            // the server's memory long after they have gone.
            if (obj.MapContextId != client.Player.MapContextId)
            {
                Logger.WriteLog(LogType.Security,
                    $"{client.Player.FamilyName} asked to use object {packet.EntityId}, which is on map {obj.MapContextId} and not on {client.Player.MapContextId}. Ignored.");
                return;
            }

            var distance = Vector3.Distance(client.Player.Position, obj.Position);

            if (distance > MaxUseDistance)
            {
                Logger.WriteLog(LogType.Security,
                    $"{client.Player.FamilyName} asked to use object {packet.EntityId} from {distance:F0} units away. Ignored.");
                return;
            }

            // Using an object is the only thing a use request may ask for. The queued action
            // carried the packet's own action id to ActorActionManager.PerformRecovery, which
            // performs whatever that id names, on the player, as soon as the windup has run - so
            // any footlocker or crafting station was a hundred milliseconds of free choice over
            // the whole action table. WeaponReload filled the clip and cleared a jam with no
            // reload asked for and none of its time served, and anything AbilityManager can
            // resolve landed as an ability: its recovery is the half that takes the skill, the
            // cost, the cooldown and the range as read, because the request half checked them.
            //
            // The arg id is left as it arrived. It only picks which of the four use-object
            // recoveries runs, and each of those acts on an object that holds this player in its
            // TriggeredByPlayers - the list this request adds them to - so one that does not
            // match the object it was sent to finds nothing to do. It also has to go back
            // unchanged: the client files its pending action under (actionId, actionArgId) and
            // does not recognise its own windup or recovery under any other arg.
            if (packet.ActionId != ActionId.UseObject)
            {
                Logger.WriteLog(LogType.Security,
                    $"{client.Player.FamilyName} sent {packet.ActionId}/{packet.ActionArgId} to use object {packet.EntityId}; an object is used with {ActionId.UseObject}. Ignored.");
                return;
            }

            // Out of service (SetEnabled): the client offers no Use for it, so only a client that
            // did not know yet - or did not care - asks. Refused as the client itself would put it,
            // and the request closed and cancelled.
            if (!obj.IsEnabled)
            {
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmUseObjectNotUsable);
                return;
            }

            // One use at a time. Every request queued another windup and recovery, and each
            // recovery goes to everyone nearby, with no limit on how many one player could have
            // waiting. The client's own action queue holds one use; a second is not it.
            if (client.Player.MapChannel.PerformRecovery.Any(a => a.Actor == client.Player && a.ActionId == ActionId.UseObject))
                return;

            // Whoever is using it, once each (added below only if absent). The list was appended to on every request and
            // nothing removes a footlocker's users, so it grew for as long as the server ran;
            // connections that have closed are dropped from it here as well.
            obj.TriggeredByPlayers.RemoveAll(c => c.State == ClientState.Disconnected);

            switch (obj.DynamicObjectType)
            {
                case DynamicObjectType.ControlPoint:
                    {
                        if (!TryLockForUse(client, obj, packet))
                            break;

                        // The object's id rides on the action so its recovery can find the lock.
                        var actionData = new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 10000);
                        actionData.SourceId = obj.EntityId;

                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 10000));
                        client.Player.MapChannel.PerformRecovery.Add(actionData);

                        if (!obj.TriggeredByPlayers.Contains(client))
                            obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Lockbox:
                    {
                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 100));
                        client.Player.MapChannel.PerformRecovery.Add(new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 100));

                        if (!obj.TriggeredByPlayers.Contains(client))
                            obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Logos:
                    {
                        if (!TryLockForUse(client, obj, packet))
                            break;

                        var actionData = new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 10000);
                        actionData.SourceId = obj.EntityId;

                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 10000));
                        client.Player.MapChannel.PerformRecovery.Add(actionData);

                        if (!obj.TriggeredByPlayers.Contains(client))
                            obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Kraftwerks:
                    KraftwerksManager.Instance.Use(client, obj, packet.ActionArgId);
                    break;
                case DynamicObjectType.Hortimonculus:
                    AbilityManager.Instance.RequestUseHortimonculus(client, obj, packet);
                    break;
                case DynamicObjectType.DropshipPad:
                    // The hovering ship is a two-state switch to the client, so it offers a use;
                    // there is nothing to do with one - the pad works by walking into the beam.
                    break;
                default:
                    Logger.WriteLog(LogType.Debug, $"ToDo: RequestUseObjectPacket: unsuported object type {obj.DynamicObjectType}");
                    break;
            }
        }

        internal void DynamicObjectWorker(MapChannel mapChannel, long delta)
        {
            // dynamicObjects
            // dropShips
            // etc...

            // controlPoints
            foreach (var entry in mapChannel.ControlPoints)
            {
                var controlPoint = entry.Value;
                // spawn object
                if (!controlPoint.IsInWorld)
                {
                    controlPoint.RespawnTime -= delta;

                    if (controlPoint.RespawnTime <= 0)
                    {
                        CellManager.Instance.AddToWorld(mapChannel, controlPoint);
                        controlPoint.IsInWorld = true;
                        controlPoint.StateId = UseObjectState.CpointStateUnclaimed;
                        controlPoint.WindupTime = 10000;
                    }
                }

                // check for players neer object
                DynamicObjectProximityWorker(mapChannel, controlPoint, delta);
            }

            // footlocker
            foreach (var entry in mapChannel.FootLockers)
            {
                var footlocker = entry.Value;
                // spawn object
                if (!footlocker.IsInWorld)
                {
                    footlocker.RespawnTime -= delta;

                    if (footlocker.RespawnTime <= 0)
                    {
                        CellManager.Instance.AddToWorld(mapChannel, footlocker);
                        footlocker.IsInWorld = true;
                        footlocker.StateId = UseObjectState.CpointStateUnclaimed;
                        footlocker.WindupTime = 10000;
                    }
                }
            }

            // crafting stations
            KraftwerksManager.Instance.Worker(mapChannel);

            // teleporters
            foreach (var entry in mapChannel.Teleporters)
            {
                var teleporter = entry.Value;
                // spawn object
                if (!teleporter.IsInWorld)
                {
                    teleporter.RespawnTime -= delta;

                    if (teleporter.RespawnTime <= 0)
                    {
                        CellManager.Instance.AddToWorld(mapChannel, teleporter);
                        teleporter.IsInWorld = true;
                        teleporter.StateId = UseObjectState.TsState1;
                    }
                }

                // check for players neer object
                DynamicObjectProximityWorker(mapChannel, teleporter, delta);
            }

            // dynamicObjects: logos shrines and dropship pads
            foreach (var dynamicObject in mapChannel.DynamicObjects)
            {
                // spawn object
                if (!dynamicObject.IsInWorld)
                {
                    dynamicObject.RespawnTime -= delta;

                    if (dynamicObject.RespawnTime <= 0)
                    {
                        // A shrine is built with no state and gets the usable default here; a
                        // pad's dropship is built hovering (TsState1) and keeps it.
                        if (dynamicObject.StateId == 0)
                        {
                            dynamicObject.StateId = UseObjectState.IdStateActive;
                            dynamicObject.WindupTime = 10000;
                        }

                        CellManager.Instance.AddToWorld(mapChannel, dynamicObject);
                        dynamicObject.IsInWorld = true;
                    }
                }
            }
        }

        internal void DynamicObjectProximityWorker(MapChannel mapChannel, DynamicObject obj, long delta)
        {
            switch (obj.DynamicObjectType)
            {
                // teleporters
                case DynamicObjectType.Waypoint:
                case DynamicObjectType.Wormhole:
                case DynamicObjectType.DropshipTeleporter:
                    {
                        // check for players that enter range
                        PlayerEnterWaypoint(obj);

                        // check for players that leave range
                        PlayerExitWaypoint(obj);

                        break;
                    }
                // Control point
                case DynamicObjectType.ControlPoint:
                default:
                    break;
            }
        }

        // 1 object to n client's
        internal void CellIntroduceDynamicObjectToClients(DynamicObject dynamicObject, List<Client> listOfClients)
        {
            foreach (var client in listOfClients)
                CreateDynamicObjectOnClient(client, dynamicObject);
        }

        // n objects to 1 client
        internal void CellIntroduceDynamicObjectsToClient(Client client, List<DynamicObject> listOfObjects)
        {
            foreach (var dynamicObject in listOfObjects)
                CreateDynamicObjectOnClient(client, dynamicObject);
        }

        internal void CreateDynamicObjectOnClient(Client client, DynamicObject dynamicObject)
        {
            if (dynamicObject == null)
                return;

            if (dynamicObject.EntityClassId == 0)
                return;
				
            var classInfo = EntityClassManager.Instance.GetClassInfo(EntityManager.Instance.GetEntityClassId(dynamicObject.EntityId));

            if (classInfo == null)
                return;

            // An FX emitter is no usable - it has no Recv_UsableInfo to take the state below.
            if (dynamicObject.DynamicObjectType == DynamicObjectType.Emitter)
            {
                client.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(dynamicObject.EntityId, dynamicObject.EntityClassId, EmitterManager.EntityData(dynamicObject, classInfo)));
                return;
            }

            var entityData = new List<PythonPacket>
            {
                // PhysicalEntity
                new IsTargetablePacket(classInfo.TargetFlag),
                new WorldLocationDescriptorPacket(dynamicObject.Position, dynamicObject.Rotation),
                // set state
                new UsableInfoPacket(dynamicObject.IsEnabled, dynamicObject.StateId, 0, dynamicObject.WindupTime, dynamicObject.ActivateMission)
        };

            // Only for an object that actually has a lock. An unlocked usable is the default the
            // client already assumes, and sending a lock of zeroes would tell it the same thing
            // at the cost of a packet per object per client.
            if (dynamicObject.Lock != null)
                entityData.Add(new LockInfoPacket(dynamicObject.Lock));

            client.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(dynamicObject.EntityId, dynamicObject.EntityClassId, entityData));

            // A Hortimonculus plant: its owner and its hit points.
            if (dynamicObject.DynamicObjectType == DynamicObjectType.Hortimonculus)
                AbilityManager.ShowPlantTo(client, dynamicObject);

            // A force field: its hit points, and whether it blocks this client's avatar.
            if (dynamicObject.DynamicObjectType == DynamicObjectType.ForceField)
                ForceFields.ShowTo(client, dynamicObject);

            // Someone is partway through using it. Players are introduced before objects, and the
            // user is standing at it, so this client already has the actor the effect runs to.
            if (dynamicObject.UsedBy != null)
            {
                client.CallMethod(dynamicObject.EntityId, new LockToActorPacket(dynamicObject.UsedBy.EntityId));
                client.CallMethod(dynamicObject.EntityId, new UseInterruptiblePacket(dynamicObject.UsedBy.EntityId));
            }
        }

        internal void CellDiscardDynamicObjectToClients(ulong entityId, List<Client> clients)
        {
            if (entityId == 0)
                return;

            foreach (var client in clients)
                EntityManager.Instance.DestroyPhysicalEntity(client, entityId, EntityType.Object);
        }

        internal void CellDiscardDynamicObjectsToClient(Client client, List<DynamicObject> discardObjects)
        {
            foreach (var dynamicObject in discardObjects)
                client.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(dynamicObject.EntityId));
        }

        /* Destroys an object on client and serverside
         * Frees the memory and informs clients about removal
         */
        internal void DynamicObjectDestroy(MapChannel mapChannel, DynamicObject dynObject)
        {
            // TODO, check timers
            // remove from world
            EntityManager.Instance.UnregisterEntity(dynObject.EntityId);
            CellManager.Instance.RemoveFromWorld(mapChannel, dynObject);

            // destroy callback
            Logger.WriteLog(LogType.Debug, "ToDO remove dynamic object from server");
        }

        #region Use lock

        /// <summary>
        /// Starts a timed use of an object, or refuses it because someone else is partway through
        /// one. Control points and logos objects only: a capture or a tablet is one player's at a
        /// time, and both classes have an in-use effect for the client to play.
        ///
        /// Everyone in range, the user included, gets LockToActor(user) and then
        /// UseInterruptible(user). The lock turns the HUD's use prompt into the in-use text for
        /// everyone else and is what the client checks before it plays the effect; the effect runs
        /// from the object to the user until the use ends (<see cref="ReleaseUseLock"/>).
        ///
        /// A holder that is no longer waiting on a use of this object - one whose action was
        /// dropped without a recovery - does not keep it: the lock passes to the new user.
        ///
        /// A refusal is ActorManager.RefuseRequest - UserActionFailed and ActionFailed - which ends
        /// the client's windup and clears its pending action. The client has no in-use message, so
        /// it shows its generic "cannot do that now".
        /// </summary>
        private bool TryLockForUse(Client client, DynamicObject obj, RequestUseObjectPacket packet)
        {
            var user = client.Player;
            var holder = obj.UsedBy;

            if (holder != null && IsUsing(user.MapChannel, holder, obj))
            {
                Logger.WriteLog(LogType.Debug, $"{user.FamilyName} asked to use object {obj.EntityId}, which {(holder == user ? "they are already using" : $"entity {holder.EntityId} is using")}. Refused.");
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmCannotPerformActionNow);
                return false;
            }

            obj.UsedBy = user;

            CellManager.Instance.CellCallMethod(obj, new LockToActorPacket(user.EntityId));
            CellManager.Instance.CellCallMethod(obj, new UseInterruptiblePacket(user.EntityId));

            return true;
        }

        /// <summary>Whether <paramref name="actor"/> still has a use of <paramref name="obj"/> waiting to finish.</summary>
        private static bool IsUsing(MapChannel mapChannel, Actor actor, DynamicObject obj)
        {
            return mapChannel != null
                   && mapChannel.PerformRecovery.Any(a => a.Actor == actor && a.ActionId == ActionId.UseObject && a.SourceId == obj.EntityId);
        }

        /// <summary>
        /// Ends the lock a use-object action holds, if it holds one: when the use finishes, when it
        /// is interrupted, or when its actor leaves the map with it still pending. An interrupted
        /// use - or one whose actor died, or left - gets UseInterrupted first, which takes the
        /// in-use effect off; LockToActor(0) then clears the lock, which also takes the effect off
        /// and puts the object's state effect back. Either way, nothing is left showing the object
        /// as in use.
        ///
        /// The object is the action's SourceId, and it is only unlocked if this action's actor is
        /// the one holding it, so a use of anything else - a footlocker, a station, a
        /// Hortimonculus plant - passes through untouched.
        /// </summary>
        internal void ReleaseUseLock(ActionData action, bool interrupted)
        {
            if (action.ActionId != ActionId.UseObject || action.SourceId == 0)
                return;

            if (!EntityManager.Instance.TryGetObject(action.SourceId, out var obj) || obj.UsedBy != action.Actor)
                return;

            obj.UsedBy = null;

            if (interrupted || action.IsInrerrupted || action.Actor.State == CharacterState.Dead)
                CellManager.Instance.CellCallMethod(obj, new UseInterruptedPacket(action.Actor.EntityId));

            CellManager.Instance.CellCallMethod(obj, new LockToActorPacket(0));
        }

        #endregion

        #region ControlPoint

        internal void InitControlPoints()
        {
            //var contolPoints = ControlPointTable.GetControlPoints();
            var mapChannel = MapChannelManager.Instance.FindByContextId(1220);

            var newControlPoint = new DynamicObject
            {
                Position = new Vector3(197.66f, 162.27f, -54.08f),
                Rotation = 3.05f,
                MapContextId = 1220,
                EntityClassId = (EntityClasses)3814,
                DynamicObjectType = DynamicObjectType.ControlPoint,
                ObjectData = new ControlPointStatus(215, 1, 1, 30000)
            };

            newControlPoint.DynamicObjectType = DynamicObjectType.ControlPoint;

            mapChannel.ControlPoints.Add(1, newControlPoint);
        }

        internal void CaptureControlPointRecovery(MapChannel mapChannel, ActionData action)
        {
            foreach (var entry in mapChannel.ControlPoints)
            {
                var controlpoint = entry.Value;

                foreach (var client in controlpoint.TriggeredByPlayers)
                    if (client.Player == action.Actor)
                    {
                        if (action.IsInrerrupted)
                        {
                            Logger.WriteLog(LogType.Debug, $"Action is interupted");
                            controlpoint.TriggeredByPlayers.Remove(client);
                            break;
                        }

                        // As with a logos: a capture belongs to whoever is still standing at the
                        // point when its ten seconds are up.
                        if (!IsInUseRange(action.Actor, controlpoint))
                        {
                            Logger.WriteLog(LogType.Security,
                                $"{client.Player.FamilyName} was no longer at control point {controlpoint.EntityId} when the use finished; not captured.");
                            controlpoint.TriggeredByPlayers.Remove(client);
                            break;
                        }

                        Logger.WriteLog(LogType.Debug, $"Action Exicuted");
                        controlpoint.TriggeredByPlayers.Remove(client);
                        controlpoint.TargetCategory = controlpoint.TargetCategory == TargetCategory.Friendly ? TargetCategory.Hostile : TargetCategory.Friendly;
                        controlpoint.StateId = controlpoint.StateId == UseObjectState.CpointStateFactionAOwned ? UseObjectState.CpointStateFactionBOwned : UseObjectState.CpointStateFactionAOwned;

                        CellManager.Instance.CellCallMethod(controlpoint, new ForceStatePacket(controlpoint.StateId, 100));
                        CellManager.Instance.CellCallMethod(controlpoint, new UsableInfoPacket(controlpoint.IsEnabled, controlpoint.StateId, 0, 10000, 0));
                        break;
                    }
            }
        }

        #endregion

        #region Dropship

        /// <summary>
        /// Ticks every dropship in the world through its phases. A spawner dropship lands, drops
        /// its creatures and leaves. A teleporter dropship is one end of a player's flight and
        /// its role says which (<see cref="DropshipRole"/>): the departure lands beside the
        /// player (Begin), hovers (Spawn) while they board - a PreTeleport hides them - then
        /// lifts off (End) and, as it goes, either hands the client the destination map
        /// (Wonkavate) or, for a pad on the same map, moves them there; either way an arrival
        /// dropship is waiting at the far end, and it is the arrival's End that gives the
        /// player their movement back and their Ingame state. Phase times are the client's
        /// animation lengths, near enough.
        /// </summary>
        public void DropshipsWorker(long timePassed)
        {
            // Dropships is server-wide; each one is removed from its own map, not from
            // whichever map the caller happened to be iterating.
            foreach (var entry in Dropships.ToList())
            {
                var dropship = entry.Value;

                if (dropship.DropshipType != DropshipType.Spawner && dropship.DropshipType != DropshipType.Teleporter)
                {
                    Logger.WriteLog(LogType.Debug, $"error dropshiptype {dropship.DropshipType}");
                    Dropships.Remove(entry.Key);
                    continue;
                }

                // A teleporter dropship whose passenger has gone (a dropped connection) has
                // nothing left to do; take it out of the world rather than fly it into a null.
                if (dropship.DropshipType == DropshipType.Teleporter && (dropship.Client?.Player == null || dropship.Client.Player.Disconected))
                {
                    if (MapChannelManager.Instance.MapChannelArray.TryGetValue(dropship.MapContextId, out var lostMap))
                        CellManager.Instance.RemoveFromWorld(lostMap, dropship);

                    Dropships.Remove(entry.Key);
                    continue;
                }

                dropship.PhaseTimeleft -= timePassed;

                if (dropship.PhaseTimeleft > 0)
                    continue;

                if (dropship.Phase == 0 || dropship.Phase == 1 || dropship.Phase == 4)
                    CellManager.Instance.CellCallMethod(dropship, new ForceStatePacket(dropship.StateId, 0));

                switch (dropship.Phase)
                {
                    case 0:
                        dropship.Phase = 1;
                        dropship.StateId = UseObjectState.CsStateSpawn;
                        break;
                    case 1:
                        dropship.Phase = 2;
                        dropship.PhaseTimeleft = 2000;
                        break;
                    case 2:
                        dropship.Phase = 3;

                        if (dropship.DropshipType == DropshipType.Teleporter && dropship.Role == DropshipRole.Departure)
                        {
                            // Aboard: everyone sees the player fade, the player sees the teleport begin.
                            CellManager.Instance.CellCallMethod(dropship.Client.Player.MapChannel, dropship.Client.Player, new PreTeleportPacket(TeleportType.Default));
                            dropship.Client.CallMethod(SysEntity.ClientMethodId, new BeginTeleportPacket());
                        }

                        if (dropship.DropshipType == DropshipType.Spawner)
                        {
                            // create list of creatures to spawn
                            var creatureList = SpawnPoolManager.Instance.CreateListOfCreatures(dropship.SpawnPool);

                            // spawn creatures
                            SpawnPoolManager.Instance.SpawnCreatures(dropship.SpawnPool, creatureList, dropship.Arrival);
                            SpawnPoolManager.Instance.DecreaseQueuedCreatureCount(dropship.SpawnPool, dropship.SpawnPool.QueuedCreatures);
                        }

                        break;
                    case 3:
                        dropship.PhaseTimeleft = 3000;
                        dropship.Phase = 4;
                        dropship.StateId = UseObjectState.CsStateEnd;
                        break;
                    case 4:
                        dropship.Phase = 5;
                        dropship.PhaseTimeleft = 5000;

                        if (dropship.DropshipType == DropshipType.Teleporter && dropship.Role == DropshipRole.Arrival)
                            dropship.Client.CallMethod(SysEntity.ClientMethodId, new UnrequestMovementBlockPacket());
                        break;
                    case 5:
                        if (dropship.DropshipType == DropshipType.Teleporter)
                        {
                            if (dropship.Role == DropshipRole.Departure)
                                Depart(dropship);
                            else
                            {
                                dropship.Client.State = ClientState.Ingame;
                                ManifestationManager.Instance.ResetInactivity(dropship.Client);
                            }
                        }

                        if (dropship.DropshipType == DropshipType.Spawner)
                            SpawnPoolManager.Instance.DecreaseQueueCount(dropship.SpawnPool);

                        // remove object
                        if (MapChannelManager.Instance.MapChannelArray.TryGetValue(dropship.MapContextId, out var dropshipMap))
                            CellManager.Instance.RemoveFromWorld(dropshipMap, dropship);

                        Dropships.Remove(dropship.EntityId);
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"Unsupported phase {dropship.Phase}");
                        break;
                }
            }
        }

        /// <summary>
        /// The departure has lifted off with the player aboard. To another map: the player leaves
        /// this one and the client is handed the destination; MapChannelManager builds the
        /// arrival when the client reports the map loaded. To a pad on this map: the player is
        /// moved to it under the movement block and the arrival is built there now.
        /// </summary>
        private void Depart(Dropship dropship)
        {
            var client = dropship.Client;
            var player = client.Player;

            if (dropship.StaysOnMap)
            {
                var mapChannel = player.MapChannel;
                var rotation = (float)dropship.DestinationRotation;

                player.Target = 0;

                // The server goes where it is sending them, and the movement check starts over
                // from there (PlaceAt) - a bare Position would read the client's next Move as one
                // enormous step.
                player.PlaceAt(dropship.Destination);
                player.Rotation = rotation;

                // Teleport moves the client's own manifestation; MoveObject tells everyone else.
                client.CallMethod(player.EntityId, new TeleportPacket(dropship.Destination, rotation, TeleportType.Default, 5));
                client.CellMoveObject(client, new MoveObjectMessage(player.EntityId, new Models.Movement(dropship.Destination, 0f, 0, new Vector2(rotation, 0f))), false);

                var arrival = new Dropship(TargetCategory.Friendly, DropshipType.Teleporter, client, DropshipRole.Arrival);

                CellManager.Instance.AddToWorld(mapChannel, arrival);
                Dropships.Add(arrival.EntityId, arrival);
                return;
            }

            CellManager.Instance.RemoveFromWorld(client);
            player.MapChannel.ClientList.Remove(client);

            // Effects end with the map, as MapChannelManager.RemovePlayer ends them - a
            // dropship ride never goes through it. A sprint used to ride along: the arrival
            // introduced the player to everyone without it, their own client included, while
            // the arrival's ActorInfo gave that client the sprint's speed and its drain picked
            // up again, under an effect id handed out by the map they had left and with no
            // buff on any screen to show for it. The timed buffs are kept aside, clocks stopped,
            // and go on again when the ride lands them (EffectCarry).
            EffectCarry.Stash(player);
            GameEffectManager.Instance.ClearEffects(player.MapChannel, player);

            CommunicatorManager.Instance.LeaveMapChannels(client);
            client.CallMethod(SysEntity.ClientMethodId, new UnrequestMovementBlockPacket());
            client.CallMethod(SysEntity.ClientMethodId, new PreWonkavatePacket());
            client.CallMethod(SysEntity.CurrentInputStateId, new WonkavatePacket(dropship.DestinationMapId, 1, MapChannelManager.Instance.MapChannelArray[dropship.DestinationMapId].MapInfo.MapVersion, dropship.Destination, 0));
            player.PlaceAt(dropship.Destination);
            player.Rotation = (float)dropship.DestinationRotation;
            player.Target = 0;
            client.State = ClientState.Teleporting;
            client.AwaitingMapLoaded = true;
        }

        #endregion

        #region Footlocker

        internal void FootlockerRecovery(MapChannel mapChannel, ActionData action)
        {
            Logger.WriteLog(LogType.Debug, $"ToDo: FootlockerRecovery, ActionId = {action.ActionId} ActionArgId = {action.ActionArgId}");
        }

        internal void InitFootlockers()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var footlockers = unitOfWork.Footlockers.GetFootlockers();

            foreach (var footlocker in footlockers)
            {
                var mapChannel = MapChannelManager.Instance.FindByContextId(footlocker.MapContextId);

                var newFootlocker = new DynamicObject
                {
                    Position = footlocker.Position,
                    Rotation = footlocker.Rotation,
                    MapContextId = footlocker.MapContextId,
                    EntityClassId = (EntityClasses)footlocker.ClassId,
                    DynamicObjectType = DynamicObjectType.Lockbox,
                    Comment = footlocker.Comment
                };

                mapChannel.FootLockers.Add(footlocker.Id, newFootlocker);
            }
        }

        #endregion

        #region Logos
        internal void LogosRecovery(MapChannel mapChannel, ActionData action)
        {
            foreach (var obj in mapChannel.DynamicObjects)
            {
                foreach (var client in obj.TriggeredByPlayers)
                    if (client.Player == action.Actor)
                    {
                        if (action.IsInrerrupted)
                        {
                            Logger.WriteLog(LogType.Debug, $"Action is interupted");
                            obj.TriggeredByPlayers.Remove(client);
                            //CellManager.Instance.CellCallMethod(mapChannel, action.Actor, new PerformWindupPacket(PerformType.TwoArgs, action.ActionId, action.ActionArgId));
                            break;
                        }

                        // Still at it when the ten seconds are up, not only when they started.
                        // The client interrupts a use the moment the player moves (useobject.py
                        // sets moveInterrupts), so the only client this refuses is one that did
                        // not - and a tablet is a permanent thing to be given for a use that was
                        // walked away from.
                        if (!IsInUseRange(action.Actor, obj))
                        {
                            Logger.WriteLog(LogType.Security,
                                $"{client.Player.FamilyName} was no longer at logos object {obj.EntityId} when the use finished; nothing given.");
                            obj.TriggeredByPlayers.Remove(client);
                            break;
                        }

                        Logger.WriteLog(LogType.Debug, $"Action Exicuted");
                        obj.TriggeredByPlayers.Remove(client);
                        CellManager.Instance.CellCallMethod(obj, new UsableInfoPacket(obj.IsEnabled, obj.StateId, 0, 10000, 0));

                        var logosId = 0u;
                        foreach (var entry in mapChannel.DynamicObjects)
                        {
                            // The list holds the dropship pads' hovering ships as well now.
                            if (entry is Logos logos && action.SourceId == logos.EntityId)
                            {
                                logosId = logos.Id;
                                break;
                            }
                        }

                        var haveLogos = false;
                        foreach (var logos in client.Player.Logos)
                        {
                            if (logos == logosId)
                            {
                                haveLogos = true;
                                break;
                            }
                        }

                        if (!haveLogos)
                            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Logos, logosId);

                        break;
                    }
            }
        }
        #endregion

        #region Waypoint

        internal void InitTeleporters()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var teleporters = unitOfWork.Teleporters.GetTeleporters();

            foreach (var teleporter in teleporters)
            {
                if (teleporter.MapContextId == 0)
                    continue;

                var mapChannel = MapChannelManager.Instance.FindByContextId(teleporter.MapContextId);

                var newTeleporter = new DynamicObject
                {
                    Position = teleporter.Position,
                    Rotation = teleporter.Rotation,
                    MapContextId = teleporter.MapContextId,
                    EntityClassId = (EntityClasses)teleporter.ClassId,
                    Comment = teleporter.Description,
                    ObjectData = new WaypointInfo(teleporter.Id, false, (WaypointType)teleporter.Type)
                };

                switch (teleporter.Type)
                {
                    case 1:
                        newTeleporter.DynamicObjectType = DynamicObjectType.LocalTeleporter;
                        break;
                    case 2:
                        newTeleporter.DynamicObjectType = DynamicObjectType.Waypoint;
                        break;
                    case 3:
                        newTeleporter.DynamicObjectType = DynamicObjectType.Wormhole;
                        break;
                    case 4:
                        // A dropship pad is two things. The trigger is the 5 m circle on the pad
                        // that opens the travel window and gains the pad for whoever walks into
                        // it. The hovering dropship with its transporter beam is what the client
                        // shows there - UsableTwoStateHumDropshipBeam in TsState1 is the ship
                        // hovering with the beam on (TsState0 is an empty pad), which is how the
                        // help text describes gaining one: "walking across the pad when a Dropship
                        // is hovering with its transporter beam activated". The client's map has
                        // the landing pad geometry itself; the ship was always the server's.
                        CellManager.Instance.AddToWorld(mapChannel, new MapTrigger(teleporter.Id, teleporter.Description, teleporter.Position, teleporter.Rotation, teleporter.MapContextId));

                        mapChannel.DynamicObjects.Add(new DynamicObject
                        {
                            EntityId = EntityManager.Instance.GetEntityId,
                            EntityClassId = EntityClasses.UsableTwoStateHumDropshipBeam,
                            DynamicObjectType = DynamicObjectType.DropshipPad,
                            Position = teleporter.Position,
                            Rotation = teleporter.Rotation,
                            MapContextId = teleporter.MapContextId,
                            TargetCategory = TargetCategory.Friendly,
                            StateId = UseObjectState.TsState1,
                            Comment = teleporter.Description
                        });
                        break;
                    case 5:
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"InitTeleporters: unsuported teleporter type {teleporter.Type}");

                        MapErrorManager.Instance.Record(teleporter.MapContextId,
                            $"Teleporter {teleporter.Id} ({teleporter.Description}) is type {teleporter.Type}, which nothing handles.");

                        break;
                }

                mapChannel.Teleporters.Add(teleporter.Id, newTeleporter);
                Teleporters.Add(teleporter.Id, newTeleporter);
            }
        }

        internal void CheckPlayerWaypoint(Client client, WaypointInfo objectData)
        {
            // check if player has requested waypoint
            foreach (var waypoint in client.Player.GainedWaypoints)
                if (waypoint.WaypointId == objectData.WaypointId)
                 return;

            var newWaypoint = new CharacterTeleporterEntry(client.Player.Id, objectData.WaypointId, (byte)objectData.WaypointType);
            // add waypoint to player as he entered for the first time
            client.CallMethod(client.Player.EntityId, new WaypointGainedPacket(objectData.WaypointId, objectData.WaypointType));
            client.Player.GainedWaypoints.Add(newWaypoint);

            // And on the map, where this is the one thing about a marker the client cannot work
            // out for itself. The marker changes colour under the player as they stand on it.
            MapMarkerManager.Instance.WaypointDiscovered(client, objectData.WaypointId);

            // update Db
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Teleporter, newWaypoint);
        }

        internal Dictionary<uint, MapWaypointInfoList> CreateListOfWaypoints(Client client, WaypointType waypointType)
        {
            var listOfWaypoints = new Dictionary<uint, MapWaypointInfoList>();
            var listOfMapInstances = new List<MapInstanceInfo>();
            var waypointInfo = new List<WaypointInfo>();
            var mapChannel = client.Player.MapChannel;

            // create waypoint list for player
            foreach (var waypoint in client.Player.GainedWaypoints)
            {
                if ((WaypointType)waypoint.WaypointType != waypointType)
                    continue;

                // A gained id the table no longer has - a pad removed from the data, say - is
                // not a reason to throw the window away.
                if (!Teleporters.TryGetValue(waypoint.WaypointId, out var teleporter) || !(teleporter.ObjectData is WaypointInfo teleporterData))
                    continue;

                if (teleporterData.WaypointType == WaypointType.Waypoint && teleporter.MapContextId != mapChannel.MapInfo.MapContextId)
                    continue;

                if (teleporterData.WaypointType != waypointType)
                    continue;

                if (waypoint.WaypointId == teleporterData.WaypointId)
                {
                    waypointInfo.Add(new WaypointInfo(teleporterData.WaypointId, teleporterData.Contested, teleporterData.WaypointType)
                    {
                        Position = teleporter.Position
                    });

                    listOfMapInstances.Add(new MapInstanceInfo(1, mapChannel.MapInfo.MapContextId, MapInstanceStatus.Low)); // ToDo: send mapInstanceStatus based on map population
                }
            }

            listOfWaypoints.Add(mapChannel.MapInfo.MapContextId, new MapWaypointInfoList(mapChannel.MapInfo.MapContextId, listOfMapInstances, waypointInfo));

            return listOfWaypoints;
        }

        internal void SelectWaypoint(Client client, SelectWaypointPacket packet)
        {
            // Both ids come from the client and used to be indexed straight into the map and
            // teleporter dictionaries, so an unknown map or waypoint id threw KeyNotFoundException
            // in the handler and the player was disconnected. Now the request is checked the way
            // the waypoint window itself is built: the player has to be standing at a waypoint,
            // the destination has to exist, and it has to be one this character has gained - a
            // dropship pad included, see CreateListOfDropships.
            if (client.Player == null || client.State != ClientState.Ingame)
                return;

            // The client sends None for the map when it means the one it is on.
            var mapContextId = packet.MapInstanceId != 0 ? packet.MapInstanceId : client.Player.MapContextId;

            if (!MapChannelManager.Instance.MapChannelArray.TryGetValue(mapContextId, out var targetMap)
                || !targetMap.Teleporters.TryGetValue(packet.WaypointId, out var teleporter)
                || !(teleporter.ObjectData is WaypointInfo objData))
            {
                Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} asked for unknown waypoint {packet.WaypointId} on map {mapContextId}");
                return;
            }

            if (!IsAtWaypoint(client))
            {
                Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} is not standing at a waypoint");
                return;
            }

            // A dropship pad has to have been gained like any other waypoint, the one under the
            // player's feet excepted - it is being gained as they stand there.
            var standingOn = PadUnder(client);

            if (!client.Player.GainedWaypoints.Any(w => w.WaypointId == objData.WaypointId)
                && (objData.WaypointType != WaypointType.Dropship || standingOn == null || standingOn.TriggerId != objData.WaypointId))
            {
                Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} has not gained waypoint {objData.WaypointId}");
                return;
            }

            if (objData.WaypointType == WaypointType.Dropship)
            {
                // A flight starts from a pad, not from a waypoint's window.
                if (standingOn == null)
                {
                    Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} asked for dropship pad {objData.WaypointId} without standing on a pad");
                    return;
                }

                // One flight at a time: a second request while the departure is landing would
                // build a second dropship for the same passenger.
                if (Dropships.Values.Any(d => d.DropshipType == DropshipType.Teleporter && d.Client == client))
                    return;

                // The travel window only lists this planet's pads; a request for another
                // planet's did not come from the window.
                if (targetMap.MapInfo.Planet != client.Player.MapChannel.MapInfo.Planet)
                {
                    Logger.WriteLog(LogType.Security,
                        $"SelectWaypoint: {client.Player.Name} asked for dropship pad {objData.WaypointId} on {targetMap.MapInfo.MapName}, another planet. Ignored.");
                    return;
                }

                if (standingOn.TriggerId == objData.WaypointId)
                    return; // the pad they are standing on: nowhere to go

                // A flight, whether or not it crosses a map: the departure lands, takes the
                // player aboard and leaves; where it leaves for is the dropship's business
                // (DropshipsWorker). Movement stays blocked until the arrival sets them down.
                var dropship = new Dropship(TargetCategory.Friendly, DropshipType.Teleporter, client, DropshipRole.Departure, teleporter.Position, mapContextId)
                {
                    DestinationRotation = teleporter.Rotation
                };

                CellManager.Instance.AddToWorld(client.Player.MapChannel, dropship);
                Dropships.Add(dropship.EntityId, dropship);
                client.CallMethod(SysEntity.ClientMethodId, new RequestMovementBlockPacket());

                if (mapContextId != client.Player.MapContextId)
                    client.LoadingMap = mapContextId;

                return;
            }

            if (mapContextId != client.Player.MapContextId)
            {
                Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} asked for waypoint {objData.WaypointId} on another map; only dropship pads cross maps");
                return;
            }

            var movementData = new Models.Movement
                (
                new Vector3(
                    teleporter.Position.X,
                    teleporter.Position.Y + 1,
                    teleporter.Position.Z),
                0f,
                0,
                new Vector2((float)teleporter.Rotation, 0f)
            );

            // The server goes where it is sending them. This is a teleport within one map, so
            // there is no map change to carry the position across, and it never wrote one: the
            // player was moved on every screen while the server went on holding the pad they left
            // from, which is what every range check on them was measured from until their next
            // Move happened to correct it.
            client.Player.PlaceAt(movementData.Position);
            client.Player.Rotation = (float)teleporter.Rotation;

            client.CellCallMethod(client, client.Player.EntityId, new PreTeleportPacket(TeleportType.Default));
            client.CallMethod(client.Player.EntityId, new TeleportPacket(teleporter.Position, teleporter.Rotation, TeleportType.Default, 5));
            client.CallMethod(SysEntity.ClientMethodId, new BeginTeleportPacket());
            client.CellMoveObject(client, new MoveObjectMessage(client.Player.EntityId, movementData), false);

            teleporter.TriggeredByPlayers.Remove(client);    // ToDO: maybe safely remove client
        }

        /// <summary>
        /// Takes a player leaving the map out of every object's and trigger's list of who is at
        /// it: waypoints and teleporters, dropship pads, control points, logos, crafting stations,
        /// footlockers - anything with a TriggeredByPlayers or TriggeredBy.
        ///
        /// Those lists are left by walking away: the proximity workers drop a client whose
        /// player is no longer near, and a use's recovery drops its user. A player who logged
        /// out, dropped or zoned while standing on a waypoint keeps the last position they had,
        /// so they were near it for good; the pad worker only ever tests clients still on the
        /// map's list; and RemovePlayer cancels the queued recoveries that would have let a
        /// station or a logos go. Each such entry kept the whole Client - manifestation,
        /// inventory lists, packet queues, socket - alive for the rest of the process, in a list
        /// scanned every second. Waypoints are where people log out.
        ///
        /// Called from RemovePlayer. It walks every object and trigger on the map, which is fine
        /// for something that happens once per player per map change.
        /// </summary>
        internal void ForgetPlayer(MapChannel mapChannel, Client client)
        {
            if (mapChannel == null || client == null)
                return;

            static void Forget(DynamicObject obj, Client leaving)
            {
                obj?.TriggeredByPlayers.RemoveAll(c => c == leaving);
            }

            foreach (var obj in mapChannel.DynamicObjects)
                Forget(obj, client);

            foreach (var obj in mapChannel.Teleporters.Values)
                Forget(obj, client);

            foreach (var obj in mapChannel.ControlPoints.Values)
                Forget(obj, client);

            foreach (var obj in mapChannel.FootLockers.Values)
                Forget(obj, client);

            foreach (var obj in mapChannel.Kraftwerks.Values)
                Forget(obj, client);

            foreach (var cell in mapChannel.MapCellInfo.Cells.Values)
            {
                foreach (var obj in cell.DynamicObjectList)
                    Forget(obj, client);

                foreach (var trigger in cell.MapTriggers)
                    trigger.TriggeredBy.RemoveAll(c => c == client);
            }
        }

        /// <summary>
        /// Whether the player currently has a waypoint window open on the server's side: the
        /// proximity workers add a client to a teleporter's TriggeredByPlayers or a dropship
        /// pad's TriggeredBy while it is within range, and take it out again when it leaves.
        /// </summary>
        private static bool IsAtWaypoint(Client client)
        {
            var mapChannel = client.Player.MapChannel;

            if (mapChannel == null)
                return false;

            foreach (var teleporter in mapChannel.Teleporters.Values)
                if (teleporter.TriggeredByPlayers.Contains(client))
                    return true;

            // A pad's trigger lives in the cell its centre is in, which need not be the cell the
            // player is in when they are within its 5 m of it.
            return PadUnder(client) != null;
        }

        internal void TeleportAcknowledge(Client client)
        {
            client.CallMethod(client.Player.EntityId, new TeleportArrivalPacket());
        }

        internal void PlayerEnterWaypoint(DynamicObject obj)
        {
            var cellSeed = CellManager.Instance.GetCellSeed(obj.Position);
            var mapChannel = MapChannelManager.Instance.FindByContextId(obj.MapContextId);

            foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
            {
                // check if player is near waypoint
                if (!client.Player.IsNear2m(obj))
                {
                    continue;
                }

                // check if already added
                if (obj.TriggeredByPlayers.Any(p => p == client))
                {
                    continue;
                }

                // if not add him and send enter packet
                obj.TriggeredByPlayers.Add(client);

                var objectData = (WaypointInfo)obj.ObjectData;

                CheckPlayerWaypoint(client, objectData);

                var waypointInfoList = CreateListOfWaypoints(client, objectData.WaypointType);

                client.CallMethod(SysEntity.ClientMethodId, new EnteredWaypointPacket(obj.MapContextId, obj.MapContextId, waypointInfoList, objectData.WaypointType, objectData.WaypointId));

                // check if we already added him to the waypoint
            }
        }

        internal void PlayerExitWaypoint(DynamicObject obj)
        {
            for (var i = obj.TriggeredByPlayers.Count - 1; i >= 0; i--)
            {
                var client = obj.TriggeredByPlayers[i];

                // A connection that has gone is not at anything, wherever its player was left.
                if (client.State == ClientState.Disconnected)
                {
                    obj.TriggeredByPlayers.RemoveAt(i);
                    continue;
                }

                if (!client.Player.IsNear2m(obj))
                {
                    obj.TriggeredByPlayers.RemoveAt(i);

                    client.CallMethod(SysEntity.ClientMethodId, new ExitedWaypointPacket());
                }
            }
        }

        /// <summary>
        /// The dropship travel window for a player standing on a pad: every pad they have gained
        /// on the same planet, plus the one they are standing on, grouped by map and placed where
        /// it really is - the window plots each entry on that map's picture. Gaining is by
        /// walking into a pad's beam (see <see cref="MapTriggerManager.PlayerEnterTriggerRange"/>),
        /// as the client's help text says: "you must first travel to another map and gain access
        /// to a Dropship Transport there before you can use this method of travel". Every pad on
        /// both planets used to be offered, the first of each map at a made-up position.
        /// </summary>
        internal Dictionary<uint, MapWaypointInfoList> CreateListOfDropships(Client client, uint currentPadId)
        {
            var dropships = new Dictionary<uint, MapWaypointInfoList>();
            var player = client?.Player;

            if (player?.MapChannel == null)
                return dropships;

            var planet = player.MapChannel.MapInfo.Planet;
            var gained = new HashSet<uint>(player.GainedWaypoints.Select(w => w.WaypointId)) { currentPadId };

            foreach (var teleporter in Teleporters.Values)
            {
                if (!(teleporter.ObjectData is WaypointInfo info) || info.WaypointType != WaypointType.Dropship)
                    continue;

                if (!gained.Contains(info.WaypointId))
                    continue;

                var map = MapChannelManager.Instance.FindByContextId(teleporter.MapContextId);

                if (map == null || map.MapInfo.Planet != planet)
                    continue;

                if (!dropships.TryGetValue(teleporter.MapContextId, out var list))
                {
                    list = new MapWaypointInfoList(teleporter.MapContextId,
                        new List<MapInstanceInfo> { new MapInstanceInfo(1, teleporter.MapContextId, MapInstanceStatus.Low) },
                        new List<WaypointInfo>());

                    dropships.Add(teleporter.MapContextId, list);
                }

                list.Waypoints.Add(new WaypointInfo(info.WaypointId, info.Contested, teleporter.Position, info.WaypointType));
            }

            return dropships;
        }

        /// <summary>The dropship pad trigger the player is standing in, or null.</summary>
        internal static MapTrigger PadUnder(Client client)
        {
            var mapChannel = client?.Player?.MapChannel;

            if (mapChannel == null)
                return null;

            foreach (var cell in CellManager.CellsIn(mapChannel, client.Player.Cells))
                foreach (var trigger in cell.MapTriggers)
                    if (trigger.TriggeredBy.Contains(client))
                        return trigger;

            return null;
        }

        /// <summary>
        /// Gives the player every dropship pad in the world, the way walking into each beam
        /// would. For testing a network that spans maps with nothing else on them yet.
        /// </summary>
        internal int GainAllDropshipPads(Client client)
        {
            var given = 0;

            foreach (var teleporter in Teleporters.Values)
            {
                if (!(teleporter.ObjectData is WaypointInfo info) || info.WaypointType != WaypointType.Dropship)
                    continue;

                if (client.Player.GainedWaypoints.Any(w => w.WaypointId == info.WaypointId))
                    continue;

                CheckPlayerWaypoint(client, info);
                given++;
            }

            return given;
        }
        #endregion
    }
}
