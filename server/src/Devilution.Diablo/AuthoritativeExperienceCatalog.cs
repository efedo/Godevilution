using Devilution.Data;

namespace Devilution.Diablo;

/** Native character-level experience threshold supplied by validated content. */
public sealed record AuthoritativeExperienceLevel(
    uint Level,
    uint RequiredExperience);

/** Validated cumulative experience thresholds for server-owned level progression. */
public sealed class AuthoritativeExperienceCatalog
{
    public const uint MaximumSupportedLevel = 50;

    private readonly IReadOnlyList<AuthoritativeExperienceLevel> definitions;

    public AuthoritativeExperienceCatalog(
        IEnumerable<AuthoritativeExperienceLevel> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.OrderBy(definition => definition.Level).ToArray();
        if (ordered.Length == 0)
            throw new InvalidDataException("The experience catalog cannot be empty.");
        for (var index = 0; index < ordered.Length; index++) {
            var definition = ordered[index];
            if (definition.Level != (uint)index + 1
                || definition.Level > MaximumSupportedLevel
                || definition.RequiredExperience == 0
                || (index > 0
                    && definition.RequiredExperience <= ordered[index - 1].RequiredExperience)) {
                throw new InvalidDataException(
                    "Experience definitions must contain contiguous levels with strictly increasing thresholds.");
            }
        }
        this.definitions = ordered;
    }

    public IReadOnlyList<AuthoritativeExperienceLevel> Definitions => definitions;

    public uint MaximumLevel => definitions[^1].Level;

    public uint GetRequiredExperience(uint level)
    {
        if (level == 0 || level > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return definitions[checked((int)level - 1)].RequiredExperience;
    }

    public bool TryGetRequiredExperience(
        uint level,
        out uint requiredExperience)
    {
        if (level == 0 || level > MaximumLevel) {
            requiredExperience = 0;
            return false;
        }
        requiredExperience = definitions[checked((int)level - 1)].RequiredExperience;
        return true;
    }

    public static AuthoritativeExperienceCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeExperienceCatalog(table.Rows.Select(row =>
            new AuthoritativeExperienceLevel(
                row.RequiredUInt32("level"),
                row.RequiredUInt32("experience"))));
    }
}
