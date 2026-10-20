using System.Collections.Frozen;

namespace Devilution.Diablo;

/** Fresh original Catacombs hero values before InitPlayer and the population reseed. */
public sealed record AuthoritativeCatacombsFreshLevelHeroContext(
	int MaximumLifeBase,
	int MaximumManaBase,
	int LightRadius,
	int IdleFrames,
	AuthoritativeCathedralPopulationPoint PreEntryFuture,
	bool LevelChanging,
	bool LevelLoading = true);

/** Production input for a fresh quest-disabled Catacombs floor. */
public sealed class AuthoritativeCatacombsFreshLevelRequest
{
	internal AuthoritativeCatacombsFreshLevelRequest(
		AuthoritativeCatacombsPopulationMetadata metadata,
		int depth,
	uint dungeonSeed,
	uint storySeed,
	AuthoritativeCatacombsEntry entry,
	uint? retainedLevelSeed,
	AuthoritativeCatacombsFreshLevelHeroContext hero,
	string stableLevelSymbol,
	int maximumPlacementAttempts,
	int maximumItemPlacementAttempts,
	int maximumThemeSelectionAttempts,
	IReadOnlySet<int> initialUniqueItemFlags)
	{
		Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
		Hero = hero ?? throw new ArgumentNullException(nameof(hero));
		_ = AuthoritativeNativeDungeonFamilyContext.Catacombs(depth);
		if (entry is not AuthoritativeCatacombsEntry.Main
			and not AuthoritativeCatacombsEntry.Previous
			and not AuthoritativeCatacombsEntry.TownWarpDown)
			throw new ArgumentOutOfRangeException(nameof(entry));
		if (entry == AuthoritativeCatacombsEntry.TownWarpDown && depth != 5)
			throw new ArgumentOutOfRangeException(nameof(entry), "The native Town warp enters only L5.");
		if (string.IsNullOrWhiteSpace(stableLevelSymbol) || stableLevelSymbol.Contains('\0'))
			throw new InvalidDataException("A stable Catacombs level symbol is required.");
		if (maximumPlacementAttempts <= 0 || maximumItemPlacementAttempts <= 0
			|| maximumThemeSelectionAttempts <= 0)
			throw new InvalidDataException("Fresh Catacombs generation bounds must be positive.");
		if (hero.MaximumLifeBase <= 0 || hero.MaximumManaBase < 0
			|| hero.LightRadius is < 2 or > 15 || hero.IdleFrames is < 1 or > sbyte.MaxValue
			|| hero.PreEntryFuture.X is < 0 or >= AuthoritativeCathedralPopulationGeometry.Width
			|| hero.PreEntryFuture.Y is < 0 or >= AuthoritativeCathedralPopulationGeometry.Width
			|| !hero.LevelLoading)
			throw new InvalidDataException("Fresh Catacombs requires native hero animation, stats, and pre-entry bounds.");
		ArgumentNullException.ThrowIfNull(initialUniqueItemFlags);
		if (initialUniqueItemFlags.Any(value => value is < 0 or >= AuthoritativeCathedralInitialItemGenerator.PoolCapacity))
			throw new InvalidDataException("Fresh Catacombs unique history contains an invalid native item flag.");

		Depth = depth;
		DungeonSeed = dungeonSeed;
		StorySeed = storySeed;
		Entry = entry;
		RetainedLevelSeed = retainedLevelSeed;
		StableLevelSymbol = stableLevelSymbol;
		MaximumPlacementAttempts = maximumPlacementAttempts;
		MaximumItemPlacementAttempts = maximumItemPlacementAttempts;
		MaximumThemeSelectionAttempts = maximumThemeSelectionAttempts;
		InitialUniqueItemFlags = initialUniqueItemFlags.ToFrozenSet();
	}

	public AuthoritativeCatacombsPopulationMetadata Metadata { get; }
	public int Depth { get; }
	public uint DungeonSeed { get; }
	public uint StorySeed { get; }
	public AuthoritativeCatacombsEntry Entry { get; }
	public uint? RetainedLevelSeed { get; }
	public AuthoritativeCatacombsFreshLevelHeroContext Hero { get; }
	public string StableLevelSymbol { get; }
	public int MaximumPlacementAttempts { get; }
	public int MaximumItemPlacementAttempts { get; }
	public int MaximumThemeSelectionAttempts { get; }
	public IReadOnlySet<int> InitialUniqueItemFlags { get; }
}

