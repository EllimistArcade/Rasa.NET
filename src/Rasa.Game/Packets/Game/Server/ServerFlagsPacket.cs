using System.Collections.Generic;

namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The whole set of flags that are on, which the client takes as its new set outright:
    /// serverflagmanager.Recv_ServerFlags does g_serverFlags = list(serverFlags).
    ///
    /// That makes it the only message that can turn a flag off, since Recv_ClearServerFlag
    /// raises before it gets that far - see ClearServerFlagPacket.
    /// </summary>
    public class ServerFlagsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ServerFlags;

        public IReadOnlyCollection<ServerFlag> Flags { get; }

        public ServerFlagsPacket(IReadOnlyCollection<ServerFlag> flags)
        {
            Flags = flags;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteTuple(Flags.Count);

            foreach (var flag in Flags)
                pw.WriteUInt((uint)flag);
        }
    }
}
