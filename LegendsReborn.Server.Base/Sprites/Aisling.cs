using Chaos.Common.Definitions;
using Chaos.Geometry;
using Chaos.Geometry.Abstractions.Definitions;

using Darkages.Common;
using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Managers;
using Darkages.Models;
using Darkages.Network.Client;
using Darkages.Network.Client.Abstractions;
using Darkages.Templates;
using Darkages.Types;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using ServiceStack;
using Chaos.Extensions.Common;
using Chaos.Time;
using Newtonsoft.Json.Converters;
using static ServiceStack.Diagnostics.Events;
using static System.Formats.Asn1.AsnWriter;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Darkages.Sprites;

public class KillRecord
{
    public int TotalKills { get; set; }
    public int MonsterLevel { get; set; }
    public DateTime TimeKilled { get; set; }
}
public class IgnoredRecord
{
    public string Username { get; set; }
    public int Serial { get; set; }
    public string IgnoredUser { get; set; }
}
public class Aisling : Sprite
{
    public WorldClient Client { get; set; }
    public readonly ConcurrentDictionary<uint, Sprite> SpritesInView = [];

    #region Security
    public string CreationIP { get; set; }
    public string LastIP { get; set; }
    #endregion Security
    public bool Saving { get; set; }
    public bool Loading { get; set; }
    public Dictionary<string, EphemeralReactor> ActiveReactors = new();
    public int DamageCounter = 0;
    public Dictionary<string, KillRecord> MonsterKillCounters = new();
    public List<Popup> Popups;
    public Dictionary<string, DateTime> Reactions = new();
    public HashSet<Sprite> View = new();
    private readonly object _syncLock = new();
    public ushort FormatAC = 127;
    public int MapMusic = 0;
    public bool Banned { get; set; }
    //Pill fix - New aisling variables.
    #region Quest Tracking Information
    //Quest Tracking Information
    #region From The Heart
    //Track progress for Aite Quest: 0 - Not Started || 1 - Has Accepted the Quest || 2 - Blaise has read the letter || 3 - Sent to Devlin || 4 - Devlin's Favour || 5 - Has Book || 6 - Conix Buff || 7 - Has Conix || 8 - Blaise Request || 9 - Enter Dark Maze || 10 - Quest Complete
    public int AiteQuest { get; set; }
    //Ritual of the Sun( Gave up Vita)
    public int Sun { get; set; }
    //Ritual of the Moon (Gave up Mana)
    public int Moon { get; set; }
    //Does Devlin trust the player? If so, allow quest for 41 armour
    public bool TrustDevlin { get; set; }
    #endregion From The Heart
    #region Devlin's Requests
    // 0 == No Quest || 1 == Bee Sting Done || 2 == Raw Wax Done || 3 == Raw Honey Done
    public int DevlinQuest { get; set; }
    //Does the player have Beag Ioc Fein?: 0 - no || 1 - yes
    public int Devlinbif { get; set; }
    #endregion Devlin's Requests
    #region Dark Things
    //Dar's Quests: 0 - None || 1 - Spider's Eye || 2 - Spider's Silk || 3 - Centipede's Gland || 4 - Bat's Wing || 5 - Scorpion's Sting || 6 - Great Bat's Wing || 7 - Mimic's Fang || 8 - White Bat's Wing || 9 - Kardi Fur || 10 - Marauder's Spine || 11 - Succubus's Hair || 12 - Wraith's Blood
    public int Dar { get; set; }
    //Dar's Final Reward: Aosdan Relic - Transports player to Kadath to begin/complete Master/Sub quests.
    public int DarReward { get; set; }
    #endregion Dark Things
    #region An Honest Fae
    //41 Armor Quest: 0 - Incomplete || 1- Complete
    public int Circle2Armor { get; set; }
    #endregion
    #region Spare Equipment
    //Spare Equipment Var.  0 - None || 1 - Travel from Mileth > Black Market || 
    public int Whetstone { get; set; }
    #endregion Spare Equipment
    #region A New Beginning
    public bool RionaQuest { get; set; }
    #endregion A New Beginning
    #region A Dark Future
    public int DarkFuture { get; set; }
    public int DarkMistake { get; set; }
    #endregion A Dark Future
    #region Royal Betrayal
    public int RoyalBetrayal { get; set; }
    #endregion Royal Betrayal
    #region A Waking Nightmare
    public int Nightmare { get; set; }
    public int NightmareReplacement { get; set; }
    #endregion A Waking Nightmare
    #region The Missing Queen
    public int FaeQueen { get; set; }
    #endregion The Missing Queen
    #region Pentagram
    public int Pentagram { get; set; }
    public bool PentaComplete { get; set; }
    #endregion Pentagram
    #region Sage
    public int Sage { get; set; }
    public DateTime SageTimer { get; set; }
    public bool SageAbuse { get; set; } = false;
    #endregion Sage
    #region The Perfect Loaf
    public int PerfectLoaf { get; set; }
    public DateTime PerfectLoafTimer { get; set; }
    #endregion The Perfect Loaf
    #region Mukul Flowers
    public int MukulFlower { get; set; }
    #endregion Mukul Flowers
    #region Karlopos Quest
    public int KarloposQuest { get; set; }
    #endregion Karlopos Quest
    #region
    public int WizardPendant { get; set; }
    #endregion
    #endregion Quest Tracking Information
    #region Crafting

    public Dictionary<string, string> WeavingRecipes = new();
    public Dictionary<string, string> GemSmithingRecipes = new();
    public Dictionary<string, string> SmeltingRecipes = new();
    public Dictionary<string, string> TailoringRecipes = new();
    public Dictionary<string, string> SmithingRecipes = new();
    public Dictionary<string, string> ForgingRecipes = new();
    public Dictionary<string, string> AlchemyRecipes = new();
    public Dictionary<string, string> JewelingRecipes = new();