/** Complete native-boundary result before level-ready processing. */
public sealed class AuthoritativeCatacombsFreshLevelResult
{
	internal AuthoritativeCatacombsFreshLevelResult(
		AuthoritativeCatacombsFreshLevelRequest request,
		AuthoritativeCatacombsGeometry geometry,
		AuthoritativeCatacombsInitialPopulationParameters initialPopulation,
		AuthoritativeCatacombsFullPopulationRequest fullPopulationRequest,
		AuthoritativeCatacombsFullPopulationResult population,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<bool> preThemePopulated,
		IReadOnlyList<byte> initialTileFlags,
		IReadOnlyList<byte> initialLight,
		IReadOnlyList<byte> initialPreLight,
		AuthoritativeCathedralEncounterLightAllocator lightAllocator,
		uint monsterClearFinalRandomState,
		AuthoritativeCathedralPlayerPreparation playerPreparation,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState> visionRecords,
		IReadOnlyList<bool> visionActive,
		bool updateVision)
	{
		Request = request ?? throw new ArgumentNullException(nameof(request));
		Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
		InitialPopulation = initialPopulation ?? throw new ArgumentNullException(nameof(initialPopulation));
		FullPopulationRequest = fullPopulationRequest ?? throw new ArgumentNullException(nameof(fullPopulationRequest));
		Population = population ?? throw new ArgumentNullException(nameof(population));
		TriggerPositions = Array.AsReadOnly(triggerPositions.ToArray());
		PreThemePopulated = Array.AsReadOnly(preThemePopulated.ToArray());
		InitialTileFlags = Array.AsReadOnly(initialTileFlags.ToArray());
		InitialLight = Array.AsReadOnly(initialLight.ToArray());
		InitialPreLight = Array.AsReadOnly(initialPreLight.ToArray());
		LightAllocator = lightAllocator ?? throw new ArgumentNullException(nameof(lightAllocator));
		MonsterClearFinalRandomState = monsterClearFinalRandomState;
		PlayerPreparation = playerPreparation ?? throw new ArgumentNullException(nameof(playerPreparation));
		VisionRecords = Array.AsReadOnly(visionRecords.ToArray());
		VisionActive = Array.AsReadOnly(visionActive.ToArray());
		UpdateVision = updateVision;
	}

