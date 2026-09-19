using System.Collections.Generic;

namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Recv_SearchPetitionsAck(success, resultList) - client/petitionmanager.py:95, empty body.
    ///
    /// resultList is a list of PetitionInfo dictionaries without their bodies, which is this
    /// server's definition - the client has none. See PetitionInfo.
    /// </summary>
    public class SearchPetitionsAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SearchPetitionsAck;

        public bool Success { get; set; }
        public List<PetitionInfo> Results { get; set; }

        public SearchPetitionsAckPacket(bool success, List<PetitionInfo> results = null)
        {
            Success = success;
            Results = results ?? new List<PetitionInfo>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteBool(Success);

            pw.WriteList(Results.Count);

            foreach (var result in Results)
                result.Write(pw, false);
        }
    }
}