    public int MiningSuccess { get; set; }
    public int MiningSkill { get; set; }
    public int HarvestSuccess { get; set; }
    public int HarvestSkill { get; set; }
    // Player's Herbalism Rank: 0 - Not an Herbalist || 1 - Initial Rank || 11 - Master Herbalist || 12 - Legendary Herbalist
    public int Herbalism { get; set; }
    // Player's Harvesting Successes: Rank 1: 20 || Rank 2: 40 || Rank 3: 60 || Rank 4: 80 || Rank 5: 100 || Rank 6: 300 || Rank 7: 500 || Rank 8: 750 || Rank 9: 1000 || Master: 1500 || Legendary: 10000
    public int HerbSuccess { get; set; }
    // Player's Gem Cutting Rank: 0 - Not a Gemcutter || 1 - Initial Rank || 11 - Master Gemcutter || 12 - Legendary Gemcutter
    public int GemCuttingSkill { get; set; }
    // Player's Gem Cutting Successes: Rank 1: 20 || Rank 2: 40 || Rank 3: 60 || Rank 4: 80 || Rank 5: 100 || Rank 6: 300 || Rank 7: 500 || Rank 8: 750 || Rank 9: 1000 || Master: 1500 || Legendary: 10000
    public int GemSuccess { get; set; }
    // Player's Gem Smithing Rank: 0 - Not a Gem Smith || 1 - Initial Rank || 11 - Master Jeweller || 12 - Legendary Jeweller
    public int JewelCraftingSkill { get; set; }
    // Player's Gem Smithing Successes: Rank 1: 20 || Rank 2: 40 || Rank 3: 60 || Rank 4: 80 || Rank 5: 100 || Rank 6: 300 || Rank 7: 500 || Rank 8: 750 || Rank 9: 1000 || Master: 1500 || Legendary: 10000
    public int JewelCraftingSuccess { get; set; }
    //Which crafting material?: 0 - None || 1 - Leather || 2 - Iron || 3 - Mythril || 4 - Hy-Brasyl || 5 - Master
    public int CraftMaterial { get; set; }
    //Which Gem?  0 - None || 1 - Beryl || 2 - Emerald || 3 - Ruby || 4 - Sapphire
    public int Gem { get; set; }
    //Which Gem Type? 0 - none || 1 - Raw || 2 - Uncut || 3 - Finished
    public int GemType { get; set; }
    //Which Item Slot?: 0 - None || 1 - Gauntlet || 2 - Greaves || 3 - Bracer || 4 - Necklace || 5 - Earring ||
    public int CraftSlot { get; set; }
    //Which Gemstone?: 0 - None || 1 - Sapphire || 2 - Ruby || 3 - Emerald || 4 - Talos (Master only) || 5 - Hy-Brasyl (Master only) ||
    public int CraftStone { get; set; }
    //Player's Weaving Skill Level
    public int WeavingSkill { get; set; }
    //Player's Weaving Successes
    public int WeavingSuccess { get; set; }
    //Player's Tailoring skill level
    public int TailoringSkill { get; set; }
    //Player's Tailoring Successes
    public int TailoringSuccess { get; set; }
    public int SmeltingSkill { get; set; }
    public int SmeltingSuccess { get; set; }
    public int AlchemySkill { get; set; }
    public int AlchemySuccess { get; set; }
    //What kind of potion is being brewed?  0 - None || 1 - Ioc || 2 - Spiorad || 3 - Cothromach || 4 - Betony || 5 - Persica || 6 - Beothaich || 7 - Hemloch ||
    public int PotionType { get; set; }
    // What size of potion is being brewed? 0 - None || 1 - Mion || 2 - Base || 3 - Lan || 4 - Ainmeal || 5 - Uasal ||
    public int PotionSize { get; set; }
    public int SmithingSkill { get; set; }
    public int SmithingSuccess { get; set; }
    public int WeaponChoice { get; set; }
    public int ForgingSkill { get; set; }
    public int ForgingSuccess { get; set; }
    //Crafting Circle 0 - None || 6 - Master ||
    public int CraftCircle { get; set; }
    //Crafting Class 0 - None || 1 - Monk || 2 - Priest || 3 - Wizard || 4 - Rogue || 5 - Warrior ||
    public int CraftClass { get; set; }
    //Crafting for which sex? - 0 - None || 1 - Male || 2 - Female || 3 - All ||
    public int CraftSex { get; set; }
    //Ring Crafting Materials: 0 - None || 1 - Bronze || 2 - Silver || 3 - Mythril
    public int RingMat { get; set; }
    // 0 - None || 1 - Beryl || 2 - Ruby || 3 - Sapphire || 4 - Emerald
    public int RingGem { get; set; }
    // 0 - None || 1 - Beryl || 2 - Ruby || 3 - Sapphire || 4 - Emerald
    public int NecklaceType { get; set; }
    //0 - None || 1 - Basic|| 2 - Flawed || 3 - Fine || 4 - Flawless|| 5 - Perfect||
    public int NecklaceGrade { get; set; }
    public int CraftedSlot { get; set; }
    public int Fiber { get; set; }
    public string SmithName { get; set; }
    public int RecipeDifficulty { get; set; }

    #endregion
    #region Religion
    //How many times has the player gotten a Holy Danaan?
    public int DanaanCount { get; set; }
    //Faith tracker for religion
    public int Faith { get; set; }
    //Which faith is the player? 0 - None || 1 - Deoch || 2 - Glioca || 3 - Cail || 4 - Luathas || 5 - Gramail || 6 - Fiosachd || 7 - Ceannlaidir || 8 - Sgrios
    public int Religion { get; set; }
    //Has the player already received a set of holy scriptures?
    public int Scripture { get; set; }
    //Who initiated you?
    public string Initiator { get; set; }
    //How many initiates?
    public int InitiateCount { get; set; }
    //How long since the player last prayed?
    public DateTime LastPrayed { get; set; }
    [JsonIgnore] bool CanPray;
    //String used by religious npcs for any interaction with ReactorSequences.
    public string Initiate { get; set; }
    public bool DepositingStackableItem { get; set; }
    //How long since the last geas?
    public DateTime LastGeas { get; set; }
    [JsonIgnore] bool CanGeas;
    //Timers used by religious npcs for Mass attendance
    public DateTime DeochMass { get; set; }
    public DateTime GliocaMass { get; set; }
    public DateTime CailMass { get; set; }
    public DateTime LuathasMass { get; set; }
    public DateTime GramailMass { get; set; }
    public DateTime FiosachdMass { get; set; }
    public DateTime CeannlaidirMass { get; set; }
    public DateTime SgriosMass { get; set; }
    //Timer used by religious npcs for Mass hosts
    public DateTime HostMass { get; set; }
    //How many attended masses?
    public int MassCount { get; set; }
    [JsonIgnore] bool CanMass { get; set; }
    public bool AddWorshipper { get; set; }
    public bool LesserGeas { get; set; }
    public bool GreaterGeas { get; set; }
    public int Catalyst { get; set; }
    public DateTime GramailPort { get; set; }
    public void RemoveScar(Aisling priest, Aisling target)
    {
        priest.Faith -= 25;
        target.Scars -= 1;
        target.Animate(4);
        target.Client.ScarLegend();
        if (target.Scars > 0)
            target.Client.Aisling.LegendBook.AddLegend(target.Client, new Legend.LegendItem
            {
                Category = "Scar",
                Color = (byte)LegendColor.Mahogany,
                Icon = (byte)LegendIcon.Victory,
                Value = string.Format("Scar of Sgrios ({0})", target.Client.Aisling.Scars)
            });

    }

