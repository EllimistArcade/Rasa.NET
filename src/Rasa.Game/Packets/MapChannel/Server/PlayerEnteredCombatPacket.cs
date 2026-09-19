namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells a player they have entered combat. No arguments - <c>Recv_PlayerEnteredCombat(self)</c>
    /// takes none.
    ///
    /// Goes to the player's own entity id and nowhere else: the handler is on Manifestation and
    /// all it does is post UI_UPDATE_AVATAR_COMBAT_STATUS, which only the avatar status window
    /// listens for, to show or hide its combat indicator. Other players are told nothing.
    ///
    /// Not to be confused with the character state machine's combat axis
    /// (<c>at_peace</c> / <c>combat_engaged</c>). That one is the *visual* stance - weapon aimed,
    /// strafing - the client drives it itself on its own timer and reports it with
    /// RequestVisualCombatMode, and it decides animations and bone tracking. This is the server's
    /// own notion of being in a fight, and the only thing that reads it is regeneration.
    /// </summary>
    public class PlayerEnteredCombatPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PlayerEnteredCombat;

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(0);
        }
    }
}
