using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Portable source-contract tests for ordinary Cathedral GetLevelMTypes. */
public sealed class AuthoritativeCathedralBestiaryTests
{
	[Fact]
	public void AddsGolemFirstAndStopsAtTwentyFourUniqueTypes()
	{
		var catalog = CreateCatalog(image: 100);
		catalog[109] = catalog[109] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		var result = Generate(catalog, 1, 2588);

		Assert.Equal(AuthoritativeCathedralBestiaryGenerator.MaxLevelMonsterTypes, result.Entries.Count);
		Assert.Equal(109, result.Entries[0].NativeType);
		Assert.Equal(AuthoritativeCathedralBestiaryGenerator.PlaceSpecial, result.Entries[0].PlaceFlags);
		Assert.Equal(0, result.Entries[0].Index);
		Assert.Equal(result.Entries.Count, result.Entries.Select(value => value.NativeType).Distinct().Count());
		Assert.Equal(Enumerable.Range(0, result.Entries.Count), result.Entries.Select(value => value.Index));
		Assert.InRange(result.ImageCost, 0, AuthoritativeCathedralBestiaryGenerator.ImageBudget);
	}

	[Fact]
	public void CountsGolemImageInTheFourThousandImageBudget()
	{
		var catalog = CreateCatalog(image: 250);
		catalog[109] = catalog[109] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		var result = Generate(catalog, 1, 2588);

		Assert.Equal(AuthoritativeCathedralBestiaryGenerator.ImageBudget, result.ImageCost);
		Assert.Equal(16, result.Entries.Count);
		Assert.Equal(109, result.Entries[0].NativeType);
	}

