namespace Darkages.Enums;

[Flags]
public enum Class
{
    Peasant = 1,
    Warrior = 1 << 1,
    Rogue = 1 << 2,
    Wizard = 1 << 3,
    Priest = 1 << 4,
    Monk = 1 << 5,
    Diacht = 1 << 6,
    Monster = 1 << 8,
    Quest = 1 << 9
}

[Flags]
public enum ClassStage
{
    Class = 0,
    Subpathed_Master = 1,
    Subpathed_Grand_Master = 2,
    Pure_Master = 3,
    Pure_Grand_Master = 4,
}

public static class SpriteClassRaceExtensions
{
    public static bool ClassFlagIsSet(this Class self, Class flag) => (self & flag) == flag;
    public static bool StageFlagIsSet(this ClassStage self, ClassStage flag) => (self & flag) == flag;
}

public static class ClassStrings
{
    public static int ClassDisplayInt(string c)
    {
        return c switch
        {
            "Peasant" => 0,
            "Warrior" => 1,
            "Rogue" => 2,
            "Wizard" => 3,
            "Priest" => 4,
            "Monk" => 5,
            "Diacht" => 6,
            _ => 0
        };
    }

    public static int ItemClassToIntMetaData(string c)
    {
        return c switch
        {
            "Peasant" => 0,
            "Warrior" => 1,
            "Rogue" => 2,
            "Wizard" => 3,
            "Priest" => 4,
            "Monk" => 5,
            "Diacht" => 6,
            _ => 0
        };
    }

    public static int JobDisplayFlag(string c)
    {
        return c switch
        {
            "None" => 0,
            "Gladiator" => 1,
            "Druid" => 2,
            "Archer" => 4,
            "Bard" => 8,
            "Summoner" => 16,
            _ => 0
        };
    }
}