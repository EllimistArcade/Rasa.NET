namespace Rasa.Data
{
    public enum QueueState
    {
        Authenticating = 0,
        Authenticated  = 1,
        InQueue        = 2,
        Redirecting    = 3,
        Disconnected   = 4,

        /// <summary>
        /// Handed off and the account has since logged in at the world port. The connection may
        /// still be open, but it holds no slot: the player is counted as a world client now.
        /// </summary>
        Arrived        = 5
    }
}
