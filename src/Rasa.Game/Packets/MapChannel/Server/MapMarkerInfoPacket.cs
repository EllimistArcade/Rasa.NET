using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Every status marker on the map the player is standing in, as
    /// <c>{markerEntityId: stateData}</c>.
    ///
    /// <c>Recv_MapMarkerInfo</c> replaces the client's whole dictionary and refreshes every
    /// marker, and <c>ClearMapMarkerInfo</c> empties it on each map change - so this is per-map
    /// state that has to be sent again every time the player arrives somewhere, including on the
    /// way back to a map they have already been to.
    ///
    /// It is also per-player: the flag that makes this worth sending is whether *this* character
    /// has found the waypoint, which is why it is a server push and not more client data.
    /// </summary>
    public class MapMarkerInfoPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MapMarkerInfo;

        public Dictionary<ulong, MapMarkerState> Markers { get; }

        public MapMarkerInfoPacket(Dictionary<ulong, MapMarkerState> markers)
        {
            Markers = markers;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteDictionary(Markers.Count);

            foreach (var marker in Markers)
            {
                pw.WriteULong(marker.Key);
                marker.Value.Write(pw);
            }
        }
    }
}
