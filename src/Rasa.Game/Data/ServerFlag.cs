namespace Rasa.Data
{
    /// <summary>
    /// Server-wide switches the client asks about before offering a feature.
    ///
    /// The values are the client's own, from generated/client/constant/serverflags.py. The client
    /// keeps whatever set the server sends it in client/serverflagmanager.py and answers
    /// IsServerFlagSet() from it; the only reader in the shipped client scripts is
    /// client/minioncommand.py, which hides every minion command unless MinionCommands is set.
    /// The PTS_ ones were for the public test server and have no reader left in the client, so
    /// setting them does nothing but show up in /flag list.
    /// </summary>
    public enum ServerFlag : uint
    {
        PtsTestGateNpc      = 1,
        PtsPvpMap           = 4,
        PtsNewCrafting      = 7,
        MapEpicGauntlet     = 8,
        PtsDisablePalisades = 9,
        MinionCommands      = 10,
        TestFlag1           = 10000001,
        TestFlag2           = 10000002
    }
}
