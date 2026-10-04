using Devilution.Engine;

namespace Devilution.Diablo;

/** Native UniqueMonstersData pack modes. */
public enum AuthoritativeCathedralUniquePack
{
	None = 0,
	Independent = 1,
	Leashed = 2,
}

/** Detached native unique-monster row; native quest suppression is keyed by the source ID set. */
public sealed record AuthoritativeCathedralUniqueDefinition(
	int NativeUniqueId,
	int MonsterType,
	int DungeonLevel,
	int MaximumHitPoints,
	int Ai,
	int Intelligence,
	int MinDamage,
	int MaxDamage,
	int Resistance,
	int ReducePlayerStrength,
	int ReducePlayerMagic,
	int ReducePlayerDexterity,
	int ReducePlayerVitality,
	int ReducePlayerMaxHP,
	int ReducePlayerMaxMana,
	AuthoritativeCathedralUniquePack MonsterPack,
	int CustomToHit,
	int CustomArmorClass,
	int TalkMessage,
	bool QuestOnly);

/** Complete native LightManager::Light state for one light-array slot. */
public sealed record AuthoritativeCathedralEncounterLightState(
	int LightId,
	AuthoritativeCathedralPopulationPoint Tile,
	AuthoritativeCathedralPopulationPoint OldTile,
	int OffsetX,
	int OffsetY,
	int Radius,
	int OldRadius,
	bool IsInvalid,
	bool HasChanged);

/** Native LightManager active-light pool before InitMonsters. */
public sealed class AuthoritativeCathedralEncounterLightAllocator
{
	public const int MaximumLights = 32;

	public AuthoritativeCathedralEncounterLightAllocator(
		IReadOnlyList<int> activeLightSlots,
		int activeLightCount,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState>? lightRecords = null,
		bool updateLighting = false,
		bool disableLighting = false)
	{
		ArgumentNullException.ThrowIfNull(activeLightSlots);
		if (activeLightSlots.Count != MaximumLights || activeLightCount is < 0 or > MaximumLights)
			throw new InvalidDataException("The native light allocator requires all 32 slots and a valid active count.");
		if (activeLightSlots.Any(value => value is < 0 or >= MaximumLights)
			|| activeLightSlots.Distinct().Count() != MaximumLights)
			throw new InvalidDataException("Native light allocator slots must be a permutation of 0..31.");
		var records = lightRecords?.ToArray() ?? Enumerable.Range(0, MaximumLights)
			.Select(lightId => new AuthoritativeCathedralEncounterLightState(
				lightId, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false))
			.ToArray();
		if (records.Length != MaximumLights
			|| records.Any(record => record is null))
			throw new InvalidDataException("Native light records must contain all 32 indexed slots.");
		if (!records.Select(record => record.LightId).SequenceEqual(Enumerable.Range(0, MaximumLights))
			|| records.Any(record => record.LightId is < 0 or >= MaximumLights
				|| !IsBytePoint(record.Tile) || !IsBytePoint(record.OldTile)
				|| record.OffsetX is < sbyte.MinValue or > sbyte.MaxValue
				|| record.OffsetY is < sbyte.MinValue or > sbyte.MaxValue
				|| record.Radius is < 0 or > byte.MaxValue
				|| record.OldRadius is < 0 or > byte.MaxValue))
			throw new InvalidDataException("Native light records must contain all 32 indexed slots.");
		ActiveLightSlots = Array.AsReadOnly(activeLightSlots.ToArray());
		ActiveLightCount = activeLightCount;
		LightRecords = Array.AsReadOnly(records);
		UpdateLighting = updateLighting;
		DisableLighting = disableLighting;
	}

	public IReadOnlyList<int> ActiveLightSlots { get; }
	public int ActiveLightCount { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> LightRecords { get; }
	public bool UpdateLighting { get; }
	/** Native LightManager::disableLighting gate; a disabled AddLight consumes no slot. */
	public bool DisableLighting { get; }

	private static bool IsBytePoint(AuthoritativeCathedralPopulationPoint point)
		=> point.X is >= 0 and <= byte.MaxValue && point.Y is >= 0 and <= byte.MaxValue;
}

/** Legitimate pre-encounter state and external metadata for ordinary InitMonsters. */
public sealed class AuthoritativeCathedralEncounterParameters
{
	private const byte TileVisible = 1 << 1;
	private const byte TilePopulated = 1 << 3;
	private const byte TileLit = 1 << 6;
	private static readonly int[] QuestSuppressedUniqueIds = [0, 2, 3, 7, 8];

