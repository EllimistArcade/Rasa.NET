using System.Collections.Generic;

namespace Rasa.Structures
{
    using Game;

    /// <summary>
    /// One trade between two players, from the invite until it completes or is cancelled.
    /// </summary>
    public class TradeSession
    {
        public Client Initiator { get; }
        public Client Target { get; }

        /// <summary>False while the invite is pending; true once the trade window is open.</summary>
        public bool Accepted { get; set; }

        /// <summary>
        /// Bumped on every change to the terms. The client echoes the last one it saw in
        /// RequestConfirmTrade, so a confirmation that raced a change is refused rather than
        /// accepted for terms the player never looked at.
        /// </summary>
        public int ConfigurationId { get; set; }

        public int InitiatorCredits { get; set; }
        public int TargetCredits { get; set; }
        public bool InitiatorConfirmed { get; set; }
        public bool TargetConfirmed { get; set; }

        /// <summary>Environment.TickCount64 when the invite was sent.</summary>
        public long CreatedTick { get; } = System.Environment.TickCount64;

        public TradeSession(Client initiator, Client target)
        {
            Initiator = initiator;
            Target = target;
        }

        /// <summary>
        /// What each side has put up, in the order it was offered. The trade window holds five a
        /// side (shared/gameconstants.py DEFAULT_TRADE_INVENTORY_SIZE), and the items stay in
        /// their owner's inventory until the exchange - nothing is escrowed, so a trade that
        /// falls over cannot strand anything.
        /// </summary>
        public List<OfferedItem> InitiatorItems { get; } = new List<OfferedItem>();
        public List<OfferedItem> TargetItems { get; } = new List<OfferedItem>();

        public bool IsInitiator(Client client) => client == Initiator;

        public List<OfferedItem> ItemsOf(Client client) => client == Initiator ? InitiatorItems : TargetItems;

        public Client PartnerOf(Client client) => client == Initiator ? Target : Initiator;

        public int CreditsOf(Client client) => client == Initiator ? InitiatorCredits : TargetCredits;

        public void SetCredits(Client client, int amount)
        {
            if (client == Initiator)
                InitiatorCredits = amount;
            else
                TargetCredits = amount;
        }

        public bool IsConfirmed(Client client) => client == Initiator ? InitiatorConfirmed : TargetConfirmed;

        public void SetConfirmed(Client client, bool confirmed)
        {
            if (client == Initiator)
                InitiatorConfirmed = confirmed;
            else
                TargetConfirmed = confirmed;
        }

        /// <summary>
        /// One item on the table, as it was when it was put there.
        ///
        /// The entity id alone was not enough to hold anyone to their offer. What the other
        /// player reads before they confirm is the stack size and the condition, and neither is
        /// fixed while the window is open: a stack can be split, spent on a reload, sold in part
        /// at a vendor, fed to a crafting job or destroyed down to one, and none of those go
        /// anywhere near the trade handlers, so none of them clears a confirmation. A hundred
        /// rounds offered, confirmed by the buyer, then destroyed down to one and confirmed by
        /// the seller handed over the one - the exchange took the item as it stood at that
        /// moment. Complete measures the live item against this.
        /// </summary>
        public class OfferedItem
        {
            public ulong EntityId { get; }

            /// <summary>The size of the stack when it was offered.</summary>
            public uint StackSize { get; }

            /// <summary>Its condition when it was offered; the window shows it as a percentage.</summary>
            public int CurrentHitPoints { get; }

            public OfferedItem(Item item)
            {
                EntityId = item.EntityId;
                StackSize = item.StackSize;
                CurrentHitPoints = item.CurrentHitPoints;
            }

            /// <summary>Whether the item is still what was offered, rather than what is left of it.</summary>
            public bool Matches(Item item)
            {
                return item != null && item.StackSize == StackSize && item.CurrentHitPoints == CurrentHitPoints;
            }
        }
    }
}
