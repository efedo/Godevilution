using System.Security.Cryptography;
using System.Text;
using Devilution.Domain.Protocol;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Explicit entity/content identity assigned to one native monster slot. */
public sealed record AuthoritativeCathedralMonsterRuntimeBinding
{
	public AuthoritativeCathedralMonsterRuntimeBinding(int nativeSlot, uint entityId, uint contentMonsterId, uint levelId = 0)
	{
		if (nativeSlot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity)
			throw new ArgumentOutOfRangeException(nameof(nativeSlot));
		if (entityId == 0)
			throw new ArgumentOutOfRangeException(nameof(entityId));
		if (contentMonsterId == 0)
			throw new ArgumentOutOfRangeException(nameof(contentMonsterId));
		NativeSlot = nativeSlot;
		EntityId = entityId;
		ContentMonsterId = contentMonsterId;
		LevelId = levelId;
	}

	public int NativeSlot { get; }
	public uint EntityId { get; }
	public uint ContentMonsterId { get; }
	/** Runtime level identity supplied by the caller; zero remains a valid legacy identity. */
	public uint LevelId { get; }
}

/** Explicit reason a raw native row is retained but not installed as a live target. */
public enum AuthoritativeCathedralMonsterRuntimeUnsupportedReason
{
	None,
	EmptyHitPoints,
	InvalidHitPointRange,
	NonStandMode,
	MovingState,
	DormantGolem,
	PreSpawnSkeleton,
	Petrified,
	InvalidNativeState,
	UnsupportedGoalState,
	LowHitPoints,
	UnavailableSourceMetadata,
	GargoyleSpecial,
	InvalidNativeAi,
	DerivedRuntimeValueOutsideManagedBounds,
}

/** One detached active-prefix row, including unsupported native states. */
public sealed class AuthoritativeCathedralMonsterRuntimeRecord
{
	internal AuthoritativeCathedralMonsterRuntimeRecord(
		AuthoritativeCathedralMonsterRuntimeBinding binding,
		AuthoritativeCathedralPopulationMonsterState initialState,
		AuthoritativeNativeMonsterInitialization initialization,
		AuthoritativeCombatTarget? target,
		AuthoritativeCathedralMonsterRuntimeUnsupportedReason unsupportedReason)
	{
		Binding = binding;
		InitialState = initialState with { };
		Initialization = initialization;
		Target = target;
		UnsupportedReason = unsupportedReason;
	}

	public AuthoritativeCathedralMonsterRuntimeBinding Binding { get; }
	public AuthoritativeCathedralPopulationMonsterState InitialState { get; }
	public AuthoritativeNativeMonsterInitialization Initialization { get; }
	public AuthoritativeCombatTarget? Target { get; }
	public bool LiveTargetCreated => Target is not null;
	public AuthoritativeCathedralMonsterRuntimeUnsupportedReason UnsupportedReason { get; }
}

/**
 * Detached native-to-managed handoff.  It does not install actors, allocate
 * drops, mutate the source state, or advance an RNG; it only binds native
 * records to caller-owned identities and creates bounded cold Stand import
 * candidates, not installed/gameplay-ready actors. Active uniques require
 * complete caller-supplied definitions and stable identities before handoff.
 */
public sealed class AuthoritativeCathedralMonsterRuntimeAdapterResult
{
	internal AuthoritativeCathedralMonsterRuntimeAdapterResult(
		string metadataFingerprint,
		IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsterSlots,
		IReadOnlyList<int> activeMonsterIds,
		int activeMonsterCount,
		IReadOnlyList<bool> monsterPetrified,
		IReadOnlyList<AuthoritativeCathedralMonsterRuntimeRecord> records,
		Func<int, int, int, int, AuthoritativeCombatTarget> createPreSpawnTarget,
        IReadOnlyList<AuthoritativeNativeInitialSpecialStand> initialSpecialStandActors)
	{
		MetadataFingerprint = metadataFingerprint;
		MonsterSlots = Array.AsReadOnly(monsterSlots.Select(monster => monster with { }).ToArray());
		ActiveMonsterIds = Array.AsReadOnly(activeMonsterIds.ToArray());
		ActiveMonsterCount = activeMonsterCount;
		MonsterPetrified = Array.AsReadOnly(monsterPetrified.ToArray());
		Records = Array.AsReadOnly(records.ToArray());
        InitialSpecialStandActors = Array.AsReadOnly(initialSpecialStandActors.ToArray());
        foreach (var actor in InitialSpecialStandActors)
            actor.ValidateShape();
		this.createPreSpawnTarget = createPreSpawnTarget ?? throw new ArgumentNullException(nameof(createPreSpawnTarget));
	}

	private readonly Func<int, int, int, int, AuthoritativeCombatTarget> createPreSpawnTarget;

