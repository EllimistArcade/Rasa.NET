namespace Rasa.Data
{
    /// <summary>
    /// The home lockbox ("footlocker") tabs, from the client's own
    /// <c>generated.client.lockboxtabdata.lookup</c>, which is <c>{tabId: (price, size)}</c>:
    /// five tabs of 96 slots, the first free and the rest at 100 K, 1 M, 10 M and 100 M credits.
    ///
    /// The prices have to match the client's exactly. Its purchase window quotes the price in
    /// PmFootlockerPurchaseTabConfirmation and checks the player's funds against it before it
    /// sends anything, so a server charging a different number would either refuse somebody who
    /// had been told they could afford it, or take a sum nobody agreed to.
    /// </summary>
    public static class LockboxTab
    {
        /// <summary>The tab every character has without paying for it.</summary>
        public const int FreeTab = 1;

        /// <summary>The highest tab that exists. The client draws no widget past it.</summary>
        public const int LastTab = 5;

        /// <summary>Slots per tab. Every tab in the table is the same size.</summary>
        public const int SlotsPerTab = 96;

        /// <summary>Home inventory size: every tab's slots, whether or not they are unlocked.</summary>
        public const int TotalSlots = LastTab * SlotsPerTab;   // 480

        private static readonly int[] Prices = { 0, 100000, 1000000, 10000000, 100000000 };

        /// <summary>Whether <paramref name="tabId"/> is a tab this client knows about at all.</summary>
        public static bool Exists(int tabId) => tabId >= FreeTab && tabId <= LastTab;

        /// <summary>
        /// What <paramref name="tabId"/> costs to unlock, or 0 for the free tab. Callers check
        /// <see cref="Exists"/> first; an unknown tab has no price and returns 0.
        /// </summary>
        public static int Price(int tabId) => Exists(tabId) ? Prices[tabId - FreeTab] : 0;

        /// <summary>
        /// How many home inventory slots a character with <paramref name="purchasedTabs"/> tabs
        /// may address. Slots past this belong to tabs the client will not even draw.
        /// </summary>
        public static int UnlockedSlots(int purchasedTabs)
        {
            if (purchasedTabs < FreeTab)
                return FreeTab * SlotsPerTab;

            return purchasedTabs > LastTab ? TotalSlots : purchasedTabs * SlotsPerTab;
        }
    }
}
