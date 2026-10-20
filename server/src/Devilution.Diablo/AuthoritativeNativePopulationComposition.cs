namespace Devilution.Diablo;

/** Family-neutral view of already selected bestiary, themes, and initial objects. */
internal sealed class AuthoritativeNativeInitialPopulationView
{
	public AuthoritativeNativeInitialPopulationView(
		AuthoritativeNativeDungeonFamilyContext familyContext,
		int depth,
		uint populationSeed,
		uint storySeed,
		AuthoritativeCathedralPopulationGeometry geometry,
		IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
		AuthoritativeCathedralPopulationPlayer player,
		int trapPercent,
		IReadOnlyList<int> storyMessages,
		int maximumPlacementAttempts,
		AuthoritativeCathedralBestiaryResult bestiary,
		AuthoritativeCathedralThemeResult themes,
		AuthoritativeCathedralPopulationResult population)
	{
		familyContext.RequireMatch(depth);
		ArgumentNullException.ThrowIfNull(geometry);
		ArgumentNullException.ThrowIfNull(objects);
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(storyMessages);
		ArgumentNullException.ThrowIfNull(bestiary);
		ArgumentNullException.ThrowIfNull(themes);
		ArgumentNullException.ThrowIfNull(population);
		FamilyContext = familyContext;
		Depth = depth;
		PopulationSeed = populationSeed;
		StorySeed = storySeed;
		Geometry = geometry;
		Objects = objects;
		Player = player;
		TrapPercent = trapPercent;
		StoryMessages = storyMessages;
		MaximumPlacementAttempts = maximumPlacementAttempts;
		Bestiary = bestiary;
		Themes = themes;
		Population = population;
	}

	public AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }
	public int Depth { get; }
	public uint PopulationSeed { get; }
	public uint StorySeed { get; }
	public AuthoritativeCathedralPopulationGeometry Geometry { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> Objects { get; }
	public AuthoritativeCathedralPopulationPlayer Player { get; }
	public int TrapPercent { get; }
	public IReadOnlyList<int> StoryMessages { get; }
	public int MaximumPlacementAttempts { get; }
	public AuthoritativeCathedralBestiaryResult Bestiary { get; }
	public AuthoritativeCathedralThemeResult Themes { get; }
	public AuthoritativeCathedralPopulationResult Population { get; }

	public static AuthoritativeNativeInitialPopulationView FromCathedral(
		AuthoritativeCathedralInitialPopulationParameters parameters,
		AuthoritativeCathedralInitialPopulationResult result)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		ArgumentNullException.ThrowIfNull(result);
		var template = parameters.ObjectTemplate;
		return new(template.FamilyContext, template.Depth, template.PopulationSeed, template.StorySeed,
			template.Geometry, template.Objects, template.Player, template.TrapPercent,
			template.StoryMessages, template.MaximumPlacementAttempts,
			result.Bestiary, result.Themes, result.Population);
	}

	public static AuthoritativeNativeInitialPopulationView FromCatacombs(
		AuthoritativeCatacombsInitialPopulationParameters parameters,
		AuthoritativeCatacombsInitialPopulationResult result)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		ArgumentNullException.ThrowIfNull(result);
		var template = parameters.ObjectTemplate;
		return new(parameters.FamilyContext, template.Depth, template.PopulationSeed, template.StorySeed,
			template.Geometry, template.Objects, template.Player, template.TrapPercent,
			template.StoryMessages, template.MaximumPlacementAttempts,
			result.Bestiary, result.Themes, result.Population);
	}
}

/** Common, validated inputs after each public family request has enforced its own contract. */
internal sealed class AuthoritativeNativeFullPopulationInputView
{
	public AuthoritativeNativeFullPopulationInputView(
		AuthoritativeNativeDungeonFamilyContext familyContext,
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
		IReadOnlyList<bool> initialVisibleTransparencyRegions)
	{
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
		ArgumentNullException.ThrowIfNull(initialVisibleTransparencyRegions);
		FamilyContext = familyContext;
		MonsterClearRandomState = monsterClearRandomState;
		InitialMonsterTileRefs = initialMonsterTileRefs;
		InitialPlayerRefs = initialPlayerRefs;
		InitialTileFlags = initialTileFlags;
		InitialCorpses = initialCorpses;
		InitialLight = initialLight;
		InitialPreLight = initialPreLight;
		LightAllocator = lightAllocator;
		TriggerPositions = triggerPositions;
		OrderedUniqueDefinitions = orderedUniqueDefinitions;
		MonsterDensityDivisor = monsterDensityDivisor;
		ShrineAvailable = shrineAvailable;
		ItemCatalog = itemCatalog;
		ItemPolicy = itemPolicy;
		ItemPlayer = itemPlayer;
		InitialItemSlots = initialItemSlots;
		InitialActiveItemIds = initialActiveItemIds;
		InitialActiveItemCount = initialActiveItemCount;
		InitialUniqueItemFlags = initialUniqueItemFlags;
		InitialItemTiles = initialItemTiles;
		PotionDefinitions = potionDefinitions;
		MaximumPlacementAttempts = maximumPlacementAttempts;
		MaximumItemPlacementAttempts = maximumItemPlacementAttempts;
		InitialVisibleTransparencyRegions = initialVisibleTransparencyRegions;
	}

