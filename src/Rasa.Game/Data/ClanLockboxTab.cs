namespace Rasa.Data
{
    /// <summary>
    /// The clan lockbox tabs, from the client's own <c>shared.gameconstants</c>:
    /// <c>CLAN_LOCKBOX_TAB_PRICES = {1: None, 2: 50000, 3: 100000, 4: 500000, 5: 1000000}</c>,
    /// with <c>DEFAULT_CLAN_INVENTORY_SIZE = 500</c> split evenly across the five, so 100 slots
    /// each and the first one free.
    ///
    /// **Paid in prestige, and the clan's, not the buyer's.** The confirmation the window puts up
    /// says so in as many words - *"Would you like to spend %(price)s of your clan's prestige to
    /// unlock tab %(tabId)s for your clan?"* - and the funds it checks before sending are the
    /// prestige figure on the lockbox window, which is the clan's balance. The refusal is
    /// PmInsufficientFundsToPurchaseClanLockboxTab: *"Insufficient prestige to purchase clan
    /// lockbox tab."*
    ///
    /// The prices have to match the client's to the point, for the same reason the personal
    /// lockbox's do: the window quotes the number and checks it before anything is sent.
    /// </summary>
    public static class ClanLockboxTab
    {
        /// <summary>The tab a clan has from the day it is founded.</summary>
        public const uint FreeTab = 1;

        /// <summary>NUM_CLAN_LOCKBOX_TABS. The client ignores a count above this outright.</summary>
        public const uint LastTab = 5;

        /// <summary>CLAN_LOCKBOX_TAB_SIZE: 500 slots over five tabs.</summary>
        public const uint SlotsPerTab = 100;

        /// <summary>DEFAULT_CLAN_INVENTORY_SIZE.</summary>
        public const uint TotalSlots = LastTab * SlotsPerTab;   // 500

        private static readonly uint[] Prices = { 0, 50000, 100000, 500000, 1000000 };

        /// <summary>Whether <paramref name="tabId"/> is a tab the client knows about.</summary>
        public static bool Exists(uint tabId) => tabId >= FreeTab && tabId <= LastTab;

        /// <summary>The prestige <paramref name="tabId"/> costs, or 0 for the free tab.</summary>
        public static uint Price(uint tabId) => Exists(tabId) ? Prices[tabId - FreeTab] : 0;

        /// <summary>
        /// The slots <paramref name="tabId"/> covers, as a half-open [first, last) range. Tab 1
        /// is the first hundred and so on, which is how the client lays them out
        /// (inventory._GetSlotRangeForClanLockboxTab).
        /// </summary>
        public static (uint First, uint Last) SlotRange(uint tabId)
        {
            if (!Exists(tabId))
                return (0, 0);

            var first = (tabId - FreeTab) * SlotsPerTab;

            return (first, first + SlotsPerTab);
        }

        /// <summary>
        /// How many clan lockbox slots a clan holding <paramref name="purchasedTabs"/> tabs may
        /// use. The client hides the rest; this is what stops them being addressed anyway.
        /// </summary>
        public static uint UnlockedSlots(uint purchasedTabs)
        {
            if (purchasedTabs < FreeTab)
                return FreeTab * SlotsPerTab;

            return purchasedTabs > LastTab ? TotalSlots : purchasedTabs * SlotsPerTab;
        }
    }
}
