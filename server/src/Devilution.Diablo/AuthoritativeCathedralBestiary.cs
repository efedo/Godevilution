using Devilution.Engine;

namespace Devilution.Diablo;

/** Native MonsterData availability values used by IsMonsterAvailable. */
public enum AuthoritativeCathedralMonsterAvailability
{
	Never = 0,
	Always = 1,
	Retail = 2,
}

/**
 * Detached raw MonstersData plus the level-selection metadata consumed by
 * GetLevelMTypes. The six animation slots retain the raw native values;
 * Bestiary::addType ignores slot five when hasSpecial is false.
 */
public sealed record AuthoritativeCathedralRawMonsterDefinition
{
	public AuthoritativeCathedralRawMonsterDefinition(
		int nativeMonsterId,
		AuthoritativeCathedralMonsterAvailability availability,
		int image,
		int minDungeonLevel,
		int maxDungeonLevel,
		bool isSkeleton,
		int minimumHitPoints,
		int maximumHitPoints,
		int ai,
		int intelligence,
		uint abilityFlags,
		int minDamage,
		int maxDamage,
		int minDamageSpecial,
		int maxDamageSpecial,
		int armorClass,
		int resistance,
		int reducePlayerStrength,
		int reducePlayerMagic,
		int reducePlayerDexterity,
		int reducePlayerVitality,
		int reducePlayerMaxHP,
		int reducePlayerMaxMana,
		bool hasSpecial,
		int width,
		IReadOnlyList<int> frames,
		IReadOnlyList<int> rates)
	{
		if (nativeMonsterId is < 0 or >= RawCatalogCount)
			throw new InvalidDataException("Native monster IDs must be in the complete 0..137 catalog.");
		if (isSkeleton != IsSkeletonType(nativeMonsterId))
			throw new InvalidDataException("Native skeleton metadata does not match the source monster ID.");
		if (!Enum.IsDefined(availability))
			throw new InvalidDataException("Unknown native monster availability.");
		if (image is < 0 or > ushort.MaxValue
			|| minDungeonLevel is < 0 or > sbyte.MaxValue
			|| maxDungeonLevel < minDungeonLevel
			|| maxDungeonLevel > sbyte.MaxValue)
			throw new InvalidDataException("Invalid native monster level or image metadata.");
		if (width is < 0 or > ushort.MaxValue)
			throw new InvalidDataException("Native monster sprite width is outside uint16 range.");
		if (minimumHitPoints is < 0 or > ushort.MaxValue
			|| maximumHitPoints < minimumHitPoints
			|| maximumHitPoints > ushort.MaxValue
			|| ai is < -1 or > 39
			|| abilityFlags > ushort.MaxValue
			|| new[] {
				intelligence,
				minDamage,
				maxDamage,
				minDamageSpecial,
				maxDamageSpecial,
				armorClass,
				resistance,
				reducePlayerStrength,
				reducePlayerMagic,
				reducePlayerDexterity,
				reducePlayerVitality,
				reducePlayerMaxHP,
				reducePlayerMaxMana,
			}.Any(value => value is < 0 or > byte.MaxValue))
			throw new InvalidDataException("Native monster population metadata is outside its source ranges.");
		Frames = CopyAnimation(frames, nameof(frames));
		Rates = CopyAnimation(rates, nameof(rates));
		var selectedFrames = Frames.Select((value, index) => hasSpecial || index < 5 ? value : 0).ToArray();
		var selectedRates = Rates.Select((value, index) => (hasSpecial || index < 5) && selectedFrames[index] != 0 ? value : 0).ToArray();
		if (selectedFrames.Any(value => value is < 0 or > 127)
			|| selectedRates.Any(value => value is < 0 or > 127))
			throw new InvalidDataException("Active native monster animation values must be nonnegative int8 values.");

		NativeMonsterId = nativeMonsterId;
		Availability = availability;
		Image = image;
		MinDungeonLevel = minDungeonLevel;
		MaxDungeonLevel = maxDungeonLevel;
		HasSpecial = hasSpecial;
		Width = width;
		PopulationDefinition = new AuthoritativeCathedralPopulationMonsterDefinition(
			nativeMonsterId,
			isSkeleton,
			minimumHitPoints,
			maximumHitPoints,
			ai,
			intelligence,
			abilityFlags,
			minDamage,
			maxDamage,
			minDamageSpecial,
			maxDamageSpecial,
			armorClass,
			resistance,
			reducePlayerStrength,
			reducePlayerMagic,
			reducePlayerDexterity,
			reducePlayerVitality,
			reducePlayerMaxHP,
			reducePlayerMaxMana,
			Array.AsReadOnly(selectedFrames),
			Array.AsReadOnly(selectedRates));
	}

	public const int RawCatalogCount = 138;

