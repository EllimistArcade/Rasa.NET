using System.Collections.Generic;

namespace Rasa.Packets.Clan.Server
{
    using Data;
    using Memory;
    using Structures.Char;

    /// <summary>
    /// The clan lockbox's transaction history. <c>LoadClanLockboxLogs</c> replaces whatever the
    /// window is showing; <c>UpdateClanLockboxLogs</c> adds to it, and the client ignores an empty
    /// one, so nothing is sent when nothing happened.
    ///
    /// Each entry is the ten-field tuple <c>client/clanlockboxlog.py</c> unpacks:
    /// <c>(transactionTypeId, characterId, characterName, userName, creditTypeId, amount,
    /// itemTemplateId, lootModuleIds, quantity, transactionTime)</c>.
    ///
    /// Two of those fields decide how the line reads, and both are decided by being None rather
    /// than by their value. <c>GetTargetString</c> tests <c>itemTemplateId is not None</c> first,
    /// so a credit row that sent 0 there would be drawn as an item - item 0, whose name the client
    /// then looks up and does not find. Only once that is None does it look at
    /// <c>creditTypeId</c>, and an unrecognised one logs an error and prints nothing at all. So an
    /// item row sends None for the credit type and a credit row sends None for the item, and
    /// neither sends a zero.
    ///
    /// <c>quantity</c> is None unless there is more than one, because the client prefixes the
    /// item name with it whenever it is set above 1; and <c>transactionTime</c> is Unix seconds,
    /// handed straight to <c>datetime.fromtimestamp</c>.
    /// </summary>
    public class ClanLockboxLogsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; }

        public List<ClanLockboxLogEntry> Logs { get; }

        private ClanLockboxLogsPacket(GameOpcode opcode, List<ClanLockboxLogEntry> logs)
        {
            Opcode = opcode;
            Logs = logs;
        }

        /// <summary>The whole history, replacing what the window holds.</summary>
        public static ClanLockboxLogsPacket Load(List<ClanLockboxLogEntry> logs) =>
            new ClanLockboxLogsPacket(GameOpcode.LoadClanLockboxLogs, logs);

        /// <summary>What has happened since, appended to it.</summary>
        public static ClanLockboxLogsPacket Update(List<ClanLockboxLogEntry> logs) =>
            new ClanLockboxLogsPacket(GameOpcode.UpdateClanLockboxLogs, logs);

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteList(Logs.Count);

            foreach (var log in Logs)
            {
                pw.WriteTuple(10);

                pw.WriteUInt(log.TransactionType);
                pw.WriteUInt(log.CharacterId);
                pw.WriteUnicodeString(log.CharacterName ?? string.Empty);
                pw.WriteUnicodeString(log.UserName ?? string.Empty);

                // An item row leaves the credit type unset; a credit row leaves the item unset.
                if (log.ItemTemplateId != 0)
                    pw.WriteNoneStruct();
                else
                    pw.WriteUInt(log.CreditType);

                pw.WriteLong(log.Amount);

                if (log.ItemTemplateId != 0)
                    pw.WriteUInt(log.ItemTemplateId);
                else
                    pw.WriteNoneStruct();

                // Module ids decorate an item's name. Nothing records them yet, and the client
                // takes an empty list happily.
                pw.WriteList(0);

                if (log.Quantity > 1)
                    pw.WriteUInt(log.Quantity);
                else
                    pw.WriteNoneStruct();

                pw.WriteLong(log.TransactionTime);
            }
        }
    }
}
