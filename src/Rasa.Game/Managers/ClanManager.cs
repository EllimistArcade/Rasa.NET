using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets;
    using Packets.Clan.Client;
    using Packets.Clan.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Misc;
    using Structures;
    using Structures.Char;
    using Repositories.UnitOfWork;

    public class ClanManager
    {
        /*   Clan Packets:
         * - DisplayClanMessage
         * - InviteToClan
         * - SetClanData
         * - SetClanMemberData
         * - ClanMembersRosterBegin
         * - ClanMembersRosterEnd
         * - PlayerJoinedClan
         * - PlayerLeftClan
         * - ClanDisbanded
         * - ClanDeleted
         * - DisplayClanLeaderInfo
         * - DisplayClanMemberInfoHeader
         * - DisplayClanMemberInfo
         * - ClanCreated
         * - GetPvPClanStatus
         *  
         *   Clan Handlers:
         * - GetPvPClanMembershipStatus
         * - CreateClan
         * - ClanInvitationResponse
         * - InviteToClanByName
         * - InviteToClanById
         * - ClanChangeRankTitle
         * - LeaveClan
         * - DisbandClan
         * - KickPlayerFromClan
         * - RemovePlayer
         */

        #region Singleton

        private static ClanManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        // Matches game client limits
        private readonly uint _minClanNameLength = 3;
        private readonly uint _maxClanNameLength = 20;
        private readonly byte _clankRankLeader = 3;
        private readonly int _requiredCreditsForClanCreation = 10000;

        // Arbitrary limit right now
        public static readonly uint _maxClanMembers = 100;

        public static ClanManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new ClanManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private ClanManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        #endregion

        #region Caching

        public ConcurrentDictionary<uint, Lazy<ClanEntry>> Clans { get; set; } = new ConcurrentDictionary<uint, Lazy<ClanEntry>>();
        
        public ConcurrentDictionary<uint, Lazy<List<ClanMemberEntry>>> ClanMembers { get; set; } = new ConcurrentDictionary<uint, Lazy<List<ClanMemberEntry>>>();

        #endregion

        internal void ClansInit()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            List<ClanEntry> clans = unitOfWork.Clans.GetClans();
            foreach(ClanEntry clan in clans)
            {
                Clans.AddOrUpdate(clan.Id, new Lazy<ClanEntry>(clan), (x, y) => new Lazy<ClanEntry>(clan));

                List<ClanMemberEntry> clanMembers = unitOfWork.ClanMembers.GetAllClanMembersByClanId(clan.Id);
                ClanMembers.AddOrUpdate(clan.Id, new Lazy<List<ClanMemberEntry>>(clanMembers), (x, y) => new Lazy<List<ClanMemberEntry>>(clanMembers));
            }
            InitCurrentClanInventories(unitOfWork.Clans.GetClans());
        }

        internal void InitializePlayerClanData(Client client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            ClanEntry clan = unitOfWork.Clans.GetClanByCharacterId(client.Player.Id);

            if (clan != null)
            {
                var clanData = new ClanData(clan);
                var member = unitOfWork.ClanMembers.GetClanMemberByCharacterId(client.Player.Id);

                RegisterClanMember(member.ClanId, member);

                SetClanData(client, clanData);
                SetClanMemberData(client, clanData);

                SetClanDataForOnlineMembers(clanData.Id, client.Player.Id);
                SetMemberDataForOnlineMembers(clanData.Id, client.Player.Id);                
            }
        }

        internal void InitCurrentClanInventories(List<ClanEntry> clans)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            foreach (ClanEntry clan in clans)
            {
                List<ClanInventoryEntry> getClanInventoryData = unitOfWork.ClanInventories.GetItems(clan.Id);

                foreach (var item in getClanInventoryData)
                {
                    var itemData = unitOfWork.Items.GetItem(item.ItemId);

                    if (itemData == null)
                    {
                        // A lockbox row whose item is gone used to be dereferenced here, at
                        // server start; the row is garbage and is removed.
                        Logger.WriteLog(LogType.Error, $"Clan {clan.Id} lockbox slot {item.SlotId} refers to item {item.ItemId}, which does not exist; row removed.");
                        unitOfWork.ClanInventories.DeleteInvItemByItemId(item.ItemId);
                        continue;
                    }

                    var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemData.ItemTemplateId);

                    // continue, not return: one bad template used to skip every later clan's lockbox.
                    if (itemTemplate == null)
                    {
                        Logger.WriteLog(LogType.Error, $"Item {item.ItemId} has unknown template {itemData.ItemTemplateId}; skipped.");
                        continue;
                    }

                    Item newItem = new Item
                    {
                        OwnerSlotId = item.SlotId,
                        ItemTemplate = itemTemplate,
                        StackSize = itemData.StackSize,
                        CurrentHitPoints = itemData.CurrentHitPoints,
                        Color = itemData.Color,
                        Id = item.ItemId,
                        Crafter = itemData.CrafterName
                    };

                    // check if item is weapon
                    if (newItem.ItemTemplate.WeaponInfo != null)
                        newItem.CurrentAmmo = itemData.AmmoCount;

                    EntityManager.Instance.RegisterEntity(newItem.EntityId, EntityType.Item);
                    EntityManager.Instance.RegisterItem(newItem.EntityId, newItem);
                }
            }
        }

        #region Client Packet Handlers
    
        internal void GetPvPClanMembershipStatus(Client client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            ClanEntry clan = unitOfWork.Clans.GetClanByCharacterId(client.Player.Id);

            string clanName = clan?.Name;
            uint pvpTimeoutSeconds = 0;

            CharacterEntry character = unitOfWork.Characters.Get(client.Player.Id);

            if (character != null)
                pvpTimeoutSeconds = PvPCooldownRemainingSeconds(character);

            client.CallMethod(SysEntity.ClientClanManagerId, new GetPvPClanStatusPacket(clanName, pvpTimeoutSeconds));
        }

        /// <summary>How long a character has to wait before joining or founding a PvP clan.</summary>
        private static readonly TimeSpan PvPClanCooldown = TimeSpan.FromDays(7);

        /// <summary>
        /// Seconds of PvP-clan cooldown left, zero when it has run out. The remaining time is
        /// when the cooldown ends minus now; this used to be computed as now minus the
        /// timestamp (the elapsed time, not the remaining), and in CanCreateClan as a negative
        /// TimeSpan cast to uint, which wrapped to about four billion.
        /// </summary>
        private static uint PvPCooldownRemainingSeconds(CharacterEntry character)
        {
            var remaining = character.LastPvPClan + PvPClanCooldown - DateTime.UtcNow;

            return remaining > TimeSpan.Zero ? (uint)Math.Ceiling(remaining.TotalSeconds) : 0;
        }

        internal void CreateClan(Client client, CreateClanPacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // A character is in one clan at most - clan_member.character_id is unique - and
            // nothing checked here. A member sending CreateClan got as far as the membership
            // insert, which threw on the key: by then the clan row existed and the creation fee
            // had been taken, so the player was 10,000 credits poorer, disconnected, and a clan
            // with no members held the name from the next restart on.
            if (client.Player.ClanId != 0 || unitOfWork.Clans.GetClanByCharacterId(client.Player.Id) != null)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanYouAreAlreadyInAClan, new Dictionary<string, string>()));
                return;
            }

            if (!CanCreateClan(client, packet, client.Player.Id))
                return;

            ClanEntry clan = unitOfWork.Clans.CreateClan(packet.ClanName, packet.IsPvP);

            if (clan == null)
                return;

            // Wrap the database data to what the client expects
            var clanData = new ClanData(clan);

            // Create the member data the client expects 
            // This player created the clan and is the leader
            ClanMemberData clanMemberData = CreateClanMemberData(clanData, client, _clankRankLeader);

            // AddOrUpdate the database with the clan creator as a member of this clan. If this
            // fails the clan row goes with it, so a failed creation leaves nothing behind.
            try
            {
                AddMemberToClan(clanMemberData);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"CreateClan: could not add character {client.Player.Id} as leader of new clan {clan.Id} ({packet.ClanName}); removing the clan: {e}");
                unitOfWork.Clans.DeleteClan(clan.Id);
                return;
            }

            // Pay for the clan creation - only now that there is a clan to pay for. Checked
            // before the row was written, charged after.
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, -_requiredCreditsForClanCreation);

            // Signals the client to set the default rank titles for a clan
            client.CallMethod(SysEntity.ClientClanManagerId, new ClanCreatedPacket(clanData.Id));

            // Send the data packets to the client
            SetClanData(client, clanData);
            SetClanMemberData(client, clanData);

            client.Player.ClanId = clan.Id;

            // Cache the newly created clan
            RegisterClan(clan);
        }

        internal void KickPlayerFromClan(Client client, KickPlayerFromClanPacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            ClanMemberEntry member = GetClanMember(client.Player.ClanId, client.Player.Id);
            ClanMemberEntry memberToBeKicked = GetClanMember(packet.ClanId, packet.CharacterId);
            ClanEntry clan = GetClan(member.ClanId);
            ClanMemberData memberToBeKickedData = CreateClanMemberData(new ClanData(clan), memberToBeKicked.CharacterId, memberToBeKicked.Rank, memberToBeKicked.Note);

            // The leader and the rank below them can kick players
            if (member.Rank >= _clankRankLeader - 1 && client.Player.ClanId == packet.ClanId)
            {
                using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

                if (clan.IsPvP)
                {
                    // Start the 7 day cooldown for the one being kicked. This used to stamp
                    // every member of the clan, so a kick put the whole clan on cooldown.
                    unitOfWork.Clans.UpdateLastPvPClanTime(memberToBeKicked.CharacterId, DateTime.UtcNow);
                }

                if (unitOfWork.ClanMembers.DeleteClanMember(memberToBeKicked))
                {                    
                    UnregisterClanMember(memberToBeKicked);

                    SetMemberDataForOnlineMembers(member.ClanId, packet.CharacterId);

                    CallMethodForOnlineMembers(member.ClanId, (uint)SysEntity.ClientClanManagerId,
                        new PlayerLeftClanPacket(memberToBeKickedData.CharacterId, memberToBeKickedData.CharacterName, memberToBeKickedData.FamilyName, memberToBeKickedData.ClanId, true),
                        packet.CharacterId);

                    // Notfies the kicked client that they were kicked and clears out the clan data
                    // The client expects the characterId to be the entityId for this message
                    var memberClient = Server.Clients.Find(c => c.Player.Id == memberToBeKicked.CharacterId);

                    if (memberClient != null)
                    {
                        memberClient.CallMethod(SysEntity.ClientClanManagerId, new PlayerLeftClanPacket(memberClient.Player.EntityId, memberClient.Player.Name, memberClient.Player.FamilyName, packet.ClanId, true));
                        memberClient.CallMethod(memberClient.Player.EntityId, new ClanIdPacket(0));
                    }
                }
            }
        }

        /// <summary>
        /// /clankick and /kickclan. The clan window kicks by character id; the slash command has
        /// only what the player typed, which in every by-name command here is the family name -
        /// the same thing InviteToClanByName resolves.
        ///
        /// Resolving it is all this adds: the kick itself, and every permission check on it, is
        /// the by-id path, so the two routes cannot drift apart.
        /// </summary>
        internal void KickPlayerFromClanByName(Client client, KickPlayerFromClanByNamePacket packet)
        {
            if (client?.Player == null || packet == null)
                return;

            if (string.IsNullOrWhiteSpace(packet.Name))
                return;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var account = unitOfWork.GameAccounts.Get(packet.Name);

            if (account == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, $"{packet.Name} do not exist");
                return;
            }

            var character = unitOfWork.Characters.GetByAccountId(account.Id, account.SelectedSlot);

            if (character == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, $"{packet.Name} do not exist");
                return;
            }

            // Both ends have to be in the clan before the by-id path runs: it reads the kicker's
            // member row and the target's without checking either, so a stale ClanId on the
            // client - kicked while their window still showed the clan - threw out of the packet
            // handler. The clan window cannot reach that state, which is why it went unnoticed.
            if (GetClanMember(packet.ClanId, client.Player.Id) == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, "You are not in that clan");
                return;
            }

            if (GetClanMember(packet.ClanId, character.Id) == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, $"{packet.Name} is not in your clan");
                return;
            }

            KickPlayerFromClan(client, new KickPlayerFromClanPacket(character.Id, packet.ClanId));
        }

        internal void ClanInvitationResponse(Client client, ClanInvitationResponsePacket packet)
        {
            var invitee = Server.Clients.Find(c => c.Player.EntityId == packet.InvitedCharacterEntityId);

            if (invitee == null)
                return;

            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            // We don't do anything right now when the invitation is declined
            if (!packet.Accepted) return;

            var clan = GetClan(packet.ClanId);

            if (clan == null)
                return;

            // The cooldown applies to joining a PvP clan as well as founding one.
            if (clan.IsPvP)
            {
                using var cooldownUnitOfWork = _gameUnitOfWorkFactory.CreateChar();
                var character = cooldownUnitOfWork.Characters.Get(client.Player.Id);

                if (character != null && PvPCooldownRemainingSeconds(character) > 0)
                {
                    client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanAcceptInPvpTimeout, new Dictionary<string, string>()));
                    return;
                }
            }

            var clanData = new ClanData(clan);
            ClanMemberData memberData = CreateClanMemberData(clanData, invitee);

            // The player accepted so they must be online
            memberData.IsOnline = true;

            // AddOrUpdate the database for the invitee to be in the clan
            AddMemberToClan(memberData);

            // Membership changed, update all clan members game clients
            // Include a message for the player joined message to update the clan chat
            SetMemberDataForOnlineMembers(packet.ClanId, invitee.Player.Id);

            CallMethodForOnlineMembers(packet.ClanId, (uint)SysEntity.ClientClanManagerId,
                new PlayerJoinedClanPacket(SetClanMemberDataPacket.NameKey, memberData),
                invitee.Player.Id);

            // AddOrUpdate the joined players clan window
            SetClanData(client, clanData);
            SetClanMemberData(client, clanData);

            client.Player.ClanId = clanData.Id;

            //update clan inventory from db
            InventoryManager.Instance.SetupLocalClanInventory(client);
        }

        internal void CleanupClan(Client client)
        {
            for (int i = 0; i < 500; i++)
                client.Player.Inventory.ClanInventory[i] = 0;

            client.Player.ClanId = 0;
        }

        internal void InviteToClanByName(Client client, InviteToClanByNamePacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            ClanEntry inviterClan = unitOfWork.Clans.GetClanByCharacterId(client.Player.Id);
            List<ClanMemberEntry> members = unitOfWork.ClanMembers.GetAllClanMembersByClanId(inviterClan.Id);
            GameAccountEntry inviteeAccount = unitOfWork.GameAccounts.Get(packet.FamilyName);

            if (inviteeAccount == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, $"{packet.FamilyName} do not exist");
                return;
            }

            CharacterEntry inviteeCharacter = unitOfWork.Characters.GetByAccountId(inviteeAccount.Id, inviteeAccount.SelectedSlot);

            // An account whose selected slot holds no character: every character deleted, or a
            // fresh account whose family name happens to match.
            if (inviteeCharacter == null)
            {
                CommunicatorManager.Instance.SystemMessage(client, $"{packet.FamilyName} has no character to invite");
                return;
            }

            var messageArgs = CreatePlayerMessageArgs("playername", $"{inviteeCharacter.Name} {inviteeAccount.FamilyName}");

            if (members.Count >= _maxClanMembers)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanInviteError, messageArgs));
                return;
            }

            if (inviteeAccount != null)
            {                
                ClanEntry existingClan = unitOfWork.Clans.GetClanByCharacterId(inviteeCharacter.Id);

                // Invitee is not already in a clan
                if(existingClan != null)
                {
                    client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanPlayerAlreadyInAClan, messageArgs));
                    return;
                }

                if (inviteeCharacter != null)
                {
                    // Make sure the invitee is online
                    var invitee = Server.Clients.Find(c => c.Player.Id == inviteeCharacter.Id);

                    if (invitee != null)
                    {
                        SendInviteToCharacter(client, invitee.Player.EntityId, inviterClan);
                    }
                }
            }
        }        

        internal void InviteToClanById(Client client, InviteToClanByIdPacket packet)
        {
            var invitee = Server.Clients.Find(c => c.Player.EntityId == packet.CharacterEntityId);

            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            if (invitee == null)
                return;
            
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            ClanEntry inviterClan = unitOfWork.Clans.GetClanByCharacterId(client.Player.Id);
            ClanEntry existingClan = unitOfWork.Clans.GetClanByCharacterId(invitee.Player.Id);

            var messageArgs = CreatePlayerMessageArgs("playername", $"{invitee.Player.Name} {invitee.Player.FamilyName}");

            // Invitee is not already in a clan
            if (existingClan != null)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanPlayerAlreadyInAClan, messageArgs));
                return;
            }

            List<ClanMemberEntry> members = unitOfWork.ClanMembers.GetAllClanMembersByClanId(inviterClan.Id);

            if(members.Count >= _maxClanMembers)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanInviteError, messageArgs));
                return;
            }
            
            SendInviteToCharacter(client, packet.CharacterEntityId, inviterClan);
        }

        internal void ClanChangeRankTitle(Client client, ClanChangeRankTitlePacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            ClanEntry clan = unitOfWork.Clans.GetClanByCharacterId(client.Player.Id);
            List<ClanMemberEntry> allMembers = GetClanMembers(clan.Id);
            ClanMemberEntry clanLeader = allMembers.FirstOrDefault(x => x.Rank == _clankRankLeader);

            // Only the clan leader can change ranks
            if (clanLeader != null && clanLeader.CharacterId == client.Player.Id)
            {
                if(unitOfWork.Clans.UpdateRankTitleByClanId(clan.Id, packet.Rank, packet.Title))
                {   
                    // Get the clan now that the rank title is updated
                    ClanEntry updatedClan = unitOfWork.Clans.GetClanById(clan.Id);

                    RegisterClan(updatedClan);

                    SetClanDataForOnlineMembers(updatedClan.Id);
                }
            }
            else
            {
                Logger.WriteLog(LogType.Error, $"ClanManager: Character ID {client.Player.Id} attempted to change rank title but is not the leader.");
            }
        }

        internal void LeaveClan(Client client, LeaveClanPacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            ClanMemberEntry member = GetClanMember(packet.ClanId, client.Player.Id);
            ClanEntry clan = GetClan(packet.ClanId);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (clan.IsPvP)
            {
                // Start the 7 day cooldown for the one leaving, not for the clan they leave.
                unitOfWork.Clans.UpdateLastPvPClanTime(member.CharacterId, DateTime.UtcNow);
            }

            if (unitOfWork.ClanMembers.DeleteClanMember(member))
            {                
                UnregisterClanMember(member);

                // Notifies other players still in the clan that we left
                CallMethodForOnlineMembers(packet.ClanId, (uint)SysEntity.ClientClanManagerId,
                    new PlayerLeftClanPacket(client.Player.Id, client.Player.Name, client.Player.FamilyName, packet.ClanId, false),
                    client.Player.Id);

                // Notfies the leavers client that we succesfully left and clears out the clan data
                // The client expects the characterId to be the entityId for this message
                client.CallMethod(SysEntity.ClientClanManagerId, new PlayerLeftClanPacket(client.Player.EntityId, client.Player.Name, client.Player.FamilyName, packet.ClanId, false));
                client.CallMethod(client.Player.EntityId, new ClanIdPacket(0));
            }
        }

        internal void DisbandClan(Client client, DisbandClanPacket packet)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            ClanEntry clan = GetClan(packet.ClanId);
            ClanMemberEntry member = GetClanMember(clan.Id, client.Player.Id);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if(member.Rank == _clankRankLeader)
            {
                if (clan.IsPvP)
                {
                    // Save the time they were last in a PvP clan to start the 7 day cooldown.
                    // Prevents PvP clans from disbanding and creating another clan to workaround the cooldown.
                    unitOfWork.Clans.UpdateLastPvPClanTimeForMembers(clan.Id, DateTime.UtcNow);
                }

                List<ClanMemberEntry> members = GetClanMembers(clan.Id);                

                foreach (ClanMemberEntry m in members)
                {
                    var memberClient = Server.Clients.Find(c => c.Player.Id == m.CharacterId);

                    if (memberClient != null)
                    {
                        // 0 Clears the overhead frame next to the player name
                        memberClient.CallMethod(memberClient.Player.EntityId, new ClanIdPacket(0));

                        // Shows a message in the players chat and updates the clan UI
                        memberClient.CallMethod(SysEntity.ClientClanManagerId, new ClanDisbandedPacket(clan.Id));
                    }
                }

                unitOfWork.ClanMembers.DeleteClanMembers(clan.Id);
                unitOfWork.Clans.DeleteClan(packet.ClanId);

                UnregisterClan(clan);
                UnregisterClanMembers(clan.Id);

                //TODO: Clear clan lockbox db inventory?
            }
        }

        internal void RemovePlayer(Client client)
        {
            var clanId = client.Player.ClanId;
            if (clanId > 0)
            {
                ClanMemberEntry member = GetClanMember(clanId, client.Player.Id);
                UnregisterClanMember(member);

                SetMemberDataForOnlineMembers(clanId, client.Player.Id);
            }
        }


        internal void ClanPromotePlayer(Client client, ClanPromotePlayerPacket packet)
        {
            ClanMemberEntry member = GetClanMember(client.Player.ClanId, packet.CharacterId);

            if (member?.Rank < _clankRankLeader)
            {
                UpdateClanMemberRank(member, (byte)(member.Rank + 1));
            }
        }

        internal void ClanDemotePlayer(Client client, ClanDemotePlayerPacket packet)
        {
            ClanMemberEntry member = GetClanMember(client.Player.ClanId, packet.CharacterId);

            if (member?.Rank - 1 >= 0)
            {
                UpdateClanMemberRank(member, (byte)(member.Rank - 1));
            }
        }

        internal void MakePlayerClanLeader(Client client, MakePlayerClanLeaderPacket packet)
        {
            ClanMemberEntry leader = GetClanMembers(client.Player.ClanId).FirstOrDefault(x => x.Rank == _clankRankLeader);

            // Only the leader can assign a new leader
            if(leader?.CharacterId == client.Player.Id)
            {
                ClanMemberEntry member = GetClanMember(client.Player.ClanId, packet.CharacterId);
                UpdateClanLeader(member, leader);                
            }
        }

        #endregion

        #region Helper Functions

        private ClanMemberData CreateClanMemberData(ClanData clanData, uint characterId, byte rank = 0, string note = "")
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            CharacterEntry character = unitOfWork.Characters.Get(characterId);
            GameAccountEntry account = unitOfWork.GameAccounts.Get(character.AccountId);

            return new ClanMemberData
            {               
                CharacterId = character.Id,
                ContextId = character.MapContextId,
                Level = character.Level,
                CharacterName = character.Name,
                FamilyName = account.FamilyName,
                UserId = account.Id,
                ClanId = clanData.Id,
                Rank = rank,
                Note = note,
            };
        }

        private ClanMemberData CreateClanMemberData(ClanData clanData, Client client, byte rank = 0, string note = "")
        {
            return new ClanMemberData
            {
                CharacterEntityId = client.Player.EntityId,
                CharacterId = client.Player.Id,
                ContextId = client.Player.MapContextId,
                Level = client.Player.Level,
                CharacterName = client.Player.Name,
                FamilyName = client.Player.FamilyName,
                UserId = client.AccountEntry.Id,
                ClanId = clanData.Id,
                Rank = rank,
                Note = note,
            };
        }

        private void SetClanMemberData(Client client, ClanData clanData)
        {
            Manifestation player = client.Player;

            client.CallMethod(SysEntity.ClientClanManagerId, new ClanMembersRosterBeginPacket(clanData.Id));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            List<ClanMemberEntry> clanMemberEntries = unitOfWork.ClanMembers.GetAllClanMembersByClanId(clanData.Id);

            foreach (ClanMemberEntry member in clanMemberEntries)
            {
                ClanMemberData clanMemberData = CreateClanMemberData(clanData, member.CharacterId, member.Rank, member.Note);
                clanMemberData.IsOnline = Server.Clients.Contains(Server.Clients.Find(c => c.Player.Id == member.CharacterId));
                
                if (player.Id == member.CharacterId)
                {
                    // The game is expecting the characterId to be the manifestationId for the current player                    
                    clanMemberData.CharacterEntityId = player.EntityId;
                    clanMemberData.CharacterId = player.Id;
                }

                client.CallMethod(SysEntity.ClientClanManagerId, new SetClanMemberDataPacket(SetClanMemberDataPacket.NameKey, clanMemberData));
            }

            client.CallMethod(SysEntity.ClientClanManagerId, new ClanMembersRosterEndPacket(clanData.Id));
        }
        private void SetClanData(Client client, ClanData clanData)
        {
            Manifestation player = client.Player;            
            client.CallMethod(SysEntity.ClientClanManagerId, new SetClanDataPacket(SetClanDataPacket.NameKey, clanData));
            client.CallMethod(player.EntityId, new ClanIdPacket(clanData.Id));
        }

        private void AddMemberToClan(ClanMemberData clanMember)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.ClanMembers.InsertClanMemberData(clanMember.ClanId, clanMember.CharacterId, clanMember.Rank, clanMember.Note);            

            // AddOrUpdate the cache with the newly added member from the database
            List<ClanMemberEntry> members = unitOfWork.ClanMembers.GetAllClanMembersByClanId(clanMember.ClanId);
            RegisterClanMember(clanMember.ClanId, members.FirstOrDefault(x => x.CharacterId == clanMember.CharacterId));
        }

        private void SendInviteToCharacter(Client client, ulong characterEntityId, ClanEntry clan)
        {
            Client inviteeClient = Server.Clients.Find(c => c.Player.EntityId == characterEntityId);

            var inviteData = new ClanInviteData
            {
                // Sending the full name here because it looks better on the invitation prompt in-game
                // "Name Familyname invited you to join ClanType ClanName"
                InviterFamilyName = $"{client.Player.Name} {client.Player.FamilyName}",
                ClanId = clan.Id,
                ClanName = clan.Name,
                IsPvP = clan.IsPvP,
                InvitedCharacterEntityId = characterEntityId
            };

            inviteeClient.CallMethod(SysEntity.ClientClanManagerId, new InviteToClanPacket(InviteToClanPacket.NameKey, inviteData));
        }

        private bool CanCreateClan(Client client, CreateClanPacket packet, uint characterId)
        {
            if (packet.ClanName.Length > _maxClanNameLength)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanNameTooLong, new Dictionary<string, string>()));
                return false;
            }

            if (packet.ClanName.Length < _minClanNameLength)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanNameRequired, new Dictionary<string, string>()));
                return false;
            }

            if (ClanNameExists(packet.ClanName))
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanNameNotAvailable, 
                    new Dictionary<string, string>() { { "clanname", packet.ClanName } }));
                return false;
            }

            if(client.Player.Credits[CurencyType.Credits] < _requiredCreditsForClanCreation)
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmInsufficientFundsToCreateClan, new Dictionary<string, string>()));
                return false;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // Verify content of clan name: PmInappropriateClanName
            List<string> censoredWords = unitOfWork.CensoredWords.GetCensoredWords();
            var censor = new Censor(censoredWords);

            if(censor.ContainsProfanity(packet.ClanName))
            {
                client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmInappropriateClanName, new Dictionary<string, string>()));
                return false;
            }

            if (packet.IsPvP)
            {
                CharacterEntry character = unitOfWork.Characters.Get(characterId);

                // Verify the creator is not on PvP timeout: PmClanCannotCreateUserInPvpTimeout
                if (character != null && PvPCooldownRemainingSeconds(character) > 0)
                {
                    client.CallMethod(SysEntity.ClientClanManagerId, new DisplayClanMessagePacket((int)PlayerMessage.PmClanCannotCreateUserInPvpTimeout, new Dictionary<string, string>()));
                    return false;
                }
            }

            // The fee is taken by CreateClan once the clan and its leader both exist.
            return true;
        }        

        private bool ClanNameExists(string clanName)
        {
            if (string.IsNullOrEmpty(clanName))
                throw new ArgumentNullException(nameof(clanName));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            return unitOfWork.Clans.GetClanByName(clanName) != null;
        }

        private void SetMemberDataForOnlineMembers(uint clanId, uint skipCharacterId = 0)
        {
            var clanData = new ClanData(Clans.GetValueOrDefault(clanId).Value);

            CallMethodForOnlineMembers(clanId, (client) => SetClanMemberData(client, clanData), skipCharacterId);
        }

        private void SetClanDataForOnlineMembers(uint clanId, uint skipCharacterId = 0)
        {
            var clanData = new ClanData(Clans.GetValueOrDefault(clanId).Value);

            CallMethodForOnlineMembers(clanId, (client) => SetClanData(client, clanData), skipCharacterId);
        }

        public void CallMethodForOnlineMembers(uint clanId, ulong entityId, ServerPythonPacket packet, uint skipCharacterId = 0, uint onlyThisCharacterId = 0)
        {
            foreach (ClanMemberEntry member in GetClanMembers(clanId))
            {
                // If the member is online get their cached client
                var memberClient = Server.Clients.Find(c => c.Player.Id == member.CharacterId);

                if (memberClient != null)
                {
                    // Don't send a message to this player
                    if ((skipCharacterId != 0 && skipCharacterId == memberClient.Player.Id) ||
                        onlyThisCharacterId != 0 && onlyThisCharacterId != memberClient.Player.Id)
                        continue;

                    memberClient.CallMethod(entityId, packet);
                }
            }
        }

        public void CallMethodForOnlineMembers(uint clanId, Action<Client> methodToCall, uint skipCharacterId = 0, uint onlyThisCharacterId = 0)
        {
            foreach (ClanMemberEntry member in GetClanMembers(clanId))
            {
                // If the member is online get their cached client
                var memberClient = Server.Clients.Find(c => c.Player.Id == member.CharacterId);

                if (memberClient != null)
                {
                    // Don't send a message to this player
                    if ((skipCharacterId != 0 && skipCharacterId == memberClient.Player.Id) ||
                        onlyThisCharacterId != 0 && onlyThisCharacterId != memberClient.Player.Id)
                        continue;

                    methodToCall.Invoke(memberClient);
                }
            }
        }

        private Dictionary<string, string> CreatePlayerMessageArgs(string key, string value)
        {
            return new Dictionary<string, string>
            {
                { key, value }
            };
        }

        private void UpdateClanMemberRank(ClanMemberEntry member, byte newRank)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.ClanMembers.UpdateRankByCharacterId(newRank, member.CharacterId);
            CharacterEntry character = unitOfWork.Characters.Get(member.CharacterId);
            GameAccountEntry account = unitOfWork.GameAccounts.Get(member.CharacterId);

            // AddOrUpdate the game clients 
            SetMemberDataForOnlineMembers(member.ClanId);

            string newRankTitle = GetRankTitleForRank(member.ClanId, newRank);

            var messageArgs = new Dictionary<string, string>
            {
                { "firstname", character.Name },
                { "lastname", account.FamilyName },
                { "rankname", newRankTitle },
            };

            if (newRank > member.Rank)
            {
                CallMethodForOnlineMembers(member.ClanId, (uint)SysEntity.ClientClanManagerId,
                    new DisplayClanMessagePacket((int)PlayerMessage.PmClanPlayerPromoted, messageArgs));
            }
            else
            {
                CallMethodForOnlineMembers(member.ClanId, (uint)SysEntity.ClientClanManagerId,
                    new DisplayClanMessagePacket((int)PlayerMessage.PmClanPlayerDemoted, messageArgs));
            }

            // Refresh the cached member
            member.Rank = (byte)newRank;
            RegisterClanMember(member.ClanId, member);
        }

        private void UpdateClanLeader(ClanMemberEntry member, ClanMemberEntry leaderMember)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.ClanMembers.UpdateRankByCharacterId(_clankRankLeader, member.CharacterId);
            unitOfWork.ClanMembers.UpdateRankByCharacterId((byte)(_clankRankLeader - 1), leaderMember.CharacterId);
            CharacterEntry memberCharacter = unitOfWork.Characters.Get(member.CharacterId);
            GameAccountEntry account = unitOfWork.GameAccounts.Get(member.CharacterId);

            // AddOrUpdate the game clients 
            SetMemberDataForOnlineMembers(member.ClanId);

            var messageArgs = new Dictionary<string, string>
            {
                { "leadername", $"{memberCharacter.Name} {account.FamilyName}" },
                { "clanname", GetClan(member.ClanId).Name },
            };

            CallMethodForOnlineMembers(member.ClanId, (uint)SysEntity.ClientClanManagerId,
                new DisplayClanMessagePacket((int)PlayerMessage.PmClanNewLeader, messageArgs));

            // Refresh the cached member
            member.Rank = _clankRankLeader;
            RegisterClanMember(member.ClanId, member);

            // Refresh the old leader
            leaderMember.Rank = (byte)(_clankRankLeader - 1);
            RegisterClanMember(leaderMember.ClanId, leaderMember);
        }

        private string GetRankTitleForRank(uint clanId, uint newRank)
        {
            ClanEntry clan = GetClan(clanId);

            switch(newRank)
            {
                case 0:
                    return clan.RankTitle0;
                case 1:
                    return clan.RankTitle1;
                case 2:
                    return clan.RankTitle2;
                case 3:
                    return clan.RankTitle3;
                default:
                    return clan.RankTitle0;
            }
        }

        #endregion

        #region Caching Helpers

        private void RegisterClan(ClanEntry clan)
        {
            Clans.AddOrUpdate(clan.Id, new Lazy<ClanEntry>(clan), (id, oldClan) => new Lazy<ClanEntry>(clan));
        }

        private void UnregisterClan(ClanEntry clan)
        {
            CallMethodForOnlineMembers(clan.Id, (client) => CleanupClan(client));
            Clans.Remove(clan.Id, out _);
        }

        private ClanEntry GetClan(uint clanId)
        {
            Lazy<ClanEntry> clan = Clans.GetValueOrDefault(clanId);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            return clan.Value ?? unitOfWork.Clans.GetClanById(clanId);
        }        

        private void RegisterClanMember(uint clanId, ClanMemberEntry member)
        {
            List<ClanMemberEntry> members = ClanMembers.GetValueOrDefault(clanId)?.Value;
            ClanMemberEntry existingMember = members?.FirstOrDefault(x => x.CharacterId == member.CharacterId);

            if (existingMember != null)
            {
                members[members.IndexOf(existingMember)] = member;
            }
            else
            {
                if (members == null)
                    members = new List<ClanMemberEntry>();

                members.Add(member);
            }

            ClanMembers.AddOrUpdate(clanId, new Lazy<List<ClanMemberEntry>>(members), (x, y) => new Lazy<List<ClanMemberEntry>>(members));
        }

        private void UnregisterClanMember(ClanMemberEntry member)
        {
            List<ClanMemberEntry> members = ClanMembers.GetValueOrDefault(member?.ClanId ?? 0).Value;
            ClanMemberEntry existingMember = members?.FirstOrDefault(x => x.CharacterId == member?.CharacterId);

            if (existingMember != null)
            {
                CallMethodForOnlineMembers(member.ClanId, (client) => CleanupClan(client), 0, member.CharacterId);
                members.Remove(existingMember);
                ClanMembers.AddOrUpdate(member.ClanId, new Lazy<List<ClanMemberEntry>>(members), (x, y) => new Lazy<List<ClanMemberEntry>>(members));
            }
        }
        
        private void UnregisterClanMembers(uint clanId)
        {
            _ = ClanMembers.Remove(clanId, out _);        
        }

        private List<ClanMemberEntry> GetClanMembers(uint clanId)
        {
            Lazy<List<ClanMemberEntry>> clanMembers = ClanMembers.GetValueOrDefault(clanId);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            return clanMembers.Value ?? unitOfWork.ClanMembers.GetAllClanMembersByClanId(clanId);
        }

        public ClanMemberEntry GetClanMember(uint clanId, uint characterId)
        {
            Lazy<List<ClanMemberEntry>> clanMembers = ClanMembers.GetValueOrDefault(clanId);

            return clanMembers?.Value?.FirstOrDefault(x => x.CharacterId == characterId);
        }

        #endregion
    }
}
