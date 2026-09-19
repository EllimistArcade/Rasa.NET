using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Rasa.Packets.Game.Client
{
    using Data;
    using Memory;
    using Packets;
    using Structures;

    public class RequestCreateCharacterInSlotPacket : ClientPythonPacket
    {
        public const double MinHeight = 0.90000000000000002;
        public const double MaxHeight = 1.0600000000000001;

        public override GameOpcode Opcode { get; } = GameOpcode.RequestCreateCharacterInSlot;

        public byte SlotNum { get; set; }
        public string FamilyName { get; set; }
        public string CharacterName { get; set; }
        public byte Gender { get; set; }
        public double Scale { get; set; }
        public Race RaceId { get; set; }

        public Dictionary<EquipmentData, AppearanceData> AppearanceData { get; } = new Dictionary<EquipmentData, AppearanceData>();

        private static readonly Regex NameRegex = new Regex(@"^\w{3,20}$", RegexOptions.Compiled);

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            SlotNum = (byte) pr.ReadInt();
            FamilyName = pr.ReadUnicodeString();
            CharacterName = pr.ReadUnicodeString();
            Gender = (byte) pr.ReadInt();
            Scale = pr.ReadDouble();

            var appearanceCount = pr.ReadDictionary();
            for (var i = 0; i < appearanceCount; i++)
            {
                var data = pr.ReadStruct<AppearanceData>();

                AppearanceData.Add(data.SlotId, data);
            }

            RaceId = (Race) pr.ReadInt();
        }

        public CreateCharacterResult Validate()
        {
            // ReadUnicodeString answers a Python None with null; the length checks below used
            // to dereference it and disconnect the client at the character screen.
            if (CharacterName == null || FamilyName == null)
                return CreateCharacterResult.InvalidEncoding;

            var characterName = ValidateName(CharacterName);

            if (characterName != CreateCharacterResult.Success)
                return characterName;

            // The family name was never checked: empty, over-long or any characters at all
            // went into account.family_name as sent, and it is shown to every other player.
            var familyName = ValidateName(FamilyName);

            if (familyName != CreateCharacterResult.Success)
                return familyName;

            if (Scale < MinHeight || Scale > MaxHeight)
                return CreateCharacterResult.InvalidCharacterHeight;

            if (RaceId < Race.Human || RaceId > Race.Thrax)
                return CreateCharacterResult.CharacterCreationInvalidRace;

            // Male or female; nothing the client offers sends anything else.
            if (Gender > 1)
                return CreateCharacterResult.InvalidEncoding;

            return CreateCharacterResult.Success;
        }

        private static CreateCharacterResult ValidateName(string name)
        {
            if (name.Length < 3)
                return CreateCharacterResult.NameTooShort;

            if (name.Length > 20)
                return CreateCharacterResult.NameTooLong;

            if (!NameRegex.IsMatch(name))
                return CreateCharacterResult.NameFormatInvalid;

            return CreateCharacterResult.Success;
        }
    }
}
