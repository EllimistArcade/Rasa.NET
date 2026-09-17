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

            switch (obj.DynamicObjectType)
            {
                case DynamicObjectType.ControlPoint:
                    {
                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 10000));
                        client.Player.MapChannel.PerformRecovery.Add(new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 10000));

                        obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Lockbox:
                    {
                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 100));
                        client.Player.MapChannel.PerformRecovery.Add(new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 100));

                        obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Logos:
                    {
                        var actionData = new ActionData(client.Player, packet.ActionId, packet.ActionArgId, 10000);
                        actionData.SourceId = obj.EntityId;

                        client.CallMethod(client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
                        client.CallMethod(packet.EntityId, new UsePacket(client.Player.EntityId, obj.StateId, 10000));
                        client.Player.MapChannel.PerformRecovery.Add(actionData);

                        obj.TriggeredByPlayers.Add(client);
                        break;
                    }
                case DynamicObjectType.Kraftwerks:
                    KraftwerksManager.Instance.Use(client, obj, packet.ActionArgId);
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

            // dynamicObjects
            foreach (var dynamicObject in mapChannel.DynamicObjects)
            {
                // spawn object
                if (!dynamicObject.IsInWorld)
                {
                    dynamicObject.RespawnTime -= delta;

                    if (dynamicObject.RespawnTime <= 0)
                    {
                        CellManager.Instance.AddToWorld(mapChannel, dynamicObject);
                        dynamicObject.IsInWorld = true;
                        dynamicObject.StateId = UseObjectState.IdStateActive;
                        dynamicObject.WindupTime = 10000;
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

            var entityData = new List<PythonPacket>
            {
                // PhysicalEntity
                new IsTargetablePacket(classInfo.TargetFlag),
                new WorldLocationDescriptorPacket(dynamicObject.Position, dynamicObject.Rotation),
                // set state
                new UsableInfoPacket(dynamicObject.IsEnabled, dynamicObject.StateId, 0, dynamicObject.WindupTime, dynamicObject.ActivateMission)
        };

            client.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(dynamicObject.EntityId, dynamicObject.EntityClassId, entityData));
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
                        controlpoint.Faction = controlpoint.Faction == Factions.AFS ? Factions.Bane : Factions.AFS;
                        controlpoint.StateId = controlpoint.StateId == UseObjectState.CpointStateFactionAOwned ? UseObjectState.CpointStateFactionBOwned : UseObjectState.CpointStateFactionAOwned;

                        CellManager.Instance.CellCallMethod(controlpoint, new ForceStatePacket(controlpoint.StateId, 100));
                        CellManager.Instance.CellCallMethod(controlpoint, new UsableInfoPacket(true, controlpoint.StateId, 0, 10000, 0));
                        break;
                    }
            }
        }

        #endregion

        #region Dropship
        public void DropshipsWorker(long timePassed)
        {
            // Dropships is server-wide; each one is removed from its own map, not from
            // whichever map the caller happened to be iterating.
            foreach (var entry in Dropships)
            {
                var dropship = entry.Value;

                if (dropship.DropshipType != DropshipType.Spawner && dropship.DropshipType != DropshipType.Teleporter)
                {
                    Logger.WriteLog(LogType.Debug, $"error dropshiptype {dropship.DropshipType}");
                    return;
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

                        if (dropship.DropshipType == DropshipType.Teleporter)
                        {
                            if (dropship.Client.State == ClientState.Ingame)
                            {
                                CellManager.Instance.CellCallMethod(dropship.Client.Player.MapChannel, dropship.Client.Player, new PreTeleportPacket(TeleportType.Default));
                                dropship.Client.CallMethod(SysEntity.ClientMethodId, new BeginTeleportPacket());
                            }
                        }

                        if (dropship.DropshipType == DropshipType.Spawner)
                        {
                            // create list of creatures to spawn
                            var creatureList = SpawnPoolManager.Instance.CreateListOfCreatures(dropship.SpawnPool);

                            // spawn creatures
                            SpawnPoolManager.Instance.SpawnCreatures(dropship.SpawnPool, creatureList);
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

                        if (dropship.DropshipType == DropshipType.Teleporter)
                            if (dropship.Client.State == ClientState.Teleporting)
                                dropship.Client.CallMethod(SysEntity.ClientMethodId, new UnrequestMovementBlockPacket());
                        break;
                    case 5:
                        if (dropship.DropshipType == DropshipType.Teleporter)
                        {
                            switch (dropship.Client.State)
                            {
                                case ClientState.Ingame:
                                    CellManager.Instance.RemoveFromWorld(dropship.Client);
                                    dropship.Client.Player.MapChannel.ClientList.Remove(dropship.Client);

                                    // Effects end with the map, as MapChannelManager.RemovePlayer ends them - a
                                    // dropship ride never goes through it. A sprint used to ride along: the arrival
                                    // introduced the player to everyone without it, their own client included, while
                                    // the arrival's ActorInfo gave that client the sprint's speed and its drain picked
                                    // up again, under an effect id handed out by the map they had left and with no
                                    // buff on any screen to show for it.
                                    GameEffectManager.Instance.ClearEffects(dropship.Client.Player);

                                    CommunicatorManager.Instance.LeaveMapChannels(dropship.Client);
                                    dropship.Client.CallMethod(SysEntity.ClientMethodId, new UnrequestMovementBlockPacket());
                                    dropship.Client.CallMethod(SysEntity.ClientMethodId, new PreWonkavatePacket());
                                    dropship.Client.CallMethod(SysEntity.CurrentInputStateId, new WonkavatePacket(dropship.DestinationMapId, 1, MapChannelManager.Instance.MapChannelArray[dropship.DestinationMapId].MapInfo.MapVersion, dropship.Destination, 0));
                                    dropship.Client.Player.Position = dropship.Destination;
                                    dropship.Client.Player.Target = 0;
                                    dropship.Client.State = ClientState.Teleporting;
                                    dropship.Client.AwaitingMapLoaded = true;
                                    break;
                                case ClientState.Teleporting:
                                    dropship.Client.State = ClientState.Ingame;
                                    ManifestationManager.Instance.ResetInactivity(dropship.Client);
                                    break;
                                default:
                                    Logger.WriteLog(LogType.Error, $"Unsupported CLientState {dropship.Client.State}");
                                    break;
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
                        CellManager.Instance.CellCallMethod(obj, new UsableInfoPacket(true, obj.StateId, 0, 10000, 0));

                        var logosId = 0u;
                        foreach (var entry in mapChannel.DynamicObjects)
                        {
                            var logos = entry as Logos;
                            if (action.SourceId == logos.EntityId)
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
                        CellManager.Instance.AddToWorld(mapChannel, new MapTrigger(teleporter.Id, teleporter.Description, teleporter.Position, teleporter.Rotation, teleporter.MapContextId));
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

                var teleporter = Teleporters[waypoint.WaypointId];
                var teleporterData = teleporter.ObjectData as WaypointInfo;

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
            // the destination has to exist, and it has to be one this character has gained
            // (dropships are offered to everyone, see CreateListOfDropships).
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

            if (objData.WaypointType != WaypointType.Dropship
                && !client.Player.GainedWaypoints.Any(w => w.WaypointId == objData.WaypointId))
            {
                Logger.WriteLog(LogType.Debug, $"SelectWaypoint: {client.Player.Name} has not gained waypoint {objData.WaypointId}");
                return;
            }

            if (mapContextId != client.Player.MapContextId)
            {
                var dropship = new Dropship(Factions.AFS, DropshipType.Teleporter, client, teleporter.Position, mapContextId);

                CellManager.Instance.AddToWorld(client.Player.MapChannel, dropship);
                Dropships.Add(dropship.EntityId, dropship);
                client.CallMethod(SysEntity.ClientMethodId, new RequestMovementBlockPacket());

                client.LoadingMap = mapContextId;
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

            client.CellCallMethod(client, client.Player.EntityId, new PreTeleportPacket(TeleportType.Default));
            client.CallMethod(client.Player.EntityId, new TeleportPacket(teleporter.Position, teleporter.Rotation, TeleportType.Default, 5));
            client.CallMethod(SysEntity.ClientMethodId, new BeginTeleportPacket());
            client.CellMoveObject(client, new MoveObjectMessage(client.Player.EntityId, movementData), false);

            teleporter.TriggeredByPlayers.Remove(client);    // ToDO: maybe safely remove client
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

            if (mapChannel.MapCellInfo.Cells.TryGetValue(client.Player.Cells[2, 2], out var cell))
                foreach (var trigger in cell.MapTriggers)
                    if (trigger.TriggeredBy.Contains(client))
                        return true;

            return false;
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

                if (!client.Player.IsNear2m(obj))
                {
                    obj.TriggeredByPlayers.RemoveAt(i);

                    client.CallMethod(SysEntity.ClientMethodId, new ExitedWaypointPacket());
                }
            }
        }

        internal Dictionary<uint, MapWaypointInfoList> CreateListOfDropships()

        {
            var dropships = new Dictionary<uint, MapWaypointInfoList>();

            // for now we add all dropships, ToDO: give player only gained dropships
            foreach (var entry in Teleporters)
            {
                var teleporter = entry.Value;
                var teleporterInfo = teleporter.ObjectData as WaypointInfo;

                if (teleporterInfo.WaypointType == WaypointType.Dropship)
                {
                    if (dropships.ContainsKey(teleporter.MapContextId))
                    {
                        var map = dropships[teleporter.MapContextId];
                        var waypoints = map.Waypoints;

                        waypoints.Add(new WaypointInfo(teleporterInfo.WaypointId, teleporterInfo.Contested, teleporter.Position, teleporterInfo.WaypointType));
                    }
                    else
                    {
                        //create new entry
                        var instance = new List<MapInstanceInfo> { new MapInstanceInfo(1, teleporter.MapContextId, MapInstanceStatus.Low) };
                        var waypoints = new List<WaypointInfo> { new WaypointInfo(teleporterInfo.WaypointId, teleporterInfo.Contested, new Vector3(-225.353f, 99.597f, -70.5246f), WaypointType.Dropship) };

                        var mapWaypointInfoList = new MapWaypointInfoList(teleporter.MapContextId, instance, waypoints);

                        dropships.Add(teleporter.MapContextId, mapWaypointInfoList);
                    }
                }
            }

            return dropships;
        }
        #endregion
    }
}
