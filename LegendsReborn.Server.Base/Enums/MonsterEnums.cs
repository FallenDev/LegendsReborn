namespace Darkages.Enums;

public enum MonsterEnums
{
    Pure,
    Elemental,
    Physical
}

[Flags]
public enum LootQualifer
{
    Random = 1 << 1,
    Table = 1 << 2,
    Event = 1 << 3,
    Gold = 1 << 5,
    None = 256,

    Quest_Boss = Random | Gold,
    Monster = Random,
    Beastman = Random | Gold
}

[Flags]
public enum MoodQualifer
{
    Idle = 1,
    Aggressive = 2,
    Unpredicable = 4,
    Neutral = 8,
    VeryAggressive = 16
}