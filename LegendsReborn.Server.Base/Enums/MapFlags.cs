namespace Darkages.Enums;

[Flags]
public enum MapFlags : uint
{
    Snow = 1,
    Rain = 2,
    NoMap = 64,
    Winter = 128,
    CanSummon = 256,
    CanLocate = 512,
    CanTeleport = 1024,
    CanUseSkill = 2048,
    CanUseSpell = 4096,
    ArenaTeam = 8192,
    PlayerKill = 16384,
    SendToHell = 32768,
    ShouldComa = 65536,
    HasDayNight = 131072,
    Event = 262144,
    Jail = 524288,
    T1 = 1048576,
    T2 = 2097152,
    T3 = 4194304,
    T4 = 8388608,
    T5 = 16777216,
           
    Darkness = Snow | Rain,
    Default = CanSummon | CanLocate | CanTeleport | CanUseSkill | CanUseSpell | SendToHell | ShouldComa,
    NoMagicTown = CanSummon | CanLocate | CanTeleport | CanUseSkill | ShouldComa | SendToHell,
    Dungeon = CanUseSkill | CanUseSpell | SendToHell | ShouldComa,
    Hosted = PlayerKill | ArenaTeam | CanUseSkill | CanUseSpell,
    QuestDarkness = Darkness | Dungeon,
    Gathering = ShouldComa | SendToHell,
    Lobby = ArenaTeam,
    Minigame = Event | ArenaTeam,
    T1Chest = T1 | Dungeon,
    T2Chest = T2 | Dungeon,
    T3Chest = T3 | Dungeon,
    T4Chest = T4 | Dungeon,
    T5Chest = T5 | Dungeon,
    Raid = CanUseSkill | CanUseSpell | ShouldComa | NoMap,
    Gnoseos = NoMap |CanUseSpell | CanUseSkill,
    Syndicate = NoMap | CanUseSpell | CanUseSkill,
}

public static class MapExtensions
{
    public static bool MapFlagIsSet(this MapFlags self, MapFlags flag) => (self & flag) == flag;
}