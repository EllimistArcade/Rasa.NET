using System.Collections.Generic;

namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Everything wrong with the map the player is standing in, as a modal dialog.
    ///
    /// clientmethod.Recv_DisplayMapErrors(errorList) puts up a message box - modal, and created
    /// with bAlwaysVisible - saying that map initialisation failed and that behaviour may be
    /// incorrect until the tracebacks are gone. It then writes every entry to the client log in
    /// full. That is why this only ever goes to a GM: a player cannot act on it and should not
    /// have a modal dialog about server data put in front of them.
    ///
    /// Two things the client does with each entry shape what is worth sending. The dialog shows
    /// only error.splitlines().pop() - the last line - while the log gets the whole thing, so an
    /// entry is written as a single line here and nothing is lost either way. And splitlines()
    /// on an empty string returns an empty list, so pop() raises IndexError: an empty entry
    /// takes down the whole dialog. MapErrorManager refuses to record one.
    ///
    /// The strings go out as unicode. The dialog builds its prompt on a unicode literal, and
    /// concatenating a byte string onto it decodes as ASCII, so one non-ASCII character
    /// anywhere in a map name or a description would raise instead of showing the dialog.
    /// </summary>
    public class DisplayMapErrorsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplayMapErrors;

        public IReadOnlyList<string> Errors { get; }

        public DisplayMapErrorsPacket(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(Errors.Count);

            foreach (var error in Errors)
                pw.WriteUnicodeString(error);
        }
    }
}
