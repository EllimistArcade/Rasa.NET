namespace Rasa.Data
{
    /// <summary>
    /// The chat channels the client knows, from its own generated.client.chatchannel table. The
    /// ids above ten million are the client's convention for channels added after the original
    /// set, not a separate numbering.
    /// </summary>
    public static class ChatChannelId
    {
        public const uint General = 1;
        public const uint NewPlayer = 2;
        public const uint LookingForGroup = 3;

        // per map: the same id is a different channel on each map
        public const uint MapGeneral = 4;
        public const uint MapTrade = 6;
        public const uint MapDefense = 7;

        public const uint ClanCommon = 10000001;
        public const uint ClanLeaders = 10000002;
        public const uint GeneralFrench = 10000003;
        public const uint GeneralGerman = 10000004;
        public const uint TrialAccount = 10000005;
        public const uint TrialAccountFrench = 10000006;
        public const uint TrialAccountGerman = 10000007;
        public const uint Team = 10000008;
    }
}
