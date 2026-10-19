using Devilution.Data;

namespace Devilution.Diablo;

/** Class-bound native hero context selected by an optional compiled runtime table. */
public sealed class AuthoritativeCathedralNativeCharacterStart
{
    public const string TableName = "native-character-start.tsv";

    private static readonly string[] RequiredColumns = [
        "class_id",
        "class_symbol",
        "hero_base_life_maximum",
        "hero_base_mana_maximum",
        "hero_light_radius",
        "hero_idle_frames",
    ];

    private AuthoritativeCathedralNativeCharacterStart(
        uint classId,
        string classSymbol,
        AuthoritativeCathedralFreshLevelHeroContext hero,
        bool hasCompiledTable)
    {
        ClassId = classId;
        ClassSymbol = classSymbol;
        Hero = hero;
        HasCompiledTable = hasCompiledTable;
    }

    public uint ClassId { get; }

    public string ClassSymbol { get; }

    public AuthoritativeCathedralFreshLevelHeroContext Hero { get; }

    /** False only for the compatibility profile synthesized when the table is absent. */
    public bool HasCompiledTable { get; }

    /** Resolves the closed optional table, or the byte-compatible legacy Rogue start. */
    public static AuthoritativeCathedralNativeCharacterStart Resolve(
        TsvTable? table,
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativePlayerAnimationCatalog playerAnimations,
        AuthoritativePlayerLoadoutCatalog playerLoadouts)
    {
        ArgumentNullException.ThrowIfNull(playerClasses);
        ArgumentNullException.ThrowIfNull(playerAnimations);
        ArgumentNullException.ThrowIfNull(playerLoadouts);

        if (table is null)
            return CreateLegacyRogue(playerClasses, playerAnimations, playerLoadouts);
        if (!string.Equals(table.SourcePath, TableName, StringComparison.Ordinal)
            || !table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Native character-start table must use its exact ordered schema at '{TableName}'.");
        if (table.Rows.Count != 1)
            throw new InvalidDataException("Native character-start table must contain exactly one class row.");

        var row = table.Rows[0];
        var classId = row.RequiredUInt32("class_id");
        if (classId is not (0U or 1U or 2U))
            throw new InvalidDataException("Native character-start table supports only Warrior, Rogue, or Sorcerer.");
        var playerClass = playerClasses.GetRequired(classId);
        if (playerClass.Availability != DiabloPlayerClassAvailability.BaseGame
            || !string.Equals(row.Required("class_symbol"), playerClass.Symbol, StringComparison.Ordinal))
            throw new InvalidDataException("Native character-start class ID and symbol must identify one base-game class.");

        var template = CreateCanonicalTemplate(playerClass, playerLoadouts);
        var hero = new AuthoritativeCathedralFreshLevelHeroContext(
            template.BaseLifeMaximum,
            template.BaseManaMaximum,
            template.CreatePlayer().LightRadius,
            playerAnimations.GetRequired(classId).IdleFrames,
            new(57, 40),
            LevelChanging: true,
            LevelLoading: true);
        if (row.RequiredInt32("hero_base_life_maximum") != hero.MaximumLifeBase
            || row.RequiredInt32("hero_base_mana_maximum") != hero.MaximumManaBase
            || row.RequiredInt32("hero_light_radius") != hero.LightRadius
            || row.RequiredInt32("hero_idle_frames") != hero.IdleFrames)
            throw new InvalidDataException(
                "Native character-start hero facts must match the canonical class, loadout, and animation catalogs.");

        return new(classId, playerClass.Symbol, hero, hasCompiledTable: true);
    }

    private static AuthoritativeCathedralNativeCharacterStart CreateLegacyRogue(
        AuthoritativePlayerClassCatalog playerClasses,
        AuthoritativePlayerAnimationCatalog playerAnimations,
        AuthoritativePlayerLoadoutCatalog playerLoadouts)
    {
        var playerClass = playerClasses.Definitions.SingleOrDefault(definition =>
            string.Equals(definition.Symbol, "rogue", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Native Cathedral population requires the canonical Rogue class.");
        var template = CreateCanonicalTemplate(playerClass, playerLoadouts);
        // The original compiled L1 profile captured radius 8. Keep that legacy
        // generation input unchanged; class-bound profiles derive live radius.
        var hero = new AuthoritativeCathedralFreshLevelHeroContext(
            template.BaseLifeMaximum,
            template.BaseManaMaximum,
            LightRadius: 8,
            IdleFrames: playerAnimations.GetRequired(playerClass.ClassId).IdleFrames,
            PreEntryFuture: new(57, 40),
            LevelChanging: true,
            LevelLoading: true);
        return new(playerClass.ClassId, playerClass.Symbol, hero, hasCompiledTable: false);
    }

    private static DiabloPlayerTemplate CreateCanonicalTemplate(
        AuthoritativePlayerClassDefinition playerClass,
        AuthoritativePlayerLoadoutCatalog playerLoadouts)
        => playerLoadouts.CreateTemplate(
            "native-cathedral-population-profile",
            playerClass.ClassId,
            goldOverride: null,
            lifeOverride: null,
            manaOverride: null,
            startingLevelId: 1,
            startingPositionX: 57,
            startingPositionY: 40);
}
