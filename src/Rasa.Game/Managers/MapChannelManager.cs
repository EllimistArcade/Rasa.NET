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

        /// <summary>
        /// The map channel with this context id, or null. A context id that names no channel is
        /// an ordinary thing to ask about - an actor whose map was torn down, or one that never
        /// had one - and callers already treat the answer as optional: ActorManager.Heal guards
        /// "if (mapChannel != null)" before broadcasting, which the throw this used to do made
        /// unreachable. The world loop drives those callers, so the exception took the process
        /// down rather than the one heal.
        /// </summary>
        public MapChannel FindByContextId(uint contextId)
        {
            return MapChannelArray.TryGetValue(contextId, out var mapChannel) ? mapChannel : null;
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
            Timer.Add("AutoFire", 100, true, null);
            Timer.Add("CheckForObjects", 1000, true, null);
            Timer.Add("ClientEffectUpdate", 500, true, null);
            Timer.Add("CellUpdateVisibility", 1000, true, null);
            Timer.Add("CheckForCreatures", 1000, true, null);
            Timer.Add("CheckForMapTriggers", 1000, true, null);
            Timer.Add("Regenerate", 1000, true, null);
        }

        private readonly Dictionary<string, long> _workerFaultQuietUntil = new();
        private readonly Dictionary<string, int> _workerFaultsSinceLog = new();

        /// <summary>
        /// Runs one worker so that its failure costs only itself. The world tick is a chain of
        /// these, and one throwing used to abandon everything after it - every other worker on that
        /// map and every map after it - on each tick it kept throwing. A fault is logged in full
        /// the first time, then at most once a minute per worker with a count.
        /// </summary>
        private void Guard(string worker, MapChannel mapChannel, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                var now = Environment.TickCount64;
                var faults = (_workerFaultsSinceLog.TryGetValue(worker, out var n) ? n : 0) + 1;

                if (_workerFaultQuietUntil.TryGetValue(worker, out var quietUntil) && now < quietUntil)
                {
                    _workerFaultsSinceLog[worker] = faults;
                    return;
                }

                var where = mapChannel == null ? "" : $" on map {mapChannel.MapInfo.MapContextId}";
                var repeat = faults > 1 ? $" ({faults} faults since the last of these)" : "";

                Logger.WriteLog(LogType.Error, $"{worker}{where} threw{repeat}: {e}");

                _workerFaultsSinceLog[worker] = 0;
                _workerFaultQuietUntil[worker] = now + 60000;
            }
        }

        public void MapChannelWorker(long delta)
        {
            Timer.Update(delta);

            PartyManager.Instance.ExpireHeldMembers();

            // Clan feuds whose time is up.
            Guard("ClanFeuds.Worker", null, () => ClanFeuds.Instance.Worker());

            // Server-wide lists, ticked once. These used to run inside the per-map loop below,
            // guarded by that map having players, so with N populated maps every auto-fire
            // timer and every dropship advanced N times per tick.
            Guard("DynamicObjectManager.DropshipsWorker", null, () => DynamicObjectManager.Instance.DropshipsWorker(delta));

            if (Timer.IsTriggered("AutoFire"))
                Guard("ManifestationManager.AutoFireTimerDoWork", null, () => ManifestationManager.Instance.AutoFireTimerDoWork(delta));

            foreach (var t in MapChannelArray)
            {
                var mapChannel = t.Value;

                mapChannel.MapChannelElapsed += delta;

                // A /killmap asked for since the last tick: done here, between thinks.
                Guard("MapReset.Worker", mapChannel, () => MapReset.Worker(mapChannel));

                // Everyone who has been sent into this map since the last tick goes onto its list.
                //
                // This used to move one client per map per second (a 1000 ms timer, one Dequeue),
                // a pace carried over from the original server, where the dequeue did the work of
                // bringing a player in. Here MapLoaded does that work, and does not wait for the
                // dequeue: a player is registered, in the cells and walking about as soon as their
                // client has loaded. The list is only how the per-player workers find them -
                // visibility, regeneration, inactivity, map links, regions, and the removal pass
                // that takes out a player whose connection has dropped - so after a restart with a
                // few hundred players coming back to one map, the last of them spent minutes in
                // the world with a frozen view of it, no regeneration, and nothing to notice them
                // leave. The workers already pass over a client that is still loading, which is
                // what most dequeued clients were even at one a second.
                while (mapChannel.QueuedClients.Count > 0)
                {
                    var queued = mapChannel.QueuedClients.Dequeue();

                    if (queued != null && !mapChannel.ClientList.Contains(queued))
                        mapChannel.ClientList.Add(queued);
                }

                if (mapChannel.ClientList.Count > 0)
                {
                    // Rushing Blow: the charging players carried a step on, every tick, before
                    // the blows whose windup is up are resolved.
                    Guard("AbilityManager.ChargeWorker", mapChannel, () => AbilityManager.Instance.ChargeWorker(mapChannel));

                    // Kael rushing blow: the blows whose charge is over.
                    Guard("KaelRushingBlow.Worker", mapChannel, () => KaelRushingBlow.Worker(mapChannel));

                    // Creature bombs, death blasts and self-destructs whose time has come.
                    Guard("CreatureBombs.Worker", mapChannel, () => CreatureBombs.Worker(mapChannel));

                    // Creature heals, repairs and revives whose windup is up.
                    Guard("CreatureSupport.Worker", mapChannel, () => CreatureSupport.Worker(mapChannel));

                    // Summoned turrets and pets whose time is up; grubs out of their cocoons.
                    Guard("CreatureSummons.Worker", mapChannel, () => CreatureSummons.Worker(mapChannel));

                    // Linkers' channels whose windup is done, and the boosts that have run out.
                    Guard("CreatureBuffs.Worker", mapChannel, () => CreatureBuffs.Worker(mapChannel));

                    // Creature actions that are not missiles, whose windup is done.
                    Guard("CreatureWindups.Worker", mapChannel, () => CreatureWindups.Worker(mapChannel));

                    // Miasmas whose time as a cloud is up coalesce.
                    Guard("CreatureMiasma.Worker", mapChannel, () => CreatureMiasma.Worker(mapChannel));

                    // Crab Mines: seeking, running, going off.
                    Guard("AbilityManager.CrabMineWorker", mapChannel, () => AbilityManager.Instance.CrabMineWorker(mapChannel));

                    // Reality Ripper: taking creatures in, and closing.
                    Guard("AbilityManager.RealityRipperWorker", mapChannel, () => AbilityManager.Instance.RealityRipperWorker(mapChannel));

                    // Trap: shooting, drawing the hate, running out.
                    Guard("AbilityManager.TrapWorker", mapChannel, () => AbilityManager.Instance.TrapWorker(mapChannel));

                    Guard("ActorActionManager.DoWork", mapChannel, () => ActorActionManager.Instance.DoWork(mapChannel, delta));
                    Guard("MissileManager.DoWork", mapChannel, () => MissileManager.Instance.DoWork(mapChannel, delta));
                    Guard("BehaviorManager.MapChannelThink", mapChannel, () => BehaviorManager.Instance.MapChannelThink(mapChannel, delta));

                    // despawn timers, and minions whose master has gone
                    Guard("MinionManager.Worker", mapChannel, () => MinionManager.Instance.Worker(mapChannel, delta));

                    // players whose combat timer has run out
                    Guard("ManifestationManager.CombatWorker", mapChannel, () => ManifestationManager.Instance.CombatWorker(mapChannel));

                    // CellManager worker
                    if (Timer.IsTriggered("CellUpdateVisibility"))
                        Guard("CellManager.DoWork", mapChannel, () => CellManager.Instance.DoWork(mapChannel));

                    // check for objects
                    if (Timer.IsTriggered("CheckForObjects"))
                        Guard("DynamicObjectManager.DynamicObjectWorker", mapChannel, () => DynamicObjectManager.Instance.DynamicObjectWorker(mapChannel, delta));

                    // check for creatures
                    if (Timer.IsTriggered("CheckForCreatures"))
                        Guard("SpawnPoolManager.SpawnPoolWorker", mapChannel, () => SpawnPoolManager.Instance.SpawnPoolWorker(mapChannel, delta));

                    // check for mapTriggers
                    if (Timer.IsTriggered("CheckForMapTriggers"))
                    {
                        Guard("MapTriggerManager.TriggersProximityWorker", mapChannel, () => MapTriggerManager.Instance.TriggersProximityWorker(mapChannel));

                        // zone borders and instance doors: anyone standing in one leaves the map
                        Guard("MapLinkManager.Worker", mapChannel, () => MapLinkManager.Instance.Worker(mapChannel));

                        // ambient/music/sky/minimap regions: tell whoever changed region
                        Guard("RegionManager.Worker", mapChannel, () => RegionManager.Instance.Worker(mapChannel));
                    }

                    // check for effects (buffs)
                    if (Timer.IsTriggered("ClientEffectUpdate"))
                    {
                        Guard("GameEffectManager.DoWork", mapChannel, () => GameEffectManager.Instance.DoWork(mapChannel, delta));

                        // Fire Support's beacons: their blasts and napalm pools.
                        Guard("AbilityManager.FireSupportWorker", mapChannel, () => AbilityManager.Instance.FireSupportWorker(mapChannel));

                        // Scatterbombs: the spent bombs are taken away once their blasts have played.
                        Guard("AbilityManager.ScatterbombWorker", mapChannel, () => AbilityManager.Instance.ScatterbombWorker(mapChannel));

                        // Cadaver Immolation: the bodies whose delay is up.
                        Guard("AbilityManager.CorpseWorker", mapChannel, () => AbilityManager.Instance.CorpseWorker(mapChannel));

                        // Hortimonculus: the plants' healing, protection and decay.
                        Guard("AbilityManager.HortimonculusWorker", mapChannel, () => AbilityManager.Instance.HortimonculusWorker(mapChannel));

                        // Reanimation: the risen whose master has gone, and the spent ones.
                        Guard("AbilityManager.ReanimationWorker", mapChannel, () => AbilityManager.Instance.ReanimationWorker(mapChannel));

                        // Spotter: the spent ones taken away, the fallen let go.
                        Guard("AbilityManager.SpotterWorker", mapChannel, () => AbilityManager.Instance.SpotterWorker(mapChannel));

                        // Mind Control: the frightened kept running, the confused turned on someone new.
                        Guard("AbilityManager.MindControlWorker", mapChannel, () => AbilityManager.Instance.MindControlWorker(mapChannel));

                        // Tactical Evasion: the smoke screens, and the marks a retreat goes back to.
                        Guard("AbilityManager.SmokeWorker", mapChannel, () => AbilityManager.Instance.SmokeWorker(mapChannel));

                        // Shield Drones: the shield raised, held over whoever is under it, and its heal.
                        Guard("ShieldDrone.Worker", mapChannel, () => ShieldDrone.Worker(mapChannel));

                        // Amoeboids: the regurgitated children whose time is up.
                        Guard("AmoeboidVomit.Worker", mapChannel, () => AmoeboidVomit.Worker(mapChannel));

                        // Falls that ended with the player standing still: no Move to end them.
                        Guard("FallDamage.Worker", mapChannel, () => FallDamage.Worker(mapChannel));
                    }

                    // a second's health, armour, power and chi for everyone here
                    if (Timer.IsTriggered("Regenerate"))
                        Guard("ActorManager.Regenerate", mapChannel, () => ActorManager.Instance.Regenerate(mapChannel));

                    // warn idle players and flag long-idle ones for removal below
                    ManifestationManager.Instance.CheckInactivity(mapChannel);

                    // check for players leaving the map: /logout, inactivity, and dropped
                    // connections flagged by Client.Close()
                    //
                    // Every one flagged, not the first: this took one player per map per tick and
                    // stopped, so two hundred connections dropping together from one map - an
                    // ISP blip, the server's own network - took twenty seconds to clear, with each
                    // character still registered, in the cells and fought by creatures meanwhile.
                    // RemovePlayer writes to the database, so a crowd is spread over a few ticks
                    // by RemovalBudgetMs rather than stalling one; the first always goes.
                    var removalFrom = System.Diagnostics.Stopwatch.GetTimestamp();
                    var removed = 0;

                    foreach (var client in mapChannel.ClientList.ToArray())
                        if (client != null && client.Player.RemoveFromMap)
                        {
                            if (removed > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(removalFrom).TotalMilliseconds >= RemovalBudgetMs)
                                break;

                            removed++;

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
                        }
                }
            }
        }

        /// <summary>How long one map's removal pass may run in a tick before the rest wait for the next; see MapChannelWorker.</summary>
        private const double RemovalBudgetMs = 50;

        /// <summary>The account level whose clients get EnableDevCommands when they enter the world.</summary>
        public const GmLevel DevCommandsLevel = GmLevel.GameMaster;

        /// <summary>Whether this client is to get EnableDevCommands now: a GM's, not sent it yet.</summary>
        public static bool ShouldEnableDevCommands(Client client) =>
            client?.AccountEntry != null && !client.DevCommandsSent && client.AccountEntry.Level >= (byte)DevCommandsLevel;

        public void MapLoaded(Client client)
        {
            // Only in answer to a Wonkavate, and once for each. Nothing was checked, and this is
            // one of the methods a connection may call from outside the world - the loading
            // screen is where it comes from - so it could be sent from anywhere, any number of
            // times, and every path below assumes a player who is arriving.
            //
            // From the character screen after a logout it put the character back in the world:
            // registered, in its old map's cells and introduced to everyone there, but on no map's
            // client list, which is the only place the removal that follows a dropped connection is
            // looked for. Once the connection closed, the character stayed where it was, frozen,
            // for as long as the server ran. A second one during a dropship arrival built a second
            // arrival dropship; the first landed the player, and the second found them already in
            // the world and took itself for a departure with nowhere to go - MapChannelArray[0], a
            // KeyNotFoundException at the top of the map channel worker, on every tick after,
            // because the dropship was only removed at the end of the phase that threw.
            //
            // The state has to agree as well as the flag: a pending load can be overtaken by
            // something else changing the state before the client answers.
            if (!client.AwaitingMapLoaded
                || (client.State != ClientState.Loading && client.State != ClientState.Teleporting))
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry?.Id} sent MapLoaded in state {client.State} with {(client.AwaitingMapLoaded ? "a" : "no")} map load pending; ignored.");
                return;
            }

            client.AwaitingMapLoaded = false;

            if (client.State == ClientState.Teleporting)
            {
                var dropship = new Dropship(TargetCategory.Friendly, DropshipType.Teleporter, client, DropshipRole.Arrival);
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

                // The buffs brought from the map left, now there is somebody to show them to.
                EffectCarry.Restore(client);

                // And what they sold before the ride, still to be bought back.
                NpcManager.Instance.ResendBuyback(client);

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

            // A GM's client gets its developer commands, once per connection: see
            // EnableDevCommandsPacket for what a client needs to do anything with it.
            if (ShouldEnableDevCommands(client))
            {
                client.CallMethod(SysEntity.ClientMethodId, new EnableDevCommandsPacket());
                client.DevCommandsSent = true;
            }

            CellManager.Instance.AddToWorld(client); // will introduce the player to all clients, including the current owner

            // Before the first link check: a player who arrives through a pass is standing in
            // the gate on this side, and must walk out of it before it can send them back.
            MapLinkManager.Instance.PlayerEnteredMap(client);
            ManifestationManager.Instance.AssignPlayer(client);

            // The buffs brought from the map left (nothing on a login): after the player is in
            // the cells and their own client has its actor's info, so the attach reaches it and
            // everyone around.
            EffectCarry.Restore(client);

            // The Recently Sold list: what is still to be bought back after a map change, or a
            // clean one on a login.
            NpcManager.Instance.ResendBuyback(client);

            ClanManager.Instance.InitializePlayerClanData(client);
            InventoryManager.Instance.InitClanInventory(client);

            // The clan's feuds, for the tracker and the Clan Warfare list: after a login the client
            // knows of none, and after a map link this only refreshes them.
            ClanFeuds.Instance.PlayerEnteredWorld(client);

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
            client.AwaitingMapLoaded = true;
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
            client.Player.PlaceAt(position);
            client.Player.Rotation = orientation;
            client.LoadingMap = mapContextId;

            var packet = new WonkavatePacket(
                mapChannel.MapInfo.MapContextId,
                0,                  // ToDo MapInstanceId
                mapChannel.MapInfo.MapVersion,
                position,
                orientation);

            client.CallMethod(SysEntity.CurrentInputStateId, packet);
            client.AwaitingMapLoaded = true;
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

        /// <summary>
        /// Takes a player out of the map: out of the managers that track them, out of the entity
        /// tables and the cells, off the map's client list, and - on a logout - back to the
        /// character screen.
        ///
        /// Every step runs in <see cref="RemovalStep"/>, which logs a failure and goes on to the
        /// next. It used to be one straight run, with the database writes second and the cells
        /// near the end, and the caller clears RemoveFromMap before calling and drops the client
        /// from the list if this throws - so a SqliteException out of the position save (a busy
        /// database, a full disk) or a throw from an effect's OnDetached left a manifestation
        /// registered and standing in its cells with nothing that would ever look at it again.
        /// Worse, if the throw came after the inventories were emptied, every player who later
        /// walked into those cells was introduced to it, and building its entity data indexed
        /// its empty equipment list: the newcomer's MapLoaded threw and they were disconnected,
        /// for as long as the server ran. A step that fails now costs what that step does - a
        /// save, a notice - and the player still leaves.
        ///
        /// Each step also tolerates having been done already (lists emptied, ids unregistered,
        /// cells left), so a second removal of the same player is harmless.
        /// </summary>
        public void RemovePlayer(Client client, bool logout)
        {
            var player = client.Player;
            var mapChannel = player.MapChannel;

            // A target is an entity on this map; the client does not always re-target after a
            // map change, and MissileLaunch refuses cross-map targets, so drop it here.
            player.Target = 0;

            // Position and time played to the database, chat channels left, friends told.
            RemovalStep(client, "leaving chat and saving position", () => CommunicatorManager.Instance.PlayerExitMap(client));

            // unregister mapChannelClient
            RemovalStep(client, "unregistering the character", () =>
            {
                EntityManager.Instance.UnregisterEntity(player.EntityId);
                EntityManager.Instance.UnregisterPlayer(player.EntityId);
                EntityManager.Instance.UnregisterActor(player.EntityId);
            });

            // unregister character Inventory
            RemovalStep(client, "releasing the inventory", () =>
            {
                DestroyInventory(client, player.Inventory.EquippedInventory);
                DestroyInventory(client, player.Inventory.HomeInventory);
                DestroyInventory(client, player.Inventory.PersonalInventory);
                DestroyInventory(client, player.Inventory.WeaponDrawer);

                // The auction house's pick-up items are this player's; they are loaded again from
                // their rows on arrival, like the lists above, and were left registered each time.
                DestroyInventory(client, player.Inventory.InboxItems);

                // Listed items are the auction house's (AuctionHouseManager.Listed), which keeps
                // them while the seller is away and gives the same objects back on their next
                // load; the seller's list of them only goes.
                player.Inventory.AuctionItems.Clear();
            });

            // The Recently Sold list lasts the session: a map change keeps it, and the arrival
            // shows it again (NpcManager.ResendBuyback).
            if (logout)
                RemovalStep(client, "discarding the buyback list", () => NpcManager.Instance.DiscardBuybackItems(client));

            RemovalStep(client, "removing queued actions", () => ActorActionManager.Instance.RemoveActor(player));

            // Effects are per map as far as the clients know - nobody on the next map was told
            // about them - and a sprint left running would keep draining adrenaline unseen.
            // A map change keeps the timed buffs aside, clocks stopped, to go on again on arrival
            // (EffectCarry); a logout keeps nothing.
            RemovalStep(client, "clearing effects", () =>
            {
                if (logout)
                    EffectCarry.Drop(player);
                else
                    EffectCarry.Stash(player);

                GameEffectManager.Instance.ClearEffects(mapChannel, player);
            });

            // The weapon is put away with them. A manifestation arriving on a map starts with
            // nothing in its hands - the client transitions to _no_tool and is never told
            // otherwise, since nothing sends WeaponReady on map entry - while this flag lived on
            // the Manifestation, which survives the change. The two then disagreed for the rest
            // of the session: the server thought a weapon was out that the player could see was
            // not, which let a tool action through that the client refuses (basetoolaction.py
            // checks IsWeaponReady) and skipped the draw the fire path performs for itself.
            player.WeaponReady = false;

            // The combat stance goes the same way. The client's manifestation arrives at peace -
            // ClearMap removed it and the new map creates it afresh - but the flag stayed as it
            // was, and AssignPlayer sends it back in ActorInfo (isHoldingCombatMode). A player who
            // zoned in stance was put back into it with a hold, which outlasts the client's own
            // 2.5 s return to peace. The RequestVisualCombatMode that would have cleared it is one
            // the client sends after the server has already set Loading, which is dropped.
            player.InCombatMode = false;
            player.RequestedCombatMode = false;
            player.AutoFireCombatMode = false;

            // And whatever it was following or walking up to: that is on the map being left.
            player.TrackingTargetEntityId = 0;

            // Before the player leaves the cells, while their minions can still be told to go:
            // "Player-controlled subordinates will teleport with their masters, but not change
            // maps." Leaving the map is leaving them behind, so they are dismissed, not orphaned.
            RemovalStep(client, "dismissing minions", () => MinionManager.Instance.DismissAll(client));

            // Off every waypoint, pad, station and control point's list of who is at it; nothing
            // else takes a player who left standing on one off it.
            RemovalStep(client, "leaving waypoints and objects", () => DynamicObjectManager.Instance.ForgetPlayer(mapChannel, client));

            RemovalStep(client, "leaving the cells", () => CellManager.Instance.RemoveFromWorld(client));
            RemovalStep(client, "leaving map links", () => MapLinkManager.Instance.RemovePlayer(client));
            RemovalStep(client, "leaving regions", () => RegionManager.Instance.RemovePlayer(client));
            RemovalStep(client, "removing the character", () => ManifestationManager.Instance.RemovePlayerCharacter(client));
            RemovalStep(client, "leaving the clan roster", () => ClanManager.Instance.RemovePlayer(client));
            RemovalStep(client, "leaving looking-for-group", () => LookingForGroupManager.Instance.RemovePlayer(client));
            RemovalStep(client, "cancelling summons", () => SummonManager.Instance.RemovePlayer(client));
            RemovalStep(client, "cancelling trade", () => TradeManager.Instance.RemovePlayer(client));
            RemovalStep(client, "leaving the squad", () => PartyManager.Instance.RemovePlayer(client));
            RemovalStep(client, "closing petitions", () => PetitionManager.Instance.RemovePlayer(client));

            // Leaving the world, not the map: the cooldowns still running go to the database, to
            // be picked up when the character is next loaded.
            if (logout)
                RemovalStep(client, "saving cooldowns", () => ActionReuse.Save(client));

            if (logout && player.Disconected == false)
            {
                RemovalStep(client, "returning to character selection", () => PassClientToCharacterSelection(client));
                player.Disconected = true;
            }

            // remove from list
            mapChannel?.ClientList.Remove(client);
        }

        /// <summary>
        /// One step of <see cref="RemovePlayer"/>. A failure is logged against the player and the
        /// step, and the removal carries on: see RemovePlayer for why no step may stop the rest.
        /// </summary>
        private static void RemovalStep(Client client, string step, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Removing {client.Player?.FamilyName} from the world: {step} failed, carrying on with the rest: {e}");
            }
        }

        /// <summary>
        /// Takes a disconnected player out of the world when no map channel is going to.
        ///
        /// Close() only flags a departing player; the map channel worker acts on the flag, and
        /// looks for it among the clients on its own map's list. A player on no map's list is
        /// never looked at. A dropship journey is exactly that: the departure takes the client
        /// off its map's list when it sends the Wonkavate, and only MapLoaded puts it on the
        /// arrival map's, keeping the manifestation and its items registered in between. A
        /// connection that dropped on that loading screen - a crash, Alt+F4 - left them all
        /// registered for as long as the server ran.
        ///
        /// Called on the main loop for each connection it drops. A player still registered and
        /// on no map's list or login queue is removed here, the way the worker would have; any
        /// other is left alone, so nobody is removed twice.
        /// </summary>
        /// <summary>
        /// Whether any map still lists a connection of this account, on its client list or its
        /// login queue. A connection that has closed stays on its map's list until the worker has
        /// taken its character out of the world, so this is true for exactly as long as a
        /// character of the account may still be there.
        /// </summary>
        public bool HoldsClientOf(uint accountId)
        {
            foreach (var mapChannel in MapChannelArray.Values)
            {
                foreach (var client in mapChannel.ClientList)
                    if (client?.AccountEntry?.Id == accountId)
                        return true;

                foreach (var client in mapChannel.QueuedClients)
                    if (client?.AccountEntry?.Id == accountId)
                        return true;
            }

            return false;
        }

        public void RemoveStrandedPlayer(Client client)
        {
            var player = client.Player;

            if (player == null)
                return;

            if (!EntityManager.Instance.Players.TryGetValue(player.EntityId, out var registered) || registered != player)
            {
                // Out of the world already - but a map change keeps what they sold to buy back
                // (RemovePlayer), and a connection lost on its loading screen would leave those
                // items registered for good.
                if (player.Inventory.BuybackItems.Count > 0)
                {
                    try
                    {
                        NpcManager.Instance.DiscardBuybackItems(client);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLog(LogType.Error, $"Failed to discard the buyback list of disconnected player {player.FamilyName}: {e}");
                    }
                }

                return;
            }

            foreach (var mapChannel in MapChannelArray.Values)
                if (mapChannel.ClientList.Contains(client) || mapChannel.QueuedClients.Contains(client))
                    return;

            player.RemoveFromMap = false;

            try
            {
                RemovePlayer(client, true);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Failed to remove disconnected player {player.FamilyName}, who was on no map's client list: {e}");
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
