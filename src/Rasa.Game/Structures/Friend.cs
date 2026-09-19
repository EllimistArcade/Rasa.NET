namespace Rasa.Structures
{
    using Game;
    using Memory;
    using Structures.Char;

    /// <summary>
    /// One friend-list row, unpacked by client/social.py _UnpackFriendState as
    /// (characterId, userId, characterName, familyName, level, contextId, isOnline).
    /// The client keys its friend dictionary on userId, which is the account id.
    /// </summary>
    public class Friend : IPythonDataStruct
    {
        public ulong CharacterId { get; set; }
        public uint UserId { get; set; }
        public string CharacterName { get; set; }
        public string FamilyName { get; set; }
        public uint Level { get; set; }

        /// <summary>
        /// Map the friend is on, or null when offline. Must go out as a real None: the client
        /// only resolves a map name when contextId is not None, and GetGameContextName
        /// (client/clientlanguagemanager.py:274) raises UnboundLocalError for an id that is in
        /// neither lookup table - 0 included - which would abort the whole Recv_ handler.
        /// </summary>
        public uint? ContextId { get; set; }

        public bool IsOnline { get; set; }
        
        public Friend()
        {
        }
        
        public Friend(Client client)
        {
            CharacterId = client.Player.Id;
            UserId = client.AccountEntry.Id;
            CharacterName = client.Player.Name;
            FamilyName = client.Player.FamilyName;
            Level = client.Player.Level;
            ContextId = client.Player.MapContextId;
            IsOnline = true;
        }

        /// <summary>
        /// An offline friend, shown with the character they last had selected so the list
        /// reads "Name Family" rather than a bare family name.
        /// </summary>
        public Friend(GameAccountEntry account)
        {
            UserId = account.Id;
            FamilyName = account.FamilyName;
            IsOnline = false;

            var character = account.Characters == null ? null : account.GetCharacterBySlot(account.SelectedSlot);
            if (character != null)
            {
                CharacterId = character.Id;
                CharacterName = character.Name;
                Level = character.Level;
            }
        }

        public void Read(PythonReader pr)
        {
        }

        public void Write(PythonWriter pw)
        {
            pw.WriteList(7);
            pw.WriteULong(CharacterId);
            pw.WriteUInt(UserId);
            pw.WriteUnicodeString(CharacterName);
            pw.WriteUnicodeString(FamilyName);
            pw.WriteUInt(Level);

            if (ContextId.HasValue)
                pw.WriteUInt(ContextId.Value);
            else
                pw.WriteNoneStruct();

            pw.WriteBool(IsOnline);
        }
    }
}
