using System.Collections.Frozen;

namespace Devilution.Diablo;

/** Complete detached Catacombs input through CreateThemeRooms. */
public sealed class AuthoritativeCatacombsFullPopulationRequest
{
	private const int CellCount = AuthoritativeCathedralPopulationGeometry.CellCount;
	private const int MonsterCapacity = AuthoritativeCathedralPopulationParameters.MonsterCapacity;
	private const int ItemCapacity = AuthoritativeCathedralInitialItemGenerator.PoolCapacity;
	private const int ActiveItemCapacity = AuthoritativeCathedralInitialItemGenerator.ActiveCapacity;
	private const byte TilePopulated = 1 << 3;

	public AuthoritativeCatacombsFullPopulationRequest(
		AuthoritativeCatacombsInitialPopulationParameters initialPopulation,
		uint monsterClearRandomState,
		IReadOnlyList<int> initialMonsterTileRefs,
		IReadOnlyList<int> initialPlayerRefs,
		IReadOnlyList<byte> initialTileFlags,
		IReadOnlyList<byte> initialCorpses,
		IReadOnlyList<byte> initialLight,
		IReadOnlyList<byte> initialPreLight,
		AuthoritativeCathedralEncounterLightAllocator lightAllocator,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition> orderedUniqueDefinitions,
		int monsterDensityDivisor,
		IReadOnlyList<bool> shrineAvailable,
		AuthoritativeCathedralThemeItemCatalog itemCatalog,
		AuthoritativeCathedralThemeItemPolicy itemPolicy,
		AuthoritativeCathedralThemeItemPlayerContext? itemPlayer,
		IReadOnlyList<AuthoritativeCathedralInitialItemState> initialItemSlots,
		IReadOnlyList<int> initialActiveItemIds,
		int initialActiveItemCount,
		IReadOnlySet<int> initialUniqueItemFlags,
		IReadOnlyList<AuthoritativeCathedralInitialItemTile> initialItemTiles,
		IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> potionDefinitions,
		int maximumPlacementAttempts = 1_000_000,
		int maximumItemPlacementAttempts = 1_000_000,
		IReadOnlyList<bool>? initialVisibleTransparencyRegions = null)
		: this(initialPopulation, monsterClearRandomState, initialMonsterTileRefs,
			initialPlayerRefs, initialTileFlags, initialCorpses, initialLight,
			initialPreLight, lightAllocator, triggerPositions, orderedUniqueDefinitions,
			monsterDensityDivisor, shrineAvailable, itemCatalog, itemPolicy, itemPlayer,
			initialItemSlots, initialActiveItemIds, initialActiveItemCount,
			initialUniqueItemFlags, initialItemTiles, potionDefinitions,
			maximumPlacementAttempts, maximumItemPlacementAttempts,
			initialVisibleTransparencyRegions, playerInitialized: false)
	{
	}