	public int NativeMonsterId { get; init; }
	public AuthoritativeCathedralMonsterAvailability Availability { get; init; }
	public int Image { get; init; }
	public int MinDungeonLevel { get; init; }
	public int MaxDungeonLevel { get; init; }
	public bool IsSkeleton => PopulationDefinition.IsSkeleton;
	public int MinimumHitPoints => PopulationDefinition.MinimumHitPoints;
	public int MaximumHitPoints => PopulationDefinition.MaximumHitPoints;
	public int Ai => PopulationDefinition.Ai;
	public int Intelligence => PopulationDefinition.Intelligence;
	public uint AbilityFlags => PopulationDefinition.AbilityFlags;
	public int MinDamage => PopulationDefinition.MinDamage;
	public int MaxDamage => PopulationDefinition.MaxDamage;
	public int MinDamageSpecial => PopulationDefinition.MinDamageSpecial;
	public int MaxDamageSpecial => PopulationDefinition.MaxDamageSpecial;
	public int ArmorClass => PopulationDefinition.ArmorClass;
	public int Resistance => PopulationDefinition.Resistance;
	public int ReducePlayerStrength => PopulationDefinition.ReducePlayerStrength;
	public int ReducePlayerMagic => PopulationDefinition.ReducePlayerMagic;
	public int ReducePlayerDexterity => PopulationDefinition.ReducePlayerDexterity;
	public int ReducePlayerVitality => PopulationDefinition.ReducePlayerVitality;
	public int ReducePlayerMaxHP => PopulationDefinition.ReducePlayerMaxHP;
	public int ReducePlayerMaxMana => PopulationDefinition.ReducePlayerMaxMana;
	public bool HasSpecial { get; init; }
	public int Width { get; init; }
	public IReadOnlyList<int> Frames { get; init; }
	public IReadOnlyList<int> Rates { get; init; }
	public AuthoritativeCathedralPopulationMonsterDefinition PopulationDefinition { get; }

	public AuthoritativeCathedralPopulationMonsterDefinition ToPopulationDefinition()
		=> PopulationDefinition with {
			Frames = Array.AsReadOnly(PopulationDefinition.Frames.ToArray()),
			Rates = Array.AsReadOnly(PopulationDefinition.Rates.ToArray()),
		};

	private static IReadOnlyList<int> CopyAnimation(IReadOnlyList<int> values, string name)
	{
		ArgumentNullException.ThrowIfNull(values, name);
		if (values.Count != 6 || values.Any(value => value is < -128 or > 127))
			throw new InvalidDataException($"Native monster {name} must contain six int8 values.");
		return Array.AsReadOnly(values.ToArray());
	}

	private static bool IsSkeletonType(int nativeMonsterId)
		=> nativeMonsterId is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27;
}

/** Pure GetLevelMTypes prerequisites for an ordinary single-player Cathedral. */
public sealed class AuthoritativeCathedralBestiaryParameters
{
	public AuthoritativeCathedralBestiaryParameters(
		int depth,
		uint populationSeed,
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> catalog)
	{
		if (depth is < 1 or > 4)
			throw new InvalidDataException("Cathedral bestiary selection requires depth 1..4.");
		ArgumentNullException.ThrowIfNull(catalog);
		if (catalog.Count != AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount)
			throw new InvalidDataException("The complete native 0..137 monster catalog in source order is required.");
		if (catalog.Any(value => value is null))
			throw new InvalidDataException("The native monster catalog cannot contain null entries.");
		var detached = catalog.Select(CloneValidated).ToArray();
		if (!detached.Select(value => value.NativeMonsterId).SequenceEqual(Enumerable.Range(0, detached.Length)))
			throw new InvalidDataException("The complete native 0..137 monster catalog in source order is required.");
		Depth = depth;
		PopulationSeed = populationSeed;
		Catalog = Array.AsReadOnly(detached);
	}

	public int Depth { get; }
	public uint PopulationSeed { get; }
	public IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> Catalog { get; }

	private static AuthoritativeCathedralRawMonsterDefinition CloneValidated(AuthoritativeCathedralRawMonsterDefinition value)
		=> new(
			value.NativeMonsterId,
			value.Availability,
			value.Image,
			value.MinDungeonLevel,
			value.MaxDungeonLevel,
			value.IsSkeleton,
			value.MinimumHitPoints,
			value.MaximumHitPoints,
			value.Ai,
			value.Intelligence,
			value.AbilityFlags,
			value.MinDamage,
			value.MaxDamage,
			value.MinDamageSpecial,
			value.MaxDamageSpecial,
			value.ArmorClass,
			value.Resistance,
			value.ReducePlayerStrength,
			value.ReducePlayerMagic,
			value.ReducePlayerDexterity,
			value.ReducePlayerVitality,
			value.ReducePlayerMaxHP,
			value.ReducePlayerMaxMana,
			value.HasSpecial,
			value.Width,
			value.Frames,
			value.Rates);
}

/** One fresh CMonster entry produced by Bestiary::addType. */
public sealed record AuthoritativeCathedralSelectedMonster
{
	internal AuthoritativeCathedralSelectedMonster(
		int index,
		AuthoritativeCathedralRawMonsterDefinition raw,
		int placeFlags)
	{
		Index = index;
		NativeType = raw.NativeMonsterId;
		PlaceFlags = placeFlags;
		CorpseId = 0;
		Frames = Array.AsReadOnly(raw.PopulationDefinition.Frames.ToArray());
		Rates = Array.AsReadOnly(raw.PopulationDefinition.Rates.ToArray());
		Widths = Array.AsReadOnly(raw.PopulationDefinition.Frames.Select(frame => frame == 0 ? 0 : raw.Width).ToArray());
		PopulationDefinition = raw.ToPopulationDefinition();
	}

