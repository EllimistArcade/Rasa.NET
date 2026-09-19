namespace Rasa.Data
{
    /// <summary>
    /// Stored in petition.type. The client has one window and two Send buttons; this is the
    /// only thing that tells the two apart once the text is in the table.
    /// </summary>
    public enum PetitionType : byte
    {
        HelpRequest = 0,
        BugReport = 1
    }
}
