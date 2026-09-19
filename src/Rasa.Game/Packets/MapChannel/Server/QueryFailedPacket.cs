namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// No results, or a search the server could not run. The client shows the message and then
    /// clears its result list, so this is also how an empty search is answered.
    /// </summary>
    public class QueryFailedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.QueryFailed;

        public PlayerMessage PlayerMessage { get; set; }

        public QueryFailedPacket(PlayerMessage playerMessage)
        {
            PlayerMessage = playerMessage;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt((uint)PlayerMessage);
        }
    }
}
