namespace Rasa.Data
{
    /// <summary>
    /// Where a filed petition has got to. Stored in petition.status.
    ///
    /// There is no "in progress": nothing in the client can show one, and a status only a
    /// console operator ever sees is a note, which is what Resolution is for.
    /// </summary>
    public enum PetitionStatus : byte
    {
        /// <summary>Filed and waiting. Everything starts here.</summary>
        Open = 0,

        /// <summary>Dealt with, from the console, with a line about what was done.</summary>
        Resolved = 1,

        /// <summary>Withdrawn by the player who filed it.</summary>
        Cancelled = 2
    }
}
