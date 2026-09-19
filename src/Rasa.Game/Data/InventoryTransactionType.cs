namespace Rasa.Data
{
    /// <summary>
    /// What a clan lockbox log line records, from the client's
    /// <c>generated.client.constant.inventorytransactiontype</c>. The client turns each of these
    /// into its own word - withdrawn, deposited, destroyed, tab purchase - and logs an error and
    /// prints an empty line for anything it does not recognise.
    /// </summary>
    public static class InventoryTransactionType
    {
        public const byte Withdrawal = 1;
        public const byte Deposit = 2;
        public const byte Deletion = 3;
        public const byte TabPurchase = 4;
    }
}
