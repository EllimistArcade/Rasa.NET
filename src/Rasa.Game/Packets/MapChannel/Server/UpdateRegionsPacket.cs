using System.Collections.Generic;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// UpdateRegions(regionIdList): the regions of the current map the player is standing in.
    /// The client appends the map's default region itself, looks each id up in the .map region
    /// table (ignoring ids it does not know), sorts by priority and applies the ambient sound,
    /// music, environment map, sky, display name and cavern minimap of the winners.
    /// </summary>
    public class UpdateRegionsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.UpdateRegions;

        public IReadOnlyList<uint> RegionIds { get; }

        public UpdateRegionsPacket(IReadOnlyList<uint> regionIds)
        {
            RegionIds = regionIds ?? new List<uint>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(RegionIds.Count);

            foreach (var regionId in RegionIds)
                pw.WriteUInt(regionId);
        }
    }
}
