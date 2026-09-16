namespace Rasa.Packets.Minion.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The answer to a minion command. One class for all ten acks, because they differ only in
    /// opcode and in how many fields they carry.
    ///
    /// The arity is not uniform and is not ours to choose - <c>client/minioncommand.py</c> unpacks
    /// a fixed number of names from each one:
    ///
    /// <list type="bullet">
    /// <item><c>(success, messageId)</c> - MinionCommandAck alone.</item>
    /// <item><c>(success, messageId, minionId)</c> - Stay, Go, FollowMe, TargetMe, AssistMe,
    /// Temperament.</item>
    /// <item><c>(success, messageId, minionId, targetId)</c> - FollowTarget, Target,
    /// AssistTarget.</item>
    /// </list>
    ///
    /// <c>minionId</c> and <c>targetId</c> exist so <c>_BuildMessageArgs</c> can look the entities
    /// up and substitute <c>%(minionName)s</c> and <c>%(targetName)s</c> into the message. An id
    /// the client cannot resolve costs nothing - <c>_GetActorName</c> returns an empty string -
    /// but the message then reads with a hole in it, so send ids the client can see.
    ///
    /// <c>messageId</c> is what actually appears on screen; every one of them already exists in
    /// PlayerMessage as PmMinion*. <c>success</c> is sent alongside it and, in the shipped client,
    /// ignored: every ack handler displays the message whatever it says. It is sent honestly
    /// anyway.
    ///
    /// Which ack answers a <c>/cmd</c> string is a distinction worth keeping straight. A stance
    /// change answers with <see cref="Temperament"/>, not <see cref="Command"/>: PlayerMessage
    /// carries PmMinionPassiveSuccess / Defensive / Aggressive for the stance and
    /// PmMinionInvalidCommand plus three TooManyArgs variants for a string that would not parse,
    /// and MinionTemperamentAck has no client-to-server opcode of its own, which only makes sense
    /// if it is the reply to a stance word inside MinionCommand.
    /// </summary>
    public class MinionAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; }

        public bool Success { get; }
        public PlayerMessage MessageId { get; }
        public ulong MinionId { get; }
        public ulong TargetId { get; }

        /// <summary>How many fields this ack's opcode carries: 2, 3 or 4.</summary>
        private readonly byte _arity;

        private MinionAckPacket(GameOpcode opcode, byte arity, bool success, PlayerMessage messageId, ulong minionId = 0, ulong targetId = 0)
        {
            Opcode = opcode;
            _arity = arity;
            Success = success;
            MessageId = messageId;
            MinionId = minionId;
            TargetId = targetId;
        }

        /// <summary>An ack that names only the minion: Stay, Go, FollowMe, TargetMe, AssistMe.</summary>
        public static MinionAckPacket ForMinion(GameOpcode opcode, bool success, PlayerMessage messageId, ulong minionId) =>
            new MinionAckPacket(opcode, 3, success, messageId, minionId);

        /// <summary>An ack that names a minion and a target: FollowTarget, Target, AssistTarget.</summary>
        public static MinionAckPacket ForTarget(GameOpcode opcode, bool success, PlayerMessage messageId, ulong minionId, ulong targetId) =>
            new MinionAckPacket(opcode, 4, success, messageId, minionId, targetId);

        /// <summary>The stance acknowledgement.</summary>
        public static MinionAckPacket Temperament(bool success, PlayerMessage messageId, ulong minionId) =>
            new MinionAckPacket(GameOpcode.MinionTemperamentAck, 3, success, messageId, minionId);

        /// <summary>The bare acknowledgement, which is how a /cmd string that would not parse is refused.</summary>
        public static MinionAckPacket Command(bool success, PlayerMessage messageId) =>
            new MinionAckPacket(GameOpcode.MinionCommandAck, 2, success, messageId);

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(_arity);

            pw.WriteBool(Success);
            pw.WriteUInt((uint)MessageId);

            if (_arity >= 3)
                pw.WriteULong(MinionId);

            if (_arity >= 4)
                pw.WriteULong(TargetId);
        }
    }
}
