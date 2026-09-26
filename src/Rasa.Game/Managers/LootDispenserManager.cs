using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Communicator.Server;
    using Packets.LootDispenser.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Rasa.Packets.ClientMethod.Server;
    using Rasa.Packets.Game.Server;
    using Rasa.Packets.LootDispenser.Client;
    using Structures;

    public class LootDispenserManager
    {
        /*      LootDispenser Packets
         * - LootInfo(self, lootItems):
         * - AttachInfo(self, attachedEntityId):
         * - OverallQuality(self, overallQualityId):
         * - CanLootItems(self, isLootable, canLootPerItem):
         * - TakenInfo(self, takenItems):
         * - ActorGotLoot(self, actorId, lootEntityIds):
         * - Use(self, actorId, curStateId, * args):
         * - LootCorpse(self, actorId, lootItems):
         * 
         *      LootDispenser Handlers:
         * - RequestCorpseLooting (self.entityId,)
         * - CancelCorpseLooting (self.entityId,)
         * - RequestLootAllFromCorpse (self.entityId, autoLootOnly)
         * - RequestLootItemFromCorpse (self.entityId, itemId, destSlot)
         */

        private static LootDispenserManager _instance;
        private static readonly object InstanceLock = new object();

        public static LootDispenserManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new LootDispenserManager();
                    }
                }

                return _instance;
            }
        }

        private LootDispenserManager()
        {
        }

        internal void AttachInfo(Client client, LootDispenser loot)
        {
            client.CallMethod(loot.EntityId, new AttachInfoPacket(loot.AttachedTo));
        }

        internal void LootInfo(Client client, LootDispenser loot)
        {
            client.CallMethod(loot.EntityId, new LootInfoPacket(loot.LootItems));
        }

        internal void OverallQuality(Client client, LootDispenser loot)
        {
            client.CallMethod(loot.EntityId, new OverallQualityPacket(loot.LootQuality));
        }

        /// <summary>
        /// What this looter may take: the corpse window draws only the items named here
        /// (IsItemLootable), so a squad mate's rolled item, or under Rotation the holder's, is
        /// left out of everyone else's.
        /// </summary>
        internal void CanLootItems(Client client, LootDispenser loot)
        {
            client.CallMethod(loot.EntityId, new CanLootItemsPacket(loot.IsLootable, LootableBy(loot, client.Player.EntityId)));
        }

        /// <summary>The corpse's items this manifestation may take (LootItem.MayTake).</summary>
        public static List<LootItem> LootableBy(LootDispenser loot, ulong entityId) =>
            loot.LootItems.FindAll(i => i.MayTake(entityId));

        /// <summary>What one take gave this player - the items they took and their share of the credits - if anything.</summary>
        internal void GotLoot(Client client, LootDispenser loot, List<LootItem> items, int credits)
        {
            if ((items == null || items.Count == 0) && credits <= 0)
                return;

            client.CallMethod(SysEntity.ClientMethodId, new GotLootPacket(loot.AttachedTo, items, credits));
        }

        /// <summary>
        /// A corpse's credits divided among those sharing them: an equal share each, the odd
        /// credits one apiece from the first on (the looter who emptied the corpse is first).
        /// Nobody is left out of a share for being last; a pot smaller than the squad gives the
        /// first ones a credit each and the rest nothing.
        /// </summary>
        public static List<int> SplitCredits(int credits, int recipients)
        {
            var shares = new List<int>();

            if (recipients <= 0)
                return shares;

            var each = Math.Max(0, credits) / recipients;
            var odd = Math.Max(0, credits) % recipients;

            for (var i = 0; i < recipients; i++)
                shares.Add(each + (i < odd ? 1 : 0));

            return shares;
        }

        /// <summary>
        /// Who the credits of an emptied corpse go to: the looter who emptied it, first, and the
        /// rest of the squad that shared in the kill (CreditSharers) who are still in the world on
        /// this map. A corpse that was one player's goes to that player.
        /// </summary>
        public static List<Client> CreditRecipients(Client taker, LootDispenser loot)
        {
            var recipients = new List<Client> { taker };
            var mapChannel = taker.Player?.MapChannel;

            if (mapChannel == null || loot.CreditSharers.Count == 0)
                return recipients;

            foreach (var entityId in loot.CreditSharers)
            {
                if (entityId == taker.Player.EntityId)
                    continue;

                var sharer = mapChannel.ClientList.Find(c => c?.Player != null && c.Player.EntityId == entityId
                    && c.State == ClientState.Ingame && c.Player.MapContextId == taker.Player.MapContextId);

                if (sharer != null && !recipients.Contains(sharer))
                    recipients.Add(sharer);
            }

            return recipients;
        }

        /// <summary>How long a corpse with nothing left on it stays in the world.</summary>
        public const long EmptyCorpseMs = 20000;

        /// <summary>How long a corpse that still has something on it stays.</summary>
        public const long LootableCorpseMs = 120000;

        /// <summary>How long a corpse someone has the window open on stays, whatever else is true.</summary>
        public const long BeingLootedCorpseMs = 300000;

        /// <summary>
        /// Whether a dead creature can leave the world yet.
        ///
        /// The rule was twenty seconds from the moment its health hit zero, full stop - no regard
        /// for loot still on it or for a player standing over it with the window open. Twenty
        /// seconds is about one more fight, so a corpse routinely vanished between the kill and
        /// the looting, and the window went with it.
        /// </summary>
        internal bool MayDespawn(MapChannel mapChannel, Creature creature, long deadTime)
        {
            if (mapChannel == null || creature.CorpseLootEntityId == 0
                || !mapChannel.LootDispensers.TryGetValue(creature.CorpseLootEntityId, out var loot))
                return deadTime >= EmptyCorpseMs;

            // Someone has it open. Their window closing clears this, and the cap is there so a
            // player who walks away with it open cannot hold a corpse for ever.
            if (loot.CurrentLooter != 0)
                return deadTime >= BeingLootedCorpseMs;

            if (loot.HasLoot)
                return deadTime >= LootableCorpseMs;

            return deadTime >= EmptyCorpseMs;
        }

        internal LootDispenser Create(Client killer, Creature creature)
        {
            return Create(killer, creature, new List<Client> { killer }, 0);
        }

        /// <summary>
        /// The dispenser for a corpse, owned by the first of the looters - the killer, or the squad
        /// member whose turn it is - and open to all of them. partyId marks its items as the
        /// squad's (Free For All).
        /// </summary>
        internal LootDispenser Create(Client killer, Creature creature, List<Client> looters, uint partyId)
        {
            var mapChannel = killer.Player.MapChannel;
            var owner = looters.Count > 0 ? looters[0] : killer;
            var loot = new LootDispenser();
            loot.IsLootable = true;
            loot.AttachedTo = creature.EntityId;
            loot.Owner = owner.Player.EntityId;

            foreach (var looter in looters)
                loot.Looters.Add(looter.Player.EntityId);

            loot.Looters.Add(loot.Owner);

            CreateLoot(owner, loot, partyId);

            mapChannel.LootDispensers.Add(loot.EntityId, loot);

            // So the despawn check can find it without walking every dispenser on the map on
            // every creature on every tick.
            creature.CorpseLootEntityId = loot.EntityId;

            return loot;
        }

        /// <summary>
        /// One Random, not one per call. Three `new Random()` in a row seeded from the clock gave
        /// three values from the same tick, so the quality tracked the item count.
        /// </summary>
        private static readonly Random Roll = new Random();

        private LootDispenser CreateLoot(Client killer, LootDispenser loot, uint partyId = 0)
        {
            int giveLoot;

            lock (Roll)
            {
                giveLoot = Roll.Next(0, 2);
                loot.Credits = Roll.Next(1, 10);
                loot.LootQuality = (LootQuality)Roll.Next(1, 7);
            }

            if (giveLoot > 0)
            {
                // A real item, made now rather than at the moment it is taken. The corpse window
                // resolves every row to an entity and silently drops the ones it cannot
                // (corpselootwindow: GetEntity(itemId), continue on None), so a row without an
                // item behind it is an empty window. It also means what is taken is what was
                // rolled, rather than a second item built from the same template.
                var item = ItemManager.Instance.CreateFromTemplateId(28, (uint)giveLoot * 3);

                if (item != null)
                    loot.LootItems.Add(new LootItem(item, killer.Player.EntityId, partyId));
            }

            return loot;
        }

        /// <summary>
        /// The corpse's loot, for whoever the killer's squad loot method gives it to
        /// (PartyManager.LootersFor): each of them is shown the dispenser.
        /// </summary>
        internal void Loot(Client client, Creature creature)
        {
            var (looters, partyId, party, eligible) = PartyManager.Instance.LootersFor(client, creature.Position);
            var loot = Create(client, creature, looters, partyId);

            // Everyone who shared in the kill shares in its credits, whatever the method does
            // with the items.
            if (party != null && eligible.Count > 1)
                foreach (var member in eligible)
                    loot.CreditSharers.Add(member.Player.EntityId);

            // What is at or over the squad's threshold is rolled for among everyone sharing in
            // the corpse; a winner the method had left out is shown it too.
            var shownTo = new List<Client>(looters);

            foreach (var winner in LootRolls.Distribute(loot, party, eligible))
                if (!shownTo.Contains(winner))
                    shownTo.Add(winner);

            foreach (var looter in shownTo)
            {
                looter.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(loot.EntityId, loot.EntityClassId));

                AttachInfo(looter, loot);
                LootInfo(looter, loot);
                OverallQuality(looter, loot);
                CanLootItems(looter, loot);
            }
        }

        /// <summary>The looters of a dispenser who are on this map now.</summary>
        private static List<Client> LootersHere(MapChannel mapChannel, LootDispenser loot)
        {
            return mapChannel?.ClientList.FindAll(c => c?.Player != null && loot.Looters.Contains(c.Player.EntityId)) ?? new List<Client>();
        }

        /// <summary>
        /// Drops every dispenser attached to a creature that is leaving the world: out of the
        /// map's table, off the owner's screen if they are still here, and its entity id freed.
        /// </summary>
        internal void RemoveForCreature(MapChannel mapChannel, Creature creature)
        {
            List<ulong> attached = null;

            foreach (var entry in mapChannel.LootDispensers)
                if (entry.Value.AttachedTo == creature.EntityId)
                    (attached ??= new List<ulong>()).Add(entry.Key);

            if (attached == null)
                return;

            List<uint> unclaimedRows = null;

            foreach (var lootEntityId in attached)
            {
                var loot = mapChannel.LootDispensers[lootEntityId];

                mapChannel.LootDispensers.Remove(lootEntityId);

                // Everyone it was shown to: the owner, and a Free For All squad.
                var shownTo = LootersHere(mapChannel, loot);

                foreach (var looter in shownTo)
                    looter.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(lootEntityId));

                // The rolled items are real entities now, made when the loot was rolled rather
                // than when it is taken, so a corpse that goes unlooted takes them with it.
                // Without this they would sit in RegisteredEntities for the life of the process
                // and their ids would never come back.
                foreach (var lootItem in loot.LootItems)
                {
                    if (lootItem.Taken || lootItem.Item == null)
                        continue;

                    foreach (var looter in shownTo)
                        looter.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(lootItem.EntityId));

                    // The row's id is the item's, and CreateItem registered it in
                    // RegisteredEntities as well as Items. Freeing it while it was still
                    // registered handed the id to the next entity created, and registering
                    // that one threw "An item with the same key has already been added" out
                    // of the map worker - from the next loot roll, or the next spawn.
                    EntityManager.Instance.UnregisterEntity(lootItem.EntityId);
                    EntityManager.Instance.UnregisterItem(lootItem.EntityId);
                    EntityManager.Instance.FreeEntity(lootItem.EntityId);

                    if (lootItem.Item.Id != 0)
                        (unclaimedRows ??= new List<uint>()).Add(lootItem.Item.Id);
                }

                EntityManager.Instance.FreeEntity(lootEntityId);
            }

            DeleteUnclaimedRows(unclaimedRows);
        }

        /// <summary>
        /// Deletes the items-table rows of rolled loot that nobody took.
        ///
        /// CreateItem writes the row when the loot is rolled - the corpse window needs a real item
        /// behind every line it draws - and a take moves the item, row and all, into the looter's
        /// inventory. An item left on a corpse that despawned, was eaten, filched or revived had
        /// its entity freed above, but its row stayed: no inventory row points at it and nothing
        /// ever reads it, and at one rolled item in every two kills the table grew by thousands a
        /// day for the life of the database. One unit of work for the corpse; a database error
        /// is logged and costs only the rows, never the despawn.
        /// </summary>
        private static void DeleteUnclaimedRows(List<uint> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
                return;

            try
            {
                using var unitOfWork = Server.GameUnitOfWorkFactory.CreateChar();

                unitOfWork.Items.DeleteItems(itemIds);
                unitOfWork.Complete();
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not delete the rows of {itemIds.Count} unlooted item(s) ({string.Join(", ", itemIds)}): {e}");
            }
        }

        /// <summary>
        /// The dispenser this packet names, if it is on the player's map and they may loot it.
        /// Every one of these came in indexed straight off the dictionary, so an id for a corpse
        /// on another map - or one that has already been cleaned up - was a KeyNotFoundException
        /// out of the handler, which closes the connection.
        /// </summary>
        private static LootDispenser FindLootable(Client client, ulong entityId)
        {
            var dispensers = client?.Player?.MapChannel?.LootDispensers;

            if (dispensers == null || !dispensers.TryGetValue(entityId, out var loot))
                return null;

            // Loot belongs to whoever earned it - the killer, the squad member whose turn it was,
            // or a Free For All squad. The client only offers a corpse it was told about, but the
            // packet can name any id.
            if (!loot.Looters.Contains(client.Player.EntityId))
                return null;

            return loot;
        }

        /// <summary>
        /// The client has used a corpse and wants the window. Answering with LootCorpse is what
        /// opens it; while this was a stub, nothing ever did, which is why the two per-item
        /// methods had never been reachable.
        /// </summary>
        internal void RequestCorpseLooting(Client client, RequestCorpseLootingPacket packet)
        {
            var loot = FindLootable(client, packet.EntityId);

            if (loot == null || loot.FullyLooted)
                return;

            var remaining = loot.Remaining();

            // The window draws a row only for an item it can resolve to an entity, so the items
            // have to exist on the client before it opens. Re-sending one it already has is
            // harmless: clientmethod.Recv_CreatePhysicalEntity treats a repeat of the same class
            // as an update.
            foreach (var lootItem in remaining)
                if (lootItem.Item != null)
                    ItemManager.Instance.SendItemDataToClient(client, lootItem.Item, false);

            loot.CurrentLooter = client.Player.EntityId;

            LootInfo(client, loot);
            CanLootItems(client, loot);

            client.CallMethod(loot.EntityId, new LootCorpsePacket(client.Player.EntityId, remaining));
        }

        /// <summary>
        /// The window has closed. The client sends this on any close, not only a deliberate
        /// cancel, and expects no answer - so this only lets go of the corpse.
        /// </summary>
        internal void CancelCorpseLooting(Client client, CancelCorpseLootingPacket packet)
        {
            var dispensers = client?.Player?.MapChannel?.LootDispensers;

            if (dispensers == null || !dispensers.TryGetValue(packet.EntityId, out var loot))
                return;

            // Only the player who has it open may close it.
            if (loot.CurrentLooter == client.Player.EntityId)
                loot.CurrentLooter = 0;
        }

        /// <summary>Takes one item off a corpse.</summary>
        internal void RequestLootItemFromCorpse(Client client, RequestLootItemFromCorpsePacket packet)
        {
            var loot = FindLootable(client, packet.EntityId);

            if (loot == null || loot.FullyLooted)
                return;

            var lootItem = loot.Find(packet.ItemId);

            // Already gone, or never on this corpse. Say so rather than ignoring it: the row is
            // still on the asking player's screen until TakenInfo tells them otherwise.
            if (lootItem == null || lootItem.Taken)
            {
                client.CallMethod(loot.EntityId, new TakenInfoPacket(client.Player.EntityId, Taken(loot)));
                return;
            }

            // Someone else's: a squad mate won it, or Rotation gave the corpse to someone else.
            // The window never showed it; the list of what this player may take goes again.
            if (!lootItem.MayTake(client.Player.EntityId))
            {
                CanLootItems(client, loot);
                return;
            }

            if (!TakeItem(client, loot, lootItem, packet.DestSlot))
                return;

            Finish(client, loot, new List<LootItem> { lootItem });
        }

        /// <summary>
        /// The client sends this two ways and they are not the same request.
        ///
        /// The Loot All button sends autoLootOnly false: take everything.
        ///
        /// Walking near a corpse sends it with autoLootOnly **true**, from lootdispenser's
        /// _UpdateTick, which runs every frame while a dispenser is attached and fires the
        /// moment the player is inside the corpse's auto-loot radius. Nobody clicked anything.
        /// That one means "take what I said I would pick up automatically", which is items at or
        /// below the player's auto-loot threshold - Junk by default, since that is what the
        /// client's own option defaults to.
        ///
        /// Treating them alike is why looting felt random: walking over a body silently emptied
        /// it, so the window either never opened or opened onto a corpse that had already been
        /// cleared out from under it.
        /// </summary>
        internal void RequestLootAllFromCorpse(Client client, RequestLootAllFromCorpsePacket packet)
        {
            var loot = FindLootable(client, packet.EntityId);

            if (loot == null || loot.FullyLooted)
                return;

            var threshold = client.Player?.AutoLootThreshold ?? LootQuality.Junk;
            var taken = new List<LootItem>();

            // Through the same path as taking one at a time, so both keep the same books. It
            // used to build a second item from each template and add that, leaving the rolled
            // items behind and nothing marked as taken - and since FullyLooted was never set
            // either, the same corpse paid out again on every request.
            foreach (var lootItem in loot.Remaining())
            {
                if (packet.AutoLootOnly && !WithinThreshold(lootItem, threshold))
                    continue;

                // Only what is theirs to take: a squad mate's rolled item stays for them.
                if (!lootItem.MayTake(client.Player.EntityId))
                    continue;

                if (TakeItem(client, loot, lootItem, null))
                    taken.Add(lootItem);
            }

            // Credits come along either way: they have no quality to weigh against a threshold,
            // and leaving a handful behind would keep an otherwise empty corpse standing.
            Finish(client, loot, taken);
        }

        /// <summary>
        /// The end of a take: pays the credits if the corpse is now empty, then tells the looter
        /// what the take gave them (GotLoot, with the items and their share), each other sharer
        /// their share, and the rest of the squad what was taken (PartyMemberLoot) and who got
        /// credits they did not share in.
        /// </summary>
        private void Finish(Client client, LootDispenser loot, List<LootItem> taken)
        {
            var shares = Settle(client, loot);
            var ownShare = 0;

            foreach (var (recipient, share) in shares)
                if (recipient == client)
                    ownShare = share;

            GotLoot(client, loot, taken, ownShare);

            foreach (var (recipient, share) in shares)
                if (recipient != client)
                    GotLoot(recipient, loot, null, share);

            PartyManager.Instance.AnnounceLoot(client, loot.AttachedTo, taken, ownShare);
            // A corpse the squad shared: the members left out of its credits hear who got them.
            if (loot.CreditSharers.Count > 0)
                PartyManager.Instance.AnnounceCredits(shares);
        }

        /// <summary>Whether walking past a corpse should pick this item up unasked.</summary>
        private static bool WithinThreshold(LootItem lootItem, LootQuality threshold)
        {
            var quality = (LootQuality)(lootItem.Item?.ItemTemplate?.QualityId ?? 0);

            // Rank, not the raw id: the ids are the client's and Junk is the largest of them.
            return quality.Rank() <= threshold.Rank();
        }

        /// <summary>
        /// The best quality this player's client will pick up by walking over a corpse. Sent at
        /// login and whenever the option changes; it was a logged ToDo, so every player was
        /// treated as if they had asked for everything.
        /// </summary>
        internal void SetAutoLootThreshold(Client client, SetAutoLootThresholdPacket packet)
        {
            if (client.Player == null)
                return;

            var threshold = (LootQuality)packet.LootLevel;

            // The client only ever sends one of its five option values, but the packet is the
            // client's word: an unknown one would rank as int.MaxValue and auto-loot everything.
            if (!Enum.IsDefined(typeof(LootQuality), threshold) || threshold == LootQuality.Mission)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry?.Id} sent auto-loot threshold {packet.LootLevel}, which is not a quality.");
                return;
            }

            client.Player.AutoLootThreshold = threshold;
        }

        /// <summary>
        /// Moves one rolled item into the player's inventory. False if it would not fit, in which
        /// case the item stays on the corpse rather than disappearing between the two.
        /// </summary>
        private bool TakeItem(Client client, LootDispenser loot, LootItem lootItem, uint? destSlot)
        {
            if (lootItem.Taken || lootItem.Item == null || !lootItem.MayTake(client.Player.EntityId))
                return false;

            var placed = destSlot.HasValue
                ? InventoryManager.Instance.AddItemToInventory(client, lootItem.Item, destSlot.Value)
                : InventoryManager.Instance.AddItemToInventory(client, lootItem.Item);

            if (placed == null)
            {
                client.CallMethod(SysEntity.CommunicatorId,
                    new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return false;
            }

            lootItem.Taken = true;

            // No ActorGotLoot: its only effect is the pick-up sound, which the GotLoot this take
            // ends with plays as well (Finish).

            // Everyone sharing the corpse sees the row go, not only the one who took it.
            foreach (var looter in LootersHere(client.Player.MapChannel, loot))
                looter.CallMethod(loot.EntityId, new TakenInfoPacket(client.Player.EntityId, Taken(loot)));

            return true;
        }

        /// <summary>
        /// Pays out the credits and closes the corpse once nothing is left on it. The credits are
        /// split among CreditRecipients; returns who was paid what, empty while the corpse still
        /// holds items.
        /// </summary>
        private List<(Client Recipient, int Share)> Settle(Client client, LootDispenser loot)
        {
            var paidOut = new List<(Client, int)>();

            // Items only. The credits are paid out *by* this method, so asking whether the corpse
            // still holds anything - which counts them - would be circular: the credits would
            // keep the corpse open, and nothing would ever pay them.
            if (loot.LootItems.Exists(i => !i.Taken))
            {
                // Still something on it: refresh what can be taken and leave it open.
                CanLootItems(client, loot);
                return paidOut;
            }

            if (loot.Credits > 0)
            {
                var recipients = CreditRecipients(client, loot);
                var shares = SplitCredits(loot.Credits, recipients.Count);

                for (var i = 0; i < recipients.Count; i++)
                {
                    if (shares[i] <= 0)
                        continue;

                    CharacterManager.Instance.UpdateCharacter(recipients[i], CharacterUpdate.Credits, shares[i]);
                    paidOut.Add((recipients[i], shares[i]));
                }

                loot.Credits = 0;
            }

            loot.FullyLooted = true;
            loot.IsLootable = false;
            loot.CurrentLooter = 0;

            // Emptied: nobody sharing it has anything left to open.
            foreach (var looter in LootersHere(client.Player.MapChannel, loot))
                if (looter != client)
                    CanLootItems(looter, loot);

            CanLootItems(client, loot);

            return paidOut;
        }

        /// <summary>The rows TakenInfo should mark; the client keys off the ones it is sent.</summary>
        private static List<LootItem> Taken(LootDispenser loot) => loot.LootItems.FindAll(i => i.Taken);
    }
}
