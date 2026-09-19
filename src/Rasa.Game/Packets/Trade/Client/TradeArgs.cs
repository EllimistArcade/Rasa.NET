namespace Rasa.Packets.Trade.Client
{
    using Memory;

    internal static class TradeArgs
    {
        /// <summary>
        /// Reads a Python integer the client may have marshalled as an int or a long. Entity ids
        /// the server sent with WriteULong come back as longs; configuration ids and credit
        /// amounts are plain ints unless they outgrow 32 bits.
        /// </summary>
        public static long ReadInteger(PythonReader pr)
        {
            switch (pr.PeekType())
            {
                case PythonType.Long:
                    return pr.ReadLong();
                case PythonType.Structs:
                    pr.ReadUnkStruct();
                    return 0;
                default:
                    return pr.ReadInt();
            }
        }
    }
}
