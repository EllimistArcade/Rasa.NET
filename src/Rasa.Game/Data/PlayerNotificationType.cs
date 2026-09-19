namespace Rasa.Data
{
    /// <summary>
    /// The first argument of DisplayPlayerNotification, from generated/client/playernotification.
    ///
    /// It picks where the message lands, and the client decides that, not the server:
    /// gameui.py's DisplayPlayerNotification sends Big to the centre-screen banner and
    /// Destination to the sub-region strip, and everything else falls through to the ordinary
    /// player-message path - so Info, Alert and CurrentLocation all end up as chat lines today.
    /// They are still worth sending as themselves rather than collapsing to Info: the three are
    /// distinct in the client's own table, and a UI that separates them later will get the right
    /// thing without the server changing.
    /// </summary>
    public enum PlayerNotificationType
    {
        /// <summary>Centre-screen big text.</summary>
        Big = 1,

        Info = 2,
        Alert = 3,

        /// <summary>The sub-region name strip, as used when you cross into a new area.</summary>
        Destination = 4,

        CurrentLocation = 5
    }
}
