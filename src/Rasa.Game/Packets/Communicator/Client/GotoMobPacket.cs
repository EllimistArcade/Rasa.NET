using System.Collections.Generic;

namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    public class GotoMobPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.GotoMob;

        public string ArgString { get; set; }

        /// <summary>
        /// The name id whose creature name is exactly what was typed, or null when nothing
        /// matched exactly - which is the ordinary case, since "/gotomob thrax" matches
        /// "Thrax Warrior" as a word and nothing outright.
        /// </summary>
        public uint? ExactMobNameId { get; set; }

        public List<uint> PartialMobNameIds = new List<uint>();

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ArgString = pr.ReadUnicodeString();

            // communicator.GotoMob leaves exactMobNameId at None when no creature name equals
            // what was typed. Reading that as an int threw, and a throw out of a packet read
            // disconnects the client - so /gotomob hung up on the GM in every case but an exact
            // name with no other name containing it.
            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadNoneStruct();
                ExactMobNameId = null;
            }
            else
                ExactMobNameId = pr.ReadUInt();

            var listLen = pr.ReadList();

            // partialMobNameIds is a list of name ids, not of tuples: the client appends
            // creatureNameId itself. Reading a tuple per entry threw on any partial match.
            for (var i = 0; i < listLen; i++)
                PartialMobNameIds.Add(pr.ReadUInt());
        }
    }
}
