using System;
using System.Collections.Generic;

namespace Rasa.Memory
{
    public static class BufferManager
    {
        private static readonly Stack<BufferData> BufferDatas = new Stack<BufferData>();

        public static byte[] Buffer { get; private set; }
        public static int BlockSize { get; private set; }

        public static void Initialize(int blockSize, int maxClients, int concurrentOperationsByClient)
        {
            // Already initialized
            if (Buffer != null)
                return;

            if ((long)blockSize * maxClients * concurrentOperationsByClient > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize * maxClients * concurrentOperationsByClient, "Can not alloc an array this big!");

            BlockSize = blockSize;

            // Block Size * Max Client count * concurrentOperationsByClient:
            // The block size is the size of the buffer
            // The max client count * concurrentOperationsByClient makes sure that there is enough buffers for every client for receive and concurrent send operations
            Buffer = new byte[BlockSize * maxClients * concurrentOperationsByClient];

            // It's a stack, and we should preferable use the buffers at the beginning, so we put it in in reverse order
            lock (BufferDatas)
                for (var i = Buffer.Length - BlockSize; i >= 0; i -= BlockSize)
                    BufferDatas.Push(new BufferData(i));
        }

        /// <summary>
        /// Takes a buffer from the pool, or returns null when there are none left.
        /// </summary>
        /// <remarks>
        /// Running out is a load problem, not a programming error. This used to throw, and the
        /// throw happened on whichever socket thread happened to ask, where nothing caught it and
        /// it took the process down - so one client arriving at a bad moment could end everyone
        /// else's session. The caller drops the one connection it was about to serve instead,
        /// which hands back that connection's buffers and lets the server carry on.
        /// </remarks>
        public static BufferData RequestBuffer()
        {
            BufferData data;

            // The emptiness check belongs inside the lock with the pop it guards: sends are now
            // issued from several threads at once, so two callers seeing one buffer left would
            // both go on to pop and the second would throw out of Stack itself.
            lock (BufferDatas)
            {
                if (BufferDatas.Count == 0)
                    return null;

                data = BufferDatas.Pop();
            }

            data.Reset();
            data.Free = false;

            return data;
        }

        public static void FreeBuffer(BufferData data)
        {
            lock (BufferDatas)
            {
                // A buffer freed twice would sit in the pool twice and be handed to two
                // connections at once, each writing over the other's packet. Whatever freed it
                // twice is the bug; quietly sharing the buffer is the damage.
                if (data.Free)
                    return;

                data.Reset();
                data.Free = true;

                BufferDatas.Push(data);
            }
        }
    }
}
