using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.ClientMethod.Server;
    using Packets.Game.Server;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.World;
    using Timer;

    public class MapChannelManager
    {
        private static MapChannelManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly int MapChannel_PlayerQueue = 32;
        public readonly Dictionary<uint, MapChannel> MapChannelArray = new Dictionary<uint, MapChannel>();           // list of loaded maps
        public readonly Timer Timer = new();

        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        public static MapChannelManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new MapChannelManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }
        public MapChannelManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        /// <summary>
        /// Wait between RequestLogout and an honoured CharacterLogout. Sent to the client as
        /// LogoutTimeRemaining, which keeps the logout window's Logout button disabled until it
        /// has elapsed.
        /// </summary>
        public const int LogoutDelayMs = 5000;

        /// <summary>
        /// Allowance for clock-rate drift on the early-logout check. A normal client cannot be
        /// early: it starts its countdown when LogoutTimeRemaining arrives, which is after the
        /// server recorded the request.
        /// </summary>
        private const int LogoutDelayToleranceMs = 250;

        public void CharacterLogout(Client client)
        {
            // Nothing requested, or the request was cancelled.
            if (client.Player.LogoutActive == false)
                return;

            // The delay used to be advisory - enforced only by the client's disabled button - so a
            // client that skipped the countdown could leave instantly, mid-fight. Refuse it until
            // the countdown the server announced has actually run.
            var waited = Environment.TickCount64 - client.Player.LogoutRequestedTick;

            if (waited < LogoutDelayMs - LogoutDelayToleranceMs)
            {
                Logger.WriteLog(LogType.Security, $"{client.Player.FamilyName} sent CharacterLogout {waited} ms into a {LogoutDelayMs} ms logout countdown; ignored");
                return;
            }

            client.Player.RemoveFromMap = true;
            client.State = ClientState.LoggedIn;
        }

        /// <summary>
        /// The logout window's Cancel button (client/ui/logoutwindow.py:108). Withdraws a pending
        /// logout, so a CharacterLogout that follows is ignored until the player requests again.
        /// </summary>
        public void CancelLogoutRequest(Client client)
        {
            // Once CharacterLogout has flagged the player for removal the logout is under way;
            // a late cancel does not pull them back.
            if (!client.Player.LogoutActive || client.Player.RemoveFromMap)
                return;

            client.Player.LogoutActive = false;
        }

        public MapChannel FindByContextId(uint contextId)
        {
            return MapChannelArray[contextId];
        }

        public Dictionary<int, AbilityDrawerData> GetPlayerAbilities(uint characterId)
        {
            var abilities = new Dictionary<int, AbilityDrawerData>();
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var abilitiesData = unitOfWork.CharacterAbilityDrawers.GetCharacterAbilities(characterId);

            foreach (var ability in abilitiesData)
            {
                if (ability.AbilityId == 0) continue;

                abilities.Add(ability.AbilitySlot, new AbilityDrawerData(ability.AbilitySlot, ability.AbilityId, ability.AbilityLevel));
            }

            return abilities;
        }

        public Dictionary<SkillId, SkillsData> GetPlayerSkills(uint characterId)
        {
            var skills = new Dictionary<SkillId, SkillsData>();
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var skillsData = unitOfWork.CharacterSkills.GetCharacterSkills(characterId);

            foreach (var skill in skillsData)
                skills.Add((SkillId)skill.SkillId, new SkillsData((SkillId)skill.SkillId, skill.AbilityId, skill.SkillLevel));

            return skills;
        }

        public void MapChannelInit()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var loadedMaps = unitOfWork.MapInfos.Get();

            foreach (var mapInfo in loadedMaps)
            {
                // load all maps
                var newMapChannel = new MapChannel
                {
                    MapInfo = new MapInfo(mapInfo),
                    //TimerClientEffectUpdate = Environment.TickCount,
                    //TimerMissileUpdate = Environment.TickCount,
                    //TimerDynObjUpdate = Environment.TickCount,
                    //TimerGeneralTimer = Environment.TickCount,
                    //TimerController = Environment.TickCount,
                    //TimerPlayerUpdate = Environment.TickCount,
                    //PlayerCount = 0,
                    PlayerLimit = 128,
                    ClientList = new List<Client>()
                };
                // register mapChannel
                MapChannelArray.Add(mapInfo.Id, newMapChannel);
            }
            Logger.WriteLog(LogType.Initialize, "");
            Logger.WriteLog(LogType.Initialize, "Server ready!");

            Timer.Add("AutoFire", 100, true, null);
            Timer.Add("CheckForLogingClients", 1000, true, null);
            Timer.Add("CheckForObjects", 1000, true, null);
            Timer.Add("ClientEffectUpdate", 500, true, null);
            Timer.Add("CellUpdateVisibility", 1000, true, null);
            Timer.Add("CheckForCreatures", 1000, true, null);
            Timer.Add("CheckForMapTriggers", 1000, true, null);
        }

        public void MapChannelWorker(long delta)
        {
            Timer.Update(delta);

            PartyManager.Instance.ExpireHeldMembers();

            // Server-wide lists, ticked once. These used to run inside the per-map loop below,
            // guarded by that map having players, so with N populated maps every auto-fire
            // timer and every dropship advanced N times per tick.
            DynamicObjectManager.Instance.DropshipsWorker(delta);

            if (Timer.IsTriggered("AutoFire"))
                ManifestationManager.Instance.AutoFireTimerDoWork(delta);

            foreach (var t in MapChannelArray)
            {
                var mapChannel = t.Value;

                mapChannel.MapChannelElapsed += delta;

                if (Timer.IsTriggered("CheckForLogingClients"))
                    if (mapChannel.QueuedClients.Count > 0)
                    {
                        // create new mapClient
                        var dequedClient = mapChannel.QueuedClients.Dequeue();

                        // add it to list
                        mapChannel.ClientList.Add(dequedClient);
                    }

                if (mapChannel.ClientList.Count > 0)
                {
                    ActorActionManager.Instance.DoWork(mapChannel, delta);
                    MissileManager.Instance.DoWork(mapChannel, delta);
                    BehaviorManager.Instance.MapChannelThink(mapChannel, delta);

                    // despawn timers, and minions whose master has gone
                    MinionManager.Instance.Worker(mapChannel, delta);

                    // players whose combat timer has run out
                    ManifestationManager.Instance.CombatWorker(mapChannel);

                    // CellManager worker
                    if (Timer.IsTriggered("CellUpdateVisibility"))
                        CellManager.Instance.DoWork(mapChannel);

                    // check for objects
                    if (Timer.IsTriggered("CheckForObjects"))
                        DynamicObjectManager.Instance.DynamicObjectWorker(mapChannel, delta);

                    // check for creatures
                    if (Timer.IsTriggered("CheckForCreatures"))
                        SpawnPoolManager.Instance.SpawnPoolWorker(mapChannel, delta);

                    // check for mapTriggers
                    if (Timer.IsTriggered("CheckForMapTriggers"))
                    {
                        MapTriggerManager.Instance.TriggersProximityWorker(mapChannel);

                        // zone borders and instance doors: anyone standing in one leaves the map
                        MapLinkManager.Instance.Worker(mapChannel);

                        // ambient/music/sky/minimap regions: tell whoever changed region
                        RegionManager.Instance.Worker(mapChannel);
                    }

                    // check for effects (buffs)
                    if (Timer.IsTriggered("ClientEffectUpdate"))
                        GameEffectManager.Instance.DoWork(mapChannel, delta);

                    // warn idle players and flag long-idle ones for removal below
                    ManifestationManager.Instance.CheckInactivity(mapChannel);

                    // check for players leaving the map: /logout, inactivity, and dropped
                    // connections flagged by Client.Close()
                    foreach (var client in mapChannel.ClientList)
                        if (client != null && client.Player.RemoveFromMap)
                        {
                            // The MainLoop thread has no handler of its own, so an exception
                            // escaping here stops the whole server ticking. Clear the flag
                            // first and drop the entry on failure so a bad removal is logged
                            // once instead of retried - and thrown - on every tick.
                            client.Player.RemoveFromMap = false;

                            try
                            {
                                RemovePlayer(client, true);
                            }
                            catch (Exception e)
                            {
                                Logger.WriteLog(LogType.Error, $"Failed to remove {client.Player.FamilyName} from map {mapChannel.MapInfo.MapContextId}: {e}");
                                mapChannel.ClientList.Remove(client);
                            }

                            break;
                        }
                }
            }
        }

        public void MapLoaded(Client client)
        {
            if (client.State == ClientState.Teleporting)
            {
                var dropship = new Dropship(Factions.AFS, DropshipType.Teleporter, client);
                var mapChannel = MapChannelArray[client.LoadingMap];

                client.Player.MapChannel = mapChannel;
                client.Player.MapContextId = dropship.Client.LoadingMap;

                mapChannel.ClientList.Add(client);

                CellManager.Instance.AddToWorld(client.Player.MapChannel, dropship);
                DynamicObjectManager.Instance.Dropships.Add(dropship.EntityId, dropship);
                CommunicatorManager.Instance.LoginOk(dropship.Client);
                ServerFlagManager.Instance.SendFlags(client);

                // The manifestation, its items and its entity registrations survive the map
                // change; the client's picture of them does not. Show it what the server
                // already has rather than loading and registering it all a second time, and
                // recompute the stats without the full reset that healed the player.
                InventoryManager.Instance.ResendToClient(client);
                ManifestationManager.Instance.UpdateStatsValues(client, false);

                CellManager.Instance.AddToWorld(dropship.Client); // will introduce the player to all clients, including the current owner
                MapLinkManager.Instance.PlayerEnteredMap(client);
                CellManager.Instance.CellCallMethod(dropship.Client.Player.MapChannel, dropship.Client.Player, new TeleportArrivalPacket());
                client.CallMethod(SysEntity.ClientMethodId, new RequestMovementBlockPacket());
                ManifestationManager.Instance.AssignPlayer(client);
                CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Position);
                CommunicatorManager.Instance.PlayerEnterMap(dropship.Client);

                return;
            }

            client.State = ClientState.Ingame;
            ManifestationManager.Instance.ResetInactivity(client);
            InventoryManager.Instance.InitForClient(client);
            ManifestationManager.Instance.UpdateStatsValues(client, true);

            // register new Player
            EntityManager.Instance.RegisterEntity(client.Player.EntityId, EntityType.Character);
            EntityManager.Instance.RegisterPlayer(client.Player.EntityId, client.Player);
            EntityManager.Instance.RegisterActor(client.Player.EntityId, client.Player);
            CommunicatorManager.Instance.LoginOk(client);

            // Before anything the player can act on: the client asks its own flag set whether to
            // offer a feature, and an empty set means the feature is simply missing.
            ServerFlagManager.Instance.SendFlags(client);

            // Whatever is broken about the map they have just walked into, if they are someone
            // who can do anything about it. The dialog is modal and always visible, so a player
            // would be stuck reading about server data they cannot fix.
            if (client.AccountEntry != null && client.AccountEntry.Level >= (byte)GmLevel.Observer)
                MapErrorManager.Instance.SendTo(client);

            CellManager.Instance.AddToWorld(client); // will introduce the player to all clients, including the current owner

            // Before the first link check: a player who arrives through a pass is standing in
            // the gate on this side, and must walk out of it before it can send them back.
            MapLinkManager.Instance.PlayerEnteredMap(client);
            ManifestationManager.Instance.AssignPlayer(client);

            ClanManager.Instance.InitializePlayerClanData(client);
            InventoryManager.Instance.InitClanInventory(client);
            CommunicatorManager.Instance.PlayerEnterMap(client);
            PartyManager.Instance.PlayerEnteredWorld(client);
        }

        public void PassClientToCharacterSelection(Client client)
        {
            // ToDo
            /*if (ClientsGameMainCount >= MAX_GAMEMAIN_CLIENTS)
            {
                // force disconnect
                closesocket(cgm->socket);
                //free(cgm);
                return;
            }*/
            CharacterManager.Instance.StartCharacterSelection(client);
            //Increase count and return struct
            //ClientsGameMainCount++;
        }
        public void PassClientToMapInstance(Client client)
        {
            var mapInstance = client.Player.MapChannel;
            client.CallMethod(SysEntity.ClientMethodId, new PreWonkavatePacket());
            client.CallMethod(SysEntity.CurrentInputStateId, new WonkavatePacket
               (
                   mapInstance.MapInfo.MapContextId,
                   1,           // InstanceId
                   mapInstance.MapInfo.MapVersion,
                    client.Player.Position,
                   (float)client.Player.Rotation
               ));

            client.State = ClientState.Loading;
            client.State = ClientState.Loading;
            client.Player.MapChannel.QueuedClients.Enqueue(client);
        }

        /// <summary>
        /// Moves an ingame player to a position on any loaded map by way of the loading screen:
        /// out of the current map channel, then Wonkavate into the new one. Summon and .teleport
        /// each had a copy of this that forgot to point the player at the new map, so when the
        /// client answered with MapLoaded it was added to the OLD map's cells at its OLD position.
        /// Everyone there saw a frozen ghost, its broadcasts went to the wrong map, and the first
        /// cell crossing on the new map indexed the old map's cell table with new-map seeds and
        /// threw KeyNotFoundException on the main loop.
        /// </summary>
        /// <returns>false when the map is not loaded or the player is not in a state to move.</returns>
        public bool ChangeMap(Client client, uint mapContextId, Vector3 position, float orientation)
        {
            if (client.Player == null || client.State != ClientState.Ingame)
                return false;

            if (!MapChannelArray.TryGetValue(mapContextId, out var mapChannel))
                return false;

            client.CallMethod(SysEntity.ClientMethodId, new PreWonkavatePacket());
            client.State = ClientState.Loading;

            // Out of the old map while the player still points at it: entities, cells, the
            // managers that track it, and the old ClientList.
            RemovePlayer(client, false);

            // The old map's cells go with the old map. RemovePlayer takes the player out of
            // them but leaves the matrix naming them, and the next line points the player at a
            // map that has no such cells - so anything broadcast over the matrix in between was
            // indexing the new map's table with the old map's seeds. AddToWorld builds a fresh
            // one when the client answers with MapLoaded.
            client.Player.Cells = new uint[5, 5];

            // What MapLoaded reads back when the client is ready: the map channel it adds the
            // player to, and the position the cell matrix is built from.
            client.Player.MapChannel = mapChannel;
            client.Player.MapContextId = mapContextId;
            client.Player.Position = position;
            client.Player.Rotation = orientation;
            client.LoadingMap = mapContextId;

            var packet = new WonkavatePacket(
                mapChannel.MapInfo.MapContextId,
                0,                  // ToDo MapInstanceId
                mapChannel.MapInfo.MapVersion,
                position,
                orientation);

            client.CallMethod(SysEntity.CurrentInputStateId, packet);
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Position, packet);
            mapChannel.ClientList.Add(client);

            return true;
        }

        public void Ping(Client client, double ping)
        {
            client.CallMethod(SysEntity.ClientMethodId, new AckPingPacket(ping));
        }

        /// <summary>
        /// Destroys every item entity a slot list holds, and empties the list with them.
        ///
        /// Emptying it is the point. Destroying an item hands its entity id back to the
        /// EntityManager's free list, which gives that id to the next item created - somebody
        /// else's, moments later - while the slots here went on naming it. A connection that is
        /// no longer in the world still had its four lists: at the character screen after a
        /// logout, or between maps for as long as the client took to answer with MapLoaded. Every
        /// handler that resolves a slot to an entity id resolved those to another player's items,
        /// which was enough to auction, sell, bank or craft with them.
        /// </summary>
        private static void DestroyInventory(Client client, List<ulong> inventory)
        {
            foreach (var entityId in inventory)
                if (entityId != 0)
                    EntityManager.Instance.DestroyPhysicalEntity(client, entityId, EntityType.Item);

            inventory.Clear();
        }

        public void RemovePlayer(Client client, bool logout)
        {
            // A target is an entity on this map; the client does not always re-target after a
            // map change, and MissileLaunch refuses cross-map targets, so drop it here.
            client.Player.Target = 0;

            // unregister Communicator
            CommunicatorManager.Instance.PlayerExitMap(client);
            // unregister mapChannelClient
            EntityManager.Instance.UnregisterEntity(client.Player.EntityId);
            EntityManager.Instance.UnregisterPlayer(client.Player.EntityId);
            EntityManager.Instance.UnregisterActor(client.Player.EntityId);

            // unregister character Inventory
            DestroyInventory(client, client.Player.Inventory.EquippedInventory);
            DestroyInventory(client, client.Player.Inventory.HomeInventory);
            DestroyInventory(client, client.Player.Inventory.PersonalInventory);
            DestroyInventory(client, client.Player.Inventory.WeaponDrawer);

            NpcManager.Instance.DiscardBuybackItems(client);
            ActorActionManager.Instance.RemoveActor(client.Player);

            // Before the player leaves the cells, while their minions can still be told to go:
            // "Player-controlled subordinates will teleport with their masters, but not change
            // maps." Leaving the map is leaving them behind, so they are dismissed, not orphaned.
            MinionManager.Instance.DismissAll(client);

            CellManager.Instance.RemoveFromWorld(client);
            MapLinkManager.Instance.RemovePlayer(client);
            RegionManager.Instance.RemovePlayer(client);
            ManifestationManager.Instance.RemovePlayerCharacter(client);
            ClanManager.Instance.RemovePlayer(client);
            LookingForGroupManager.Instance.RemovePlayer(client);
            SummonManager.Instance.RemovePlayer(client);
            TradeManager.Instance.RemovePlayer(client);
            PartyManager.Instance.RemovePlayer(client);
            PetitionManager.Instance.RemovePlayer(client);

            if (logout)
                if (client.Player.Disconected == false)
                {
                    PassClientToCharacterSelection(client);
                    client.Player.Disconected = true;
                }

            // remove from list
            for (var i = 0; i < client.Player.MapChannel.ClientList.Count; i++)
            {
                if (client == client.Player.MapChannel.ClientList[i])
                {
                    client.Player.MapChannel.ClientList.RemoveAt(i);
                    //mapClient.MapChannel.PlayerCount--;
                    break;
                }
            }

        }

        public void RequestLogout(Client client)
        {
            // A repeated request restarts the countdown, matching the fresh one the client shows.
            client.Player.LogoutActive = true;
            client.Player.LogoutRequestedTick = Environment.TickCount64;

            client.CallMethod(SysEntity.ClientMethodId, new LogoutTimeRemainingPacket(LogoutDelayMs));
        }

        public MapInstance GetMapInstance(uint mapContextId)
        {
            // TODO support additional maps
            var map = new MapInstance(new MapInfo(1220, "adv_foreas_concordia_wilderness", 1556, 0));

            return map;
        }
    }
}
