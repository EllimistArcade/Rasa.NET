namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Whether a player is on a trial account.
    ///
    /// Recv_IsTrialAccount(self, bIsTrialAccount) on the Manifestation
    /// (client/augmentations/manifestation.py:1406) stores the flag and repaints that player's
    /// name. Every client reader - the overhead name, target info window and target list - only
    /// tests it for truth and appends ID_NAME_TRIAL_ACCOUNT when set, so false written as None
    /// is equivalent to the client's own False default.
    /// </summary>
    public class IsTrialAccountPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.IsTrialAccount;

        public bool IsTrialAccount { get; set; }

        public IsTrialAccountPacket(bool isTrialAccount)
        {
            IsTrialAccount = isTrialAccount;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteBool(IsTrialAccount);
        }
    }
}
