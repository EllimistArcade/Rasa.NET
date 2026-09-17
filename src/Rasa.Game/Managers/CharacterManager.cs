using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Game.Client;
    using Packets.Game.Server;
    using Packets.MapChannel.Server;
    using Misc;
    using Packets.ClientMethod.Server;
    using Packets.Communicator.Client;
    using Packets.Communicator.Server;
    using Packets.Manifestation.Server;
    using Packets;
    using Repositories.Char;
    using Repositories.UnitOfWork;
    using Repositories.World;
    using Structures;
    using Structures.Char;

    public class CharacterManager
    {
        private static CharacterManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly object _createLock = new();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public const ulong SelectionPodStartEntityId = 100;
        public const byte MaxSelectionPods = 16;

        public static CharacterManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new CharacterManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        public CharacterManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        public void StartCharacterSelection(Client client)
        {
            if (client.State != ClientState.LoggedIn)
                return;

            client.CallMethod(SysEntity.ClientMethodId, new BeginCharacterSelectionPacket(client.AccountEntry.FamilyName, client.AccountEntry.Characters.Any(), client.AccountEntry.Id, client.AccountEntry.CanSkipBootcamp));

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var charactersBySlot = unitOfWork.Characters.GetByAccountId(client.AccountEntry.Id);

            for (byte i = 1; i <= MaxSelectionPods; ++i)
            {
                CharacterEntry character = null;
                if (charactersBySlot.ContainsKey(i))
                {
                    character = charactersBySlot[i];
                }
                SendCharacterInfoProdCreate(client, i, character);
            }

            client.State = ClientState.CharacterSelection;

            // get userOptions
            var optionsList = unitOfWork.UserOptions.Get(client.AccountEntry.Id);

            foreach (var userOption in optionsList)
                client.UserOptions.Add(new UserOptions((UserOption)userOption.OptionId, userOption.Value));
            
            client.CallMethod(SysEntity.ClientMethodId, new UserOptionsPacket(client.UserOptions));
        }

        public void RequestCharacterName(Client client, int gender)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var name = unitOfWork.RandomNames.GetFirstName((Gender)gender);
            client.CallMethod(SysEntity.ClientMethodId, new GeneratedCharacterNamePacket
            {
                Name = name
            });
        }

        public void RequestFamilyName(Client client)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var name = unitOfWork.RandomNames.GetLastName();
            client.CallMethod(SysEntity.ClientMethodId, new GeneratedFamilyNamePacket
            {
                Name = name
            });
        }

        /// <summary>
        /// Clone credits: a snapshot of a character in a new pod, so a player can take a second
        /// run at the class tree without levelling again, or refund the skill points they spent.
        ///
        /// What carries over and what does not is the live game's rule, not a guess. Kept:
        /// attributes, the logos tablet, obtained waypoints, the surname, and the place the
        /// source was standing when the clone was made - clone somewhere hostile and the clone
        /// wakes up there. Reset: skills, missions, friends and clan. Level, experience and class
        /// come across too, because a clone taken at 14.99 exists precisely so both Tier 3
        /// branches can be tried from the same progress.
        ///
        /// The clone arrives with nothing. It does not inherit the source's pack, and unlike a
        /// new character it gets no starter kit either.
        /// </summary>
        public void RequestCloneCharacterToSlot(Client client, RequestCloneCharacterToSlotPacket packet)
        {
            // Same rule as creating: the pod screen is the only place this is safe, because the
            // account entry is reloaded underneath whatever is loaded.
            if (client.State != ClientState.CharacterSelection)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to clone a character while in state {client.State}.");

                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return;
            }

            var result = packet.Validate();
            if (result != CreateCharacterResult.Success)
            {
                SendCharacterCreateFailed(client, result);
                return;
            }

            if (packet.SlotNum < 1 || packet.SlotNum > MaxSelectionPods
                || packet.CloneSlotNum < 1 || packet.CloneSlotNum > MaxSelectionPods
                || packet.SlotNum == packet.CloneSlotNum)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to clone slot {packet.CloneSlotNum} into slot {packet.SlotNum}.");

                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return;
            }

            if (client.AccountEntry.GetCharacterBySlot(packet.SlotNum) != null)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.CharacterSlotInUse);
                return;
            }

            var source = client.AccountEntry.GetCharacterBySlot(packet.CloneSlotNum);

            if (source == null)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.InvalidCharacterToCloneFrom);
                return;
            }

            // The client greys its clone button out at zero, so this only catches a client that
            // did not - but it is the check that stops a credit going negative on a uint.
            if (source.CloneCredits == 0)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.NotEnoughCloneCredits);
                return;
            }

            uint characterId;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            lock (_createLock)
            {
                var createdCharacterId = InternalClone(client, packet, unitOfWork);

                if (createdCharacterId == null)
                {
                    return;
                }

                characterId = createdCharacterId.Value;
            }

            CopyProgressToClone(unitOfWork, source, characterId);

            // Spent last, so a clone that failed anywhere above costs nothing.
            //
            // Held in a local rather than re-read off `source` afterwards. UpdateCharacterCloneCredits
            // writes through the tracked entity, and whether that is the same object as `source`
            // depends on which context loaded the account - so reading it back would subtract
            // twice on one path and once on the other.
            var remainingCredits = source.CloneCredits - 1;

            unitOfWork.Characters.UpdateCharacterCloneCredits(source.Id, remainingCredits);

            if (unitOfWork.CharacterLockboxes.Get(client.AccountEntry.Id) == null)
                unitOfWork.CharacterLockboxes.Add(client.AccountEntry.Id);

            unitOfWork.Complete();

            client.CallMethod(SysEntity.ClientMethodId,
                new CharacterCreateSuccessPacket(packet.SlotNum, client.AccountEntry.FamilyName));

            client.ReloadGameAccountEntry();

            SendCharacterInfo(client, packet.SlotNum, unitOfWork.Characters.Get(characterId));

            // The source pod shows a credit count, which just went down by one.
            SendCharacterInfo(client, packet.CloneSlotNum, unitOfWork.Characters.Get(source.Id));

            // And the pod's own method for exactly this, which CharacterInfo does not replace:
            // Recv_CloneCreditsChanged posts UI_UPDATE_CHARACTER_SELECTION_SLOT_CLONE_CREDITS,
            // which repaints the stats panel if that slot is the selected one. CharacterInfo
            // reaches _UpdatePod and _AutoSelectCharacter, and neither of those repaints, so the
            // number would sit stale on screen until the player clicked away and back.
            client.CallMethod(SelectionPodStartEntityId + packet.CloneSlotNum,
                new CloneCreditsChangedPacket(remainingCredits));
        }

        /// <summary>
        /// The row for a clone. Unlike a new character there is no family name to set or check:
        /// cloning needs a character to clone from, so the account already has one, and the
        /// surname is the one thing the live game's rules say always carries over.
        /// </summary>
        private uint? InternalClone(Client client, RequestCloneCharacterToSlotPacket packet, ICharUnitOfWork unitOfWork)
        {
            var characterEntry = unitOfWork.Characters.Create(client.AccountEntry, packet.SlotNum,
                packet.CharacterName,
                (byte)packet.RaceId,
                packet.Scale,
                packet.Gender);

            if (characterEntry == null)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return null;
            }

            unitOfWork.CharacterAppearances.Add(characterEntry, CreateCharacterAppearanceEntries(packet.AppearanceData));

            return characterEntry.Id;
        }

        /// <summary>
        /// Everything the live game's rules say survives cloning. Skills and the ability drawer
        /// are deliberately absent - resetting them is what makes a clone a respec - and so are
        /// missions, friends and clan.
        /// </summary>
        private static void CopyProgressToClone(ICharUnitOfWork unitOfWork, CharacterEntry source, uint cloneId)
        {
            unitOfWork.Characters.UpdateCharacterLevel(cloneId, source.Level);
            unitOfWork.Characters.UpdateCharacterExpirience(cloneId, source.Experience);
            unitOfWork.Characters.UpdateCharacterClass(cloneId, source.Class);
            unitOfWork.Characters.UpdateCharacterAttributes(cloneId, source.Body, source.Mind, source.Spirit);

            // "Location upon cloning": the clone appears where the source was standing, hostile
            // ground included.
            unitOfWork.Characters.UpdateCharacterPosition(cloneId, source.CoordX, source.CoordY, source.CoordZ,
                source.Rotation, source.MapContextId);

            foreach (var logosId in unitOfWork.CharacterLogoses.GetLogos(source.Id))
                unitOfWork.CharacterLogoses.SetLogos(cloneId, logosId);

            foreach (var teleporter in unitOfWork.CharacterTeleporters.Get(source.Id))
                unitOfWork.CharacterTeleporters.Add(
                    new CharacterTeleporterEntry(cloneId, teleporter.WaypointId, teleporter.WaypointType));
        }

        public void RequestCreateCharacterInSlot(Client client, RequestCreateCharacterInSlotPacket packet)
        {
            // The selection screen is the only place the client sends this from. Nothing else here
            // is safe against a create that arrives while a character is loaded: the new row is
            // written, the account entry is reloaded under a live manifestation, and the caller
            // has no reason to be anywhere but the pod screen.
            if (client.State != ClientState.CharacterSelection)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to create a character while in state {client.State}.");

                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return;
            }

            var result = packet.Validate();
            if (result != CreateCharacterResult.Success)
            {
                SendCharacterCreateFailed(client, result);
                return;
            }

            // The pods are 1..MaxSelectionPods. The packet used to take any byte, and the row was
            // inserted with whatever it said: slot 0 or 17+ made a character no pod ever shows and
            // no switch can reach, which still counted for the family-name lock and "has
            // characters"; a second character in an occupied slot was worse, because character
            // selection keys the account's characters by slot and threw on the duplicate at every
            // login from then on, locking the account out until someone edited the table.
            if (packet.SlotNum < 1 || packet.SlotNum > MaxSelectionPods)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to create a character in slot {packet.SlotNum}.");

                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return;
            }

            // AccountEntry.Characters is reloaded after every create and delete, and an account
            // can only be logged in once, so this is current. The unique index on
            // (account_id, slot) is the backstop if it ever is not.
            if (client.AccountEntry.GetCharacterBySlot(packet.SlotNum) != null)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.CharacterSlotInUse);
                return;
            }

            uint characterId;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // TODO to remove this lock, the family name check and update must be redesigned to be thread safe
            lock (_createLock)
            {
                var createdCharacterId = InternalCreate(client, packet, unitOfWork);

                if (createdCharacterId == null)
                {
                    return;
                }

                characterId = createdCharacterId.Value;
            }

            // give basic items
            GiveBasicItems(client, characterId);

            // give first lockbox tab
            if (unitOfWork.CharacterLockboxes.Get(client.AccountEntry.Id) == null)
                unitOfWork.CharacterLockboxes.Add(client.AccountEntry.Id);

			unitOfWork.Complete();
			
            client.CallMethod(SysEntity.ClientMethodId, new CharacterCreateSuccessPacket(packet.SlotNum, packet.FamilyName));

            client.ReloadGameAccountEntry();

            var character = unitOfWork.Characters.Get(characterId);
            SendCharacterInfo(client, packet.SlotNum, character);
        }

        #region Name changes

        /// <summary>
        /// Names the client will accept, from PM_NAME_TOO_SHORT, PM_NAME_TOO_LONG and
        /// PM_NAME_FORMAT_INVALID: "Your name must start with a capital letter, contain only
        /// letters, and must not contain letters repeated more than twice in a row", 3 to 20
        /// characters.
        /// </summary>
        public const int MinNameLength = 3;
        public const int MaxNameLength = 20;

        /// <summary>/changefirstname: renames the character the player is on.</summary>
        internal void ChangeFirstName(Client client, ChangeFirstNamePacket packet)
        {
            if (!IsNameChanger(client))
                return;

            Rename(client, client, packet.Name, false);
        }

        /// <summary>/changelastname: renames the account's family, so every character on it.</summary>
        internal void ChangeLastName(Client client, ChangeLastNamePacket packet)
        {
            if (!IsNameChanger(client))
                return;

            Rename(client, client, packet.Name, true);
        }

        /// <summary>
        /// Renames a character or an account family, telling the player who asked what went
        /// wrong. The target can be another player, for the GM command.
        /// </summary>
        public bool Rename(Client requester, Client target, string newName, bool familyName)
        {
            if (target?.Player == null || target.AccountEntry == null)
                return false;

            var name = newName?.Trim() ?? string.Empty;
            var oldName = familyName ? target.Player.FamilyName : target.Player.Name;

            if (string.Equals(oldName, name, StringComparison.Ordinal))
                return false;

            if (!IsValidName(name, out var formatError))
            {
                NameMessage(requester, formatError);
                return false;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (new Censor(unitOfWork.CensoredWords.GetCensoredWords()).ContainsProfanity(name))
            {
                NameMessage(requester, PlayerMessage.PmNameUnacceptable);
                return false;
            }

            if (familyName)
            {
                if (!unitOfWork.GameAccounts.CanChangeFamilyName(target.AccountEntry.Id, name))
                {
                    NameMessage(requester, PlayerMessage.PmFamilyNameReserved);
                    return false;
                }

                unitOfWork.GameAccounts.UpdateFamilyName(target.AccountEntry.Id, name);
                target.Player.FamilyName = name;
            }
            else
            {
                // Character creation never checked this, so duplicates can exist already; a
                // rename at least does not add more.
                if (unitOfWork.Characters.IsCharacterNameTaken(name, target.Player.Id))
                {
                    NameMessage(requester, PlayerMessage.PmNameInUse);
                    return false;
                }

                unitOfWork.Characters.UpdateCharacterName(target.Player.Id, name);
                target.Player.Name = name;
            }

            // UpdateCharacterName saves as it goes; UpdateFamilyName only changes the tracked
            // row, and the unit of work discards that on dispose unless it is completed. The
            // family name change was lost here, and ReloadGameAccountEntry then read the old
            // name straight back.
            unitOfWork.Complete();

            target.ReloadGameAccountEntry();

            // CharacterName and ActorName are part of the entity data every client gets when it
            // first sees the player (CreatePlayerEntityData); resending them updates the name on
            // screen for everyone nearby without a relog. Characters of this account that are not
            // in the world pick the family name up the next time they log in.
            var mapChannel = target.Player.MapChannel;

            if (mapChannel != null)
                CellManager.Instance.CellCallMethod(mapChannel, target.Player,
                    familyName ? new ActorNamePacket(target.Player.FamilyName) : (PythonPacket)new CharacterNamePacket(target.Player.Name));

            var args = new Dictionary<string, string> { ["oldname"] = oldName ?? string.Empty, ["newname"] = name };
            var changed = familyName ? PlayerMessage.PmLastNameChanged : PlayerMessage.PmFirstNameChanged;

            target.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(changed, args, MsgFilterId.GeneralSystemMessages));

            if (requester != target)
                requester.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(changed, args, MsgFilterId.GeneralSystemMessages));

            Logger.WriteLog(LogType.Command, $"{requester.AccountEntry.FamilyName} changed {(familyName ? "the family name" : "the character name")} of account {target.AccountEntry.Id} from {oldName} to {name}");

            return true;
        }

        public static bool IsValidName(string name, out PlayerMessage error)
        {
            error = PlayerMessage.PmNameFormatInvalid;

            if (string.IsNullOrEmpty(name) || name.Length < MinNameLength)
            {
                error = PlayerMessage.PmNameTooShort;
                return false;
            }

            if (name.Length > MaxNameLength)
            {
                error = PlayerMessage.PmNameTooLong;
                return false;
            }

            if (!char.IsUpper(name[0]))
                return false;

            for (var i = 0; i < name.Length; i++)
            {
                if (!char.IsLetter(name[i]))
                    return false;

                // No letter three times in a row.
                if (i >= 2 && char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 1])
                           && char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 2]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Name changes are a GM tool here: the slash commands are open to every player, and a
        /// free rename at any moment is a way to be mistaken for someone else.
        /// </summary>
        /// <summary>
        /// Who may use /changefirstname and /changelastname.
        ///
        /// GameMaster, matching the .rename command. It was "any GM level at all", which let an
        /// Observer - the level that exists to read the world without changing it, and the level
        /// every pre-existing account was left on - rename itself and its whole account family.
        /// </summary>
        private static bool IsNameChanger(Client client)
        {
            if (client?.AccountEntry == null || client.Player == null)
                return false;

            if (client.AccountEntry.Level >= (byte)GmLevel.GameMaster)
                return true;

            Logger.WriteLog(LogType.Security, $"AccountId = {client.AccountEntry.Id} tried to change a name without being a GM");
            CommunicatorManager.Instance.SystemMessage(client, "Name changes are done by a GM.");

            return false;
        }

        private static void NameMessage(Client client, PlayerMessage message)
        {
            client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(message, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
        }

        #endregion

        private uint? InternalCreate(Client client, RequestCreateCharacterInSlotPacket packet, ICharUnitOfWork unitOfWork)
        {
            var changeFamilyName = false;
            if (!string.IsNullOrWhiteSpace(client.AccountEntry.FamilyName) && packet.FamilyName != client.AccountEntry.FamilyName)
            {
                if (!client.AccountEntry.Characters.Any())
                {
                    changeFamilyName = true;
                }
                else
                {
                    SendCharacterCreateFailed(client, CreateCharacterResult.InvalidCharacterName);
                    return null;
                }
            }

            if ((string.IsNullOrWhiteSpace(client.AccountEntry.FamilyName) || packet.FamilyName != client.AccountEntry.FamilyName))
            {
                if (!unitOfWork.GameAccounts.CanChangeFamilyName(client.AccountEntry.Id, packet.FamilyName))
                {
                    SendCharacterCreateFailed(client, CreateCharacterResult.FamilyNameReserved);
                    return null;
                }
            }

            var characterEntry = unitOfWork.Characters.Create(client.AccountEntry, packet.SlotNum,
                packet.CharacterName,
                (byte)packet.RaceId,
                packet.Scale,
                packet.Gender);
            if (characterEntry == null)
            {
                SendCharacterCreateFailed(client, CreateCharacterResult.TechnicalDifficulty);
                return null;
            }

            var appearances = CreateCharacterAppearanceEntries(packet.AppearanceData);
            unitOfWork.CharacterAppearances.Add(characterEntry, appearances);

            if (string.IsNullOrWhiteSpace(client.AccountEntry.FamilyName) || changeFamilyName)
            {
                unitOfWork.GameAccounts.UpdateFamilyName(client.AccountEntry.Id, packet.FamilyName);
            }

            return characterEntry.Id;
        }

        private IEnumerable<CharacterAppearanceEntry> CreateCharacterAppearanceEntries(
            IDictionary<EquipmentData, AppearanceData> appearanceData)
        {
            yield return new CharacterAppearanceEntry((uint)EquipmentData.Shoes, (uint)EntityClasses.ArmorRecruitV01CMNBoots, 2139062144);
            yield return new CharacterAppearanceEntry((uint)EquipmentData.Torso, (uint)EntityClasses.ArmorRecruitV01CMNVest, 2139062144);
            yield return new CharacterAppearanceEntry((uint)EquipmentData.Legs, (uint)EntityClasses.ArmorRecruitV01CMNLegs, 2139062144);

            using var worldUnitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var appearancesFromPacket = appearanceData
                .Select(appearance => CreateCharacterAppearanceEntry(appearance.Value, worldUnitOfWork))
                .ToList();

            foreach (var characterAppearanceEntry in appearancesFromPacket)
            {
                yield return characterAppearanceEntry;
            }
        }

        private static CharacterAppearanceEntry CreateCharacterAppearanceEntry(AppearanceData appearanceData, IWorldUnitOfWork unitOfWork)
        {
            var databaseEntry = appearanceData.GetDatabaseEntry();
            databaseEntry.Class = unitOfWork.Equipment.GetItemClass(appearanceData.Class);
            return databaseEntry;
        }

        private void GiveBasicItems(Client client, uint characterId)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 0, unitOfWork.Items.CreateItem(new Item(145, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[17131]].ItemClassInfo.MaxHitPoints, 2139062144)));
            unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 50, unitOfWork.Items.CreateItem(new Item(28, 100, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[28]].ItemClassInfo.MaxHitPoints, 2139062144)));
            unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 1, unitOfWork.Items.CreateItem(new Item(13126, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[13126]].ItemClassInfo.MaxHitPoints, 2139062144)));
            unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 2, unitOfWork.Items.CreateItem(new Item(13186, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[13186]].ItemClassInfo.MaxHitPoints, 2139062144)));
            unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 3, unitOfWork.Items.CreateItem(new Item(13156, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[13156]].ItemClassInfo.MaxHitPoints, 2139062144)));
            //unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 4, unitOfWork.Items.CreateItem(new Item(13066, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[13066]].ItemClassInfo.MaxHitPoints, 2139062144)));
            //unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, characterId, (int)InventoryType.Personal, 5, unitOfWork.Items.CreateItem(new Item(13096, 1, EntityClassManager.Instance.LoadedEntityClasses[ItemManager.Instance.ItemTemplateItemClass[13096]].ItemClassInfo.MaxHitPoints, 2139062144)));
        }

        /// <summary>
        /// Deleting is something the character selection screen asks for, and the shipped client
        /// only offers it there. Nothing refused the packet from a client that was in the world,
        /// though, so a modified one could delete the character its own player was standing in -
        /// leaving the session running against a row that no longer exists.
        ///
        /// The test is the connection's state rather than Player.MapChannel: RemovePlayer takes
        /// the client out of the map's client list but leaves the channel reference on the
        /// Manifestation, so a player who reached selection through /logout still has one, and
        /// gating on it would refuse a delete that is perfectly legitimate.
        ///
        /// Any delete from in the world is refused, not just of the character being played. A
        /// real client cannot ask for either, and deleting one of your other characters
        /// mid-session is no more a thing the selection screen can do.
        /// </summary>
        public void RequestDeleteCharacterInSlot(Client client, RequestDeleteCharacterInSlotPacket packet)
        {
            if (client.State == ClientState.Ingame
                || client.State == ClientState.Loading
                || client.State == ClientState.Teleporting)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to delete the character in slot {packet.Slot} "
                    + $"while in the world (state {client.State}).");

                client.CallMethod(SysEntity.ClientMethodId, new DeleteCharacterFailedPacket());
                return;
            }

            try
            {
                var charactersBySlot = client.AccountEntry.GetCharacterBySlot(packet.Slot);
                if (charactersBySlot == null)
                {
                    return;
                }

                int listings;

                using (var unitOfWork = _gameUnitOfWorkFactory.CreateChar())
                {
                    unitOfWork.CharacterAppearances.DeleteForChar(charactersBySlot.Id);

                    // An auction row names its seller by id and carries no foreign key, so a
                    // character deleted with listings running used to leave them standing:
                    // still in other players' browse results, still buyable, and still naming
                    // the character the proceeds were meant to go to. They go in the same
                    // SaveChanges as the character row, so the two cannot come apart.
                    listings = unitOfWork.Auctions.DeleteAuctionsBySeller(charactersBySlot.Id);

                    // TODO delete ClanMember entry
                    unitOfWork.Characters.Delete(charactersBySlot.Id);
                    unitOfWork.Complete();
                }

                if (listings > 0)
                    Logger.WriteLog(LogType.Debug,
                        $"Character {charactersBySlot.Id} was deleted with {listings} auction(s) running; the listings were taken down with it.");

                // Client.Player still points at the character that was just deleted - it is left
                // loaded when the player returns to character selection. Client.SaveCharacter
                // skips a player whose Id is 0 and otherwise looks the row up with
                // GetWritableEnsuring, so dropping the connection from here (Alt+F4 at the
                // selection screen) would go looking for a row that no longer exists and throw.
                // Close() catches that, so it only ever cost a misleading "Failed to save
                // character on disconnect" in the log - but there is genuinely nothing left to
                // save, and the log should not say otherwise.
                if (client.Player != null && client.Player.Id == charactersBySlot.Id)
                    client.Player.Id = 0;

                client.ReloadGameAccountEntry();

                client.CallMethod(SysEntity.ClientMethodId, new CharacterDeleteSuccessPacket(client.AccountEntry.Characters.Any()));

                SendCharacterInfo(client, packet.Slot, null);
            }
            catch
            {
                client.CallMethod(SysEntity.ClientMethodId, new DeleteCharacterFailedPacket());
            }
        }

        public void RequestSwitchToCharacterInSlot(Client client, RequestSwitchToCharacterInSlotPacket packet)
        {
            // Only from the pod screen. From the world this replaced the manifestation while
            // the old one was still in its map's cells and every manager's tables - never
            // removed, a frozen copy for everyone else, and the client in two maps at once.
            if (client.State != ClientState.CharacterSelection)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to switch to the character in slot {packet.SlotNum} while in state {client.State}.");
                return;
            }

            if (packet.SlotNum < 1 || packet.SlotNum > MaxSelectionPods)
                return;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // Look before the selected slot is changed: it used to be written first, so a
            // switch to an empty pod left the account pointing at nothing.
            var character = unitOfWork.Characters.GetByAccountId(client.AccountEntry.Id, packet.SlotNum);

            if (character == null)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to switch to slot {packet.SlotNum}, which is empty.");
                return;
            }

            client.AccountEntry.SelectedSlot = packet.SlotNum;
            unitOfWork.GameAccounts.UpdateSelectedSlot(client.AccountEntry.Id, packet.SlotNum);
            unitOfWork.Characters.UpdateLoginData(character.Id);
            unitOfWork.Complete();

            client.Player = CreateCharacterManifestation(client, character);
            client.Player.MapChannel = MapChannelManager.Instance.FindByContextId(client.Player.MapContextId);
            client.LoadingMap = client.Player.MapContextId;
            MapChannelManager.Instance.PassClientToMapInstance(client);
        }

        private void SendCharacterCreateFailed(Client client, CreateCharacterResult result)
        {
            client.CallMethod(SysEntity.ClientMethodId, new UserCreationFailedPacket(result));
        }

        private void SendCharacterInfoProdCreate(Client client, byte slot, [CanBeNull] CharacterEntry data)
        {
            var newEntityPacket = new CreatePhysicalEntityPacket(SelectionPodStartEntityId + slot, EntityClasses.CharacterSelectionPod);

            var characterInfo = CreateCharacterInfoPacket(client, slot, data);

            newEntityPacket.EntityData.Add(characterInfo);

            client.CallMethod(SysEntity.ClientMethodId, newEntityPacket);
        }

        private void SendCharacterInfo(Client client, byte slot, [CanBeNull] CharacterEntry data)
        {
            var characterInfo = CreateCharacterInfoPacket(client, slot, data);

            client.CallMethod(SelectionPodStartEntityId + slot, characterInfo);
        }

        private CharacterInfoPacket CreateCharacterInfoPacket(Client client, byte slot, [CanBeNull] CharacterEntry data)
        {
            var characterInfo = data == null
                ? new CharacterInfoPacket(slot, slot == client.AccountEntry.SelectedSlot, client.AccountEntry.FamilyName)
                : new CharacterInfoPacket(slot, slot == client.AccountEntry.SelectedSlot, client.AccountEntry.FamilyName, data);
            return characterInfo;
        }

        private Manifestation CreateCharacterManifestation(Client client, CharacterEntry character)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var characterAppearances = unitOfWork.CharacterAppearances.GetByCharacterId(character.Id);
            var appearanceData = new Dictionary<EquipmentData, AppearanceData>();
            var lockboxInfo = unitOfWork.CharacterLockboxes.Get(client.AccountEntry.Id);
            var missions = unitOfWork.CharacterMissions.Get(client.AccountEntry.Id, client.AccountEntry.SelectedSlot);
            var missionData = new Dictionary<int, MissionLog>();
            var clan = unitOfWork.Clans.GetClanByCharacterId(character.Id);
            var logos = unitOfWork.CharacterLogoses.GetLogos(character.Id);

            foreach (var appearance in characterAppearances)
                appearanceData.Add((EquipmentData)appearance.Slot, new AppearanceData(appearance));

            var newCharacter = new Manifestation(character, appearanceData)
            {
                ClanId = clan?.Id ?? 0,
                ClanName = clan?.Name,
                GainedWaypoints = unitOfWork.CharacterTeleporters.Get(character.Id),
                LockboxCredits = lockboxInfo?.Credits ?? 0,
                // Floored: the free tab is not bought, so a missing or zeroed lockbox row must
                // not cost it. Sending 0 tells the client every tab is locked, including that
                // one - and its own purchase check needs the tab below unlocked, so the player
                // would have had no lockbox at all and no way to buy one.
                LockboxTabs = Math.Max(lockboxInfo?.PurashedTabs ?? 0, LockboxTab.FreeTab),
                Skills = MapChannelManager.Instance.GetPlayerSkills(character.Id),
                Titles = unitOfWork.CharacterTitles.Get(character.Id),
                Abilities = MapChannelManager.Instance.GetPlayerAbilities(character.Id),
                Missions = missionData,
                LoginTime = DateTime.Now,
                Logos = logos
            };

            return newCharacter;
        }

        /// <summary>
        /// Applies a signed change to one of the player's balances and keeps it inside what the
        /// column can hold: never below zero, never past int.MaxValue. A clamp firing means some
        /// caller charged without checking funds first, so it is logged rather than swallowed.
        /// </summary>
        private static int ClampCurrency(Client client, CurencyType type, int change)
        {
            var balance = (long)client.Player.Credits[type] + change;

            if (balance < 0)
            {
                Logger.WriteLog(LogType.Error, $"{client.Player.FamilyName}: {type} change of {change} would leave {balance}; clamped to 0.");
                return 0;
            }

            if (balance > int.MaxValue)
            {
                Logger.WriteLog(LogType.Error, $"{client.Player.FamilyName}: {type} change of {change} would leave {balance}; clamped to {int.MaxValue}.");
                return int.MaxValue;
            }

            return (int)balance;
        }

        public void UpdateCharacter(Client client, CharacterUpdate job, object value = null)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            switch (job)
            {
                case CharacterUpdate.Attributes:
                    unitOfWork.Characters.UpdateCharacterAttributes(client.Player.Id, client.Player.SpentBody, client.Player.SpentMind, client.Player.SpentSpirit);
                    break;

                case CharacterUpdate.Class:
                    unitOfWork.Characters.UpdateCharacterClass(client.Player.Id, client.Player.Class);
                    break;

                case CharacterUpdate.CloneCredits:
                    unitOfWork.Characters.UpdateCharacterCloneCredits(client.Player.Id, client.Player.CloneCredits);
                    break;

                case CharacterUpdate.Credits:
                    // The value is a signed change, and it goes straight into the purse and the
                    // row. Nothing used to stop it landing below zero, so a charge that skipped
                    // its own funds check left the player in debt rather than being refused; the
                    // sum is widened because two large gains in a row would otherwise wrap
                    // negative and look exactly like that.
                    client.Player.Credits[CurencyType.Credits] =
                        ClampCurrency(client, CurencyType.Credits, (int)value);

                    // inform owner
                    client.CallMethod(client.Player.EntityId, new UpdateCreditsPacket(CurencyType.Credits, client.Player.Credits[CurencyType.Credits], 0));
                    // update db
                    unitOfWork.Characters.UpdateCharacterCredits(client.Player.Id, client.Player.Credits[CurencyType.Credits]);
                    break;

                case CharacterUpdate.Expirience:
                    unitOfWork.Characters.UpdateCharacterExpirience(client.Player.Id, client.Player.Experience);
                    break;

                case CharacterUpdate.Level:
                    unitOfWork.Characters.UpdateCharacterLevel(client.Player.Id, client.Player.Level);
                    break;

                case CharacterUpdate.Login:
                    // TotalMinutes, not Minutes: Minutes is the minute hand (0..59), so a
                    // session of an hour and ten minutes used to count as ten. TotalTimePlayed
                    // on the manifestation is the value loaded at login and LoginTime is set
                    // once, so the sum is right however many times this runs in one session.
                    var sessionMinutes = (long)(DateTime.Now - client.Player.LoginTime).TotalMinutes;
                    var totalTimePlayed = (uint)Math.Max(0, sessionMinutes) + client.Player.TotalTimePlayed;

                    unitOfWork.Characters.UpdateCharacterLogin(client.Player.Id, totalTimePlayed, client.Player.NumLogins);
                    break;

                case CharacterUpdate.Logos:
                    client.Player.Logos.Add((uint)value);
                    unitOfWork.CharacterLogoses.SetLogos(client.Player.Id, (uint)value);
                    client.CallMethod(client.Player.EntityId, new LogosStoneAddedPacket((uint)value));
                    break;

                case CharacterUpdate.Position:
                    var data = value as WonkavatePacket;

                    if (data != null)
                    {
                        // The character being moved is the one in the world; no need to go by
                        // the selected slot, which can name an empty pod.
                        unitOfWork.Characters.UpdateCharacterPosition(client.Player.Id, data.Position.X, data.Position.Y, data.Position.Z, data.Orientation, data.MapContextId);
                    }
                    else
                        unitOfWork.Characters.UpdateCharacterPosition(
                            client.Player.Id,
                            client.Player.Position.X,
                            client.Player.Position.Y,
                            client.Player.Position.Z,
                            client.Player.Rotation,
                            client.Player.MapContextId
                            );

                    break;

                case CharacterUpdate.Prestige:
                    // Same shape as Credits: value is the signed change, clamped the same way.
                    // Prestige was loaded into Player.Credits at login but never written back.
                    client.Player.Credits[CurencyType.Prestige] =
                        ClampCurrency(client, CurencyType.Prestige, (int)value);

                    client.CallMethod(client.Player.EntityId, new UpdateCreditsPacket(CurencyType.Prestige, client.Player.Credits[CurencyType.Prestige], 0));
                    unitOfWork.Characters.UpdateCharacterPrestige(client.Player.Id, client.Player.Credits[CurencyType.Prestige]);
                    break;

                case CharacterUpdate.Stats:
                    break;

                case CharacterUpdate.ActiveWeapon:
                    client.Player.ActiveWeapon = (byte)value;
                    unitOfWork.Characters.UpdateCharacterActiveWeapon(client.Player.Id, client.Player.ActiveWeapon);
                    break;
                case CharacterUpdate.Teleporter:
                    var teleporter = (CharacterTeleporterEntry)value;

                    unitOfWork.CharacterTeleporters.Add(teleporter);
                    break;
                default:
                    break;
            }
        }
    }
}