	internal AuthoritativeCatacombsFullPopulationRequest(
		AuthoritativeCatacombsInitialPopulationParameters initialPopulation,
		uint monsterClearRandomState,
		IReadOnlyList<int> initialMonsterTileRefs,
		IReadOnlyList<int> initialPlayerRefs,
		IReadOnlyList<byte> initialTileFlags,
		IReadOnlyList<byte> initialCorpses,
		IReadOnlyList<byte> initialLight,
		IReadOnlyList<byte> initialPreLight,
		AuthoritativeCathedralEncounterLightAllocator lightAllocator,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition> orderedUniqueDefinitions,
		int monsterDensityDivisor,
		IReadOnlyList<bool> shrineAvailable,
		AuthoritativeCathedralThemeItemCatalog itemCatalog,
		AuthoritativeCathedralThemeItemPolicy itemPolicy,
		AuthoritativeCathedralThemeItemPlayerContext? itemPlayer,
		IReadOnlyList<AuthoritativeCathedralInitialItemState> initialItemSlots,
		IReadOnlyList<int> initialActiveItemIds,
		int initialActiveItemCount,
		IReadOnlySet<int> initialUniqueItemFlags,
		IReadOnlyList<AuthoritativeCathedralInitialItemTile> initialItemTiles,
		IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> potionDefinitions,
		int maximumPlacementAttempts,
		int maximumItemPlacementAttempts,
		IReadOnlyList<bool>? initialVisibleTransparencyRegions,
		bool playerInitialized)
	{
		ArgumentNullException.ThrowIfNull(initialPopulation);
		ArgumentNullException.ThrowIfNull(initialMonsterTileRefs);
		ArgumentNullException.ThrowIfNull(initialPlayerRefs);
		ArgumentNullException.ThrowIfNull(initialTileFlags);
		ArgumentNullException.ThrowIfNull(initialCorpses);
		ArgumentNullException.ThrowIfNull(initialLight);
		ArgumentNullException.ThrowIfNull(initialPreLight);
		ArgumentNullException.ThrowIfNull(lightAllocator);
		ArgumentNullException.ThrowIfNull(triggerPositions);
		ArgumentNullException.ThrowIfNull(orderedUniqueDefinitions);
		ArgumentNullException.ThrowIfNull(shrineAvailable);
		ArgumentNullException.ThrowIfNull(itemCatalog);
		ArgumentNullException.ThrowIfNull(itemPolicy);
		ArgumentNullException.ThrowIfNull(initialItemSlots);
		ArgumentNullException.ThrowIfNull(initialActiveItemIds);
		ArgumentNullException.ThrowIfNull(initialUniqueItemFlags);
		ArgumentNullException.ThrowIfNull(initialItemTiles);
		ArgumentNullException.ThrowIfNull(potionDefinitions);

		FamilyContext = AuthoritativeNativeDungeonFamilyContext.Catacombs(initialPopulation.Depth);
		if (initialPopulation.FamilyContext != FamilyContext
			|| initialPopulation.Generation.Depth != initialPopulation.Depth
			|| !initialPopulation.Generation.NativeTransparency.SequenceEqual(initialPopulation.PreThemeGeometry.Transparency))
			throw new InvalidDataException("The Catacombs initial result, geometry, and family context must agree.");
		if (itemPolicy.Depth != initialPopulation.Depth
			|| itemPolicy.GameMode != DiabloGameMode.SinglePlayer
			|| itemPolicy.Difficulty != DiabloDifficulty.Normal
			|| itemPolicy.Hellfire || itemPolicy.Spawn || itemPolicy.QuestEnabled || itemPolicy.SetLevel
			|| !itemPolicy.LegacyLevelItemGenerationAllowed
			|| itemPolicy.CurrentDungeonLevel is < 0 or > 8
			|| (itemPolicy.CurrentDungeonLevel != 0 && itemPolicy.CurrentDungeonLevel != initialPopulation.Depth))
			throw new InvalidDataException("Only ordinary original-Diablo Catacombs SP Normal generation is supported.");
		if (maximumPlacementAttempts <= 0 || maximumItemPlacementAttempts <= 0)
			throw new InvalidDataException("Full Catacombs population bounds must be positive.");
		if (monsterDensityDivisor <= 0)
			throw new InvalidDataException("Full Catacombs monster density divisor must be positive.");
		RequireCount(initialMonsterTileRefs, CellCount, nameof(initialMonsterTileRefs));
		RequireCount(initialPlayerRefs, CellCount, nameof(initialPlayerRefs));
		RequireCount(initialTileFlags, CellCount, nameof(initialTileFlags));
		RequireCount(initialCorpses, CellCount, nameof(initialCorpses));
		RequireCount(initialLight, CellCount, nameof(initialLight));
		RequireCount(initialPreLight, CellCount, nameof(initialPreLight));
		RequireCount(shrineAvailable, 26, nameof(shrineAvailable));
		if (!shrineAvailable.Any(value => value))
			throw new InvalidDataException("Ordinary Catacombs shrine generation requires an admitted shrine row.");
		if (initialVisibleTransparencyRegions is not null)
			RequireCount(initialVisibleTransparencyRegions, 256, nameof(initialVisibleTransparencyRegions));

		var geometry = initialPopulation.PreThemeGeometry;
		if (triggerPositions.Count is < 1 or > 7
			|| triggerPositions.Distinct().Count() != triggerPositions.Count
			|| triggerPositions.Any(point => (uint)point.X >= AuthoritativeCathedralPopulationGeometry.Width
				|| (uint)point.Y >= AuthoritativeCathedralPopulationGeometry.Width))
			throw new InvalidDataException("Catacombs encounter triggers must be a bounded distinct native trigger list.");
		if (geometry.PlayerOccupied.Any(value => value) || geometry.MonsterOccupied.Any(value => value))
			throw new InvalidDataException("Fresh full Catacombs population cannot retain pre-phase player or monster occupancy.");
		for (var index = 0; index < CellCount; index++) {
			if (initialMonsterTileRefs[index] != 0 || initialPlayerRefs[index] != 0)
				throw new InvalidDataException("Full Catacombs population requires fresh monster and player tile references.");
			if (((initialTileFlags[index] & TilePopulated) != 0) != geometry.Populated[index])
				throw new InvalidDataException("Pre-theme TileFlags must match the supplied raw Catacombs populated channel.");
		}
		if (playerInitialized)
			AuthoritativeCathedralFullPopulationRequest.ValidatePlayerInitializedLight(
				initialPopulation.ObjectTemplate.Player, lightAllocator, itemPlayer);
		else if (lightAllocator.ActiveLightCount != 0)
			throw new InvalidDataException("Full fresh Catacombs population requires an inactive initial light allocator.");
		if (initialItemSlots.Count != ItemCapacity
			|| initialActiveItemIds.Count != ActiveItemCapacity
			|| initialActiveItemCount != 0
			|| !initialActiveItemIds.SequenceEqual(Enumerable.Range(0, ActiveItemCapacity)))
			throw new InvalidDataException("Full fresh Catacombs population requires an empty native item pool.");
		foreach (var item in initialItemSlots)
			AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
		if (initialUniqueItemFlags.Any(value => value is < 0 or >= ItemCapacity))
			throw new InvalidDataException("Initial Catacombs unique-item history contains an unsafe native index.");
		RequireCount(initialItemTiles, CellCount, nameof(initialItemTiles));
		foreach (var tile in initialItemTiles) {
			ArgumentNullException.ThrowIfNull(tile);
			if (tile.PlayerRef != 0 || tile.MonsterRef != 0 || tile.ObjectRef != 0 || tile.ItemRef != 0)
				throw new InvalidDataException("Full fresh Catacombs item placement requires empty tile references.");
		}
		AuthoritativeCathedralFullPopulationRequest.ValidatePotionRows(itemCatalog, potionDefinitions);

		MonsterClearRandomState = monsterClearRandomState;
		InitialPopulation = initialPopulation;
		InitialMonsterTileRefs = Array.AsReadOnly(initialMonsterTileRefs.ToArray());
		InitialPlayerRefs = Array.AsReadOnly(initialPlayerRefs.ToArray());
		InitialTileFlags = Array.AsReadOnly(initialTileFlags.ToArray());
		InitialCorpses = Array.AsReadOnly(initialCorpses.ToArray());
		InitialLight = Array.AsReadOnly(initialLight.ToArray());
		InitialPreLight = Array.AsReadOnly(initialPreLight.ToArray());
		LightAllocator = new(lightAllocator.ActiveLightSlots, lightAllocator.ActiveLightCount,
			lightAllocator.LightRecords, lightAllocator.UpdateLighting, lightAllocator.DisableLighting);
		TriggerPositions = Array.AsReadOnly(triggerPositions.ToArray());
		OrderedUniqueDefinitions = Array.AsReadOnly(orderedUniqueDefinitions.ToArray());
		MonsterDensityDivisor = monsterDensityDivisor;
		ShrineAvailable = Array.AsReadOnly(shrineAvailable.ToArray());
		ItemCatalog = itemCatalog;
		ItemPolicy = itemPolicy;
		ItemPlayer = itemPlayer;
		InitialItemSlots = Array.AsReadOnly(initialItemSlots.Select(value => value with { Animation = value.Animation with { } }).ToArray());
		InitialActiveItemIds = Array.AsReadOnly(initialActiveItemIds.ToArray());
		InitialActiveItemCount = initialActiveItemCount;
		InitialUniqueItemFlags = initialUniqueItemFlags.ToFrozenSet();
		InitialItemTiles = Array.AsReadOnly(initialItemTiles.ToArray());
		PotionDefinitions = Array.AsReadOnly(potionDefinitions.ToArray());
		MaximumPlacementAttempts = maximumPlacementAttempts;
		MaximumItemPlacementAttempts = maximumItemPlacementAttempts;
		InitialVisibleTransparencyRegions = Array.AsReadOnly((initialVisibleTransparencyRegions ?? new bool[256]).ToArray());
	}

