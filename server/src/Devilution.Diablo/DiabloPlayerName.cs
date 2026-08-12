namespace Devilution.Diablo;

/** Native-compatible character-name validation and PvP-ear identity encoding. */
public static class DiabloPlayerName
{
    public const int MaximumLength = 31;

    private const string ForbiddenCharacters = ",<>%&\\\"?*#/: ";

    private static readonly string[] EncodedForbiddenNames = [
        "gvdl",
        "dvou",
        "tiju",
        "cjudi",
        "bttipmf",
        "ojhhfs",
        "cmj{{bse",
        "benjo",
    ];

    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaximumLength)
            return false;
        if (name.Any(character => character is < (char)0x21 or > (char)0x7e
                || ForbiddenCharacters.Contains(character))) {
            return false;
        }

        var shifted = string.Create(name.Length, name, static (characters, source) => {
            for (var index = 0; index < source.Length; index++)
                characters[index] = checked((char)(source[index] + 1));
        });
        return !EncodedForbiddenNames.Any(forbidden =>
            shifted.Contains(forbidden, StringComparison.Ordinal));
    }

    public static string RequireValid(string? name, string parameterName)
    {
        if (!IsValid(name)) {
            throw new InvalidDataException(
                $"Character name '{name}' is outside the native multiplayer name rules ({parameterName}).");
        }
        return name!;
    }

    public static (uint CreateInfo, uint Seed) EncodeEarIdentity(string name)
    {
        RequireValid(name, nameof(name));
        Span<byte> bytes = stackalloc byte[6];
        for (var index = 0; index < Math.Min(name.Length, bytes.Length); index++)
            bytes[index] = checked((byte)name[index]);
        return (
            checked((uint)(bytes[0] << 8 | bytes[1])),
            (uint)bytes[2] << 24
                | (uint)bytes[3] << 16
                | (uint)bytes[4] << 8
                | bytes[5]);
    }
}