	public string MetadataFingerprint { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> MonsterSlots { get; }
	public IReadOnlyList<int> ActiveMonsterIds { get; }
	public int ActiveMonsterCount { get; }
	public IReadOnlyList<bool> MonsterPetrified { get; }
	public IReadOnlyList<AuthoritativeCathedralMonsterRuntimeRecord> Records { get; }
    /** Empty for legacy Stand-only imports; exact mutable cursors for explicit first-entry imports. */
    public IReadOnlyList<AuthoritativeNativeInitialSpecialStand> InitialSpecialStandActors { get; }

	internal AuthoritativeCombatTarget CreatePreSpawnTarget(int nativeSlot, int positionX, int positionY, int direction)
		=> createPreSpawnTarget(nativeSlot, positionX, positionY, direction);
}

public static class AuthoritativeCathedralMonsterRuntimeAdapter
{
	internal enum AdmissionPolicy
	{
		LegacyCathedral,
		CathedralFirstEntry,
		Catacombs,
	}

	private const string FingerprintDomain = "godevilution.cathedral-monster-runtime-adapter";
	private const string FingerprintVersion = "1.0.0";
	private const int NativeStandMode = 0;
	private const int NativeGolemType = AuthoritativeCathedralPopulationParameters.GolemNativeType;
	private const uint NativeFlagGolem = 1U << 5;
	private const uint NativeFlagKnockback = 1U << 7;
	private const uint NativeFlagBerserk = 1U << 11;
	private const int NativeNoLeader = byte.MaxValue;
	private static readonly int[] NativeSkeletonTypes = [8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27];
	private static readonly string[] NativeAiNames = [
		"Zombie", "Fat", "SkeletonMelee", "SkeletonRanged", "Scavenger", "Rhino", "GoatMelee", "GoatRanged",
		"Fallen", "Magma", "SkeletonKing", "Bat", "Gargoyle", "Butcher", "Succubus", "Sneak", "Storm",
		"FireMan", "Gharbad", "Acid", "AcidUnique", "Golem", "Zhar", "Snotspill", "Snake", "Counselor",
		"Mega", "Diablo", "Lazarus", "LazarusSuccubus", "Lachdanan", "Warlord", "FireBat", "Torchant",
		"HorkDemon", "Lich", "ArchLich", "Psychorb", "Necromorb", "BoneDemon",
	];

	/** Combines the ordered generation, unique, and runtime metadata identities. */
	public static string ComputeMetadataFingerprint(
		string generationPopulationFingerprint,
		string uniqueFingerprint,
		string runtimeFingerprint)
	{
		ValidateFingerprint(generationPopulationFingerprint, nameof(generationPopulationFingerprint));
		ValidateFingerprint(uniqueFingerprint, nameof(uniqueFingerprint));
		ValidateFingerprint(runtimeFingerprint, nameof(runtimeFingerprint));
		var canonical = string.Join('\n',
			FingerprintDomain,
			FingerprintVersion,
			$"generation-population:{generationPopulationFingerprint}",
			$"uniques:{uniqueFingerprint}",
			$"runtime:{runtimeFingerprint}");
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
	}

	public static string ComputeMetadataFingerprint(
		string generationPopulationFingerprint,
		string uniqueFingerprint,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog)
	{
		ArgumentNullException.ThrowIfNull(runtimeCatalog);
		return ComputeMetadataFingerprint(generationPopulationFingerprint, uniqueFingerprint, runtimeCatalog.Fingerprint);
	}

	public static AuthoritativeCathedralMonsterRuntimeAdapterResult AdaptWithDeterministicIds(
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
		string metadataFingerprint,
		uint entityIdBase,
		uint contentMonsterIdBase,
		DiabloDifficulty difficulty = DiabloDifficulty.Normal,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions = null,
		uint levelId = 0,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog = null,
		IReadOnlyDictionary<int, string>? uniqueIdentities = null,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition>? populationMonsters = null)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(runtimeCatalog);
		var bindings = BuildDeterministicBindings(state, entityIdBase, contentMonsterIdBase, levelId);
		return AdaptCore(state, runtimeCatalog, metadataFingerprint, bindings, difficulty, uniqueDefinitions, monsterTypeCatalog, uniqueIdentities, populationMonsters, AdmissionPolicy.LegacyCathedral);
	}