	public AuthoritativeCatacombsFreshLevelRequest Request { get; }
	public AuthoritativeCatacombsGeometry Geometry { get; }
	public AuthoritativeCatacombsInitialPopulationParameters InitialPopulation { get; }
	public AuthoritativeCatacombsFullPopulationRequest FullPopulationRequest { get; }
	public AuthoritativeCatacombsFullPopulationResult Population { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
	public IReadOnlyList<bool> PreThemePopulated { get; }
	public IReadOnlyList<byte> InitialTileFlags { get; }
	public IReadOnlyList<byte> InitialLight { get; }
	public IReadOnlyList<byte> InitialPreLight { get; }
	public AuthoritativeCathedralEncounterLightAllocator LightAllocator { get; }
	public uint MonsterClearFinalRandomState { get; }
	public AuthoritativeCathedralPlayerPreparation PlayerPreparation { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords { get; }
	public IReadOnlyList<bool> VisionActive { get; }
	public bool UpdateVision { get; }
	public int NextTransparencyValue => Geometry.GenerationResult.NextTransparencyValue;
}

/** Fresh L5-L8 Catacombs geometry, native population phases, player preparation, and initial view. */
public static class AuthoritativeCatacombsFreshLevelGenerator
{
	private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
	private const byte TilePopulated = 1 << 3;
	private const byte DefaultDungeonLight = 15;
	private const int DefaultMaximumAttempts = 1_000_000;

	public static AuthoritativeCatacombsFreshLevelRequest CreateRequest(
		AuthoritativeCatacombsPopulationMetadata metadata,
		int depth,
	uint dungeonSeed,
	uint storySeed,
	AuthoritativeCatacombsEntry entry,
	AuthoritativeCatacombsFreshLevelHeroContext hero,
	string? stableLevelSymbol = null,
	uint? retainedLevelSeed = null,
	IReadOnlySet<int>? initialUniqueItemFlags = null,
	int maximumPlacementAttempts = DefaultMaximumAttempts,
	int maximumItemPlacementAttempts = DefaultMaximumAttempts,
	int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
	{
		ArgumentNullException.ThrowIfNull(metadata);
		ArgumentNullException.ThrowIfNull(hero);
		return new(metadata, depth, dungeonSeed, storySeed, entry, retainedLevelSeed, hero,
			stableLevelSymbol ?? $"catacombs{depth}", maximumPlacementAttempts,
			maximumItemPlacementAttempts, maximumThemeSelectionAttempts,
			initialUniqueItemFlags ?? new HashSet<int>());
	}

	public static AuthoritativeCatacombsFreshLevelRequest CreateEntryRequest(
		AuthoritativeCatacombsPopulationMetadata metadata,
		int depth,
		uint dungeonSeed,
		uint storySeed,
		AuthoritativeCatacombsEntry entry,
		AuthoritativeCatacombsFreshLevelHeroContext hero,
		string? stableLevelSymbol = null,
		uint? retainedLevelSeed = null,
		IReadOnlySet<int>? initialUniqueItemFlags = null,
		int maximumPlacementAttempts = DefaultMaximumAttempts,
		int maximumItemPlacementAttempts = DefaultMaximumAttempts,
		int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
		=> CreateRequest(metadata, depth, dungeonSeed, storySeed, entry, hero,
			stableLevelSymbol, retainedLevelSeed, initialUniqueItemFlags,
			maximumPlacementAttempts, maximumItemPlacementAttempts, maximumThemeSelectionAttempts);

	public static AuthoritativeCatacombsFreshLevelResult Generate(AuthoritativeCatacombsFreshLevelRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var clearedMonsterPool = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(request.DungeonSeed);
		var generation = AuthoritativeCatacombsGenerator.Generate(request.Depth, request.DungeonSeed,
			request.Entry, request.RetainedLevelSeed,
			AuthoritativeCatacombsGenerationPolicy.QuestDisabledOriginalDiabloStairs);
		var geometry = AuthoritativeCatacombsGeometryMaterializer.Materialize(
			checked((uint)request.Depth), generation, request.Metadata.Geometry.Pieces,
			request.Metadata.EffectivePieceProperties, request.StableLevelSymbol);
		var derived = DerivePreThemeState(request.Metadata, geometry);
		var entryPosition = new AuthoritativeCathedralPopulationPoint(generation.EntryPosition.X, generation.EntryPosition.Y);
		var player = new AuthoritativeCathedralPopulationPlayer(
			0, request.Depth, true, request.Hero.LevelChanging, entryPosition, request.Hero.PreEntryFuture);
		var initialPopulation = new AuthoritativeCatacombsInitialPopulationParameters(
			request.Depth, request.DungeonSeed, request.StorySeed, generation,
			derived.Geometry, request.Metadata.Population.Monsters,
			request.Metadata.Population.Objects, player,
			request.Metadata.RuleForDepth(request.Depth).TrapPercent,
			request.Metadata.StoryMessages, request.MaximumPlacementAttempts,
			request.MaximumThemeSelectionAttempts);
		var initialResult = AuthoritativeCatacombsInitialPopulationGenerator.Generate(initialPopulation);
		// InitPlayer's cursor stream is derived from the theme boundary before the
		// independent population stream is consumed by the full suffix.
		var playerPreparation = PreparePlayer(initialResult.Themes.FinalRandomState, request.Hero.IdleFrames);
		var pendingVision = CreatePendingVision(entryPosition, request.Hero.LightRadius);
		var initialItems = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
			.Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
		var activeItemIds = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.ActiveCapacity).ToArray();
		var initialLight = Enumerable.Repeat(DefaultDungeonLight, CellCount).ToArray();
		var initialPreLight = new byte[CellCount];
		var lightAllocator = CreatePlayerLightAllocator(entryPosition, request.Hero.LightRadius);
		var itemPolicy = new AuthoritativeCathedralThemeItemPolicy(
			request.Depth, DiabloGameMode.SinglePlayer, DiabloDifficulty.Normal,
			Hellfire: false, Spawn: false, QuestEnabled: false, SetLevel: false,
			LegacyLevelItemGenerationAllowed: true, CurrentDungeonLevel: request.Depth);
		var fullRequest = new AuthoritativeCatacombsFullPopulationRequest(
			initialPopulation,
			request.DungeonSeed,
			new int[CellCount],
			new int[CellCount],
			derived.InitialTileFlags,
			new byte[CellCount],
			initialLight,
			initialPreLight,
			lightAllocator,
			derived.TriggerPositions,
			request.Metadata.UniqueMonsters.Definitions,
			request.Metadata.RuleForDepth(request.Depth).MonsterDensityDivisor,
			request.Metadata.ShrineAvailable,
			request.Metadata.Items.Catalog,
			itemPolicy,
			new AuthoritativeCathedralThemeItemPlayerContext(
				PlayerPresent: true,
				LevelLoading: request.Hero.LevelLoading,
				MaximumManaBase: request.Hero.MaximumManaBase,
				MaximumLifeBase: request.Hero.MaximumLifeBase),
			initialItems,
			activeItemIds,
			0,
			request.InitialUniqueItemFlags,
			Enumerable.Range(0, CellCount).Select(_ => new AuthoritativeCathedralInitialItemTile()).ToArray(),
			CreatePotionDefinitions(request.Metadata),
			request.MaximumPlacementAttempts,
			request.MaximumItemPlacementAttempts,
			new bool[256],
			playerInitialized: true);
		var population = AuthoritativeCatacombsFullPopulationGenerator.Generate(fullRequest, initialResult);
		return new(request, geometry, initialPopulation, fullRequest, population,
			derived.TriggerPositions, derived.PreThemePopulated, derived.InitialTileFlags,
			initialLight, initialPreLight, lightAllocator, clearedMonsterPool.FinalRandomState,
			playerPreparation, pendingVision.Records, pendingVision.Active, pendingVision.Update);
	}

	public static AuthoritativeCatacombsFreshLevelResult Generate(
		AuthoritativeCatacombsPopulationMetadata metadata,
		int depth,
		uint dungeonSeed,
		uint storySeed,
		AuthoritativeCatacombsEntry entry,
		AuthoritativeCatacombsFreshLevelHeroContext hero,
		string? stableLevelSymbol = null,
		uint? retainedLevelSeed = null,
		IReadOnlySet<int>? initialUniqueItemFlags = null,
		int maximumPlacementAttempts = DefaultMaximumAttempts,
		int maximumItemPlacementAttempts = DefaultMaximumAttempts,
		int maximumThemeSelectionAttempts = DefaultMaximumAttempts)
		=> Generate(CreateRequest(metadata, depth, dungeonSeed, storySeed, entry, hero,
			stableLevelSymbol, retainedLevelSeed, initialUniqueItemFlags, maximumPlacementAttempts,
			maximumItemPlacementAttempts, maximumThemeSelectionAttempts));

	private static AuthoritativeCatacombsFreshPreThemeState DerivePreThemeState(
		AuthoritativeCatacombsPopulationMetadata metadata,
		AuthoritativeCatacombsGeometry geometry)
	{
		var level = geometry.Level;
		if (level.Width != AuthoritativeCatacombsGeometryMaterializer.NativeWidth
			|| level.Height != AuthoritativeCatacombsGeometryMaterializer.NativeHeight
			|| level.NativePieceLayer.Count != CellCount
			|| level.NativeTransparency.Count != CellCount
			|| level.NativeSpecialLayer.Count != CellCount
			|| geometry.EffectivePieceProperties.Count != AuthoritativeCathedralPopulationGeometry.PieceCount)
			throw new InvalidDataException("Fresh Catacombs requires complete 112x112 geometry and native SOL state.");
		var pieces = level.NativePieceLayer.ToArray();
		var special = level.NativeSpecialLayer.Select(value => checked((byte)value)).ToArray();
		var transparency = level.NativeTransparency.Select(value => checked((byte)value)).ToArray();
		var triggerPositions = geometry.Triggers.Select(value => new AuthoritativeCathedralPopulationPoint(value.X, value.Y)).ToArray();
		var depth = geometry.GenerationResult.Depth;
		var expectedTriggerCount = depth == 5 ? 3 : 2;
		if (triggerPositions.Length != expectedTriggerCount
			|| triggerPositions.Distinct().Count() != triggerPositions.Length
			|| geometry.Triggers.Count(trigger => trigger.Kind == AuthoritativeCatacombsTriggerKind.PreviousLevel) != 1
			|| geometry.Triggers.Count(trigger => trigger.Kind == AuthoritativeCatacombsTriggerKind.NextLevel) != 1
			|| geometry.Triggers.Count(trigger => trigger.Kind == AuthoritativeCatacombsTriggerKind.TownWarpUp) != (depth == 5 ? 1 : 0)
			|| geometry.Triggers.Any(trigger => !Enum.IsDefined(trigger.Kind)
				|| (uint)trigger.X >= AuthoritativeCatacombsGeometryMaterializer.NativeWidth
				|| (uint)trigger.Y >= AuthoritativeCatacombsGeometryMaterializer.NativeHeight))
			throw new InvalidDataException("Fresh Catacombs requires the exact depth-specific native stair and warp triggers.");
		var populated = new bool[CellCount];
		foreach (var trigger in triggerPositions) {
			for (var dy = -2; dy <= 2; dy++) {
				for (var dx = -2; dx <= 2; dx++) {
					var x = trigger.X + dx;
					var y = trigger.Y + dy;
					if ((uint)x >= AuthoritativeCathedralPopulationGeometry.Width
						|| (uint)y >= AuthoritativeCathedralPopulationGeometry.Width)
						throw new InvalidDataException("Native Freeupstairs would write outside the Catacombs tile grid.");
					populated[y * AuthoritativeCathedralPopulationGeometry.Width + x] = true;
				}
			}
		}
		var flags = populated.Select(value => value ? TilePopulated : (byte)0).ToArray();
		var populationGeometry = new AuthoritativeCathedralPopulationGeometry(
			pieces, special, transparency, metadata.EffectivePieceProperties,
			populated, new bool[CellCount], new bool[CellCount]);
		return new(populationGeometry, triggerPositions, populated, flags);
	}

	private static IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> CreatePotionDefinitions(
		AuthoritativeCatacombsPopulationMetadata metadata)
		=> Array.AsReadOnly(metadata.Items.Catalog.Items
			.Where(row => row.NativeItemIndex is AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId
				or AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId)
			.Select(row => new AuthoritativeCathedralInitialItemDefinition(row.NativeItemIndex,
				row.ItemType, row.CursorGraphic, row.Name, row.Name, row.EquipLocation, row.ItemClass,
				row.MinimumDamage, row.MaximumDamage, row.MinimumArmorClass, row.MaximumArmorClass,
				row.Flags, row.MiscId, row.SpellId, row.Value, row.Durability,
				row.MinimumStrength, row.MinimumMagic, row.MinimumDexterity,
				row.AnimationFrameCount, row.AnimationTicksPerFrame)).ToArray());

	private static AuthoritativeCathedralEncounterLightAllocator CreatePlayerLightAllocator(
		AuthoritativeCathedralPopulationPoint entryPosition,
		int radius)
	{
		var records = Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights)
			.Select(lightId => new AuthoritativeCathedralEncounterLightState(
				lightId, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
		records[0] = new(0, entryPosition, entryPosition, 0, 0, radius, radius, false, true);
		return new(Enumerable.Range(0, AuthoritativeCathedralEncounterLightAllocator.MaximumLights).ToArray(),
			1, records, updateLighting: true, disableLighting: false);
	}

	private static AuthoritativeCathedralPlayerPreparation PreparePlayer(uint randomState, int idleFrames)
	{
		var random = new AuthoritativeLegacyRandom(randomState);
		var currentFrame = random.GenerateRnd(idleFrames - 1);
		var tickCounter = random.GenerateRnd(3);
		return new(idleFrames, currentFrame, tickCounter, 4, randomState, random.State, false);
	}

	private static AuthoritativeCatacombsPendingVisionState CreatePendingVision(
		AuthoritativeCathedralPopulationPoint entryPosition,
		int radius)
	{
		var records = Enumerable.Range(0, 4)
			.Select(lightId => new AuthoritativeCathedralEncounterLightState(
				lightId, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
		records[0] = new(0, entryPosition, new(0, 0), 0, 0, radius, 0, false, false);
		return new(records, new[] { true, false, false, false }, true);
	}

	private sealed record AuthoritativeCatacombsFreshPreThemeState(
		AuthoritativeCathedralPopulationGeometry Geometry,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions,
		IReadOnlyList<bool> PreThemePopulated,
		IReadOnlyList<byte> InitialTileFlags);

	private sealed record AuthoritativeCatacombsPendingVisionState(
		IReadOnlyList<AuthoritativeCathedralEncounterLightState> Records,
		IReadOnlyList<bool> Active,
		bool Update);
}
