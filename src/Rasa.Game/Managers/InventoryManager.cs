using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Communicator.Server;
    using Packets.Inventory.Client;
    using Packets.Inventory.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Repositories.Char;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;

    public class InventoryManager
    {
        /*    Inventory Packets:
         *      Done:
         *  - AddBuybackItem
         *  - InventoryAddItem
         *  - InventoryCreate
         *  - InventoryRemoveItem
         *  - LockboxTabPermissions
         *  - RemoveBuybackItem
         *  
         *      ToDo:
         *  - AddAuctionItem
         *  - AddInboxItem
         *  - AddOverflowItem
         *  - AddWagerItem
         *  - InventoryDestroy
         *  - InventoryMoveFailed
         *  - InventoryReload
         *  - RemoveAuctionItem
         *  - RemoveInboxItem
         *  - RemoveOverflowItem
         *  - RemoveWagerItem
         *  - ResetAuctionInventory
         *  - ResetBuybackInventory
         *  - ResetInboxInventory
         *  - ResetOverflowInventory
         *  - ResetWagerInventory
         *  
         *    Inventory Handlers:
         *  - ClanLockbox_DepositItemInSlot         => implemented
         *  - ClanLockbox_DepositItemInTab          => implemented
         *  - ClanLockbox_DestroyItem               => implemented
         *  - ClanLockbox_MoveItem                  => implemented
         *  - ClanLockbox_WithdrawItem              => implemented
         *  - HomeInventory_DestroyItem             => implemented
         *  - HomeInventory_MoveItem                => implemented
         *  - OverflowTransfer                      => ToDo
         *  - PersonalInventory_DestroyItem         => implemented
         *  - PersonalInventory_MoveItem            => implemented
         *  - PurchaseClanLockboxTab                => ToDo
         *  - PurchaseLockboxTab                    => implemented
         *  - RequestEquipArmor                     => implemented
         *  - RequestEquipWeapon                    => implemented
         *  - RequestLockboxTabPermissions          => implemented
         *  - RequestMoveItemToHomeInventory        => implemented
         *  - RequestTakeItemFromHomeInventory      => implemented
         *  - RequestTakeItemFromInboxInventory     => ToDo
         *  - TransferCreditToLockbox               => implemented
         *  - WeaponDrawerInventory_MoveItem        => implemented
         */

        private static InventoryManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        public static InventoryManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new InventoryManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private InventoryManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        #region Handlers

        public void HomeInventory_DestroyItem(Client client, HomeInventory_DestroyItemPacket packet)
        {
            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            // An id that is not an item used to be passed on and dereferenced.
            if (tempItem == null)
                return;

            ReduceStackCount(client, InventoryType.HomeInventory, tempItem, packet.Quantity);

            // ToDo delete item from db? or we sill keep all items
        }

        public void HomeInventory_MoveItem(Client client, HomeInventory_MoveItemPacket packet)
        {
            // remove item
            if (packet.SrcSlot == packet.DestSlot)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 480)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 480)
                return;

            var entityId = client.Player.Inventory.HomeInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.HomeInventory, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.HomeInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.HomeInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.HomeInventory, entityId, packet.DestSlot, true);
        }

        public void PersonalInventory_DestroyItem(Client client, PersonalInventory_DestroyItemPacket packet)
        {
            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            // An id that is not an item used to be passed on and dereferenced.
            if (tempItem == null)
                return;

            ReduceStackCount(client, InventoryType.Personal, tempItem, packet.Quantity);

            // ToDo delete item from db? or we sill keep all items
        }

        public void PersonalInventory_MoveItem(Client client, PersonalInventory_MoveItemPacket packet)
        {
            // remove item
            if (packet.SrcSlot == packet.DestSlot)
                return;

            // Every slot check in this file is against the list's size, exclusive: several
            // used to be inclusive, so the index one past the end passed and the list threw.
            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
            {
                Logger.WriteLog(LogType.Debug, $"SrcSlot out of range => {packet.SrcSlot}");
                return;
            }

            if (packet.DestSlot < 0 || packet.DestSlot >= 250)
            {
                Logger.WriteLog(LogType.Debug, $"DestSlot out of range => {packet.DestSlot}");
                return;
            }

            var entityId = client.Player.Inventory.PersonalInventory[packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, (uint)packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.PersonalInventory[packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.PersonalInventory[packet.DestSlot], (uint)packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.Personal, entityId, (uint)packet.DestSlot, true);
        }

        public void PurchaseLockboxTab(Client client, PurchaseLockboxTabPacket packet)
        {
            /* ToDo
             * player credits are checked on client side
             * should we add server side check too?
             */

            if (packet.TabId == 2)  // price is 100 000
                ManifestationManager.Instance.LossCredits(client, 100000);
            if (packet.TabId == 3)  // price is 1 000 000
                ManifestationManager.Instance.LossCredits(client, 1000000);
            if (packet.TabId == 4)  // price is 10 000 000
                ManifestationManager.Instance.LossCredits(client, 10000000);
            if (packet.TabId == 5)  // price is 100 000 000
                ManifestationManager.Instance.LossCredits(client, 100000000);

            // update Player
            client.Player.LockboxTabs = packet.TabId;
            // update Db
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterLockboxes.UpdatePurashedTabs(client.AccountEntry.Id, packet.TabId);
            // send data to client
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(packet.TabId));
        }

        public void RequestEquipArmor(Client client, RequestEquipArmorPacket packet)
        {
            if (packet.SrcInventory != InventoryType.Personal)
            {
                Logger.WriteLog(LogType.Debug, $"Unsupported inventory => {packet.SrcInventory}");
                return;
            }

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 50)
            {
                Logger.WriteLog(LogType.Debug, $"SrcSlot out of range => {packet.SrcSlot}");
                return;
            }

            // The list has 22 entries; the old check let 22 through.
            if (packet.DestSlot >= client.Player.Inventory.EquippedInventory.Count)
            {
                Logger.WriteLog(LogType.Debug, $"DestSlot out of range => {packet.DestSlot}");
                return;
            }

            var entityIdEquippedItem = client.Player.Inventory.EquippedInventory[(int)packet.DestSlot]; // the old equipped item (can be none)
            var entityIdInventoryItem = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot]; // the new equipped item (can be none)

            // Nothing coming in and nothing going out: the dequip path below would have looked
            // up the class of an item that is not there.
            if (entityIdInventoryItem == 0 && entityIdEquippedItem == 0)
                return;

            // can we equip the item
            var itemToEquip = EntityManager.Instance.GetItem(entityIdInventoryItem);

            if (entityIdInventoryItem != 0 && itemToEquip == null)
            {
                Logger.WriteLog(LogType.Error, $"RequestEquipArmor: slot {packet.SrcSlot} holds entity {entityIdInventoryItem} but no item is registered for it.");
                return;
            }

            if (itemToEquip != null)
            {
                // The item goes in the slot its class says it is for, and nowhere else. The slot
                // index in the equipped list is the equipment slot id. Nothing checked this, so
                // any category-0 item could be put in any of the 22 slots - the same chest piece
                // in all of them - and UpdateStatsValues sums ArmorValue over every slot.
                var equipable = EntityClassManager.Instance.LoadedEntityClasses[itemToEquip.ItemTemplate.Class].EquipableClassInfo;

                if (equipable == null || (uint) equipable.EquipmentSlotId != packet.DestSlot)
                {
                    Logger.WriteLog(LogType.Security,
                        $"AccountId = {client.AccountEntry.Id} tried to equip {itemToEquip.ItemTemplate.Class} in slot {packet.DestSlot}"
                        + (equipable == null ? ", which is not equipment." : $", which is for {equipable.EquipmentSlotId}."));
                    return;
                }

                if (!ValidateItemEquip(client, itemToEquip))
                    return;
            }

            // swap items on the client and server
            if (client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot] != 0)
                RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);

            if (client.Player.Inventory.EquippedInventory[(int)packet.DestSlot] != 0)
                RemoveItemBySlot(client, InventoryType.EquipedInventory, packet.DestSlot);

            if (entityIdEquippedItem != 0)
                AddItemBySlot(client, InventoryType.Personal, entityIdEquippedItem, packet.SrcSlot, true);

            if (entityIdInventoryItem != 0)
                AddItemBySlot(client, InventoryType.EquipedInventory, entityIdInventoryItem, packet.DestSlot, true);

            // update appearance
            if (itemToEquip == null)
            {
                // remove item graphic if dequipped
                var prevEquippedItem = EntityManager.Instance.GetItem(entityIdEquippedItem);
                var equipableClassInfo = EntityClassManager.Instance.GetEquipableClassInfo(prevEquippedItem);
                ManifestationManager.Instance.RemoveAppearanceItem(client, equipableClassInfo.EquipmentSlotId);
            }
            else
                ManifestationManager.Instance.SetAppearanceItem(client, itemToEquip);

            ManifestationManager.Instance.UpdateAppearance(client);
            ManifestationManager.Instance.UpdateStatsValues(client, false);
            ManifestationManager.Instance.NotifyEquipmentUpdate(client);

            // Send Data to client
            client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
        }

        public void RequestEquipWeapon(Client client, RequestEquipWeaponPacket packet)
        {
            var srcSlot = packet.SrcSlot;
            var invType = packet.InventoryType;
            var destSlot = packet.DestSlot;

            if (invType != InventoryType.Personal)
            {
                Console.WriteLine("unsuported inventory");
                return;
            }

            if (srcSlot < 0 || srcSlot >= 50)
                return;

            if (destSlot < 0 || destSlot >= 5)
                return;

            // equip item
            var entityIdEquippedItem = client.Player.Inventory.WeaponDrawer[(int)destSlot]; // the old equipped item (can be none)
            var entityIdInventoryItem = client.Player.Inventory.PersonalInventory[(int)srcSlot]; // the new equipped item (can be none)

            // can we equip the item
            var itemToEquip = EntityManager.Instance.GetItem(entityIdInventoryItem);
            var canEquip = ValidateItemEquip(client, itemToEquip);

            if (itemToEquip == null && canEquip == false)
                return;

            if (canEquip == false)
                return;

            // swap items on the client and server
            if (client.Player.Inventory.PersonalInventory[(int)srcSlot] != 0)
                RemoveItemBySlot(client, InventoryType.Personal, srcSlot);
            if (client.Player.Inventory.WeaponDrawer[(int)destSlot] != 0)
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, destSlot);
            if (entityIdEquippedItem != 0)
                AddItemBySlot(client, InventoryType.Personal, entityIdEquippedItem, srcSlot, true);
            if (entityIdInventoryItem != 0)
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, entityIdInventoryItem, destSlot, true);

            if (destSlot == client.Player.ActiveWeapon)
                if (itemToEquip == null)
                {
                    // remove item graphic if dequipped
                    var prevEquippedItem = EntityManager.Instance.GetItem(entityIdEquippedItem);
                    var equipableClassInfo = EntityClassManager.Instance.GetEquipableClassInfo(prevEquippedItem);

                    RemoveItemBySlot(client, InventoryType.EquipedInventory, 13);
                    ManifestationManager.Instance.RemoveAppearanceItem(client, equipableClassInfo.EquipmentSlotId);

                    // we dont have weapon, set weaponReady to false
                    if (client.Player.WeaponReady)
                        ManifestationManager.Instance.WeaponReady(client, false);
                }
                else
                    ManifestationManager.Instance.SetAppearanceItem(client, itemToEquip);

            // Tell client that he have new weapon
            ManifestationManager.Instance.NotifyEquipmentUpdate(client);

            ManifestationManager.Instance.UpdateAppearance(client);
        }

        public void RequestLockboxTabPermissions(Client client)
        {
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(client.Player.LockboxTabs));
        }

        public void RequestMoveItemToClanLockbox(Client client, RequestMoveItemToClanLockboxPacket packet)
        {
            Logger.WriteLog(LogType.Debug, $"ToDO: RequestMoveItemToClanLockboxPacket");
        }

        public void RequestMoveItemToHomeInventory(Client client, RequestMoveItemToHomeInventoryPacket packet)
        {
            // remove item
            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 480)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.HomeInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.HomeInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.HomeInventory, entityId, packet.DestSlot, true);
        }

        public void ClanLockbox_DepositItemInSlot(Client client, ClanLockbox_DepositItemInSlotPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 500)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);

            // If DestSlot is not empty, move current item to SrcSlot (item swap)
            bool wasSwap = client.Player.Inventory.ClanInventory[(int)packet.DestSlot] != 0;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var depositedItem = EntityManager.Instance.GetItem(entityId);

            if (depositedItem == null)
                return;

            // Rows are found by item id throughout: the character id they were written with
            // is not always this character's, and a delete by slot that misses leaves a row
            // pointing at an item that has moved on.
            if (wasSwap)
            {
                unitOfWork.CharacterInventories.DeleteInvItemByItemId(depositedItem.Id);
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.ClanInventory[(int)packet.DestSlot], packet.SrcSlot, true, true);

                RemoveItemBySlotForClan(client.Player.ClanId, packet.DestSlot, 0);
                unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.DestSlot);
            }

            AddItemBySlot(client, InventoryType.ClanInventory, entityId, packet.DestSlot, true, true);

            if (!wasSwap)
                unitOfWork.CharacterInventories.DeleteInvItemByItemId(depositedItem.Id);

            depositedItem.OwnerSlotId = packet.DestSlot;
            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, packet.DestSlot, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_DepositItemInTab(Client client, ClanLockbox_DepositItemInTabPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 500)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(entityId);

            RemoveItemBySlot(client, InventoryType.Personal, (uint)packet.SrcSlot);

            // AddItemToClanInventory saves the stack sizes it changes, and deletes every row
            // of an item it merges away; updating tempItem here afterwards was redundant, and
            // after a full merge it was an update of a deleted row.
            Item item = AddItemToClanInventory(client, tempItem);
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (item == null)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            // The personal row is found by item id: the character id it was written with is
            // not always this character's.
            unitOfWork.CharacterInventories.DeleteInvItemByItemId(tempItem.Id);

            if (EntityManager.Instance.GetItem(entityId) == null)
                return;

            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, item.OwnerSlotId, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_MoveItem(Client client, ClanLockbox_MoveItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot == packet.DestSlot)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 500)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 500)
                return;

            var entityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            // If DestSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.ClanInventory[(int)packet.DestSlot] != 0)
            {
                // Todo swap items
                return;
            }
            RemoveItemBySlot(client, InventoryType.ClanInventory, packet.SrcSlot); // Put this above swap if check once swap is implemented

            EntityManager.Instance.GetItem(entityId).OwnerSlotId = packet.DestSlot;
            AddItemBySlot(client, InventoryType.ClanInventory, entityId, packet.DestSlot, true, false);

            RemoveItemBySlotForClan(client.Player.ClanId, packet.SrcSlot, client.Player.Id);
            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, packet.DestSlot, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_WithdrawItem(Client client, ClanLockbox_WithdrawItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            // Only the leader and the rank below them can withdraw items from the clan lockbox.
            ClanMemberEntry member = ClanManager.Instance.GetClanMember(client.Player.ClanId, client.Player.Id);
            if (member.Rank < 2)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmClanInsufficientPermissions, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 500)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 250)
                return;

            var entityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(entityId);
            bool wasSwap = client.Player.Inventory.PersonalInventory[(int)packet.DestSlot] != 0;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (packet.ManagePersonalSlot)
            {
                wasSwap = false;
                Item item = AddItemToInventory(client, tempItem);

                if (item == null)
                {
                    RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
                    client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                    return;
                }
            }
            else
            {
                if (wasSwap)
                {
                    RemoveItemBySlot(client, InventoryType.ClanInventory, packet.SrcSlot);
                    unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.SrcSlot);
                    AddItemBySlot(client, InventoryType.ClanInventory, client.Player.Inventory.PersonalInventory[(int)packet.DestSlot], packet.SrcSlot, true, true);

                    var newEntityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];
                    RefreshClanLockbox(client.Player.ClanId, newEntityId, client.Player.Id, packet.SrcSlot, ref client.Player.Inventory.ClanInventory, true);

                    var swappedOut = EntityManager.Instance.GetItem(client.Player.Inventory.PersonalInventory[(int)packet.DestSlot]);

                    RemoveItemBySlot(client, InventoryType.Personal, packet.DestSlot);

                    if (swappedOut != null)
                        unitOfWork.CharacterInventories.DeleteInvItemByItemId(swappedOut.Id);
                }
                AddItemBySlot(client, InventoryType.Personal, entityId, packet.DestSlot, true, true);
            }

            if (!wasSwap)
            {
                RemoveItemBySlotForClan(client.Player.ClanId, packet.SrcSlot, 0);
                unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.SrcSlot);

                RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
            }
        }

        public void ClanLockbox_DestroyItem(Client client, ClanLockbox_DestroyItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            //TODO: Support deleting portions
            if ((tempItem.StackSize - packet.Quantity) > 0)
                return;

            RemoveItemBySlotForClan(client.Player.ClanId, tempItem.OwnerSlotId, 0);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, tempItem.OwnerSlotId);

            RefreshClanLockbox(client.Player.ClanId, packet.EntityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
        }

        public void RequestTakeItemFromHomeInventory(Client client, RequestTakeItemFromHomeInventoryPacket packet)
        {
            // remove item
            if (packet.SrcSlot < 0 || packet.SrcSlot >= 480)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 250)
                return;

            var entityId = client.Player.Inventory.HomeInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.HomeInventory, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.PersonalInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.PersonalInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.Personal, entityId, packet.DestSlot, true);
        }

        public void TransferCreditToLockbox(Client client, int amount)
        {
            /*
             * ToDo:
             * there is some bug with withdraw if withdraw value is less then 256
             * client send positive value, insted of negative one
             * so we will set min transfer value to 500 for now
             * we can take closer look at this later
             */

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            //deposit
            if (amount >= 500)
            {
                if (client.Player.Credits[CurencyType.Credits] >= amount)
                {
                    var deposit = client.Player.LockboxCredits + amount;

                    ManifestationManager.Instance.LossCredits(client, -amount);

                    client.CallMethod(client.Player.EntityId, new LockboxFundsPacket(deposit));

                    client.Player.LockboxCredits = deposit;
                    unitOfWork.CharacterLockboxes.UpdateCredits(client.AccountEntry.Id, deposit);
                }
                else
                    CommunicatorManager.Instance.SystemMessage(client, "Not enof credit's in inventory\nP.S. Go earn some credits :)");
            }
            // withdraw
            else if (amount <= -500)
            {
                if (client.Player.LockboxCredits >= -amount)
                {
                    var withdraw = client.Player.LockboxCredits + amount;

                    ManifestationManager.Instance.GainCredits(client, -amount);
                    client.CallMethod(client.Player.EntityId, new LockboxFundsPacket(withdraw));

                    client.Player.LockboxCredits = withdraw;
                    unitOfWork.CharacterLockboxes.UpdateCredits(client.AccountEntry.Id, withdraw);
                }
                else
                    CommunicatorManager.Instance.SystemMessage(client, "Not enof credit's in Lockbox\nP.S. Dont be greedy :)");
            }
            else
                CommunicatorManager.Instance.SystemMessage(client, "Minimum transfer value is 500 credits");

        }

        public void ClanCreditTransfer(Client client, long amount, uint creditType)
        {
            // amount > 0 deposits into the lockbox, amount < 0 withdraws from it.
            // creditType 1 is credits, 2 is prestige.
            if (client.Player == null || client.Player.ClanId == 0)
                return;

            if (creditType != 1 && creditType != 2)
                return;

            if (amount > -500 && amount < 500)
            {
                CommunicatorManager.Instance.SystemMessage(client, "Minimum transfer value is 500 credits");
                return;
            }

            // The character side is an int; anything past that cannot be a real request.
            if (amount < int.MinValue || amount > int.MaxValue)
                return;

            var currency = creditType == 1 ? CurencyType.Credits : CurencyType.Prestige;
            var characterUpdate = creditType == 1 ? CharacterUpdate.Credits : CharacterUpdate.Prestige;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var clanInfo = unitOfWork.Clans.GetClanById(client.Player.ClanId);

            if (clanInfo == null)
                return;

            long lockboxBalance = creditType == 1 ? clanInfo.Credits : clanInfo.Prestige;
            long lockboxAfter = lockboxBalance + amount;
            long playerAfter = client.Player.Credits[currency] - amount;

            if (playerAfter < 0)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInsufficientDepositFunds, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            if (lockboxAfter < 0)
            {
                CommunicatorManager.Instance.SystemMessage(client, "Not enough credit's");
                return;
            }

            if (lockboxAfter > uint.MaxValue || playerAfter > int.MaxValue)
                return;

            // The character is charged first and the lockbox credited second. This used to be
            // the other way round, and the character step threw before it ran: the long
            // amount was boxed and unboxed as an int, an InvalidCastException, so the clan
            // kept every deposit, the depositor kept the money, and the client was
            // disconnected. If the second step fails now the player is short, not the clan.
            CharacterManager.Instance.UpdateCharacter(client, characterUpdate, (int)(-amount));

            if (creditType == 1)
                unitOfWork.Clans.UpdateCredits(client.Player.ClanId, (uint)lockboxAfter);
            else
                unitOfWork.Clans.UpdatePrestige(client.Player.ClanId, (uint)lockboxAfter);

            var lockboxCredits = creditType == 1 ? (uint)lockboxAfter : clanInfo.Credits;
            var lockboxPrestige = creditType == 2 ? (uint)lockboxAfter : clanInfo.Prestige;

            foreach (var dynamicObj in EntityManager.Instance.DynamicObjects)
            {
                var dynamicObject = dynamicObj.Value;

                if (dynamicObject.EntityClassId == EntityClasses.UsableClanLockboxV01)
                    ClanManager.Instance.CallMethodForOnlineMembers(client.Player.ClanId, dynamicObject.EntityId, new UpdateClanLockboxCreditsPacket(lockboxCredits, lockboxPrestige));
            }
        }

        public void WeaponDrawerInventory_MoveItem(Client client, WeaponDrawerInventory_MoveItemPacket packet)
        {
            // Nothing checked either slot; the drawer has five.
            if (packet.SrcSlot >= 5 || packet.DestSlot >= 5 || packet.SrcSlot == packet.DestSlot)
                return;

            var srcEntityId = client.Player.Inventory.WeaponDrawer[(int)packet.SrcSlot];

            if (srcEntityId == 0)
                return;

            var destEntityId = client.Player.Inventory.WeaponDrawer[(int)packet.DestSlot];
            // swap items on the client and server
            if (destEntityId != 0)
            {
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.SrcSlot);
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.DestSlot);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, srcEntityId, packet.DestSlot, true);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, destEntityId, packet.SrcSlot, true);
            }
            else
            {
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.SrcSlot);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, srcEntityId, packet.DestSlot, true);
            }
        }

        #endregion

        #region Helper Functions

        public void UpdateItemSlot(Client client, ulong entityId)
        {
            Item tempItem = EntityManager.Instance.GetItem(entityId);
            ItemManager.Instance.SendItemDataToClient(client, tempItem, false);
        }

        public void AddItemBySlot(Client client, InventoryType inventoryType, ulong entityId, uint slotId, bool updateDB, bool actuallyAdd = false)
        {
            var tempItem = EntityManager.Instance.GetItem(entityId);

            if (tempItem == null)
                return;

            // set entityId in slot
            switch (inventoryType)
            {
                case InventoryType.Personal:
                    client.Player.Inventory.PersonalInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.HomeInventory:
                    client.Player.Inventory.HomeInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.EquipedInventory:
                    client.Player.Inventory.EquippedInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.WeaponDrawerInventory:
                    client.Player.Inventory.WeaponDrawer[(int)slotId] = tempItem.EntityId; // update slot

                    // EquippedInventory[13] is the weapon in hand, which is the drawer slot
                    // ActiveWeapon names - not whichever drawer slot was written last. This
                    // used to set it unconditionally, so equipping into a non-active slot,
                    // swapping drawer slots, or just loading the drawer in row order made
                    // CurrentWeapon() answer a weapon the player was not holding, and fire,
                    // reload and ammo all acted on that one.
                    if (slotId == client.Player.ActiveWeapon)
                        client.Player.Inventory.EquippedInventory[13] = tempItem.EntityId;
                    break;
                case InventoryType.ClanInventory:
                    client.Player.Inventory.ClanInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                default:
                    Console.WriteLine("Unsuported inventory type");
                    break;
            }
            // send inventoryAddItem
            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryAddItemPacket(inventoryType, tempItem.EntityId, slotId));
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // OwnerId is the character id the inventory row is written with, and it follows
            // the destination: the home lockbox is the account's (0), the clan lockbox is the
            // clan's, and the three character inventories are this character's. It used to
            // be set only for Home, so an item taken out of the home lockbox kept OwnerId 0
            // and its Personal row was written with character_id 0 - which the loader only
            // reads for Home, so the item was gone at the next login - and an item pulled from
            // the clan lockbox kept whatever the depositor had left in it.
            switch (inventoryType)
            {
                case InventoryType.HomeInventory:
                case InventoryType.ClanInventory:
                    tempItem.OwnerId = 0;
                    break;
                case InventoryType.Personal:
                case InventoryType.EquipedInventory:
                case InventoryType.WeaponDrawerInventory:
                    tempItem.OwnerId = client.Player.Id;
                    break;
            }

            // update item in database
            if (updateDB)
            {
                if (inventoryType == InventoryType.ClanInventory)
                {
                    if (actuallyAdd)
                    {
                        unitOfWork.ClanInventories.AddInvItem(client.Player.ClanId, slotId, tempItem.Id);
                    }
                    else
                    {
                        unitOfWork.ClanInventories.MoveInvItem(client.Player.ClanId, slotId, tempItem.Id);
                    }
                }
                else
                {
                    if (actuallyAdd)
                    {
                        unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, tempItem.OwnerId, (uint)inventoryType, slotId, tempItem.Id);
                    }
                    else
                    {
                        unitOfWork.CharacterInventories.MoveInvItem(client.AccountEntry.Id, tempItem.OwnerId, (uint)inventoryType, slotId, tempItem.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Removes a merged-away item from the database: its inventory row first, then the item
        /// row. The item row alone used to be deleted here, and the caller was expected to
        /// delete the inventory row afterwards - by slot, with a character id that is not
        /// written consistently, and after a call that threw. Nothing in the schema stops a
        /// character_inventory or clan_inventory row from pointing at an item that no longer
        /// exists, and a row like that made the next login (or, for a clan, the next server
        /// start) dereference null. An item has one row in one of the two tables; both
        /// deletes are no-ops when there is nothing to delete.
        /// </summary>
        private static void DeleteItemRows(ICharUnitOfWork unitOfWork, Item item)
        {
            if (item.Id == 0)
                return;

            // One transaction: either all three rows go or none does, so a failure between
            // them cannot leave the item row gone and an inventory row pointing at it.
            using var transaction = unitOfWork.BeginTransaction();

            unitOfWork.CharacterInventories.DeleteInvItemByItemId(item.Id);
            unitOfWork.ClanInventories.DeleteInvItemByItemId(item.Id);
            unitOfWork.Items.DeleteItem(item.Id);

            transaction.Commit();
        }

        /// <summary>
        /// Puts an item in the slot the player asked for, falling back to the ordinary
        /// first-that-fits placement when that slot is not usable.
        ///
        /// The slot is a personal-inventory index the client worked out itself (inventory.py adds
        /// the category's start to the slot within it), so it is checked here rather than trusted:
        /// out of range, occupied, or in another category's block all fall back instead of being
        /// refused, because the player asked to take the item and where it lands is the lesser
        /// question.
        /// </summary>
        public Item AddItemToInventory(Client client, Item item, uint destSlot)
        {
            if (item == null)
                return null;

            var inventory = client.Player.Inventory.PersonalInventory;
            var categoryOffset = ((int)item.ItemTemplate.InventoryCategory - 1) * 50;

            var usable = categoryOffset >= 0
                         && destSlot < inventory.Count
                         && destSlot >= categoryOffset
                         && destSlot < categoryOffset + 50
                         && inventory[(int)destSlot] == 0;

            if (!usable)
                return AddItemToInventory(client, item);

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);

            item.OwnerId = client.Player.Id;
            item.OwnerSlotId = destSlot;
            item.CurrentHitPoints = itemClassInfo.MaxHitPoints;

            ItemManager.Instance.SendItemDataToClient(client, item, false);
            AddItemBySlot(client, InventoryType.Personal, item.EntityId, destSlot, true, true);

            return item;
        }

        public Item AddItemToInventory(Client client, Item item)
        {
            if (item == null)
                return null;

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);

            // get item category offset
            var itemCategoryOffset = (int)item.ItemTemplate.InventoryCategory - 1;

            if (itemCategoryOffset < 0 || itemCategoryOffset >= 5)
            {
                Logger.WriteLog(LogType.Error, $"AddItemToInventory: ItemTemplateId = {item.ItemTemplate.ItemTemplateId} inventory category = {item.ItemTemplate.InventoryCategory} is invalid");
                return null;
            }

            itemCategoryOffset *= 50;
            var stackSizeChanged = false;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            // see if we can merge the item into an already existing item
            for (var i = 0; i < 50; i++)
                if (client.Player.Inventory.PersonalInventory[itemCategoryOffset + i] != 0)
                {
                    // get item
                    var slotItem = EntityManager.Instance.GetItem(client.Player.Inventory.PersonalInventory[itemCategoryOffset + i]);

                    // same item template?
                    if (slotItem.ItemTemplate.ItemTemplateId != item.ItemTemplate.ItemTemplateId)
                        continue;

                    // calculate how many items we can add to the stack
                    var stackAdd = itemClassInfo.StackSize - slotItem.StackSize;
                    if (stackAdd == 0)
                        continue;

                    // add item to existing stack
                    var stackMove = Math.Min(stackAdd, item.StackSize);
                    slotItem.StackSize += stackMove;
                    unitOfWork.Items.UpdateItemStackSize(slotItem);

                    // remove stack's from source item
                    item.StackSize -= stackMove;
                    stackSizeChanged = true;

                    // notify client of changed stack count
                    client.CallMethod(slotItem.EntityId, new SetStackCountPacket(slotItem.StackSize));

                    if (item.StackSize == 0)
                    {
                        // destroy the item
                        EntityManager.Instance.DestroyPhysicalEntity(client, item.EntityId, EntityType.Item);
                        DeleteItemRows(unitOfWork, item);
                        // return the 'new' item instead
                        return slotItem;
                    }

                }

            // item have new stackSize?
            if (stackSizeChanged)
            {
                client.CallMethod(item.EntityId, new SetStackCountPacket(item.StackSize));

                // The rows of the stacks it was merged into were updated as it went; the
                // remainder's own row still says the whole amount. Left like that, a purchase
                // that half-merged and then took a free slot came back at its full size on the
                // next login - the merged part counted twice.
                if (item.Id != 0)
                    unitOfWork.Items.UpdateItemStackSize(item);
            }

            // find free slot
            for (var i = 0; i < 50; i++)
            {
                if (client.Player.Inventory.PersonalInventory[itemCategoryOffset + i] == 0)
                {
                    item.OwnerId = client.Player.Id;
                    item.OwnerSlotId = (uint)(itemCategoryOffset + i);
                    item.CurrentHitPoints = itemClassInfo.MaxHitPoints;
                    // send data to client
                    ItemManager.Instance.SendItemDataToClient(client, item, false);
                    // add item to empty slot
                    AddItemBySlot(client, InventoryType.Personal, item.EntityId, (uint)(itemCategoryOffset + i), true, true);
                    return item;
                }
            }

            return null;
        }

        public Item AddItemToClanInventory(Client client, Item item)
        {
            if (item == null)
                return null;

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var stackSizeChanged = false;
            // see if we can merge the item into an already existing item
            for (var i = 0; i < 500; i++)
                if (client.Player.Inventory.ClanInventory[i] != 0)
                {
                    // get item
                    var slotItem = EntityManager.Instance.GetItem(client.Player.Inventory.ClanInventory[i]);

                    // same item template?
                    if (slotItem.ItemTemplate.ItemTemplateId != item.ItemTemplate.ItemTemplateId)
                        continue;

                    // calculate how many items we can add to the stack
                    var stackAdd = itemClassInfo.StackSize - slotItem.StackSize;
                    if (stackAdd == 0)
                        continue;

                    // add item to existing stack
                    var stackMove = Math.Min(stackAdd, item.StackSize);
                    slotItem.StackSize += stackMove;
                    unitOfWork.Items.UpdateItemStackSize(slotItem);

                    // remove stack's from source item
                    item.StackSize -= stackMove;
                    stackSizeChanged = true;

                    // notify client of changed stack count
                    //client.CallMethod(slotItem.EntityId, new SetStackCountPacket(slotItem.Stacksize));
                    ClanManager.Instance.CallMethodForOnlineMembers(client.Player.ClanId, slotItem.EntityId, new SetStackCountPacket(slotItem.StackSize));

                    if (item.StackSize == 0)
                    {
                        // destroy the item
                        EntityManager.Instance.DestroyPhysicalEntity(client, item.EntityId, EntityType.Item);
                        DeleteItemRows(unitOfWork, item);
                        // return the 'new' item instead
                        return slotItem;
                    }

                }

            // item have new stackSize?
            if (stackSizeChanged)
            {
                client.CallMethod(item.EntityId, new SetStackCountPacket(item.StackSize));

                // The rows of the stacks it was merged into were updated as it went; the
                // remainder's own row still says the whole amount. Left like that, a purchase
                // that half-merged and then took a free slot came back at its full size on the
                // next login - the merged part counted twice.
                if (item.Id != 0)
                    unitOfWork.Items.UpdateItemStackSize(item);
            }

            // find free slot
            for (var i = 0; i < 500; i++)
            {
                if (client.Player.Inventory.ClanInventory[i] == 0)
                {
                    // AddItemBySlot sets OwnerId for the destination; SelectedSlot (a pod
                    // number) used to be stored here as if it were a character id.
                    item.OwnerSlotId = (uint)(i);
                    item.CurrentHitPoints = itemClassInfo.MaxHitPoints;
                    // send data to client
                    ItemManager.Instance.SendItemDataToClient(client, item, false);
                    // add item to empty slot
                    AddItemBySlot(client, InventoryType.ClanInventory, item.EntityId, (uint)(i), true, true);
                    return item;
                }
            }

            return null;
        }

        public Item CurrentWeapon(Client client)
        {
            return EntityManager.Instance.GetItem(client.Player.Inventory.EquippedInventory[13]);
        }

        public uint FreeSlotIndex(Manifestation player, InventoryType inventoryType, uint slotIndex)
        {
            switch (inventoryType)
            {
                case InventoryType.Personal:
                    player.Inventory.PersonalInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.HomeInventory:
                    player.Inventory.HomeInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.EquipedInventory:
                    player.Inventory.EquippedInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.WeaponDrawerInventory:
                    player.Inventory.WeaponDrawer[(int)slotIndex] = 0;    // update slot

                    if (slotIndex == player.ActiveWeapon)
                        player.Inventory.EquippedInventory[13] = 0;       // nothing in hand
                    break;
                default:
                    Console.WriteLine("RemoveItemBySlot: Invalid inventoryType{0}/slotIndex{1}\n", inventoryType, slotIndex);
                    break;
            }

            return slotIndex;
        }

        public void InitForClient(Client client)
        {
            InitCharacterInventory(client);

            // init LockboxTabPermissions
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(client.Player.LockboxTabs));
        }

        /// <summary>
        /// Shows the client the inventory the server already holds for it, after a map change
        /// that made the client forget its entities. Nothing is loaded or registered: the
        /// items and their entity ids are the ones in hand. The dropship arrival used to call
        /// InitForClient here, which loaded the inventory from the database a second time and
        /// registered a second Item entity for every row without destroying the first.
        /// </summary>
        public void ResendToClient(Client client)
        {
            var inventory = client.Player.Inventory;

            ResendList(client, InventoryType.Personal, inventory.PersonalInventory, -1);
            ResendList(client, InventoryType.HomeInventory, inventory.HomeInventory, -1);
            // Slot 13 is the weapon in hand, a mirror of a drawer slot, and is not shown as an
            // equipped item; the login path does not send it either.
            ResendList(client, InventoryType.EquipedInventory, inventory.EquippedInventory, 13);
            ResendList(client, InventoryType.WeaponDrawerInventory, inventory.WeaponDrawer, -1);

            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(client.Player.LockboxTabs));
        }

        private static void ResendList(Client client, InventoryType inventoryType, List<ulong> slots, int skipSlot)
        {
            for (var slot = 0; slot < slots.Count; slot++)
            {
                if (slot == skipSlot || slots[slot] == 0)
                    continue;

                var item = EntityManager.Instance.GetItem(slots[slot]);

                if (item == null)
                {
                    slots[slot] = 0;
                    continue;
                }

                ItemManager.Instance.SendItemDataToClient(client, item, false);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryAddItemPacket(inventoryType, item.EntityId, (uint)slot));
            }

            // it seems  that InventoryCreatePacket dont need to be called, ToDo; investigate more
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.Personal, client.MapClient.Inventory.PersonalInventory, 250));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.HomeInventory, client.MapClient.Inventory.HomeInventory, 480));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.WeaponDrawerInventory, client.MapClient.Inventory.WeaponDrawer, 5));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.EquipedInventory, client.MapClient.Inventory.EquippedInventory, 22));
        }

        public void SetupLocalClanInventory(Client client)
        {
            if (client.Player.ClanId == 0)
                return;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            List<ClanInventoryEntry> getClanInventoryData = unitOfWork.ClanInventories.GetItems(client.Player.ClanId);

            foreach (var item in getClanInventoryData)
            {
                var itemData = unitOfWork.Items.GetItem(item.ItemId);

                if (itemData == null)
                {
                    // A lockbox row whose item is gone. It used to be dereferenced here, which
                    // disconnected every member of the clan at MapLoaded; the row is garbage, so
                    // it is removed and the rest of the lockbox still loads.
                    Logger.WriteLog(LogType.Error, $"Clan {client.Player.ClanId} lockbox slot {item.SlotId} refers to item {item.ItemId}, which does not exist; row removed.");
                    unitOfWork.ClanInventories.DeleteInvItemByItemId(item.ItemId);
                    continue;
                }

                var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemData.ItemTemplateId);

                if (itemTemplate == null)
                {
                    Logger.WriteLog(LogType.Error, $"Item {item.ItemId} has unknown template {itemData.ItemTemplateId}; skipped.");
                    continue;
                }

                Item tempItem = null;

                foreach (var entities in EntityManager.Instance.Items)
                {
                    Item existingItem = entities.Value;

                    if (existingItem.Id == item.ItemId)
                    {
                        tempItem = existingItem;
                    }
                }

                // check if item is weapon
                if (tempItem.ItemTemplate.WeaponInfo != null)
                    tempItem.CurrentAmmo = itemData.AmmoCount;

                // fill invenoty slot
                ItemManager.Instance.SendItemDataToClient(client, tempItem, false);

                AddItemBySlot(client, InventoryType.ClanInventory, tempItem.EntityId, tempItem.OwnerSlotId, false);
            }

            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.ClanInventory, client.Player.Inventory.ClanInventory, 500));
        }

        public void InitClanInventory(Client client)
        {
            for (uint i = 0; i < 500; i++)
                client.Player.Inventory.ClanInventory.Add(0);

            SetupLocalClanInventory(client);
        }

        private static bool IsSlotFree(Client client, InventoryType inventoryType, uint slotId)
        {
            var inventory = client.Player.Inventory;

            return inventoryType switch
            {
                InventoryType.Personal => slotId < inventory.PersonalInventory.Count && inventory.PersonalInventory[(int)slotId] == 0,
                InventoryType.EquipedInventory => slotId < inventory.EquippedInventory.Count && inventory.EquippedInventory[(int)slotId] == 0,
                InventoryType.WeaponDrawerInventory => slotId < inventory.WeaponDrawer.Count && inventory.WeaponDrawer[(int)slotId] == 0,
                _ => false,
            };
        }

        public void InitCharacterInventory(Client client)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var getInventoryData = unitOfWork.CharacterInventories.GetItems(client.AccountEntry.Id);

            // Every character id a row of this account can legitimately carry. Rows are per
            // account; one with a character id outside this set (0 from the home lockbox,
            // a pod number, a depositor on another account) was written by an older build
            // and belongs to nobody, so it would never load again.
            var accountCharacterIds = new HashSet<uint>((client.AccountEntry.Characters ?? new List<CharacterEntry>()).Select(c => c.Id));

            // init for server inventory. Cleared first: this runs again on the manifestation
            // after a summon or .teleport, and used to append another block of slots each
            // time, so the lists grew by 757 entries per zone change.
            client.Player.Inventory.EquippedInventory.Clear();
            client.Player.Inventory.HomeInventory.Clear();
            client.Player.Inventory.PersonalInventory.Clear();
            client.Player.Inventory.WeaponDrawer.Clear();
            client.Player.Inventory.AuctionItems.Clear();

            for (uint i = 0; i < 22; i++)
                client.Player.Inventory.EquippedInventory.Add(0);

            for (uint i = 0; i < 480; i++)
                client.Player.Inventory.HomeInventory.Add(0);

            for (uint i = 0; i < 250; i++)
                client.Player.Inventory.PersonalInventory.Add(0);

            for (uint i = 0; i < 5; i++)
                client.Player.Inventory.WeaponDrawer.Add(0);

            foreach (var item in getInventoryData)
            {
                var itemData = unitOfWork.Items.GetItem(item.ItemId);

                if (itemData == null)
                {
                    // An inventory row whose item is gone (a stack merged away before the
                    // row was deleted, in older builds). Dereferencing it here disconnected the
                    // character at every login; the row is removed and the rest still loads.
                    Logger.WriteLog(LogType.Error, $"Account {client.AccountEntry.Id} inventory {item.InventoryType} slot {item.SlotId} refers to item {item.ItemId}, which does not exist; row removed.");
                    unitOfWork.CharacterInventories.DeleteInvItemByItemId(item.ItemId);
                    continue;
                }

                var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemData.ItemTemplateId);

                if (itemTemplate == null)
                {
                    Logger.WriteLog(LogType.Error, $"Item {item.ItemId} has unknown template {itemData.ItemTemplateId}; skipped.");
                    continue;
                }

                var newItem = new Item
                {
                    OwnerId = item.CharacterId,
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

                // register item
                EntityManager.Instance.RegisterEntity(newItem.EntityId, EntityType.Item);
                EntityManager.Instance.RegisterItem(newItem.EntityId, newItem);

                // fill invenoty slot
                ItemManager.Instance.SendItemDataToClient(client, newItem, false);

                var inventoryType = (InventoryType)item.InventoryType;

                // An orphaned character-inventory row is adopted by the first character on
                // the account to log in with that slot free: the row gets this character's
                // id, and the item is back. If the slot is taken it is left for a later
                // login and reported.
                if (item.CharacterId != client.Player.Id
                    && !accountCharacterIds.Contains(item.CharacterId)
                    && (inventoryType == InventoryType.Personal || inventoryType == InventoryType.EquipedInventory || inventoryType == InventoryType.WeaponDrawerInventory))
                {
                    if (IsSlotFree(client, inventoryType, item.SlotId))
                    {
                        Logger.WriteLog(LogType.Error, $"Account {client.AccountEntry.Id} {inventoryType} slot {item.SlotId} item {item.ItemId} was stored with character id {item.CharacterId}, which is not a character of this account; assigned to {client.Player.Id} ({client.Player.Name}).");
                        unitOfWork.CharacterInventories.MoveInvItem(client.AccountEntry.Id, client.Player.Id, item.InventoryType, item.SlotId, item.ItemId);
                        newItem.OwnerId = client.Player.Id;
                        item.CharacterId = client.Player.Id;
                    }
                    else
                    {
                        Logger.WriteLog(LogType.Error, $"Account {client.AccountEntry.Id} {inventoryType} slot {item.SlotId} item {item.ItemId} was stored with character id {item.CharacterId}, which is not a character of this account, and the slot is taken; left as is.");
                    }
                }

                if (item.CharacterId == client.Player.Id)
                {
                    if ((InventoryType)item.InventoryType == InventoryType.Personal)
                        AddItemBySlot(client, InventoryType.Personal, newItem.EntityId, newItem.OwnerSlotId, false);

                    else if ((InventoryType)item.InventoryType == InventoryType.EquipedInventory)
                        AddItemBySlot(client, InventoryType.EquipedInventory, newItem.EntityId, newItem.OwnerSlotId, false);

                    else if ((InventoryType)item.InventoryType == InventoryType.WeaponDrawerInventory)
                    {
                        // AddItemBySlot sets EquippedInventory[13] itself when this is the
                        // active drawer slot.
                        AddItemBySlot(client, InventoryType.WeaponDrawerInventory, newItem.EntityId, newItem.OwnerSlotId, false);
                    }

                    else if ((InventoryType)item.InventoryType == InventoryType.AuctionInventory)
                    {
                        // Listed at an auction house. SendItemDataToClient above already created
                        // the entity, which is all the client needs to render the row when it
                        // asks for auction status; it belongs in no inventory list it can move
                        // items in, so it only goes in the server's own auction list.
                        client.Player.Inventory.AuctionItems.Add(newItem.EntityId);
                    }
                }
                else if (item.CharacterId == 0)
                {
                    if ((InventoryType)item.InventoryType == InventoryType.HomeInventory)
                    {
                        client.Player.Inventory.HomeInventory[(int)item.SlotId] = newItem.EntityId;
                        // make the item appear on the client
                        AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.HomeInventory[(int)item.SlotId], item.SlotId, false);
                    }
                }

            }

            // character_inventory rows arrive in whatever order the query returns them, and the
            // auction list is shown to the seller in listing order, so put it back in slot order.
            client.Player.Inventory.AuctionItems.Sort((left, right) =>
            {
                var leftItem = EntityManager.Instance.GetItem(left);
                var rightItem = EntityManager.Instance.GetItem(right);

                return (leftItem?.OwnerSlotId ?? 0).CompareTo(rightItem?.OwnerSlotId ?? 0);
            });
        }

        /// <summary>How many items of the entity class the player carries in their personal inventory, all stacks together.</summary>
        public uint CountItemsByClass(Client client, EntityClasses entityClass)
        {
            var total = 0u;

            foreach (var entityId in client.Player.Inventory.PersonalInventory)
            {
                if (entityId == 0)
                    continue;

                var item = EntityManager.Instance.GetItem(entityId);

                if (item?.ItemTemplate != null && item.ItemTemplate.Class == entityClass)
                    total += item.StackSize;
            }

            return total;
        }

        /// <summary>
        /// Takes <paramref name="quantity"/> items of the entity class out of the personal
        /// inventory, smallest stacks first so partial stacks are used up before full ones are
        /// broken. Returns how many it could not take (0 when the player had enough); check with
        /// <see cref="CountItemsByClass"/> first when the whole amount has to be there.
        /// </summary>
        public uint RemoveItemsByClass(Client client, EntityClasses entityClass, uint quantity)
        {
            var stacks = new List<Item>();

            foreach (var entityId in client.Player.Inventory.PersonalInventory)
            {
                if (entityId == 0)
                    continue;

                var item = EntityManager.Instance.GetItem(entityId);

                if (item?.ItemTemplate != null && item.ItemTemplate.Class == entityClass && item.StackSize > 0)
                    stacks.Add(item);
            }

            foreach (var stack in stacks.OrderBy(s => s.StackSize))
            {
                if (quantity == 0)
                    break;

                var take = Math.Min(quantity, stack.StackSize);
                ReduceStackCount(client, InventoryType.Personal, stack, take);
                quantity -= take;
            }

            return quantity;
        }

        public void ReduceStackCount(Client client, InventoryType inventoryType, Item tempItem, uint stackDecreaseCount)
        {
            if (client.Player == null || tempItem == null || stackDecreaseCount == 0)
                return;

            // Ownership is decided by where the entity actually sits: it has to be in this
            // client's list for the inventory named. It used to compare Item.OwnerId (a
            // character id, or 0 for the home lockbox) with AccountEntry.SelectedSlot (1..16),
            // which almost never matched, so destroying an item did nothing - and WeaponReload,
            // which consumes ammo through here, reloaded for free.
            List<ulong> slots;

            switch (inventoryType)
            {
                case InventoryType.Personal:
                    slots = client.Player.Inventory.PersonalInventory;
                    break;
                case InventoryType.HomeInventory:
                    slots = client.Player.Inventory.HomeInventory;
                    break;
                default:
                    return;
            }

            var slotId = slots.IndexOf(tempItem.EntityId);

            if (slotId < 0)
            {
                Logger.WriteLog(LogType.Security, $"{client.Player.Name} tried to reduce item {tempItem.EntityId}, which is not in their {inventoryType}");
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // uint - uint: a count larger than the stack wrapped to ~4 billion instead of
            // emptying it.
            if (stackDecreaseCount >= tempItem.StackSize)
            {
                EntityManager.Instance.DestroyPhysicalEntity(client, tempItem.EntityId, EntityType.Item);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryRemoveItemPacket(inventoryType, tempItem.EntityId));
                FreeSlotIndex(client.Player, inventoryType, (uint)slotId);

                // By item id: the row's character id has been written as the character id, the
                // account's selected slot or 0 depending on which path stored it.
                unitOfWork.CharacterInventories.DeleteInvItemByItemId(tempItem.Id);
                // ToDo will we delete items from db, or we will let tham stay, so thay can be retrived
                //ItemsTable.DeleteItem(tempItem.ItemId);
            }
            else
            {
                tempItem.StackSize -= stackDecreaseCount;
                client.CallMethod(tempItem.EntityId, new SetStackCountPacket(tempItem.StackSize));
                unitOfWork.Items.UpdateItemStackSize(tempItem);
            }
        }

        public void RemoveItemBySlot(Client client, InventoryType inventoryType, uint slotIndex)
        {
            var entityId = 0ul;

            switch (inventoryType)
            {
                case InventoryType.Personal:
                    entityId = client.Player.Inventory.PersonalInventory[(int)slotIndex];
                    client.Player.Inventory.PersonalInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.HomeInventory:
                    entityId = client.Player.Inventory.HomeInventory[(int)slotIndex];
                    client.Player.Inventory.HomeInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.EquipedInventory:
                    entityId = client.Player.Inventory.EquippedInventory[(int)slotIndex];
                    client.Player.Inventory.EquippedInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.WeaponDrawerInventory:
                    entityId = client.Player.Inventory.WeaponDrawer[(int)slotIndex];
                    client.Player.Inventory.WeaponDrawer[(int)slotIndex] = 0;

                    if (slotIndex == client.Player.ActiveWeapon)
                        client.Player.Inventory.EquippedInventory[13] = 0;
                    break;
                case InventoryType.ClanInventory:
                    entityId = client.Player.Inventory.ClanInventory[(int)slotIndex];
                    client.Player.Inventory.ClanInventory[(int)slotIndex] = 0;
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"RemoveItemBySlot: Unsuported Inventory type {inventoryType}");
                    return;
            }

            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryRemoveItemPacket(inventoryType, entityId));
        }

        public void RequestTooltipForItemTemplateId(Client client, uint itemTemplateId)
        {

            var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemTemplateId);
            var classInfo = EntityClassManager.Instance.GetClassInfo(itemTemplate.Class);

            if (itemTemplate == null)
            {
                Logger.WriteLog(LogType.Error, $"RequestTooltipForItemTemplateId: Unknown itemTemplateId {itemTemplateId}");
                return; // todo: even answer on a unknown template, else the client will continue to spam us with requests
            }
            client.CallMethod(SysEntity.ClientGameUIManagerId, new ItemTemplateTooltipInfoPacket(itemTemplate, classInfo));
        }

        public void RequestTooltipForModuleId(Client client, int moduleId)
        {
            Logger.WriteLog(LogType.Debug, $"ToDo: RequestTooltipForModuleId");
            //var moduleInfo = new ItemModule(moduleId, 1, new ModuleInfo(1, 1, 1, 1, 1, 1, 1, 1, 1));

            //client.SendPacket(12, new ModuleTooltipInfoPacket(moduleInfo));
        }

        public bool ValidateItemEquip(Client client, Item itemToEquip)
        {
            var canEquip = true;
            // min level criteria met?
            if (itemToEquip != null)
            {
                // check requirements
                foreach (var requirement in itemToEquip.ItemTemplate.ItemInfo.Requirements)
                {
                    switch (requirement.Key)
                    {
                        case RequirementsType.ReqXpLevel:
                            if (client.Player.Level < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqXpLevel])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Level too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqBody:
                            if (client.Player.Attributes[Attributes.Body].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqBody])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Body attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqMind:
                            if (client.Player.Attributes[Attributes.Mind].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqMind])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Mind attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqSpirit:
                            if (client.Player.Attributes[Attributes.Spirit].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqSpirit])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Spirit attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;

                        case RequirementsType.ReqXpLevelMax:
                            if (client.Player.Level > itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqXpLevelMax])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Level too high, cannot equip item.");
                                canEquip = false;
                            }

                            break;

                        default:
                            Logger.WriteLog(LogType.Error, $"Unknown RequirementsType {requirement.Key}");
                            break;
                    }
                }

                // check race requirements
                if (itemToEquip.ItemTemplate.ItemInfo.RaceReq != 0 && itemToEquip.ItemTemplate.ItemInfo.RaceReq != (int)client.Player.Race)
                {
                    CommunicatorManager.Instance.SystemMessage(client, "Item is not for your race, cannot equip it.");
                    canEquip = false;
                }

                // check skill requrements if it's still true
                if (canEquip)
                    if (itemToEquip.ItemTemplate.EquipableInfo != null)
                    {
                        if (client.Player.Skills.ContainsKey((SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId))
                        {
                            if (client.Player.Skills[(SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId].SkillLevel >= itemToEquip.ItemTemplate.EquipableInfo.SkillLevel)
                                canEquip = true;
                            else
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Skill level to low, cannot equip item.");
                                canEquip = false;
                            }
                        }
                        else
                        {
                            CommunicatorManager.Instance.SystemMessage(client, $"{(SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId} not learned, cannot equip item.");
                            canEquip = false;
                        }
                    }
            }

            return canEquip;
        }

        public void RefreshClanLockbox(uint clanId, ulong entityId, uint characterId, uint slotId, ref List<ulong> clanInventory, bool addBySlot)
        {
            if (addBySlot)
                ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => AddItemBySlot(client, InventoryType.ClanInventory, entityId, slotId, false), characterId);

            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => UpdateItemSlot(client, entityId), characterId);
            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (uint)SysEntity.ClientInventoryManagerId, new ClanInventoryReload(InventoryType.ClanInventory, clanInventory, 500));
        }

        public void RemoveItemBySlotForClan(uint clanId, uint slotId, uint skipThisCharacter)
        {
            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => RemoveItemBySlot(client, InventoryType.ClanInventory, slotId), skipThisCharacter);
        }

        #endregion
    }
}
