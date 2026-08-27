namespace Devilution.Protocol;

/** Shared wire-level shape rules for optional authoritative player names. */
public static class AuthoritativePlayerNameRules
{
    public const int MaximumLength = 31;

    /** Returns whether a name is empty or printable ASCII within the wire limit. */
    public static bool IsValidShape(string? name)
    {
        return name is not null
            && name.Length <= MaximumLength
            && name.All(character => character is >= (char)0x21 and <= (char)0x7e);
    }
}