	[Fact]
	public void UsesSourceOrderForCandidatesAndNativeSwapTailPruning()
	{
		var catalog = CreateCatalog(image: 0);
		for (var index = 0; index < catalog.Count; index++)
			catalog[index] = catalog[index] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		catalog[109] = catalog[109] with { Availability = AuthoritativeCathedralMonsterAvailability.Never, Image = 100 };
		catalog[1] = catalog[1] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 3901 };
		catalog[2] = catalog[2] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 100 };
		catalog[3] = catalog[3] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 100 };

		var result = Generate(catalog, 1, 1);

		// ID 1 is removed by swapping in the tail, so seed 1 selects ID 3
		// from [3, 2], then ID 2 from the one-entry tail.
		Assert.Equal(new[] { 109, 3, 2 }, result.SelectedNativeTypes);
		// Exactly two positive-limit GenerateRnd calls, including the one-entry list.
		Assert.Equal(2_156_045_615U, result.FinalRandomState);
		Assert.Equal(300, result.ImageCost);
	}

	[Fact]
	public void RepeatsNativeSwapTailPruningBeforeRolling()
	{
		var catalog = CreateCatalog(image: 0);
		for (var index = 0; index < catalog.Count; index++)
			catalog[index] = catalog[index] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		catalog[109] = catalog[109] with { Availability = AuthoritativeCathedralMonsterAvailability.Never, Image = 100 };
		catalog[1] = catalog[1] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 3901 };
		catalog[2] = catalog[2] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 3901 };
		catalog[3] = catalog[3] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, Image = 100 };

		var result = Generate(catalog, 1, 1);

		Assert.Equal(new[] { 109, 3 }, result.SelectedNativeTypes);
		Assert.Equal(200, result.ImageCost);
	}

	[Fact]
	public void FiltersNeverAndOutOfDepthTypesWithoutChangingSourceOrder()
	{
		var catalog = CreateCatalog(image: 0);
		for (var index = 0; index < catalog.Count; index++)
			catalog[index] = catalog[index] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		catalog[1] = catalog[1] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, MinDungeonLevel = 4 };
		catalog[2] = catalog[2] with { Availability = AuthoritativeCathedralMonsterAvailability.Retail, MinDungeonLevel = 1, MaxDungeonLevel = 4 };
		catalog[3] = catalog[3] with { Availability = AuthoritativeCathedralMonsterAvailability.Always, MinDungeonLevel = 2 };

		var depth1 = Generate(catalog, 1, 7);
		var depth4 = Generate(catalog, 4, 7);

		Assert.DoesNotContain(1, depth1.SelectedNativeTypes);
		Assert.DoesNotContain(3, depth1.SelectedNativeTypes);
		Assert.Contains(2, depth1.SelectedNativeTypes);
		Assert.Contains(1, depth4.SelectedNativeTypes);
		Assert.Contains(2, depth4.SelectedNativeTypes);
		Assert.Contains(3, depth4.SelectedNativeTypes);
	}

	[Fact]
	public void AcceptsNativeLevelMetadataBeyondRuntimeCathedralDepth()
	{
		var catalog = CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget);
		catalog[1] = catalog[1] with { MinDungeonLevel = 26, MaxDungeonLevel = 26 };
		var parameters = new AuthoritativeCathedralBestiaryParameters(4, 7, catalog);

		Assert.Equal(26, parameters.Catalog[1].MaxDungeonLevel);
		Assert.DoesNotContain(1, AuthoritativeCathedralBestiaryGenerator.Generate(parameters).SelectedNativeTypes);
	}

	[Fact]
	public void ZeroBudgetConsumesNoSelectionRandomness()
	{
		var catalog = CreateCatalog(image: 1);
		catalog[109] = catalog[109] with { Image = AuthoritativeCathedralBestiaryGenerator.ImageBudget };

		var result = Generate(catalog, 1, 0xDEAD_BEEF);

		Assert.Equal(0xDEAD_BEEFU, result.FinalRandomState);
		Assert.Single(result.Entries);
		Assert.Equal(AuthoritativeCathedralBestiaryGenerator.ImageBudget, result.ImageCost);
	}

	[Fact]
	public void EmptyEligibleListConsumesNoSelectionRandomness()
	{
		var catalog = CreateCatalog(image: 1);
		for (var index = 0; index < catalog.Count; index++)
			catalog[index] = catalog[index] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };

		var result = Generate(catalog, 1, 0x1234_5678);

		Assert.Equal(0x1234_5678U, result.FinalRandomState);
		Assert.Equal(new[] { 109 }, result.SelectedNativeTypes);
	}

	[Fact]
	public void RepeatedGolemCandidateMergesPlaceFlagsWithoutAddingAType()
	{
		var catalog = CreateCatalog(image: 1);
		for (var index = 0; index < catalog.Count; index++)
			catalog[index] = catalog[index] with { Availability = AuthoritativeCathedralMonsterAvailability.Never };
		catalog[109] = catalog[109] with { Availability = AuthoritativeCathedralMonsterAvailability.Always };

		var result = Generate(catalog, 1, 7);

		Assert.Equal(new[] { 109 }, result.SelectedNativeTypes);
		Assert.Equal(AuthoritativeCathedralBestiaryGenerator.PlaceSpecial | AuthoritativeCathedralBestiaryGenerator.PlaceScatter, result.Entries[0].PlaceFlags);
		Assert.NotEqual(7U, result.FinalRandomState);
	}

	[Fact]
	public void NegativeNativeGenerateRndIsRejectedInsteadOfClamped()
	{
		// 0x015A4E35 * 1457187811 + 1 == 0x80000000, which is the
		// native AdvanceRndSeed INT_MIN edge before GenerateRnd's shift/mod.
		Assert.Throws<InvalidDataException>(() => Generate(CreateCatalog(image: 1), 1, 1_457_187_811U));
	}

	[Fact]
	public void PreservesFreshAnimationArraysAndZeroesDisabledSixthSlot()
	{
		var catalog = CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget);
		catalog[109] = CreateRaw(
			109,
			image: AuthoritativeCathedralBestiaryGenerator.ImageBudget,
			hasSpecial: true,
			width: 96,
			frames: [8, 7, 6, 5, 4, 3],
			rates: [4, 3, 2, 1, 5, 6]);

		var result = Generate(catalog, 1, 1);
		var golem = Assert.Single(result.Entries);

		Assert.Equal(new[] { 8, 7, 6, 5, 4, 3 }, golem.Frames);
		Assert.Equal(new[] { 4, 3, 2, 1, 5, 6 }, golem.Rates);
		Assert.Equal(new[] { 96, 96, 96, 96, 96, 96 }, golem.Widths);
		Assert.Equal(0, golem.CorpseId);
		Assert.False(golem.PopulationDefinition.IsSkeleton);

		var noSpecial = Generate(CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget), 1, 1).Entries[0];
		Assert.Equal(0, noSpecial.Frames[5]);
		Assert.Equal(0, noSpecial.Rates[5]);
		Assert.Equal(0, noSpecial.Widths[5]);

		var rawUnusedSixthSlot = CreateRaw(
			109,
			image: AuthoritativeCathedralBestiaryGenerator.ImageBudget,
			frames: [8, 7, 6, 5, 4, 9],
			rates: [4, 3, 2, 1, 5, 9]);
		Assert.Equal(9, rawUnusedSixthSlot.Frames[5]);
		Assert.Equal(9, rawUnusedSixthSlot.Rates[5]);
		var rawCatalog = CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget);
		rawCatalog[109] = rawUnusedSixthSlot;
		var rawParameters = new AuthoritativeCathedralBestiaryParameters(1, 1, rawCatalog);
		Assert.Equal(9, rawParameters.Catalog[109].Frames[5]);
		Assert.Equal(9, rawParameters.Catalog[109].Rates[5]);
		var selectedUnusedSixthSlot = AuthoritativeCathedralBestiaryGenerator.Generate(rawParameters).Entries[0];
		Assert.Equal(0, selectedUnusedSixthSlot.Frames[5]);
		Assert.Equal(0, selectedUnusedSixthSlot.Rates[5]);
		Assert.Equal(0, selectedUnusedSixthSlot.Widths[5]);
	}

	[Fact]
	public void CopiesOnlyWidthAndRateForNativeAnimationFramesThatExist()
	{
		var catalog = CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget);
		catalog[109] = CreateRaw(
			109,
			image: AuthoritativeCathedralBestiaryGenerator.ImageBudget,
			hasSpecial: true,
			width: 96,
			frames: [8, 0, 6, 0, 4, 3],
			rates: [4, 9, 2, 9, 5, 6]);

		var golem = Assert.Single(Generate(catalog, 1, 1).Entries);

		Assert.Equal(new[] { 8, 0, 6, 0, 4, 3 }, golem.Frames);
		Assert.Equal(new[] { 4, 0, 2, 0, 5, 6 }, golem.Rates);
		Assert.Equal(new[] { 96, 0, 96, 0, 96, 96 }, golem.Widths);
	}

	[Fact]
	public void CarriesPopulationDefinitionFieldsIntoTheSelectedType()
	{
		var catalog = CreateCatalog(image: AuthoritativeCathedralBestiaryGenerator.ImageBudget);
		catalog[109] = CreateRaw(
			109,
			image: AuthoritativeCathedralBestiaryGenerator.ImageBudget,
			minimumHitPoints: 17,
			maximumHitPoints: 31,
			ai: 12,
			intelligence: 7,
			abilityFlags: 0x1234,
			width: 88);

		var selected = Assert.Single(Generate(catalog, 1, 1).Entries).PopulationDefinition;

		Assert.Equal(109, selected.NativeType);
		Assert.Equal(17, selected.MinimumHitPoints);
		Assert.Equal(31, selected.MaximumHitPoints);
		Assert.Equal(12, selected.Ai);
		Assert.Equal(7, selected.Intelligence);
		Assert.Equal(0x1234U, selected.AbilityFlags);
		Assert.Equal(88, catalog[109].Width);
	}

	[Fact]
	public void RawCatalogAndResultsDetachMutableCollections()
	{
		var frames = new List<int> { 8, 7, 6, 5, 4, 0 };
		var rates = new List<int> { 4, 3, 2, 1, 5, 0 };
		var raw = CreateRaw(109, frames: frames, rates: rates);
		frames[0] = 0;
		rates[0] = 0;
		Assert.Equal(8, raw.Frames[0]);
		Assert.Equal(4, raw.Rates[0]);

		var catalog = CreateCatalog(image: 100);
		var parameters = new AuthoritativeCathedralBestiaryParameters(1, 1, catalog);
		catalog[0] = CreateRaw(0, image: 999);
		Assert.Equal(0, parameters.Catalog[0].NativeMonsterId);
		Assert.Equal(100, parameters.Catalog[0].Image);

		var result = AuthoritativeCathedralBestiaryGenerator.Generate(parameters);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)result.SelectedNativeTypes)[0] = 4);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)result.Entries[0].Frames)[0] = 4);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)parameters.Catalog[0].PopulationDefinition.Frames)[0] = 4);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)result.Entries[0].PopulationDefinition.Frames)[0] = 4);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)result.PopulationDefinitions[0].Rates)[0] = 4);
	}

	[Fact]
	public void RejectsMalformedCatalogSizesIdsDepthsAndAnimationMetadata()
	{
		var catalog = CreateCatalog(image: 1);
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralBestiaryParameters(0, 1, catalog));
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralBestiaryParameters(1, 1, catalog.Take(137).ToArray()));

		var reordered = catalog.ToArray();
		(reordered[0], reordered[1]) = (reordered[1], reordered[0]);
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralBestiaryParameters(1, 1, reordered));
		Assert.Throws<InvalidDataException>(() => CreateRaw(138));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, availability: (AuthoritativeCathedralMonsterAvailability)99));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, image: ushort.MaxValue + 1));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, minimumHitPoints: ushort.MaxValue, maximumHitPoints: 0));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, ai: 40));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, abilityFlags: ushort.MaxValue + 1U));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, minDungeonLevel: 128));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, maxDungeonLevel: 128));
		Assert.Throws<InvalidDataException>(() => CreateRaw(8, isSkeleton: false));
		Assert.Throws<InvalidDataException>(() => CreateRaw(1, frames: [8, 7, 6, 5, 4]));
		var invalidAfterConstruction = catalog.ToArray();
		invalidAfterConstruction[1] = invalidAfterConstruction[1] with { NativeMonsterId = 200 };
		Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralBestiaryParameters(1, 1, invalidAfterConstruction));
	}

	[Fact]
	public void SupportsExactly138RawTypesButKeepsThe200MonsterRuntimeCapacitySeparate()
	{
		var catalog = CreateCatalog(image: 0);
		var parameters = new AuthoritativeCathedralBestiaryParameters(4, 1, catalog);
		var result = AuthoritativeCathedralBestiaryGenerator.Generate(parameters);

		Assert.Equal(138, parameters.Catalog.Count);
		Assert.Equal(200, AuthoritativeCathedralPopulationParameters.MonsterCapacity);
		Assert.All(result.Entries, value => Assert.InRange(value.NativeType, 0, 137));
	}

	private static AuthoritativeCathedralBestiaryResult Generate(
		IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> catalog,
		int depth,
		uint seed)
		=> AuthoritativeCathedralBestiaryGenerator.Generate(
			new AuthoritativeCathedralBestiaryParameters(depth, seed, catalog));

	private static List<AuthoritativeCathedralRawMonsterDefinition> CreateCatalog(int image)
		=> Enumerable.Range(0, AuthoritativeCathedralRawMonsterDefinition.RawCatalogCount)
			.Select(index => CreateRaw(index, image: image))
			.ToList();

	private static AuthoritativeCathedralRawMonsterDefinition CreateRaw(
		int nativeMonsterId,
		AuthoritativeCathedralMonsterAvailability availability = AuthoritativeCathedralMonsterAvailability.Always,
		int image = 100,
		int minDungeonLevel = 1,
		int maxDungeonLevel = 4,
		bool? isSkeleton = null,
		int minimumHitPoints = 4,
		int maximumHitPoints = 9,
		int ai = 1,
		int intelligence = 1,
		uint abilityFlags = 0,
		int width = 96,
		bool hasSpecial = false,
		IReadOnlyList<int>? frames = null,
		IReadOnlyList<int>? rates = null)
		=> new(
			nativeMonsterId,
			availability,
			image,
			minDungeonLevel,
			maxDungeonLevel,
			isSkeleton ?? IsSkeleton(nativeMonsterId),
			minimumHitPoints,
			maximumHitPoints,
			ai,
			intelligence,
			abilityFlags,
			2,
			4,
			2,
			6,
			2,
			1,
			1,
			2,
			3,
			4,
			5,
			6,
			hasSpecial,
			width,
			frames ?? (hasSpecial ? [8, 7, 6, 5, 4, 3] : [8, 7, 6, 5, 4, 0]),
			rates ?? (hasSpecial ? [4, 3, 2, 1, 5, 6] : [4, 3, 2, 1, 5, 0]));

	private static bool IsSkeleton(int nativeMonsterId)
		=> nativeMonsterId is 8 or 9 or 10 or 11 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27;
}
