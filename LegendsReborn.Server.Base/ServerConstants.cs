using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Models;
using Darkages.Types;

using Newtonsoft.Json;

namespace Darkages;

public class ServerConstants : IServerConstants
{
    public bool AssailsCancelSpells { get; set; }

    public string BadRequestMessage { get; set; }

    public byte BaseMR { get; set; } = 70;

    public byte BaseStatAttribute { get; set; }

    public double BehindDamageMod { get; set; }

    public double RogueBehindDamageMod { get; set; }

    public bool CancelCastingWhenWalking { get; set; }

    public bool CancelWalkingIfRefreshing { get; set; }

    public bool CanMoveDuringReap { get; set; }

    public string CantAttack { get; set; }

    public string CantCarryMoreMsg { get; set; }

    public string CantDoThat { get; set; }

    public string CantDropItemMsg { get; set; }

    public string CantEquipThatMessage { get; set; }

    public string CantUseThat { get; set; }

    public string CantWearYetMessage { get; set; }

    public string ChantPrefix { get; set; }

    public string ChantSuffix { get; set; }

    public int ClickLootDistance { get; set; }

    public int ClientVersion { get; set; }

    public string ConAddedMessage { get; set; }

    public int ConnectionCapacity { get; set; }

    public string CursedItemMessage { get; set; }

    public int DeathHPPenalty { get; set; }

    public int DeathMap { get; set; }
    public int DeathMapX { get; set; }
    public int DeathMapY { get; set; }
    public string DeathReepingMessage { get; set; }
    public bool DebugMode { get; set; }
    public ItemColor DefaultItemColor { get; set; }
    public int DefaultItemDurability { get; set; }
    public uint DefaultItemValue { get; set; }
    public bool DevMode { get; set; }
    public string[] DevModeExemptions { get; set; }
    public string DexAddedMessage { get; set; }
    public string DoesNotFitMessage { get; set; }
    public bool DontSavePlayers { get; set; }
    public double ExperienceMultiplier { get; set; }
    public double FasNadurStrength { get; set; }
    public List<string> GameMasters { get; set; }
    public bool GiveAssailOnCreate { get; set; }
    public double GlobalBaseSkillDelay { get; set; }
    public double GlobalBaseAssailDelay { get; set; }
    public double GlobalBaseItemDelay { get; set; }
    public double GlobalSpawnTimer { get; set; }
    public double GroupExpBonus { get; set; }
    public string GroupRequestDeclinedMsg { get; set; }
    public string HandShakeMessage { get; set; }
    public int HelperMenuId { get; set; }
    public string HelperMenuTemplateKey { get; set; }
    public int HpGainFactor { get; set; }
    public string IntAddedMessage { get; set; }
    public string ItemNotRequiredMsg { get; set; }
    public string LevelUpMessage { get; set; }
    public bool LogClientPackets { get; set; }
    public int LOGIN_PORT { get; set; }
    public int LOBBY_PORT { get; set; }
    public ReservedRedirectInfo[] ReservedRedirects { get; set; } = [];
    public bool LogServerPackets { get; set; }
    public int LootTableStackSize { get; set; }
    public uint MaxCarryGold { get; set; }
    public int MaxActionsPerSecond { get; set; }
    public int MaxHP { get; set; }
    public string MerchantBuy { get; set; }
    public string MerchantBuyMessage { get; set; }
    public string MerchantCancelMessage { get; set; }
    public string MerchantConfirmMessage { get; set; }
    public string MerchantRefuseTradeMessage { get; set; }
    public string MerchantSell { get; set; }
    public string MerchantStackErrorMessage { get; set; }
    public string MerchantTradeCompletedMessage { get; set; }
    public string MerchantTradeErrorMessage { get; set; }
    public string MerchantWarningMessage { get; set; }
    public double MessageClearInterval { get; set; }
    public int MinimumHp { get; set; }
    public int MonsterSpellSuccessRate { get; set; }
    public double MorFasNadurStrength { get; set; }
    public int MpGainFactor { get; set; }
    public bool MultiUserLogin { get; set; }
    public double MundaneRespawnInterval { get; set; }
    public double NationReturnHours { get; set; }
    public string NoManaMessage { get; set; }
    public string NotEnoughGoldToDropMsg { get; set; }
    public double PingInterval { get; set; }
    public int PlayerLevelCap { get; set; }
    public int PVPMap { get; set; }
    public double PvpDamageMod { get; set; }
    public string ReapMessage { get; set; }
    public string ReapMessageDuringAction { get; set; }
    public int RefreshRate { get; set; }
    public int RegenRate { get; set; }
    public string RepairItemMessage { get; set; }
    public double SaveRate { get; set; }
    public int SERVER_PORT { get; set; }
    public string SERVER_TITLE { get; set; }
    public string ServerWelcomeMessage { get; set; }
    public int SkullLength { get; set; }
    public string SomethingWentWrong { get; set; }
    public string SpellFailedMessage { get; set; }
    public int StartingMap { get; set; }
    [JsonProperty] public Position StartingPosition { get; set; }
    public byte StatCap { get; set; }
    public int StatsPerLevel { get; set; }
    public string StrAddedMessage { get; set; }
    public string TooWeakToLift { get; set; }
    public short TransitionPointX { get; set; }
    public short TransitionPointY { get; set; }
    public int TransitionZone { get; set; }
    public bool UseLobby { get; set; }
    public bool UseLoruleItemRarity { get; set; }
    public bool UseLoruleVariants { get; set; }
    public string UserDroppedGoldMsg { get; set; }
    public int VeryNearByProximity { get; set; }
    public int WarpCheckRate { get; set; }
    public double WeightIncreasemodifier { get; set; }
    public string WisAddedMessage { get; set; }
    public int WithinRangeProximity { get; set; }
    public int WithinViewProximity { get; set; }
    public int WithinCastProximity { get; set; }
    public string WrongClassMessage { get; set; }
    public string YouDroppedGoldMsg { get; set; }

