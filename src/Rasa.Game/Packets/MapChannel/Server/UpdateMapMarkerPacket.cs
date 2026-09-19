namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// One marker's state, changed while the player is standing there.
    ///
    /// <c>Recv_UpdateMapMarker</c> posts its UI event with the entity id as the source, so the
    /// client refreshes that marker alone rather than redrawing the map - which is the difference
    /// between this and MapMarkerInfo, and the reason both exist.
    /// </summary>
    public class UpdateMapMarkerPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.UpdateMapMarker;

        public ulong MarkerEntityId { get; }
        public MapMarkerState State { get; }

        public UpdateMapMarkerPacket(ulong markerEntityId, MapMarkerState state)
        {
            MarkerEntityId = markerEntityId;
            State = state;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteULong(MarkerEntityId);
            State.Write(pw);
        }
    }
}
