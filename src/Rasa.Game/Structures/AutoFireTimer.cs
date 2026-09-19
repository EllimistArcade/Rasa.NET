namespace Rasa.Structures
{
    using Game;

    public class AutoFireTimer
    {
        /// <summary>
        /// How long the server keeps firing without a keep-alive from the client, in ms. The
        /// client resends AutoFireKeepAlive about every 2.5 s; this was never initialised, so
        /// the timer expired on its first tick and holding fire gave exactly one shot.
        /// </summary>
        public const long DefaultMaxAliveTime = 10000;

        public Client Client { get; set; }
        public long RefireTime { get; set; }
        public long MaxAliveTime { get; set; } = DefaultMaxAliveTime;
        public long Delay { get; set; }

        public AutoFireTimer(Client client, long refireTime, long delay)
        {
            Client = client;
            RefireTime = refireTime;
            Delay = delay;
        }
    }
}