    /// <summary>
    /// Enable to refresh Monsters when player F5s
    /// </summary>
    public bool F5ReloadsMonsters { get; set; }

    /// <summary>
    /// Enable to refresh Players when player F5s
    /// </summary>
    public bool F5ReloadsPlayers { get; set; }

    //Set to true/false if you want sleep debuff to cause double damage.
    public bool SleepProcsDoubleDmg { get; set; }

    //How much damage does Naomh Aite reduce? default is 3. (-30%)
    public int AiteDamageReductionMod { get; set; }

    //Base Monster Damage Mod, For Base Attacks, Assail ect. Default is 60.
    public int BaseDamageMod { get; set; }

    //Script to use for AC Formula, default is "AC Formula"
    public string ACFormulaScript { get; set; }

    //Script to use for Elemental Table, default is "Elements 1.0"
    public string ElementTableScript { get; set; }

    //Script to use for all Monster Exp Rewards, Experience ect. default is "Monster Exp 1x"
    public string MonsterRewardScript { get; set; }

    //Script to use for all Base damage monster calculations. Default is "Base Damage"
    public string BaseDamageScript { get; set; }

    //Script to use for all Monster Creations. Default is "Create Monster"
    public string MonsterCreationScript { get; set; }
        
    public int MaxSpellsPerSecond { get; set; }
        
    public int MaxChantTimeBurdenMs { get; set; }
        
    public bool ProhibitF5Walk { get; set; }
        
    public bool ProhibitItemSwitchWalk { get; set; }
        
    public bool ProhibitSpeedWalk { get; set; }

    public int ArenaEventCheckFrequency { get; set; }
    public int ArenaEventDelayOnStartupTime { get; set; }
    public int ArenaEventTriggerHour { get; set; }
    public int ArenaEventSignupTime { get; set; }
    public bool ArenaEventStartAtPM { get; set; }
    public int ArenaEventTeamFormationTime { get; set; }
    public int ArenaTimeToWalkToStartingZone { get; set; }
    public int ArenaTimerForRoundToReset { get; set; }
    public int ArenaRoundsNeededToWinElixirLeague { get; set; }
    public int ArenaDelayToAssignFinalWinner { get; set; }
}