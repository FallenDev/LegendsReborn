namespace Darkages.Enums;

[Flags]
public enum PostQualifier
{
    BreakInvisible = 0,
    IgnoreDefense = 1,
    None = 3
}

public static class PostQualifierExtensions
{
    public static bool QualifierFlagIsSet(this PostQualifier self, PostQualifier flag) => (self & flag) == flag;
}