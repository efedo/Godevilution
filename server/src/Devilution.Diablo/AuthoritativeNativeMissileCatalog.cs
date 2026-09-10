using Devilution.Data;

namespace Devilution.Diablo;

/** Native missile identity and presentation metadata supplied by validated content. */
public sealed record AuthoritativeNativeMissileDefinition(
    string MissileId,
    string AddFunction,
    string ProcessFunction,
    string CastSound,
    string HitSound,
    string Graphic,
    string Flags,
    string MovementDistribution,
    string? OnExpiryMissile)
{
    /** Compatibility identity used by native monster presentation rather than misdat.tsv. */
    public bool CompatibilityAlias { get; init; }
}

/** Validated native misdat.tsv identities used by server and presentation bridges. */
public sealed class AuthoritativeNativeMissileCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "missile_id",
        "add_function",
        "process_function",
        "cast_sound",
        "hit_sound",
        "graphic",
        "flags",
        "movement_distribution",
        "on_expiry_missile",
    ];

    private static readonly IReadOnlySet<string> CompleteNativeMissileIds = new HashSet<string>(
        [
            "Acid",
            "AcidPuddle",
            "AcidSplat",
            "Apocalypse",
            "ApocalypseBoom",
            "Arrow",
            "BloodHit",
            "BloodRitual",
            "BloodStar",
            "BloodStarExplosion",
            "BoneHit",
            "BoneSpirit",
            "ChainBall",
            "ChainLightning",
            "ChargedBolt",
            "DiabloApocalypse",
            "DiabloApocalypseBoom",
            "DoomSerpents",
            "Elemental",
            "Etherealize",
            "FireArrow",
            "Fireball",
            "Firebolt",
            "FireMan",
            "FireOnly",
            "FireWall",
            "FireWallControl",
            "FlameWave",
            "FlameWaveControl",
            "FlashBottom",
            "FlashTop",
            "Golem",
            "Guardian",
            "Healing",
            "HealOther",
            "HolyBolt",
            "Identify",
            "Inferno",
            "InfernoControl",
            "Infravision",
            "Invisibility",
            "ItemRepair",
            "Krull",
            "Lightning",
            "LightningArrow",
            "LightningControl",
            "MagmaBall",
            "MagmaBallExplosion",
            "ManaShield",
            "MetalHit",
            "Nova",
            "NovaBall",
            "Phasing",
            "Rage",
            "RedPortal",
            "Resurrect",
            "ResurrectBeam",
            "Rhino",
            "Spurt",
            "StaffRecharge",
            "StoneCurse",
            "Telekinesis",
            "Teleport",
            "ThinLightning",
            "ThinLightningControl",
            "TownPortal",
            "TrapDisarm",
            "WeaponExplosion",
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> RequiredCompatibilityAliasIds = new HashSet<string>(
        ["OrangeFlare", "YellowFlare", "BlueFlare", "RedFlare", "BlueFlare2"],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> KnownFlags = new HashSet<string>(
        ["Acid", "Arrow", "Fire", "Invisible", "Lightning", "Magic", "Physical", "Unblockable"],
        StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> KnownMovementDistributions = new HashSet<string>(
        ["", "Blockable", "Unblockable"],
        StringComparer.Ordinal);

    private readonly IReadOnlyList<AuthoritativeNativeMissileDefinition> definitions;
    private readonly IReadOnlyDictionary<string, AuthoritativeNativeMissileDefinition> byId;

    public AuthoritativeNativeMissileCatalog(
        IEnumerable<AuthoritativeNativeMissileDefinition> definitions,
        IEnumerable<string>? compatibilityAliases = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var sourceDefinitions = definitions.ToArray();
        var aliases = (compatibilityAliases ?? [])
            .Select(alias => new AuthoritativeNativeMissileDefinition(
                alias,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null) {
                CompatibilityAlias = true,
            })
            .ToArray();
        var ordered = sourceDefinitions.Concat(aliases).ToArray();
        if (ordered.Length == 0)
            throw new InvalidDataException("The native missile catalog cannot be empty.");

        var index = new Dictionary<string, AuthoritativeNativeMissileDefinition>(
            StringComparer.Ordinal);
        foreach (var definition in ordered) {
            if (string.IsNullOrWhiteSpace(definition.MissileId)
                || !IsIdentifier(definition.AddFunction, allowEmpty: true)
                || !IsIdentifier(definition.ProcessFunction, allowEmpty: true)
                || !IsIdentifier(definition.CastSound, allowEmpty: true)
                || !IsIdentifier(definition.HitSound, allowEmpty: true)
                || !IsIdentifier(definition.Graphic, allowEmpty: true)
                || !KnownMovementDistributions.Contains(definition.MovementDistribution)
                || !index.TryAdd(definition.MissileId, definition)) {
                throw new InvalidDataException(
                    $"Native missile definition '{definition.MissileId}' is invalid or duplicated.");
            }

            var flags = ParseFlags(definition.Flags);
            if (flags.Any(flag => !KnownFlags.Contains(flag)))
                throw new InvalidDataException(
                    $"Native missile '{definition.MissileId}' contains an unknown flag.");
            if (definition.OnExpiryMissile is not null
                && !IsIdentifier(definition.OnExpiryMissile, allowEmpty: false)) {
                throw new InvalidDataException(
                    $"Native missile '{definition.MissileId}' has an invalid expiry missile.");
            }
        }

        foreach (var definition in ordered.Where(definition =>
                     !string.IsNullOrWhiteSpace(definition.OnExpiryMissile))) {
            if (!index.ContainsKey(definition.OnExpiryMissile!))
                throw new InvalidDataException(
                    $"Native missile '{definition.MissileId}' references unknown expiry missile "
                    + $"'{definition.OnExpiryMissile}'.");
        }

        this.definitions = Array.AsReadOnly(ordered);
        byId = index;
    }

    public IReadOnlyList<AuthoritativeNativeMissileDefinition> Definitions => definitions;

    /** Rejects partial native missile metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production native-missile table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("missile_id");
            _ = row.Required("flags");
        }
    }

    /**
     * Requires the complete native `misdat.tsv` identity namespace. Direct
     * compatibility callers may still construct intentionally partial catalogs.
     */
    public void ValidateCompleteNativeNamespace()
    {
        var actual = definitions
            .Where(definition => !definition.CompatibilityAlias)
            .Select(definition => definition.MissileId)
            .ToHashSet(StringComparer.Ordinal);
        var missing = CompleteNativeMissileIds
            .Except(actual, StringComparer.Ordinal)
            .OrderBy(missileId => missileId, StringComparer.Ordinal)
            .ToArray();
        var unexpected = actual
            .Except(CompleteNativeMissileIds, StringComparer.Ordinal)
            .OrderBy(missileId => missileId, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length == 0 && unexpected.Length == 0)
            return;

        throw new InvalidDataException(
            "The active native missile namespace must match the complete native misdat.tsv contract. "
            + $"Missing: {string.Join(", ", missing)}. "
            + $"Unexpected: {string.Join(", ", unexpected)}.");
    }

    /** Requires the base native monster-color aliases used by compatibility presentation. */
    public void ValidateCompatibilityAliases()
    {
        var actual = definitions
            .Where(definition => definition.CompatibilityAlias)
            .Select(definition => definition.MissileId)
            .ToHashSet(StringComparer.Ordinal);
        var missing = RequiredCompatibilityAliasIds
            .Except(actual, StringComparer.Ordinal)
            .OrderBy(missileId => missileId, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException(
                "The active native missile catalog is missing required compatibility aliases: "
                + string.Join(", ", missing));
    }

    public bool TryGet(
        string missileId,
        out AuthoritativeNativeMissileDefinition definition)
        => byId.TryGetValue(missileId, out definition!);

    public AuthoritativeNativeMissileDefinition GetRequired(string missileId)
    {
        if (!byId.TryGetValue(missileId, out var definition))
            throw new KeyNotFoundException(
                $"Native missile '{missileId}' is not present in the active catalog.");
        return definition;
    }

    /** Ensures native missile graphics resolve through the active sprite catalog. */
    public void ValidateGraphicReferences(AuthoritativeNativeMissileSpriteCatalog sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        foreach (var definition in definitions.Where(definition =>
                     !definition.CompatibilityAlias
                     && !string.IsNullOrWhiteSpace(definition.Graphic))) {
            if (!sprites.TryGet(definition.Graphic, out _))
                throw new InvalidDataException(
                    $"Native missile '{definition.MissileId}' references unknown graphic "
                    + $"'{definition.Graphic}'.");
        }
    }

    public static AuthoritativeNativeMissileCatalog LoadTsv(
        string sourcePath,
        string contents,
        IEnumerable<string>? compatibilityAliases = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeNativeMissileCatalog(table.Rows.Select(row =>
            new AuthoritativeNativeMissileDefinition(
                row.Required("missile_id"),
                OptionalText(row, "add_function"),
                OptionalText(row, "process_function"),
                OptionalText(row, "cast_sound"),
                OptionalText(row, "hit_sound"),
                OptionalText(row, "graphic"),
                OptionalText(row, "flags"),
                OptionalText(row, "movement_distribution"),
                OptionalTextOrNull(row, "on_expiry_missile"))),
            compatibilityAliases);
    }

    public static IReadOnlyList<string> LoadCompatibilityAliasesTsv(
        string sourcePath,
        string contents)
        => TsvTable.Parse(sourcePath, contents).Rows
            .Select(row => row.Required("missile_id"))
            .ToArray();

    private static IReadOnlyList<string> ParseFlags(string flags)
        => string.IsNullOrWhiteSpace(flags)
            ? []
            : flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string OptionalText(TsvRow row, string column)
        => row.TryGet(column, out var value) ? value : string.Empty;

    private static string? OptionalTextOrNull(TsvRow row, string column)
        => row.TryGet(column, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static bool IsIdentifier(string value, bool allowEmpty)
    {
        if (allowEmpty && string.IsNullOrEmpty(value))
            return true;
        if (string.IsNullOrEmpty(value)
            || !char.IsLetter(value[0]) && value[0] != '_')
            return false;
        return value.Skip(1).All(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-');
    }
}
