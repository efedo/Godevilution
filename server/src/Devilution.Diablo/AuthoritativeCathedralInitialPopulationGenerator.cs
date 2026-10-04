namespace Devilution.Diablo;

/** Validated pre-selection inputs for the ordinary fresh Cathedral initial population. */
public sealed class AuthoritativeCathedralInitialPopulationParameters
{
    internal AuthoritativeCathedralBestiaryParameters BestiaryParameters { get; }
    internal AuthoritativeCathedralThemeParameters ThemeTemplate { get; }
    internal AuthoritativeCathedralPopulationParameters ObjectTemplate { get; }

    public AuthoritativeCathedralInitialPopulationParameters(
        int depth, uint populationSeed, uint storySeed,
        AuthoritativeCathedralPopulationGeometry preThemeGeometry,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> rawMonsterCatalog,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objectCatalog,
        AuthoritativeCathedralPopulationPlayer player, int trapPercent,
        IReadOnlyList<int> storyMessages, int nextTransparencyValue,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        int maximumPlacementAttempts = 1_000_000,
        int maximumThemeSelectionAttempts = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(preThemeGeometry);
        BestiaryParameters = new(depth, populationSeed, rawMonsterCatalog);
        // Validate shared object metadata without running or supplying a selected bestiary.
        ObjectTemplate = new(depth, populationSeed, storySeed, preThemeGeometry,
            objectCatalog, [BestiaryParameters.Catalog[AuthoritativeCathedralPopulationParameters.GolemNativeType].PopulationDefinition],
            player, trapPercent, storyMessages, maximumPlacementAttempts);
        ThemeTemplate = new(depth, preThemeGeometry.Pieces, preThemeGeometry.Transparency,
            preThemeGeometry.PieceProperties, preThemeGeometry.Populated,
            new bool[AuthoritativeCathedralPopulationGeometry.CellCount], nextTransparencyValue,
            triggerPositions, [AuthoritativeCathedralPopulationParameters.GolemNativeType],
            populationSeed, maximumThemeSelectionAttempts);
    }

    public int Depth => ObjectTemplate.Depth;
    public uint PopulationSeed => ObjectTemplate.PopulationSeed;
    public uint StorySeed => ObjectTemplate.StorySeed;
    public AuthoritativeCathedralPopulationGeometry PreThemeGeometry => ObjectTemplate.Geometry;
}

/** The connected pre-selection, held-theme and initial-object results; not final encounters. */
public sealed class AuthoritativeCathedralInitialPopulationResult
{
    internal AuthoritativeCathedralInitialPopulationResult(
        AuthoritativeCathedralBestiaryResult bestiary,
        AuthoritativeCathedralThemeResult themes,
        AuthoritativeCathedralPopulationResult population)
    {
        Bestiary = bestiary;
        Themes = themes;
        Population = population;
    }

    public AuthoritativeCathedralBestiaryResult Bestiary { get; }
    public AuthoritativeCathedralThemeResult Themes { get; }
    public AuthoritativeCathedralPopulationResult Population { get; }
    public uint FinalRandomState => Population.FinalRandomState;
}

/**
 * Pure GetLevelMTypes -> InitThemes -> population reseed -> HoldThemeRooms ->
 * InitGolems/InitObjects. Inputs contain no selected types, held cells or final state.
 * Fresh bestiary/object storage, ordinary original Diablo, no quests, SP Normal.
 */
public static class AuthoritativeCathedralInitialPopulationGenerator
{
    public static AuthoritativeCathedralInitialPopulationResult Generate(
        AuthoritativeCathedralInitialPopulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var bestiary = AuthoritativeCathedralBestiaryGenerator.Generate(parameters.BestiaryParameters);
        var themeInput = parameters.ThemeTemplate;
        var themes = AuthoritativeCathedralThemeGenerator.Generate(new(
            parameters.Depth, themeInput.Pieces, themeInput.Transparency, themeInput.PieceProperties,
            themeInput.InitialPopulated, themeInput.ObjectOccupied, themeInput.NextTransparencyValue,
            themeInput.TriggerPositions, bestiary.Entries.Select(entry => entry.NativeType).ToArray(),
            bestiary.FinalRandomState, themeInput.MaximumThemeSelectionAttempts));
        var objectInput = parameters.ObjectTemplate;
        var initial = parameters.PreThemeGeometry;
        var heldGeometry = new AuthoritativeCathedralPopulationGeometry(initial.Pieces, initial.Special,
            initial.Transparency, initial.PieceProperties, themes.HeldPopulatedCells,
            initial.PlayerOccupied, initial.MonsterOccupied);
        // Reset to the independent population seed; do not continue the theme stream.
        var population = AuthoritativeCathedralPopulationGenerator.Generate(new(
            parameters.Depth, parameters.PopulationSeed, parameters.StorySeed,
            heldGeometry, objectInput.Objects, bestiary.PopulationDefinitions,
            objectInput.Player, objectInput.TrapPercent, objectInput.StoryMessages,
            objectInput.MaximumPlacementAttempts));
        return new(bestiary, themes, population);
    }
}
