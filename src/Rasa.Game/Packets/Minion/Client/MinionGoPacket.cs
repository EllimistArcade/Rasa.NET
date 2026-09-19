using System.Numerics;

namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionGo</c>, which sends <c>(x, y, z)</c> - three bare coordinates, not a tuple inside
    /// the tuple. They come from <c>PickMoveLocation</c>, the point under the player's reticule,
    /// and the client has already refused anything further than
    /// <c>MAX_MINION_GO_DISTANCE</c> (40 m) or not on terrain.
    ///
    /// Each coordinate is read tolerantly. A whole number marshals as an int rather than a
    /// double, so a player aiming at an exact metre would otherwise fail to parse - and a parse
    /// failure here is a dropped connection, not a dropped command. Same reasoning as
    /// RequestUnstickPacket.
    /// </summary>
    public class MinionGoPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionGo;

        public Vector3 Destination { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            var x = ReadNumber(pr);
            var y = ReadNumber(pr);
            var z = ReadNumber(pr);

            Destination = new Vector3((float)x, (float)y, (float)z);
        }

        private static double ReadNumber(PythonReader pr)
        {
            switch (pr.PeekType())
            {
                case PythonType.Double: return pr.ReadDouble();
                case PythonType.Int: return pr.ReadInt();
                case PythonType.Long: return pr.ReadLong();
                default:
                    pr.ReadNoneStruct();
                    return 0;
            }
        }
    }
}