    #endregion Religion
    #region Scars
    public int Scars { get; set; }
    public bool HealScar { get; set; }
    public bool GrantScar { get; set; }
    #endregion Scars
    #region Hairstyling
    public int Styling { get; set; }
    public int SlotNumber { get; set; }
    #endregion Hairstyling
    #region Mentor
    public string MentoredBy { get; set; }
    public bool HasMentor { get; set; }
    public bool NewStudent { get; set; }
    public bool OldStudent { get; set; }
    public string StudentName { get; set; }
    public string MentorName { get; set; }
    public DateTime MentorStart { get; set; }
    public int Students { get; set; }
    #endregion Mentor
    #region Gold Banking
    public int BankedGold { get; set; }
    public int ConfirmGold { get; set; }
    public bool DepositGold { get; set; }
    public bool WithdrawGold { get; set; }
    #endregion Gold Banking
    #region Higgle
    public int Higgle { get; set; }
    public DateTime HiggleTimer { get; set; }
    #endregion Higgle
    #region Kasmanium Mines
    //Mines unlock variable
    public int Mines { get; set; }
    //Maximum floor of the mines
    public int KasmaniumFloor { get; set; }
    //Current floor of the mines
    public int MinesFloor { get; set; }
    #endregion Kasmanium Mines
    #region Tutorial and Knowledge
    public int FaeGaelic { get; set; }
    public int Dornan { get; set; }
    public int Richter { get; set; }
    public int Tulia { get; set; }
    public int Siobhan { get; set; }
    public int Lothe { get; set; }
    #endregion Tutorial and Knowledge
    #region Monk Dojo
    public int Dojo { get; set; }
    #endregion Monk Dojo
    #region Gathering
    //when did the aisling last harvest something?
    public DateTime LastHarvested { get; set; }
    //can the player harvest?
    [JsonIgnore] bool CanHarvest;
    public int PlantType { get; set; }

    #endregion Gathering
    #region Arena Variables
    //Which team is the Aisling on? 0 == None || 1 == Red || 2 == Blue || 86 == Host
    public int Team { get; set; }
    //Is the player a Guardian?  true == yes || false == no
    public bool Guardian { get; set; }
    //Which event is being hosted? 0 == None || 1 == Battle || 2 = Bomb || 3 == Guardians
    public int Event { get; set; }
    //Does the Aisling get a prize? 0 == No Entry || 1 == Winner || 2 == Participant ||
    public int ArenaPrize { get; set; }
    public int Victories { get; set; }
    public int Participation { get; set; }
    public int BombCount { get; set; }
    public int BombLimit { get; set; }
    public int BombRange { get; set; }
    public bool ArenaEliminated { get; set; }

    #endregion Arena Variables
    #region Clan Variables
    //Clan Creation Steps - 1 = Check Name || 2 - Appoint Primarch || 3 - || 4 - || 5 - Demote Primarch || 6 - Demote Council || 7 - Remove Member
    public int ClanCreation { get; set; }
    public string NewClanName { get; set; }
    public int ClanBanking { get; set; }
    public bool WakeInClan { get; set; }
    #endregion Clan Variables
    #region Political Variables
    #endregion Political Variables
    #region Beastman Variables
    //Goblins
    public int Goblins { get; set; }
    //Grimloks
    public int Grimloks { get; set; }
    // 1 = Goblin, 2 = Grimlok
    public int Alliance { get; set; }
    //Astrid Kobolds
    public int AstKobolds { get; set; }
    public DateTime AllianceTimer { get; set; }
    #endregion Beastman Variables
    #region Farm Variables
    public int Grapes { get; set; }
    public int Cherries { get; set; }
    #endregion Farm Variables
    #region Parcels
    public string Recipient { get; set; }
    #endregion Parcels
    #region Teleports
    public bool AbelCrypt { get; set; }
    public bool MilethCrypt { get; set; }
    public bool PietSewer { get; set; }
    public bool CthonicTen { get; set; }
    public bool CthonicTwenty { get; set; }
    public bool Conflux { get; set; }
    public int QuestProg { get; set; }
    #endregion Teleports
    #region Class Change
    public Class OriginalPath { get; set; }
    public Class PathChoice { get; set; }
    //0 = pure || 44 = sub || 99 = same class sub
    public int Subbed { get; set; }
    //public bool MeetsSubRequirements()
    //{

    //}

    #endregion Class Change
    #region Rogue Dagger
    public int RogueDagger { get; set; }
    #endregion Rogue Dagger
    #region Master
    public int MasterBoss { get; set; }
    public int TrialOfAmbition { get; set; }
    public int TrialOfCommunity { get; set; }
    public int TrialOfKnowledge { get; set; }
    public int TrialOfSkill { get; set; }
    public int TrialOfStrength { get; set; }
    public int TrialOfWealth { get; set; }
    public int MasterAbilityQuest { get; set; }
    public DateTime MasterAbilityQuestTimer { get; set; }
    public DateTime MasterAbilityRetryTimer { get; set; }

    #endregion Master
    #region Temporary Variables
    public int StatRefund { get; set; }
    public int Bank_Quantity { get; set; }
    public string RelInitiate { get; set; }
    public string RelPray { get; set; }
    public string RelGeas { get; set; }
    public string RelEntreat { get; set; }
    public bool Pray { get; set; }
    public int Alpha { get; set; }
    public int Beta { get; set; }

