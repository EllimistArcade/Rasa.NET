using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Communicator.Server;
    using Packets.Inventory.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Packets.Mission.Server;
    using Repositories.Char;
    using Repositories.UnitOfWork;
    using Structures;

    public class NpcManager
    {
        /* NPC augmentation. Supports dialog, mission manipulation, and vending
         * 
         *  - NPCInfo
         *  - NPCConversationStatus
         *  - Converse
         *  - Train                     => not used by client
         */

        private static NpcManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public static NpcManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new NpcManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private NpcManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        #region NPC

        public void NpcInit()
        {

        }

        public void AssignNPCMission(Client client, AssignNPCMissionPacket packet)
        {
            var mission = MissionManager.Instance.LoadedMissions[packet.MissionId];

            if (client.Player.Missions.Count > 30)
            {
                CommunicatorManager.Instance.SystemMessage(client, "Mission log is full.");
                return;
            }

            client.CallMethod(client.Player.EntityId, new MissionGainedPacket(packet.MissionId, mission));
        }

        public void CompleteNPCMission(Client client, CompleteNPCMissionPacket packet)
        {
            Logger.WriteLog(LogType.Debug, $"ToDo: CompleteNPCMission");
        }

        public void RequestNpcConverse(Client client, RequestNPCConversePacket packet)
        {
            var creature = EntityManager.Instance.GetCreature(packet.EntityId);

            if (creature == null)
                return;

            // ToDo create DB structures, and replace constant data with dinamic

            var convoDataDict = new Dictionary<ConversationType, object>();

            if (creature.Npc.Vendor != null)
                convoDataDict.Add(ConversationType.Vending, new List<uint> { creature.Npc.Vendor.VendorPackageId });

            if (creature.Npc.NpcMissionIds != null)
                if (creature.Npc.NpcMissionIds.Count > 0)
                {
                    var dispensableMissions = new Dictionary<uint, MissionInfo>();
                    var completeableMissions = new Dictionary<uint, RewardInfo>();
                    var completeableObjectives = new List<CompleteableObjectives>();

                    foreach (var missionId in creature.Npc.NpcMissionIds)
                    {
                        var mission = MissionManager.Instance.LoadedMissions[missionId];

                        if (mission.MissionGiver == creature.DbId)
                            dispensableMissions.Add(mission.MissionId, mission);

                        if (mission.MissionReciver == creature.DbId)
                            completeableMissions.Add(mission.MissionId, mission.MissionConstantData.RewardInfo);
                    }

                    // insert data into convoDataDict
                    if (dispensableMissions.Count > 0)
                        convoDataDict.Add(ConversationType.MissionDispense, dispensableMissions);

                    if (completeableMissions.Count > 0)
                        convoDataDict.Add(ConversationType.MissionComplete, completeableMissions);

                    if (completeableObjectives.Count > 0)
                        convoDataDict.Add(ConversationType.ObjectiveChoice, completeableObjectives);
                }

            // Auctioner = 14
            if (creature.Npc.NpcIsAuctioneer)
                convoDataDict.Add(ConversationType.Auctioneer, true);

            if (creature.Npc.NpcIsClanMaster)
                convoDataDict.Add(ConversationType.Clan, true);

            /*
            // Greeting = 0
            var greetingId = 19;

            // ForceTopic = 1
            var forceTopicType = new ForceTopic(ConversationType.MissionReward, 429);

            // DispensableMissions = 2
            var missionId = 429;
            var missionLevel = 1;
            var groupType = 1;
            var credits = new List<Curency>
            {
                new Curency(CurencyType.Credits, 200),
                new Curency(CurencyType.Prestige, 100)
            };
            var fixedItems = new List<RewardItem>
            {
                new RewardItem(145, 27120, 1, new List<int>{900620 }, 2),
                new RewardItem(145, 27120, 1, new List<int>{900007 }, 2)
            };
            var selectableRewards = new List<RewardItem>
            {
                new RewardItem(28, 3147, 20, new List<int>(), 1),
                new RewardItem(28, 3147, 50, new List<int>(), 1)
            };
            var fixedReward = new FixedReward(credits, fixedItems);
            var selectableReward = new List<RewardItem>(selectableRewards);
            var rewardInfo = new RewardInfo(fixedReward, selectableReward);
            var missionObjectives = new List<MissionObjectives> { new MissionObjectives(4), new MissionObjectives(5) };
            var itemRequired = new List<RewardItem> { new RewardItem(26544) };
            var missionInfo = new MissionInfo(missionLevel, rewardInfo, missionObjectives, itemRequired, groupType);
            var dispensableMissions = new List<DispensableMissions>
            {
                new DispensableMissions(missionId, missionInfo),
                new DispensableMissions(298, missionInfo)
            };

            // CompletableMissions = 3
            var completeableMissions = new List<CompleteableMissions>
            {
                new CompleteableMissions(missionId, rewardInfo),
                new CompleteableMissions(298, rewardInfo)
            };

            // MissionReminder = 4
            var remindableMissions = new List<int> { 298, 429, 430 };

            // ObjectiveAmbient = 5
            var ambientObjectives = new List<AmbientObjectives>
            {
                new AmbientObjectives(missionId, 5, 1),
                new AmbientObjectives(missionId, 4, 1)
            };

            // ObjectiveComplete = 6
            var objectiveComplete = new List<CompleteableObjectives>
            {
                new CompleteableObjectives(missionId, 5, 1),
                new CompleteableObjectives(missionId, 4, 1)
            };

            // RewardableMission = 7 (mission without objectives ???)
            var revardableMissions = new List<RewardableMissions>
            {
                new RewardableMissions(298, rewardInfo),
                new RewardableMissions(missionId, rewardInfo),
            };

            // ObjectiveChoice = 8,
            // EndConversation = 9,
            // Training = 10,
            var training = new TrainingConverse(true, 1);
            // Vending = 11,
            var vendor = new ConvoDataDict
            {
                VendorConverse = new List<int> { 142 }
            };
            // ImportantGreering = 12,
            // Clan = 13,
            // Auctioner = 14,
            var auctioneer = new ConvoDataDict
            {
                IsAuctioneer = true
            };
            // ForcedByScript = 15

            var testConvoDataDict = new Dictionary<ConversationType, ConvoDataDict>
            {
                { ConversationType.MissionDispense, new ConvoDataDict(dispensableMissions) },
                { ConversationType.ObjectiveComplete, new ConvoDataDict(objectiveComplete) },
                { ConversationType.MissionComplete, new ConvoDataDict(completeableMissions) },
                //{ ConversationType.MissionReward, new ConvoDataDict(revardableMissions) },
                { ConversationType.Greeting, new ConvoDataDict(greetingId) },
                //{ ConversationType.MissionReminder, new ConvoDataDict(remindableMissions) },
                //{ ConversationType.ObjectiveAmbient, new ConvoDataDict(ambientObjectives) },
                //{ ConversationType.Training, new ConvoDataDict(training) }
                { ConversationType.Auctioneer, auctioneer },
                { ConversationType.Vending, vendor }
            };
            */

            client.CallMethod(creature.EntityId, new ConversePacket(convoDataDict));
        }

        public void UpdateConversationStatus(Client client, Creature creature)
        {
            var npc = creature.Npc;
            var vendor = creature.Npc.Vendor;
            var statusSet = false;

            /* ToDo
             * implement Player=>MissionStatus
             * npc missions shold be checked with player mission status
             */

            if (npc.NpcMissionIds != null)
            {
                var availableMissions = new List<uint>();
                var completeMission = new List<uint>();

                foreach (var missionId in npc.NpcMissionIds)
                {
                    var mission = MissionManager.Instance.LoadedMissions[missionId];

                    if (mission.MissionReciver == creature.DbId)
                        completeMission.Add(missionId);

                    if (mission.MissionGiver == creature.DbId)
                        availableMissions.Add(missionId);
                }

                // if we have completable mission send it, else send available missions
                if (completeMission.Count > 0)
                    client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.ObjectivComplete, completeMission));  // complete mission
                else
                    client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.Available, availableMissions));       // available missions

                statusSet = true;
            }

            /*
            foreach (var entry in npcData.RelatedMissions)
            {
                var missionLogEntry = MissionManager.Instance.FindPlayerMission(client, entry.MissionIndex);
                var mission = MissionManager.Instance.GetById(missionLogEntry.MissionIndex);

                if (missionLogEntry != null)
                {
                    if (mission == null)
                        continue;

                    if (missionLogEntry.State >= mission.StateCount)
                        continue;

                    // search for objective or mission related updates
                    var scriptlineStart = mission.StateMapping[missionLogEntry.State];
                    var scriptlineEnd = mission.StateMapping[missionLogEntry.State + 1];

                    for (var i = scriptlineStart; i < scriptlineEnd; i++)
                    {
                        var scriptline = mission.ScriptLines[i];

                        if (scriptline.Command == MissionScriptCommand.CompleteObjective)
                        {
                            if (creature.DbId == scriptline.Value1) // same NPC?
                            {
                                // objective already completed?
                                if (missionLogEntry.MissionData[scriptline.StorageIndex] == 1)
                                    continue;

                                // send objective completable flag
                                client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.ObjectivComplete, new List<int> { })); // status - complete objective

                                statusSet = true;

                                break;
                            }
                            else if (scriptline.Command == MissionScriptCommand.Collector)
                            {
                                if (creature.DbId == scriptline.Value1) // same NPC?
                                {
                                    // mission already completed?
                                    if (missionLogEntry.State != (mission.StateCount - 1))
                                        continue;

                                    // send mission completable flag
                                    client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.MissionComplete, new List<int> { })); // status - complete objective

                                    statusSet = true;

                                    break;
                                }
                            }
                        }
                    }
                }
                else if (MissionManager.Instance.IsCompletedByPlayer(client, mission.MissionIndex) == false)
                {
                    // check if the npc is actually the mission dispenser and not only a objective related npc
                    if (MissionManager.Instance.IsCreatureMissionDispenser(MissionManager.Instance.GetByIndex(mission.MissionIndex), creature))
                    {
                        // mission available overwrites any other converse state
                        client.SendPacket(creature.Actor.EntityId, new NPCConversationStatusPacket(ConversationStatus.Available, new List<int> { })); // status - available

                        statusSet = true;

                        break;
                    }
                }
            }*/

            // is NPC vendor?
            if (vendor != null && statusSet == false)
            {
                // creature->npcData.isVendor
                client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.Vending, new List<uint> { vendor.VendorPackageId })); // status - vending
                statusSet = true;
            }

            // is NPC auctioner?
            if (creature.Npc.NpcIsAuctioneer && statusSet == false)
            {
                client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.Auctioneer, new List<uint>())); // status - none
                statusSet = true;
            }

            // is NPC clan master?
            if (creature.Npc.NpcIsClanMaster && statusSet == false)
            {
                client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.Clan, new List<uint>())); // status - none
                statusSet = true;
            }

            // no status set yet? Send NONE conversation status
            if (statusSet == false)
            {
                // no other status, set NONE status
                client.CallMethod(creature.EntityId, new NPCConversationStatusPacket(ConversationStatus.None, new List<uint>()));// status - none

                statusSet = true;
            }
        }
        #endregion

        #region Auctioneer
        public void RequestNPCOpenAuctionHouse(Client client, ulong entityId)
        {
            client.CallMethod(entityId, new OpenAuctionHousePacket());
        }
        #endregion

        #region Vendor
        /// <summary>
        /// Opens a vendor's stock for one player.
        ///
        /// The stock itself is made once and shared by everyone: the items are registered
        /// globally against the vendor's entity id and stay there for the life of the process.
        /// What is *not* shared is the client's knowledge of them. A client can only draw an
        /// item it has been sent - vendorwindow does GetEntity(itemId) per row and falls back to
        /// the literal string "Unknown entity" - and the Vend packet carries only ids and prices,
        /// not the items.
        ///
        /// So the item data has to go to whoever opens the vendor, every time. It used to be
        /// sent from inside CreateVendorItem, which runs only when the stock is first made:
        /// the first player to open that vendor after a restart saw it correctly and every other
        /// player saw a list of "Unknown entity" rows. The same happened to that first player
        /// once they relogged, since a client drops its entities when it leaves the world.
        /// </summary>
        public void RequestNPCVending(Client client, RequestNPCVendingPacket packet)
        {
            // Any entity id can arrive here, and Creatures was indexed directly:
            // KeyNotFoundException in the handler, which closes the connection.
            var creature = EntityManager.Instance.GetCreature(packet.EntityId);

            if (creature?.Npc?.Vendor?.VendorItems == null)
                return;

            if (!EntityManager.Instance.VendorItems.TryGetValue(packet.EntityId, out var entityList))
            {
                entityList = new List<ulong>();

                foreach (var itemTemplateId in creature.Npc.Vendor.VendorItems)
                {
                    var created = ItemManager.Instance.CreateVendorItem(itemTemplateId);

                    if (created == null)
                    {
                        Logger.WriteLog(LogType.Error,
                            $"Vendor {packet.EntityId} stocks item template {itemTemplateId}, which does not exist.");
                        continue;
                    }

                    entityList.Add(created.EntityId);
                }

                EntityManager.Instance.RegisterVendorItem(packet.EntityId, entityList);
            }

            var itemList = new List<Item>();

            foreach (var entityId in entityList)
            {
                var item = EntityManager.Instance.GetItem(entityId);

                // Registered once but freed since. Skip it: a null in the list is a
                // NullReferenceException inside VendPacket.Write, on the main loop.
                if (item == null)
                    continue;

                ItemManager.Instance.SendItemDataToClient(client, item, false);
                itemList.Add(item);
            }

            client.CallMethod(packet.EntityId, new VendPacket(itemList));
        }

        public void RequestCancelVendor(Client client, ulong entityId)
        {
            Logger.WriteLog(LogType.Debug, $"RequestCancelVendor => ToDo"); // ToDo
        }

        public void RequestVendorBuyback(Client client, RequestVendorBuybackPacket packet)
        {
            // Only what this player sold this session can come back. The id used to be taken
            // as given: any registered item - one already in the inventory, one shown to this
            // account by another character, another player's - was added to the inventory at
            // its sell price. Adding an item that was already there merged it with itself and
            // wrote the doubled stack to the row, then inserted a second inventory row for the
            // same item id; on the next login the player had two of it, both doubled.
            var buyback = client.Player.Inventory.BuybackItems;

            if (!buyback.Contains(packet.ItemEntityId))
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to buy back item {packet.ItemEntityId}, which they did not sell.");
                return;
            }

            var item = EntityManager.Instance.GetItem(packet.ItemEntityId);

            if (item == null)
            {
                buyback.Remove(packet.ItemEntityId);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new RemoveBuybackItemPacket(packet.ItemEntityId));
                return;
            }

            var unitPrice = Math.Max(item.ItemTemplate.SellPrice, 0);
            var price = (long) unitPrice * item.StackSize;

            if (client.Player.Credits[CurencyType.Credits] < price)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInsufficientFunds, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            var quantity = item.StackSize;
            var placedItem = InventoryManager.Instance.AddItemToInventory(client, item);

            if (placedItem == null)
            {
                // Some or none of it fitted. What did not fit stays on the buyback list as the
                // remainder; the player pays for what they got.
                var placed = quantity - item.StackSize;

                if (placed == 0)
                {
                    client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                    return;
                }

                price = (long) unitPrice * placed;
            }
            else
            {
                buyback.Remove(packet.ItemEntityId);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new RemoveBuybackItemPacket(packet.ItemEntityId));
            }

            // remove credits
            ManifestationManager.Instance.LossCredits(client, (int) price);
        }

        public void RequestVendorPurchase(Client client, RequestVendorPurchasePacket packet)
        {
            // Everything in the packet is the client's word. The vendor has to be one whose stock
            // this server has laid out (RequestNPCVending registers it), and the item one of that
            // stock: without that, any item entity id the client had ever been shown - another
            // player's rifle, a corpse's loot - could be "bought" here at its template's BuyPrice,
            // which is 0 for anything no vendor sells.
            if (!EntityManager.Instance.VendorItems.TryGetValue(packet.VendorEntityId, out var stock)
                || !stock.Contains(packet.ItemEntityId))
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to buy item {packet.ItemEntityId} from {packet.VendorEntityId}, which does not sell it.");
                return;
            }

            var vendorItem = EntityManager.Instance.GetItem(packet.ItemEntityId);

            if (vendorItem == null)
            {
                Logger.WriteLog(LogType.Error, "RequestVendorPurchase: The item instance does not exist");
                return;
            }

            // Quantity is unsigned on the wire. It used to be cast to int and multiplied by the
            // price, so a value of 2^31 or more made the total negative: it passed the credit
            // check, CreateItem clamped the stack to the class maximum, and the debit added the
            // amount instead. A stack is the most one purchase can hand over, so that is the cap.
            var maxStack = EntityClassManager.Instance.GetItemClassInfo(vendorItem).StackSize;

            if (packet.Quantity == 0 || packet.Quantity > maxStack)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to buy {packet.Quantity} of item {packet.ItemEntityId} (stack size {maxStack}).");
                return;
            }

            var unitPrice = vendorItem.ItemTemplate.BuyPrice;

            if (unitPrice < 0)
            {
                Logger.WriteLog(LogType.Error, $"RequestVendorPurchase: item template {vendorItem.ItemTemplate.ItemTemplateId} has a negative BuyPrice.");
                return;
            }

            // Priced in long so nothing here can wrap; the debit below takes an int, and a
            // purchase the player cannot afford never reaches it.
            var total = (long) unitPrice * packet.Quantity;

            if (client.Player.Credits[CurencyType.Credits] < total)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInsufficientFunds, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            // A fresh item with its own row in the items table, holding the whole quantity.
            var boughtItem = ItemManager.Instance.DuplicateItem(client, packet);

            if (boughtItem == null)
                return;

            var quantity = boughtItem.StackSize;

            // Merges what it can into existing stacks, then takes a free slot for the rest. On a
            // full merge it deletes boughtItem's row itself and returns the stack it merged into;
            // if it runs out of room it returns null with the unplaced remainder still in
            // boughtItem.StackSize.
            var placedItem = InventoryManager.Instance.AddItemToInventory(client, boughtItem);

            if (placedItem == null)
            {
                var placed = quantity - boughtItem.StackSize;

                // The remainder was never placed: take its entity back from the client and its
                // row out of the table. The row used to be left behind on both paths, and on the
                // partial one the message below then read the null placedItem, which threw, so
                // the player kept the merged part and was never charged for it.
                EntityManager.Instance.DestroyPhysicalEntity(client, boughtItem.EntityId, EntityType.Item);

                if (boughtItem.Id != 0)
                    using (var unitOfWork = _gameUnitOfWorkFactory.CreateChar())
                        unitOfWork.Items.DeleteItem(boughtItem.Id);

                if (placed == 0)
                {
                    client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                    return;
                }

                quantity = placed;
                total = (long) unitPrice * quantity;
            }

            // send player message
            client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmGotLootFromUnknown, new Dictionary<string, string> { { "quantity", quantity.ToString() }, { "loot", vendorItem.ItemTemplate.Class.ToString() } }, MsgFilterId.LootObtained));

            // remove credits
            ManifestationManager.Instance.LossCredits(client, (int) total);
        }

        /// <summary>
        /// Repair one item at one vendor, RequestRepair(itemId, vendorId). The client builds
        /// this through a RepairAction and nothing calls the function that builds one, so it is
        /// unreachable in practice - but an opcode with no packet class closes the connection,
        /// and the work is RepairOne either way.
        /// </summary>
        public void RequestRepair(Client client, RequestRepairPacket packet)
        {
            if (!IsVendor(client, packet.VendorEntityId))
                return;

            RepairOne(client, packet.ItemEntityId);
        }

        private enum RepairResult
        {
            /// <summary>Repaired and paid for.</summary>
            Repaired,

            /// <summary>Not this player's item, not an item, or already at full hit points.</summary>
            Skipped,

            /// <summary>Repairable, but they cannot pay for it.</summary>
            Unaffordable
        }

        public void RequestVendorRepair(Client client, RequestVendorRepairPacket packet)
        {
            if (!IsVendor(client, packet.VendorEntityId))
                return;

            foreach (var itemEntityId in packet.ItemEntitesId)
            {
                // Stop at the first item they cannot afford rather than skipping it and
                // repairing the cheaper ones behind it.
                if (RepairOne(client, itemEntityId) == RepairResult.Unaffordable)
                    break;
            }
        }

        /// <summary>The same test RequestVendorPurchase applies: the entity has to be a vendor.</summary>
        private static bool IsVendor(Client client, ulong vendorEntityId)
        {
            if (client?.Player == null)
                return false;

            if (EntityManager.Instance.VendorItems.ContainsKey(vendorEntityId))
                return true;

            Logger.WriteLog(LogType.Security, $"AccountId = {client.AccountEntry.Id} asked {vendorEntityId} for repairs, and it is not a vendor.");

            return false;
        }

        /// <summary>
        /// The item, if this player may repair it and it needs repairing, with what that costs.
        /// Any entity id used to be accepted, including an item another player is carrying
        /// (repaired at this player's expense) and ids that are no item at all (a
        /// NullReferenceException in the handler).
        /// </summary>
        private static bool Repairable(Client client, ulong itemEntityId, out Item item, out int maxHitPoints, out int cost)
        {
            item = EntityManager.Instance.GetItem(itemEntityId);
            maxHitPoints = 0;
            cost = 0;

            var inventory = client.Player.Inventory;

            if (item == null
                || !(inventory.PersonalInventory.Contains(itemEntityId)
                     || inventory.EquippedInventory.Contains(itemEntityId)
                     || inventory.WeaponDrawer.Contains(itemEntityId)))
            {
                Logger.WriteLog(LogType.Security, $"AccountId = {client.AccountEntry.Id} asked to repair {itemEntityId}, which is not in their inventory.");
                return false;
            }

            var classInfo = EntityClassManager.Instance.GetClassInfo(item.ItemTemplate.Class);

            if (classInfo?.ItemClassInfo == null)
                return false;

            maxHitPoints = classInfo.ItemClassInfo.MaxHitPoints;

            // Nothing to repair - and with CurrentHitPoints above the maximum the old
            // arithmetic produced a negative cost, which LossCredits paid to the player.
            if (item.CurrentHitPoints >= maxHitPoints)
                return false;

            cost = (int)Math.Round((double)(maxHitPoints - item.CurrentHitPoints) * item.ItemTemplate.SellPrice / 100);

            return true;
        }

        /// <summary>Repairs one item to full and pays for it.</summary>
        private RepairResult RepairOne(Client client, ulong itemEntityId)
        {
            if (!Repairable(client, itemEntityId, out var item, out var maxHitPoints, out var cost))
                return RepairResult.Skipped;

            if (cost > client.Player.Credits[CurencyType.Credits])
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInsufficientFunds, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return RepairResult.Unaffordable;
            }

            item.CurrentHitPoints = maxHitPoints;
            ManifestationManager.Instance.LossCredits(client, cost);
            ItemManager.Instance.SendItemDataToClient(client, item, true);

            // The condition change itself. SendItemDataToClient carries the new hit points in
            // ItemInfo, but only ItemStatus makes the client act on them: it is what refreshes
            // the vendor's repair list and clears a weapon's broken icon in the drawer.
            ItemManager.Instance.SendItemStatus(client, item, maxHitPoints);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.Items.UpdateCurrentHitPoints(item);

            return RepairResult.Repaired;
        }

        public void RequestVendorSale(Client client, RequestVendorSalePacket packet)
        {
            var itemEntityId = packet.ItemEntityId;

            if (packet.Quantity <= 0)
                return;

            // note: Players can only sell items directly from their personal inventory
            //       so we only have to scan there for the item entityId
            var slotIndex = 250U;

            for (var i = 0; i < 250; i++)
            {
                if (client.Player.Inventory.PersonalInventory[i] == itemEntityId)
                {
                    slotIndex = (uint)i;
                    break;
                }
            }

            if (slotIndex == 250U)
            {
                Logger.WriteLog(LogType.Error, "RequestVendorSale: Item entity not found in player's inventory\n");
                return;
            }

            // get item handle
            var soldItem = EntityManager.Instance.GetItem(itemEntityId);

            if (soldItem == null)
            {
                Logger.WriteLog(LogType.Error, "RequestVendorSale: Item reference found but item instance does not exist\n");
                return;
            }

            var quantity = (uint) Math.Min(packet.Quantity, soldItem.StackSize);
            var sellPrice = Math.Min((long) Math.Max(soldItem.ItemTemplate.SellPrice, 0) * quantity, int.MaxValue);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (quantity < soldItem.StackSize)
            {
                // Selling part of a stack. The whole stack used to go, for the price of the part:
                // split off the part as an item of its own, and that is what was sold.
                soldItem.StackSize -= quantity;
                unitOfWork.Items.UpdateItemStackSize(soldItem);
                client.CallMethod(soldItem.EntityId, new SetStackCountPacket(soldItem.StackSize));

                soldItem = ItemManager.Instance.CreateFromTemplateId(soldItem.ItemTemplate.ItemTemplateId, quantity, soldItem.Crafter);

                if (soldItem == null)
                {
                    Logger.WriteLog(LogType.Error, $"RequestVendorSale: could not split {quantity} off item {itemEntityId}.");
                    return;
                }

                ItemManager.Instance.SendItemDataToClient(client, soldItem, false);
            }
            else
            {
                // remove item; the row by item id, since the character id it carries is
                // not written consistently and a miss leaves a row behind
                InventoryManager.Instance.RemoveItemBySlot(client, InventoryType.Personal, slotIndex);
                unitOfWork.CharacterInventories.DeleteInvItemByItemId(soldItem.Id);
            }

            // add credits to player
            ManifestationManager.Instance.GainCredits(client, (int) sellPrice);

            // add item to buyback list, retiring the oldest if it is full
            var buyback = client.Player.Inventory.BuybackItems;

            while (buyback.Count >= Inventory.MaxBuybackItems)
            {
                var retired = buyback[0];
                buyback.RemoveAt(0);
                DiscardSoldItem(client, retired, unitOfWork);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new RemoveBuybackItemPacket(retired));
            }

            buyback.Add(soldItem.EntityId);
            client.CallMethod(SysEntity.ClientInventoryManagerId, new AddBuybackItemPacket(soldItem.EntityId, (int) sellPrice, buyback.Count));
        }

        /// <summary>
        /// A sold item nobody can buy back any more: off the client, out of the entity tables,
        /// and its row out of the items table.
        /// </summary>
        private static void DiscardSoldItem(Client client, ulong entityId, ICharUnitOfWork unitOfWork)
        {
            var item = EntityManager.Instance.GetItem(entityId);

            EntityManager.Instance.DestroyPhysicalEntity(client, entityId, EntityType.Item);

            if (item != null && item.Id != 0)
                unitOfWork.Items.DeleteItem(item.Id);
        }

        /// <summary>
        /// Called when the character leaves the world. Whatever was still on the buyback list
        /// is gone for good, so its entities and rows go with it; they used to be left
        /// registered for the life of the process.
        /// </summary>
        public void DiscardBuybackItems(Client client)
        {
            var buyback = client.Player.Inventory.BuybackItems;

            if (buyback.Count == 0)
                return;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            foreach (var entityId in buyback)
                DiscardSoldItem(client, entityId, unitOfWork);

            buyback.Clear();
        }

        #endregion
    }
}