	public int Index { get; }
	public int NativeType { get; }
	public int PlaceFlags { get; init; }
	public int CorpseId { get; }
	public IReadOnlyList<int> Frames { get; }
	public IReadOnlyList<int> Rates { get; }
	public IReadOnlyList<int> Widths { get; }
	public AuthoritativeCathedralPopulationMonsterDefinition PopulationDefinition { get; }
}

/** Detached result of the ordinary GetLevelMTypes selection. */
public sealed class AuthoritativeCathedralBestiaryResult
{
	internal AuthoritativeCathedralBestiaryResult(
		IReadOnlyList<AuthoritativeCathedralSelectedMonster> entries,
		uint finalRandomState,
		int imageCost)
	{
		Entries = Array.AsReadOnly(entries.ToArray());
		PopulationDefinitions = Array.AsReadOnly(Entries.Select(value => value.PopulationDefinition).ToArray());
		SelectedNativeTypes = Array.AsReadOnly(Entries.Select(value => value.NativeType).ToArray());
		FinalRandomState = finalRandomState;
		ImageCost = imageCost;
	}

	public IReadOnlyList<AuthoritativeCathedralSelectedMonster> Entries { get; }
	public IReadOnlyList<AuthoritativeCathedralPopulationMonsterDefinition> PopulationDefinitions { get; }
	public IReadOnlyList<int> SelectedNativeTypes { get; }
	public uint FinalRandomState { get; }
	public int ImageCost { get; }
}

/** Pure, bounded implementation of the original Cathedral GetLevelMTypes path. */
public static class AuthoritativeCathedralBestiaryGenerator
{
	public const int MaxLevelMonsterTypes = 24;
	public const int ImageBudget = 4000;
	public const int PlaceScatter = 1;
	public const int PlaceSpecial = 2;
	public const int PlaceUnique = 4;
	public const int GolemNativeType = AuthoritativeCathedralPopulationParameters.GolemNativeType;

	public static AuthoritativeCathedralBestiaryResult Generate(AuthoritativeCathedralBestiaryParameters parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		var random = new DeterministicLegacyRandom(parameters.PopulationSeed);
		var selected = new List<AuthoritativeCathedralSelectedMonster>(MaxLevelMonsterTypes);
		var byNativeType = new Dictionary<int, int>();
		var golem = parameters.Catalog[GolemNativeType];
		var imageCost = golem.Image;
		AddMonsterType(golem, PlaceSpecial, selected, byNativeType);

		var candidates = parameters.Catalog
			.Where(value => IsMonsterAvailable(value, parameters.Depth))
			.ToList();
		while (candidates.Count > 0 && selected.Count < MaxLevelMonsterTypes && imageCost < ImageBudget) {
			for (var index = 0; index < candidates.Count;) {
				if (candidates[index].Image > ImageBudget - imageCost) {
					candidates[index] = candidates[^1];
					candidates.RemoveAt(candidates.Count - 1);
					continue;
				}
				index++;
			}
			if (candidates.Count == 0)
				break;

			var candidateIndex = random.GenerateRnd(candidates.Count);
			if (candidateIndex < 0 || candidateIndex >= candidates.Count)
				throw new InvalidDataException("Native GenerateRnd produced an unrepresentable monster-list index.");
			var candidate = candidates[candidateIndex];
			candidates[candidateIndex] = candidates[^1];
			candidates.RemoveAt(candidates.Count - 1);
			if (AddMonsterType(candidate, PlaceScatter, selected, byNativeType))
				imageCost = checked(imageCost + candidate.Image);
		}

		return new AuthoritativeCathedralBestiaryResult(selected, random.State, imageCost);
	}

	private static bool IsMonsterAvailable(AuthoritativeCathedralRawMonsterDefinition monster, int depth)
		=> monster.Availability != AuthoritativeCathedralMonsterAvailability.Never
			&& depth >= monster.MinDungeonLevel
			&& depth <= monster.MaxDungeonLevel;

	private static bool AddMonsterType(
		AuthoritativeCathedralRawMonsterDefinition monster,
		int placeFlags,
		IList<AuthoritativeCathedralSelectedMonster> selected,
		IDictionary<int, int> byNativeType)
	{
		if (byNativeType.TryGetValue(monster.NativeMonsterId, out var existingIndex)) {
			var existing = selected[existingIndex];
			selected.Remove(existing);
			selected.Insert(existingIndex, existing with { PlaceFlags = existing.PlaceFlags | placeFlags });
			return false;
		}
		var index = selected.Count;
		byNativeType.Add(monster.NativeMonsterId, index);
		selected.Add(new AuthoritativeCathedralSelectedMonster(index, monster, placeFlags));
		return true;
	}
}
