namespace Rasa.Repositories.Char
{
    using Auction;
    using Character;
    using CharacterAppearance;
    using CharacterSkills;
    using Clan;
    using ClanInventory;
    using ClanLockboxLog;
    using ClanMember;
    using GameAccount;
    using CharacterAbilityDrawer;
    using UnitOfWork;
    using CensorWord;
    using CharacterInventory;
    using CharacterLockbox;
    using CharacterLogos;
    using CharacterActionReuse;
    using CharacterMission;
    using CharacterOption;
    using CharacterTeleporter;
    using CharacterTitle;
    using Friend;
    using Ignored;
    using Items;
    using Petition;
    using UserOption;

    public interface ICharUnitOfWork : IUnitOfWork
    {
        IAuctionRepository Auctions { get; }
        ICensoredWordRepository CensoredWords { get; }
        ICharacterRepository Characters { get; }
        ICharacterAbilityDrawerRepository CharacterAbilityDrawers { get; }
        ICharacterAppearanceRepository CharacterAppearances { get; }
        ICharacterInventoryRepository CharacterInventories { get; }
        ICharacterLockboxRepository CharacterLockboxes { get; }
        ICharacterLogosRepository CharacterLogoses { get; }
        ICharacterActionReuseRepository CharacterActionReuses { get; }
        ICharacterMissionRepository CharacterMissions { get; }
        ICharacterOptionRepository CharacterOptions { get; }
        ICharacterSkillsRepository CharacterSkills { get; }
        ICharacterTeleporterRepository CharacterTeleporters { get; }
        ICharacterTitleRepository CharacterTitles { get; }
        IClanRepository Clans { get; }
        IClanInventoryRepository ClanInventories { get; }
        IClanMemberRepository ClanMembers { get; }
        IClanLockboxLogRepository ClanLockboxLogs { get; }
        IFriendRepository Friends { get; }
        IGameAccountRepository GameAccounts { get; }
        IIgnoredRepository Ignoreds { get; }
        IItemRepository Items { get; }
        IPetitionRepository Petitions { get; }
        IUserOptionRepository UserOptions { get; }
    }
}