	internal AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }
	public uint MonsterClearRandomState { get; }
	public AuthoritativeCatacombsInitialPopulationParameters InitialPopulation { get; }
	public IReadOnlyList<int> InitialMonsterTileRefs { get; }
	public IReadOnlyList<int> InitialPlayerRefs { get; }
	public IReadOnlyList<byte> InitialTileFlags { get; }
	public IReadOnlyList<byte> InitialCorpses { get; }
	public IReadOnlyList<byte> InitialLight { get; }
	public IReadOnlyList<byte> InitialPreLight { get; }
	public AuthoritativeCathedralEncounterLightAllocator LightAllocator { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
	public IReadOnlyList<AuthoritativeCathedralUniqueDefinition> OrderedUniqueDefinitions { get; }
	public int MonsterDensityDivisor { get; }
	public IReadOnlyList<bool> ShrineAvailable { get; }
	public AuthoritativeCathedralThemeItemCatalog ItemCatalog { get; }
	public AuthoritativeCathedralThemeItemPolicy ItemPolicy { get; }
	public AuthoritativeCathedralThemeItemPlayerContext? ItemPlayer { get; }
	public IReadOnlyList<AuthoritativeCathedralInitialItemState> InitialItemSlots { get; }
	public IReadOnlyList<int> InitialActiveItemIds { get; }
	public int InitialActiveItemCount { get; }
	public IReadOnlySet<int> InitialUniqueItemFlags { get; }
	public IReadOnlyList<AuthoritativeCathedralInitialItemTile> InitialItemTiles { get; }
	public IReadOnlyList<AuthoritativeCathedralInitialItemDefinition> PotionDefinitions { get; }
	public int MaximumPlacementAttempts { get; }
	public int MaximumItemPlacementAttempts { get; }
	public IReadOnlyList<bool> InitialVisibleTransparencyRegions { get; }

	internal AuthoritativeNativeFullPopulationInputView ToCommonView()
		=> new(FamilyContext, MonsterClearRandomState, InitialMonsterTileRefs, InitialPlayerRefs,
			InitialTileFlags, InitialCorpses, InitialLight, InitialPreLight, LightAllocator,
			TriggerPositions, OrderedUniqueDefinitions, MonsterDensityDivisor, ShrineAvailable,
			ItemCatalog, ItemPolicy, ItemPlayer, InitialItemSlots, InitialActiveItemIds,
			InitialActiveItemCount, InitialUniqueItemFlags, InitialItemTiles, PotionDefinitions,
			MaximumPlacementAttempts, MaximumItemPlacementAttempts, InitialVisibleTransparencyRegions);

	private static void RequireCount<T>(IReadOnlyList<T> values, int expected, string name)
	{
		if (values.Count != expected)
			throw new InvalidDataException($"Native {name} requires exactly {expected} entries.");
	}
}

