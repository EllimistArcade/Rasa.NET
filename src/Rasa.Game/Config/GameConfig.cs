namespace Rasa.Config
{
    public class GameConfig
    {
        public string PublicAddress { get; set; }
        public int Port { get; set; }
        public int Backlog { get; set; }

        /// <summary>
        /// How often the world loop's metrics go out to GM clients, in milliseconds. Zero turns
        /// it off, which is the default: the 1.16.5.0 client takes the message and does nothing
        /// with it, because GameClient::SetServerPerfMetrics is an empty stub in the retail
        /// build - see ServerPerformanceMetricsPacket. Turn it on for a client that implements
        /// it. 'perf' on the console reads the same numbers either way.
        /// </summary>
        public int PerformanceMetricsInterval { get; set; }
    }
}
