using System.Collections.Generic;

namespace Rasa.Packets.LookingForGroup.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// The matching ads.
    ///
    /// Recv_LookingForGroupSearchResults(adInfoTupleList) takes a single argument that it
    /// iterates, unpacking each entry into a LookingForGroupAdInfo
    /// (client/lookingforgroupmanager.py:67), so the wire form is a 1-tuple holding a list
    /// of 10-tuples. An empty list is legitimate - it repaints the results pane with
    /// nothing in it, which is how the player sees "no matches".
    /// </summary>
    public class LookingForGroupSearchResultsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.LookingForGroupSearchResults;

        public List<LookingForGroupAdInfo> Results { get; }

        public LookingForGroupSearchResultsPacket(List<LookingForGroupAdInfo> results)
        {
            Results = results;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(Results.Count);

            foreach (var result in Results)
                result.Write(pw);
        }
    }
}