/** Full fresh Catacombs result; it remains distinct from the Cathedral API family. */
public sealed class AuthoritativeCatacombsFullPopulationResult
{
	internal AuthoritativeCatacombsFullPopulationResult(
		AuthoritativeCatacombsInitialPopulationResult initialPopulation,
		AuthoritativeNativeFullPopulationOutput output)
	{
		InitialPopulation = initialPopulation ?? throw new ArgumentNullException(nameof(initialPopulation));
		ArgumentNullException.ThrowIfNull(output);
		ClearedMonsterPool = output.ClearedMonsterPool;
		Encounters = output.Encounters;
		InitialItems = output.InitialItems;
		BeforeThemeRooms = output.BeforeThemeRooms;
		ThemeRooms = output.ThemeRooms;
	}

	public AuthoritativeCatacombsInitialPopulationResult InitialPopulation { get; }
	public AuthoritativeCathedralFreshMonsterPool ClearedMonsterPool { get; }
	public AuthoritativeCathedralEncounterResult Encounters { get; }
	public AuthoritativeCathedralInitialItemResult InitialItems { get; }
	public AuthoritativeCathedralThemeRoomState BeforeThemeRooms { get; }
	public AuthoritativeCathedralThemeRoomResult ThemeRooms { get; }
	public AuthoritativeCathedralThemeRoomState State => ThemeRooms.State;
	public uint FinalRandomState => ThemeRooms.FinalRandomState;
}

/** Family-explicit Catacombs composition; selection phases run once before the shared suffix. */
public static class AuthoritativeCatacombsFullPopulationGenerator
{
	public static AuthoritativeCatacombsFullPopulationResult Generate(AuthoritativeCatacombsFullPopulationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var initial = AuthoritativeCatacombsInitialPopulationGenerator.Generate(request.InitialPopulation);
		return Generate(request, initial);
	}

	internal static AuthoritativeCatacombsFullPopulationResult Generate(
		AuthoritativeCatacombsFullPopulationRequest request,
		AuthoritativeCatacombsInitialPopulationResult initial)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(initial);
		var initialView = AuthoritativeNativeInitialPopulationView.FromCatacombs(request.InitialPopulation, initial);
		var output = AuthoritativeCathedralFullPopulationGenerator.GenerateAfterInitial(
			request.ToCommonView(), initialView);
		return new(initial, output);
	}
}
