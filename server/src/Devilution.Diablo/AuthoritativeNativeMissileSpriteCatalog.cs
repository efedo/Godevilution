using Devilution.Data;

namespace Devilution.Diablo;

/** Native missile sprite dimensions and animation timing supplied by content. */
public sealed record AuthoritativeNativeMissileSpriteDefinition(
    string MissileId,
    int Width,
    int AlternateWidth,
    string Name,
    int FrameCount,
    string Flags,
    IReadOnlyList<int> FrameDelay,
    IReadOnlyList<int> FrameLength);

/** Validated native missile_sprites.tsv metadata for presentation clients. */
public sealed class AuthoritativeNativeMissileSpriteCatalog
{
    public const int NativeDirectionCount = 16;
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "missile_id",
        "width",
        "alternate_width",
        "name",
        "frame_count",
        "flags",
        "frame_delay",
        "frame_length",
    ];
    private static readonly IReadOnlySet<string> RequiredNativeSpriteIds = new HashSet<string>(
        [
            "Acid",
            "AcidPuddle",
            "AcidSplat",
            "ApocalypseBoom",
            "Arrow",
            "BigExplosion",
            "BloodHit",
            "BloodStar",
            "BloodStarBlue",
            "BloodStarBlueExplosion",
            "BloodStarExplosion",
            "BloodStarRed",
            "BloodStarRedExplosion",
            "BloodStarYellow",
            "BloodStarYellowExplosion",
            "BoneHit",
            "BoneSpirit",
            "ChargedBolt",
            "DiabloApocalypseBoom",
            "DoomSerpents",
            "Elemental",
            "Etherealize",
            "FireArrow",
            "FireArrowExplosion",
            "Fireball",
            "FireWall",
            "FlashBottom",
            "FlashTop",
            "Golem",
            "Guardian",
            "HolyBolt",
            "HolyBoltExplosion",
            "Inferno",
            "Krull",
            "Lightning",
            "LightningArrow",
            "MagmaBall",
            "MagmaBallExplosion",
            "ManaShield",
            "MetalHit",
            "RedPortal",
            "Resurrect",
            "Spurt",
            "StoneCurseShatter",
            "ThinLightning",
            "TownPortal",
        ],
        StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> KnownFlags = new HashSet<string>(
        ["MonsterOwned", "NotAnimated"],
        StringComparer.Ordinal);

    private readonly IReadOnlyList<AuthoritativeNativeMissileSpriteDefinition> definitions;
    private readonly IReadOnlyDictionary<string, AuthoritativeNativeMissileSpriteDefinition> byId;

    public AuthoritativeNativeMissileSpriteCatalog(
        IEnumerable<AuthoritativeNativeMissileSpriteDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var ordered = definitions.ToArray();
        if (ordered.Length == 0)
            throw new InvalidDataException("The native missile sprite catalog cannot be empty.");

        var index = new Dictionary<string, AuthoritativeNativeMissileSpriteDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in ordered) {
            if (!IsIdentifier(definition.MissileId)
                || definition.Width < 0
                || definition.FrameCount is < 1 or > NativeDirectionCount
                || definition.FrameDelay.Count != NativeDirectionCount
                || definition.FrameLength.Count != NativeDirectionCount
                || definition.FrameDelay.Any(value => value < 0)
                || definition.FrameLength.Any(value => value < 0)
                || !index.TryAdd(definition.MissileId, definition)) {
                throw new InvalidDataException(
                    $"Native missile sprite definition '{definition.MissileId}' is invalid or duplicated.");
            }
            var flags = ParseFlags(definition.Flags);
            if (flags.Any(flag => !KnownFlags.Contains(flag)))
                throw new InvalidDataException(
                    $"Native missile sprite '{definition.MissileId}' contains an unknown flag.");
        }

        this.definitions = Array.AsReadOnly(ordered);
        byId = index;
    }

    public IReadOnlyList<AuthoritativeNativeMissileSpriteDefinition> Definitions => definitions;

    /** Rejects partial native missile sprite metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-missile sprite table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("missile_id");
            _ = row.Required("width");
            _ = row.Required("alternate_width");
            _ = row.Required("frame_count");
            _ = row["flags"];
            _ = row.Required("frame_delay");
            _ = row.Required("frame_length");
        }
    }

    /**
     * Requires every base native `missile_sprites.tsv` identity. Variant packs
     * may add presentation identities through ordered overlays.
     */
    public void ValidateRequiredNativeNamespace()
    {
        var actual = definitions
            .Select(definition => definition.MissileId)
            .ToHashSet(StringComparer.Ordinal);
        var missing = RequiredNativeSpriteIds
            .Except(actual, StringComparer.Ordinal)
            .OrderBy(missileId => missileId, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length == 0)
            return;

        throw new InvalidDataException(
            "The active native missile sprite namespace is missing required base "
            + $"missile_sprites.tsv identities: {string.Join(", ", missing)}.");
    }

    public bool TryGet(
        string missileId,
        out AuthoritativeNativeMissileSpriteDefinition definition)
        => byId.TryGetValue(missileId, out definition!);

    public AuthoritativeNativeMissileSpriteDefinition GetRequired(string missileId)
    {
        if (!byId.TryGetValue(missileId, out var definition))
            throw new KeyNotFoundException(
                $"Native missile sprite '{missileId}' is not present in the active catalog.");
        return definition;
    }

    public static AuthoritativeNativeMissileSpriteCatalog LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeMissileSpriteCatalog(table.Rows.Select(row =>
            new AuthoritativeNativeMissileSpriteDefinition(
                row.Required("missile_id"),
                row.RequiredInt32("width"),
                row.OptionalInt32("alternate_width"),
                OptionalText(row, "name"),
                row.RequiredInt32("frame_count"),
                OptionalText(row, "flags"),
                ParseDirectionValues(row, "frame_delay"),
                ParseDirectionValues(row, "frame_length"))));
    }

    private static IReadOnlyList<int> ParseDirectionValues(TsvRow row, string column)
    {
        if (!row.TryGet(column, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Native missile sprite row {row.LineNumber} requires '{column}'.");
        var entries = value.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new int[entries.Length];
        for (var index = 0; index < entries.Length; index++) {
            if (!int.TryParse(entries[index], out values[index]))
                throw new InvalidDataException(
                    $"Native missile sprite row {row.LineNumber} contains an invalid '{column}' value.");
        }
        return values;
    }

    private static string OptionalText(TsvRow row, string column)
        => row.TryGet(column, out var value) ? value : string.Empty;

    private static IReadOnlyList<string> ParseFlags(string flags)
        => string.IsNullOrWhiteSpace(flags)
            ? []
            : flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsIdentifier(string value)
        => !string.IsNullOrEmpty(value)
            && char.IsLetter(value[0])
            && value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}
