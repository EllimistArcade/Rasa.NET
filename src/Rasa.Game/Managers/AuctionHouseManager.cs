using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Memory;
    using Packets.Inventory.Server;
    using Packets.MapChannel.Server;
    using Rasa.Game;
    using Rasa.Packets.MapChannel.Client;
    using Repositories.Char;
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
         *  - QuerySuccess             => done
         *  - QueryFailed              => done
         *  - AuctionStatusSuccess      => done
         *  - AuctionStatusFailed       => not used by client
         *  - AuctionBuyoutFailed      => done
         *  - AuctionBuyoutSuccess     => done
         *  - AuctionSold              => done
         *  - CancelAuctionFailed       => done
         *  - CancelAuctionSuccess      => done
         *  - AuctionExpired           => done
         *
         *  AuctionHouse Handlers:
         *  - RequestAuctionBuyout      => done
         *  - RequestAuctionStatus      => done
         *  - RequestCancelAuction      => done
         *  - RequestCancelAuctioneer   => done
         *  - RequestCreateAuction      => done
         *  - RequestQueryAuctions      => done
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

        /// <summary>
        /// Buys an auction outright. The item goes to the buyer's inbox, not their pack: the
        /// client has a Pick Up Items tab for exactly this, and it is the only delivery that
        /// works when the pack is full. The seller is paid into their character row, so the
        /// money reaches them whether or not they are logged in.
        /// </summary>
        public void RequestAuctionBuyout(Client client, RequestAuctionBuyoutPacket packet)
        {
            var item = EntityManager.Instance.GetItem(packet.ItemId);

            if (item == null)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionItemNotFound);
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var auction = unitOfWork.Auctions.GetAuctionByItemId(item.Id);

            if (auction == null)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionItemNotFound);
                return;
            }

            if (auction.SellerId == client.Player.Id)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionCannotPurchaseOwnItem);
                return;
            }

            // The price the buyer agreed to, checked against the price on the row: a listing can
            // only be bought for what it says, however stale the window they clicked in.
            if (packet.Price != auction.Price)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionPendingTransaction);
                return;
            }

            if (client.Player.Credits[CurencyType.Credits] < auction.Price)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionInsufficientFunds);
                return;
            }

            if (client.Player.Inventory.InboxItems.Count >= Inventory.MaxInboxItems)
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionNoBuyoutInboxFull);
                return;
            }

            // Delivered before anyone is charged: if the inbox will not take it, nothing else
            // has happened yet and the auction is still standing.
            if (!InventoryManager.Instance.DeliverToInbox(unitOfWork, client.AccountEntry.Id, client.Player.Id, item))
            {
                BuyoutFailed(client, packet.ItemId, PlayerMessage.PmAuctionNoBuyoutInboxFull);
                return;
            }

            unitOfWork.Auctions.DeleteAuction(item.Id);
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, -(int)auction.Price);
            PaySeller(unitOfWork, auction);
            RemoveFromSellersAuctionList(auction.SellerId, item.EntityId, auction.Price);

            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, new AuctionBuyoutSuccessPacket(item.EntityId));
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

        /// <summary>
        /// The browse tab's search. Every query carries a category - the client keeps its search
        /// button disabled until one is picked - plus a level range and one exact quality, since
        /// its rarity box has no "any" entry.
        /// </summary>
        public void RequestQueryAuctions(Client client, RequestQueryAuctionsPacket packet)
        {
            if (!AuctionCategory.Names.TryGetValue(packet.CategoryId, out var category))
            {
                QueryFailed(client, PlayerMessage.PmAuctionNoResultsFound);
                Logger.WriteLog(LogType.Error, $"Auction search for category {packet.CategoryId}, which is not a category the client offers.");
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var now = DateTime.UtcNow;
            var matches = new List<(AuctionEntry Auction, Item Item, int Level)>();

            foreach (var auction in unitOfWork.Auctions.GetAuctions())
            {
                if (auction.SellerId == client.Player.Id)
                    continue;               // the browse tab is for other people's auctions

                var item = FindAuctionedItem(auction);

                if (item?.ItemTemplate == null)
                    continue;

                if (item.ItemTemplate.QualityId != (int)packet.QualityId)
                    continue;

                var level = LevelRequirementOf(item);

                if (level < packet.MinLevel || level > packet.MaxLevel)
                    continue;

                if (!MatchesCategory(item, category))
                    continue;

                matches.Add((auction, item, level));
            }

            if (matches.Count == 0)
            {
                QueryFailed(client, PlayerMessage.PmAuctionNoResultsFound);
                return;
            }

            var reply = new QuerySuccessPacket();

            // The envelope: tuple + list header. Rows are added while they still fit.
            //
            // Every match used to go into this one reply. A reply is written into a single pool
            // block, so past about a hundred rows the write threw inside Send, LengthedSocket
            // logged that it was skipping the packet, and the searcher was left looking at an
            // empty browse tab - the busier the category, the more certainly it returned nothing.
            var size = PythonSize.Of(pw => reply.Write(pw));

            // Cheapest first, because that is the half of a full category a buyer wants, and
            // because an item priced out of the page today comes into it as the ones under it
            // sell. By auction id - which is the order they arrive in - the newest listings would
            // be the ones nobody could ever see.
            foreach (var match in matches.OrderBy(m => m.Auction.Price))
            {
                var modules = match.Item.ItemTemplate.ItemInfo?.ModuleIds ?? new List<int>();

                var row = new AuctionItem
                {
                    ItemId = (uint)match.Item.EntityId,
                    Sellername = match.Auction.SellerName,
                    BidPrice = 0,
                    BuyoutPrice = match.Auction.Price,
                    RemainingDuration = match.Auction.RemainingHours(now),
                    ItemTemplateId = match.Item.ItemTemplate.ItemTemplateId,
                    StackSize = match.Item.StackSize,
                    LootModuleId1 = modules.Count > 0 ? (uint)modules[0] : 0,
                    LootModuleId2 = modules.Count > 1 ? (uint)modules[1] : 0,
                    LootModuleId3 = modules.Count > 2 ? (uint)modules[2] : 0,
                    LootModuleId4 = modules.Count > 3 ? (uint)modules[3] : 0,
                    QualitiId = (uint)match.Item.ItemTemplate.QualityId,
                    LevelRequirement = (uint)match.Level
                };

                var rowSize = PythonSize.Of(row);

                if (size + rowSize + PythonSize.ListHeaderSlack > PythonSize.PayloadBudget)
                    break;

                size += rowSize;

                // The buyer has never seen this item, so its entity has to exist on their client
                // before the row can draw a name, an icon or a tooltip. Only for the rows that go:
                // a search of a busy category used to create an entity on the searcher's client
                // for every match, including the hundreds that were never in the reply.
                ItemManager.Instance.SendItemDataToClient(client, match.Item, false);

                reply.AuctionItemList.Add(row);
            }

            if (reply.AuctionItemList.Count < matches.Count)
                Logger.WriteLog(LogType.Debug,
                    $"Auction search by {client.Player.FamilyName} matched {matches.Count} auctions in {category}; the {reply.AuctionItemList.Count} cheapest fit the reply.");

            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, reply);
        }

        /// <summary>
        /// Returns every auction past its duration to the seller's inbox. Called on a timer and
        /// again when a character logs in, which is what catches auctions that ran out while the
        /// server was down.
        /// </summary>
        public void ExpireAuctions()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var now = DateTime.UtcNow;

            foreach (var auction in unitOfWork.Auctions.GetAuctions())
            {
                if (auction.RemainingHours(now) > 0)
                    continue;

                Expire(unitOfWork, auction);
            }
        }

        /// <summary>Expires just this character's auctions, on their way into the world.</summary>
        public void ExpireAuctionsFor(Client client)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var now = DateTime.UtcNow;

            foreach (var auction in unitOfWork.Auctions.GetAuctionsBySeller(client.Player.Id))
            {
                if (auction.RemainingHours(now) > 0)
                    continue;

                Expire(unitOfWork, auction);
            }
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

        /// <summary>
        /// Moves one expired auction's item back to its seller's inbox and deletes the row. An
        /// inbox that is full leaves the auction standing rather than destroying the item - it
        /// is simply retried on the next sweep, once the seller has made room.
        /// </summary>
        private void Expire(ICharUnitOfWork unitOfWork, AuctionEntry auction)
        {
            var item = FindAuctionedItem(auction);

            if (item == null)
            {
                Logger.WriteLog(LogType.Error, $"Auction on item {auction.ItemId} has expired but its item is gone; row left in place.");
                return;
            }

            var accountId = AccountOf(auction.SellerId, unitOfWork, item);

            if (!InventoryManager.Instance.DeliverToInbox(unitOfWork, accountId, auction.SellerId, item))
            {
                Logger.WriteLog(LogType.Debug, $"Auction on item {auction.ItemId} has expired but {auction.SellerName}'s inbox is full; it will be retried.");
                return;
            }

            unitOfWork.Auctions.DeleteAuction(auction.ItemId);
            RemoveFromSellersAuctionList(auction.SellerId, item.EntityId, null);
        }

        /// <summary>
        /// Adds the proceeds to the seller's character row. Done through CharacterManager when
        /// they are online so their purse and their client agree, and straight to the row when
        /// they are not - the money has to arrive either way.
        /// </summary>
        private static void PaySeller(ICharUnitOfWork unitOfWork, AuctionEntry auction)
        {
            var seller = OnlineSeller(auction.SellerId);

            if (seller != null)
            {
                CharacterManager.Instance.UpdateCharacter(seller, CharacterUpdate.Credits, (int)auction.Price);
                return;
            }

            var character = unitOfWork.Characters.Get(auction.SellerId);

            if (character == null)
            {
                Logger.WriteLog(LogType.Error, $"Auction on item {auction.ItemId} sold but seller {auction.SellerId} no longer exists; proceeds dropped.");
                return;
            }

            unitOfWork.Characters.UpdateCharacterCredits(auction.SellerId, character.Credit + (int)auction.Price);
        }

        /// <summary>
        /// Takes the item out of the seller's own auction list and tells their client, so a
        /// seller who is standing at an auction house sees the listing go. A price means it
        /// sold; no price means it ran out.
        /// </summary>
        private static void RemoveFromSellersAuctionList(uint sellerId, ulong entityId, uint? soldFor)
        {
            var seller = OnlineSeller(sellerId);

            if (seller == null)
                return;

            seller.Player.Inventory.AuctionItems.Remove(entityId);
            seller.CallMethod(SysEntity.ClientInventoryManagerId, new RemoveAuctionItemPacket(entityId));
            seller.CallMethod(SysEntity.ClientAuctionHouseManagerId, soldFor.HasValue
                ? (Rasa.Packets.PythonPacket)new AuctionSoldPacket(entityId, soldFor.Value)
                : new AuctionExpiredPacket(entityId));
        }

        private static Client OnlineSeller(uint sellerId) =>
            Server.Clients.Find(c => c?.Player != null && c.Player.Id == sellerId && c.State == ClientState.Ingame);

        /// <summary>
        /// The live Item behind an auction row. A seller who is logged in has it in their
        /// auction list; otherwise it is whichever registered item carries that database id.
        /// </summary>
        private static Item FindAuctionedItem(AuctionEntry auction)
        {
            var seller = OnlineSeller(auction.SellerId);

            if (seller != null)
                foreach (var entityId in seller.Player.Inventory.AuctionItems)
                {
                    var held = EntityManager.Instance.GetItem(entityId);

                    if (held != null && held.Id == auction.ItemId)
                        return held;
                }

            foreach (var entry in EntityManager.Instance.Items)
                if (entry.Value != null && entry.Value.Id == auction.ItemId)
                    return entry.Value;

            return null;
        }

        /// <summary>
        /// Which account the seller's rows belong to. Their character_inventory rows are keyed by
        /// account, and an offline seller has no Client to read it from.
        /// </summary>
        private static uint AccountOf(uint sellerId, ICharUnitOfWork unitOfWork, Item item)
        {
            var seller = OnlineSeller(sellerId);

            if (seller != null)
                return seller.AccountEntry.Id;

            var character = unitOfWork.Characters.Get(sellerId);

            return character?.AccountId ?? 0;
        }

        /// <summary>The experience level an item asks for, or zero when it asks for none.</summary>
        private static int LevelRequirementOf(Item item)
        {
            if (item.ItemTemplate?.ItemInfo?.Requirements == null)
                return 0;

            return item.ItemTemplate.ItemInfo.Requirements.TryGetValue(RequirementsType.ReqXpLevel, out var level)
                ? level
                : 0;
        }

        /// <summary>
        /// Whether an item belongs to one of the browse tab's categories. The category name is a
        /// path through the item class naming scheme, so "Weapon_Pistol" wants a class whose
        /// name starts with Weapon and carries Pistol as one of its underscore-separated words -
        /// Weapon_Avatar_Pistol_Physical_CMN_01_to_04 and not Weapon_Avatar_Rifle_Physical.
        /// </summary>
        public static bool MatchesCategory(Item item, string category)
        {
            if (string.IsNullOrEmpty(category) || item?.ItemTemplate == null)
                return false;

            if (!EntityClassManager.Instance.LoadedEntityClasses.TryGetValue(item.ItemTemplate.Class, out var entityClass))
                return false;

            var className = entityClass?.ClassName;

            if (string.IsNullOrEmpty(className))
                return false;

            var parts = category.Split('_');
            var words = className.Split('_');

            if (words.Length == 0 || !words[0].Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                return false;

            for (var i = 1; i < parts.Length; i++)
                if (!words.Any(word => word.Equals(parts[i], StringComparison.OrdinalIgnoreCase)))
                    return false;

            return true;
        }

        private static void BuyoutFailed(Client client, ulong itemEntityId, PlayerMessage message)
        {
            client.CallMethod(SysEntity.ClientAuctionHouseManagerId,
                new AuctionBuyoutFailedPacket(itemEntityId, message));
        }

        private static void QueryFailed(Client client, PlayerMessage message)
        {
            client.CallMethod(SysEntity.ClientAuctionHouseManagerId, new QueryFailedPacket(message));
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
