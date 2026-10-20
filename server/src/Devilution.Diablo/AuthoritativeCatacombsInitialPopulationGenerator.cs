namespace Devilution.Diablo;

/** Validated, detached inputs for a fresh quest-disabled L2 population phase. */
public sealed class AuthoritativeCatacombsInitialPopulationParameters
{
	internal AuthoritativeCathedralBestiaryParameters BestiaryParameters { get; }
	internal AuthoritativeCathedralThemeParameters ThemeTemplate { get; }
	internal AuthoritativeCathedralPopulationParameters ObjectTemplate { get; }
	internal AuthoritativeCatacombsGenerationResult Generation { get; }
	internal AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }

	public AuthoritativeCatacombsInitialPopulationParameters(
		int depth,
		uint populationSeed,
		uint storySeed,
		AuthoritativeCatacombsGenerationResult generation,
		AuthoritativeCathedralPopulationGeometry preThemeGeometry,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> rawMonsterCatalog,
		IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objectCatalog,
		AuthoritativeCathedralPopulationPlayer player,
		int trapPercent,
		IReadOnlyList<int> storyMessages,
		int maximumPlacementAttempts = 1_000_000,
		int maximumThemeSelectionAttempts = 1_000_000)
	{
		ArgumentNullException.ThrowIfNull(generation);
		ArgumentNullException.ThrowIfNull(preThemeGeometry);
		FamilyContext = AuthoritativeNativeDungeonFamilyContext.Catacombs(depth);
		generation.Validate();
		Generation = generation.CopyOwned();
		Generation.Validate();
		if (Generation.Depth != depth
			|| !Generation.NativeTransparency.SequenceEqual(preThemeGeometry.Transparency))
			throw new InvalidDataException("Catacombs population geometry must match the admitted generation result.");

		BestiaryParameters = new(depth, populationSeed, rawMonsterCatalog, FamilyContext);
		var golem = BestiaryParameters.Catalog[AuthoritativeCathedralBestiaryGenerator.GolemNativeType].PopulationDefinition;
		ObjectTemplate = new(depth, populationSeed, storySeed, preThemeGeometry,
			objectCatalog, [golem], player, trapPercent, storyMessages,
			maximumPlacementAttempts, FamilyContext);
		ThemeTemplate = new(depth, preThemeGeometry.Pieces, preThemeGeometry.Transparency,
			preThemeGeometry.PieceProperties, preThemeGeometry.Populated,
			new bool[AuthoritativeCathedralPopulationGeometry.CellCount], Generation.NextTransparencyValue,
			Array.Empty<AuthoritativeCathedralPopulationPoint>(), [AuthoritativeCathedralBestiaryGenerator.GolemNativeType],
			populationSeed, maximumThemeSelectionAttempts, FamilyContext);
	}

	public int Depth => ObjectTemplate.Depth;
	public uint PopulationSeed => ObjectTemplate.PopulationSeed;
	public uint StorySeed => ObjectTemplate.StorySeed;
	public AuthoritativeCathedralPopulationGeometry PreThemeGeometry => ObjectTemplate.Geometry;
}

/** Detached L2 initial selections and population, distinct from the C1 result identity. */
public sealed class AuthoritativeCatacombsInitialPopulationResult
{
	internal AuthoritativeCatacombsInitialPopulationResult(
		AuthoritativeCathedralBestiaryResult bestiary,
		AuthoritativeCathedralThemeResult themes,
		IReadOnlyList<AuthoritativeCatacombsSelectedTheme> selectedThemes,
		AuthoritativeCathedralPopulationResult population)
	{
		Bestiary = bestiary ?? throw new ArgumentNullException(nameof(bestiary));
		Themes = themes ?? throw new ArgumentNullException(nameof(themes));
		SelectedThemes = Array.AsReadOnly(selectedThemes.ToArray());
		Population = population ?? throw new ArgumentNullException(nameof(population));
	}

	public AuthoritativeCathedralBestiaryResult Bestiary { get; }
	public AuthoritativeCathedralThemeResult Themes { get; }
	public IReadOnlyList<AuthoritativeCatacombsSelectedTheme> SelectedThemes { get; }
	public AuthoritativeCathedralPopulationResult Population { get; }
	public uint FinalRandomState => Population.FinalRandomState;
}

/**
 * Pure L2 GetLevelMTypes → InitThemes/HoldThemeRooms → InitGolems/InitObjects
 * path for original quest-disabled single-player Normal. Geometry/catalog inputs
 * are detached and supplied by the profile; no map files or global state are read.
 */
public static class AuthoritativeCatacombsInitialPopulationGenerator
{
	public const string Policy = "quest-disabled-original-diablo-catacombs-initial-population";

	public static AuthoritativeCatacombsInitialPopulationResult Generate(
		AuthoritativeCatacombsInitialPopulationParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		var familyContext = AuthoritativeNativeDungeonFamilyContext.Catacombs(parameters.Depth);
		if (parameters.FamilyContext != familyContext)
			throw new InvalidDataException("Catacombs population parameters have an invalid family context.");

		var bestiary = AuthoritativeCathedralBestiaryGenerator.Generate(parameters.BestiaryParameters, familyContext);
		var (themes, selectedThemes) = AuthoritativeCathedralThemeGenerator.GenerateCatacombs(
			parameters.ThemeTemplate,
			familyContext,
			parameters.Generation.Themes,
			bestiary.SelectedNativeTypes,
			bestiary.FinalRandomState);

		var initial = parameters.PreThemeGeometry;
		var heldGeometry = new AuthoritativeCathedralPopulationGeometry(
			initial.Pieces,
			initial.Special,
			initial.Transparency,
			initial.PieceProperties,
			themes.HeldPopulatedCells,
			initial.PlayerOccupied,
			initial.MonsterOccupied);
		var objectTemplate = parameters.ObjectTemplate;
		var objectInput = new AuthoritativeCathedralPopulationParameters(
			parameters.Depth,
			parameters.PopulationSeed,
			parameters.StorySeed,
			heldGeometry,
			objectTemplate.Objects,
			bestiary.PopulationDefinitions,
			objectTemplate.Player,
			objectTemplate.TrapPercent,
			objectTemplate.StoryMessages,
			objectTemplate.MaximumPlacementAttempts,
			familyContext);
		var population = AuthoritativeCathedralPopulationGenerator.Generate(objectInput, familyContext);
		return new(bestiary, themes, selectedThemes, population);
	}
}