    #endregion Temporary Variables
    #region Cosmetics
    public uint Lazulite { get; set; }
    #endregion Cosmetics
    #region Oren Ruins
    public int Ruins { get; set; }
    #endregion Oren Ruins
    #region RaidVariables
    public int EarthRaidPoints { get; set; }
    public int WindRaidPoints { get; set; }
    public int FireRaidPoints { get; set; }
    public int WaterRaidPoints { get; set; }
    public int SpiritRaidPoints { get; set; }
    public DateTime EarthTimer { get; set; }
    public DateTime WindTimer { get; set; }
    public DateTime FireTimer { get; set; }
    public DateTime WaterTimer { get; set; }
    public DateTime SpiritTimer { get; set; }
    //0 - None || 1 - Earth || 2 - Wind || 3 - Fire || 4 - Water || 5 - Spirit||
    public int MasterRaidProgress { get; set; }
    #endregion RaidVariables
    #region Enhanced Master Armor
    public int EnhancedMasterArmor { get; set; }
    #endregion Enhanced Master Armor
    public DateTime OrderTimer { get; set; }

    public Aisling()
    {
        OffenseElement = ElementManager.Element.None;
        DefenseElement = ElementManager.Element.None;
        Clan = string.Empty;
        LegendBook = new Legend();
        ClanTitle = string.Empty;
        ClanRank = string.Empty;
        LoggedIn = false;
        CreationIP = string.Empty;
        SpouseName = string.Empty;
        ActiveStatus = ActivityStatus.Awake;
        PortalSession = new PortalSession();
        PartyStatus = GroupStatus.AcceptingRequests;
        AllianceTimer = DateTime.Now - TimeSpan.FromDays(1);
        PerfectLoafTimer = DateTime.Now - TimeSpan.FromDays(1);
        LastPrayed = DateTime.Now - TimeSpan.FromDays(15);
        LastGeas = DateTime.Now - TimeSpan.FromDays(15);
        DeochMass = DateTime.Now - TimeSpan.FromDays(15);
        GliocaMass = DateTime.Now - TimeSpan.FromDays(15);
        CailMass = DateTime.Now - TimeSpan.FromDays(15);
        LuathasMass = DateTime.Now - TimeSpan.FromDays(15);
        GramailMass = DateTime.Now - TimeSpan.FromDays(15);
        FiosachdMass = DateTime.Now - TimeSpan.FromDays(15);
        CeannlaidirMass = DateTime.Now - TimeSpan.FromDays(15);
        SgriosMass = DateTime.Now - TimeSpan.FromDays(15);
        GramailPort = DateTime.Now - TimeSpan.FromDays(15);
        HostMass = DateTime.Now - TimeSpan.FromDays(15);
        LastHarvested = DateTime.Now - TimeSpan.FromDays(15);
        MasterAbilityQuestTimer = DateTime.Now - TimeSpan.FromMinutes(20);
        MasterAbilityRetryTimer = DateTime.Now - TimeSpan.FromHours(24);
        HiggleTimer = DateTime.Now - TimeSpan.FromHours(3);
        SageTimer = DateTime.Now - TimeSpan.FromDays(45);
        OrderTimer = DateTime.Now - TimeSpan.FromDays(7);
        EarthTimer = DateTime.Now - TimeSpan.FromDays(7);
        WindTimer = DateTime.Now - TimeSpan.FromDays(7);
        FireTimer = DateTime.Now - TimeSpan.FromDays(7);
        WaterTimer = DateTime.Now - TimeSpan.FromDays(7);
        SpiritTimer = DateTime.Now - TimeSpan.FromDays(7);
        Remains = new CursedSachel(this);
        DiscoveredMaps = new List<int>();
        Popups = new List<Popup>();
        IgnoredList = new List<string>();
        WeavingRecipes = new Dictionary<string, string>();
        GemSmithingRecipes = new Dictionary<string, string>();
        SmeltingRecipes = new Dictionary<string, string>();
        TailoringRecipes = new Dictionary<string, string>();
        ForgingRecipes = new Dictionary<string, string>();
        SmithingRecipes = new Dictionary<string, string>();
        AlchemyRecipes = new Dictionary<string, string>();
        JewelingRecipes = new Dictionary<string, string>();
        GroupId = 0;
    }
    public bool ShouldWalk => !ServerSetup.Instance.Config.ProhibitF5Walk || !(DateTime.UtcNow.Subtract(Client.LastClientRefresh).TotalMilliseconds < 150);
    public int AbpLevel { get; set; }
    public int AbpNext { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public ActivityStatus ActiveStatus { get; set; }
    public AnimalForm AnimalForm { get; set; }
    public int AreaId => CurrentMapId;
    public ushort Armor { get; set; }
    public Bank Bank { get; set; }
    public byte Blind { get; set; }
    //{
    //    get
    //    {
    //        var blind = HasDebuff("blind");
    //        return blind ? (byte)0x08 : (byte)0x00; 
    //    }
    //}
    public int BodyColor { get; set; }
    public int BodyStyle { get; set; }
    public byte BootColor { get; set; }
    public byte Boots { get; set; }
    public bool CanReact { get; set; }
    public string Clan { get; set; }
    public string ClanRank { get; set; }
    public string ClanTitle { get; set; }
    public DateTime Created { get; set; }
    public int CurrentWeight { get; set; }
    public bool Dead => IsDead();
    public List<int> DiscoveredMaps { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public BodySprite Display { get; set; }
    public EquipmentManager EquipmentManager { get; set; }
    public ExchangeSession Exchange { get; set; }
    public int ExpLevel { get; set; }
    public uint ExpNext { get; set; }
    public uint ExpTotal { get; set; }
    public int FaceColor { get; set; }
    public int FaceStyle { get; set; }
    public AislingFlags Flags { get; set; }
    public bool GameMaster { get; set; }
    //Pill Fix - Flag for anyone given the power to host events.
    public bool EventHost { get; set; }
    public bool Developer { get; set; }
    public int GamePoints { get; set; }
    public UserOptions GameSettings { get; init; } = new();

    [JsonConverter(typeof(StringEnumConverter))]
    public Enums.Gender Gender { get; set; }
    public uint GoldPoints { get; set; }
    public Party GroupParty => ServerSetup.Instance.GlobalGroupCache.ContainsKey(GroupId)
        ? ServerSetup.Instance.GlobalGroupCache[GroupId]
        : null;
    public byte HairColor { get; set; }
    public ushort HairStyle { get; set; }
    public byte HeadAccessory1 { get; set; }
    public byte HeadAccessory2 { get; set; }
    public ushort Helmet { get; set; }
    public Inventory Inventory { get; set; }
    public bool Invisible { get; set; }
    public bool CanSeeInvis => Buffs.ContainsKey("true sight") || Buffs.ContainsKey("eisd creutair");
    public bool IsCastingSpell { get; set; }
    public DateTime LastLogged { get; set; }
    public int LastMapId { get; set; }
    public bool LeaderPrivileges
    {
        get
        {
            if (!ServerSetup.Instance.GlobalGroupCache.ContainsKey(GroupId))
                return false;

            var group = ServerSetup.Instance.GlobalGroupCache[GroupId];
            return (@group != null) && (@group.LeaderName.ToLower() == Username.ToLower());
        }
    }
    public Legend LegendBook { get; set; }
    public bool LoggedIn { get; set; }
    public int MaximumWeight => (int)(ExpLevel / 4 + _Str + ServerSetup.Instance.Config.WeightIncreasemodifier);
    public ushort MonsterForm { get; set; }
    public byte NameColor { get; set; }
    public string Nation { get; set; } = "Mileth"; // default nation.
    public ushort OverCoat { get; set; }
    public byte OverCoatColor { get; set; }
    public byte Pants { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public GroupStatus PartyStatus { get; set; }
    public string Password { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public Class Path { get; set; }
    public byte[] PictureData { get; set; }
    public NationTemplate PlayerNation
    {
        get
        {
            if (Nation != null)
                return ServerSetup.Instance.GlobalNationTemplateCache[Nation];
            throw new InvalidOperationException();
        }
    }
    public PortalSession PortalSession { get; set; }
    public new Position Position => new(XPos, YPos);
    public string ProfileMessage { get; set; }
    public bool ProfileOpen { get; set; }
    public bool ReactorActive { get; set; }
    public CursedSachel Remains { get; set; }
    public byte Resting { get; set; }
    public byte Shield { get; set; }
    public SkillBook SkillBook { get; set; }
    public bool Skulled => HasDebuff("skulled");
    public SpellBook SpellBook { get; set; }
    public string SpouseName { get; set; }
    public ClassStage Stage { get; set; }
    public int StatPoints { get; set; }
    public int Title { get; set; }
    public bool TutorialCompleted { get; set; }
    public string Username { get; set; }
    public bool UsingTwoHanded { get; set; }
    public byte Weapon { get; set; }
    public int World { get; set; } = 1;
    public List<Aisling> PartyMembers => GroupParty?.PartyMembers;
    public byte Lantern { get; set; }
    public Summon SummonObjects { get; set; }
    public int StageMarker { get; set; }
    public bool WithdrawMultiple { get; set; }
    public int SilverMoon { get; set; }
    public int Nunchaku { get; set; }
    public int LostExp { get; set; }
    public bool HealExp { get; set; }
    public int ElixirWin { get; set; }
    public int ElixirPlay { get; set; }
    public List<string> IgnoredList { get; set; }
    public int ExchangeID { get; set; }
    public bool UndineAlliance { get; set; }
    public int Weddings { get; set; }
    public ResettingCounter SpellThrottle { get; set; }
    public ResettingCounter SkillThrottle { get; set; }
    public ResettingCounter ActionThrottle { get; set; }
    public ChantTimer ChantTimer { get; set; }
    public ResettingCounter WalkCounter { get; set; }
    public int Valentine { get; set; }

    public void SendTargetedClientMethod(PlayerScope op, Action<IWorldClient> method, IEnumerable<Aisling> definer = null)
    {
        var selectedPlayers = new List<Aisling>();

        switch (op)
        {
            case PlayerScope.NearbyAislingsExludingSelf:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && WithinRangeOf(otherPlayers)).Where(player => player.Serial != Serial));
                break;
            case PlayerScope.NearbyAislings:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && WithinRangeOf(otherPlayers)));
                break;
            case PlayerScope.Clan:
                selectedPlayers.AddRange(GetObjects<Aisling>(null, otherPlayers => otherPlayers != null && !string.IsNullOrEmpty(otherPlayers.Clan) && string.Equals(otherPlayers.Clan, Clan, StringComparison.CurrentCultureIgnoreCase)));
                break;
            case PlayerScope.VeryNearbyAislings:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && WithinRangeOf(otherPlayers, ServerSetup.Instance.Config.VeryNearByProximity)));
                break;
            case PlayerScope.AislingsOnSameMap:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && CurrentMapId == otherPlayers.CurrentMapId));
                break;
            case PlayerScope.GroupMembers:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && GroupParty.Has(otherPlayers)));
                break;
            case PlayerScope.NearbyGroupMembersExcludingSelf:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && WithinRangeOf(otherPlayers) && GroupParty.Has(otherPlayers)).Where(player => player.Serial != Serial));
                break;
            case PlayerScope.NearbyGroupMembers:
                selectedPlayers.AddRange(GetObjects<Aisling>(Map, otherPlayers => otherPlayers != null && WithinRangeOf(otherPlayers) && GroupParty.Has(otherPlayers)));
                break;
            case PlayerScope.DefinedAislings when definer == null:
                return;
            case PlayerScope.DefinedAislings:
                selectedPlayers.AddRange(definer);
                break;
            case PlayerScope.All:
                selectedPlayers.AddRange(ServerSetup.Instance.Game.Aislings);
                break;
            case PlayerScope.Self:
            default:
                method(Client);
                return;
        }
    }

    public bool CanSeeGhosts() => IsDead();
    public bool CastDeath()
    {
        if (!Client.Aisling.Flags.HasFlag(AislingFlags.Ghost))
        {
            LastMapId = CurrentMapId;
            LastPosition = Position;

            Client.CloseDialog();
            Client.AislingToGhostForm();
            return true;
        }

        return false;
    }
    public void CastSpell(Spell spell, CastInfo castInfo)
    {
        if (!spell.CanUse(this))
            return;

        if (spell.Scripts == null)
            return;

        var scripts = spell.Scripts.Values;

        if (!string.IsNullOrEmpty(castInfo.Data))
            foreach (var script in scripts)
                script.Arguments = castInfo.Data;

        var target = GetObject(Map, i => i.Serial == castInfo.Target, Get.Monsters | Get.Aislings | Get.Mundanes);

        if (!SpellThrottle.TryIncrement())
            return;

        if (target != null)
            foreach (var script in scripts)
                script.OnUse(this, target);
        else if
            (spell.Template.TargetType
             != SpellTemplate.SpellUseType
                 .ChooseTarget) //if there is no target, dont auto target self for spells where you have to choose a target
            foreach (var script in scripts)
                script.OnUse(this, this);
        else
            return;

        if (spell.Template.Cooldown > 0)
        {
            spell.NextAvailableUse = DateTime.UtcNow.AddSeconds(spell.Template.Cooldown);
            Client.Send(new ServerFormat3F(0, spell.Slot, spell.Template.Cooldown));
        }
        else
            spell.NextAvailableUse = DateTime.UtcNow.AddSeconds(castInfo.SpellLines > 0 ? 0 : 0.34);

        Client.Aisling.IsCastingSpell = false;

    }
    public Skill[] GetAssails() =>
        SkillBook.Get(i => (i != null) && (i.Template != null)
                                       && (i.Template.Type == SkillScope.Assail)).ToArray();
    public bool GiveGold(uint offer, bool sendClientUpdate = true)
    {
        if (GoldPoints + offer < ServerSetup.Instance.Config.MaxCarryGold)
        {
            GoldPoints += offer;
            return true;
        }

        if (sendClientUpdate)
            Client?.SendStats(StatusFlags.ExpGold);

        return false;
    }
    public Aisling GiveHealth(Sprite target, int value)
    {
        target.CurrentHp += value;

        if (target.CurrentHp > target.MaximumHp)
            target.CurrentHp = target.MaximumHp;

        return this;
    }
    public void GoHome()
    {
        if (Client.Aisling.EventHp != null || Client.Aisling.EventMp != null || Client.Aisling.Team != 0)
        {
            Client.Aisling.EventHp = null;
            Client.Aisling.EventMp = null;
            Client.Aisling.Team = 0;
        }

        var destinationMap = Client.Aisling.PlayerNation.AreaId;
        if (Client.Aisling.TutorialCompleted)
        {
            if (!string.IsNullOrEmpty(Client.Aisling.ClanName()) && Client.Aisling.WakeInClan)
            {
                var clanTemplate = ServerSetup.Instance.GlobalClanTemplateCache[Client.Aisling.ClanName()];
                var x = clanTemplate.HallX;
                var y = clanTemplate.HallY;
                var areaID = clanTemplate.HallID;
                if (areaID != 0)
                    Client.TransitionToMap(areaID, new Position(x, y));
                else
                    Client.TransitionToMap(destinationMap, Client.Aisling.PlayerNation.MapPosition);
            }
            else if (ServerSetup.Instance.GlobalMapCache.ContainsKey(destinationMap))
                Client.TransitionToMap(destinationMap, Client.Aisling.PlayerNation.MapPosition);
        }
        else
            Client.TransitionToMap(11364, new Position(2, 7));
    }
    public bool HasKilled(string value, int number)
    {
        if (MonsterKillCounters.ContainsKey(value))
            return MonsterKillCounters[value].TotalKills >= number;

        return false;
    }
    public bool HasSkill(string skillname) => SkillBook.Skills.Values.Any(skill => skill?.Template?.Name?.EqualsI(skillname) == true);
    public bool IsDead()
    {
        var result = Flags.HasFlag(AislingFlags.Ghost);
        return result;
    }
    public void Remove(bool update = false, bool delete = true)
    {
        if (update)
            Show(Scope.NearbyAislingsExludingSelf, new ServerFormat0E(Serial));

        try
        {
            if (delete)
            {
                var objs = GetObjects(Map,
                    i => i.WithinViewOf(this, false) && (i.Target != null) && (i.Target.Serial == Serial),
                    Get.Monsters | Get.Mundanes);

                if (objs != null)
                    foreach (var obj in objs)
                        obj.Target = null;
            }
        }
        finally
        {
            if (delete)
                DelObject(this);
        }
    }
    public void ReviveInFront()
    {
        var infront = GetInfront().OfType<Aisling>();
        foreach (var obj in infront)
        {
            if (obj.Serial == Serial)
                continue;

            if (!obj.LoggedIn)
                continue;
            if (obj.Client.Aisling.IsDoomed)
            {
                obj.Client.Aisling.RemoveDebuff("doom", true);
                obj.CurrentHp = Convert.ToInt32(obj.MaximumHp * 0.1);
                obj.Client.SendStats(StatusFlags.All);
            }
            if (obj.HasDebuff("skulled"))
            {
                obj.RemoveDebuff("skulled", true);
                obj.Client.Revive();
                obj.CurrentHp = Convert.ToInt32(obj.MaximumHp * 0.1);
                obj.Client.SendStats(StatusFlags.All);
            }
            obj.Animate(5);
            obj.SendSound(8);
        }
    }
    public void SendToHell()
    {
        if (!ServerSetup.Instance.GlobalMapCache.ContainsKey(ServerSetup.Instance.Config.DeathMap))
            return;

        if (CurrentMapId == ServerSetup.Instance.Config.DeathMap)
            return;

        Remains.Owner = this;

        if (!Inventory.IsFull || (EquipmentManager.Length > 0))
            Remains.ReepItems();

        for (var i = 0; i < 2; i++)
            RemoveBuffsAndDebuffs();

        WarpToHell();
    }
    public IEnumerable<Item> FlattenItems(params Item[] items)
    {
        foreach (var item in items)
        {
            if (!item.Template.CanStack && (item.Stacks > 1))
            {
                for (var i = 0; i < item.Stacks; i++)
                {
                    var newItem = Item.Create(this, item.Template.Name);
                    newItem.Stacks = 1;

                    yield return newItem;
                }

                continue;
            }

            if (item.Template.CanStack && (item.Stacks > item.Template.MaxStack))
            {
                var numMaxStacks = item.Stacks / item.Template.MaxStack;
                var extra = (ushort)(item.Stacks % item.Template.MaxStack);

                for (var i = 0; i < numMaxStacks; i++)
                {
                    var newItem = Item.Create(this, item.Template.Name);
                    newItem.Stacks = newItem.Template.MaxStack;

                    yield return newItem;
                }

                if (extra > 0)
                {
                    var newItem = Item.Create(this, item.Template.Name);
                    newItem.Stacks = extra;

                    yield return newItem;
                }

                continue;
            }

            yield return item;
        }
    }
    public bool CanCarry(params Item[] items) => CanCarry(items.Select(item => (item, (int)item.Stacks)));
    public bool CanCarry(IEnumerable<(Item Item, int Count)> hypotheticalItems)
    {
        var weightSum = 0;
        var slotSum = 0;

        //group all separated stacks together by summing their counts
        foreach (var set in hypotheticalItems.GroupBy(
                     set => set.Item.DisplayName,
                     (_, grp) =>
                     {
                         var col = grp.ToList();

                         return (col.First().Item, Count: col.Sum(i => i.Count));
                     }))
        {
            var weightlessAllowance = 0;

            //for stackable items, we can fill the existing stacks in our inventory without adding any weight
            if (set.Item.Template.CanStack)
            {
                var numUniqueStacks = Inventory.Snapshot().Count(i => i.DisplayName.EqualsI(set.Item.DisplayName));
                var totalCount = Inventory.CountOf(set.Item.DisplayName);
                var maxCount = set.Item.Template.MaxStack * numUniqueStacks;

                var allowedCount = numUniqueStacks == 0 ? set.Item.Template.MaxStack : set.Item.Template.MaxStack - totalCount;

                if (set.Count > allowedCount)
                    return false;
                //so we calculate that value and subtract it from the count we're using to calculate how much this item will weigh
                weightlessAllowance = maxCount - totalCount;
            }

            //separate each stack into its most condensed possible form
            var maxStacks = Math.Max(set.Item.Template.MaxStack, (byte)1);
            //the number of stacks we will actually need to add to the inventory
            var countActual = Math.Max(0, set.Count - weightlessAllowance);
            var estimatedStacks = (int)Math.Ceiling(countActual / (decimal)maxStacks);
            weightSum += set.Item.Template.CarryWeight * estimatedStacks;
            slotSum += estimatedStacks;
        }

        return (CurrentWeight + weightSum <= MaximumWeight) && (Inventory.AvailableSlots >= slotSum);
    }
    public bool CanCarry(params (Item Item, int Count)[] hypotheticalItems) => CanCarry(hypotheticalItems.AsEnumerable());
    public bool GiveManyItems(string itemName, int amount)
    {
        var overstackedItem = Item.Create(this, itemName);

        if (overstackedItem == null)
            return false;

        overstackedItem.Stacks = (ushort)amount;

        var items = FlattenItems(overstackedItem)
            .ToArray();

        if (!CanCarry(items))
            return false;

        foreach (var item in items)
            Inventory.TryAddToNextSlot(item);

        Client.SendStats(StatusFlags.All);

        return true;
    }
    public bool GiveItem(string name, byte? slot = null)
    {
        var item = Item.Create(this, name);
        item.Stacks = 1;
        return GiveItem(item, slot);
    }
    public bool GiveItem(Item obj, byte? slot = null)
    {
        if ((obj == null)
            || (!obj.Template.CanStack && (obj.Stacks > 1))
            || (obj.Template.CanStack && (obj.Template.MaxStack < obj.Stacks)))
            return false;

        if (obj.Stacks == 0)
            obj.Stacks = 1;

        if (!CanCarry(obj))
        {
            if (obj.Template.CanStack && Inventory.HasCount($"{obj.Template.Name}", 1))
            {
                Client.SystemMessage($"You can't carry more than {obj.Template.MaxStack} of those.");
                return false;
            }
            if (obj.Template.CarryWeight + CurrentWeight > MaximumWeight)
            {
                Client.SystemMessage("You can't carry anything else.");
                return false;
            }
        }

        if (slot.HasValue)
            return Inventory.TryAdd(obj, slot.Value);

        return Inventory.TryAddToNextSlot(obj);
    }
    public void Hide()
    {
        Invisible = true;

        //if IN PARTY
        //dont hide
        //if SEE INVIS
        //dont hide

        //if we cant see invis AND they arent in the party
        //
        foreach (var nearby in AislingsNearby())
            if (!nearby.ShouldSee(this))
                HideFrom(nearby);
            else
                ShowTo(nearby);
    }
    public void RemoveHideBuffs()
    {
        RemoveBuff("hide");
        RemoveBuff("veil");

        //stealth
        if (Invisible)
            Invisible = false;

        Client.UpdateDisplay();
    }
    public override string ToString() => Username;
    public void RecalculateCurrentWeight()
    {
        var inventoryWeight = Inventory.Snapshot()
            .Select(item => (int)item.Template.CarryWeight)
            .Sum();

        var equippedWeight = EquipmentManager.Equipment.Values
            .Where(gear => gear != null)
            .Select(gear => (int)gear.Item.Template.CarryWeight)
            .Sum();

        CurrentWeight = inventoryWeight + equippedWeight;
    }
    public Aisling TrainSpell(Spell lpSpell)
    {
        Client.TrainSpell(lpSpell);
        return this;
    }
    public void UpdateStats() => Client?.SendStats(StatusFlags.All);
    public void RaiseStat(Stat stat, int amount = 1)
    {
        switch (stat)
        {
            case Stat.Str:
                _Str += amount;
                break;
            case Stat.Int:
                _Int += amount;
                break;
            case Stat.Wis:
                _Wis += amount;
                break;
            case Stat.Con:
                _Con += amount;
                break;
            case Stat.Dex:
                _Dex += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }
    public int GetStat(Stat stat) =>
        stat switch
        {
            Stat.Str => _Str,
            Stat.Int => _Int,
            Stat.Wis => _Wis,
            Stat.Con => _Con,
            Stat.Dex => _Dex,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null)
        };
    public void WarpToHell()
    {
        Direction = 0;
        Client.TransitionToMap(ServerSetup.Instance.Config.DeathMap,
            new Position(ServerSetup.Instance.Config.DeathMapX, ServerSetup.Instance.Config.DeathMapY));
        UpdateStats();
    }
    public void EnterAbyss()
    {
        Abyss = true;
        Client.LeaveArea(true, true);
    }
    #region Exchange Methods
    public void CancelExchange()
    {
        if (Exchange == null || Exchange.Trader == null)
            return;

        var trader = Exchange.Trader;

        var exchangeA = Exchange;
        var exchangeB = trader.Exchange;

        var itemsA = exchangeA.Items.ToArray();
        var itemsB = exchangeB.Items.ToArray();

        var goldA = exchangeA.Gold;
        var goldB = exchangeB.Gold;

        Exchange = null;
        trader.Exchange = null;

        foreach (var item in itemsB)
            if (trader.GiveItem(item))
            {
            }

        foreach (var item in itemsA)
            if (GiveItem(item))
            {
            }

        GoldPoints += goldA;
        trader.GoldPoints += goldB;

        if (trader.GoldPoints > ServerSetup.Instance.Config.MaxCarryGold)
            trader.GoldPoints = ServerSetup.Instance.Config.MaxCarryGold;

        if (GoldPoints > ServerSetup.Instance.Config.MaxCarryGold)
            GoldPoints = ServerSetup.Instance.Config.MaxCarryGold;
        trader.Client.Save();
        trader.Client.SendStats(StatusFlags.ExpGold);

        Client.Save();
        Client.SendStats(StatusFlags.ExpGold);

        var packet = new NetworkPacketWriter();
        packet.Write((byte)0x42);
        packet.Write((byte)0x00);

        packet.Write((byte)0x04);
        packet.Write((byte)0x00);
        packet.WriteStringA("Trade cancelled.");
        Client.Send(packet);

        packet = new NetworkPacketWriter();
        packet.Write((byte)0x42);
        packet.Write((byte)0x00);

        packet.Write((byte)0x04);
        packet.Write((byte)0x01);
        packet.WriteStringA("Trade cancelled.");
        trader.Client.Send(packet);
    }
    public void FinishExchange()
    {
        var trader = Exchange.Trader;

        var exchangeA = Exchange;
        var exchangeB = trader.Exchange;

        var itemsA = exchangeA.Items.ToArray();
        var itemsB = exchangeB.Items.ToArray();

        var goldA = exchangeA.Gold;
        var goldB = exchangeB.Gold;


        Exchange = null;
        trader.Exchange = null;

        foreach (var item in itemsB)
            if (GiveItem(item))
            {
            }

        foreach (var item in itemsA)
            if (trader.GiveItem(item))
            {
            }

        GoldPoints += goldB;
        trader.GoldPoints += goldA;

        if (trader.GoldPoints > ServerSetup.Instance.Config.MaxCarryGold)
            trader.GoldPoints = ServerSetup.Instance.Config.MaxCarryGold;

        if (GoldPoints > ServerSetup.Instance.Config.MaxCarryGold)
            GoldPoints = ServerSetup.Instance.Config.MaxCarryGold;

        exchangeA.Items.Clear();
        exchangeB.Items.Clear();

        trader.Client?.SendStats(StatusFlags.All);
        trader.Client?.Save();
        Client?.SendStats(StatusFlags.All);
        Client?.Save();
    }
    //public void AddExchangeItem (Aisling aisling, byte slot)
    //{
    //    var trader = Exchange.Trader;
    //    var exchangeA = Exchange;
    //    var exchangeB = trader.Exchange;
    //    var tradedItem = Inventory.TryGetObject(slot, out var item);
    //    if (!item.Template.Flags.HasFlag(ItemFlags.Tradeable))
    //    {
    //        aisling.Client.SystemMessage("You can't exchange this item.");
    //        return;
    //    }
    //    if (!trader.CanCarry(item))
    //    {
    //        Client.SystemMessage($"{trader.Username} can't carry any more.");
    //        trader.Client.SystemMessage("You can't carry anything else.");
    //    }
    //    if (item.Template.CanStack)
    //        Client.SendExchangeRequestAmount(item.Slot);
    //    else
    //    {
    //        var packet = new NetworkPacketWriter();
    //        packet.Write((byte)0x42);
    //        packet.Write((byte)0x00);

    //        packet.Write((byte)0x02);
    //        packet.Write((byte)0x01);
    //        packet.Write((byte)Exchange.Items.Count);
    //        packet.Write(item.DisplayImage);
    //        packet.Write(item.Color);
    //        packet.WriteStringA(item.DisplayName);
    //        trader.Client.Send(packet);
    //    }
    //}
    #endregion Exchange Methods
    //Pill Fix - Bools/Methods
    #region Pill's bool/methods
    #region Stage
    public string Rank()
    {
        var client = Client.Aisling;
        switch (client.Stage)
        {
            case ClassStage.Class:
                {
                    if (client.GameMaster)
                        return $"Aosda";
                }
                break;
            case ClassStage.Pure_Master:
                {
                    return $"Master {client.Path}";
                }

            case ClassStage.Subpathed_Master:
                {
                    return $"Master {client.Path}";
                }

            case ClassStage.Pure_Grand_Master:
                {
                    return $"Grand Master {client.Path}";
                }
            case ClassStage.Subpathed_Grand_Master:
                {
                    return $"Grand Master {client.Path}";
                }
        }
        return $"{client.Path}";
    }
    #endregion Stage
    #region Clans
    public string ClanRankTitle()
    {
        try
        {
            foreach (var guild in ServerSetup.Instance.GlobalClanTemplateCache)
            {
                if (guild.Value.Primogen.ToLower() != null)
                    if (guild.Value.Primogen.ToLower() == Username.ToLower())
                        return $"Primogen";

                if (guild.Value.Primarch.ToLower() != null)
                    if (guild.Value.Primarch.ToLower() == Username.ToLower())
                        return $"Primarch";

                foreach (var council in guild.Value.Council)
                    if (council.ToLower() == Username.ToLower())
                        return $"Council";
            }
        }
        catch
        {
            return "";
        }
        return "";
    }

    public string ClanName()
    {
        foreach (var guild in ServerSetup.Instance.GlobalClanTemplateCache)
        {
            foreach (var aisling in guild.Value.Members)
                if (aisling.ToLower() == Username.ToLower())
                    return $"{guild.Value.GuildName}";
        }

        return string.Empty;
    }
    #endregion Clans
    #endregion
}