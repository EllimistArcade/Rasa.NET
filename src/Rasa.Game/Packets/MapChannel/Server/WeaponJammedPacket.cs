namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells the client its weapon has jammed, or come free.
    ///
    /// Addressed to the weapon's own entity id, not the player's -
    /// <c>Recv_WeaponJammed(self, isJammed)</c> is a method on the weapon augmentation, the same
    /// way WeaponAmmoInfo is.
    ///
    /// On true the client posts the jam message, the error text, the tutorial, and a WEAPON_JAMMED
    /// client event that turns the weapon tray's ammo readout into "Jammed". On false it does only
    /// the event, which is what puts the readout back - so the clear has to be sent, or the weapon
    /// works again while the UI still says it is stuck.
    /// </summary>
    public class WeaponJammedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.WeaponJammed;

        public bool IsJammed { get; }

        public WeaponJammedPacket(bool isJammed)
        {
            IsJammed = isJammed;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteBool(IsJammed);
        }
    }
}
