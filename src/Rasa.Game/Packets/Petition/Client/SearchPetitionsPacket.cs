namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:60 - SendSearchPetitions(), with no arguments at all. No
    /// caller, no slash command. Groundwork.
    ///
    /// Taking no arguments is what decides what it can mean: there is nothing to search *by*,
    /// so it can only be "the petitions I filed". See PetitionManager.SearchPetitions for why
    /// it is not everyone's.
    /// </summary>
    public class SearchPetitionsPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SearchPetitions;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
