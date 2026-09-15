using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Packets.Inventory.Server;
    using Packets.MapChannel.Server;
    using Rasa.Game;
    using Rasa.Packets.MapChannel.Client;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;

    /// <summary>
    /// The auction house: listing items, showing the seller their own auctions, and taking a
    /// listing back down. Bidding, buyout, expiry and the browse tab are still stubs.
    ///
    /// A listed item keeps its items row and its character_inventory row - the row's type just
    /// becomes AuctionInventory - so the item survives a restart exactly as a lockbox item does,
    /// and its entity is created on the client at login by the usual inventory load. The auction
    /// table holds only what the auction adds on top: seller, price, deposit and start time.
    /// </summary>
    public class AuctionHouseManager
    {
        /*    AuctionHouse Packets:
         *  - AuctionCreationFailed     => done
         *  - AuctionCreationSuccess    => done
         *  - QuerySuccess
         *  - QueryFailed
         *  - AuctionStatusSuccess      => done
         *  - AuctionStatusFailed       => not used by client
         *  - AuctionBuyoutFailed
         *  - AuctionBuyoutSuccess
         *  - AuctionSold
         *  - CancelAuctionFailed       => done
         *  - CancelAuctionSuccess      => done
         *  - AuctionExpired
         *
         *  AuctionHouse Handlers:
         *  - RequestAuctionBuyout
         *  - RequestAuctionStatus      => done
         *  - RequestCancelAuction      => done
         *  - RequestCancelAuctioneer   => done
         *  - RequestCreateAuction      => done
         *  - RequestQueryAuctions
         *
         */

        private static AuctionHouseManager _instance;
        private static readonly object InstanceLock = new object();

        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        /// <summary>
        /// Deposit charged to list an item, in tenths of a percent, by the duration the seller
        /// picked. These are the client's own figures: it reads them from auctionhouse.durationdata
        /// and shows the result in the Deposit field before the player presses Create, so the
        /// server has to charge the same or the window lies. 12h costs 0.5%, three days 2.5%.
        /// </summary>
        private static readonly uint[] DepositTenthsOfPercent = { 5, 10, 20, 25 };

        /// <summary>Hours each duration id runs for: 12 hours, then one, two and three days.</summary>
        private static readonly uint[] DurationHours = { 12, 24, 48, 72 };

        public static AuctionHouseManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new AuctionHouseManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private AuctionHouseManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        #region Handlers

        public void RequestAuctionBuyout(Client client, RequestAuctionBuyoutPacket packet)
        {
            Logger.WriteLog(LogType.AI, $"ToDo: RequestAuctionBuyout ");
        }

        /// <summary>
        /// Fills the "My Auctions" tab. The client asks for this every time the tab is opened,
        /// and replaces its whole auction dictionary with what comes back.
        /// </summary>
        public void RequestAuctionStatus(Client client, RequestAuctionStatusPacket packet)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var auctions = unitOfWork.Auctions.GetAuctionsBySeller(client.Player.Id);
            var now = DateTime.UtcNow;
            var rows = new List<AuctionStatus>();

            foreach (var auction in auctions)
            {
                var entityId = FindAuctionedEntityId(client, auction.ItemId);

                if (entityId == 0)
                {
                    // The client looks the row up by entity id; one it has never been told about
                    // renders as a blank line it cannot cancel. Better to leave it out and say so.
                    Logger.WriteLog(LogType.Error, $"Auction for item {auction.ItemId} has no entity on character {client.Player.Id}; not listed.");
                    continue;
                }

                rows.Add(new AuctionStatus(entityId, auction.Price, auction.RemainingHours(now)));
            }

            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, new AuctionStatusSuccessPacket(rows));
        }

        /// <summary>Takes a listing down and puts the item back in the seller's pack. The deposit is not refunded.</summary>
        public void RequestCancelAuction(Client client, RequestCancelAuctionPacket packet)
        {
            var item = EntityManager.Instance.GetItem(packet.ItemEntityId);

            if (item == null || !client.Player.Inventory.AuctionItems.Contains(packet.ItemEntityId))
            {
                FailCancel(client, packet.ItemEntityId, PlayerMessage.PmAuctionItemNotFound);
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var auction = unitOfWork.Auctions.GetAuctionByItemId(item.Id);

            if (auction == null || auction.SellerId != client.Player.Id)
            {
                FailCancel(client, packet.ItemEntityId, PlayerMessage.PmAuctionItemNotFound);
                return;
            }

            var slotId = FindFreePersonalSlot(client, item);

            if (slotId < 0)
            {
                // Nothing here has changed yet, so the auction simply stays up.
                FailCancel(client, packet.ItemEntityId, PlayerMessage.PmAuctionInternalError);
                Logger.WriteLog(LogType.Debug, $"Character {client.Player.Id} cancelled an auction with no free inventory slot for the item.");
                return;
            }

            unitOfWork.Auctions.DeleteAuction(item.Id);
            client.Player.Inventory.AuctionItems.Remove(packet.ItemEntityId);

            client.CallMethod(SysEntity.ClientInventoryManagerId, new RemoveAuctionItemPacket(packet.ItemEntityId));

            item.OwnerId = client.Player.Id;
            item.OwnerSlotId = (uint)slotId;

            // The row already exists from when the item was listed, so this is a move, not an
            // insert - AddItemBySlot with actuallyAdd false updates it in place.
            InventoryManager.Instance.AddItemBySlot(client, InventoryType.Personal, item.EntityId, (uint)slotId, true);

            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, new CancelAuctionSuccessPacket(packet.ItemEntityId));
        }

        /// <summary>
        /// The client sends this when the auction window closes. The server keeps no auction
        /// house session - every request carries the auctioneer it came from - so there is
        /// nothing to tear down, and answering it at all would be wrong: the client has already
        /// forgotten the auctioneer by the time it sends this.
        /// </summary>
        public void RequestCancelAuctioneer(Client client)
        {
            Logger.WriteLog(LogType.Debug, $"Character {client.Player.Id} closed the auction house.");
        }

        public void RequestCreateAuction(Client client, RequestCreateAuctionPacket packet)
        {
            var item = EntityManager.Instance.GetItem(packet.ItemEntityId);
            var slotId = FindPersonalSlotOf(client, packet.ItemEntityId);

            if (item == null || slotId < 0)
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionCouldNotFindItem);
                return;
            }

            if (packet.Price == 0)
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionInvalidPriceSet);
                return;
            }

            if (packet.Duration >= DurationHours.Length)
            {
                // Not a duration the window offers, so the deposit the player was shown is not
                // one this server can reproduce.
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionInvalidPriceSet);
                Logger.WriteLog(LogType.Error, $"Character {client.Player.Id} asked for auction duration {packet.Duration}, which does not exist.");
                return;
            }

            if (!CanBeAuctioned(item))
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionItemCannotBeAuctioned);
                return;
            }

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);

            if (itemClassInfo != null && item.CurrentHitPoints < itemClassInfo.MaxHitPoints)
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionItemNeedsRepair);
                return;
            }

            if (client.Player.Inventory.AuctionItems.Count >= Inventory.MaxAuctionItems)
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionMaxAuctions);
                return;
            }

            var deposit = CalculateDeposit(item, packet.Price, packet.Duration);

            if (client.Player.Credits[CurencyType.Credits] < deposit)
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionNotEnoughCreditsForDeposit);
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var auction = new AuctionEntry(item.Id, client.Player.Id, client.Player.Name, packet.Price,
                deposit, DurationHours[packet.Duration]);

            // Written before anything is charged or moved: if the item is somehow already listed,
            // the player keeps their credits and their item.
            if (!unitOfWork.Auctions.CreateAuction(auction))
            {
                Fail(client, packet.ItemEntityId, PlayerMessage.PmAuctionInternalError);
                return;
            }

            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, -(int)deposit);

            // Out of the pack, into the auction inventory. RemoveItemBySlot does no database work
            // of its own, so the row is moved here.
            InventoryManager.Instance.RemoveItemBySlot(client, InventoryType.Personal, (uint)slotId);

            var auctionSlot = NextAuctionSlot(client);
            client.Player.Inventory.AuctionItems.Add(item.EntityId);
            item.OwnerSlotId = auctionSlot;

            unitOfWork.CharacterInventories.MoveInvItem(client.AccountEntry.Id, client.Player.Id,
                (uint)InventoryType.AuctionInventory, auctionSlot, item.Id);

            client.CallMethod(SysEntity.ClientInventoryManagerId, new AddAuctionItemPacket(item.EntityId));
            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, new AuctionCreationSuccessPacket(item.EntityId));
        }

        public void RequestQueryAuctions(Client client, RequestQueryAuctionsPacket packet)
        {
            Logger.WriteLog(LogType.AI, $"ToDo: RequestQueryAuctions");
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// What listing an item costs, by the client's own formula:
        ///
        ///     minDepositPrice = max(buyoutPrice, itemValue)
        ///     deposit         = max(int(depositRatio * minDepositPrice), 1)
        ///
        /// where itemValue is the vendor buyback price times the stack size, and depositRatio is
        /// the duration's tenths of a percent over a thousand. Done in whole numbers here: the
        /// client's float arithmetic floors to the same credit, and a deposit that came out one
        /// higher than the window showed would be rejected as unaffordable by a player who had
        /// exactly enough.
        /// </summary>
        public static uint CalculateDeposit(Item item, uint price, uint durationId)
        {
            if (durationId >= DepositTenthsOfPercent.Length)
                return 0;

            var stackSize = Math.Max(item.StackSize, 1u);
            var itemValue = (uint)Math.Max(item.ItemTemplate.SellPrice, 0) * stackSize;
            var basis = Math.Max(price, itemValue);
            var deposit = (ulong)basis * DepositTenthsOfPercent[durationId] / 1000;

            return deposit < 1 ? 1 : (uint)Math.Min(deposit, uint.MaxValue);
        }

        /// <summary>
        /// The same three rules the create window filters the item list by, so the server agrees
        /// with what the player was offered.
        ///
        /// Note ItemInfo.Tradable is misnamed: ItemManager fills it from the template's
        /// NotTradableFlag, and ItemInfoPacket writes it into the client's notTradable field, so
        /// the two inversions cancel on the wire and true here means *not* tradable.
        /// </summary>
        public static bool CanBeAuctioned(Item item)
        {
            if (item?.ItemTemplate == null)
                return false;

            if (item.ItemTemplate.BoundToCharacter)
                return false;

            if (!item.ItemTemplate.HasSellableFlag)
                return false;

            return item.ItemTemplate.ItemInfo == null || !item.ItemTemplate.ItemInfo.Tradable;
        }

        /// <summary>Tells the create window why it could not list the item.</summary>
        private static void Fail(Client client, ulong itemEntityId, PlayerMessage message)
        {
            client.CallMethod(SysEntity.ClientAuctionHouseManagerId,
                new AuctionCreationFailedPacket(itemEntityId, message));
        }

        /// <summary>
        /// Tells the My Auctions tab why it could not take a listing down. A separate packet from
        /// Fail: the client routes the two to different handlers, and a cancel answered with
        /// AuctionCreationFailed puts the message in the wrong window.
        /// </summary>
        private static void FailCancel(Client client, ulong itemEntityId, PlayerMessage message)
        {
            client.CallMethod(SysEntity.ClientAuctionHouseManagerId,
                new CancelAuctionFailedPacket(itemEntityId, message));
        }

        /// <summary>Which personal inventory slot an entity sits in, or -1 if it is not in the pack.</summary>
        private static int FindPersonalSlotOf(Client client, ulong entityId)
        {
            var inventory = client.Player.Inventory.PersonalInventory;

            for (var i = 0; i < inventory.Count; i++)
                if (inventory[i] == entityId)
                    return i;

            return -1;
        }

        /// <summary>
        /// A free personal slot in the item's own category block, or -1 when that block is full.
        /// Categories are fifty slots each and an item only goes in its own, the same rule
        /// AddItemToInventory follows.
        /// </summary>
        private static int FindFreePersonalSlot(Client client, Item item)
        {
            var categoryOffset = (int)item.ItemTemplate.InventoryCategory - 1;

            if (categoryOffset < 0 || categoryOffset >= 5)
                return -1;

            categoryOffset *= 50;

            var inventory = client.Player.Inventory.PersonalInventory;

            for (var i = 0; i < 50; i++)
                if (categoryOffset + i < inventory.Count && inventory[categoryOffset + i] == 0)
                    return categoryOffset + i;

            return -1;
        }

        /// <summary>
        /// The lowest slot number no auctioned item is using. Cancelling takes an item out of
        /// the middle of the list, so the next free slot is not the same thing as the count.
        /// </summary>
        private static uint NextAuctionSlot(Client client)
        {
            var used = new HashSet<uint>();

            foreach (var entityId in client.Player.Inventory.AuctionItems)
            {
                var item = EntityManager.Instance.GetItem(entityId);

                if (item != null)
                    used.Add(item.OwnerSlotId);
            }

            for (var slot = 0u; slot < Inventory.MaxAuctionItems; slot++)
                if (!used.Contains(slot))
                    return slot;

            return (uint)Inventory.MaxAuctionItems;
        }

        /// <summary>The entity id of one of this character's auctioned items, by its database id.</summary>
        private static ulong FindAuctionedEntityId(Client client, uint itemId)
        {
            foreach (var entityId in client.Player.Inventory.AuctionItems)
            {
                var item = EntityManager.Instance.GetItem(entityId);

                if (item != null && item.Id == itemId)
                    return entityId;
            }

            return 0;
        }

        #endregion
    }
}
