namespace Rasa.Packets.Crafting.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_CraftingSuccess / Recv_CraftingFailure / Recv_CraftingCatastrophicFailure(manifestationId)
    /// on the Kraftwerks entity. The client queues them and shows the matching player message when
    /// the window next updates, so one of these should follow every request it made.
    /// </summary>
    public class CraftingResultPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; }

        public ulong ManifestationId { get; }

        private CraftingResultPacket(GameOpcode opcode, ulong manifestationId)
        {
            Opcode = opcode;
            ManifestationId = manifestationId;
        }

        public static CraftingResultPacket Success(ulong manifestationId) => new CraftingResultPacket(GameOpcode.CraftingSuccess, manifestationId);
        public static CraftingResultPacket Failure(ulong manifestationId) => new CraftingResultPacket(GameOpcode.CraftingFailure, manifestationId);
        public static CraftingResultPacket CatastrophicFailure(ulong manifestationId) => new CraftingResultPacket(GameOpcode.CraftingCatastrophicFailure, manifestationId);

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteULong(ManifestationId);
        }
    }
}
