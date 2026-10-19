using System.Globalization;

namespace Devilution.Client;

internal readonly record struct CharacterClassChoice(uint Id, string Name);

/** Resolves launcher-known class limits without changing server admission. */
internal static class ClientCharacterSetupPolicy
{
    public const string SupportedClassIdsEnvironmentVariable
        = "GODEVILUTION_SUPPORTED_CHARACTER_CLASS_IDS";

    private static readonly CharacterClassChoice[] ClassChoices = [
        new(0, "Warrior"),
        new(1, "Rogue"),
        new(2, "Sorcerer"),
        new(3, "Monk"),
        new(4, "Bard"),
        new(5, "Barbarian"),
    ];

    private static readonly IReadOnlyList<CharacterClassChoice> ReadOnlyClassChoices
        = Array.AsReadOnly(ClassChoices);

    public static IReadOnlyList<CharacterClassChoice> AllClasses => ReadOnlyClassChoices;

    public static IReadOnlyList<CharacterClassChoice> GetAvailableClasses(
        string? selectedHost,
        int selectedPort,
        string? configuredHost,
        int configuredPort,
        string? supportedClassIds,
        bool resumeSession = false)
    {
        if (resumeSession || !IsConfiguredEndpoint(
                selectedHost, selectedPort, configuredHost, configuredPort))
            return ReadOnlyClassChoices;

        if (string.IsNullOrEmpty(supportedClassIds))
            return ReadOnlyClassChoices;

        var allowedIds = ParseSupportedClassIds(supportedClassIds);
        return Array.AsReadOnly(ClassChoices
            .Where(choice => allowedIds.Contains(choice.Id))
            .ToArray());
    }

    public static uint SelectClassId(
        IReadOnlyList<CharacterClassChoice> choices,
        uint? requestedClassId)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0)
            throw new InvalidDataException("The selected setup profile has no supported character classes.");

        if (requestedClassId is { } requested
            && choices.Any(choice => choice.Id == requested))
            return requested;

        return choices[0].Id;
    }

    private static bool IsConfiguredEndpoint(
        string? selectedHost,
        int selectedPort,
        string? configuredHost,
        int configuredPort)
        => !string.IsNullOrWhiteSpace(selectedHost)
            && !string.IsNullOrWhiteSpace(configuredHost)
            && selectedPort == configuredPort
            && string.Equals(
                selectedHost.Trim(),
                configuredHost.Trim(),
                StringComparison.OrdinalIgnoreCase);

    private static HashSet<uint> ParseSupportedClassIds(string value)
    {
        var allowedIds = new HashSet<uint>();
        foreach (var rawToken in value.Split(',', StringSplitOptions.None)) {
            var token = rawToken.Trim();
            if (token.Length == 0
                || token.Any(character => character is < '0' or > '9')
                || !uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                throw InvalidSupportedClassIds();

            if (!ClassChoices.Any(choice => choice.Id == id))
                throw InvalidSupportedClassIds();
            if (!allowedIds.Add(id))
                throw InvalidSupportedClassIds();
        }

        if (allowedIds.Count == 0)
            throw InvalidSupportedClassIds();

        return allowedIds;
    }

    private static InvalidDataException InvalidSupportedClassIds()
        => new($"{SupportedClassIdsEnvironmentVariable} must contain a nonempty, comma-separated list of unique supported class IDs (0 through 5).");
}
