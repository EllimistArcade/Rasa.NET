using System.Numerics;

namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// /stuck. The client builds an Unstick action and sends the position it thinks it is at:
    /// actions/unstick.py sends (stuckPos,), one element holding the manifestation's own
    /// position.
    ///
    /// That position is read so the packet is consumed and the connection is not dropped, but it
    /// is not what the server acts on - a client that says it is stuck somewhere convenient would
    /// otherwise be asking to be moved there. The server uses the position it holds itself.
    ///
    /// The position arrives as a sequence of three numbers, or as None when the client could not
    /// work out where it was; both are accepted.
    /// </summary>
    public class RequestUnstickPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestUnstick;

        /// <summary>Where the client believes it is. Informational only.</summary>
        public Vector3 ClientPosition { get; set; }

        /// <summary>False when the client sent None instead of a position.</summary>
        public bool HasClientPosition { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            var type = pr.PeekType();

            if (type != PythonType.Tuple && type != PythonType.List)
            {
                // None, or something this client build does not send; nothing to read past it
                // except the marker itself.
                pr.ReadNoneStruct();
                return;
            }

            var count = type == PythonType.Tuple ? pr.ReadTuple() : pr.ReadList();
            var values = new double[3];

            for (var i = 0; i < count; i++)
            {
                var value = pr.ReadNumber();

                if (i < values.Length)
                    values[i] = value;
            }

            if (count >= 3)
            {
                ClientPosition = new Vector3((float)values[0], (float)values[1], (float)values[2]);
                HasClientPosition = true;
            }
        }
    }
}
