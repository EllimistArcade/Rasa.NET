using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.Char.CharacterInventory
{
    using Context.Char;
    using Structures.Char;

    public class CharacterInventoryRepository : ICharacterInventoryRepository
    {
        private readonly CharContext _charContext;

        public CharacterInventoryRepository(CharContext charContext)
        {
            _charContext = charContext;
        }

        public void AddInvItem(uint accountId, uint characterId, uint inventoryType, uint slotId, uint itemId)
        {
            var entry = new CharacterInventoryEntry(accountId, characterId, inventoryType, slotId, itemId);

            try
            {
                _charContext.CharacterInventoryEntries.Add(entry);
                _charContext.SaveChanges();
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Error creating item:");
                Logger.WriteLog(LogType.Error, e);
            }
        }

        public void DeleteInvItem(uint accountId, uint characterId, uint inventoryType, uint slotId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.CharacterInventoryEntries);
            var entry = query.Where(e => e.AccountId == accountId && e.CharacterId == characterId && e.InventoryType == inventoryType && e.SlotId == slotId).FirstOrDefault();

            // Remove(null) throws; a row that is already gone is not an error here.
            if (entry == null)
                return;

            _charContext.Remove(entry);
            _charContext.SaveChanges();
        }

        /// <summary>
        /// Deletes the inventory row for one item, wherever it is. An item has exactly one row
        /// (MoveInvItem relies on that too), so this does not depend on the caller knowing the
        /// character id the row was written with, which has not always been the same thing.
        /// </summary>
        public void DeleteInvItemByItemId(uint itemId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.CharacterInventoryEntries);
            var entry = query.FirstOrDefault(e => e.ItemId == itemId);

            if (entry == null)
                return;

            _charContext.Remove(entry);
            _charContext.SaveChanges();
        }

        /// <summary>
        /// Removes every inventory row of one character - personal, equipped and weapon drawer;
        /// the home lockbox is the account's and carries character id 0 - and returns the item
        /// ids those rows pointed at, so the caller can delete the items too. Staged on the
        /// context, not saved: the caller commits with the character row.
        /// </summary>
        public List<uint> DeleteForCharacter(uint accountId, uint characterId)
        {
            var rows = _charContext.CreateTrackingQuery(_charContext.CharacterInventoryEntries)
                .Where(e => e.AccountId == accountId && e.CharacterId == characterId)
                .ToList();

            _charContext.CharacterInventoryEntries.RemoveRange(rows);

            return rows.Select(r => r.ItemId).ToList();
        }

        public List<CharacterInventoryEntry> GetItems(uint accountId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.CharacterInventoryEntries);
            var characterInventoryEntries = query.Where(e => e.AccountId == accountId).ToList();

            return characterInventoryEntries;
        }

        /// <summary>
        /// Whether the item's inventory row belongs to this account and either to this character
        /// or to the account's home lockbox (character id 0). MoveInvItem finds its row by item id
        /// alone and rewrites the owner, so a slot move for an item the caller does not hold in the
        /// database - one handed to another player, or one whose row is gone - would otherwise
        /// take that row over.
        /// </summary>
        public bool IsHeldBy(uint itemId, uint accountId, uint characterId)
        {
            var query = _charContext.CreateNoTrackingQuery(_charContext.CharacterInventoryEntries);

            return query.Any(e => e.ItemId == itemId && e.AccountId == accountId
                                  && (e.CharacterId == characterId || e.CharacterId == 0));
        }

        public void MoveInvItem(uint accountId, uint characterId, uint inventoryType, uint slotId, uint itemId)
        {
            var invItem = _charContext.CreateTrackingQuery(_charContext.CharacterInventoryEntries).FirstOrDefault(e => e.ItemId == itemId);

            if (invItem == null)
            {
                Logger.WriteLog(LogType.Error, $"Item {itemId} has no inventory row; move skipped.");
                return;
            }

            invItem.AccountId = accountId;
            invItem.CharacterId = characterId;
            invItem.SlotId = slotId;
            invItem.InventoryType = inventoryType;
            _charContext.SaveChanges();
        }
    }
}