	public static AuthoritativeCathedralMonsterRuntimeAdapterResult AdaptWithBindings(
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
		string metadataFingerprint,
		IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> bindings,
		DiabloDifficulty difficulty = DiabloDifficulty.Normal,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions = null,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog = null,
		IReadOnlyDictionary<int, string>? uniqueIdentities = null,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition>? populationMonsters = null)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(runtimeCatalog);
		ArgumentNullException.ThrowIfNull(bindings);
		return AdaptCore(state, runtimeCatalog, metadataFingerprint, bindings, difficulty, uniqueDefinitions, monsterTypeCatalog, uniqueIdentities, populationMonsters, AdmissionPolicy.LegacyCathedral);
	}

    /** Explicit first-entry import; all legacy APIs retain their Stand-only admission policy. */
    public static AuthoritativeCathedralMonsterRuntimeAdapterResult AdaptLevelEntryWithBindings(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
        string metadataFingerprint,
        IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> bindings,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> populationMonsters,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions = null,
        AuthoritativeMonsterTypeCatalog? monsterTypeCatalog = null,
        IReadOnlyDictionary<int, string>? uniqueIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(populationMonsters);
        if (bindings.Any(binding => binding.LevelId == 0)
            || bindings.Select(binding => binding.LevelId).Distinct().Count() != 1)
            throw new InvalidDataException("First-entry actor import requires one explicit non-Town level.");
        return AdaptCore(state, runtimeCatalog, metadataFingerprint, bindings, difficulty,
            uniqueDefinitions, monsterTypeCatalog, uniqueIdentities, populationMonsters,
            AdmissionPolicy.CathedralFirstEntry);
    }

	internal static AuthoritativeCathedralMonsterRuntimeAdapterResult AdaptCatacombsWithBindings(
		int catacombsDepth,
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
		string metadataFingerprint,
		IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> bindings,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> populationMonsters,
		DiabloDifficulty difficulty = DiabloDifficulty.Normal,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions = null,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog = null,
		IReadOnlyDictionary<int, string>? uniqueIdentities = null)
	{
		if (catacombsDepth is < 5 or > 8)
			throw new ArgumentOutOfRangeException(nameof(catacombsDepth));
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(runtimeCatalog);
		ArgumentNullException.ThrowIfNull(bindings);
		ArgumentNullException.ThrowIfNull(populationMonsters);
		return AdaptCore(state, runtimeCatalog, metadataFingerprint, bindings, difficulty,
			uniqueDefinitions, monsterTypeCatalog, uniqueIdentities, populationMonsters,
			AdmissionPolicy.Catacombs, catacombsDepth);
	}

	private static AuthoritativeCathedralMonsterRuntimeAdapterResult AdaptCore(
		AuthoritativeCathedralThemeRoomState state,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
		string metadataFingerprint,
		IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> bindings,
		DiabloDifficulty difficulty,
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog,
		IReadOnlyDictionary<int, string>? uniqueIdentities,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition>? populationMonsters,
		AdmissionPolicy admissionPolicy,
		int catacombsDepth = 0)
	{
		ValidateFingerprint(metadataFingerprint, nameof(metadataFingerprint));
		if (difficulty != DiabloDifficulty.Normal)
			throw new InvalidDataException("The detached live-monster handoff only supports original Normal difficulty.");
		if (state.ActiveMonsterCount > state.ActiveMonsterIds.Count)
			throw new InvalidDataException("The active monster count exceeds the active-ID array.");
		if (bindings.Count != state.ActiveMonsterCount)
			throw new InvalidDataException("One identity binding is required for every active native monster slot.");
		if (populationMonsters is not null
			&& (populationMonsters.Count != AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount
				|| populationMonsters.Where((monster, index) => monster is null || monster.NativeMonsterId != index).Any()))
			throw new InvalidDataException("Native attack animation metadata requires the complete ordered bestiary.");

		var orderedSlots = state.ActiveMonsterIds.Take(state.ActiveMonsterCount).ToArray();
		ValidateGroupReferences(state, orderedSlots);
		var bindingBySlot = new Dictionary<int, AuthoritativeCathedralMonsterRuntimeBinding>();
		var entityIds = new HashSet<uint>();
		var contentIdTypes = new Dictionary<uint, int>();
		foreach (var binding in bindings)
		{
			var nativeType = state.MonsterSlots[binding.NativeSlot].Type;
			if (!orderedSlots.Contains(binding.NativeSlot)
				|| !bindingBySlot.TryAdd(binding.NativeSlot, binding)
				|| !entityIds.Add(binding.EntityId)
				|| (contentIdTypes.TryGetValue(binding.ContentMonsterId, out var existingType)
					&& existingType != nativeType))
				throw new InvalidDataException("Identity bindings must cover the active native slots exactly once with unique IDs.");
			contentIdTypes.TryAdd(binding.ContentMonsterId, nativeType);
		}
		if (bindingBySlot.Count != orderedSlots.Length)
			throw new InvalidDataException("Identity bindings do not cover the active native slots.");

		var uniqueById = BuildUniqueMap(uniqueDefinitions);
		var uniqueIdentityById = BuildUniqueIdentityMap(uniqueIdentities);
		if (monsterTypeCatalog is not null) {
			foreach (var nativeSlot in orderedSlots) {
				var nativeType = state.MonsterSlots[nativeSlot].Type;
				if (admissionPolicy != AdmissionPolicy.LegacyCathedral) {
                    var binding = bindingBySlot[nativeSlot];
                    if (!monsterTypeCatalog.TryGet(binding.ContentMonsterId, out var exact)
                        || exact.NativeMonsterId != (uint)nativeType
                        || monsterTypeCatalog.IsNativeMonsterIdAmbiguous((uint)nativeType)
                            && exact.NativeAi != GetNativeAi(state.MonsterSlots[nativeSlot].Ai))
                        throw new InvalidDataException($"First-entry monster slot {nativeSlot} has no exact stable native/AI binding.");
                } else if (!monsterTypeCatalog.TryGetNative((uint)nativeType, out _))
                    throw new InvalidDataException($"Monster type catalog has no unambiguous native row {nativeType}.");
			}
		}
		ValidateActiveUniqueInputs(state, orderedSlots, uniqueById, uniqueIdentityById, bindingBySlot, monsterTypeCatalog, admissionPolicy != AdmissionPolicy.LegacyCathedral);
		var records = new List<AuthoritativeCathedralMonsterRuntimeRecord>(orderedSlots.Length);
		foreach (var nativeSlot in orderedSlots)
		{
			var stateRow = state.MonsterSlots[nativeSlot] with { };
			var definition = runtimeCatalog.Get(stateRow.Type);
			var nativeAttackFrameCount = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Frames[2];
			var nativeAttackTicksPerFrame = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Rates[2];
			var nativeHitRecoveryFrameCount = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Frames[3];
			var nativeHitRecoveryTicksPerFrame = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Rates[3];
			var nativeWalkFrameCount = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Frames[1];
			var nativeWalkTicksPerFrame = populationMonsters is null ? 0 : populationMonsters[stateRow.Type].Rates[1];
			var nativeSpecialAttackFrameCount = admissionPolicy == AdmissionPolicy.Catacombs && populationMonsters is not null
				? populationMonsters[stateRow.Type].Frames[5]
				: 0;
			var nativeSpecialAttackTicksPerFrame = admissionPolicy == AdmissionPolicy.Catacombs && populationMonsters is not null
				? populationMonsters[stateRow.Type].Rates[5]
				: 0;
			var binding = bindingBySlot[nativeSlot];
			uniqueById.TryGetValue(stateRow.UniqueType, out var unique);
			var uniqueIdentity = unique is not null && uniqueIdentityById.TryGetValue(stateRow.UniqueType, out var suppliedIdentity)
				? suppliedIdentity
				: string.Empty;
			var initializationBinding = new AuthoritativeNativeMonsterBinding(
				stateRow.Slot,
				stateRow.Type,
				stateRow.UniqueType,
				binding.EntityId,
				binding.ContentMonsterId,
				binding.LevelId,
				uniqueIdentity);
			var initialization = new AuthoritativeNativeMonsterInitialization(
				metadataFingerprint,
				stateRow,
				definition.Treasure,
				definition.SelectionRegion,
				definition.AnimationFrameNum,
				definition.AnimationFrameNumSpecial,
				initializationBinding,
				state.MonsterPetrified[nativeSlot]) {
				NativeAttackFrameCount = nativeAttackFrameCount,
				NativeAttackTicksPerFrame = nativeAttackTicksPerFrame,
				CatacombsDepth = catacombsDepth,
				NativeHitRecoveryFrameCount = nativeHitRecoveryFrameCount,
				NativeHitRecoveryTicksPerFrame = nativeHitRecoveryTicksPerFrame,
				NativeWalkFrameCount = nativeWalkFrameCount,
				NativeWalkTicksPerFrame = nativeWalkTicksPerFrame,
				NativeSpecialAttackFrameCount = nativeSpecialAttackFrameCount,
				NativeSpecialAttackTicksPerFrame = nativeSpecialAttackTicksPerFrame,
			};
			initialization.Validate();
			initialization.ValidateTarget(
				binding.EntityId,
				binding.ContentMonsterId,
				binding.LevelId,
				unique is not null,
				uniqueIdentity);

			var reason = GetUnsupportedReason(stateRow, definition, state.MonsterPetrified[nativeSlot],
				admissionPolicy, populationMonsters?[stateRow.Type], unique, uniqueIdentity);
			AuthoritativeCombatTarget? target = null;
			if (reason == AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None) {
				try {
					var leaderEntityId = stateRow.LeaderRelation == 0
						? 0u
						: bindingBySlot[stateRow.Leader].EntityId;
					target = CreateTarget(stateRow, definition, binding, initialization, runtimeCatalog, difficulty, uniqueById, uniqueIdentityById, monsterTypeCatalog, leaderEntityId);
				} catch (ArgumentException) {
					reason = AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DerivedRuntimeValueOutsideManagedBounds;
				} catch (InvalidDataException) {
					reason = AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DerivedRuntimeValueOutsideManagedBounds;
				}
			}

			records.Add(new AuthoritativeCathedralMonsterRuntimeRecord(
				binding,
				stateRow,
				initialization,
				target,
				reason));
		}

		AuthoritativeCombatTarget CreatePreSpawnTarget(int nativeSlot, int positionX, int positionY, int direction)
		{
			var record = records.SingleOrDefault(value => value.Binding.NativeSlot == nativeSlot)
				?? throw new InvalidDataException("A linked native skeleton slot is outside the retained active pool.");
			if (record.UnsupportedReason != AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton
				|| record.Target is not null || !NativeSkeletonTypes.Contains(record.InitialState.Type)
				|| record.InitialState.Tile != default || record.InitialState.Future != default
				|| record.InitialState.Old != default || record.InitialState.Mode != NativeStandMode
				|| record.InitialState.Goal != 1 || record.InitialState.GoalVar1 != 0
				|| record.InitialState.GoalVar2 != 0 || record.InitialState.GoalVar3 != 0
				|| record.InitialState.UniqueType != 255 || state.MonsterRefs.Any(value =>
					value == nativeSlot + 1 || value == -nativeSlot - 1))
				throw new InvalidDataException("A native sarcophagus may activate only its exact pre-spawn skeleton reservation.");
			var linkedState = record.InitialState with {
				Tile = new(positionX, positionY),
				Future = new(positionX, positionY),
				Old = new(positionX, positionY),
				Direction = direction,
			};
			var binding = new AuthoritativeCathedralMonsterRuntimeBinding(
				nativeSlot, record.Binding.EntityId, record.Binding.ContentMonsterId, record.Binding.LevelId);
			var leaderEntityId = linkedState.LeaderRelation == 0
				? 0U
				: bindingBySlot[linkedState.Leader].EntityId;
			return CreateTarget(linkedState, runtimeCatalog.Get(linkedState.Type), binding,
				record.Initialization, runtimeCatalog, difficulty, uniqueById, uniqueIdentityById,
				monsterTypeCatalog, leaderEntityId);
		}

		return new AuthoritativeCathedralMonsterRuntimeAdapterResult(
			metadataFingerprint,
			state.MonsterSlots,
			state.ActiveMonsterIds,
			state.ActiveMonsterCount,
			state.MonsterPetrified,
			records,
			CreatePreSpawnTarget,
            records.Where(row => row.Target is not null && row.InitialState.Mode == 11)
                .Select(row => new AuthoritativeNativeInitialSpecialStand(row.Binding.NativeSlot,
                    row.Binding.EntityId, row.Binding.LevelId, row.InitialState.AnimNumberOfFrames,
                    row.InitialState.AnimTicksPerFrame, row.InitialState.AnimCurrentFrame,
                    row.InitialState.AnimTickCounter)).ToArray());
	}

	private static IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> BuildDeterministicBindings(
		AuthoritativeCathedralThemeRoomState state,
		uint entityIdBase,
		uint contentMonsterIdBase,
		uint levelId)
	{
		if (entityIdBase == 0)
			throw new ArgumentOutOfRangeException(nameof(entityIdBase));
		if (contentMonsterIdBase == 0)
			throw new ArgumentOutOfRangeException(nameof(contentMonsterIdBase));
		var bindings = new List<AuthoritativeCathedralMonsterRuntimeBinding>(state.ActiveMonsterCount);
		foreach (var nativeSlot in state.ActiveMonsterIds.Take(state.ActiveMonsterCount))
		{
			var nativeType = state.MonsterSlots[nativeSlot].Type;
			if (nativeType < 0
				|| uint.MaxValue - entityIdBase < (uint)nativeSlot
				|| uint.MaxValue - contentMonsterIdBase < (uint)nativeType)
				throw new InvalidDataException("Deterministic identity bases would overflow native IDs.");
			bindings.Add(new AuthoritativeCathedralMonsterRuntimeBinding(
				nativeSlot,
				entityIdBase + (uint)nativeSlot,
				contentMonsterIdBase + (uint)nativeType,
				levelId));
		}
		return bindings;
	}

	private static Dictionary<int, AuthoritativeCathedralUniqueDefinition> BuildUniqueMap(
		IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? definitions)
	{
		if (definitions is null)
			return new Dictionary<int, AuthoritativeCathedralUniqueDefinition>();
		var result = new Dictionary<int, AuthoritativeCathedralUniqueDefinition>();
		foreach (var definition in definitions)
		{
			if (definition is null
				|| definition.NativeUniqueId is < 0 or >= AuthoritativeCathedralUniqueMonsterCatalog.UniqueCatalogCount
				|| !result.TryAdd(definition.NativeUniqueId, definition))
				throw new InvalidDataException("Unique metadata must have distinct native IDs in the 0..99 range.");
		}
		return result;
	}

	private static Dictionary<int, string> BuildUniqueIdentityMap(
		IReadOnlyDictionary<int, string>? identities)
	{
		var result = new Dictionary<int, string>();
		var stableIdentities = new HashSet<string>(StringComparer.Ordinal);
		if (identities is null)
			return result;
		foreach (var pair in identities) {
			if (pair.Key is < 0 or >= AuthoritativeCathedralUniqueMonsterCatalog.UniqueCatalogCount
				|| string.IsNullOrWhiteSpace(pair.Value)
				|| pair.Value.Any(char.IsControl)
				|| !NativeTextLimits.IsIdentityWithinLimit(pair.Value)
				|| !result.TryAdd(pair.Key, pair.Value)
				|| !stableIdentities.Add(pair.Value))
				throw new InvalidDataException("Unique identities must be distinct, bounded native identities.");
		}
		return result;
	}

	private static void ValidateActiveUniqueInputs(
		AuthoritativeCathedralThemeRoomState state,
		IReadOnlyList<int> orderedSlots,
		IReadOnlyDictionary<int, AuthoritativeCathedralUniqueDefinition> uniqueDefinitions,
		IReadOnlyDictionary<int, string> uniqueIdentities,
		IReadOnlyDictionary<int, AuthoritativeCathedralMonsterRuntimeBinding> bindings,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog,
		bool levelEntry)
	{
		foreach (var nativeSlot in orderedSlots) {
			var row = state.MonsterSlots[nativeSlot];
			if (monsterTypeCatalog is not null) {
				if (levelEntry
                    ? !monsterTypeCatalog.TryGet(bindings[nativeSlot].ContentMonsterId, out var exact)
                        || exact.NativeMonsterId != (uint)row.Type
                    : !monsterTypeCatalog.TryGetNative((uint)row.Type, out var typeDefinition)
                        || typeDefinition.MonsterId != bindings[nativeSlot].ContentMonsterId)
					throw new InvalidDataException("The supplied monster type catalog does not match the bound stable content identity.");
			}
			if (row.UniqueType == byte.MaxValue)
				continue;
			if (!uniqueDefinitions.TryGetValue(row.UniqueType, out var unique)
				|| !uniqueIdentities.ContainsKey(row.UniqueType))
				throw new InvalidDataException($"Active unique monster {row.UniqueType} requires complete definition and stable identity metadata.");
			if (unique.MonsterType != row.Type)
				throw new InvalidDataException($"Unique monster {row.UniqueType} does not match native type {row.Type}.");
		}
	}

	private static AuthoritativeCathedralMonsterRuntimeUnsupportedReason GetUnsupportedReason(
		AuthoritativeCathedralPopulationMonsterState state,
		AuthoritativeCathedralMonsterRuntimeDefinition definition,
		bool initialAnimationPetrified,
		AdmissionPolicy admissionPolicy,
        AuthoritativeCathedralRawMonsterDefinition? nativeDefinition,
		AuthoritativeCathedralUniqueDefinition? unique,
		string uniqueIdentity)
	{
		if (state.Invalid)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.InvalidNativeState;
		if (definition.Name.Length == 0)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.UnavailableSourceMetadata;
		if (state.HitPoints <= 0)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.EmptyHitPoints;
		if ((state.HitPoints >> 6) <= 0)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.LowHitPoints;
		if (state.MaxHitPoints < state.HitPoints)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.InvalidHitPointRange;
		var supportedEmergence = admissionPolicy != AdmissionPolicy.LegacyCathedral && state.Mode == 11
            && state.Ai == 2 && NativeSkeletonTypes.Contains(state.Type)
            && state.UniqueType == byte.MaxValue && state.Tile != default
            && state.Old == state.Tile && (state.Flags & ((1U << 1) | (1U << 2))) == 0
            && nativeDefinition is not null
            && state.AnimNumberOfFrames is >= 1 and <= sbyte.MaxValue
            && state.AnimNumberOfFrames == nativeDefinition.Frames[5]
            && state.AnimTicksPerFrame is >= 1 and <= sbyte.MaxValue
            && definition.AnimationFrameNumSpecial == 0
            && state.AnimTicksPerFrame == Math.Max(1, nativeDefinition.Rates[5])
            && state.AnimCurrentFrame >= 0 && state.AnimCurrentFrame < state.AnimNumberOfFrames
            && state.AnimTickCounter >= 0 && state.AnimTickCounter < state.AnimTicksPerFrame;
		var supportedCatacombsRangedSkeletonStand = admissionPolicy == AdmissionPolicy.Catacombs
			&& state.Mode == 11 && state.Ai == 3 && NativeSkeletonTypes.Contains(state.Type)
			&& state.UniqueType == byte.MaxValue && state.Tile != default
			&& state.Future == state.Tile && state.Old == state.Tile
			&& (state.Flags & ((1U << 1) | (1U << 2))) == 0
			&& state.Goal == 1 && state.GoalVar1 == 0 && state.GoalVar2 == 0 && state.GoalVar3 == 0
			&& !initialAnimationPetrified && nativeDefinition is not null && nativeDefinition.Ai == 3
			&& nativeDefinition.Frames[5] is >= 1 and <= sbyte.MaxValue
			&& nativeDefinition.Rates[5] is >= 1 and <= sbyte.MaxValue
			&& definition.AnimationFrameNumSpecial == 0
			&& state.AnimNumberOfFrames == nativeDefinition.Frames[5]
			&& state.AnimTicksPerFrame == nativeDefinition.Rates[5]
			&& state.AnimCurrentFrame >= 0 && state.AnimCurrentFrame < state.AnimNumberOfFrames
			&& state.AnimTickCounter >= 0 && state.AnimTickCounter < state.AnimTicksPerFrame;
		var supportedCatacombsGargoyle = admissionPolicy == AdmissionPolicy.Catacombs
			&& state.Mode == 7 && state.Ai == 12 && state.UniqueType == byte.MaxValue
			&& (state.Flags & (1U << 2)) != 0
			&& (state.Flags & ~(1U << 2 | 1U << 9)) == 0
			&& state.Tile != default && state.Future == state.Tile && state.Old == state.Tile
			&& state.Goal == 1 && state.GoalVar1 == 0 && state.GoalVar2 == 0 && state.GoalVar3 == 0
			&& !initialAnimationPetrified && nativeDefinition is not null && nativeDefinition.Ai == 12
			&& nativeDefinition.Frames[5] is >= 1 and <= sbyte.MaxValue
			&& nativeDefinition.Rates[5] is >= 1 and <= sbyte.MaxValue
			&& state.AnimNumberOfFrames == nativeDefinition.Frames[5]
			&& state.AnimTicksPerFrame == nativeDefinition.Rates[5]
			&& state.AnimCurrentFrame == 0 && state.AnimTickCounter == 0;
		if (state.Mode != NativeStandMode && !supportedEmergence
			&& !supportedCatacombsRangedSkeletonStand && !supportedCatacombsGargoyle)
            return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.NonStandMode;
		if (state.Future != state.Tile)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.MovingState;
		if (initialAnimationPetrified)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.Petrified;
		if (state.Goal != 1 || state.GoalVar1 != 0 || state.GoalVar2 != 0 || state.GoalVar3 != 0)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.UnsupportedGoalState;
		if (!TryGetNativeAi(state.Ai, out _))
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.InvalidNativeAi;
		if (state.AnimCurrentFrame < 0 || state.AnimTickCounter < 0)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DerivedRuntimeValueOutsideManagedBounds;
		if (state.Type == NativeGolemType)
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem;
		if (state.Tile == default && NativeSkeletonTypes.Contains(state.Type))
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton;
		var trustedCatacombsUniqueAiOverride = HasTrustedCatacombsUniqueAiOverride(
			state, unique, uniqueIdentity, admissionPolicy);
		if (!supportedCatacombsGargoyle
			&& ((TryGetNativeAi(state.Ai, out var nativeAi) && nativeAi == "Gargoyle")
				|| !trustedCatacombsUniqueAiOverride
					&& (definition.StableSymbol.Contains("Gargoyle", StringComparison.OrdinalIgnoreCase)
						|| definition.Name.Contains("Gargoyle", StringComparison.OrdinalIgnoreCase))))
			return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.GargoyleSpecial;
		return AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None;
	}

	private static bool HasTrustedCatacombsUniqueAiOverride(
		AuthoritativeCathedralPopulationMonsterState state,
		AuthoritativeCathedralUniqueDefinition? unique,
		string uniqueIdentity,
		AdmissionPolicy admissionPolicy)
		=> admissionPolicy == AdmissionPolicy.Catacombs
			&& unique is not null
			&& !string.IsNullOrWhiteSpace(uniqueIdentity)
			&& unique.MonsterType == state.Type
			&& unique.Ai == state.Ai
			&& TryGetNativeAi(unique.Ai, out var uniqueAi)
			&& uniqueAi != "Gargoyle";

	private static AuthoritativeCombatTarget CreateTarget(
		AuthoritativeCathedralPopulationMonsterState state,
		AuthoritativeCathedralMonsterRuntimeDefinition definition,
		AuthoritativeCathedralMonsterRuntimeBinding binding,
		AuthoritativeNativeMonsterInitialization initialization,
		AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
		DiabloDifficulty difficulty,
		IReadOnlyDictionary<int, AuthoritativeCathedralUniqueDefinition> uniqueDefinitions,
		IReadOnlyDictionary<int, string> uniqueIdentities,
		AuthoritativeMonsterTypeCatalog? monsterTypeCatalog,
		uint leaderEntityId)
	{
		uniqueDefinitions.TryGetValue(state.UniqueType, out var unique);
		var level = runtimeCatalog.GetEffectiveLevel(state.Type, difficulty, unique);
		// The raw MonsterData experience is imported here. Unique XP doubling
		// belongs to the native defeat/drop path, not actor initialization.
		var experience = (uint)definition.Experience;
		var toHit = runtimeCatalog.GetEffectiveToHit(state.Type, difficulty, unique: unique);
		var toHitSpecial = runtimeCatalog.GetEffectiveToHit(state.Type, difficulty, special: true, unique: unique);
		var nativeAi = GetNativeAi(state.Ai);
		var uniqueIdentity = unique is not null && uniqueIdentities.TryGetValue(state.UniqueType, out var suppliedIdentity)
			? suppliedIdentity
			: string.Empty;
		var fallenStandFrame = nativeAi == "Fallen" ? state.AnimCurrentFrame : 0;
		var fallenStandTick = nativeAi == "Fallen" ? state.AnimTickCounter : 0;
        var typeDefinition = monsterTypeCatalog is not null
            && monsterTypeCatalog.TryGet(binding.ContentMonsterId, out var exactType)
            && exactType.NativeMonsterId == (uint)state.Type
            ? exactType
            : monsterTypeCatalog is not null
                && monsterTypeCatalog.TryGetNative((uint)state.Type, out var resolvedType)
                ? resolvedType
                : null;
		if (level > int.MaxValue
			|| experience > NativeCompatibilityLimits.MaximumNativeMonsterExperienceReward
			|| toHit > NativeCompatibilityLimits.MaximumNativeMonsterToHit
			|| toHitSpecial > NativeCompatibilityLimits.MaximumNativeMonsterToHit)
			throw new InvalidDataException("Native runtime values exceed the managed live-target contract.");

		var target = new AuthoritativeCombatTarget(
			binding.EntityId,
			state.Tile.X,
			state.Tile.Y,
			hitPoints: 0,
			armorClass: state.ArmorClass,
			maxHitPoints: 0,
			monsterId: binding.ContentMonsterId,
			levelId: binding.LevelId,
			attackDamage: state.MaxDamage,
			fireResistance: GetResistancePercent(state.Resistance, 1 << 1, 1 << 4),
			lightningResistance: GetResistancePercent(state.Resistance, 1 << 2, 1 << 5),
			magicResistance: GetResistancePercent(state.Resistance, 1 << 0, 1 << 3),
			acidResistance: (state.Resistance & (1 << 7)) != 0 ? 100 : 0,
			monsterLevel: checked((int)level),
			futurePositionX: state.Future.X,
			futurePositionY: state.Future.Y,
			minDamage: state.MinDamage,
			maxDamage: state.MaxDamage,
			minDamageSpecial: state.MinDamageSpecial,
			maxDamageSpecial: state.MaxDamageSpecial,
			unique: unique is not null,
			uniqueIdentity: uniqueIdentity,
			dialogueMessageId: state.UniqueType == byte.MaxValue || state.TalkMessage < 0 ? 0 : state.TalkMessage,
			stoneCurseImmune: typeDefinition?.StoneCurseImmune ?? false,
			undead: definition.MonsterClass == 0,
			playerMinion: (state.Flags & NativeFlagGolem) != 0 && (state.Flags & NativeFlagBerserk) == 0,
			berserkImmune: typeDefinition?.BerserkImmune ?? false,
			demon: definition.MonsterClass == 1,
			animal: definition.MonsterClass == 2,
			knockback: (state.Flags & NativeFlagKnockback) != 0,
			experienceReward: experience,
			toHit: checked((int)toHit),
			toHitSpecial: checked((int)toHitSpecial),
			nativeAi: nativeAi,
			intelligence: state.Intelligence,
			direction: state.Direction,
			initialStandAnimationFrame: fallenStandFrame,
			initialStandAnimationTick: fallenStandTick,
			leaderEntityId: leaderEntityId,
			leaderRelation: (AuthoritativeMonsterLeaderRelation)state.LeaderRelation,
			packSize: state.PackSize,
			activeForTicks: state.ActiveForTicks);
		target.RestoreHitPointsQ6(state.HitPoints, state.MaxHitPoints);
		target.RestoreNativeInitialization(initialization, state.RndItemSeed, state.AiSeed);
		target.IsBerserked = (state.Flags & NativeFlagBerserk) != 0;
		return target;
	}

	private static void ValidateGroupReferences(
		AuthoritativeCathedralThemeRoomState state,
		IReadOnlyList<int> orderedSlots)
	{
		var activeSlots = orderedSlots.ToHashSet();
		foreach (var slot in orderedSlots) {
			var row = state.MonsterSlots[slot];
			if (row.LeaderRelation is < 0 or > 2)
				throw new InvalidDataException("Monster leader relation is outside the native enum.");
			if (row.LeaderRelation == 0) {
				if (row.Leader != NativeNoLeader)
					throw new InvalidDataException("An individual monster must retain native NoLeader.");
				continue;
			}
			if (row.Leader is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
				|| row.Leader == row.Slot
				|| !activeSlots.Contains(row.Leader))
				throw new InvalidDataException("A monster-pack relation must reference another active slot.");
		}
	}

	private static int GetResistancePercent(int resistance, int resistBit, int immuneBit)
		=> (resistance & immuneBit) != 0 ? 100 : (resistance & resistBit) != 0 ? 75 : 0;

	private static bool TryGetNativeAi(int ai, out string name)
	{
		if (ai < 0 || ai >= NativeAiNames.Length) {
			name = string.Empty;
			return false;
		}
		name = NativeAiNames[ai];
		return AuthoritativeMonsterTypeCatalog.IsValidNativeAi(name);
	}

	internal static string GetNativeAi(int ai)
	{
		if (!TryGetNativeAi(ai, out var name))
			throw new InvalidDataException($"Native MonsterAIID {ai} has no validated managed identity.");
		return name;
	}

	private static void ValidateFingerprint(string value, string parameterName)
	{
		if (!AuthoritativeNativeMonsterInitialization.IsFingerprint(value))
			throw new ArgumentException("Metadata fingerprints must be uppercase SHA-256 values.", parameterName);
	}
}