	public AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }
	public uint MonsterClearRandomState { get; }
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

	public static AuthoritativeNativeFullPopulationInputView FromCathedral(
		AuthoritativeCathedralFullPopulationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		return new(AuthoritativeNativeDungeonFamilyContext.Cathedral(request.InitialPopulation.Depth),
			request.MonsterClearRandomState, request.InitialMonsterTileRefs, request.InitialPlayerRefs,
			request.InitialTileFlags, request.InitialCorpses, request.InitialLight, request.InitialPreLight,
			request.LightAllocator, request.TriggerPositions, request.OrderedUniqueDefinitions,
			request.MonsterDensityDivisor, request.ShrineAvailable, request.ItemCatalog, request.ItemPolicy,
			request.ItemPlayer, request.InitialItemSlots, request.InitialActiveItemIds,
			request.InitialActiveItemCount, request.InitialUniqueItemFlags, request.InitialItemTiles,
			request.PotionDefinitions, request.MaximumPlacementAttempts,
			request.MaximumItemPlacementAttempts, request.InitialVisibleTransparencyRegions);
	}
}

/** Result shared by the family-specific outer full-population contracts. */
internal sealed record AuthoritativeNativeFullPopulationOutput(
	AuthoritativeCathedralFreshMonsterPool ClearedMonsterPool,
	AuthoritativeCathedralEncounterResult Encounters,
	AuthoritativeCathedralInitialItemResult InitialItems,
	AuthoritativeCathedralThemeRoomState BeforeThemeRooms,
	AuthoritativeCathedralThemeRoomResult ThemeRooms);

/** Shared values needed by family-specific level-ready composition. */
internal sealed class AuthoritativeNativeFreshLevelView
{
	public AuthoritativeNativeFreshLevelView(
		AuthoritativeNativeDungeonFamilyContext familyContext,
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralPopulationPlayer player,
		AuthoritativeCathedralBestiaryResult bestiary,
		IReadOnlyList<ushort> megaTiles,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState> visionRecords,
		IReadOnlyList<bool> visionActive,
		bool updateVision,
		int lightRadius,
		bool levelChanging,
		uint finalRandomState,
		uint monsterClearRandomState)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(bestiary);
		ArgumentNullException.ThrowIfNull(megaTiles);
		ArgumentNullException.ThrowIfNull(triggerPositions);
		ArgumentNullException.ThrowIfNull(visionRecords);
		ArgumentNullException.ThrowIfNull(visionActive);
		familyContext.RequireMatch(player.Level);
		FamilyContext = familyContext;
		State = state;
		Player = player;
		Bestiary = bestiary;
		MegaTiles = megaTiles;
		TriggerPositions = triggerPositions;
		VisionRecords = visionRecords;
		VisionActive = visionActive;
		UpdateVision = updateVision;
		LightRadius = lightRadius;
		LevelChanging = levelChanging;
		FinalRandomState = finalRandomState;
		MonsterClearRandomState = monsterClearRandomState;
	}

	public AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }
	public AuthoritativeCathedralThemeRoomState State { get; }
	public AuthoritativeCathedralPopulationPlayer Player { get; }
	public AuthoritativeCathedralBestiaryResult Bestiary { get; }
	public IReadOnlyList<ushort> MegaTiles { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> VisionRecords { get; }
	public IReadOnlyList<bool> VisionActive { get; }
	public bool UpdateVision { get; }
	public int LightRadius { get; }
	public bool LevelChanging { get; }
	public uint FinalRandomState { get; }
	public uint MonsterClearRandomState { get; }

	public static AuthoritativeNativeFreshLevelView FromCathedral(AuthoritativeCathedralFreshLevelResult fresh)
	{
		ArgumentNullException.ThrowIfNull(fresh);
		var initial = AuthoritativeNativeInitialPopulationView.FromCathedral(fresh.InitialPopulation,
			fresh.Population.InitialPopulation);
		return new(AuthoritativeNativeDungeonFamilyContext.Cathedral(fresh.Request.Depth),
			fresh.Population.State, initial.Player, initial.Bestiary,
			fresh.Geometry.GenerationResult.MegaTiles, fresh.TriggerPositions,
			fresh.VisionRecords, fresh.VisionActive, fresh.UpdateVision,
			fresh.Request.Hero.LightRadius, fresh.Request.Hero.LevelChanging,
			fresh.Population.FinalRandomState, fresh.MonsterClearFinalRandomState);
	}

	public static AuthoritativeNativeFreshLevelView FromCatacombs(AuthoritativeCatacombsFreshLevelResult fresh)
	{
		ArgumentNullException.ThrowIfNull(fresh);
		var initial = AuthoritativeNativeInitialPopulationView.FromCatacombs(fresh.InitialPopulation,
			fresh.Population.InitialPopulation);
		return new(AuthoritativeNativeDungeonFamilyContext.Catacombs(fresh.Request.Depth),
			fresh.Population.State, initial.Player, initial.Bestiary,
			fresh.Geometry.GenerationResult.MegaTiles, fresh.TriggerPositions,
			fresh.VisionRecords, fresh.VisionActive, fresh.UpdateVision,
			fresh.Request.Hero.LightRadius, fresh.Request.Hero.LevelChanging,
			fresh.Population.FinalRandomState, fresh.MonsterClearFinalRandomState);
	}
}