	public AuthoritativeCathedralEncounterParameters(
		AuthoritativeCathedralPopulationParameters population,
		AuthoritativeCathedralPopulationResult initialPopulation,
		IReadOnlyList<int> initialMonsterTileRefs,
		IReadOnlyList<bool> initialVisibility,
		IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
		IReadOnlyList<int> scatterLevelTypes,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition> orderedUniqueDefinitions,
		int monsterDensityDivisor,
		AuthoritativeCathedralEncounterLightAllocator lightAllocator,
		int maximumPlacementAttempts = 1_000_000,
		IReadOnlyList<bool>? initialVisibleTransparencyRegions = null,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState>? initialMonsterSlots = null,
		IReadOnlyList<int>? initialActiveMonsterIds = null,
		int? initialActiveMonsterCount = null,
		IReadOnlyList<bool>? initialLit = null,
		IReadOnlyList<byte>? initialTileFlags = null,
		AuthoritativeCathedralBestiaryResult? composedBestiary = null)
	{
		ArgumentNullException.ThrowIfNull(population);
		ArgumentNullException.ThrowIfNull(initialPopulation);
		ArgumentNullException.ThrowIfNull(initialMonsterTileRefs);
		ArgumentNullException.ThrowIfNull(initialVisibility);
		ArgumentNullException.ThrowIfNull(triggerPositions);
		ArgumentNullException.ThrowIfNull(scatterLevelTypes);
		ArgumentNullException.ThrowIfNull(orderedUniqueDefinitions);
		ArgumentNullException.ThrowIfNull(lightAllocator);
		if (monsterDensityDivisor <= 0 || maximumPlacementAttempts <= 0)
			throw new InvalidDataException("Encounter density and placement bounds must be positive.");
		if (population.OrderedBestiary.Any(monster => monster.NativeType == 137))
			throw new InvalidDataException("Selected NaKrul requires unsupported native uber-monster placement state.");
		if (initialPopulation.Monsters.Count > AuthoritativeCathedralPopulationParameters.MonsterCapacity
			|| initialMonsterTileRefs.Count != AuthoritativeCathedralPopulationGeometry.CellCount
			|| initialVisibility.Count != AuthoritativeCathedralPopulationGeometry.CellCount
			|| initialPopulation.TileObjectRefs.Count != AuthoritativeCathedralPopulationGeometry.CellCount
			|| initialPopulation.Pieces.Count != AuthoritativeCathedralPopulationGeometry.CellCount
			|| initialPopulation.Transparency.Count != AuthoritativeCathedralPopulationGeometry.CellCount)
			throw new InvalidDataException("Initial Cathedral encounter arrays have invalid dimensions.");
		if (initialMonsterTileRefs.Any(value => value is < -AuthoritativeCathedralPopulationParameters.MonsterCapacity
			or > AuthoritativeCathedralPopulationParameters.MonsterCapacity))
			throw new InvalidDataException("Initial monster tile references exceed the native pool bounds.");
		if (initialPopulation.Transparency.Any(value => value >= 128))
			throw new InvalidDataException("Negative nonzero native transparency regions are not safely representable.");
		if (initialVisibleTransparencyRegions is not null && initialVisibleTransparencyRegions.Count != 256)
			throw new InvalidDataException("Native visible transparency regions require all 256 entries.");
		var activeCount = initialActiveMonsterCount ?? initialPopulation.Monsters.Count;
		if (activeCount != initialPopulation.Monsters.Count || activeCount is < 0 or > AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			throw new InvalidDataException("Initial active monster count must match the managed population phase.");
		if (initialMonsterSlots is null)
			throw new InvalidDataException("The native pre-encounter 200-slot monster pool is required.");
		var slots = initialMonsterSlots.ToArray();
		if (slots.Length != AuthoritativeCathedralPopulationParameters.MonsterCapacity
			|| slots.Select((monster, index) => monster is null || monster.Slot != index).Any(value => value)
			|| slots.Any(monster => !Contains(monster.Tile)))
			throw new InvalidDataException("The complete native 200-slot monster pool is required.");
		if (slots.Take(activeCount).Zip(initialPopulation.Monsters).Any(pair => pair.First != pair.Second))
			throw new InvalidDataException("Managed active monsters must match the supplied native pool prefix.");
		var activeIds = initialActiveMonsterIds?.ToArray()
			?? Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity).ToArray();
		if (activeIds.Length != AuthoritativeCathedralPopulationParameters.MonsterCapacity
			|| activeIds.Any(value => value is < 0 or >= AuthoritativeCathedralPopulationParameters.MonsterCapacity)
			|| !activeIds.SequenceEqual(Enumerable.Range(0, AuthoritativeCathedralPopulationParameters.MonsterCapacity)))
			throw new InvalidDataException("Fresh ordinary encounters require the native identity active-monster index array.");
		for (var index = 0; index < activeCount; index++) {
			var monster = slots[index];
			if (monster.LevelType < 0 || monster.LevelType >= population.OrderedBestiary.Count
				|| monster.Type != population.OrderedBestiary[monster.LevelType].NativeType)
				throw new InvalidDataException("Active monster state does not match the supplied ordered bestiary.");
		}
		var seenMonsterRefs = new HashSet<int>();
		for (var index = 0; index < initialMonsterTileRefs.Count; index++) {
			var reference = initialMonsterTileRefs[index];
			if (reference == 0)
				continue;
			var slot = Math.Abs(reference) - 1;
			if (slot >= 0 && slot < activeCount
				&& (slots[slot].Tile == new AuthoritativeCathedralPopulationPoint(0, 0)
					|| (slots[slot].Type == AuthoritativeCathedralPopulationParameters.GolemNativeType
						&& slots[slot].Tile == new AuthoritativeCathedralPopulationPoint(1, 0))))
				throw new InvalidDataException("Off-map Golem and skeleton reservations must not occupy dungeon tile references.");
			if (slot < 0 || slot >= activeCount || !seenMonsterRefs.Add(slot)
				|| slots[slot].Tile != new AuthoritativeCathedralPopulationPoint(index % AuthoritativeCathedralPopulationGeometry.Width,
					index / AuthoritativeCathedralPopulationGeometry.Width))
				throw new InvalidDataException("Signed monster references must identify an active monster at its native tile.");
		}
		if (initialLit is not null && initialLit.Count != AuthoritativeCathedralPopulationGeometry.CellCount)
			throw new InvalidDataException("Native lit tile flags require the complete dungeon tile array.");
		if (initialTileFlags is not null && initialTileFlags.Count != AuthoritativeCathedralPopulationGeometry.CellCount)
			throw new InvalidDataException("Native tile flags require the complete dungeon tile array.");
		if (composedBestiary is not null) {
			if (composedBestiary.Entries.Count != population.OrderedBestiary.Count
				|| composedBestiary.Entries.Select(entry => entry.NativeType)
					.SequenceEqual(population.OrderedBestiary.Select(entry => entry.NativeType)) == false
				|| composedBestiary.Entries.Select((entry, index) => entry.Index != index
					|| !MatchesBestiaryEntry(entry.PopulationDefinition, population.OrderedBestiary[index])).Any(value => value))
				throw new InvalidDataException("The composed bestiary must match the managed population order.");
			var derivedScatterTypes = composedBestiary.Entries
				.Where(entry => (entry.PlaceFlags & AuthoritativeCathedralBestiaryGenerator.PlaceScatter) != 0)
				.Select(entry => entry.Index)
				.ToArray();
			if (scatterLevelTypes.Count != 0 && !scatterLevelTypes.SequenceEqual(derivedScatterTypes))
				throw new InvalidDataException("Scatter indexes must be derived from composed bestiary place flags.");
			scatterLevelTypes = derivedScatterTypes;
		}
		foreach (var trigger in triggerPositions)
			for (var x = -2; x < 2; x++)
				for (var y = -2; y < 2; y++)
					if (!Contains(new AuthoritativeCathedralPopulationPoint(trigger.X + x, trigger.Y + y)))
						throw new InvalidDataException("Trigger vision origins must be in dungeon bounds.");
		if (scatterLevelTypes.Any(type => type < 0 || type >= population.OrderedBestiary.Count)
			|| scatterLevelTypes.Distinct().Count() != scatterLevelTypes.Count)
			throw new InvalidDataException("Scatter level types must be unique ordered bestiary indexes.");
		var uniqueIds = new HashSet<int>();
		var previousUniqueId = -1;
		foreach (var definition in orderedUniqueDefinitions) {
			if (definition is null || !uniqueIds.Add(definition.NativeUniqueId)
				|| definition.NativeUniqueId <= previousUniqueId
				|| definition.NativeUniqueId is < 0 or > 99
				|| definition.MonsterType is < 0 or >= AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount
				|| definition.DungeonLevel is < 0 or > byte.MaxValue
				|| definition.MaximumHitPoints is < 0 or > ushort.MaxValue
				|| definition.Ai is < -1 or > 39
				|| (int)definition.MonsterPack is < (int)AuthoritativeCathedralUniquePack.None or > (int)AuthoritativeCathedralUniquePack.Leashed
				|| definition.CustomToHit is < 0 or > byte.MaxValue
				|| definition.CustomArmorClass is < 0 or > byte.MaxValue
				|| definition.Resistance is < 0 or > ushort.MaxValue
				|| definition.TalkMessage is < -1 or > short.MaxValue
				|| new[] {
					definition.Intelligence,
					definition.MinDamage,
					definition.MaxDamage,
					definition.ReducePlayerStrength,
					definition.ReducePlayerMagic,
					definition.ReducePlayerDexterity,
					definition.ReducePlayerVitality,
					definition.ReducePlayerMaxHP,
					definition.ReducePlayerMaxMana,
				}.Any(value => value is < 0 or > byte.MaxValue))
				throw new InvalidDataException("Invalid native unique-monster metadata.");
			if (definition.DungeonLevel == population.Depth
				&& (definition.NativeUniqueId is 4 or 5 or 6 or 9 or 12)
				&& population.OrderedBestiary.Any(monster => monster.NativeType == definition.MonsterType))
				throw new InvalidDataException("Selected Cathedral set-piece and uber unique placement prerequisites are not supplied.");
			previousUniqueId = definition.NativeUniqueId;
		}
		Population = population;
		InitialPopulation = initialPopulation;
		InitialMonsterSlots = Array.AsReadOnly(slots);
		InitialActiveMonsterIds = Array.AsReadOnly(activeIds);
		InitialActiveMonsterCount = activeCount;
		InitialMonsterTileRefs = Array.AsReadOnly(initialMonsterTileRefs.ToArray());
		var tileFlags = initialTileFlags?.ToArray() ?? new byte[AuthoritativeCathedralPopulationGeometry.CellCount];
		if (initialTileFlags is null) {
			for (var index = 0; index < tileFlags.Length; index++) {
				if (initialVisibility[index]) tileFlags[index] |= TileVisible;
				if (initialLit?[index] == true) tileFlags[index] |= TileLit;
				if (population.Geometry.Populated[index]) tileFlags[index] |= TilePopulated;
			}
		}
		if (tileFlags.Select(flags => (flags & TilePopulated) != 0)
			.Zip(population.Geometry.Populated)
			.Any(pair => pair.First != pair.Second))
			throw new InvalidDataException("Tile populated flags must match the supplied held-cell geometry.");
		var effectiveVisibility = tileFlags.Select(flags => (flags & TileVisible) != 0).ToArray();
		var effectiveLit = tileFlags.Select(flags => (flags & TileLit) != 0).ToArray();
		if (initialTileFlags is not null
			&& (!initialVisibility.SequenceEqual(effectiveVisibility)
				|| (initialLit is not null && !initialLit.SequenceEqual(effectiveLit))))
			throw new InvalidDataException("Explicit tile flags must match the visible and lit flag inputs.");
		InitialVisibility = Array.AsReadOnly(effectiveVisibility);
		InitialLit = Array.AsReadOnly(effectiveLit);
		InitialTileFlags = Array.AsReadOnly(tileFlags);
		TriggerPositions = Array.AsReadOnly(triggerPositions.ToArray());
		ScatterLevelTypes = Array.AsReadOnly(scatterLevelTypes.ToArray());
		OrderedUniqueDefinitions = Array.AsReadOnly(orderedUniqueDefinitions.ToArray());
		MonsterDensityDivisor = monsterDensityDivisor;
		LightAllocator = lightAllocator;
		MaximumPlacementAttempts = maximumPlacementAttempts;
		InitialVisibleTransparencyRegions = Array.AsReadOnly(
			(initialVisibleTransparencyRegions ?? new bool[256]).ToArray());
		ComposedBestiary = composedBestiary;
	}

