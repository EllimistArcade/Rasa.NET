namespace Rasa.Structures.Char
{
    /// <summary>
    /// One line of a clan's roster as the clan window shows it: the membership, and the character
    /// and account fields that go with it. Read by <c>IClanMemberRepository.GetRoster</c> in a single
    /// query rather than stored - there is no table behind it.
    /// </summary>
    public class ClanRosterEntry
    {
        public uint ClanId { get; set; }
        public uint CharacterId { get; set; }
        public byte Rank { get; set; }
        public string Note { get; set; }
        public string CharacterName { get; set; }
        public byte Level { get; set; }
        public uint MapContextId { get; set; }
        public uint AccountId { get; set; }
        public string FamilyName { get; set; }
    }
}