	public AuthoritativeCathedralPopulationParameters Population { get; }
	public AuthoritativeCathedralPopulationResult InitialPopulation { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> InitialMonsterSlots { get; }
	public IReadOnlyList<int> InitialActiveMonsterIds { get; }
	public int InitialActiveMonsterCount { get; }
	public IReadOnlyList<int> InitialMonsterTileRefs { get; }
	public IReadOnlyList<bool> InitialVisibility { get; }
	public IReadOnlyList<bool> InitialLit { get; }
	public IReadOnlyList<byte> InitialTileFlags { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
	public IReadOnlyList<int> ScatterLevelTypes { get; }
	public IReadOnlyList<AuthoritativeCathedralUniqueDefinition> OrderedUniqueDefinitions { get; }
	public int MonsterDensityDivisor { get; }
	public AuthoritativeCathedralEncounterLightAllocator LightAllocator { get; }
	public int MaximumPlacementAttempts { get; }
	public IReadOnlyList<bool> InitialVisibleTransparencyRegions { get; }
	public AuthoritativeCathedralBestiaryResult? ComposedBestiary { get; }

	internal static bool IsQuestSuppressedUnique(int nativeUniqueId)
		=> Array.IndexOf(QuestSuppressedUniqueIds, nativeUniqueId) >= 0;

	private static bool MatchesBestiaryEntry(
		AuthoritativeCathedralPopulationMonsterDefinition left,
		AuthoritativeCathedralPopulationMonsterDefinition right)
		=> left.NativeType == right.NativeType
			&& left.IsSkeleton == right.IsSkeleton
			&& left.MinimumHitPoints == right.MinimumHitPoints
			&& left.MaximumHitPoints == right.MaximumHitPoints
			&& left.Ai == right.Ai
			&& left.Intelligence == right.Intelligence
			&& left.AbilityFlags == right.AbilityFlags
			&& left.MinDamage == right.MinDamage
			&& left.MaxDamage == right.MaxDamage
			&& left.MinDamageSpecial == right.MinDamageSpecial
			&& left.MaxDamageSpecial == right.MaxDamageSpecial
			&& left.ArmorClass == right.ArmorClass
			&& left.Resistance == right.Resistance
			&& left.ReducePlayerStrength == right.ReducePlayerStrength
			&& left.ReducePlayerMagic == right.ReducePlayerMagic
			&& left.ReducePlayerDexterity == right.ReducePlayerDexterity
			&& left.ReducePlayerVitality == right.ReducePlayerVitality
			&& left.ReducePlayerMaxHP == right.ReducePlayerMaxHP
			&& left.ReducePlayerMaxMana == right.ReducePlayerMaxMana
			&& left.Frames.SequenceEqual(right.Frames)
			&& left.Rates.SequenceEqual(right.Rates);

	private static bool Contains(AuthoritativeCathedralPopulationPoint position)
		=> position.X is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width
			&& position.Y is >= 0 and < AuthoritativeCathedralPopulationGeometry.Width;
}

/** Detached encounter output, retaining the native active monster and allocator state. */
public sealed class AuthoritativeCathedralEncounterResult
{
	internal AuthoritativeCathedralEncounterResult(
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsters,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsterSlots,
		IReadOnlyList<int> activeMonsterIds,
		int activeMonsterCount,
		IReadOnlyList<int> monsterTileRefs,
		IReadOnlyList<bool> visibility,
		IReadOnlyList<bool> lit,
		IReadOnlyList<byte> tileFlags,
		IReadOnlyList<bool> visibleTransparencyRegions,
		IReadOnlyList<int> activeLightSlots,
		int activeLightCount,
		IReadOnlyList<AuthoritativeCathedralEncounterLightState> lightRecords,
		bool updateLighting,
		bool disableLighting,
		IReadOnlyList<int> uniqueToHit,
		int totalMonsters,
		uint finalRandomState)
	{
		Monsters = Array.AsReadOnly(monsters.ToArray());
		MonsterSlots = Array.AsReadOnly(monsterSlots.ToArray());
		ActiveMonsterIds = Array.AsReadOnly(activeMonsterIds.ToArray());
		ActiveMonsterCount = activeMonsterCount;
		MonsterTileRefs = Array.AsReadOnly(monsterTileRefs.ToArray());
		Visibility = Array.AsReadOnly(visibility.ToArray());
		Lit = Array.AsReadOnly(lit.ToArray());
		TileFlags = Array.AsReadOnly(tileFlags.ToArray());
		VisibleTransparencyRegions = Array.AsReadOnly(visibleTransparencyRegions.ToArray());
		ActiveLightSlots = Array.AsReadOnly(activeLightSlots.ToArray());
		UniqueToHit = Array.AsReadOnly(uniqueToHit.ToArray());
		LightRecords = Array.AsReadOnly(lightRecords.ToArray());
		UpdateLighting = updateLighting;
		DisableLighting = disableLighting;
		ActiveLightCount = activeLightCount;
		TotalMonsters = totalMonsters;
		FinalRandomState = finalRandomState;
	}

	public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> Monsters { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> MonsterSlots { get; }
	public IReadOnlyList<int> ActiveMonsterIds { get; }
	public int ActiveMonsterCount { get; }
	public IReadOnlyList<int> MonsterTileRefs { get; }
	public IReadOnlyList<bool> Visibility { get; }
	public IReadOnlyList<bool> Lit { get; }
	public IReadOnlyList<byte> TileFlags { get; }
	public IReadOnlyList<bool> VisibleTransparencyRegions { get; }
	public IReadOnlyList<int> ActiveLightSlots { get; }
	public int ActiveLightCount { get; }
	public IReadOnlyList<AuthoritativeCathedralEncounterLightState> LightRecords { get; }
	public bool UpdateLighting { get; }
	public bool DisableLighting { get; }
	public IReadOnlyList<int> UniqueToHit { get; }
	public int TotalMonsters { get; }
	public uint FinalRandomState { get; }
}

/** Bounded pure port of the ordinary original-Diablo InitMonsters sequence. */
public static class AuthoritativeCathedralEncounterGenerator
{
	private const int Width = 112;
	private const int MonsterCapacity = AuthoritativeCathedralPopulationParameters.MonsterCapacity;
	private const int MaximumScatterMonsterCount = MonsterCapacity - 10;
	private const int VisionRadius = 15;
	private const int NoLight = -1;
	private const int HorkDemonUniqueId = 10;
	private const byte TileVisible = 1 << 1;
	private const byte TilePopulated = 1 << 3;
	private const byte TileLit = 1 << 6;
	private const uint AllowSpecial = 1U << 2;
	private const uint TargetsMonster = 1U << 4;
	private const uint NoEnemy = 1U << 10;
	private const int NormalGoal = 1;
	private const int InquiringGoal = 6;
	private const int StandMode = 0;
	private const int SpecialMeleeAttackMode = 7;
	private const int NoUnique = 255;
	private const int NoLeader = 255;
	private static readonly int[] DeltaX = [1, 0, -1, -1, -1, 0, 1, 1];
	private static readonly int[] DeltaY = [1, 1, 1, 0, -1, -1, -1, 0];

	public static AuthoritativeCathedralEncounterResult Generate(AuthoritativeCathedralEncounterParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		return new Encounter(parameters).Generate();
	}

	private sealed class Encounter
	{
		private readonly AuthoritativeCathedralEncounterParameters input;
		private readonly DeterministicLegacyRandom random;
		private readonly AuthoritativeCathedralPopulationMonsterState[] monsterSlots;
		private readonly int[] activeMonsterIds;
		private readonly int[] monsterTileRefs;
		private readonly int[] objectRefs;
		private readonly bool[] visibility;
		private readonly bool[] lit;
		private readonly byte[] tileFlags;
		private readonly bool[] visibleTransparencyRegions;
		private readonly int[] activeLightSlots;
		private readonly AuthoritativeCathedralEncounterLightState[] lightRecords;
		private readonly List<int> uniqueToHit;
		private int activeMonsterCount;
		private int totalMonsters = MonsterCapacity;
		private int activeLightCount;
		private int uniqueTranslation;
		private int placementAttempts;
		private bool updateLighting;
		private readonly bool disableLighting;

		public Encounter(AuthoritativeCathedralEncounterParameters input)
		{
			this.input = input;
			random = new(input.InitialPopulation.FinalRandomState);
			monsterSlots = input.InitialMonsterSlots.ToArray();
			activeMonsterIds = input.InitialActiveMonsterIds.ToArray();
			monsterTileRefs = input.InitialMonsterTileRefs.ToArray();
			objectRefs = input.InitialPopulation.TileObjectRefs.ToArray();
			visibility = input.InitialVisibility.ToArray();
			lit = input.InitialLit.ToArray();
			tileFlags = input.InitialTileFlags.ToArray();
			visibleTransparencyRegions = input.InitialVisibleTransparencyRegions.ToArray();
			activeLightSlots = input.LightAllocator.ActiveLightSlots.ToArray();
			lightRecords = input.LightAllocator.LightRecords.ToArray();
			uniqueToHit = Enumerable.Repeat(0, MonsterCapacity).ToList();
			activeMonsterCount = input.InitialActiveMonsterCount;
			activeLightCount = input.LightAllocator.ActiveLightCount;
			updateLighting = input.LightAllocator.UpdateLighting;
			disableLighting = input.LightAllocator.DisableLighting;
		}

		public AuthoritativeCathedralEncounterResult Generate()
		{
			ApplyTriggerVision();
			PlaceUniqueMonsters();
			PlaceScatterMonsters();
			RemoveTriggerVision();
			return new(
				monsterSlots.Take(activeMonsterCount).ToArray(),
				monsterSlots,
				activeMonsterIds,
				activeMonsterCount,
				monsterTileRefs,
				visibility,
				lit,
				tileFlags,
				visibleTransparencyRegions,
				activeLightSlots,
				activeLightCount,
				lightRecords,
				updateLighting,
				disableLighting,
				uniqueToHit,
				totalMonsters,
				random.State);
		}

		private void ApplyTriggerVision()
		{
			foreach (var trigger in input.TriggerPositions)
				for (var x = -2; x < 2; x++)
					for (var y = -2; y < 2; y++)
						DoVision(new(trigger.X + x, trigger.Y + y), VisionRadius);
		}

		private void RemoveTriggerVision()
		{
			foreach (var trigger in input.TriggerPositions)
				for (var x = -2; x < 2; x++)
					for (var y = -2; y < 2; y++)
						DoUnVision(new(trigger.X + x, trigger.Y + y), VisionRadius);
		}

		private void PlaceUniqueMonsters()
		{
			foreach (var definition in input.OrderedUniqueDefinitions) {
				if (AuthoritativeCathedralEncounterParameters.IsQuestSuppressedUnique(definition.NativeUniqueId)
					|| definition.DungeonLevel != input.Population.Depth)
					continue;
				var levelType = FindBestiaryType(definition.MonsterType);
				if (levelType < 0)
					continue;
				var position = GetUniqueMonsterPosition();
				PlaceUnique(definition, levelType, position);
			}
		}

		private int FindBestiaryType(int nativeType)
		{
			for (var index = 0; index < input.Population.OrderedBestiary.Count; index++)
				if (input.Population.OrderedBestiary[index].NativeType == nativeType)
					return index;
			return -1;
		}

		private AuthoritativeCathedralPopulationPoint GetUniqueMonsterPosition()
		{
			var sparseAttempts = 0;
			for (var attempt = 0; attempt < input.MaximumPlacementAttempts; attempt++) {
				var position = RandomCandidate();
				var availableAround = 0;
				for (var x = position.X - 3; x < position.X + 3; x++)
					for (var y = position.Y - 3; y < position.Y + 3; y++)
						if (Contains(x, y) && CanPlaceMonster(new(x, y)))
							availableAround++;
				// Native's continue is inside a do/while: it still evaluates the
				// bottom CanPlaceMonster condition before choosing another candidate.
				var canPlace = CanPlaceMonster(position);
				if (availableAround < 9 && ++sparseAttempts < 1000 && !canPlace)
					continue;
				if (canPlace)
					return position;
			}
			throw new InvalidDataException("Native unique-monster placement exhausted the configured safety bound.");
		}

		private void PlaceUnique(
			AuthoritativeCathedralUniqueDefinition definition,
			int levelType,
			AuthoritativeCathedralPopulationPoint position)
		{
			var slot = PlaceMonster(levelType, position);
			activeMonsterCount++;
			var monster = monsterSlots[slot];
			var maxHitPoints = Math.Max((definition.MaximumHitPoints << 6) / 2, 64);
			var lightId = definition.NativeUniqueId == HorkDemonUniqueId ? NoLight : AddLight(position, 3);
			monster = monster with {
				UniqueType = definition.NativeUniqueId,
				MaxHitPoints = maxHitPoints,
				HitPoints = maxHitPoints,
				Ai = definition.Ai,
				Intelligence = definition.Intelligence,
				MinDamage = definition.MinDamage,
				MaxDamage = definition.MaxDamage,
				MinDamageSpecial = definition.MinDamage,
				MaxDamageSpecial = definition.MaxDamage,
				Resistance = definition.Resistance,
				ReducePlayerStrength = definition.ReducePlayerStrength,
				ReducePlayerMagic = definition.ReducePlayerMagic,
				ReducePlayerDexterity = definition.ReducePlayerDexterity,
				ReducePlayerVitality = definition.ReducePlayerVitality,
				ReducePlayerMaxHP = definition.ReducePlayerMaxHP,
				ReducePlayerMaxMana = definition.ReducePlayerMaxMana,
				TalkMessage = definition.TalkMessage,
				LightId = lightId,
				UniqueTranslation = uniqueTranslation++,
				Goal = definition.TalkMessage == -1 ? monster.Goal : InquiringGoal,
				ArmorClass = definition.CustomArmorClass == 0 ? monster.ArmorClass : definition.CustomArmorClass,
			};
			uniqueToHit[slot] = definition.CustomToHit;
			monsterSlots[slot] = monster;
			if (definition.MonsterPack != AuthoritativeCathedralUniquePack.None)
				PlaceGroup(levelType, 8, slot, definition.MonsterPack == AuthoritativeCathedralUniquePack.Leashed);
			monster = monsterSlots[slot];
			if (monster.Ai != 12)
				monsterSlots[slot] = ChangeToStand(monster);
		}

		private void PlaceScatterMonsters()
		{
			if (activeMonsterCount > MaximumScatterMonsterCount)
				throw new InvalidDataException("Native unique population exceeds the ordinary scatter capacity.");
			var walkable = 0;
			for (var x = 16; x < 96; x++)
				for (var y = 16; y < 96; y++)
					if (!IsSolid(new(x, y)))
						walkable++;
			var count = walkable / input.MonsterDensityDivisor;
			if (activeMonsterCount + count > MaximumScatterMonsterCount)
				count = MaximumScatterMonsterCount - activeMonsterCount;
			totalMonsters = activeMonsterCount + count;
			if (input.ScatterLevelTypes.Count == 0)
				return;
			var noProgressAttempts = 0;
			while (activeMonsterCount < totalMonsters) {
				var typeRoll = RequireIndex(input.ScatterLevelTypes.Count, "scatter type");
				var groupSize = GetScatterGroupSize();
				var before = activeMonsterCount;
				PlaceGroup(input.ScatterLevelTypes[typeRoll], groupSize, null, false);
				if (activeMonsterCount == before && ++noProgressAttempts >= input.MaximumPlacementAttempts)
					throw new InvalidDataException("Native scatter placement made no progress within the configured safety bound.");
				if (activeMonsterCount != before)
					noProgressAttempts = 0;
			}
		}

		private int GetScatterGroupSize()
		{
			if (input.Population.Depth == 1 || random.GenerateRnd(2) == 0)
				return 1;
			if (input.Population.Depth == 2)
				return RequireIndex(2, "depth-two group size") + 2;
			// Preserve the native signed remainder before adding the minimum.
			return random.GenerateRnd(3) + 3;
		}

		private void PlaceGroup(int levelType, int requested, int? leaderSlot, bool leashed)
		{
			var placed = 0;
			var available = totalMonsters - activeMonsterCount;
			if (available < 0)
				throw new InvalidDataException("Native encounter target count is below the active monster count.");
			var count = Math.Min(requested, available);
			for (var retry = 0; retry < 10; retry++) {
				while (placed != 0) {
					activeMonsterCount--;
					placed--;
					monsterTileRefs[Index(monsterSlots[activeMonsterCount].Tile)] = 0;
				}
				var origin = leaderSlot is null ? RequireOpenRandomPosition() : AddDirection(monsterSlots[leaderSlot.Value].Tile, RequireIndex(8, "pack origin"));
				var x = origin.X;
				var y = origin.Y;
				var x1 = x;
				var y1 = y;
				var placedThisTry = 0;
				for (var tries = 0; placedThisTry < count && tries < 100; AdvanceGroupPosition(ref x, ref y)) {
					if (!CanPlaceMonster(new(x, y))
						|| !SameTransparency(new(x, y), new(x1, y1))
						|| (leashed && (Math.Abs(x - x1) >= 4 || Math.Abs(y - y1) >= 4))) {
						tries++;
						continue;
					}
					var slot = PlaceMonster(levelType, new(x, y));
					if (leaderSlot is not null) {
						var minion = monsterSlots[slot];
						var leader = monsterSlots[leaderSlot.Value];
						minion = minion with {
							MaxHitPoints = checked(minion.MaxHitPoints * 2),
							HitPoints = checked(minion.MaxHitPoints * 2),
							Intelligence = leader.Intelligence,
							Leader = leashed ? leader.Slot : minion.Leader,
							LeaderRelation = leashed ? 1 : minion.LeaderRelation,
							Ai = leashed ? leader.Ai : minion.Ai,
						};
						if (minion.Ai != 12)
							minion = ChangeToStand(minion);
						monsterSlots[slot] = minion;
					}
					activeMonsterCount++;
					placed++;
					placedThisTry++;
				}
				if (placed >= count)
					break;
			}
			if (leashed && leaderSlot is not null)
				monsterSlots[leaderSlot.Value] = monsterSlots[leaderSlot.Value] with { PackSize = placed };
		}

		private void AdvanceGroupPosition(ref int x, ref int y)
		{
			var horizontal = RequireIndex(8, "pack horizontal direction");
			var vertical = RequireIndex(8, "pack vertical direction");
			x += DeltaX[horizontal];
			// Preserve the native typo: the second direction's delta-X moves Y.
			y += DeltaX[vertical];
		}

		private static AuthoritativeCathedralPopulationPoint AddDirection(
			AuthoritativeCathedralPopulationPoint origin,
			int direction)
			=> new(origin.X + DeltaX[direction], origin.Y + DeltaY[direction]);

		private int PlaceMonster(int levelType, AuthoritativeCathedralPopulationPoint position)
		{
			if (activeMonsterCount >= MonsterCapacity)
				throw new InvalidDataException("Native monster pool capacity was exceeded.");
			var data = input.Population.OrderedBestiary[levelType];
			var direction = RequireIndex(8, "monster direction");
			var tickCounter = random.GenerateRnd(data.Rates[0] - 1);
			var currentFrame = random.GenerateRnd(data.Frames[0] - 1);
			var rawHp = data.MinimumHitPoints + random.RandomIntLessThan(data.MaximumHitPoints - data.MinimumHitPoints + 1);
			var hitPoints = Math.Max((rawHp << 6) / 2, 64);
			// InitMonster writes a subset of the native slot. In particular a
			// rolled-back group retains scratch, last/temp, translation and pack
			// fields until an actual native assignment replaces them.
			var monster = monsterSlots[activeMonsterCount] with {
				Slot = activeMonsterCount,
				Type = data.NativeType,
				LevelType = levelType,
				Mode = StandMode,
				Goal = NormalGoal,
				GoalVar1 = 0,
				GoalVar2 = 0,
				GoalVar3 = 0,
				PathCount = 0,
				Enemy = 0,
				ActiveForTicks = 0,
				WhoHit = 0,
				Tile = position,
				Future = position,
				Old = position,
				Direction = direction,
				Invalid = false,
				Ai = data.Ai,
				Intelligence = data.Intelligence,
				UniqueType = NoUnique,
				Leader = NoLeader,
				LeaderRelation = 0,
				TalkMessage = -1,
				LightId = NoLight,
				HitPoints = hitPoints,
				MaxHitPoints = hitPoints,
				AnimTicksPerFrame = data.Rates[0],
				AnimTickCounter = tickCounter,
				AnimNumberOfFrames = data.Frames[0],
				AnimCurrentFrame = currentFrame,
				RndItemSeed = random.AdvanceSeed(),
				AiSeed = random.AdvanceSeed(),
				MinDamage = data.MinDamage,
				MaxDamage = data.MaxDamage,
				MinDamageSpecial = data.MinDamageSpecial,
				MaxDamageSpecial = data.MaxDamageSpecial,
				ArmorClass = data.ArmorClass,
				Resistance = data.Resistance,
				Flags = data.AbilityFlags,
				ReducePlayerStrength = data.ReducePlayerStrength,
				ReducePlayerMagic = data.ReducePlayerMagic,
				ReducePlayerDexterity = data.ReducePlayerDexterity,
				ReducePlayerVitality = data.ReducePlayerVitality,
				ReducePlayerMaxHP = data.ReducePlayerMaxHP,
				ReducePlayerMaxMana = data.ReducePlayerMaxMana,
			};
			if (monster.Ai == 12)
				monster = monster with {
					AnimTicksPerFrame = data.Rates[5],
					AnimNumberOfFrames = data.Frames[5],
					AnimCurrentFrame = 0,
					Flags = monster.Flags | AllowSpecial,
					Mode = SpecialMeleeAttackMode,
				};
			monsterSlots[monster.Slot] = monster;
			uniqueToHit[monster.Slot] = 0;
			monsterTileRefs[Index(position)] = monster.Slot + 1;
			return monster.Slot;
		}

		private int AddLight(AuthoritativeCathedralPopulationPoint position, int radius)
		{
			if (disableLighting)
				return NoLight;
			if (activeLightCount >= activeLightSlots.Length)
				return NoLight;
			var lightId = activeLightSlots[activeLightCount++];
			lightRecords[lightId] = lightRecords[lightId] with {
				Tile = position,
				OffsetX = 0,
				OffsetY = 0,
				Radius = radius,
				IsInvalid = false,
				HasChanged = false,
			};
			updateLighting = true;
			return lightId;
		}

		private AuthoritativeCathedralPopulationPoint RequireOpenRandomPosition()
		{
			for (var attempt = 0; attempt < input.MaximumPlacementAttempts; attempt++) {
				var position = RandomCandidate();
				if (CanPlaceMonster(position))
					return position;
			}
			throw new InvalidDataException("Native random monster placement exhausted the configured safety bound.");
		}

		private AuthoritativeCathedralPopulationPoint RandomCandidate()
		{
			placementAttempts++;
			if (placementAttempts > input.MaximumPlacementAttempts)
				throw new InvalidDataException("Native encounter placement exhausted the configured safety bound.");
			// Native draws both coordinates even when INT_MIN produces -32.
			// CanPlaceMonster rejects that out-of-bounds candidate and retries.
			return new(random.GenerateRnd(80) + 16, random.GenerateRnd(80) + 16);
		}

		private int RandomAnimationFrame(int frameCount)
			=> random.GenerateRnd(frameCount - 1);

		private AuthoritativeCathedralPopulationMonsterState ChangeToStand(
			AuthoritativeCathedralPopulationMonsterState monster)
		{
			var data = input.Population.OrderedBestiary[monster.LevelType];
			return monster with {
				AnimTicksPerFrame = data.Rates[0],
				AnimNumberOfFrames = data.Frames[0],
				AnimCurrentFrame = RandomAnimationFrame(data.Frames[0]),
				Flags = monster.Flags & ~AllowSpecial,
				Mode = StandMode,
			};
		}

		private int RequireIndex(int limit, string name)
		{
			var value = random.GenerateRnd(limit);
			if (value < 0 || value >= limit)
				throw new InvalidDataException($"Native {name} RNG result is not representable.");
			return value;
		}

		private bool CanPlaceMonster(AuthoritativeCathedralPopulationPoint position)
		{
			if (!Contains(position))
				return false;
			var index = Index(position);
			return monsterTileRefs[index] == 0
				&& !input.Population.Geometry.PlayerOccupied[index]
				&& !visibility[index]
				&& (tileFlags[index] & TilePopulated) == 0
				&& objectRefs[index] == 0
				&& !IsSolid(position);
		}

		private bool SameTransparency(AuthoritativeCathedralPopulationPoint left, AuthoritativeCathedralPopulationPoint right)
			=> input.InitialPopulation.Transparency[Index(left)] == input.InitialPopulation.Transparency[Index(right)];

		private bool IsSolid(AuthoritativeCathedralPopulationPoint position)
		{
			var piece = input.InitialPopulation.Pieces[Index(position)];
			return (input.Population.Geometry.PieceProperties[piece] & 1) != 0;
		}

		private void DoVision(AuthoritativeCathedralPopulationPoint origin, int radius)
		{
			SetVisible(origin);
			foreach (var quadrant in Quadrants)
				for (var ray = 0; ray < VisionRays.Length; ray++) {
					var rayLength = radius - RayLengthAdjustments[ray];
					for (var pointIndex = 0; pointIndex < rayLength; pointIndex++) {
						var relative = VisionRays[ray][pointIndex];
						var point = new AuthoritativeCathedralPopulationPoint(
							origin.X + relative.X * quadrant.X,
							origin.Y + relative.Y * quadrant.Y);
						if (!Contains(point))
							break;
						if (relative.X > 0 && relative.Y > 0) {
							var adjacent1 = new AuthoritativeCathedralPopulationPoint(point.X - quadrant.X, point.Y);
							var adjacent2 = new AuthoritativeCathedralPopulationPoint(point.X, point.Y - quadrant.Y);
							if (!PassesLight(adjacent1) && !PassesLight(adjacent2))
								break;
						}
						SetVisible(point);
						if (!PassesLight(point))
							break;
						MarkTransparent(point);
					}
			}
		}

		private void MarkTransparent(AuthoritativeCathedralPopulationPoint position)
		{
			var transparency = input.InitialPopulation.Transparency[Index(position)];
			if (transparency != 0)
				visibleTransparencyRegions[transparency] = true;
		}

		private void DoUnVision(AuthoritativeCathedralPopulationPoint center, int radius)
		{
			var extent = radius + 2;
			for (var y = center.Y - extent; y <= center.Y + extent; y++)
				for (var x = center.X - extent; x <= center.X + extent; x++)
					if (Contains(x, y))
					{
						var index = Index(new(x, y));
						visibility[index] = false;
						lit[index] = false;
						tileFlags[index] = (byte)(tileFlags[index] & ~(TileVisible | TileLit));
					}
		}

		private bool PassesLight(AuthoritativeCathedralPopulationPoint position)
		{
			if (!Contains(position))
				return false;
			var piece = input.InitialPopulation.Pieces[Index(position)];
			return (input.Population.Geometry.PieceProperties[piece] & 2) == 0;
		}

		private void SetVisible(AuthoritativeCathedralPopulationPoint position)
		{
			if (Contains(position)) {
				var index = Index(position);
				visibility[index] = true;
				tileFlags[index] |= TileVisible;
			}
		}

		private static int Index(AuthoritativeCathedralPopulationPoint position)
		{
			if (!Contains(position))
				throw new InvalidDataException("Native encounter indexing escaped dungeon bounds.");
			return position.X + position.Y * Width;
		}

		private static bool Contains(AuthoritativeCathedralPopulationPoint position)
			=> Contains(position.X, position.Y);

		private static bool Contains(int x, int y)
			=> x is >= 0 and < Width && y is >= 0 and < Width;
	}

	private static readonly (int X, int Y)[] Quadrants = [(1, 1), (-1, 1), (1, -1), (-1, -1)];
	private static readonly int[] RayLengthAdjustments = [0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 4, 3, 2, 2, 2, 1, 1, 1, 0, 0, 0, 0];
	private static readonly (int X, int Y)[][] VisionRays = [
		[(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 0), (9, 0), (10, 0), (11, 0), (12, 0), (13, 0), (14, 0), (15, 0)],
		[(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 0), (8, 1), (9, 1), (10, 1), (11, 1), (12, 1), (13, 1), (14, 1), (15, 1)],
		[(1, 0), (2, 0), (3, 0), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (12, 2), (13, 2), (14, 2), (15, 2)],
		[(1, 0), (2, 0), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 2), (9, 2), (10, 2), (11, 2), (12, 2), (13, 3), (14, 3), (15, 3)],
		[(1, 0), (2, 1), (3, 1), (4, 1), (5, 1), (6, 2), (7, 2), (8, 2), (9, 3), (10, 3), (11, 3), (12, 3), (13, 4), (14, 4)],
		[(1, 0), (2, 1), (3, 1), (4, 1), (5, 2), (6, 2), (7, 3), (8, 3), (9, 3), (10, 4), (11, 4), (12, 4), (13, 5), (14, 5)],
		[(1, 0), (2, 1), (3, 1), (4, 2), (5, 2), (6, 3), (7, 3), (8, 3), (9, 4), (10, 4), (11, 5), (12, 5), (13, 6), (14, 6)],
		[(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 3), (7, 4), (8, 4), (9, 5), (10, 5), (11, 6), (12, 6), (13, 7)],
		[(1, 1), (2, 1), (3, 2), (4, 2), (5, 3), (6, 4), (7, 4), (8, 5), (9, 6), (10, 6), (11, 7), (12, 7), (12, 8), (13, 8)],
		[(1, 1), (2, 2), (3, 2), (4, 3), (5, 4), (6, 5), (7, 5), (8, 6), (9, 7), (10, 7), (10, 8), (11, 8), (12, 9)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 5), (7, 6), (8, 7), (9, 8), (10, 9), (11, 9), (11, 10)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 7), (8, 8), (9, 9), (10, 10), (11, 11)],
		[(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (5, 6), (6, 7), (7, 8), (8, 9), (9, 10), (9, 11), (10, 11)],
		[(1, 1), (2, 2), (2, 3), (3, 4), (4, 5), (5, 6), (5, 7), (6, 8), (7, 9), (7, 10), (8, 10), (8, 11), (9, 12)],
		[(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 6), (4, 7), (5, 8), (6, 9), (6, 10), (7, 11), (7, 12), (8, 12), (8, 13)],
		[(1, 1), (1, 2), (2, 3), (2, 4), (3, 5), (3, 6), (4, 7), (4, 8), (5, 9), (5, 10), (6, 11), (6, 12), (7, 13)],
		[(0, 1), (1, 2), (1, 3), (2, 4), (2, 5), (3, 6), (3, 7), (3, 8), (4, 9), (4, 10), (5, 11), (5, 12), (6, 13), (6, 14)],
		[(0, 1), (1, 2), (1, 3), (1, 4), (2, 5), (2, 6), (3, 7), (3, 8), (3, 9), (4, 10), (4, 11), (4, 12), (5, 13), (5, 14)],
		[(0, 1), (1, 2), (1, 3), (1, 4), (1, 5), (2, 6), (2, 7), (2, 8), (3, 9), (3, 10), (3, 11), (3, 12), (4, 13), (4, 14)],
		[(0, 1), (0, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (2, 8), (2, 9), (2, 10), (2, 11), (2, 12), (3, 13), (3, 14), (3, 15)],
		[(0, 1), (0, 2), (0, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (1, 9), (1, 10), (1, 11), (2, 12), (2, 13), (2, 14), (2, 15)],
		[(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (1, 8), (1, 9), (1, 10), (1, 11), (1, 12), (1, 13), (1, 14), (1, 15)],
		[(0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9), (0, 10), (0, 11), (0, 12), (0, 13), (0, 14), (0, 15)],
	];
}
